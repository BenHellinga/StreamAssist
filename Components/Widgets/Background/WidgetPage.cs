namespace StreamAssist.Components.Widgets.Background;



// the page every widget's address serves (OBS's browser source): transparent, it connects back to the app
// (/ws/<name>) and draws what it's told. Mic Bounce: the image, eased between the app's level updates (50 a second) so it
// glides. Wheel: drawn on a canvas, spinning with the same curve the app times its ticks with (1 - (1 - x)^4), then
// the rest of the wheel darkens and the winner shows in front for a while. It only draws while something moves (a
// level changing, a spin, the winner fading) and sits idle otherwise, also while its widget is off (it shows nothing).
// If the app goes away it shows nothing and keeps trying to reconnect, so a restart needs nothing done in OBS
public static class WidgetPage
{
    public const string Html = """
<!doctype html>
<html>
<head>
<meta charset="utf-8">
<title>StreamAssist widget</title>
<style>
  html, body { margin: 0; width: 100%; height: 100%; overflow: hidden; background: transparent; }
  #bounce { position: absolute; left: 50%; bottom: 0; display: none; max-width: 100%; }
  #wheel { position: absolute; inset: 0; width: 100%; height: 100%; display: none; }
</style>
</head>
<body>
<img id="bounce" alt="">
<canvas id="wheel"></canvas>
<script>
"use strict";
const name = decodeURIComponent(location.pathname.split("/widget/")[1] || "");
const img = document.getElementById("bounce");
const canvas = document.getElementById("wheel");
const ctx = canvas.getContext("2d");

let state = null;          // the widget's settings, as the app last sent them
let level = 0, shown = 0;  // mic bounce: the app's level (0..1), and where the image is now
let talking = false;
let rotation = 0;          // wheel: where it rests (degrees)
let spin = null;           // the spin running: { from, to, lengthMs, slices, winnerIndex, started }
let winner = null;         // the winner showing: { slices, index, rotation, started, showMs }
let last = performance.now();
let looping = false;       // a frame is coming

// ---- the connection: everything the page draws comes from the app ----

function connect() {
  const socket = new WebSocket(`ws://${location.host}/ws/${encodeURIComponent(name)}`);
  socket.onmessage = e => receive(JSON.parse(e.data));
  socket.onclose = () => { state = null; spin = null; winner = null; show(); setTimeout(connect, 2000); };
  window.onresize = kick;
}

function receive(m) {
  if (m.type === "state") {
    state = m;
    if (m.kind === "Wheel") {
      rotation = m.rotation;
      spin = m.spin ? startSpin(m.spin) : null; // a winner showing keeps showing
      if (m.showing && !winner) winner = { slices: m.showing.slices, index: m.showing.winnerIndex, rotation: m.rotation,
                                           started: performance.now() - m.showing.elapsedMs, showMs: m.showing.showMs };
    } else {
      level = m.level; talking = m.isTalking;
    }
    show();
    kick();
  } else if (m.type === "live") {
    level = m.level;
    if (m.isTalking !== talking) { talking = m.isTalking; show(); }
    kick();
  } else if (m.type === "spin") {
    winner = null;
    spin = startSpin(m);
    kick();
  }
}

function startSpin(s) {
  return { from: s.from, to: s.to, lengthMs: s.lengthMs, slices: s.slices, winnerIndex: s.winnerIndex,
           showMs: s.showMs, started: performance.now() - (s.elapsedMs || 0) };
}

function show() {
  const on = state && state.found && !state.disabled;
  const bounce = on && state.kind === "MicBounce" && state.idleUrl;
  img.style.display = bounce ? "block" : "none";
  canvas.style.display = on && state.kind === "Wheel" ? "block" : "none";
  if (bounce) {
    const src = talking && state.talkingUrl ? state.talkingUrl : state.idleUrl;
    if (img.getAttribute("src") !== src) img.setAttribute("src", src);
    img.style.maxHeight = `calc(100% - ${state.height}px)`;
  }
}

// ---- drawing: only while something moves ----

// a frame is wanted (something changed): the loop runs until nothing moves any more
function kick() {
  if (looping) return;
  looping = true;
  last = performance.now();
  requestAnimationFrame(frame);
}

function frame(now) {
  const dt = Math.min(100, now - last);
  last = now;
  let moving = false;
  const on = state && state.found && !state.disabled;
  if (on && state.kind === "MicBounce") {
    // glides between the app's updates (50 a second) as smoothly as it's set to; 0 jumps to each one
    shown = state.smoothing > 0 ? shown + (level - shown) * (1 - Math.exp(-dt / Math.min(40, state.smoothing))) : level;
    if (Math.abs(level - shown) < 0.0005) shown = level;
    img.style.transform = `translate(-50%, ${-shown * state.height}px)`;
    moving = shown !== level;
  } else if (on && state.kind === "Wheel") {
    drawWheel(now);
    moving = spin !== null || winner !== null;
  }
  if (moving) requestAnimationFrame(frame);
  else looping = false;
}

// ---- the wheel ----

const ease = x => 1 - Math.pow(1 - Math.min(1, Math.max(0, x)), 4);

function drawWheel(now) {
  const ratio = window.devicePixelRatio || 1;
  const w = canvas.clientWidth, h = canvas.clientHeight;
  if (canvas.width !== Math.round(w * ratio) || canvas.height !== Math.round(h * ratio)) {
    canvas.width = Math.round(w * ratio); canvas.height = Math.round(h * ratio);
  }
  ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
  ctx.clearRect(0, 0, w, h);

  let slices = state.slices, angle = rotation, highlight = -1, fade = 0;
  if (spin) {
    const x = (now - spin.started) / spin.lengthMs;
    slices = spin.slices;
    angle = spin.from + (spin.to - spin.from) * ease(x);
    if (x >= 1) {
      rotation = spin.to;
      winner = { slices: spin.slices, index: spin.winnerIndex, rotation: spin.to, started: now, showMs: spin.showMs };
      spin = null;
    }
  }
  if (winner) {
    const t = now - winner.started;
    if (t > winner.showMs + 400) winner = null;
    else {
      slices = winner.slices; angle = winner.rotation; highlight = winner.index;
      fade = Math.min(1, t / 300) * Math.min(1, Math.max(0, (winner.showMs + 400 - t) / 400));
    }
  }

  const size = Math.min(w, h);
  const r = size / 2 - Math.max(6, size * 0.06);
  const cx = w / 2, cy = h / 2 + size * 0.03;
  if (r <= 0) return;

  if (!slices.length) {
    ctx.beginPath(); ctx.arc(cx, cy, r, 0, Math.PI * 2);
    ctx.fillStyle = "rgba(40,40,40,0.8)"; ctx.fill();
    drawPointer(cx, cy, r, size);
    return;
  }

  // the slices, 0 degrees at the top, turned by the wheel's rotation
  ctx.save();
  ctx.translate(cx, cy);
  ctx.rotate(angle * Math.PI / 180);
  slices.forEach((s, i) => {
    const a0 = (s.start - 90) * Math.PI / 180, a1 = (s.end - 90) * Math.PI / 180;
    ctx.beginPath(); ctx.moveTo(0, 0); ctx.arc(0, 0, r, a0, a1); ctx.closePath();
    ctx.fillStyle = s.color; ctx.fill();
    ctx.lineWidth = Math.max(1, size * 0.004); ctx.strokeStyle = "rgba(255,255,255,0.85)"; ctx.stroke();

    // the label along the slice's middle, towards the rim
    const mid = (a0 + a1) / 2, span = a1 - a0;
    const fontSize = Math.max(8, Math.min(r * 0.11, r * span * 0.45));
    ctx.save();
    ctx.rotate(mid);
    ctx.font = `bold ${fontSize}px "Segoe UI", system-ui, sans-serif`;
    ctx.textAlign = "right"; ctx.textBaseline = "middle";
    ctx.fillStyle = contrast(s.color);
    ctx.fillText(fit(s.text, r * 0.72), r * 0.92, 0);
    ctx.restore();
  });

  // the winner: everything else darkens
  if (highlight >= 0 && fade > 0) {
    slices.forEach((s, i) => {
      if (i === highlight) return;
      ctx.beginPath(); ctx.moveTo(0, 0);
      ctx.arc(0, 0, r + 1, (s.start - 90) * Math.PI / 180, (s.end - 90) * Math.PI / 180); ctx.closePath();
      ctx.fillStyle = `rgba(0,0,0,${0.65 * fade})`; ctx.fill();
    });
  }
  ctx.restore();

  // the hub
  ctx.beginPath(); ctx.arc(cx, cy, r * 0.08, 0, Math.PI * 2);
  ctx.fillStyle = "#ffffff"; ctx.fill();
  ctx.lineWidth = Math.max(1, size * 0.006); ctx.strokeStyle = "rgba(0,0,0,0.5)"; ctx.stroke();
  drawPointer(cx, cy, r, size);

  // the winner's name in front
  if (highlight >= 0 && fade > 0) drawWinner(slices[highlight], cx, cy, r, fade);
}

function drawPointer(cx, cy, r, size) {
  const pw = Math.max(8, size * 0.05), ph = Math.max(10, size * 0.07);
  ctx.beginPath();
  ctx.moveTo(cx - pw / 2, cy - r - ph * 0.55);
  ctx.lineTo(cx + pw / 2, cy - r - ph * 0.55);
  ctx.lineTo(cx, cy - r + ph * 0.45);
  ctx.closePath();
  ctx.fillStyle = "#ffffff"; ctx.fill();
  ctx.lineWidth = Math.max(1, size * 0.005); ctx.strokeStyle = "rgba(0,0,0,0.6)"; ctx.stroke();
}

function drawWinner(s, cx, cy, r, alpha) {
  const fontSize = r * 0.16;
  ctx.save();
  ctx.globalAlpha = alpha;
  ctx.font = `bold ${fontSize}px "Segoe UI", system-ui, sans-serif`;
  const text = fit(s.text, r * 1.6);
  const tw = ctx.measureText(text).width, pad = fontSize * 0.5;
  const bw = tw + pad * 2, bh = fontSize * 1.7;
  ctx.beginPath(); ctx.roundRect(cx - bw / 2, cy - bh / 2, bw, bh, bh * 0.25);
  ctx.fillStyle = "rgba(15,15,15,0.92)"; ctx.fill();
  ctx.lineWidth = Math.max(2, fontSize * 0.12); ctx.strokeStyle = s.color; ctx.stroke();
  ctx.fillStyle = "#ffffff"; ctx.textAlign = "center"; ctx.textBaseline = "middle";
  ctx.fillText(text, cx, cy + fontSize * 0.04);
  ctx.restore();
}

// black or white, whichever reads better on the colour
function contrast(hex) {
  const n = parseInt(hex.slice(1), 16);
  const l = 0.299 * (n >> 16) + 0.587 * ((n >> 8) & 255) + 0.114 * (n & 255);
  return l > 150 ? "#111111" : "#ffffff";
}

// the text cut with … to fit the width (at the current font)
function fit(text, width) {
  if (ctx.measureText(text).width <= width) return text;
  let cut = text;
  while (cut.length > 1 && ctx.measureText(cut + "…").width > width) cut = cut.slice(0, -1);
  return cut + "…";
}

connect();
</script>
</body>
</html>
""";
}
