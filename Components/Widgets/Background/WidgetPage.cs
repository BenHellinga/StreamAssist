namespace StreamAssist.Components.Widgets.Background;



// the page every widget's address serves (OBS's browser source): transparent, it connects back to the app
// (/ws/<name>) and draws what it's told. Mic Bounce: the image, eased between the app's level updates (50 a second) so it
// glides. Wheel: drawn on a canvas, spinning with the same curve the app times its ticks with (1 - (1 - x)^4), then
// the rest of the wheel darkens and the winner shows in front for a while. Plinko: the board (sent when anything about it
// changes) and its balls, sent 50 times a second while there are any, drawn a little behind so each ball glides between
// two of them, then a finished drop's result in front, fading in and out. It only draws while something moves (a level changing, a spin, the winner fading, balls on the board) and
// sits idle otherwise, also while its widget is off or hidden (it shows nothing).
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
let frames = [];           // plinko: the last few ball updates { t, balls: Map(id -> [x, y, colour]), alpha, counts }
let last = performance.now();
let looping = false;       // a frame is coming

// ---- the connection: everything the page draws comes from the app ----

function connect() {
  const socket = new WebSocket(`ws://${location.host}/ws/${encodeURIComponent(name)}`);
  socket.onmessage = e => receive(JSON.parse(e.data));
  socket.onclose = () => { state = null; spin = null; winner = null; frames = []; show(); setTimeout(connect, 2000); };
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
    } else if (m.kind === "Plinko") {
      frames = [];
      addFrame(m.frame);
    } else {
      level = m.level; talking = m.isTalking;
    }
    show();
    kick();
  } else if (m.type === "live") {
    level = m.level;
    if (m.isTalking !== talking) { talking = m.isTalking; show(); }
    kick();
  } else if (m.type === "plinko") {
    addFrame(m);
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
  const on = state && state.found && !state.disabled && !state.hidden;
  const bounce = on && state.kind === "MicBounce" && state.idleUrl;
  img.style.display = bounce ? "block" : "none";
  canvas.style.display = on && (state.kind === "Wheel" || state.kind === "Plinko") ? "block" : "none";
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
  const on = state && state.found && !state.disabled && !state.hidden;
  if (on && state.kind === "MicBounce") {
    // glides between the app's updates (50 a second) as smoothly as it's set to; 0 jumps to each one
    shown = state.smoothing > 0 ? shown + (level - shown) * (1 - Math.exp(-dt / Math.min(40, state.smoothing))) : level;
    if (Math.abs(level - shown) < 0.0005) shown = level;
    img.style.transform = `translate(-50%, ${-shown * state.height}px)`;
    moving = shown !== level;
  } else if (on && state.kind === "Wheel") {
    drawWheel(now);
    moving = spin !== null || winner !== null;
  } else if (on && state.kind === "Plinko") {
    drawPlinko(now);
    moving = frames.some(f => f.balls.size > 0 || f.result);
  }
  if (moving) requestAnimationFrame(frame);
  else looping = false;
}

// ---- plinko ----

const plinkoDelay = 50; // ms behind the newest update, so there's always a next one to glide towards

function addFrame(f) {
  if (!f) return;
  const balls = new Map();
  for (let i = 0; i + 3 < f.balls.length; i += 4) balls.set(f.balls[i], [f.balls[i + 1], f.balls[i + 2], f.balls[i + 3]]);
  frames.push({ t: performance.now(), balls, alpha: f.alpha, counts: f.counts, result: f.result });
  if (frames.length > 20) frames.shift();
}

function drawPlinko(now) {
  const ratio = window.devicePixelRatio || 1;
  const w = canvas.clientWidth, h = canvas.clientHeight;
  if (canvas.width !== Math.round(w * ratio) || canvas.height !== Math.round(h * ratio)) {
    canvas.width = Math.round(w * ratio); canvas.height = Math.round(h * ratio);
  }
  ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
  ctx.clearRect(0, 0, w, h);
  const board = state.board;
  if (!board) return;

  // the board fitted to the page, centred
  const [l, t, r, b] = board.bounds;
  const scale = Math.min(w / (r - l), h / (b - t));
  const X = x => w / 2 + (x - (l + r) / 2) * scale, Y = y => h / 2 + (y - (t + b) / 2) * scale;

  // the two updates either side of the moment drawn (the newest if it's caught up)
  const at = now - plinkoDelay;
  while (frames.length > 2 && frames[1].t <= at) frames.shift();
  const a = frames[0], c = frames.length > 1 ? frames[1] : null;
  const f = c ? Math.min(1, Math.max(0, (at - a.t) / (c.t - a.t))) : 0;
  const counts = (c || a || {}).counts || [];

  // the buckets, tinted (their names go on top of everything, below)
  for (const bucket of board.buckets) {
    const x0 = X(bucket.left), x1 = X(bucket.right), y0 = Y(board.bucketTop), y1 = Y(board.floor);
    ctx.globalAlpha = 0.25; ctx.fillStyle = bucket.color; ctx.fillRect(x0, y0, x1 - x0, y1 - y0); ctx.globalAlpha = 1;
  }

  // the walls, floor and dividers
  ctx.lineCap = "round"; ctx.lineWidth = Math.max(1, 0.06 * scale); ctx.strokeStyle = "rgba(255,255,255,0.85)";
  for (const [x1, y1, x2, y2] of board.walls) { ctx.beginPath(); ctx.moveTo(X(x1), Y(y1)); ctx.lineTo(X(x2), Y(y2)); ctx.stroke(); }

  // the pins
  ctx.fillStyle = "#9e9e9e"; ctx.strokeStyle = "#000000"; ctx.lineWidth = Math.max(1, 0.03 * scale);
  for (let i = 0; i + 1 < board.pins.length; i += 2) {
    ctx.beginPath(); ctx.arc(X(board.pins[i]), Y(board.pins[i + 1]), board.pinRadius * scale, 0, Math.PI * 2); ctx.fill(); ctx.stroke();
  }

  // the balls, each between where it was and where it's going
  if (a) {
    ctx.globalAlpha = c ? a.alpha + (c.alpha - a.alpha) * f : a.alpha;
    ctx.strokeStyle = "rgba(0,0,0,0.6)"; ctx.lineWidth = Math.max(1, 0.04 * scale);
    for (const [id, [x, y, color]] of (c || a).balls) {
      const from = c ? a.balls.get(id) : null;
      const bx = from ? from[0] + (x - from[0]) * f : x, by = from ? from[1] + (y - from[1]) * f : y;
      ctx.beginPath(); ctx.arc(X(bx), Y(by), board.ballRadius * scale, 0, Math.PI * 2);
      ctx.fillStyle = color; ctx.fill(); ctx.stroke();
    }
    ctx.globalAlpha = 1;
  }

  // each bucket's name over how many have landed in it (0 until one does), in its middle, in front of the balls: both
  // as big as the name fits its width (down to the min, then cut with …)
  board.buckets.forEach((bucket, i) => {
    const x0 = X(bucket.left), x1 = X(bucket.right), y0 = Y(board.bucketTop), y1 = Y(board.floor);
    const width = (x1 - x0) * 0.85, mx = (x0 + x1) / 2, my = (y0 + y1) / 2;
    ctx.textAlign = "center"; ctx.textBaseline = "middle"; ctx.fillStyle = "#ffffff"; ctx.strokeStyle = "rgba(0,0,0,0.8)";
    const size = fitSize(bucket.text, board.labelMax * scale, board.labelMin * scale, width);
    ctx.lineWidth = Math.max(2, size * 0.18);
    const label = fit(bucket.text, width), count = fit(`${counts[i] || 0}`, width);
    ctx.strokeText(label, mx, my - size * 0.6); ctx.fillText(label, mx, my - size * 0.6);
    ctx.strokeText(count, mx, my + size * 0.6); ctx.fillText(count, mx, my + size * 0.6);
  });

  // a finished drop's result, in front: a box like the wheel's winner, its border the bucket's colour
  const result = (c || a).result;
  if (result) {
    const size = 1.1 * scale;
    ctx.save();
    ctx.globalAlpha = result.alpha;
    ctx.font = `bold ${size}px "Segoe UI", system-ui, sans-serif`;
    const text = fit(result.text, (r - l) * scale * 0.75);
    const tw = ctx.measureText(text).width, pad = size * 0.5;
    const bw = tw + pad * 2, bh = size * 1.7, cx = X(0), cy = Y(board.resultY);
    ctx.beginPath(); ctx.roundRect(cx - bw / 2, cy - bh / 2, bw, bh, bh * 0.25);
    ctx.fillStyle = "rgba(15,15,15,0.92)"; ctx.fill();
    ctx.lineWidth = Math.max(2, size * 0.12); ctx.strokeStyle = result.color; ctx.stroke();
    ctx.fillStyle = "#ffffff"; ctx.textAlign = "center"; ctx.textBaseline = "middle";
    ctx.fillText(text, cx, cy + size * 0.04);
    ctx.restore();
  }
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

    // the label along the slice's middle, towards the rim: as big as the slice allows, smaller to fit its length
    // (down to the theme's min, a % of the radius), then cut with …
    const mid = (a0 + a1) / 2, span = a1 - a0;
    const min = r * (state.minTextSize || 4) / 100;
    let fontSize = Math.max(min, Math.min(r * 0.11, r * span * 0.45));
    ctx.save();
    ctx.rotate(mid);
    ctx.font = `bold ${fontSize}px "Segoe UI", system-ui, sans-serif`;
    const width = ctx.measureText(s.text).width;
    if (width > r * 0.72) {
      fontSize = Math.max(min, fontSize * r * 0.72 / width);
      ctx.font = `bold ${fontSize}px "Segoe UI", system-ui, sans-serif`;
    }
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

// the biggest font size from max down to min the text fits the width at (set as the font), for fit() to cut it if even
// the min doesn't fit
function fitSize(text, max, min, width) {
  ctx.font = `bold ${max}px "Segoe UI", system-ui, sans-serif`;
  const tw = ctx.measureText(text).width;
  const size = tw > width ? Math.max(min, max * width / tw) : max;
  ctx.font = `bold ${size}px "Segoe UI", system-ui, sans-serif`;
  return size;
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
