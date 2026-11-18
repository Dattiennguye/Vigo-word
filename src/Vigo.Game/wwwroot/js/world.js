// Bản đồ thế giới: vẽ ô địa lý + chính trị bằng Canvas 2D theo phong cách hoạt hình, có kéo/phóng to.
const BIOME = ['Biển', 'Hồ', 'Băng', 'Lãnh nguyên', 'Rừng thông', 'Thảo nguyên', 'Đồng cỏ', 'Rừng', 'Đầm lầy', 'Sa mạc', 'Xavan', 'Rừng nhiệt đới', 'Cao nguyên', 'Núi'];
const BIOME_COLOR = ['#35566e', '#4a7189', '#e8ecee', '#cdd3b8', '#6f8a55', '#a8a763', '#8aa05a', '#5f7d4a', '#7e9a74', '#d6c28c', '#c49a5a', '#4f7650', '#a6a46c', '#8c8278'];

const WorldView = {
  SC: 1.5, cv: null, ctx: null, M: null, I: null, owners: null, view: { s: 1, tx: 0, ty: 0 },
  hash: null, provCells: [], terrain: null, politic: null, hover: -1, selProv: -1, selCountry: -1, pulse: false,
  onPick: null, player: 0, cityOpen: {}, dirty: true, _ownersSig: '',

  init(map, info) {
    this.M = map; this.I = info; this.cv = $('world'); this.ctx = this.cv.getContext('2d');
    const n = map.n; this.provCells = info.provinces.map(() => []);
    for (let i = 0; i < n; i++) { const p = map.province[i]; if (p >= 0) this.provCells[p].push(i); }
    // băm không gian để chọn ô gần chuột
    const cs = map.spacing * 2; this.hcs = cs; this.hw = Math.ceil(map.width / cs) + 1; this.hh = Math.ceil(map.height / cs) + 1;
    this.hash = Array.from({ length: this.hw * this.hh }, () => []);
    for (let i = 0; i < n; i++) this.hash[Math.floor(map.y[i] / cs) * this.hw + Math.floor(map.x[i] / cs)].push(i);
    this.buildTerrain(); this.owners = null; this._ownersSig = '';
    this.resize(); this.fit(); this.bind(); this.dirty = true;
    if (!this._loop) { this._loop = true; const f = () => { this.frame(); requestAnimationFrame(f); }; requestAnimationFrame(f); }
  },

  resize() {
    const r = this.cv.getBoundingClientRect(); const d = Math.min(2, window.devicePixelRatio || 1);
    this.cv.width = Math.max(10, Math.round(r.width * d)); this.cv.height = Math.max(10, Math.round(r.height * d)); this.dpr = d; this.dirty = true;
  },
  fit() {
    const r = this.cv.getBoundingClientRect(); const s = Math.min(r.width / this.M.width, r.height / this.M.height) * 0.97;
    this.view = { s, tx: (r.width - this.M.width * s) / 2, ty: (r.height - this.M.height * s) / 2 }; this.minS = s * 0.9; this.dirty = true;
  },

  buildTerrain() {
    const M = this.M, SC = this.SC, c = document.createElement('canvas'); c.width = Math.ceil(M.width * SC); c.height = Math.ceil(M.height * SC);
    const g = c.getContext('2d'); const r = M.spacing * SC * 0.95;
    g.fillStyle = '#34536b'; g.fillRect(0, 0, c.width, c.height);
    // biển: sáng gần bờ, đậm ngoài khơi
    for (let i = 0; i < M.n; i++) {
      if (M.elev[i] > 0 && !(M.flags[i] & 2)) continue; const lake = (M.flags[i] & 2) || !(M.flags[i] & 1);
      const d = Math.min(1, Math.max(0, -M.elev[i] / 40));
      g.fillStyle = lake ? '#56798f' : `hsl(${204 - d * 6},${34 - d * 6}%,${44 - d * 16}%)`;
      g.beginPath(); g.arc(M.x[i] * SC, M.y[i] * SC, r * 1.05, 0, 6.3); g.fill();
    }
    // đất: viền tối trước, rồi tô màu quần xã (hoạt hình)
    g.fillStyle = '#3b2a18';
    for (let i = 0; i < M.n; i++) { if (M.elev[i] <= 0 || (M.flags[i] & 2)) continue; g.beginPath(); g.arc(M.x[i] * SC, M.y[i] * SC, r + 1.6, 0, 6.3); g.fill(); }
    for (let i = 0; i < M.n; i++) {
      if (M.elev[i] <= 0 || (M.flags[i] & 2)) continue;
      const base = BIOME_COLOR[M.biome[i]] || '#79d94f'; g.fillStyle = base; g.beginPath(); g.arc(M.x[i] * SC, M.y[i] * SC, r, 0, 6.3); g.fill();
      const h = Math.min(1, M.elev[i] / 70); if (h > 0.45) { g.fillStyle = `rgba(255,255,255,${(h - 0.45) * 0.45})`; g.beginPath(); g.arc(M.x[i] * SC, M.y[i] * SC - r * 0.15, r * 0.7, 0, 6.3); g.fill(); }
      if (M.flags[i] & 4) { g.fillStyle = '#56798f'; g.beginPath(); g.arc(M.x[i] * SC, M.y[i] * SC, r * 0.45, 0, 6.3); g.fill(); }
    }
    this.terrain = c;
  },

  setOwners(owners) {
    const sig = owners.join(','); if (sig === this._ownersSig) return; this._ownersSig = sig; this.owners = owners.slice(); this.buildPolitic();
  },

  buildPolitic() {
    const M = this.M, SC = this.SC; const c = this.politic && this.politic.width === Math.ceil(M.width * SC) ? this.politic : document.createElement('canvas');
    c.width = Math.ceil(M.width * SC); c.height = Math.ceil(M.height * SC); const g = c.getContext('2d'); g.clearRect(0, 0, c.width, c.height);
    const r = M.spacing * SC * 0.95; const own = this.owners;
    const cellOwner = (i) => { const p = M.province[i]; return p >= 0 ? own[p] : -1; };
    for (let i = 0; i < M.n; i++) { const o = cellOwner(i); if (o < 0 || M.elev[i] <= 0 || (M.flags[i] & 2)) continue; g.fillStyle = countryColor(o, 52, 46); g.beginPath(); g.arc(M.x[i] * SC, M.y[i] * SC, r, 0, 6.3); g.fill(); }
    // đường biên giới: chấm tại trung điểm giữa hai ô khác nước / khác tỉnh
    const bc = this.borders && this.borders.width === c.width ? this.borders : document.createElement('canvas'); bc.width = c.width; bc.height = c.height; const gb = bc.getContext('2d'); gb.clearRect(0, 0, bc.width, bc.height);
    const cs = this.hcs, hw = this.hw;
    for (let i = 0; i < M.n; i++) {
      const o = cellOwner(i); if (o < 0 || M.elev[i] <= 0) continue; const cx = Math.floor(M.x[i] / cs), cy = Math.floor(M.y[i] / cs);
      for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
        const gx = cx + dx, gy = cy + dy; if (gx < 0 || gy < 0 || gx >= hw || gy >= this.hh) continue;
        for (const j of this.hash[gy * hw + gx]) {
          if (j <= i || M.elev[j] <= 0) continue; const ddx = M.x[i] - M.x[j], ddy = M.y[i] - M.y[j]; if (ddx * ddx + ddy * ddy > (M.spacing * 1.7) ** 2) continue;
          const o2 = cellOwner(j); if (o2 < 0) continue; const pi = M.province[i], pj = M.province[j];
          if (o2 !== o) { gb.fillStyle = '#3b2a18'; gb.beginPath(); gb.arc((M.x[i] + M.x[j]) * SC / 2, (M.y[i] + M.y[j]) * SC / 2, 1.9, 0, 6.3); gb.fill(); }
          else if (pi !== pj) { gb.fillStyle = 'rgba(43,35,80,.35)'; gb.beginPath(); gb.arc((M.x[i] + M.x[j]) * SC / 2, (M.y[i] + M.y[j]) * SC / 2, 1.0, 0, 6.3); gb.fill(); }
        }
      }
    }
    this.politic = c; this.borders = bc; this.dirty = true;
  },

  setCities(cities) { this.cityState = cities; this.dirty = true; },

  // ---- toạ độ ----
  toScreen(x, y) { return [x * this.view.s + this.view.tx, y * this.view.s + this.view.ty]; },
  toWorld(px, py) { return [(px - this.view.tx) / this.view.s, (py - this.view.ty) / this.view.s]; },
  nearestCell(wx, wy) {
    const M = this.M, cs = this.hcs; const cx = Math.floor(wx / cs), cy = Math.floor(wy / cs); let best = -1, bd = (M.spacing * 2.2) ** 2;
    for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
      const gx = cx + dx, gy = cy + dy; if (gx < 0 || gy < 0 || gx >= this.hw || gy >= this.hh) continue;
      for (const j of this.hash[gy * this.hw + gx]) { const ddx = M.x[j] - wx, ddy = M.y[j] - wy, d = ddx * ddx + ddy * ddy; if (d < bd) { bd = d; best = j; } }
    }
    return best;
  },
  pick(px, py) {
    // thành phố trước (bán kính 11px màn hình)
    let bestC = -1, bd = 12 * 12; const vis = this.cityVisible.bind(this);
    for (const c of this.I.cities) { if (!vis(c)) continue; const [sx, sy] = this.toScreen(c.x, c.y); const d = (sx - px) ** 2 + (sy - py) ** 2; if (d < bd) { bd = d; bestC = c.id; } }
    const [wx, wy] = this.toWorld(px, py); const cell = this.nearestCell(wx, wy);
    const prov = cell >= 0 && this.M.elev[cell] > 0 ? this.M.province[cell] : -1;
    return { city: bestC, prov, cell };
  },
  cityVisible(c) { return c.capital || (c.seat && this.view.s > this.minS * 1.7) || this.view.s > this.minS * 3.2; },

  bind() {
    if (this._bound) return; this._bound = true; const cv = this.cv; let drag = null, moved = false; const ptrs = new Map(); let pinch = null;
    cv.addEventListener('pointerdown', (e) => { cv.setPointerCapture(e.pointerId); ptrs.set(e.pointerId, [e.offsetX, e.offsetY]); moved = false; if (ptrs.size === 1) drag = { x: e.offsetX, y: e.offsetY, tx: this.view.tx, ty: this.view.ty }; else if (ptrs.size === 2) { const a = [...ptrs.values()]; pinch = { d: Math.hypot(a[0][0] - a[1][0], a[0][1] - a[1][1]), s: this.view.s }; } });
    cv.addEventListener('pointermove', (e) => {
      if (ptrs.has(e.pointerId)) ptrs.set(e.pointerId, [e.offsetX, e.offsetY]);
      if (ptrs.size === 2 && pinch) { const a = [...ptrs.values()]; const d = Math.hypot(a[0][0] - a[1][0], a[0][1] - a[1][1]); this.zoomAt((a[0][0] + a[1][0]) / 2, (a[0][1] + a[1][1]) / 2, pinch.s * d / pinch.d, true); moved = true; return; }
      if (drag && ptrs.size === 1) { const dx = e.offsetX - drag.x, dy = e.offsetY - drag.y; if (Math.abs(dx) + Math.abs(dy) > 5) moved = true; if (moved) { this.view.tx = drag.tx + dx; this.view.ty = drag.ty + dy; this.clamp(); this.dirty = true; } }
      if (!drag || !moved) this.updateHover(e.offsetX, e.offsetY);
    });
    const up = (e) => { const was = ptrs.size; ptrs.delete(e.pointerId); if (ptrs.size < 2) pinch = null; if (ptrs.size === 0) { if (!moved && drag && was === 1 && this.onPick) this.onPick(this.pick(e.offsetX, e.offsetY), e); drag = null; } };
    cv.addEventListener('pointerup', up); cv.addEventListener('pointercancel', up);
    cv.addEventListener('wheel', (e) => { e.preventDefault(); this.zoomAt(e.offsetX, e.offsetY, this.view.s * (e.deltaY < 0 ? 1.18 : 1 / 1.18)); }, { passive: false });
    cv.addEventListener('pointerleave', () => { $('tooltip').classList.add('hidden'); });
    window.addEventListener('resize', () => { if (this.cv && this.M) { this.resize(); this.dirty = true; } });
  },
  zoomAt(px, py, s, abs) {
    s = Math.max(this.minS, Math.min(this.minS * 14, s)); const [wx, wy] = this.toWorld(px, py); this.view.s = s; this.view.tx = px - wx * s; this.view.ty = py - wy * s; this.clamp(); this.dirty = true;
  },
  clamp() { const r = this.cv.getBoundingClientRect(), v = this.view, w = this.M.width * v.s, h = this.M.height * v.s; v.tx = Math.min(r.width * 0.5, Math.max(r.width * 0.5 - w, v.tx)); v.ty = Math.min(r.height * 0.5, Math.max(r.height * 0.5 - h, v.ty)); },
  focus(x, y, s) { const r = this.cv.getBoundingClientRect(); this.view.s = Math.max(this.minS, Math.min(this.minS * 14, s || this.view.s)); this.view.tx = r.width / 2 - x * this.view.s; this.view.ty = r.height / 2 - y * this.view.s; this.clamp(); this.dirty = true; },

  updateHover(px, py) {
    const p = this.pick(px, py); const tip = $('tooltip'); let txt = '';
    if (p.city >= 0) { const c = this.I.cities[p.city]; const st = this.cityState && this.cityState[p.city]; txt = `🏠 <b>${c.name}</b>${c.capital ? ' ⭐' : ''}${st ? ' · ' + fmt(st.pop) + ' dân' : ''}`; }
    else if (p.prov >= 0) { const pr = this.I.provinces[p.prov]; const o = this.owners ? this.owners[p.prov] : -1; txt = `<b>${pr.name}</b>${o >= 0 ? ' · ' + this.I.countries[o].name : ''}`; }
    if (p.prov !== this.hover) { this.hover = p.prov; this.dirty = true; }
    if (!txt) { tip.classList.add('hidden'); return; }
    tip.innerHTML = txt; tip.classList.remove('hidden'); tip.style.left = Math.min(px + 14, this.cv.clientWidth - 160) + 'px'; tip.style.top = (py + 14) + 'px';
  },

  frame() {
    if (!this.dirty && !this.pulse) return; this.dirty = false; if (!this.terrain) return;
    const ctx = this.ctx, d = this.dpr, v = this.view; const W = this.cv.width, H = this.cv.height;
    ctx.setTransform(1, 0, 0, 1, 0, 0); ctx.fillStyle = '#34536b'; ctx.fillRect(0, 0, W, H);
    ctx.setTransform(d * v.s / this.SC, 0, 0, d * v.s / this.SC, d * v.tx, d * v.ty); ctx.imageSmoothingQuality = 'high';
    ctx.drawImage(this.terrain, 0, 0); if (this.politic) { ctx.globalAlpha = 0.62; ctx.drawImage(this.politic, 0, 0); ctx.globalAlpha = 1; if (this.borders) ctx.drawImage(this.borders, 0, 0); }
    // làm nổi bật tỉnh/quốc gia đang chọn hoặc rê chuột
    const M = this.M; const hi = (provs, a) => { ctx.fillStyle = `rgba(255,255,255,${a})`; const r = M.spacing * this.SC * 0.95; for (const p of provs) for (const i of this.provCells[p]) { if (M.elev[i] <= 0) continue; ctx.beginPath(); ctx.arc(M.x[i] * this.SC, M.y[i] * this.SC, r, 0, 6.3); ctx.fill(); } };
    if (this.owners) {
      if (this.selCountry >= 0) { const ps = []; this.owners.forEach((o, p) => { if (o === this.selCountry) ps.push(p); }); hi(ps, 0.28 + (this.pulse ? 0.12 * Math.sin(performance.now() / 220) : 0)); }
      if (this.selProv >= 0) hi([this.selProv], 0.38);
      if (this.hover >= 0 && this.hover !== this.selProv) hi([this.hover], 0.18);
    }
    ctx.setTransform(d, 0, 0, d, 0, 0); this.drawCities(ctx); this.drawLabels(ctx);
  },

  drawCities(ctx) {
    const v = this.view; const st = this.cityState;
    for (const c of this.I.cities) {
      const cs = st && st[c.id]; if (!this.cityVisible(c)) continue;
      const [x, y] = this.toScreen(c.x, c.y); const owner = cs ? cs.owner : -1; const mine = owner === this.player; const k = c.capital ? 1.25 : (c.seat ? 1 : 0.8);
      const s = Math.max(5, Math.min(14, 5 + v.s * 1.1)) * k;
      if (mine) { ctx.fillStyle = 'rgba(255,255,255,.65)'; ctx.beginPath(); ctx.arc(x, y, s * 1.25, 0, 6.3); ctx.fill(); }
      // nhà hoạt hình: thân + mái
      ctx.lineWidth = 2; ctx.strokeStyle = '#3b2a18'; ctx.fillStyle = '#fff3d6'; ctx.beginPath(); ctx.rect(x - s * 0.55, y - s * 0.2, s * 1.1, s * 0.8); ctx.fill(); ctx.stroke();
      ctx.fillStyle = owner >= 0 ? countryColor(owner, 44, 50) : '#8c3b2a'; ctx.beginPath(); ctx.moveTo(x - s * 0.8, y - s * 0.15); ctx.lineTo(x, y - s * 0.95); ctx.lineTo(x + s * 0.8, y - s * 0.15); ctx.closePath(); ctx.fill(); ctx.stroke();
      if (c.capital) { ctx.fillStyle = '#d8b25a'; ctx.beginPath(); ctx.arc(x, y - s * 1.2, s * 0.32, 0, 6.3); ctx.fill(); ctx.stroke(); }
      if (cs && cs.open) { ctx.font = `${Math.round(s * 1.1)}px sans-serif`; ctx.textAlign = 'center'; ctx.fillText('🏙', x + s * 1.1, y - s * 0.4); }
      if (v.s > this.minS * 3.2 || c.capital && v.s > this.minS * 1.8) { ctx.font = '700 11px Georgia,serif'; ctx.textAlign = 'center'; ctx.lineWidth = 3; ctx.strokeStyle = 'rgba(255,255,255,.9)'; ctx.strokeText(c.name, x, y + s * 1.3 + 8); ctx.fillStyle = '#3b2a18'; ctx.fillText(c.name, x, y + s * 1.3 + 8); }
    }
  },

  drawLabels(ctx) {
    const v = this.view; if (!this.owners) return; ctx.textAlign = 'center'; ctx.lineJoin = 'round';
    if (v.s < this.minS * 2.6) {
      for (const c of this.I.countries) {
        if (c.elim) continue; const live = this.countryLive && this.countryLive[c.id]; if (live && live.elim) continue;
        const [x, y] = this.toScreen(live && live.cx !== undefined ? live.cx : c.cx, live && live.cy !== undefined ? live.cy : c.cy); const size = Math.max(11, Math.min(20, 9 + v.s * 3));
        ctx.font = `900 ${size}px Georgia,serif`; ctx.lineWidth = 5; ctx.strokeStyle = 'rgba(240,226,190,.92)'; ctx.strokeText(c.name, x, y); ctx.fillStyle = c.id === this.player ? '#d6246b' : '#3b2a18'; ctx.fillText(c.name, x, y);
      }
    } else {
      this.I.provinces.forEach((p, i) => { const [x, y] = this.toScreen(p.cx, p.cy); const size = Math.max(10, Math.min(15, 8 + v.s * 0.8)); ctx.font = `800 ${size}px Georgia,serif`; ctx.lineWidth = 4; ctx.strokeStyle = 'rgba(255,255,255,.9)'; ctx.strokeText(p.name, x, y); ctx.fillStyle = '#3b2a18'; ctx.fillText(p.name, x, y); });
    }
  }
};
