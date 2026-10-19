'use strict';
(() => {
  const canvas = document.querySelector('#effect');
  const ctx = canvas.getContext('2d');
  const play = document.querySelector('#play');
  const reset = document.querySelector('#reset');
  const slider = document.querySelector('#frame');
  const status = document.querySelector('#status');
  const cache = new Map();
  const base = document.body.dataset.assets;
  let color = 'cyan', frame = 45, running = false, ready = false, token = 0;
  let started = 0, raf = 0;

  function pause() {
    running = false;
    cancelAnimationFrame(raf);
    play.textContent = 'IGNITE ↗';
  }
  function draw() {
    ctx.clearRect(0, 0, 256, 256);
    const image = cache.get(color);
    if (ready && image) {
      const r = IGNITION_DATA[color].frames[frame].rect;
      ctx.drawImage(image, r.x, r.y, r.w, r.h, 0, 0, 256, 256);
    }
    slider.value = String(frame);
    document.querySelector('#frame-label').textContent = `${String(frame).padStart(3, '0')} / 089`;
    document.querySelector('#time').textContent = `${(frame / 30).toFixed(2)} s`;
  }
  function tick(now) {
    if (!running) return;
    const elapsed = now - started;
    if (elapsed >= 3000) {
      if (!document.querySelector('#repeat').checked) { frame = 89; pause(); draw(); return; }
      started = now - (elapsed % 3000);
    }
    frame = Math.min(89, Math.floor((now - started) * 30 / 1000));
    draw();
    raf = requestAnimationFrame(tick);
  }
  function ignite() {
    if (!ready) return;
    pause(); frame = 0; running = true; started = performance.now();
    play.textContent = 'PAUSE'; draw(); raf = requestAnimationFrame(tick);
  }
  async function selectColor(next) {
    pause(); ready = false;
    const request = ++token;
    color = next;
    play.disabled = reset.disabled = slider.disabled = true;
    status.hidden = false; status.textContent = 'Loading preview…';
    document.querySelector('#palette-name').textContent = next.toUpperCase();
    document.documentElement.style.setProperty('--accent', {cyan:'#67e8f9',amber:'#ffb76b',violet:'#b28bff'}[next]);
    document.querySelectorAll('[data-color]').forEach(b => b.setAttribute('aria-pressed', String(b.dataset.color === next)));
    ctx.clearRect(0, 0, 256, 256);
    try {
      if (!cache.has(next)) {
        const image = new Image();
        await new Promise((resolve, reject) => {
          image.onload = resolve;
          image.onerror = () => reject(new Error('Sprite sheet unavailable'));
          image.src = `${base}${next}/${IGNITION_DATA[next].sheet}`;
        });
        if (image.naturalWidth !== 2560 || image.naturalHeight !== 2304) throw new Error('Unexpected sprite dimensions');
        cache.set(next, image);
      }
      if (request !== token) return;
      ready = true; play.disabled = reset.disabled = slider.disabled = false;
      status.hidden = true; draw();
    } catch (error) {
      if (request !== token) return;
      status.textContent = 'Preview could not load. Keep the assets folder beside this demo, then reload.';
      console.error(error);
    }
  }
  if (!ctx) { status.textContent = 'Canvas 2D is unavailable in this browser.'; return; }
  play.addEventListener('click', () => running ? pause() : ignite());
  reset.addEventListener('click', () => { pause(); frame = 0; draw(); });
  slider.addEventListener('input', () => { pause(); frame = Number(slider.value); draw(); });
  canvas.addEventListener('click', ignite);
  canvas.addEventListener('keydown', event => { if (event.key === ' ' || event.key === 'Enter') { event.preventDefault(); ignite(); } });
  document.querySelectorAll('[data-color]').forEach(button => button.addEventListener('click', () => selectColor(button.dataset.color)));
  document.querySelectorAll('[data-bg]').forEach(button => button.addEventListener('click', () => {
    document.querySelector('#stage').className = `stage ${button.dataset.bg}`;
    document.querySelectorAll('[data-bg]').forEach(b => b.setAttribute('aria-pressed', String(b === button)));
  }));
  document.addEventListener('visibilitychange', () => { if (document.hidden) pause(); });
  selectColor(color);
})();
