using StreamAssist.Components.OBS.Model;
using StreamAssist.Components.Spotify.Actions;
using StreamAssist.Components.Spotify.Background;
using StreamAssist.Components.Spotify.Model;
using StreamAssist.Components.Spotify.Values;
using StreamAssist.Components.Data.Model;
using StreamAssist.Components.Data.Background;
using StreamAssist.Components.Data.Actions;
using StreamAssist.Components.Data.Containers;
using StreamAssist.Components.Data.Values;
using System.Net;
using StreamAssist.Components.OBS.Background;
using StreamAssist.Components.OBS.Conditions;
using StreamAssist.Components.OBS.Values;
using StreamAssist.Components.OBS.Actions;
using StreamAssist.Components.OutputConsole.Actions;
using StreamAssist.Components.Input.Conditions;
using StreamAssist.Components.Audio.Actions;
using StreamAssist.Core.Connections;
using StreamAssist.Components.Chatbot.Values;
using StreamAssist.Components.Chatbot.Actions;
using StreamAssist.Components.Blocks.Values;
using StreamAssist.Components.Blocks.Actions;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Components.Chatbot.Background;
using StreamAssist.Components.Chatbot.ChatBlocks;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Components.Blocks.Conditions;
using StreamAssist.Components.Input.Background;
using StreamAssist.Components.Input.Model;
using StreamAssist.Components.Input.UI;
using System.Text.Json;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Blocks.Containers;
using StreamAssist.Components.Blocks.UI;

namespace StreamAssist.Tests;



// engine checks: OBS and Spotify (against pretend servers), Voicemeeter, Twitch info blocks without a connection, Kokoro voices
internal static partial class EngineChecks
{
    // ---- OBS, against a pretend OBS (main thread, blocking instead of awaiting, so console errors can be counted) ----
    private static void Obs()
    {
        int Errors() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Count(l => l.Contains("] error - ")); }
        string LastError() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Last(l => l.Contains("] error - ")); }
        string LastWarning() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.LastOrDefault(l => l.Contains("] warning - ")) ?? ""; }
        T Wait<T>(Task<T> task) => task.GetAwaiter().GetResult();
        bool Until(Func<bool> done)
        {
            for (int i = 0; i < 100; i++)
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                if (done()) return true;
                Thread.Sleep(30);
            }
            return false;
        }

        using var fake = new FakeObsServer();
        var obsModel = new ObsComponentModel();
        var obs = new ObsManager(obsModel);

        // connecting
        Check("obs: off until asked", !obs.IsConnected && obs.Status == ConnectionStatus.NotConnected);
        var e0 = Errors();
        obsModel.SetConnectionDetails(fake.Address, "wrong");
        obsModel.SetIsEnabled(true);
        Check("obs: wrong password fails with one error", Until(() => obs.Status == ConnectionStatus.Failed) && !obs.IsConnected
            && Errors() == e0 + 1 && LastError().Contains("error - can't connect to obs (authentication failed)"));
        obsModel.SetConnectionDetails(fake.Address, FakeObsServer.Password);
        Check("obs: the right password connects", Until(() => obs.IsConnected && obs.Status == ConnectionStatus.Connected) && obs.StatusMessage == $"Connected to {fake.Address}");
        var nowhereModel = new ObsComponentModel();
        nowhereModel.SetConnectionDetails("127.0.0.1:1", "");
        var nowhere = new ObsManager(nowhereModel);
        var e1 = Errors();
        nowhereModel.SetIsEnabled(true);
        Check("obs: nothing listening fails with one error", Until(() => nowhere.Status == ConnectionStatus.Failed) && Errors() == e1 + 1 && LastError().Contains("nothing at that address"));
        nowhere.Dispose();
        Check("obs: an empty address means the default", new Func<bool>(() => { var m = new ObsComponentModel(); m.SetConnectionDetails("  ", "x"); return m.Address == ObsComponentModel.DefaultAddress; })());

        // reading
        Check("obs: live scene", Wait(obs.TryGetCurrentSceneAsync()) == (true, "Gaming"));
        Check("obs: scenes top to bottom", Wait(obs.TryGetScenesAsync()).Value!.SequenceEqual(new[] { "Gaming", "Just Chatting" }));
        Check("obs: sources of the live scene, top to bottom", Wait(obs.TryGetSourcesAsync("")).Value!.SequenceEqual(new[] { "Cam", "Title" }));
        Check("obs: sources of a named scene", Wait(obs.TryGetSourcesAsync("Just Chatting")).Value!.SequenceEqual(new[] { "Cam" }));

        // the blocks
        StreamAssist.Shell.GlobalContext.ObsManager = obs;
        var ctx = new TriggerContext(TriggerPhase.Once, default, new Dictionary<string, string> { ["user"] = "bob" });
        T Block<T>(T block, params string[] slotTexts) where T : BlockModel
        {
            for (int i = 0; i < slotTexts.Length; i++) ((ValueSlot)block.Slots.Where(s => s is not MenuSlot).ElementAt(i)).SetText(slotTexts[i]);
            return block;
        }
        bool RunObs(ExecutableBlock block) { RecordBlock.Log.Clear(); var list = new ListBlock(); var slot = (BlockSlotList)list.Slots[0]; slot.TryInsertBlock(block, 0); slot.TryInsertBlock(new RecordBlock { Tag = "after" }, 1); list.SafeRunAsync(ctx).GetAwaiter().GetResult(); return RecordBlock.Log.Count == 1; }
        object Eval(ValueBlock block) => block.EvaluateObjectAsync(ctx).GetAwaiter().GetResult();

        Check("obs blocks: get scene / scenes / sources", Eval(new GetObsSceneBlock()) is "Gaming" && Eval(new GetObsScenesBlock()) is "Gaming, Just Chatting" && Eval(Block(new GetObsSourcesBlock(), "")) is "Cam, Title");
        Check("obs blocks: hide a source in the live scene", RunObs(Block(new SetObsSourceVisibleAction().Pick("Change", "Hide"), "", "Title")) && !fake.Scenes["Gaming"][1].Enabled);
        Check("obs blocks: visible condition", Eval(Block(new ObsSourceVisibleCondition(), "", "Title")) is false && Eval(Block(new ObsSourceVisibleCondition(), "Gaming", "Cam")) is true);
        Check("obs blocks: toggle shows it again", RunObs(Block(new SetObsSourceVisibleAction(), "Gaming", "Title")) && fake.Scenes["Gaming"][1].Enabled);
        Check("obs blocks: volume in dB", RunObs(Block(new SetObsVolumeAction(), "Mic", "-20")) && fake.Inputs["Mic"].VolumeDb == -20 && Eval(Block(new GetObsVolumeBlock(), "Mic")) is -20.0);
        Check("obs blocks: volume is kept in OBS's range", RunObs(Block(new SetObsVolumeAction(), "Mic", "40")) && fake.Inputs["Mic"].VolumeDb == 26);
        Check("obs blocks: mute toggles, muted condition", RunObs(Block(new SetObsMuteAction(), "Mic")) && fake.Inputs["Mic"].Muted && Eval(Block(new ObsSourceMutedCondition(), "Mic")) is true);
        Check("obs blocks: unmute", RunObs(Block(new SetObsMuteAction().Pick("Change", "Unmute"), "Mic")) && !fake.Inputs["Mic"].Muted);
        Check("obs blocks: text fills in parameters", RunObs(Block(new SetObsTextAction(), "Title", "{user} raided")) && fake.Inputs["Title"].Settings["text"]!.GetValue<string>() == "bob raided");
        Check("obs blocks: move a source", RunObs(Block(new SetObsTransformAction().Pick("Setting", "Position X"), "", "Title", "100")) && fake.Scenes["Gaming"][1].Transform["positionX"]!.GetValue<double>() == 100
            && Eval(Block(new GetObsTransformBlock().Pick("Setting", "Position X"), "", "Title")) is 100.0);
        Check("obs blocks: width resizes through the scale", RunObs(Block(new SetObsTransformAction().Pick("Setting", "Width"), "", "Title", "800")) && fake.Scenes["Gaming"][1].Transform["scaleX"]!.GetValue<double>() == 2);
        Check("obs blocks: a never changed setting reads its default", Eval(Block(new GetObsSourceSettingBlock { As = "Int" }, "Title", "opacity")) is 100);
        Check("obs blocks: a typed setting keeps the setting's kind", RunObs(Block(new SetObsSourceSettingAction(), "Title", "opacity", "50")) && fake.Inputs["Title"].Settings["opacity"]!.GetValueKind() == System.Text.Json.JsonValueKind.Number
            && RunObs(Block(new SetObsSourceSettingAction(), "Title", "vertical", "true")) && fake.Inputs["Title"].Settings["vertical"]!.GetValueKind() == System.Text.Json.JsonValueKind.True);
        Check("obs blocks: a new text setting stays text", RunObs(Block(new SetObsSourceSettingAction(), "Title", "font_face", "Arial")) && fake.Inputs["Title"].Settings["font_face"]!.GetValue<string>() == "Arial");
        Check("obs blocks: get a setting as text or a number", Eval(Block(new GetObsSourceSettingBlock(), "Title", "text")) is "bob raided" && Eval(Block(new GetObsSourceSettingBlock { As = "Int" }, "Title", "opacity")) is 50);
        Check("obs blocks: switch scene", RunObs(Block(new SetObsSceneAction(), "Just Chatting")) && fake.CurrentScene == "Just Chatting");
        // resizing keeps the chosen point still (Title is top-left aligned, in Gaming)
        (double Right, double Bottom, double Width) Corner()
        {
            var t = fake.Scenes["Gaming"][1].Transform;
            return (t["positionX"]!.GetValue<double>() + t["width"]!.GetValue<double>(), t["positionY"]!.GetValue<double>() + t["height"]!.GetValue<double>(), t["width"]!.GetValue<double>());
        }
        var before = Corner();
        var grow = Block(new ResizeObsSourceAction(), "Gaming", "Title", "50");
        var shrink = Block(new ResizeObsSourceAction(), "Gaming", "Title", "-50");
        var grown = RunObs(grow) ? Corner() : default;
        var shrunk = RunObs(shrink) ? Corner() : default;
        var set = Block(new ResizeObsSourceAction().Pick("Mode", "Set size to"), "Gaming", "Title", "", "100", "100");
        var setTo = RunObs(set) ? Corner() : default;
        bool Near(double a, double b) => Math.Abs(a - b) < 0.001;
        Check("obs resize: grows and shrinks by a percent, the bottom-right corner stays put", Near(grown.Width, before.Width * 1.5) && Near(grown.Right, before.Right) && Near(grown.Bottom, before.Bottom)
            && Near(shrunk.Width, before.Width * 0.75) && Near(shrunk.Right, before.Right) && Near(shrunk.Bottom, before.Bottom));
        Check("obs resize: set to a size, same corner", Near(setTo.Width, 100) && Near(setTo.Right, before.Right) && Near(setTo.Bottom, before.Bottom));
        Check("obs blocks: filters on, off and toggled", RunObs(Block(new SetObsFilterAction().Pick("Change", "Off"), "Title", "Glow")) && fake.Filters["Title/Glow"] == false
            && RunObs(Block(new SetObsFilterAction(), "Title", "Glow")) && fake.Filters["Title/Glow"]);
        fake.Streaming = true; fake.StreamMs = 7_200_000; fake.Recording = false;
        var streamTime = new GetObsOutputBlock().Pick("Setting", "Time");
        var recordTime = new GetObsOutputBlock().Pick("Output", "Recording").Pick("Setting", "Time");
        Check("obs output: streaming's time as h:mm:ss, an output that's off gives empty, Active is true/false", Eval(streamTime) is "2:00:00" && Eval(recordTime) is ""
            && Eval(new GetObsOutputBlock()) is true && Eval(new GetObsOutputBlock().Pick("Output", "Recording")) is false && streamTime.Kind == ValueKind.Text);
        fake.Recording = true; fake.RecordMs = 3_723_500;
        Check("obs output: recording's time", Eval(recordTime) is "1:02:03");
        Check("connected conditions: OBS while connected, Spotify and Twitch aren't (not set up here)",
            Eval(new StreamAssist.Components.OBS.Conditions.ObsConnectedCondition()) is true
            && Eval(new StreamAssist.Components.Spotify.Conditions.SpotifyConnectedCondition()) is false
            && Eval(new StreamAssist.Components.Chatbot.Conditions.TwitchConnectedCondition().Pick("Account", "Bot")) is false);
        fake.Streaming = false; fake.Recording = false;
        Check("obs blocks: recording started, started again silently, toggled, a replay saved", RunObs(new SetObsOutputAction().Pick("Change", "Start")) && fake.Recording
            && RunObs(new SetObsOutputAction().Pick("Change", "Start")) && fake.Recording && RunObs(new SetObsOutputAction()) && !fake.Recording
            && RunObs(new SaveObsReplayAction()) && fake.ReplaysSaved == 1);


        // failures: one error with OBS's reason, and the run stops
        var e2 = Errors();
        Check("obs blocks: a missing source fails with OBS's reason and stops", !RunObs(Block(new SetObsSourceVisibleAction(), "", "Nope")) && Errors() == e2 + 1
            && LastError().Contains("error - can't show/hide 'Nope' (No source was found by the name of `Nope`.)"));

        // obs going away: one warning, failed, and requests say obs isn't connected
        fake.Drop();
        Check("obs: closing says so once", Until(() => !obs.IsConnected && obs.Status == ConnectionStatus.Failed) && LastWarning().Contains("warning - obs disconnected (obs is shutting down)"));
        var e3 = Errors();
        Check("obs: not connected fails with one error", !RunObs(Block(new SetObsSceneAction(), "Gaming")) && Errors() == e3 + 1 && LastError().Contains("error - can't switch to scene 'Gaming', obs not connected"));

        StreamAssist.Shell.GlobalContext.ObsManager = null;
        obs.Dispose();
    }



    // ---- Spotify, against a pretend Spotify (blocking like OBS's, so console errors can be counted) ----
    private static void Spotify()
    {
        int Errors() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Count(l => l.Contains("] error - ")); }
        string LastError() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Last(l => l.Contains("] error - ")); }
        string LastWarning() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.LastOrDefault(l => l.Contains("] warning - ")) ?? ""; }
        int Prompts() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Count(l => l.Contains("] prompt - ")); }
        string LastPrompt() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.LastOrDefault(l => l.Contains("] prompt - ")) ?? ""; }
        T Wait<T>(Task<T> task) => task.GetAwaiter().GetResult();
        bool Until(Func<bool> done)
        {
            for (int i = 0; i < 100; i++)
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                if (done()) return true;
                Thread.Sleep(30);
            }
            return false;
        }

        // links
        Check("spotify: a share link is a song", SpotifyManager.TryParseLink("https://open.spotify.com/track/4cOdK2wGLETKBW3PvgPWqT?si=abc", out var link1) && link1 == "spotify:track:4cOdK2wGLETKBW3PvgPWqT");
        Check("spotify: spotify: links and other countries' links", SpotifyManager.TryParseLink("spotify:playlist:37i9dQZF1DX", out var link2) && link2 == "spotify:playlist:37i9dQZF1DX"
            && SpotifyManager.TryParseLink(" open.spotify.com/intl-de/album/abc123 ", out var link3) && link3 == "spotify:album:abc123");
        Check("spotify: words and other sites aren't links", !SpotifyManager.TryParseLink("never gonna give you up", out _) && !SpotifyManager.TryParseLink("https://example.com/track/abc", out _));

        using var fake = new FakeSpotifyServer();
        var store = new MemorySpotifyLoginStore();
        var spotifyModel = new SpotifyComponentModel();
        var spotify = new SpotifyManager(spotifyModel, store, TimeSpan.FromMilliseconds(50)); // checks the login often

        // connecting: no client id, then the first login through the browser
        Check("spotify: off until asked", !spotify.IsConnected && spotify.Status == ConnectionStatus.NotConnected);
        var s0 = Errors();
        spotifyModel.SetIsEnabled(true);
        Check("spotify: no client id fails with one error", Until(() => spotify.Status == ConnectionStatus.Failed) && Errors() == s0 + 1 && LastError().Contains("error - can't connect to spotify, no client id"));
        var p0 = Prompts();
        spotifyModel.SetClientId(" my-app ");
        Check("spotify: the first time it shows a login link", Until(() => Prompts() == p0 + 1) && LastPrompt().Contains("prompt - spotify login, open: http://localhost:")
            && LastPrompt().Contains("client_id=my-app") && LastPrompt().Contains("redirect_uri=http%3A%2F%2F127.0.0.1%3A8974%2Fcallback")
            && LastPrompt().Contains("code_challenge_method=S256") && LastPrompt().Contains("playlist-modify-private") && spotify.StatusMessage == "Waiting for login");
        var state = System.Text.RegularExpressions.Regex.Match(LastPrompt(), "state=([0-9a-f]+)").Groups[1].Value;
        using (var browser = new HttpClient())
        {
            var favicon = Wait(browser.GetAsync("http://127.0.0.1:8974/favicon.ico"));
            var page = Wait(browser.GetStringAsync($"http://127.0.0.1:8974/callback?code=the-code&state={state}"));
            Check("spotify: the browser coming back logs in", favicon.StatusCode == HttpStatusCode.NotFound && page.Contains("logged in to Spotify")
                && Until(() => spotify.Status == ConnectionStatus.Connected) && spotify.StatusMessage == "Connected to Budder");
        }
        Check("spotify: the login is kept with the app that made it", store.Login is { ClientId: "my-app", RefreshToken: FakeSpotifyServer.GoodRefreshToken } && store.Login.AccessToken == fake.Token);
        spotifyModel.SetIsEnabled(false);
        Check("spotify: disconnecting keeps the login", Until(() => spotify.Status == ConnectionStatus.NotConnected) && !spotify.IsConnected && store.Login.RefreshToken != "");
        var p1 = Prompts();
        spotifyModel.SetIsEnabled(true);
        Check("spotify: connecting again uses the saved login", Until(() => spotify.Status == ConnectionStatus.Connected) && Prompts() == p1);

        // the blocks
        StreamAssist.Shell.GlobalContext.SpotifyManager = spotify;
        var sctx = new TriggerContext(TriggerPhase.Once, default, new Dictionary<string, string> { ["input"] = "sandstorm" });
        T Block<T>(T block, params string[] slotTexts) where T : BlockModel
        {
            for (int i = 0; i < slotTexts.Length; i++) ((ValueSlot)block.Slots.Where(s => s is not MenuSlot).ElementAt(i)).SetText(slotTexts[i]);
            return block;
        }
        bool RunSpotify(ExecutableBlock block) { RecordBlock.Log.Clear(); var list = new ListBlock(); var slot = (BlockSlotList)list.Slots[0]; slot.TryInsertBlock(block, 0); slot.TryInsertBlock(new RecordBlock { Tag = "after" }, 1); list.SafeRunAsync(sctx).GetAwaiter().GetResult(); return RecordBlock.Log.Count == 1; }
        object Eval(ValueBlock block) => block.EvaluateObjectAsync(sctx).GetAwaiter().GetResult();
        int Sent(string start) { lock (fake.Requests) return fake.Requests.Count(r => r.StartsWith(start)); }

        Check("spotify blocks: nothing playing reads as not playing", Eval(new GetSpotifyPlaybackBlock().Pick("Setting", "Playing")) is false && Eval(new GetSpotifyPlaybackBlock()) is "");
        Check("spotify blocks: play a searched song on the computer when nothing is playing", RunSpotify(Block(new PlaySpotifyAction(), "rick astley"))
            && Sent("PUT me/player/play?device_id=pc {\"uris\":[\"spotify:track:never\"]}") == 1 && fake.Song!["name"]!.GetValue<string>() == "Never Gonna Give You Up");
        Check("spotify blocks: play a link where it's playing", RunSpotify(Block(new PlaySpotifyAction(), "https://open.spotify.com/track/sand?si=1"))
            && Sent("PUT me/player/play {\"uris\":[\"spotify:track:sand\"]}") == 1);
        Check("spotify blocks: play one of your playlists by name", RunSpotify(Block(new PlaySpotifyAction().Pick("Find", "Playlist"), " stream LIKES "))
            && Sent("PUT me/player/play {\"context_uri\":\"spotify:playlist:likes\"}") == 1);
        var s1 = Errors();
        Check("spotify blocks: a playlist you don't have fails once and stops", !RunSpotify(Block(new PlaySpotifyAction().Pick("Find", "Playlist"), "Nope"))
            && Errors() == s1 + 1 && LastError().Contains("error - can't play 'Nope' (you have no playlist by that name)"));
        Check("spotify blocks: a search with no song fails", !RunSpotify(Block(new QueueSpotifySongAction(), "asdfgh")) && LastError().Contains("error - can't queue 'asdfgh' (no song found)"));
        Check("spotify blocks: queue a redeem's text", RunSpotify(Block(new QueueSpotifySongAction(), "{input}")) && fake.Queue.SequenceEqual(new[] { "spotify:track:sand" }));

        Check("spotify blocks: what's playing", Eval(new GetSpotifyPlaybackBlock()) is "Sandstorm" && Eval(new GetSpotifyPlaybackBlock().Pick("Setting", "Artist")) is "Darude, Someone"
            && Eval(new GetSpotifyPlaybackBlock().Pick("Setting", "Playing")) is true && Eval(new GetSpotifyPlaybackBlock().Pick("Setting", "Progress Seconds")) is 61
            && Eval(new GetSpotifyPlaybackBlock().Pick("Setting", "Length Seconds")) is 225 && Eval(new GetSpotifyPlaybackBlock().Pick("Setting", "Link")) is "https://open.spotify.com/track/sand");
        Check("spotify blocks: pause", RunSpotify(new SetSpotifyPlaybackAction().Pick("Change", "Pause")) && !fake.Playing);
        Check("spotify blocks: pausing while paused asks nothing", RunSpotify(new SetSpotifyPlaybackAction().Pick("Change", "Pause")) && Sent("PUT me/player/pause") == 1);
        Check("spotify blocks: toggle resumes", RunSpotify(new SetSpotifyPlaybackAction()) && fake.Playing && Sent("PUT me/player/play {}") + Sent("PUT me/player/play") >= 1);
        Check("spotify blocks: next", RunSpotify(new SetSpotifyPlaybackAction().Pick("Change", "Next")) && Sent("POST me/player/next") == 1);
        Check("spotify blocks: volume is kept to 0-100", RunSpotify(Block(new SetSpotifyVolumeAction(), "150")) && fake.Volume == 100 && Eval(new GetSpotifyPlaybackBlock().Pick("Setting", "Volume")) is 100);
        Check("spotify blocks: shuffle toggles, on while on asks nothing", RunSpotify(new SetSpotifyShuffleAction()) && fake.Shuffle
            && RunSpotify(new SetSpotifyShuffleAction().Pick("Change", "On")) && Sent("PUT me/player/shuffle") == 1);
        Check("spotify blocks: repeat the song", RunSpotify(new SetSpotifyRepeatAction().Pick("Repeat", "Song")) && fake.Repeat == "track" && Eval(new GetSpotifyPlaybackBlock().Pick("Setting", "Repeat")) is "Song");
        Check("spotify blocks: add the song playing to a playlist", RunSpotify(Block(new AddToSpotifyPlaylistAction(), "Stream Likes", "")) && fake.Playlist.SequenceEqual(new[] { "spotify:track:never", "spotify:track:sand" }));
        Check("spotify blocks: a song already in the playlist isn't added again", RunSpotify(Block(new AddToSpotifyPlaylistAction(), "spotify:playlist:likes", "rick")) && fake.Playlist.Count == 2);
        Check("spotify blocks: find a song", Eval(Block(new FindSpotifySongBlock().Pick("Setting", "Length Seconds"), "rick astley")) is 213
            && Eval(Block(new FindSpotifySongBlock(), "spotify:track:sand")) is "Sandstorm");
        var savedPlay = (PlaySpotifyAction)BlockResolver.DeepCopy(Block(new PlaySpotifyAction().Pick("Find", "Album"), "{rest}"))!;
        Check("spotify blocks: saved and loaded keeps its settings", savedPlay.Picked("Find") == "Album" && ((ValueSlot)savedPlay.Slots[1]).Text == "{rest}"
            && ((GetSpotifyPlaybackBlock)BlockResolver.DeepCopy(new GetSpotifyPlaybackBlock().Pick("Setting", "Shuffle"))!).Kind == ValueKind.Bool);

        // logins: a request never refreshes or retries, a refused token disconnects (the login is kept for Retry)
        fake.Token = "rotated";
        var s2 = Errors();
        var refreshes = fake.Refreshes;
        var volumeBefore = fake.Volume;
        Check("spotify: a refused token fails the request, once disconnected, no refresh", !RunSpotify(Block(new SetSpotifyVolumeAction(), "30")) && fake.Volume == volumeBefore
            && Until(() => spotify.Status == ConnectionStatus.Failed) && spotify.StatusMessage == "Disconnected" && !spotify.IsConnected && fake.Refreshes == refreshes
            && LastWarning().Contains("warning - spotify disconnected (login refused)") && Errors() == s2 + 1 && LastError().Contains("error - can't set spotify volume (login refused)"));
        var warnings = AppLog.Lines.Count(l => l.Contains("] warning - "));
        Check("spotify: later requests just say it isn't connected", !RunSpotify(Block(new SetSpotifyVolumeAction(), "30")) && Errors() == s2 + 2
            && LastError().Contains("error - can't set spotify volume, spotify not connected") && AppLog.Lines.Count(l => l.Contains("] warning - ")) == warnings);
        spotifyModel.Reconnect();
        Check("spotify: retry uses the saved login again", Until(() => spotify.Status == ConnectionStatus.Connected) && store.Login.RefreshToken == FakeSpotifyServer.GoodRefreshToken
            && RunSpotify(Block(new SetSpotifyVolumeAction(), "30")) && fake.Volume == 30);

        // the background check refreshes a login with under 15 minutes left, a refused refresh token is a lost login
        fake.ExpiresIn = 600;
        spotifyModel.Reconnect();
        Check("spotify: the background check refreshes a login that's running out", Until(() => spotify.Status == ConnectionStatus.Connected)
            && Until(() => fake.Refreshes >= refreshes + 3) && store.Login.AccessToken == fake.Token);
        fake.RefuseRefresh = true;
        Check("spotify: a lost login says so once", Until(() => spotify.Status == ConnectionStatus.Failed) && spotify.StatusMessage == "Login expired"
            && LastWarning().Contains("warning - spotify login expired, press retry") && store.Login.RefreshToken == "" && !spotify.IsConnected);
        fake.ExpiresIn = 3600;

        StreamAssist.Shell.GlobalContext.SpotifyManager = null;
        spotify.Dispose();
    }



    // ---- Voicemeeter channels and Twitch info blocks (no Voicemeeter or Twitch here: they say so and stop) ----
    private static void VoicemeeterAndTwitchInfo()
    {
        int Errors() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Count(l => l.Contains("] error - ")); }
        string LastError() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Last(l => l.Contains("] error - ")); }
        string Names(int edition) => string.Join(", ", StreamAssist.Components.Audio.Background.VoicemeeterChannels.For(edition).Select(c => $"{c.Name}={c.Prefix}"));

        // volumes are value slots (typed or a block), saved like any other
        var sound = new StreamAssist.Components.Audio.Actions.PlaySoundAction();
        ((ValueSlot)sound.Slots[1]).SetText("0.25");
        var soundJson = JsonSerializer.Serialize(sound);
        var muteJson = JsonSerializer.Serialize(new StreamAssist.Components.Audio.Actions.MuteVoicemeeterAction());
        Check("volumes: saved as value slots, 1 by default", soundJson.Contains("\"volume\":{\"text\":\"0.25\"") && muteJson.Contains("\"soundVolume\":{\"text\":\"1\""));

        // voicemeeter's channels, as each edition's window names them
        Check("voicemeeter: banana's channels", Names(2) == "Stereo Input 1=Strip[0], Stereo Input 2=Strip[1], Stereo Input 3=Strip[2], Voicemeeter Input=Strip[3], Voicemeeter AUX=Strip[4], A1=Bus[0], A2=Bus[1], A3=Bus[2], B1=Bus[3], B2=Bus[4]");
        Check("voicemeeter: basic's channels", Names(1) == "Stereo Input 1=Strip[0], Stereo Input 2=Strip[1], Voicemeeter Input=Strip[2], A1=Bus[0], B1=Bus[1]");
        Check("voicemeeter: potato's channels", Names(3).StartsWith("Stereo Input 1=Strip[0]") && Names(3).Contains("Voicemeeter VAIO3=Strip[7]") && Names(3).EndsWith("B3=Bus[7]"));
        var banana = StreamAssist.Components.Audio.Background.VoicemeeterChannels.For(2);
        Check("voicemeeter: inputs and buses lists", StreamAssist.Components.Audio.Background.VoicemeeterChannels.Names(banana, inputs: true).Count == 5
            && StreamAssist.Components.Audio.Background.VoicemeeterChannels.Names(banana, inputs: false).SequenceEqual(new[] { "A1", "A2", "A3", "B1", "B2" }));

        var vmManager = new StreamAssist.Components.Audio.Background.VoicemeeterManager(new StreamAssist.Components.Audio.Model.AudioComponentModel());
        var v0 = Errors();
        Check("voicemeeter: not connected says so", !vmManager.TryGetMute("B1", out _) && Errors() == v0 + 1 && LastError().Contains("can't read voicemeeter Bus[3].Mute, not connected"));
        Check("voicemeeter: an unknown channel says so", !vmManager.TrySetMute("Stereo Input 9", true) && LastError().Contains("voicemeeter has no channel 'Stereo Input 9'"));
        Check("voicemeeter: a route goes from an input to a bus", !vmManager.TrySetRoute("B1", "A1", true) && LastError().Contains("it goes from an input to an output bus"));
        vmManager.Dispose();

        var mute = BlockResolver.DeepCopy(new MuteVoicemeeterAction { MuteSound = "m.mp3", UnmuteSound = "u.mp3" }.Pick("Channel", "B2")) as MuteVoicemeeterAction;
        Check("voicemeeter: the mute block saves its channel and sounds", mute is { MuteSound: "m.mp3", UnmuteSound: "u.mp3" } && mute.Picked("Channel") == "B2" && mute.Picked("SetType") == "Toggle");
        var vmList = new ListBlock();
        ((BlockSlotList)vmList.Slots[0]).TryInsertBlock(new MuteVoicemeeterAction(), 0);
        ((BlockSlotList)vmList.Slots[0]).TryInsertBlock(new RecordBlock { Tag = "after" }, 1);
        RecordBlock.Log.Clear();
        var v1 = Errors();
        vmList.SafeRunAsync(new TriggerContext(TriggerPhase.Once, default)).GetAwaiter().GetResult();
        Check("voicemeeter: without voicemeeter the block fails once and stops", RecordBlock.Log.Count == 0 && Errors() == v1 + 1 && LastError().Contains("Set Voicemeeter Mute: voicemeeter not set up"));

        // twitch info blocks pick their type from the setting
        Check("twitch info: settings and kinds", TwitchInfos.Names(TwitchInfos.Stream).SequenceEqual(new[] { "Title", "Category", "Live", "Viewers", "Uptime", "Uptime Minutes", "Followers" })
            && TwitchInfos.Find(TwitchInfos.Follower, "Following")!.Kind == ValueKind.Bool && TwitchInfos.Find(TwitchInfos.User, "Account Age Days")!.Kind == ValueKind.Int);
        var stream = new GetStreamInfoBlock().Pick("Setting", "Live");
        Check("twitch info: Live reads true/false", stream.Kind == ValueKind.Bool);
        stream.Pick("Setting", "Viewers");
        Check("twitch info: Viewers reads a whole number", stream.Kind == ValueKind.Int);
        var follow = (GetFollowerInfoBlock)BlockResolver.DeepCopy(new GetFollowerInfoBlock().Pick("Setting", "Followed On"))!;
        Check("twitch info: saved and loaded keeps the setting and type", follow.Picked("Setting") == "Followed On" && follow.Kind == ValueKind.Text);
        Check("twitch info: user starts as whoever triggered it", ((ValueSlot)new GetTwitchUserBlock().Slots[1]).Text == "{userLogin}" && ((ValueSlot)new ShoutoutAction().Slots[0]).Text == "{userLogin}");

        // twitch blocks without twitch: one error each, the run stops
        var chatbot = new ChatbotManager(Chatbot());
        StreamAssist.Shell.GlobalContext.ChatbotManager = chatbot;
        string RunTwitch(BlockModel block)
        {
            RecordBlock.Log.Clear();
            var list = new ListBlock();
            ((BlockSlotList)list.Slots[0]).TryInsertBlock(block, 0);
            ((BlockSlotList)list.Slots[0]).TryInsertBlock(new RecordBlock { Tag = "after" }, 1);
            list.SafeRunAsync(new TriggerContext(TriggerPhase.Once, default, new Dictionary<string, string> { ["userLogin"] = "viewer" })).GetAwaiter().GetResult();
            return RecordBlock.Log.Count == 0 ? LastError() : "ran on";
        }
        Check("twitch: unban, not connected", RunTwitch(new UnbanUserAction()).Contains("can't unban 'viewer', twitch not connected"));
        Check("twitch: chat mode, not connected", RunTwitch(new SetChatModeAction().Pick("Mode", "Emote Only").Pick("Change", "On")).Contains("can't set emote only, twitch not connected"));
        Check("twitch: chat mode duration has to be a number", RunTwitch(Block(new SetChatModeAction(), "lots")).Contains("Set Chat Mode: Duration 'lots' isn't a whole number"));
        Check("twitch: clip link name has to be valid", RunTwitch(new CreateClipAction { ParameterName = "my link" }).Contains("'my link' isn't a valid name"));
        Check("twitch: run ad keeps twitch's lengths", RunTwitch(Block(new RunAdAction(), "500")).Contains("500s not allowed, 30 to 180"));
        Check("twitch: stream info, not connected", RunTwitch(Block(new SetStreamInfoAction(), "New title")).Contains("can't set stream title, twitch not connected"));
        StartPollAction Poll(params string[] choices)
        {
            var poll = Block(new StartPollAction(), "Best snack?");
            foreach (var c in choices) poll.Choices.Items[^1].SetText(c);
            return poll;
        }
        Check("twitch: poll choices grow like Join's parts", Poll("Chips", "Fruit").Choices.Items.Count == 3);
        Check("twitch: poll needs 2 to 5 choices", RunTwitch(Poll("Chips")).Contains("Start Poll: 1 choices, twitch takes 2 to 5"));
        Check("twitch: poll keeps twitch's lengths", RunTwitch(Block(Poll("Chips", "Fruit"), "Best snack?", "5")).Contains("5s not allowed, 15 to 1800"));
        Check("twitch: poll, not connected", RunTwitch(Poll("Chips", "Fruit")).Contains("can't start poll, twitch not connected"));
        var savedPoll = (StartPollAction)BlockResolver.DeepCopy(Poll("Chips", "Fruit"))!;
        Check("twitch: poll saved and loaded keeps its choices", savedPoll.Choices.Items.Select(i => i.Text).SequenceEqual(new[] { "Chips", "Fruit", "" }) && savedPoll.Title.Text == "Best snack?");
        var prediction = Block(new StartPredictionAction(), "Win?");
        foreach (var o in new[] { "Yes", "No" }) prediction.Outcomes.Items[^1].SetText(o);
        Check("twitch: prediction, not connected", RunTwitch(prediction).Contains("can't start prediction, twitch not connected"));
        Check("twitch: resolve needs a winner", RunTwitch(new EndPredictionAction()).Contains("End Prediction: Resolve needs a Winner"));
        Check("twitch: end poll, not connected", RunTwitch(new EndPollAction()).Contains("can't end poll, twitch not connected"));
        Check("twitch: vip, not connected", RunTwitch(new SetVipAction()).Contains("can't add vip 'viewer', twitch not connected"));
        Check("twitch: moderator, not connected", RunTwitch(new SetModeratorAction().Pick("Change", "Remove")).Contains("can't remove moderator 'viewer', twitch not connected"));
        StreamAssist.Shell.GlobalContext.ChatbotManager = null;
        chatbot.Dispose();

        T Block<T>(T block, params string[] texts) where T : BlockModel
        {
            for (int i = 0; i < texts.Length; i++) ((ValueSlot)block.Slots.Where(s => s is not MenuSlot).ElementAt(i)).SetText(texts[i]);
            return block;
        }
    }



    // ---- kokoro voices (names only: the model isn't in the tests' folder) ----
    private static void KokoroVoiceNames()
    {
        var names = StreamAssist.Components.Audio.Background.KokoroVoices.AllVoiceNames;
        Check("kokoro: every voice has its own name, by accent and gender", names.Count == 54 && names.Distinct().Count() == 54
            && names[3] == "Kokoro: Heart (US, female)" && names[26] == "Kokoro: George (UK, male)" && names[53] == "Kokoro: Santa (Spanish, male)");
        Check("kokoro: a dropdown name finds its speaker, others aren't Kokoro voices",
            StreamAssist.Components.Audio.Background.KokoroVoices.TryFind("Kokoro: Heart (US, female)", out var heart) && heart == 3
            && !StreamAssist.Components.Audio.Background.KokoroVoices.TryFind("Microsoft David Desktop", out _));
    }
}
