// Đồ hoạ thành phố: địa hình đồi núi, cây cối, đường có vỉa hè/vạch kẻ/xe cộ, công trình nhiều kiểu.
// Phong cách isometric hoạt hình sáng màu (tự vẽ bằng canvas, tham khảo cách bố cục của các game xây thành phố).
(function () {
  const RW = [8, 12, 20, 28]; // bề rộng thật (m) theo RoadSpecs của lõi mô phỏng
  const hh = (a, b, c) => { let h = (Math.imul(a | 0, 374761393) + Math.imul(b | 0, 668265263) + Math.imul((c || 0) | 0, 1274126177)) | 0; h = Math.imul(h ^ (h >>> 13), 1274126177); h ^= h >>> 16; return (h >>> 0) / 4294967296; };
  const sm = (t) => t * t * (3 - 2 * t), cl = (t, a, b) => t < a ? a : t > b ? b : t;
  const vn = (x, y, s, sd) => { const gx = x / s, gy = y / s, ix = Math.floor(gx), iy = Math.floor(gy), fx = sm(gx - ix), fy = sm(gy - iy); const a = hh(ix, iy, sd), b = hh(ix + 1, iy, sd), c = hh(ix, iy + 1, sd), d = hh(ix + 1, iy + 1, sd); return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy; };
  const mixc = (a, b, t) => [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t];
  const rgb = (c, f) => { f = f || 1; return `rgb(${Math.min(255, c[0] * f) | 0},${Math.min(255, c[1] * f) | 0},${Math.min(255, c[2] * f) | 0})`; };
  const hex = (h) => { const n = parseInt(h.slice(1), 16); return [n >> 16, (n >> 8) & 255, n & 255]; };
  const GRASS = [[132, 156, 92], [118, 146, 82]], DARK = [78, 112, 68], ROCK = [150, 138, 122], SNOW = [236, 238, 234], SAND = [214, 196, 146], DIRT = [140, 104, 68];
  const WALLS = ['#e8dcc0', '#d9c8a4', '#cfc3ad', '#e3d2b0', '#c9b48f', '#ddd0b8'], ROOFS = ['#a8452f', '#8c3b2a', '#6e6a6c', '#b5653a', '#7a4a34', '#5d5457'];
  const TOWERS = ['#b9ae98', '#a79c86', '#c4b8a0', '#9d9486', '#b3a58c'], CARS = ['#7a2e24', '#3f4f6b', '#8a7a5a', '#d8cdb4', '#3a4a3a', '#6a5a48', '#9a6a3a', '#2e3238'];
  const OL = 'rgba(48,32,16,.6)';
  const M = 400; // vành đai cảnh quan quanh thành phố (m)

  const Art = {
    // ---------- địa hình ----------
    prepTerrain() {
      const D = this.data, S = D.size, n = Math.round(Math.sqrt(D.slope ? D.slope.length : 0)) || 0, step = n ? S / n : 20, sl = D.slope || [];
      const vz = new Float32Array((n + 1) * (n + 1));
      for (let j = 0; j <= n; j++) for (let i = 0; i <= n; i++) { let s = 0, c = 0; for (let dj = -1; dj <= 0; dj++) for (let di = -1; di <= 0; di++) { const a = i + di, b = j + dj; if (a >= 0 && b >= 0 && a < n && b < n) { s += sl[b * n + a]; c++; } } vz[j * (n + 1) + i] = c ? Math.max(0, s / c / 100 - 0.2) * 90 : 0; }
      const T = { n, step, vz, S, seed: (D.seed || 1) | 0 };
      T.city = (x, y) => { if (!n) return 0; const gx = cl(x / step, 0, n - 1e-6), gy = cl(y / step, 0, n - 1e-6), i = gx | 0, j = gy | 0, fx = gx - i, fy = gy - j, w = n + 1; return vz[j * w + i] * (1 - fx) * (1 - fy) + vz[j * w + i + 1] * fx * (1 - fy) + vz[(j + 1) * w + i] * (1 - fx) * fy + vz[(j + 1) * w + i + 1] * fx * fy; };
      T.H = (x, y) => { const d = Math.hypot(Math.max(0, -x, x - S), Math.max(0, -y, y - S)); let z = T.city(x, y); if (d > 0) z += sm(cl(d / M, 0, 1)) * (0.25 + 0.75 * vn(x, y, 170, T.seed)) * 90 + vn(x, y, 55, T.seed + 3) * 4 * sm(cl(d / 80, 0, 1)); return z; };
      // nước (mở rộng ra vành đai theo hướng sông chảy)
      const w = D.water, wn = w.n, wset = new Uint8Array(wn * wn); for (const id of w.cells) wset[id] = 1;
      const R = Math.round(M / w.step), N = wn + 2 * R; T.R = R; T.N = N; T.ws = w.step;
      T.water = (i, j) => { const a = i - R, b = j - R; if (a >= 0 && b >= 0 && a < wn && b < wn) return wset[b * wn + a] === 1; const ca = cl(a, 0, wn - 1), cb = cl(b, 0, wn - 1); const out = Math.max(Math.abs(a - ca), Math.abs(b - cb)); return out <= R * 0.8 && wset[cb * wn + ca] === 1; };
      // lưới chiếm chỗ (10 m) để không đặt cây lên đường/nhà
      const oc = 10, on = Math.ceil(S / oc), occ = new Uint8Array(on * on); T.oc = oc; T.on = on; T.occ = occ;
      const mark = (x, y, r) => { for (let b = Math.floor((y - r) / oc); b <= Math.floor((y + r) / oc); b++) for (let a = Math.floor((x - r) / oc); a <= Math.floor((x + r) / oc); a++) if (a >= 0 && b >= 0 && a < on && b < on) occ[b * on + a] = 1; };
      for (const e of D.edges) { const A = D.nodes[e[0]], B = D.nodes[e[1]], L = Math.hypot(B[0] - A[0], B[1] - A[1]), hw = RW[e[2]] / 2 + 5; for (let t = 0; t <= L; t += 4) { const u = t / L; mark(A[0] + (B[0] - A[0]) * u, A[1] + (B[1] - A[1]) * u, hw); } }
      for (const p of D.parcels) mark(p[0], p[1], p[2] === 0 ? 8 : 12);
      this.T = T; T.ver = this.ver;
    },

    // ---------- tiện ích hình học ----------
    P(x, y, z) { return [this.sx(x, y), this.sy(x, y, z)]; },
    polyW(pts, z, fill, stroke, lw) { const c = this.ctx; c.beginPath(); for (let i = 0; i < pts.length; i++) { const p = pts[i], X = this.sx(p[0], p[1]), Y = this.sy(p[0], p[1], p[2] === undefined ? z : p[2]); i ? c.lineTo(X, Y) : c.moveTo(X, Y); } c.closePath(); if (fill) { c.fillStyle = fill; c.fill(); } if (stroke) { c.strokeStyle = stroke; c.lineWidth = lw || 1; c.stroke(); } },
    prism(P, z0, z1, base, opt) {
      const c = this.ctx; let cx = 0, cy = 0; for (const p of P) { cx += p[0]; cy += p[1]; } cx /= P.length; cy /= P.length;
      const baseRgb = typeof base === 'string' ? hex(base) : base, lw = (opt && opt.lw) || 1;
      for (let i = 0; i < P.length; i++) {
        const a = P[i], b = P[(i + 1) % P.length]; let nx = b[1] - a[1], ny = -(b[0] - a[0]); const mx = (a[0] + b[0]) / 2 - cx, my = (a[1] + b[1]) / 2 - cy; if (nx * mx + ny * my < 0) { nx = -nx; ny = -ny; }
        const l = Math.hypot(nx, ny) || 1; nx /= l; ny /= l; if (nx + ny <= 0.02) continue;
        const f = 0.81 + 0.15 * (-0.6 * nx + 0.8 * ny);
        c.beginPath(); c.moveTo(this.sx(a[0], a[1]), this.sy(a[0], a[1], z0)); c.lineTo(this.sx(b[0], b[1]), this.sy(b[0], b[1], z0)); c.lineTo(this.sx(b[0], b[1]), this.sy(b[0], b[1], z1)); c.lineTo(this.sx(a[0], a[1]), this.sy(a[0], a[1], z1)); c.closePath(); c.fillStyle = rgb(baseRgb, f); c.fill(); if (!(opt && opt.noline)) { c.strokeStyle = OL; c.lineWidth = lw; c.stroke(); }
      }
      c.beginPath(); P.forEach((p, i) => { const X = this.sx(p[0], p[1]), Y = this.sy(p[0], p[1], z1); i ? c.lineTo(X, Y) : c.moveTo(X, Y); }); c.closePath(); c.fillStyle = (opt && opt.top) || rgb(baseRgb, 1.1); c.fill(); if (!(opt && opt.noline)) { c.strokeStyle = OL; c.lineWidth = lw; c.stroke(); }
    },
    rect(x, y, w, d) { return [[x - w / 2, y - d / 2], [x + w / 2, y - d / 2], [x + w / 2, y + d / 2], [x - w / 2, y + d / 2]]; },
    // ô cửa/chi tiết trên một mặt tường: a→b là cạnh chân tường, u,v theo tỉ lệ ngang/cao (mét cho v)
    fq(a, b, u0, u1, z0, z1, col, line) { const c = this.ctx, X = (u, z) => [this.sx(a[0] + (b[0] - a[0]) * u, a[1] + (b[1] - a[1]) * u), this.sy(a[0] + (b[0] - a[0]) * u, a[1] + (b[1] - a[1]) * u, z)]; const p = [X(u0, z0), X(u1, z0), X(u1, z1), X(u0, z1)]; c.beginPath(); p.forEach((q, i) => i ? c.lineTo(q[0], q[1]) : c.moveTo(q[0], q[1])); c.closePath(); c.fillStyle = col; c.fill(); if (line) { c.strokeStyle = line; c.lineWidth = 0.8; c.stroke(); } },
    windows(a, b, z0, cols, rows, ww, wh, gap, col) { for (let r = 0; r < rows; r++) for (let q = 0; q < cols; q++) { const u = (q + 0.5) / cols, hw = ww / 2; this.fq(a, b, u - hw, u + hw, z0 + gap + r * (wh + gap * 1.4), z0 + gap + r * (wh + gap * 1.4) + wh, col, 'rgba(35,28,70,.35)'); } },
    hip(x, y, w, d, z, rh, col) { // mái chóp 4 mặt (chỉ 2 mặt hướng người xem)
      const c = this.ctx, x0 = x - w / 2, y0 = y - d / 2, x1 = x + w / 2, y1 = y + d / 2, ap = [this.sx(x, y), this.sy(x, y, z + rh)], rc = hex(col);
      const tri = (p, q, f) => { c.beginPath(); c.moveTo(this.sx(p[0], p[1]), this.sy(p[0], p[1], z)); c.lineTo(this.sx(q[0], q[1]), this.sy(q[0], q[1], z)); c.lineTo(ap[0], ap[1]); c.closePath(); c.fillStyle = rgb(rc, f); c.fill(); c.strokeStyle = OL; c.lineWidth = 1; c.stroke(); };
      tri([x0, y1], [x1, y1], 1.0); tri([x1, y1], [x1, y0], 0.74);
    },
    shadow(x, y, r) { const c = this.ctx; c.fillStyle = 'rgba(30,40,70,.2)'; c.beginPath(); c.ellipse(this.sx(x + 2, y + 2), this.sy(x + 2, y + 2, 0), r * this.k * 1.4, r * this.k * 0.7, 0, 0, 6.3); c.fill(); },

    // ---------- cây cối & cảnh vật ----------
    tree(x, y, t, s, sd, alt) {
      const c = this.ctx, k = this.k, bx = this.sx(x, y), by = this.sy(x, y, 0), u = k * s;
      const g = [[70, 120, 62], [86, 132, 66], [108, 140, 64], [58, 104, 70]][(sd * 4) | 0], snow = alt > 46, aut = !snow && sd > 0.975 ? [190, 120, 50] : null, lc = aut || (snow ? mixc(g, SNOW, 0.55) : g);
      c.lineWidth = 1; c.strokeStyle = OL;
      if (t === 0) { // thông
        c.fillStyle = 'rgba(30,40,70,.18)'; c.beginPath(); c.ellipse(bx + 3 * u, by + 1.5 * u, 5 * u, 2.5 * u, 0, 0, 6.3); c.fill();
        c.fillStyle = '#8a5a34'; c.fillRect(bx - 1 * u, by - 4 * u, 2 * u, 4 * u);
        for (let i = 0; i < 3; i++) { const yy = by - (3 + i * 5) * u, w = (6.5 - i * 1.6) * u; c.fillStyle = rgb(lc, 0.85 + i * 0.12); c.beginPath(); c.moveTo(bx - w, yy); c.lineTo(bx + w, yy); c.lineTo(bx, yy - 8 * u); c.closePath(); c.fill(); c.stroke(); }
      } else if (t === 1) { // cây tròn
        c.fillStyle = 'rgba(30,40,70,.18)'; c.beginPath(); c.ellipse(bx + 3 * u, by + 1.5 * u, 6 * u, 3 * u, 0, 0, 6.3); c.fill();
        c.fillStyle = '#8a5a34'; c.fillRect(bx - 1.1 * u, by - 7 * u, 2.2 * u, 7 * u); c.strokeRect(bx - 1.1 * u, by - 7 * u, 2.2 * u, 7 * u);
        for (const [dx, dy, r, f] of [[-3, -9, 4.5, 0.9], [3, -9.5, 4.5, 0.82], [0, -13, 5, 1.08]]) { c.fillStyle = rgb(lc, f); c.beginPath(); c.arc(bx + dx * u, by + dy * u, r * u, 0, 6.3); c.fill(); c.stroke(); }
      } else if (t === 2) { // bụi
        for (const [dx, dy, r, f] of [[-2.2, -2, 2.8, 0.88], [2.2, -2, 2.8, 0.8], [0, -3.4, 3, 1.05]]) { c.fillStyle = rgb(lc, f); c.beginPath(); c.arc(bx + dx * u, by + dy * u, r * u, 0, 6.3); c.fill(); c.stroke(); }
      } else if (t === 3) { // đá
        c.fillStyle = rgb(ROCK, 1.05); c.beginPath(); c.moveTo(bx - 4 * u, by); c.lineTo(bx - 3 * u, by - 3.5 * u); c.lineTo(bx + 0.5 * u, by - 5 * u); c.lineTo(bx + 4 * u, by - 2.5 * u); c.lineTo(bx + 4.5 * u, by); c.closePath(); c.fill(); c.stroke(); c.fillStyle = 'rgba(255,255,255,.35)'; c.beginPath(); c.moveTo(bx - 3 * u, by - 3.5 * u); c.lineTo(bx + 0.5 * u, by - 5 * u); c.lineTo(bx + 1 * u, by - 2.6 * u); c.closePath(); c.fill();
      } else { // hoa
        const cols = ['#ff6b9a', '#d9c27a', '#ffffff', '#b58cff']; for (let i = 0; i < 5; i++) { const dx = (hh(sd * 1e4 | 0, i, 1) - 0.5) * 9, dy = (hh(sd * 1e4 | 0, i, 2) - 0.5) * 4; c.fillStyle = cols[(hh(sd * 1e4 | 0, i, 3) * 4) | 0]; c.beginPath(); c.arc(bx + dx * u, by + dy * u, 1.1 * u + 0.6, 0, 6.3); c.fill(); }
      }
    },
    lamp(x, y) { const c = this.ctx, u = this.k, bx = this.sx(x, y), by = this.sy(x, y, 0); c.strokeStyle = '#3a3a52'; c.lineWidth = Math.max(1, 0.7 * u); c.beginPath(); c.moveTo(bx, by); c.lineTo(bx, by - 8 * u); c.lineTo(bx + 1.8 * u, by - 8.4 * u); c.stroke(); c.fillStyle = '#fff2a8'; c.beginPath(); c.arc(bx + 2 * u, by - 8.2 * u, 1.3 * u + 0.5, 0, 6.3); c.fill(); },

    // ---------- đường ----------
    roadGeom() {
      const D = this.data, nodes = D.nodes, deg = new Array(nodes.length).fill(0), inc = nodes.map(() => []);
      D.edges.forEach((e, i) => { deg[e[0]]++; deg[e[1]]++; inc[e[0]].push(i); inc[e[1]].push(i); });
      const nhw = nodes.map((_, i) => inc[i].reduce((m, ei) => Math.max(m, RW[D.edges[ei][2]] / 2), 0));
      this.G = { deg, inc, nhw };
    },
    strip(A, B, hw, zoff, fill, stroke, bridge) {
      const T = this.T, c = this.ctx, L = Math.hypot(B[0] - A[0], B[1] - A[1]); if (L < 0.1) return; const dx = (B[0] - A[0]) / L, dy = (B[1] - A[1]) / L, px = -dy, py = dx, n = Math.max(1, Math.ceil(L / 18));
      for (let i = 0; i < n; i++) {
        const t0 = i / n, t1 = (i + 1) / n, x0 = A[0] + (B[0] - A[0]) * t0, y0 = A[1] + (B[1] - A[1]) * t0, x1 = A[0] + (B[0] - A[0]) * t1, y1 = A[1] + (B[1] - A[1]) * t1;
        const z0 = bridge ? 1.5 + zoff : T.H(x0, y0) + zoff, z1 = bridge ? 1.5 + zoff : T.H(x1, y1) + zoff;
        c.beginPath(); c.moveTo(this.sx(x0 + px * hw, y0 + py * hw), this.sy(x0 + px * hw, y0 + py * hw, z0)); c.lineTo(this.sx(x1 + px * hw, y1 + py * hw), this.sy(x1 + px * hw, y1 + py * hw, z1)); c.lineTo(this.sx(x1 - px * hw, y1 - py * hw), this.sy(x1 - px * hw, y1 - py * hw, z1)); c.lineTo(this.sx(x0 - px * hw, y0 - py * hw), this.sy(x0 - px * hw, y0 - py * hw, z0)); c.closePath(); c.fillStyle = fill; c.fill(); if (stroke) { c.strokeStyle = fill; c.lineWidth = 1; c.stroke(); }
      }
    },
    disc(x, y, r, zoff, fill, bridge) { const T = this.T, c = this.ctx, z = bridge ? 1.5 + zoff : T.H(x, y) + zoff; c.beginPath(); for (let i = 0; i < 14; i++) { const a = i / 14 * 6.2832, px = x + Math.cos(a) * r, py = y + Math.sin(a) * r; i ? c.lineTo(this.sx(px, py), this.sy(px, py, z)) : c.moveTo(this.sx(px, py), this.sy(px, py, z)); } c.closePath(); c.fillStyle = fill; c.fill(); c.strokeStyle = fill; c.lineWidth = 1; c.stroke(); },
    line(A, B, off, a0, a1, bridge, zo) { // đường kẻ dọc tim đường, từ a0 đến L-a1 mét
      const T = this.T, c = this.ctx, L = Math.hypot(B[0] - A[0], B[1] - A[1]); if (L - a0 - a1 < 2) return; const dx = (B[0] - A[0]) / L, dy = (B[1] - A[1]) / L, px = -dy * off, py = dx * off, n = Math.max(1, Math.ceil((L - a0 - a1) / 18));
      c.beginPath(); for (let i = 0; i <= n; i++) { const s = a0 + (L - a0 - a1) * i / n, x = A[0] + dx * s + px, y = A[1] + dy * s + py, z = (bridge ? 1.5 : T.H(x, y)) + (zo || 0.05); i ? c.lineTo(this.sx(x, y), this.sy(x, y, z)) : c.moveTo(this.sx(x, y), this.sy(x, y, z)); } c.stroke();
    },
    drawRoads(objs) {
      const D = this.data, N = D.nodes, c = this.ctx, k = this.k, G = this.G, ws = (m) => Math.max(0.8, m * k * 1.15);
      const bridges = D.edges.filter((e) => e[3]);
      // trụ cầu
      for (const e of bridges) { const A = N[e[0]], B = N[e[1]], L = Math.hypot(B[0] - A[0], B[1] - A[1]); c.strokeStyle = '#6d6a86'; c.lineWidth = Math.max(2, 2.2 * k); for (let s = 8; s < L - 4; s += 28) { const x = A[0] + (B[0] - A[0]) * s / L, y = A[1] + (B[1] - A[1]) * s / L; c.beginPath(); c.moveTo(this.sx(x, y), this.sy(x, y, 1.5)); c.lineTo(this.sx(x, y), this.sy(x, y, -3)); c.stroke(); } }
      // vỉa hè → lòng đường
      for (const e of D.edges) this.strip(N[e[0]], N[e[1]], RW[e[2]] / 2 + 2.2, 0.0, e[3] ? '#8f8470' : '#a99d84', true, !!e[3]);
      N.forEach((p, i) => { if (G.deg[i]) this.disc(p[0], p[1], G.nhw[i] + 2.2, 0.0, '#a99d84', G.inc[i].some((ei) => D.edges[ei][3]) && G.inc[i].every((ei) => D.edges[ei][3])); });
      for (const e of D.edges) this.strip(N[e[0]], N[e[1]], RW[e[2]] / 2, 0.08, e[3] ? '#6a5e50' : '#7a6c58', true, !!e[3]);
      N.forEach((p, i) => { if (G.deg[i]) this.disc(p[0], p[1], G.nhw[i], 0.08, '#7a6c58', G.inc[i].every((ei) => D.edges[ei][3])); });
      // vạch kẻ
      c.lineCap = 'butt'; c.lineJoin = 'round';
      for (const e of D.edges) {
        const A = N[e[0]], B = N[e[1]], t = e[2], hw = RW[t] / 2, sa = G.deg[e[0]] > 1 ? G.nhw[e[0]] + 1.5 : 1, sb = G.deg[e[1]] > 1 ? G.nhw[e[1]] + 1.5 : 1;
        if (t === 1) { c.strokeStyle = 'rgba(255,255,255,.8)'; c.lineWidth = ws(0.5); c.setLineDash([k * 5, k * 5]); this.line(A, B, 0, sa, sb, !!e[3]); }
        else if (t === 2) { c.setLineDash([]); c.strokeStyle = '#d9c27a'; c.lineWidth = ws(0.45); this.line(A, B, 0.45, sa, sb, !!e[3]); this.line(A, B, -0.45, sa, sb, !!e[3]); c.strokeStyle = 'rgba(255,255,255,.75)'; c.lineWidth = ws(0.4); c.setLineDash([k * 5, k * 6]); this.line(A, B, hw / 2, sa, sb, !!e[3]); this.line(A, B, -hw / 2, sa, sb, !!e[3]); }
        else if (t === 3) { c.setLineDash([]); c.strokeStyle = '#d9c27a'; c.lineWidth = ws(0.6); this.line(A, B, 0.6, sa, sb, !!e[3]); this.line(A, B, -0.6, sa, sb, !!e[3]); c.strokeStyle = '#fff'; c.lineWidth = ws(0.45); this.line(A, B, hw - 1.2, sa, sb, !!e[3]); this.line(A, B, -hw + 1.2, sa, sb, !!e[3]); c.setLineDash([k * 6, k * 7]); this.line(A, B, hw / 2.2, sa, sb, !!e[3]); this.line(A, B, -hw / 2.2, sa, sb, !!e[3]); }
        c.setLineDash([]);
        if (e[3]) { c.strokeStyle = '#f4f1ff'; c.lineWidth = ws(0.9); this.line(A, B, hw + 0.3, 0, 0, true, 1.2); this.line(A, B, -hw - 0.3, 0, 0, true, 1.2); c.strokeStyle = '#8a86a8'; c.lineWidth = ws(0.5); this.line(A, B, hw + 0.3, 0, 0, true, 0.2); this.line(A, B, -hw - 0.3, 0, 0, true, 0.2); }
      }
      // vạch qua đường ở ngã ba/ngã tư
      N.forEach((p, i) => {
        if (G.deg[i] < 3) return; c.fillStyle = 'rgba(255,255,255,.88)';
        for (const ei of G.inc[i]) {
          const e = D.edges[ei], o = N[e[0] === i ? e[1] : e[0]], L = Math.hypot(o[0] - p[0], o[1] - p[1]); if (L < 30 || e[3]) continue; const dx = (o[0] - p[0]) / L, dy = (o[1] - p[1]) / L, hw = RW[e[2]] / 2, d0 = G.nhw[i] + 2.2;
          for (let s = -hw + 1; s <= hw - 1; s += 2.2) { const cx = p[0] + dx * d0 - dy * s, cy = p[1] + dy * d0 + dx * s; this.polyW([[cx - dx * 0.8 - dy * 0.55, cy - dy * 0.8 + dx * 0.55], [cx + dx * 0.8 - dy * 0.55, cy + dy * 0.8 + dx * 0.55], [cx + dx * 0.8 + dy * 0.55, cy + dy * 0.8 - dx * 0.55], [cx - dx * 0.8 + dy * 0.55, cy - dy * 0.8 - dx * 0.55]].map((q) => [q[0], q[1], this.T.H(q[0], q[1]) + 0.12]), 0, 'rgba(255,255,255,.9)'); }
        }
      });
      // cột đèn dọc đường
      for (const e of D.edges) {
        if (e[3] || e[2] < 1) continue; const A = N[e[0]], B = N[e[1]], L = Math.hypot(B[0] - A[0], B[1] - A[1]); if (L < 60) continue; const dx = (B[0] - A[0]) / L, dy = (B[1] - A[1]) / L, off = RW[e[2]] / 2 + 1.5; let side = 1;
        for (let s = 24; s < L - 24; s += 38) { const x = A[0] + dx * s - dy * off * side, y = A[1] + dy * s + dx * off * side; side = -side; objs.push({ d: x + y, f: () => { this.zo = this.T.H(x, y); this.lamp(x, y); this.zo = 0; } }); }
      }
    },

    // ---------- công trình ----------
    building(x, y, b, objs) {
      const kind = b[1], lv = b[2], id = b[0], v = hh(id, kind, 7), c = this.ctx, k = this.k;
      const lot = (col) => this.polyW(this.rect(x, y, 22, 22), 0.06, col, 'rgba(35,28,70,.18)', 1);
      const A = (w, d) => this.rect(x, y, w, d);
      if (kind === 0) {
        lot('#a3b878'); this.shadow(x, y, 9); const h = 6 + lv * 2.5, wall = WALLS[(v * 6) | 0], roof = ROOFS[(hh(id, 1, 9) * 6) | 0], w = 12 + (v < 0.5 ? 1 : 0) * 2, d = 12;
        // hàng rào + bụi
        this.prism(this.rect(x - 1, y + 9.5, 17, 0.8), 0, 1.4, '#ffffff', { lw: 0.8 });
        this.prism(A(w, d), 0, h, wall);
        const P = A(w, d), l0 = P[3], l1 = P[2], r0 = P[2], r1 = P[1];
        this.fq(l0, l1, 0.42, 0.58, 0, 4.2, '#9a6a45', OL); this.windows(l0, l1, 0.0, 2, Math.max(1, Math.floor((h - 1) / 4.5)), 0.16, 2.2, 1.5, '#9fb8bd'); if (lv >= 1) this.windows(r0, r1, 0.0, 2, Math.max(1, Math.floor((h - 1) / 4.5)), 0.16, 2.2, 1.5, '#9fb8bd');
        if (v < 0.6) { this.hip(x, y, w + 3, d + 3, h, 6 + lv, roof); this.prism(this.rect(x + 3, y - 2, 2.2, 2.2), h + 1, h + 7, '#b0705a', { lw: 0.8 }); } else { this.prism(A(w + 1, d + 1), h, h + 1, '#ece7f5'); this.prism(this.rect(x - 2, y - 2, 5, 4), h + 1, h + 3, roof, { lw: 0.8 }); }
      } else if (kind === 1) {
        lot('#bdb5a2'); this.shadow(x, y, 11); const h = 20 + lv * 11, col = TOWERS[(v * 5) | 0], w = 15, d = 15, P = A(w, d);
        this.prism(P, 0, h, col); this.prism(A(w + 1.5, d + 1.5), 0, 2, '#eee9f7');
        const rows = Math.floor((h - 4) / 4.5); this.windows(P[3], P[2], 2, 4, rows, 0.15, 2.3, 1.7, '#c9d6d2'); this.windows(P[2], P[1], 2, 4, rows, 0.15, 2.3, 1.7, '#b4c4c2');
        this.prism(A(w - 3, d - 3), h, h + 2.2, '#ece7f5'); this.prism(this.rect(x - 2, y + 1, 4, 3), h + 2.2, h + 4.5, '#8a86a8', { lw: 0.8 }); c.strokeStyle = '#45405f'; c.lineWidth = 1.2; c.beginPath(); c.moveTo(this.sx(x + 3, y - 3), this.sy(x + 3, y - 3, h + 2.2)); c.lineTo(this.sx(x + 3, y - 3), this.sy(x + 3, y - 3, h + 9)); c.stroke(); c.fillStyle = '#ff5d5d'; c.beginPath(); c.arc(this.sx(x + 3, y - 3), this.sy(x + 3, y - 3, h + 9), 1.3 * k + 0.6, 0, 6.3); c.fill();
      } else if (kind === 2) {
        lot('#c4baa4'); this.shadow(x, y, 10); const h = 8 + lv * 4, P = A(16, 15), col = ['#d6b46a', '#c99a70', '#c78a7a', '#a9bcc0'][(v * 4) | 0];
        this.prism(P, 0, h, col); this.windows(P[2], P[1], 4.5, 3, Math.max(1, Math.floor((h - 5) / 4)), 0.22, 2.4, 1.5, '#b9ccca');
        // mái hiên sọc
        const a = P[3], b2 = P[2]; for (let i = 0; i < 8; i++) { const u0 = i / 8, u1 = (i + 1) / 8, c2 = this.ctx; const px = (u) => a[0] + (b2[0] - a[0]) * u, py = (u) => a[1] + (b2[1] - a[1]) * u; c2.beginPath(); c2.moveTo(this.sx(px(u0), py(u0)), this.sy(px(u0), py(u0), 4.2)); c2.lineTo(this.sx(px(u1), py(u1)), this.sy(px(u1), py(u1), 4.2)); c2.lineTo(this.sx(px(u1), py(u1) + 3.2), this.sy(px(u1), py(u1) + 3.2, 3)); c2.lineTo(this.sx(px(u0), py(u0) + 3.2), this.sy(px(u0), py(u0) + 3.2, 3)); c2.closePath(); c2.fillStyle = i % 2 ? '#e9dfc6' : '#8c3b2a'; c2.fill(); c2.strokeStyle = OL; c2.lineWidth = 0.8; c2.stroke(); }
        this.fq(a, b2, 0.15, 0.85, 0.2, 2.8, '#7a5d4a', OL);
        this.prism(this.rect(x + 2, y - 1, 9, 1.4), h, h + 4, '#fff7d6', { lw: 0.8 }); this.fq([x - 2.5, y - 0.3], [x + 6.5, y - 0.3], 0.1, 0.9, h + 0.6, h + 3.4, '#ff5d8f');
      } else if (kind === 3) {
        lot('#b3ab9a'); this.shadow(x, y, 11); const h = 8 + lv * 2, P = A(18, 14);
        this.prism(P, 0, h, '#8d8294'); this.windows(P[3], P[2], 2.2, 5, 1, 0.12, 2, 1.2, '#2e2a52'); this.fq(P[2], P[1], 0.3, 0.7, 0, 5.2, '#4a4468', OL);
        // mái răng cưa
        for (let i = 0; i < 3; i++) { const cx = x - 6 + i * 6, ya = y; this.prism(this.rect(cx, ya, 5.2, 13), h, h + 3, '#6f6577', { lw: 0.8 }); }
        // silo + ống khói
        this.prism(this.rect(x - 8, y - 9, 4, 4), 0, h + 14, '#6a6070', { lw: 0.9 }); this.fq([x - 10, y - 7], [x - 6, y - 7], 0, 1, h + 6, h + 8, '#ff5d8f'); this.fq([x - 10, y - 7], [x - 6, y - 7], 0, 1, h + 11, h + 13, '#ffffff');
        this.prism(this.rect(x + 8, y - 8, 3.4, 3.4), 0, h + 3, '#9b9bb5', { lw: 0.9 }); const sc = { x: x - 8, y: y - 9, z: h + 14 }; objs.smoke = objs.smoke || []; objs.smoke.push(sc);
        this.prism(this.rect(x + 8, y + 3, 4, 5), 0, 3, '#d3b278', { lw: 0.8 });
      } else if (kind === 4) {
        lot('#a9bd7e'); this.shadow(x, y, 11); const P = A(18, 12);
        this.prism(P, 0, 10, '#c99a70'); this.windows(P[3], P[2], 1.5, 5, 2, 0.12, 2.6, 1.4, '#9fb8bd'); this.windows(P[2], P[1], 1.5, 3, 2, 0.14, 2.6, 1.4, '#9fb8bd'); this.fq(P[3], P[2], 0.44, 0.56, 0, 4, '#8a5a34', OL);
        this.hip(x, y, 21, 15, 10, 5, '#7a3f32'); this.prism(this.rect(x + 3, y - 2, 4, 4), 10, 18, '#ffe6bf', { lw: 0.9 }); this.hip(x + 3, y - 2, 6, 6, 18, 4, '#7a3f32');
        c.strokeStyle = '#45405f'; c.lineWidth = 1.2; c.beginPath(); c.moveTo(this.sx(x - 7, y + 8), this.sy(x - 7, y + 8, 0)); c.lineTo(this.sx(x - 7, y + 8), this.sy(x - 7, y + 8, 12)); c.stroke(); c.fillStyle = '#ff5d5d'; c.fillRect(this.sx(x - 7, y + 8), this.sy(x - 7, y + 8, 12), 5 * k + 2, 3 * k + 1.5);
        this.prism(this.rect(x + 6, y + 8.5, 3, 3), 0, 1, '#d9c27a', { lw: 0.7 }); this.badge(x, y, 25, '🏫');
      } else if (kind === 5) {
        lot('#cfc7b2'); this.shadow(x, y, 12); const P = A(20, 16);
        this.prism(P, 0, 18, '#ffffff'); this.windows(P[3], P[2], 2, 6, 3, 0.1, 2.4, 1.6, '#a5bfc4'); this.windows(P[2], P[1], 2, 4, 3, 0.12, 2.4, 1.6, '#a5bfc4');
        this.prism(A(12, 10), 18, 24, '#f4f4fa'); const cx = x, cy = y; this.polyW([[cx - 1.2, cy - 4, 24.12], [cx + 1.2, cy - 4, 24.12], [cx + 1.2, cy + 4, 24.12], [cx - 1.2, cy + 4, 24.12]], 0, '#9c2f2a'); this.polyW([[cx - 4, cy - 1.2, 24.14], [cx + 4, cy - 1.2, 24.14], [cx + 4, cy + 1.2, 24.14], [cx - 4, cy + 1.2, 24.14]], 0, '#9c2f2a');
        this.fq(P[2], P[1], 0.35, 0.65, 0, 4.5, '#6f8fa4', OL); this.badge(x, y, 32, '🏥');
      } else if (kind === 6) {
        lot('#c3c6bc'); this.shadow(x, y, 10); const P = A(16, 14);
        this.prism(P, 0, 12, '#6f879c'); this.windows(P[3], P[2], 2, 4, 2, 0.14, 2.4, 1.6, '#c4d2d4'); this.fq(P[3], P[2], 0.4, 0.6, 0, 4.4, '#2a4a8c', OL); this.prism(A(17, 15), 12, 13.2, '#e8f1ff');
        this.prism(this.rect(x, y, 4, 3), 13.2, 15.4, '#2a4a8c', { lw: 0.8 }); c.fillStyle = '#9c2f2a'; c.beginPath(); c.arc(this.sx(x - 1, y), this.sy(x - 1, y, 16.2), 1.2 * k + 0.7, 0, 6.3); c.fill(); c.fillStyle = '#3b8bff'; c.beginPath(); c.arc(this.sx(x + 1, y), this.sy(x + 1, y, 16.2), 1.2 * k + 0.7, 0, 6.3); c.fill();
        this.prism(this.rect(x + 9, y + 8, 4, 2), 0, 1.8, '#f5f5ff', { lw: 0.7 }); this.badge(x, y, 23, '👮');
      } else { // công viên
        this.polyW(this.rect(x, y, 24, 24), 0.06, '#7fa05c', 'rgba(35,28,70,.25)', 1); this.polyW([[x - 12, y - 1.5], [x + 12, y - 1.5], [x + 12, y + 1.5], [x - 12, y + 1.5]], 0.08, '#f0dca8'); this.polyW([[x - 1.5, y - 12], [x + 1.5, y - 12], [x + 1.5, y + 12], [x - 1.5, y + 12]], 0.08, '#f0dca8');
        c.fillStyle = '#6f98ad'; c.strokeStyle = OL; c.lineWidth = 1; c.beginPath(); c.ellipse(this.sx(x, y), this.sy(x, y, 0.5), 5 * k * 1.4, 5 * k * 0.7, 0, 0, 6.3); c.fill(); c.stroke(); c.strokeStyle = '#fff'; c.lineWidth = 1.5; c.beginPath(); c.moveTo(this.sx(x, y), this.sy(x, y, 0.5)); c.lineTo(this.sx(x, y), this.sy(x, y, 7)); c.stroke();
        this.prism(this.rect(x - 8, y + 3.5, 4, 1.4), 0, 1.4, '#9b6a3a', { lw: 0.7 });
        for (const [dx, dy, t] of [[-8, -8, 1], [8, -8, 0], [-8, 8, 1], [8, 8, 1], [9, 1, 2], [-3, -7, 4]]) this.tree(x + dx, y + dy, t, 1, hh(id, dx, dy), 0);
      }
    },
    badge(x, y, z, ch) { const c = this.ctx, bx = this.sx(x, y), by = this.sy(x, y, z), r = Math.max(7, 7.5 * this.k); c.fillStyle = 'rgba(255,255,255,.92)'; c.strokeStyle = OL; c.lineWidth = 1.2; c.beginPath(); c.arc(bx, by, r, 0, 6.3); c.fill(); c.stroke(); c.font = `${Math.round(r * 1.2)}px sans-serif`; c.textAlign = 'center'; c.textBaseline = 'middle'; c.fillStyle = '#000'; c.fillText(ch, bx, by + 1); c.textBaseline = 'alphabetic'; },

    // ---------- lớp tĩnh (đặt vào bộ đệm) ----------
    renderStatic() {
      const D = this.data, S = D.size, T = this.T, c = this.ctx, k = this.k, objs = [];
      this.roadGeom(); this.smokes_ctx = {};
      // trời + núi xa
      const sky = c.createLinearGradient(0, 0, 0, this.ch); sky.addColorStop(0, '#a9bcc4'); sky.addColorStop(0.6, '#d8d6c2'); sky.addColorStop(1, '#efe6cc'); c.fillStyle = sky; c.fillRect(0, 0, this.cw, this.ch);
      this.zo = 0; const edgeA = -M - 20, edgeB = S + M + 20;
      for (const [lay, col, hs] of [[0, '#9aa6a8', 190], [1, '#7f8f8c', 130]]) {
        c.beginPath(); const pts = []; for (let t = 0; t <= 1.0001; t += 1 / 60) { const x = edgeA + (edgeB - edgeA) * t; pts.push([x, edgeA - lay * 30, 40 + vn(x, 0, 140, 21 + lay) * hs + vn(x, 9, 50, 30) * 30]); } for (let t = 0; t <= 1.0001; t += 1 / 60) { const y = edgeA + (edgeB - edgeA) * t; pts.push([edgeA - lay * 30, y, 40 + vn(0, y, 140, 41 + lay) * hs + vn(9, y, 50, 50) * 30]); }
        // theo cạnh trên cùng (x nhỏ & y nhỏ): nối hai cạnh sau
        const back = []; for (let t = 0; t <= 1.0001; t += 1 / 50) { const y = edgeB - (edgeB - edgeA) * t; back.push([edgeA - lay * 30, y, 40 + vn(0, y, 140, 41 + lay) * hs]); } for (let t = 0; t <= 1.0001; t += 1 / 50) { const x = edgeA + (edgeB - edgeA) * t; back.push([x, edgeA - lay * 30, 40 + vn(x, 0, 140, 21 + lay) * hs]); }
        c.beginPath(); c.moveTo(this.sx(back[0][0], back[0][1]), this.sy(back[0][0], back[0][1], -30)); for (const p of back) c.lineTo(this.sx(p[0], p[1]), this.sy(p[0], p[1], p[2])); const l = back[back.length - 1]; c.lineTo(this.sx(l[0], l[1]), this.sy(l[0], l[1], -30)); c.closePath(); c.fillStyle = col; c.fill();
      }
      // mặt đất theo đường chéo (xa → gần)
      const n = T.N, R = T.R, st = T.ws, W = this.cw, Hh = this.ch, mg = 130 * k + 50, waterCenters = [];
      const hv = (i, j) => T.H((i - R) * st, (j - R) * st);
      const Hc = new Float32Array((n + 1) * (n + 1)); for (let j = 0; j <= n; j++) for (let i = 0; i <= n; i++) Hc[j * (n + 1) + i] = hv(i, j);
      const hz = (i, j) => Hc[j * (n + 1) + i];
      for (let d = 0; d <= 2 * n - 2; d++) for (let gx = Math.max(0, d - n + 1); gx <= Math.min(d, n - 1); gx++) {
        const gy = d - gx, x0 = (gx - R) * st, y0 = (gy - R) * st, x1 = x0 + st, y1 = y0 + st, cx = this.sx(x0 + st / 2, y0 + st / 2), cy = this.sy(x0 + st / 2, y0 + st / 2, 0);
        if (cx < -mg || cx > W + mg || cy < -mg || cy > Hh + mg * 1.6) continue;
        const za = hz(gx, gy), zb = hz(gx + 1, gy), zc = hz(gx + 1, gy + 1), zd = hz(gx, gy + 1), h = (za + zb + zc + zd) / 4, wat = T.water(gx, gy);
        const Q = (z) => [[this.sx(x0, y0), this.sy(x0, y0, z === undefined ? za : z)], [this.sx(x1, y0), this.sy(x1, y0, z === undefined ? zb : z)], [this.sx(x1, y1), this.sy(x1, y1, z === undefined ? zc : z)], [this.sx(x0, y1), this.sy(x0, y1, z === undefined ? zd : z)]];
        const edgeFront = gx === n - 1 || gy === n - 1;
        if (wat) {
          const q = Q(-3); c.beginPath(); q.forEach((p, i) => i ? c.lineTo(p[0], p[1]) : c.moveTo(p[0], p[1])); c.closePath(); c.fillStyle = ((gx + gy) & 1) ? '#5a8aa3' : '#547f98'; c.fill(); c.strokeStyle = c.fillStyle; c.lineWidth = 1; c.stroke(); if (hh(gx, gy, 77) < 0.14) waterCenters.push([x0 + st / 2, y0 + st / 2, hh(gx, gy, 5)]);
          if (edgeFront) { const base = this.sy(x1, y1, -3); }
          continue;
        }
        // màu: cỏ → xanh đậm → đá → tuyết
        let col = mixc(GRASS[0], GRASS[1], hh(gx, gy, 1) * 0.6 + (((gx + gy) & 1) ? 0.15 : 0)); col = mixc(col, DARK, sm(cl((h - 8) / 14, 0, 1)) * 0.8); col = mixc(col, ROCK, sm(cl((h - 32) / 16, 0, 1))); col = mixc(col, SNOW, sm(cl((h - 66) / 12, 0, 1)));
        const nearW = T.water(gx - 1, gy) || T.water(gx + 1, gy) || T.water(gx, gy - 1) || T.water(gx, gy + 1) || T.water(gx - 1, gy - 1) || T.water(gx + 1, gy + 1); if (nearW) col = mixc(col, SAND, 0.85);
        const gxs = ((zb + zc) - (za + zd)) / (2 * st), gys = ((zc + zd) - (za + zb)) / (2 * st), sh = cl(1 + (0.5 * gxs - 0.5 * gys) * 1.4, 0.72, 1.28);
        // vách đất khi bên cạnh là nước hoặc mép bản đồ
        const sideF = (z0, ax, ay, bx, by, zA, zB, lowZ, colr) => { c.beginPath(); c.moveTo(this.sx(ax, ay), this.sy(ax, ay, zA)); c.lineTo(this.sx(bx, by), this.sy(bx, by, zB)); c.lineTo(this.sx(bx, by), this.sy(bx, by, lowZ)); c.lineTo(this.sx(ax, ay), this.sy(ax, ay, lowZ)); c.closePath(); c.fillStyle = colr; c.fill(); c.strokeStyle = colr; c.lineWidth = 1; c.stroke(); };
        if (T.water(gx + 1, gy)) sideF(0, x1, y0, x1, y1, zb, zc, -3, rgb(DIRT, 0.72)); if (T.water(gx, gy + 1)) sideF(0, x0, y1, x1, y1, zd, zc, -3, rgb(DIRT, 0.92));
        if (gx === n - 1) sideF(0, x1, y0, x1, y1, zb, zc, -60, rgb(DIRT, 0.62)); if (gy === n - 1) sideF(0, x0, y1, x1, y1, zd, zc, -60, rgb(DIRT, 0.82));
        const q = Q(); c.beginPath(); q.forEach((p, i) => i ? c.lineTo(p[0], p[1]) : c.moveTo(p[0], p[1])); c.closePath(); c.fillStyle = rgb(col, sh); c.fill(); c.strokeStyle = c.fillStyle; c.lineWidth = 1; c.stroke();
      }
      this.waterSpots = waterCenters;
      // đường & các chi tiết bên trên
      this.drawRoads(objs);
      // ô quy hoạch trống + công trình + cây
      const pmap = new Map(); for (const p of D.parcels) { if (p[3] >= 0) pmap.set(p[3], p); else if (p[2] !== 0) this.pad(p[0], p[1], ZONE[p[2]].col); }
      for (const b of D.buildings) { const p = pmap.get(b[0]); if (p) objs.push({ d: p[0] + p[1], f: () => { this.zo = T.H(p[0], p[1]); this.building(p[0], p[1], b, this.smokes_ctx); this.zo = 0; } }); }
      // cây cối rải theo băm toạ độ
      const oc = T.oc, on = T.on, free = (x, y) => { if (x < 0 || y < 0 || x >= S || y >= S) return true; return T.occ[Math.floor(y / oc) * on + Math.floor(x / oc)] === 0; };
      const inWater = (x, y) => T.water(Math.floor(x / st) + R, Math.floor(y / st) + R);
      for (let gy = -R; gy < T.N - R; gy++) for (let gx = -R; gx < T.N - R; gx++) {
        const bx = gx * st, by = gy * st, cxs = this.sx(bx + 10, by + 10); if (cxs < -60 || cxs > W + 60) continue; const csy = this.sy(bx + 10, by + 10, T.H(bx + 10, by + 10)); if (csy < -mg || csy > Hh + 60) continue;
        const inside = bx >= 0 && by >= 0 && bx < S && by < S, dens = vn(bx, by, 220, T.seed + 11), forest = vn(bx, by, 90, T.seed + 17);
        const pr = inside ? 0.14 + 0.5 * Math.max(0, dens - 0.55) * 2 : 0.42 + 0.33 * dens; const cnt = inside ? 1 : 2;
        for (let i = 0; i < cnt; i++) {
          if (hh(gx, gy, 30 + i) > pr) continue; const x = bx + hh(gx, gy, 40 + i) * st, y = by + hh(gx, gy, 50 + i) * st; if (inWater(x, y) || !free(x, y)) continue;
          const z = T.H(x, y), r = hh(gx, gy, 60 + i), sd = hh(gx, gy, 70 + i); let t, s = 0.85 + hh(gx, gy, 80 + i) * 0.5; if (z > 56 && r > 0.3) continue;
          if (r < 0.1) t = 3; else if (r < 0.2) t = 4; else if (r < 0.32) t = 2; else t = (forest > 0.5 ? 0.7 : 0.35) > hh(gx, gy, 90 + i) ? 0 : 1; if (z > 30 && t === 1) t = 0; if (t === 4 && !inside && z > 10) t = 2;
          objs.push({ d: x + y, f: () => { this.zo = z; this.tree(x, y, t, s, sd, z); this.zo = 0; } });
        }
      }
      objs.sort((a, b) => a.d - b.d); for (const o of objs) o.f();
      this.smokeList = (this.smokes_ctx.smoke || []).map((s) => ({ x: s.x, y: s.y, z: s.z + T.H(s.x, s.y) })); this.smokes_ctx = {};
      this.zo = 0;
    },
    pad(x, y, col) { const c = this.ctx, T = this.T; this.zo = T.H(x, y); c.save(); c.globalAlpha = 0.5; this.polyW(this.rect(x, y, 13, 13), 0.1, col, 'rgba(43,35,80,.35)', 1); c.restore(); this.zo = 0; },

    // ---------- xe cộ & người đi bộ ----------
    spawnCars() {
      const D = this.data, N = D.nodes, E = D.edges; this.cars = []; this.walkers = []; if (!E.length) return;
      let len = 0; const L = E.map((e) => { const l = Math.hypot(N[e[1]][0] - N[e[0]][0], N[e[1]][1] - N[e[0]][1]); len += l; return l; }); this.elen = L;
      const inc = N.map(() => []); E.forEach((e, i) => { inc[e[0]].push(i); inc[e[1]].push(i); }); this.inc = inc;
      const pop = D.indicators.pop || 0, load = cl(pop / 1500 + 0.12, 0.12, 1.1), n = Math.min(110, Math.round(len / 55 * load)), nw = Math.min(70, Math.round(len / 80 * cl(pop / 600, 0.1, 1)));
      const mk = (walk) => { const ei = (Math.random() * E.length) | 0, fwd = Math.random() < 0.5, r = Math.random(); return { e: ei, fwd, s: Math.random() * L[ei], v: walk ? 2 + Math.random() : 12 + Math.random() * 6, type: walk ? 9 : r < 0.1 ? 2 : r < 0.18 ? 1 : 0, col: walk ? ['#ff6b9a', '#4fb6ff', '#d9c27a', '#7a5cff', '#2fd2a0'][(Math.random() * 5) | 0] : CARS[(Math.random() * CARS.length) | 0], side: Math.random() < 0.5 ? 1 : -1 }; };
      for (let i = 0; i < n; i++) this.cars.push(mk(false)); for (let i = 0; i < nw; i++) this.walkers.push(mk(true));
    },
    stepCars(dt) {
      const D = this.data, N = D.nodes, E = D.edges;
      const adv = (a) => {
        const e = E[a.e]; a.s += a.v * dt; if (a.s < this.elen[a.e]) return; const to = a.fwd ? e[1] : e[0], opts = this.inc[to].filter((i) => i !== a.e), pick = opts.length ? opts[(Math.random() * opts.length) | 0] : a.e, ne = E[pick];
        a.e = pick; a.fwd = ne[0] === to; a.s = 0;
      };
      for (const a of this.cars) adv(a); for (const a of this.walkers) adv(a);
    },
    placeOf(a) { const D = this.data, e = D.edges[a.e], A = D.nodes[a.fwd ? e[0] : e[1]], B = D.nodes[a.fwd ? e[1] : e[0]], L = this.elen[a.e] || 1, u = a.s / L, dx = (B[0] - A[0]) / L, dy = (B[1] - A[1]) / L, hw = RW[e[2]] / 2; let off, x, y; if (a.type === 9) off = (hw + 1.1) * a.side; else off = hw * 0.5; x = A[0] + (B[0] - A[0]) * u + (-dy) * off; y = A[1] + (B[1] - A[1]) * u + dx * off; return { x, y, dx, dy, z: e[3] ? 1.6 : this.T.H(x, y) + 0.1 }; },
    drawCars(t) {
      const c = this.ctx, list = [];
      for (const a of this.cars) { const p = this.placeOf(a); list.push({ d: p.x + p.y, a, p }); } for (const a of this.walkers) { const p = this.placeOf(a); list.push({ d: p.x + p.y, a, p, w: 1 }); }
      list.sort((u, v) => u.d - v.d);
      for (const it of list) {
        const { p, a } = it, sxp = this.sx(p.x, p.y); if (sxp < -30 || sxp > this.w + 30) continue; this.zo = p.z;
        if (it.w) { const bx = sxp, by = this.sy(p.x, p.y, 0), u = this.k, bob = Math.sin(t / 120 + p.x) * 0.4 * u; c.fillStyle = a.col; c.fillRect(bx - 1 * u - 0.3, by - 3.6 * u + bob - 0.5, 2 * u + 0.6, 2.4 * u + 1); c.fillStyle = '#ffd7b0'; c.beginPath(); c.arc(bx, by - 4.4 * u + bob, 0.9 * u + 0.5, 0, 6.3); c.fill(); continue; }
        const dx = p.dx, dy = p.dy, px = -dy, py = dx, len = a.type === 2 ? 8 : a.type === 1 ? 6.4 : 5, wid = 2.6, hl = len / 2, hwid = wid / 2;
        const P = [[p.x - dx * hl - px * hwid, p.y - dy * hl - py * hwid], [p.x + dx * hl - px * hwid, p.y + dy * hl - py * hwid], [p.x + dx * hl + px * hwid, p.y + dy * hl + py * hwid], [p.x - dx * hl + px * hwid, p.y - dy * hl + py * hwid]];
        const body = a.type === 2 ? (hex(a.col)[0] > 200 ? '#d9c27a' : '#ff9a3c') : a.col; c.fillStyle = 'rgba(30,40,70,.22)'; this.polyW(P.map((q) => [q[0] + 0.8, q[1] + 0.8]), 0.02, 'rgba(30,40,70,.22)');
        this.prism(P, 0.3, 1.9 + (a.type === 2 ? 1.2 : 0), body, { lw: 0.7 });
        const cab = a.type === 2 ? [[p.x + dx * (hl - 3.6) - px * hwid * 0.85, p.y + dy * (hl - 3.6) - py * hwid * 0.85], [p.x + dx * (hl - 0.2) - px * hwid * 0.85, p.y + dy * (hl - 0.2) - py * hwid * 0.85], [p.x + dx * (hl - 0.2) + px * hwid * 0.85, p.y + dy * (hl - 0.2) + py * hwid * 0.85], [p.x + dx * (hl - 3.6) + px * hwid * 0.85, p.y + dy * (hl - 3.6) + py * hwid * 0.85]] : [[p.x - dx * 1.6 - px * hwid * 0.85, p.y - dy * 1.6 - py * hwid * 0.85], [p.x + dx * 1.1 - px * hwid * 0.85, p.y + dy * 1.1 - py * hwid * 0.85], [p.x + dx * 1.1 + px * hwid * 0.85, p.y + dy * 1.1 + py * hwid * 0.85], [p.x - dx * 1.6 + px * hwid * 0.85, p.y - dy * 1.6 + py * hwid * 0.85]];
        if (a.type === 2) this.prism(cab, 1.9, 4.2, '#3b3f55', { lw: 0.6, top: '#4a4f6b' }); else this.prism(cab, 1.9, 3.1, '#9fb8bd', { lw: 0.6, top: '#dff4ff' });
      }
      this.zo = 0;
    },

    // hậu kỳ: bảo hoà thấp, ngả nâu như tranh cổ + tối viền
    finish(cc) {
      const w = this.cache.width, h = this.cache.height; cc.setTransform(1, 0, 0, 1, 0, 0);
      try { const tmp = document.createElement('canvas'); tmp.width = w; tmp.height = h; tmp.getContext('2d').drawImage(this.cache, 0, 0); if ('filter' in cc) { cc.clearRect(0, 0, w, h); cc.filter = 'saturate(.62) sepia(.22) contrast(1.04)'; cc.drawImage(tmp, 0, 0); cc.filter = 'none'; } } catch (e) { }
      cc.globalCompositeOperation = 'multiply'; cc.fillStyle = 'rgba(214,190,140,.22)'; cc.fillRect(0, 0, w, h); cc.globalCompositeOperation = 'source-over';
      const g = cc.createRadialGradient(w / 2, h / 2, Math.min(w, h) * 0.3, w / 2, h / 2, Math.max(w, h) * 0.62); g.addColorStop(0, 'rgba(40,24,8,0)'); g.addColorStop(1, 'rgba(40,24,8,.38)'); cc.fillStyle = g; cc.fillRect(0, 0, w, h); cc.setTransform(this.dpr, 0, 0, this.dpr, 0, 0);
    },

    // ---------- khung hình ----------
    draw(t) {
      t = t || performance.now(); const D = this.data; if (!D) return; if (!this.T) this.prepTerrain();
      const cvx = this.ctx, w = this.w, h = this.h, d = this.dpr;
      // lớp tĩnh có vùng đệm 30% mỗi phía để kéo bản đồ mượt
      const key = this.k + '|' + this.ver + '|' + w + '|' + h; const dx = this.cache ? this.ox - this.cOx : 0, dy = this.cache ? this.oy - this.cOy : 0;
      if (!this.cache || this.cKey !== key || Math.abs(dx) > w * 0.25 || Math.abs(dy) > h * 0.25) {
        if (!this.cache) this.cache = document.createElement('canvas'); this.cw = Math.round(w * 1.6); this.ch = Math.round(h * 1.6); this.cache.width = Math.round(this.cw * d); this.cache.height = Math.round(this.ch * d);
        const cc = this.cache.getContext('2d'); cc.setTransform(d, 0, 0, d, 0, 0); this.cOx = this.ox; this.cOy = this.oy; this.cKey = key;
        const sox = this.ox, soy = this.oy; this.ox += w * 0.3; this.oy += h * 0.3; this.ctx = cc; try { this.renderStatic(); } finally { this.ctx = cvx; this.ox = sox; this.oy = soy; }
        this.finish(cc); this.cars = null;
      }
      if (!this.cars) this.spawnCars();
      const c = cvx; c.setTransform(1, 0, 0, 1, 0, 0); c.clearRect(0, 0, this.cv.width, this.cv.height);
      c.drawImage(this.cache, Math.round((this.ox - this.cOx - w * 0.3) * d), Math.round((this.oy - this.cOy - h * 0.3) * d)); c.setTransform(d, 0, 0, d, 0, 0);
      const dt = this._lt ? Math.min(0.1, (t - this._lt) / 1000) : 0; this._lt = t;
      if (this.cars.length || this.walkers.length) { this.stepCars(dt); this.drawCars(t); }
      // ánh nước lấp lánh
      c.lineWidth = 1.2; for (const s of this.waterSpots || []) { const ph = ((t / 1800) + s[2]) % 1, a = Math.sin(ph * 3.1416); if (a < 0.1) continue; const px = this.sx(s[0], s[1]), py = this.sy(s[0], s[1], -3); if (px < -20 || px > w + 20) continue; c.strokeStyle = `rgba(255,255,255,${(a * 0.75).toFixed(2)})`; c.beginPath(); c.ellipse(px, py, (4 + ph * 6) * this.k * 1.2, (2 + ph * 3) * this.k * 0.7, 0, 0, 6.3); c.stroke(); }
      // khói ống khói
      for (const s of this.smokeList || []) { for (let i = 0; i < 3; i++) { const ph = ((t / 2600) + i / 3) % 1; c.fillStyle = `rgba(245,245,255,${(0.7 * (1 - ph)).toFixed(2)})`; c.beginPath(); c.arc(this.sx(s.x, s.y) + ph * 8 * this.k, this.sy(s.x, s.y, s.z + ph * 14), (2 + ph * 4) * this.k + 1, 0, 6.3); c.fill(); } }
      // cổng quốc lộ
      const nodes = D.nodes; if (nodes.length) { const g0 = nodes[0], gx = this.sx(g0[0], g0[1]), gy = this.sy(g0[0], g0[1], this.T.H(g0[0], g0[1])); c.lineWidth = 3; c.strokeStyle = '#2b2350'; c.fillStyle = '#ffc93c'; c.beginPath(); c.arc(gx, gy - 4, 11, 0, 6.3); c.fill(); c.stroke(); c.fillStyle = '#2b2350'; c.font = '700 14px "Trebuchet MS",sans-serif'; c.textAlign = 'center'; c.fillText('⛩', gx, gy + 1); c.lineWidth = 4; c.strokeStyle = 'rgba(255,255,255,.95)'; c.font = '800 12px "Trebuchet MS",sans-serif'; c.strokeText('Quốc lộ', gx, gy - 22); c.fillText('Quốc lộ', gx, gy - 22); }
      // xem trước thao tác
      const X = (x, y, z) => this.sx(x, y), Y = (x, y, z) => this.sy(x, y, z || 0);
      if (this.drag && this.drag.road) { const d2 = this.drag; c.setLineDash([10, 8]); c.strokeStyle = '#ff5d8f'; c.lineWidth = Math.max(4, RW[this.tool.v] * this.k); c.lineCap = 'round'; c.beginPath(); c.moveTo(X(d2.x1, d2.y1), Y(d2.x1, d2.y1)); c.lineTo(X(d2.x2, d2.y2), Y(d2.x2, d2.y2)); c.stroke(); c.setLineDash([]); c.lineCap = 'butt'; }
      if (this.hover && (this.tool.kind === 'zone' || this.tool.kind === 'service')) { const [hx, hy] = this.hover; const r = this.tool.kind === 'zone' ? this.radius : 14; c.strokeStyle = '#2b2350'; c.fillStyle = 'rgba(255,255,255,.35)'; c.lineWidth = 3; c.beginPath(); c.ellipse(X(hx, hy), Y(hx, hy), r * this.k * 1.414, r * this.k * 0.707, 0, 0, 6.3); c.fill(); c.stroke(); }
    }
  };
  Object.assign(CityView, Art);
})();
