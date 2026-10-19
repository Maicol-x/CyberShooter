export const SPEC = Object.freeze({id: 'ignition-v001', width: 256, height: 256, fps: 30, duration: 3, frameCount: 90, columns: 10, rows: 9, anchor: [0.5, 0.5], loop: false});
export const PALETTES = Object.freeze({cyan: [103, 232, 249], amber: [255, 183, 107], violet: [178, 139, 255]});
const TAU = Math.PI * 2;
const clamp = (v) => Math.max(0, Math.min(1, v));
const smooth = (a, b, t) => { const v = clamp((t - a) / (b - a)); return v * v * (3 - 2 * v); };
const rgba = (rgb, a = 1) => `rgba(${rgb.join(',')},${clamp(a)})`;
const noise = (n) => { const v = Math.sin(n * 127.1 + 311.7) * 43758.5453123; return v - Math.floor(v); };

export function renderEffect(ctx, time, {size = ctx.canvas.width, palette = 'cyan'} = {}) {
  if (!PALETTES[palette]) throw new RangeError('Unknown palette');
  if (!Number.isFinite(time) || !Number.isFinite(size) || size <= 0) throw new RangeError('Invalid render arguments');
  ctx.clearRect(0, 0, size, size);
  if (time <= 0 || time >= 2.86) return;
  const rgb = PALETTES[palette];
  const light = rgb.map(v => Math.round(v + (255 - v) * 0.82));
  const gate = smooth(0, 0.27, time) * (1 - smooth(2.2, 2.86, time));
  const build = smooth(0.08, 0.8, time);
  const charge = smooth(0.5, 1.42, time);
  const fired = smooth(1.4, 1.52, time);
  const flash = Math.exp(-Math.pow((time - 1.53) / 0.15, 2));
  const decay = 1 - smooth(1.72, 2.72, time);
  ctx.save();
  ctx.scale(size / 512, size / 512);
  ctx.translate(256, 256);
  ctx.globalAlpha = gate;
  ctx.lineCap = 'butt';

  const stroke = (r, a, b, width, opacity, color = rgb, glow = 0) => {
    ctx.strokeStyle = rgba(color, opacity); ctx.lineWidth = width;
    ctx.shadowColor = rgba(color, opacity * 0.7); ctx.shadowBlur = glow;
    ctx.beginPath(); ctx.arc(0, 0, r, a, b); ctx.stroke(); ctx.shadowBlur = 0;
  };
  const line = (r1, r2, a, width, opacity, color = rgb, glow = 0) => {
    ctx.strokeStyle = rgba(color, opacity); ctx.lineWidth = width;
    ctx.shadowColor = rgba(color, opacity * 0.7); ctx.shadowBlur = glow;
    ctx.beginPath(); ctx.moveTo(Math.cos(a) * r1, Math.sin(a) * r1); ctx.lineTo(Math.cos(a) * r2, Math.sin(a) * r2); ctx.stroke(); ctx.shadowBlur = 0;
  };
  const haze = ctx.createRadialGradient(0, 0, 4, 0, 0, 180);
  haze.addColorStop(0, rgba(rgb, (0.11 + flash * 0.2) * charge));
  haze.addColorStop(0.6, rgba(rgb, 0.035 * charge)); haze.addColorStop(1, rgba(rgb, 0));
  ctx.fillStyle = haze; ctx.fillRect(-190, -190, 380, 380);
  const rotation = time * 0.35 + time * time * 0.15;
  const ringScale = 0.9 + 0.1 * build;
  stroke(199 * ringScale, 0, TAU, 0.65, 0.3 * build);
  stroke(177 * ringScale, 0, TAU, 0.7, 0.23 * build);
  for (let j = 0; j < 4; j++) {
    const a = rotation + j * TAU / 4;
    stroke(188 * ringScale, a, a + TAU * 0.156 * build, 16, 0.45 + 0.3 * charge, rgb, 14);
    stroke(182 * ringScale, a, a + TAU * 0.156 * build, 1.1, 0.78, light, 5);
    stroke(195 * ringScale, a, a + TAU * 0.156 * build, 1.5, 0.72, rgb, 7);
    stroke(187 * ringScale, a, a + TAU * 0.156 * build, 2, 0.65, light, 7);
  }
  for (let j = 0; j < 72; j++) {
    const a = j * TAU / 72 - rotation * 0.18;
    line(210, j % 6 === 0 ? 220 : 216, a, j % 6 === 0 ? 1.7 : 0.8, (j % 6 === 0 ? 0.86 : 0.57) * build, light, j % 6 === 0 ? 3 : 0);
  }
  for (let j = 0; j < 6; j++) {
    const a = -rotation * 1.6 + j * TAU / 6;
    stroke(135, a, a + 0.67 * build, 1.7, 0.48 * build, rgb, 4);
    line(139, 145, a, 1.7, 0.76 * build, light);
  }
  for (let j = 0; j < 90; j++) line(113, 115.5, j * TAU / 90 + rotation, 0.95, 0.48 * charge, rgb);
  // Deterministic inward streaks: no random state, identical time input in both renderers.
  for (let j = 0; j < 38; j++) {
    const phase = ((time * (0.48 + noise(j + 5) * 0.45)) + noise(j + 21)) % 1;
    const radius = 172 - phase * 146;
    const angle = noise(j + 62) * TAU + rotation * 0.12;
    const alpha = Math.sin(phase * Math.PI) * charge * decay * (0.23 + noise(j + 33) * 0.58);
    line(radius, Math.min(181, radius + 4 + phase * 20), angle, j % 5 === 0 ? 1.6 : 0.8, alpha, light, 5);
  }
  for (const r of [31, 44, 57]) stroke(r * (1 + flash * 0.07), 0, TAU, 0.9, (0.36 + flash * 0.46) * charge * decay, light, 5);
  // Four optical rays stay inside the export boundary.
  for (let j = 0; j < 4; j++) {
    const a = j * TAU / 4;
    line(23, 90 + charge * 99, a, 0.7, 0.17 * charge * decay, rgb, 4);
    line(17, 29 + flash * 169, a, 1.1 + flash, (0.27 + flash * 0.7) * charge * decay, light, 10);
  }
  const coreRadius = 12 + charge * 18 + flash * 31;
  const core = ctx.createRadialGradient(0, 0, 0, 0, 0, coreRadius * 2.3);
  core.addColorStop(0, rgba(light, (0.2 + flash * 0.75) * charge * decay));
  core.addColorStop(0.15, rgba(rgb, (0.26 + flash * 0.48) * charge * decay));
  core.addColorStop(0.5, rgba(rgb, 0.13 * charge * decay)); core.addColorStop(1, rgba(rgb, 0));
  ctx.fillStyle = core; ctx.fillRect(-coreRadius * 2.3, -coreRadius * 2.3, coreRadius * 4.6, coreRadius * 4.6);
  ctx.fillStyle = rgba(light, (0.32 + flash * 0.66) * charge * decay);
  ctx.shadowColor = rgba(rgb, 0.9); ctx.shadowBlur = 15 + flash * 12;
  ctx.beginPath(); ctx.arc(0, 0, 2 + charge * 4 + flash * 7, 0, TAU); ctx.fill(); ctx.shadowBlur = 0;
  if (fired > 0) {
    const wave = clamp((time - 1.49) / 0.66);
    const waveAlpha = (1 - wave) * 0.66 * fired;
    stroke(38 + wave * 161, 0, TAU, 2.8 - wave * 2, waveAlpha, light, 9);
    stroke(31 + wave * 167, 0, TAU, 0.75, waveAlpha * 0.43, rgb, 5);
  }
  ctx.restore();
}
