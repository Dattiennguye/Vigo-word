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
    this.cv = $('citycanvas'); this.ctx = this.cv.getContext('2d'); this.resize(); this.fit(); this.buildTools(); this.bind(); this.running = true; this.dirty = true; this.ver = (this.ver || 0) + 1; this.T = null; this.cars = null; this.hud(); if (!this.data.edges.length) this.say('Bắt đầu: chọn 🛣 Đường rồi kéo từ cổng ⛩ Quốc lộ vào trong, sau đó quy hoạch hai bên đường. Bấm ▶ để thời gian chạy.'); else this.say('');
    if (!this._loop) { this._loop = true; let last = 0; const f = (t) => { if (this.running && t - last > 30) { last = t; this.dirty = false; this.draw(t); } requestAnimationFrame(f); }; requestAnimationFrame(f); }
    return true;
  },
  close() { this.running = false; $('cityview').classList.add('hidden'); },
  setData(c) { this.data = c; this.ver = (this.ver || 0) + 1; this.cars = null; this.hud(); this.dirty = true; },
  resize() { const r = this.cv.getBoundingClientRect(); const d = Math.min(2, window.devicePixelRatio || 1); this.cv.width = Math.round(r.width * d); this.cv.height = Math.round(r.height * d); this.dpr = d; this.w = r.width; this.h = r.height; this.dirty = true; },
  fit() {
    const S = this.data.size; this.k = Math.min(this.w / (S * 2.15), this.h / (S * 1.15)); this.ox = this.w / 2; this.oy = (this.h - S * this.k) / 2 + 10; this.minK = this.k * 0.7;
    // đã có công trình: phóng to vào khu vực đã xây
    const pts = this.data.nodes; if (pts.length > 3) {
      let x0 = 1e9, y0 = 1e9, x1 = -1e9, y1 = -1e9; for (const [x, y] of pts) { x0 = Math.min(x0, x); y0 = Math.min(y0, y); x1 = Math.max(x1, x); y1 = Math.max(y1, y); }
      const pad = 55; x0 -= pad; y0 -= pad; x1 += pad; y1 += pad; const cx = (x0 + x1) / 2, cy = (y0 + y1) / 2;
      const wIso = ((x1 - x0) + (y1 - y0)), hIso = ((x1 - x0) + (y1 - y0)) * 0.5; const k = Math.min((this.w - 330) / wIso, (this.h - 140) / hIso); this.k = Math.max(this.minK, Math.min(this.minK * 6, k));
      this.ox = this.w / 2 - (cx - cy) * this.k; this.oy = this.h / 2 - (cx + cy) * this.k * 0.5;
    }
  },

  sx(x, y) { return this.ox + (x - y) * this.k; }, sy(x, y, z) { return this.oy + (x + y) * this.k * 0.5 - ((z || 0) + (this.zo || 0)) * this.k; },
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
  }
};
