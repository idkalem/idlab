import { dotnet } from './_framework/dotnet.js';

const canvas = document.getElementById('canvas');
const loader = document.getElementById('loader');
const status = document.getElementById('status');

function fail(text) {
  const box = document.getElementById('error');
  box.style.display = 'block';
  box.textContent = 'ID Lab bir hatayla durdu. Sayfayı yenileyin.\n\n' + text;
  loader.style.display = 'none';
}

// Pointer state for the app, collected between frames (mouse, finger and pen alike).
const pointer = { x: 0, y: 0, down: false, presses: 0, releases: 0, pressX: 0, pressY: 0, id: null };
function toCanvas(e) {
  const r = canvas.getBoundingClientRect();
  return [(e.clientX - r.left) * canvas.width / Math.max(1, r.width), (e.clientY - r.top) * canvas.height / Math.max(1, r.height)];
}
canvas.addEventListener('pointerdown', e => {
  if (pointer.id !== null && pointer.id !== e.pointerId) return; // the first finger drives the app
  if (e.pointerType === 'mouse' && e.button !== 0) return;
  pointer.id = e.pointerId;
  [pointer.x, pointer.y] = toCanvas(e);
  [pointer.pressX, pointer.pressY] = [pointer.x, pointer.y];
  pointer.down = true; pointer.presses++;
  try { canvas.setPointerCapture(e.pointerId); } catch { }
  canvas.focus();
});
canvas.addEventListener('pointermove', e => {
  if (pointer.id !== null && pointer.id !== e.pointerId) return;
  [pointer.x, pointer.y] = toCanvas(e);
});
const release = e => {
  if (pointer.id !== e.pointerId) return;
  [pointer.x, pointer.y] = toCanvas(e);
  pointer.down = false; pointer.releases++; pointer.id = null;
};
canvas.addEventListener('pointerup', release);
canvas.addEventListener('pointercancel', release);

try {
  const { setModuleImports, getAssemblyExports, getConfig, runMain } = await dotnet
    .withModuleConfig({ canvas })
    .create();

  setModuleImports('main.js', {
    toggleFullscreen: () => {
      if (document.fullscreenElement) document.exitFullscreen();
      else document.documentElement.requestFullscreen().catch(() => {});
    },
    isFullscreen: () => !!document.fullscreenElement,
    downloadText: (name, text) => {
      const url = URL.createObjectURL(new Blob([text], { type: 'text/csv;charset=utf-8' }));
      const a = Object.assign(document.createElement('a'), { href: url, download: name });
      document.body.appendChild(a); a.click(); a.remove();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
    },
    pollPointer: () => {
      const p = pointer, out = [p.x, p.y, p.down ? 1 : 0, p.presses, p.releases, p.pressX, p.pressY];
      p.presses = 0; p.releases = 0;
      return out;
    },
    loadSettings: () => { try { return localStorage.getItem('idlab.settings') || ''; } catch { return ''; } },
    saveSettings: (text) => { try { localStorage.setItem('idlab.settings', text); } catch { } },
    ready: () => {
      window.idlabReadyMs = performance.now();
      loader.style.opacity = '0'; setTimeout(() => loader.remove(), 400);
    },
    showError: (text) => fail(text),
  });

  status.textContent = 'Başlatılıyor…';
  const exports = await getAssemblyExports(getConfig().mainAssemblyName);
  // The app draws its own start-up screen once the window exists.
  const width = Math.max(320, window.innerWidth), height = Math.max(240, window.innerHeight);
  const args = ['--windowed', `${width}x${height}`];
  // Optional: ?demo=<name> opens a prepared scene (also used for testing), ?quality=hizli|dengeli|yuksek|otomatik.
  const query = new URLSearchParams(location.search);
  for (const key of ['demo', 'quality', 'topic']) if (query.get(key)) args.push('--' + key, query.get(key));
  if (query.has('profile')) args.push('--profile');
  await runMain(getConfig().mainAssemblyName, args);
  loader.style.background = 'transparent';
  loader.style.pointerEvents = 'none';
  loader.innerHTML = '';
  canvas.focus();

  const frame = () => { exports.IDLab.Web.WebMain.Frame(); requestAnimationFrame(frame); };
  requestAnimationFrame(frame);
} catch (e) {
  fail(String(e && e.stack || e));
}
