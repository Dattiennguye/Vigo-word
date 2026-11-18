// Gọi API máy chủ cục bộ. Mọi logic trò chơi nằm ở Vigo.Core; đây chỉ là lớp mỏng.
const api = {
  _n: 0, _p: new Map(),
  async call(method, url, body) {
    // Bản Android: lõi C# chạy ngay trong ứng dụng, gọi qua cầu nối thay vì mạng.
    if (window.VigoNative) return new Promise((res) => { const id = ++this._n; this._p.set(id, res); VigoNative.call(id, method, url, body === undefined ? '' : JSON.stringify(body)); });
    const r = await fetch(url, { method, headers: { 'content-type': 'application/json' }, body: body === undefined ? undefined : JSON.stringify(body) });
    let j = null; try { j = await r.json(); } catch (e) { j = { ok: false, error: 'Máy chủ không phản hồi' }; }
    if (!r.ok && j && j.ok === undefined) j.ok = false;
    return j;
  },
  get(url) { return this.call('GET', url); },
  post(url, body) { return this.call('POST', url, body || {}); }
};
const $ = (id) => document.getElementById(id);
const fmt = (n) => { n = Math.round(n); const a = Math.abs(n); if (a >= 1e9) return (n / 1e9).toFixed(1) + ' tỷ'; if (a >= 1e6) return (n / 1e6).toFixed(1) + ' tr'; if (a >= 1e4) return Math.round(n / 1e3) + ' k'; return n.toLocaleString('vi-VN'); };
const pct = (v) => Math.round(v * 100) + '%';
function hsl(h, s, l) { return `hsl(${Math.round(h) % 360},${s}%,${l}%)`; }
// Màu quốc gia: tươi, dễ phân biệt (góc vàng) — dùng cho cả bản đồ lẫn giao diện.
function countryHue(id) { return (id * 137.508 + 20) % 360; }
function countryColor(id, l = 50, s = 42) { return hsl(countryHue(id), s, l); }

window.__vigoDone = async (id, status) => {
  const res = api._p.get(id); api._p.delete(id); let j;
  try { j = await (await fetch('/__r/' + id)).json(); } catch (e) { j = { ok: false, error: 'Lỗi nội bộ' }; }
  if (status >= 400 && j && j.ok === undefined) j.ok = false; if (res) res(j);
};
// Nút Back của Android: đóng thành phố/hộp thoại nếu đang mở; trả false để ứng dụng thu nhỏ.
window.__vigoBack = () => {
  const cv = document.getElementById('cityview'); if (cv && !cv.classList.contains('hidden')) { const b = document.getElementById('cityback'); if (b) { b.click(); return true; } }
  const m = document.getElementById('modal'); if (m && !m.classList.contains('hidden')) { const x = m.querySelector('button'); if (x) { x.click(); return true; } }
  return false;
};
