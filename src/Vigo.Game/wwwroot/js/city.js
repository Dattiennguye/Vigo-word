// Thành phố chi tiết: góc nhìn isometric hoạt hình, công cụ xây đường/khu vực/dịch vụ.
const ROAD = [
  { name: 'Đường nhỏ', w: 7, fill: '#e9d9b8', cost: 'Local' }, { name: 'Phố', w: 10, fill: '#d8d3cc', cost: 'Urban' },
  { name: 'Đại lộ', w: 15, fill: '#c7c6d6', cost: 'Avenue' }, { name: 'Cao tốc', w: 20, fill: '#9ea3c4', cost: 'Highway' }
];
const ZONE = [null, { name: 'Nhà ở thấp', col: '#ffb3c7' }, { name: 'Nhà ở cao', col: '#a9d4ff' }, { name: 'Thương mại', col: '#ffe27a' }, { name: 'Công nghiệp', col: '#c9b3ff' }];
const SERVICE = [{ name: '🏫 Trường học', i: 0 }, { name: '🏥 Bệnh viện', i: 1 }, { name: '👮 Công an', i: 2 }, { name: '🌳 Công viên', i: 3 }];

const CityView = {
  cv: null, ctx: null, id: -1, data: null, k: 0.6, ox: 0, oy: 0, tool: { kind: 'pan', v: 0 }, drag: null, hover: null, dirty: true, running: false, radius: 40,

  async open(id, demo) {
    this.id = id; const r = await api.post('/api/city/open', { id, demo: demo ? 1 : 0 });
    if (!r.ok) { toast(r.error || 'Không mở được thành phố', 'bad'); return false; }
    this.data = r.city; $('cityview').classList.remove('hidden'); $('cityname').textContent = '🏙 ' + this.data.name;
    this.cv = $('citycanvas'); this.ctx = this.cv.getContext('2d'); this.resize(); this.fit(); this.buildTools(); this.bind(); this.running = true; this.dirty = true; this.hud(); if (!this.data.edges.length) this.say('Bắt đầu: chọn 🛣 Đường rồi kéo từ cổng ⛩ Quốc lộ vào trong, sau đó quy hoạch hai bên đường. Bấm ▶ để thời gian chạy.'); else this.say('');
    if (!this._loop) { this._loop = true; const f = () => { if (this.running && this.dirty) { this.dirty = false; this.draw(); } requestAnimationFrame(f); }; requestAnimationFrame(f); }
    return true;
  },
  close() { this.running = false; $('cityview').classList.add('hidden'); },
  setData(c) { this.data = c; this.hud(); this.dirty = true; },
  resize() { const r = this.cv.getBoundingClientRect(); const d = Math.min(2, window.devicePixelRatio || 1); this.cv.width = Math.round(r.width * d); this.cv.height = Math.round(r.height * d); this.dpr = d; this.w = r.width; this.h = r.height; this.dirty = true; },
  fit() {
    const S = this.data.size; this.k = Math.min(this.w / (S * 2.15), this.h / (S * 1.15)); this.ox = this.w / 2; this.oy = (this.h - S * this.k) / 2 + 10; this.minK = this.k * 0.7;
    // đã có công trình: phóng to vào khu vực đã xây
    const pts = this.data.nodes; if (pts.length > 3) {
      let x0 = 1e9, y0 = 1e9, x1 = -1e9, y1 = -1e9; for (const [x, y] of pts) { x0 = Math.min(x0, x); y0 = Math.min(y0, y); x1 = Math.max(x1, x); y1 = Math.max(y1, y); }
      const pad = 90; x0 -= pad; y0 -= pad; x1 += pad; y1 += pad; const cx = (x0 + x1) / 2, cy = (y0 + y1) / 2;
      const wIso = ((x1 - x0) + (y1 - y0)), hIso = ((x1 - x0) + (y1 - y0)) * 0.5; const k = Math.min((this.w - 420) / wIso, (this.h - 140) / hIso); this.k = Math.max(this.minK, Math.min(this.minK * 6, k));
      this.ox = this.w / 2 - (cx - cy) * this.k; this.oy = this.h / 2 - (cx + cy) * this.k * 0.5;
    }
  },

  sx(x, y) { return this.ox + (x - y) * this.k; }, sy(x, y, z) { return this.oy + (x + y) * this.k * 0.5 - (z || 0) * this.k; },
  inv(px, py) { const u = (px - this.ox) / this.k, v = (py - this.oy) / (this.k * 0.5); return [(u + v) / 2, (v - u) / 2]; },

  buildTools() {
    const t = $('citytools'); let h = '<div class="grp">Điều khiển</div><button class="btn sel" data-k="pan">✋ Kéo bản đồ</button>';
    h += '<div class="grp">Đường</div>' + ROAD.map((r, i) => `<button class="btn" data-k="road" data-v="${i}">🛣 ${r.name}</button>`).join('');
    h += '<div class="grp">Phân khu</div>' + [1, 2, 3, 4].map((z) => `<button class="btn" data-k="zone" data-v="${z}"><span style="display:inline-block;width:12px;height:12px;border:2px solid #2b2350;border-radius:4px;background:${ZONE[z].col}"></span> ${ZONE[z].name}</button>`).join('');
    h += '<div class="grp">Dịch vụ</div>' + SERVICE.map((s) => `<button class="btn" data-k="service" data-v="${s.i}">${s.name}</button>`).join('');
    h += '<div class="grp">Khác</div><button class="btn" data-k="demolish">💥 Phá bỏ</button>';
    t.innerHTML = h; t.querySelectorAll('button').forEach((b) => b.onclick = () => { this.tool = { kind: b.dataset.k, v: +(b.dataset.v || 0) }; t.querySelectorAll('button').forEach((x) => x.classList.toggle('sel', x === b)); this.say(this.toolHelp()); });
  },
  toolHelp() { const k = this.tool.kind; return k === 'road' ? 'Nhấn giữ và kéo từ điểm đầu đến điểm cuối để xây đường' : k === 'zone' ? 'Bấm hoặc quét chuột để quy hoạch khu vực quanh con đường' : k === 'service' ? 'Bấm gần một con đường để đặt công trình' : k === 'demolish' ? 'Bấm vào công trình/lô đất để phá' : 'Kéo để di chuyển, lăn chuột để phóng to'; },
  say(t) { $('citymsg').textContent = t || ''; },

  bind() {
    if (this._bound) return; this._bound = true; const cv = this.cv;
    cv.addEventListener('pointerdown', (e) => {
      cv.setPointerCapture(e.pointerId); const [x, y] = this.inv(e.offsetX, e.offsetY); const k = this.tool.kind;
      if (k === 'pan' || e.button === 2) this.drag = { pan: true, px: e.offsetX, py: e.offsetY, ox: this.ox, oy: this.oy };
      else if (k === 'road') this.drag = { road: true, x1: x, y1: y, x2: x, y2: y };
      else if (k === 'zone') { this.drag = { zone: true, last: 0 }; this.act('zone', x, y); }
      else this.act(k, x, y);
    });
    cv.addEventListener('pointermove', (e) => {
      const [x, y] = this.inv(e.offsetX, e.offsetY); this.hover = [x, y, e.offsetX, e.offsetY]; this.dirty = true; const d = this.drag; if (!d) return;
      if (d.pan) { this.ox = d.ox + e.offsetX - d.px; this.oy = d.oy + e.offsetY - d.py; }
      else if (d.road) { d.x2 = x; d.y2 = y; }
      else if (d.zone && performance.now() - d.last > 120) { d.last = performance.now(); this.act('zone', x, y); }
    });
    const up = async (e) => { const d = this.drag; this.drag = null; if (d && d.road) { const dist = Math.hypot(d.x2 - d.x1, d.y2 - d.y1); if (dist > 20) await this.act('road', d.x1, d.y1, d.x2, d.y2); } this.dirty = true; };
    cv.addEventListener('pointerup', up); cv.addEventListener('pointercancel', up); cv.addEventListener('contextmenu', (e) => e.preventDefault());
    cv.addEventListener('wheel', (e) => { e.preventDefault(); const [wx, wy] = this.inv(e.offsetX, e.offsetY); this.k = Math.max(this.minK, Math.min(this.minK * 8, this.k * (e.deltaY < 0 ? 1.15 : 1 / 1.15))); this.ox = e.offsetX - (wx - wy) * this.k; this.oy = e.offsetY - (wx + wy) * this.k * 0.5; this.dirty = true; }, { passive: false });
    window.addEventListener('resize', () => { if (this.running) { this.resize(); this.dirty = true; } });
  },

  async act(kind, x, y, x2, y2) {
    if (this.busy) return; this.busy = true;
    try {
      const body = { id: this.id, cmd: kind, x, y };
      if (kind === 'road') { Object.assign(body, { x1: x, y1: y, x2, y2, type: this.tool.v }); }
      if (kind === 'zone') { body.zone = this.tool.v; body.r = this.radius; }
      if (kind === 'service') body.kind = this.tool.v;
      const r = await api.post('/api/city/cmd', body);
      if (r.city) this.setData(r.city);
      if (!r.ok) this.say('⚠ ' + (r.error || 'Không thực hiện được')); else this.say(kind === 'road' ? 'Đã xây đường' : kind === 'zone' ? `Đã quy hoạch ${r.count} lô` : kind === 'service' ? 'Đã đặt công trình' : 'Đã phá bỏ');
    } finally { this.busy = false; }
  },

  hud() {
    const i = this.data.indicators; const bar = (n, v, c) => `<div class="barrow"><span>${n}</span><div class="bar"><i style="width:${Math.round(Math.max(0, Math.min(1, v)) * 100)}%;background:${c}"></i></div><b>${Math.round(v * 100)}</b></div>`;
    $('cityhud').innerHTML = `<div class="big">${fmt(i.pop)} <small style="font-size:13px">dân</small></div><div style="margin:2px 0 6px">😊 Chất lượng sống <b>${Math.round(i.qol * 100)}</b> <small>(20% nghèo nhất ${Math.round(i.qolLow * 100)})</small></div>`
      + bar('Việc làm', 1 - i.unemployment, '#4fcf3a') + bar('Y tế', i.health, '#ff5d8f') + bar('Giáo dục', i.education, '#4fb6ff') + bar('An ninh', i.safety, '#7a5cff') + bar('Đi lại', i.commute, '#ff9a3c') + bar('Môi trường', i.environment, '#2fd2c0') + bar('Giải trí', i.leisure, '#ffc93c')
      + '<div style="margin-top:6px;font-size:12px;color:#5b5384">Nhu cầu xây thêm</div>' + bar('Nhà ở', i.demandR, '#ffb3c7') + bar('Thương mại', i.demandC, '#ffe27a') + bar('Công nghiệp', i.demandI, '#c9b3ff')
      + `<div style="margin-top:6px;font-size:12px">Nhà ở ${fmt(i.housing)} · Việc làm ${fmt(i.jobs)}</div>`;
  },

  // ---- vẽ ----
  poly(pts, fill, stroke) { const c = this.ctx; c.beginPath(); pts.forEach((p, i) => i ? c.lineTo(p[0], p[1]) : c.moveTo(p[0], p[1])); c.closePath(); if (fill) { c.fillStyle = fill; c.fill(); } if (stroke) { c.strokeStyle = stroke; c.lineWidth = 2; c.stroke(); } },
  box(x, y, w, d, h, top, left, right) {
    const c = this.ctx, X = this.sx.bind(this), Y = this.sy.bind(this); const x0 = x - w / 2, y0 = y - d / 2, x1 = x + w / 2, y1 = y + d / 2;
    this.poly([[X(x0, y1), Y(x0, y1, 0)], [X(x1, y1), Y(x1, y1, 0)], [X(x1, y1), Y(x1, y1, h)], [X(x0, y1), Y(x0, y1, h)]], left, '#2b2350');
    this.poly([[X(x1, y1), Y(x1, y1, 0)], [X(x1, y0), Y(x1, y0, 0)], [X(x1, y0), Y(x1, y0, h)], [X(x1, y1), Y(x1, y1, h)]], right, '#2b2350');
    this.poly([[X(x0, y0), Y(x0, y0, h)], [X(x1, y0), Y(x1, y0, h)], [X(x1, y1), Y(x1, y1, h)], [X(x0, y1), Y(x0, y1, h)]], top, '#2b2350');
  },
  roof(x, y, w, d, h, rh, col, colSide) {
    const X = this.sx.bind(this), Y = this.sy.bind(this); const x0 = x - w / 2, y0 = y - d / 2, x1 = x + w / 2, y1 = y + d / 2, ax = x, ay = y;
    this.poly([[X(x0, y1), Y(x0, y1, h)], [X(x1, y1), Y(x1, y1, h)], [X(ax, ay), Y(ax, ay, h + rh)]], colSide, '#2b2350');
    this.poly([[X(x1, y1), Y(x1, y1, h)], [X(x1, y0), Y(x1, y0, h)], [X(ax, ay), Y(ax, ay, h + rh)]], col, '#2b2350');
    this.poly([[X(x0, y0), Y(x0, y0, h)], [X(x1, y0), Y(x1, y0, h)], [X(ax, ay), Y(ax, ay, h + rh)]], col, '#2b2350');
    this.poly([[X(x0, y0), Y(x0, y0, h)], [X(x0, y1), Y(x0, y1, h)], [X(ax, ay), Y(ax, ay, h + rh)]], colSide, '#2b2350');
  },
  shade(hex, f) { const n = parseInt(hex.slice(1), 16); const r = Math.min(255, Math.round((n >> 16) * f)), g = Math.min(255, Math.round(((n >> 8) & 255) * f)), b = Math.min(255, Math.round((n & 255) * f)); return `rgb(${r},${g},${b})`; },
  colbox(x, y, w, d, h, base) { this.box(x, y, w, d, h, this.shade(base, 1.12), this.shade(base, 0.86), this.shade(base, 0.7)); },

  draw() {
    const c = this.ctx, d = this.dpr; c.setTransform(d, 0, 0, d, 0, 0); c.clearRect(0, 0, this.w, this.h); const D = this.data, S = D.size, X = this.sx.bind(this), Y = this.sy.bind(this);
    // nền trời & mặt đất
    const g = c.createLinearGradient(0, 0, 0, this.h); g.addColorStop(0, '#bff0ff'); g.addColorStop(1, '#eafaff'); c.fillStyle = g; c.fillRect(0, 0, this.w, this.h);
    this.poly([[X(0, 0), Y(0, 0, -6)], [X(S, 0), Y(S, 0, -6)], [X(S, S), Y(S, S, -6)], [X(0, S), Y(0, S, -6)]], '#6a8f4a', '#2b2350');
    this.poly([[X(0, 0), Y(0, 0)], [X(S, 0), Y(S, 0)], [X(S, S), Y(S, S)], [X(0, S), Y(0, S)]], '#8be36b', '#2b2350');
    // mảng cỏ sáng/tối xen kẽ cho cảm giác hoạt hình
    c.save(); c.globalAlpha = 0.25; const st = 100; for (let a = 0; a < S; a += st) for (let b = 0; b < S; b += st) if (((a + b) / st) % 2 === 0) this.poly([[X(a, b), Y(a, b)], [X(a + st, b), Y(a + st, b)], [X(a + st, b + st), Y(a + st, b + st)], [X(a, b + st), Y(a, b + st)]], '#3fae4a'); c.restore();
    // nước
    const w = D.water, ws = w.step; for (const id of w.cells) { const gx = id % w.n, gy = Math.floor(id / w.n); const x = gx * ws, y = gy * ws; this.poly([[X(x, y), Y(x, y)], [X(x + ws, y), Y(x + ws, y)], [X(x + ws, y + ws), Y(x + ws, y + ws)], [X(x, y + ws), Y(x, y + ws)]], '#4fc3ff'); }
    // đường (vẽ viền tối trước, rồi lòng đường)
    const edges = D.edges, nodes = D.nodes;
    for (const pass of [0, 1]) for (const e of edges) {
      const a = nodes[e[0]], b = nodes[e[1]], rt = ROAD[e[2]] || ROAD[0]; const wpx = Math.max(2, rt.w * this.k * 1.1);
      c.lineCap = 'round'; c.lineJoin = 'round'; c.beginPath(); c.moveTo(X(a[0], a[1]), Y(a[0], a[1])); c.lineTo(X(b[0], b[1]), Y(b[0], b[1]));
      if (pass === 0) { c.strokeStyle = '#2b2350'; c.lineWidth = wpx + 4; c.stroke(); } else { c.strokeStyle = e[3] ? '#e0a96b' : rt.fill; c.lineWidth = wpx; c.stroke(); if (e[2] >= 2) { c.setLineDash([8, 8]); c.strokeStyle = '#fff'; c.lineWidth = 2; c.stroke(); c.setLineDash([]); } }
    }
    // cổng nối quốc lộ (nút số 0): nhà chỉ có người ở khi đường nối được tới đây
    if (nodes.length) { const g0 = nodes[0], gx = X(g0[0], g0[1]), gy = Y(g0[0], g0[1]); c.lineWidth = 3; c.strokeStyle = '#2b2350'; c.fillStyle = '#ffc93c'; c.beginPath(); c.arc(gx, gy - 4, 11, 0, 6.3); c.fill(); c.stroke(); c.fillStyle = '#2b2350'; c.font = '700 14px "Trebuchet MS",sans-serif'; c.textAlign = 'center'; c.fillText('⛩', gx, gy + 1); c.lineWidth = 4; c.strokeStyle = 'rgba(255,255,255,.95)'; c.font = '800 12px "Trebuchet MS",sans-serif'; c.strokeText('Quốc lộ', gx, gy - 22); c.fillText('Quốc lộ', gx, gy - 22); }
    // lô đất đã quy hoạch (trống) và công trình, vẽ theo độ sâu
    const items = []; 
    const pmap = new Map(); for (const p of D.parcels) { if (p[3] >= 0) pmap.set(p[3], p); else if (p[2] !== 0) this.pad(p[0], p[1], ZONE[p[2]].col); }
    for (const b of D.buildings) { const p = pmap.get(b[0]); if (p) items.push({ x: p[0], y: p[1], b }); }
    items.sort((a, b) => (a.x + a.y) - (b.x + b.y));
    for (const it of items) this.building(it.x, it.y, it.b);
    // xem trước thao tác
    if (this.drag && this.drag.road) { const d2 = this.drag; c.setLineDash([10, 8]); c.strokeStyle = '#ff5d8f'; c.lineWidth = Math.max(4, ROAD[this.tool.v].w * this.k); c.beginPath(); c.moveTo(X(d2.x1, d2.y1), Y(d2.x1, d2.y1)); c.lineTo(X(d2.x2, d2.y2), Y(d2.x2, d2.y2)); c.stroke(); c.setLineDash([]); }
    if (this.hover && (this.tool.kind === 'zone' || this.tool.kind === 'service')) { const [hx, hy] = this.hover; const r = this.tool.kind === 'zone' ? this.radius : 14; c.strokeStyle = '#2b2350'; c.fillStyle = 'rgba(255,255,255,.35)'; c.lineWidth = 3; c.beginPath(); c.ellipse(X(hx, hy), Y(hx, hy), r * this.k * 1.414, r * this.k * 0.707, 0, 0, 6.3); c.fill(); c.stroke(); }
  },
  pad(x, y, col) { const X = this.sx.bind(this), Y = this.sy.bind(this), r = 7; this.poly([[X(x - r, y - r), Y(x - r, y - r)], [X(x + r, y - r), Y(x + r, y - r)], [X(x + r, y + r), Y(x + r, y + r)], [X(x - r, y + r), Y(x - r, y + r)]], col, 'rgba(43,35,80,.6)'); },

  building(x, y, b) {
    const kind = b[1], lv = b[2]; const c = this.ctx, X = this.sx.bind(this), Y = this.sy.bind(this);
    const sh = (r) => { c.fillStyle = 'rgba(43,35,80,.18)'; c.beginPath(); c.ellipse(X(x + 3, y + 3), Y(x + 3, y + 3), r * this.k * 1.4, r * this.k * 0.7, 0, 0, 6.3); c.fill(); };
    if (kind === 0) { sh(9); const h = 8 + lv * 3; this.colbox(x, y, 13, 13, h, '#fff1d6'); this.roof(x, y, 15, 15, h, 8, '#ff6b6b', '#e24b4b'); }
    else if (kind === 1) { sh(11); const h = 18 + lv * 12; this.colbox(x, y, 15, 15, h, '#6fb6ff'); c.fillStyle = '#fff'; for (let f = 0; f < lv * 2 + 1; f++) { const z = 5 + f * 6; if (z > h - 3) break; c.fillRect(X(x + 7.5, y) - 5 * this.k, Y(x + 7.5, y, z) - 2, 3 * this.k + 2, 3 * this.k + 2); } }
    else if (kind === 2) { sh(10); const h = 9 + lv * 5; this.colbox(x, y, 15, 15, h, '#ffd54a'); this.box(x, y + 8.5, 15, 3, 3, '#ff5d8f', '#ff5d8f', '#d94677'); }
    else if (kind === 3) { sh(11); const h = 10 + lv * 3; this.colbox(x, y, 16, 14, h, '#b8a6ff'); this.colbox(x - 4, y - 3, 3, 3, h + 8, '#6b5c9e'); c.fillStyle = 'rgba(255,255,255,.7)'; c.beginPath(); c.arc(X(x - 4, y - 3), Y(x - 4, y - 3, h + 12 + (performance.now() / 400 % 3)), 4 * this.k + 2, 0, 6.3); c.fill(); }
    else if (kind === 4) { sh(10); this.colbox(x, y, 16, 14, 14, '#ff9a6b'); this.roof(x, y, 18, 16, 14, 6, '#7a5cff', '#5f43d6'); c.fillStyle = '#fff'; c.font = `${Math.max(10, 11 * this.k)}px sans-serif`; c.textAlign = 'center'; c.fillText('🏫', X(x, y), Y(x, y, 30)); }
    else if (kind === 5) { sh(11); this.colbox(x, y, 18, 16, 22, '#ffffff'); c.font = `${Math.max(12, 14 * this.k)}px sans-serif`; c.textAlign = 'center'; c.fillText('🏥', X(x, y), Y(x, y, 34)); }
    else if (kind === 6) { sh(10); this.colbox(x, y, 15, 13, 14, '#6ea8ff'); c.font = `${Math.max(12, 14 * this.k)}px sans-serif`; c.textAlign = 'center'; c.fillText('👮', X(x, y), Y(x, y, 26)); }
    else { c.fillStyle = '#5fd35a'; c.strokeStyle = '#2b2350'; c.lineWidth = 2; c.beginPath(); c.ellipse(X(x, y), Y(x, y), 11 * this.k * 1.4, 11 * this.k * 0.7, 0, 0, 6.3); c.fill(); c.stroke(); for (const [dx, dy] of [[-4, -2], [4, 2], [0, 5]]) { this.colbox(x + dx, y + dy, 2, 2, 5, '#9b6a3a'); c.fillStyle = '#2fae3c'; c.beginPath(); c.arc(X(x + dx, y + dy), Y(x + dx, y + dy, 11), 5 * this.k + 2, 0, 6.3); c.fill(); c.stroke(); } }
  }
};
