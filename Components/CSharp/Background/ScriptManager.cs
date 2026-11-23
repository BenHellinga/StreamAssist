using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.CSharp.Api;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Scripting;
using StreamAssist.Scripting.Runtime;

namespace StreamAssist.Components.CSharp.Background;



// where a script is at: being compiled, ready to run, or not (why), or its file is gone
public enum ScriptState { Compiling, Ready, Error, Missing }

public sealed record ScriptStatus(ScriptState State, string Message, DateTime At);



// the C# scripts: each binding in C# mode has a file Config/Scripts/<id>.cs (Shared/*.cs is compiled into every one of
// them, for helpers). Made once at startup: writes the project file VS Code reads for IntelliSense, compiles every
// script in the background, and again a moment after one is saved (a Shared file recompiles them all), runs them for
// their bindings, and opens them in VS Code. A script that doesn't compile can't run (each try is logged); the last one
// that compiled is never run instead, so what runs is always what's in the file
public sealed class ScriptManager : IBindableScripts, IDisposable
{
    // constants



    public const string ProjectFileName = "Scripts.csproj";
    private static readonly TimeSpan SaveSettle = TimeSpan.FromMilliseconds(300); // editors write a file in steps
    private const int MaxCompileLogLines = 200;



    // private



    private readonly string _folder;
    private readonly object _lock = new(); // _compiles is touched by the watcher's thread too
    private readonly Dictionary<string, Task<CompiledScript?>> _compiles = new(StringComparer.OrdinalIgnoreCase); // the latest compile of each id
    private readonly ConcurrentDictionary<string, ScriptStatus> _statuses = new(StringComparer.OrdinalIgnoreCase); // set on the UI thread, read by runs
    private readonly ConcurrentDictionary<string, string> _titles = new(StringComparer.OrdinalIgnoreCase); // id -> its header line's text
    private readonly Dictionary<string, List<string>> _compileLogs = new(StringComparer.OrdinalIgnoreCase); // UI thread only, this session's
    private FileSystemWatcher? _watcher;



    // public



    public string Folder => _folder;
    public string SharedFolder => Path.Combine(_folder, "Shared");
    public string RemovedFolder => Path.Combine(_folder, "Removed");



    // events



    public event Action<string>? StatusChanged; // a script's status changed (its id), always on the UI thread
    public event Action<string>? CompileLogChanged; // a script's compile output changed (its id), always on the UI thread



    // constructor



    // folder is Config/Scripts (a temp folder in the tests); watch false compiles only when asked (the tests)
    public ScriptManager(string folder, bool watch = true)
    {
        _folder = folder;
        Directory.CreateDirectory(_folder);
        Directory.CreateDirectory(SharedFolder);
        WriteProjectFile();

        if (watch) StartWatching();
    }



    // public api



    public string PathOf(string id) => Path.Combine(_folder, id + ".cs");



    public ScriptStatus? GetStatus(string id) => _statuses.TryGetValue(id, out var status) ? status : null;



    // what each compile of a script said this session, oldest first, like the console's lines: "[14:02:11] compiled",
    // "[14:02:30] error - line 12: ; expected" (one line per error). UI thread only
    public IReadOnlyList<string> CompileLog(string id) => _compileLogs.TryGetValue(id, out var lines) ? lines : [];



    public void ClearCompileLog(string id)
    {
        if (!_compileLogs.Remove(id)) return;
        CompileLogChanged?.Invoke(id);
    }



    // a new id no binding or file has
    public string NewScriptId()
    {
        string id;
        do id = BindableModel.NewScriptId();
        while (File.Exists(PathOf(id)) || File.Exists(Path.Combine(RemovedFolder, id + ".cs")));
        return id;
    }



    // writes a new script for a binding (nothing happens if it has one): title says what it's for, e.g.
    // "Chat command !quote {n}", held is true for keys and microphones (they get OnReleasedAsync too)
    public bool TryCreateScript(string id, string title, bool held)
    {
        var path = PathOf(id);
        if (File.Exists(path)) return true;

        try
        {
            File.WriteAllText(path, Template(id, title, held));
        }
        catch (Exception ex)
        {
            AppLog.WriteError($"can't create script file ({ex.Message})");
            return false;
        }

        Compile(id);
        return true;
    }



    // keeps the header line saying what the script is for (e.g. a chat command's pattern changed); only a header the
    // app wrote is changed, and only when it's different (VS Code reloads a file that changes under it)
    public void UpdateTitle(string id, string title)
    {
        var path = PathOf(id);
        try
        {
            if (!File.Exists(path)) return;
            var lines = File.ReadAllLines(path).ToList();
            var header = Header(title);
            if (lines.Count == 0 || !lines[0].StartsWith("// ") || !lines[0].EndsWith(HeaderEnd) || lines[0] == header) return;

            lines[0] = header;
            File.WriteAllLines(path, lines);
        }
        catch (Exception)
        {
            // only cosmetic, the file is left as it is (e.g. open somewhere that locks it)
        }
    }



    // another binding's copy of a script (pasted, or its config copied): the same code in a file of its own
    public void CopyScript(string fromId, string toId)
    {
        var from = PathOf(fromId);
        if (!File.Exists(from)) from = Path.Combine(RemovedFolder, fromId + ".cs");
        if (!File.Exists(from)) return; // nothing to copy, the copy starts from the template when it's opened

        try
        {
            File.WriteAllText(PathOf(toId), File.ReadAllText(from).Replace(NamespaceOf(fromId), NamespaceOf(toId)));
        }
        catch (Exception ex)
        {
            AppLog.WriteError($"can't copy script file ({ex.Message})");
            return;
        }

        Compile(toId);
    }



    // compiles every script in the folder in the background (at startup)
    public void CompileAll()
    {
        foreach (var file in Directory.GetFiles(_folder, "*.cs"))
            Compile(Path.GetFileNameWithoutExtension(file));
    }



    // the latest compile of a script, waiting for it if it's still going (null: it doesn't compile, or there's no file)
    public Task<CompiledScript?> GetCompiledAsync(string id)
    {
        lock (_lock)
            if (_compiles.TryGetValue(id, out var compile)) return compile;

        return Compile(id);
    }



    // starts compiling a script (after editors have finished writing it), the status follows on the UI thread
    public Task<CompiledScript?> Compile(string id, bool settle = false)
    {
        Post(() => SetStatus(id, new(ScriptState.Compiling, "", DateTime.Now)));

        var path = PathOf(id);
        var compile = Task.Run(async () =>
        {
            if (settle) await Task.Delay(SaveSettle);

            if (!File.Exists(path))
            {
                Post(() =>
                {
                    SetStatus(id, new(ScriptState.Missing, "no script file", DateTime.Now));
                    AddToCompileLog(id, ["error - no script file"]);
                });
                return null;
            }

            var title = ReadTitle(path) ?? $"script {id}";
            var (script, error) = CompileWithRetries(path);
            Post(() =>
            {
                _titles[id] = title;
                if (error != null)
                {
                    AppLog.WriteError($"{title}: doesn't compile ({error.Message})");
                    SetStatus(id, new(ScriptState.Error, error.Message, DateTime.Now));
                    AddToCompileLog(id, error.All.Select(e => $"error - {e}"));
                }
                else
                {
                    SetStatus(id, new(ScriptState.Ready, "", DateTime.Now));
                    AddToCompileLog(id, ["compiled"]);
                }
            });
            return script;
        });

        lock (_lock) _compiles[id] = compile;
        return compile;
    }



    // runs a binding's script on the caller's thread (a run's background thread): RunAsync, or OnReleasedAsync when a
    // held key or button is let go, kept in check by its limits (ScriptGuard). Its errors are logged with its title
    // (once, a StreamAssistException the app already logged isn't logged again); a Stop or cancellation ends it
    // quietly, a limit with why. Never throws, except the run's own cancellation
    public async Task RunAsync(string scriptId, TriggerContext context, ScriptLimits limits)
    {
        var compiled = await GetCompiledAsync(scriptId);
        var title = TitleOf(scriptId);
        if (compiled == null)
        {
            var status = GetStatus(scriptId);
            AppLog.WriteError(status?.State == ScriptState.Missing ? $"{title}: no script file ({scriptId}.cs)" : $"{title}: doesn't compile, fix it first ({status?.Message})");
            return;
        }

        // the run limit cancels the run like its binding being removed would, but says why
        using var runLimit = limits.RunSeconds is { } seconds ? new CancellationTokenSource(TimeSpan.FromSeconds(seconds)) : null;
        using var linked = runLimit == null ? null : CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, runLimit.Token);
        var run = linked == null ? context : context.WithCancellation(linked.Token);

        var guard = new ScriptGuard(ScriptGuard.Current, title, run.CancellationToken,
            limits.LoopSeconds is { } loop ? TimeSpan.FromSeconds(loop) : null,
            limits.LockWaitSeconds is { } wait ? TimeSpan.FromSeconds(wait) : null);
        guard.Use();

        try
        {
            var script = (StreamAssistScript)Activator.CreateInstance(compiled.ScriptType)!;
            script.AttachRun(new ScriptRun(run));

            if (context.Phase == TriggerPhase.Stop) await script.OnReleasedAsync();
            else await script.RunAsync();
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (runLimit?.IsCancellationRequested == true)
        {
            AppLog.WriteError($"{title}: stopped after {ScriptGuard.Seconds(TimeSpan.FromSeconds(limits.RunSeconds!.Value))} (run limit)");
        }
        catch (Exception ex)
        {
            LogFailure(title, ex, compiled.FilePath);
        }
        finally
        {
            ScriptLocks.ReleaseAll(guard);
        }
    }



    // a run's failure, logged once: a Stop, a block's error (it logged it) or one the app already logged stays quiet
    public static void LogFailure(string title, Exception ex, string filePath)
    {
        var inner = ex is System.Reflection.TargetInvocationException { InnerException: { } wrapped } ? wrapped : ex;
        switch (inner)
        {
            case ScriptStoppedException or StopRunException or BlockFailedException:
            case StreamAssistException { IsLogged: true }:
                return;
            case ScriptLimitException:
                AppLog.WriteError($"{title}: stopped, {inner.Message}{ScriptCompiler.LineOf(inner, filePath)}");
                return;
            default:
                AppLog.WriteError($"{title}: {inner.Message}{ScriptCompiler.LineOf(inner, filePath)}");
                return;
        }
    }



    // a script's name for messages (its header line), "script <id>" until it's been read
    public string TitleOf(string scriptId) => _titles.TryGetValue(scriptId, out var known) ? known : $"script {scriptId}";



    // opens the scripts folder in VS Code at this script (its folder holds the project file, which is what gives
    // IntelliSense), false if VS Code can't be found
    public bool TryOpenInVsCode(string id)
    {
        WriteProjectFile();
        var codeCmd = FindVsCode();
        if (codeCmd == null)
        {
            AppLog.WriteError("can't open vs code, not found (install it, or add code to PATH)");
            return false;
        }

        try
        {
            // cmd /s /c runs the rest as it is, quotes and all (code.cmd is a batch file)
            Process.Start(new ProcessStartInfo("cmd.exe", $"/d /s /c \"\"{codeCmd}\" \"{_folder}\" -g \"{PathOf(id)}\"\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            return true;
        }
        catch (Exception ex)
        {
            AppLog.WriteError($"can't open vs code ({ex.Message})");
            return false;
        }
    }



    // moves every script no binding has into Removed (at startup, once everything is loaded; never after a file
    // couldn't be read, since its bindings' scripts would look unused)
    public void SweepUnused(IReadOnlyCollection<string> usedIds)
    {
        var used = new HashSet<string>(usedIds, StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.GetFiles(_folder, "*.cs"))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            if (used.Contains(id)) continue;

            try
            {
                Directory.CreateDirectory(RemovedFolder);
                var target = Path.Combine(RemovedFolder, Path.GetFileName(file));
                if (File.Exists(target)) File.Delete(target);
                File.Move(file, target);
                lock (_lock) _compiles.Remove(id);
            }
            catch (Exception ex)
            {
                AppLog.WriteWarning($"can't move unused script {id} to Removed ({ex.Message})");
            }
        }
    }



    // the project file VS Code's C# extension reads: what the app compiles with (the API's dll, the same usings), so
    // its IntelliSense, errors and docs match. Rewritten when the app's folder moved
    public void WriteProjectFile()
    {
        var dll = typeof(StreamAssistScript).Assembly.Location;
        var project = $"""
            <Project Sdk="Microsoft.NET.Sdk">

              <!-- written by StreamAssist (changes here are overwritten): lets VS Code know the scripting API, for
                   IntelliSense. The app compiles each script itself when it's saved, with Shared/*.cs in it -->

              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <LangVersion>latest</LangVersion>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
              </PropertyGroup>

              <ItemGroup>
                <Compile Include="*.cs" />
                <Compile Include="Shared/**/*.cs" />
                <Using Include="StreamAssist.Scripting" />
                <Reference Include="StreamAssist.Scripting">
                  <HintPath>{dll}</HintPath>
                </Reference>
              </ItemGroup>

            </Project>

            """;

        var path = Path.Combine(_folder, ProjectFileName);
        try
        {
            if (File.Exists(path) && File.ReadAllText(path) == project) return;
            File.WriteAllText(path, project);
        }
        catch (Exception ex)
        {
            AppLog.WriteWarning($"can't write scripts project file, vs code won't know the api ({ex.Message})");
        }
    }



    // the code a new script starts with
    public static string Template(string id, string title, bool held)
    {
        var released = held ? """


                /// <summary>When the key or button is let go (or the microphone stops talking).</summary>
                public override async Task OnReleasedAsync()
                {
                }
            """ : "";

        return $$"""
            {{Header(title)}}
            namespace {{NamespaceOf(id)}};

            public sealed class Script : StreamAssistScript
            {
                /// <summary>When it's triggered (Parameters has what triggered it, e.g. Parameters["user"]).</summary>
                public override async Task RunAsync()
                {
                }{{released}}
            }

            """;
    }



    // helpers



    private const string HeaderEnd = " (written by the app)";

    private static string Header(string title) => $"// {title.Replace("\n", " ").Replace("\r", "")}{HeaderEnd}";

    private static string NamespaceOf(string id) => $"Scripts.{char.ToUpperInvariant(id[0])}{id[1..]}";



    private static string? ReadTitle(string path)
    {
        try
        {
            var first = File.ReadLines(path).FirstOrDefault() ?? "";
            if (!first.StartsWith("// ")) return null;
            var title = first[3..];
            return title.EndsWith(HeaderEnd) ? title[..^HeaderEnd.Length] : title;
        }
        catch (Exception)
        {
            return null;
        }
    }



    // a file that's mid-save can't be read for a moment (editors, OneDrive), so reading is tried a few times
    private (CompiledScript? Script, ScriptCompileError? Error) CompileWithRetries(string path)
    {
        var shared = Directory.Exists(SharedFolder) ? Directory.GetFiles(SharedFolder, "*.cs", SearchOption.AllDirectories) : [];
        for (var attempt = 0; ; attempt++)
        {
            var result = ScriptCompiler.Compile(path, shared);
            if (result.Error == null || !result.Error.Message.StartsWith("can't read") || attempt == 4) return result;
            Thread.Sleep(100);
        }
    }



    private void SetStatus(string id, ScriptStatus status)
    {
        _statuses[id] = status;
        StatusChanged?.Invoke(id);
    }



    // keeps the last MaxCompileLogLines lines
    private void AddToCompileLog(string id, IEnumerable<string> messages)
    {
        if (!_compileLogs.TryGetValue(id, out var lines)) _compileLogs[id] = lines = [];
        var time = $"[{DateTime.Now:HH:mm:ss}] ";
        lines.AddRange(messages.Select(m => time + m));
        if (lines.Count > MaxCompileLogLines) lines.RemoveRange(0, lines.Count - MaxCompileLogLines);
        CompileLogChanged?.Invoke(id);
    }



    private static void Post(Action action) => Dispatcher.UIThread.Post(action);



    private void StartWatching()
    {
        _watcher = new FileSystemWatcher(_folder, "*.cs")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };
        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.Renamed += OnFileRenamed;
        _watcher.Deleted += OnFileChanged;
        _watcher.EnableRaisingEvents = true;
    }



    // on the watcher's thread: a script recompiles, a Shared file recompiles every script
    private void OnFileChanged(object sender, FileSystemEventArgs e) => OnFileChanged(e.FullPath);

    private void OnFileRenamed(object sender, RenamedEventArgs e) => OnFileChanged(e.FullPath); // editors save by renaming a temp file



    private void OnFileChanged(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? "";
        if (string.Equals(directory, Path.GetFullPath(_folder).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            Compile(Path.GetFileNameWithoutExtension(path), settle: true);
            return;
        }

        if (Path.GetFullPath(path).StartsWith(Path.GetFullPath(SharedFolder), StringComparison.OrdinalIgnoreCase))
            foreach (var file in Directory.GetFiles(_folder, "*.cs"))
                Compile(Path.GetFileNameWithoutExtension(file), settle: true);
    }



    // code.cmd on PATH, or in VS Code's usual install folders, null if it's nowhere
    private static string? FindVsCode()
    {
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir.Trim('"'), "code.cmd");
            if (File.Exists(candidate)) return candidate;
        }

        string[] installs =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Microsoft VS Code", "bin", "code.cmd"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft VS Code", "bin", "code.cmd"),
        ];
        return installs.FirstOrDefault(File.Exists);
    }



    // disposal



    public void Dispose()
    {
        if (_watcher == null) return;
        _watcher.EnableRaisingEvents = false;
        _watcher.Changed -= OnFileChanged;
        _watcher.Created -= OnFileChanged;
        _watcher.Renamed -= OnFileRenamed;
        _watcher.Deleted -= OnFileChanged;
        _watcher.Dispose();
    }
}
