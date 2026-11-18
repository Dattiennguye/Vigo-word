// Luồng trò chơi: màn hình đầu, chọn nước, vòng lặp tuần, bảng điều khiển, lưu/nạp.
const UI = { state: null, info: null, tab: 'nation', speed: 0, timer: null, busy: false, picking: false, pickCountry: -1, seenEvent: -1, sig: {}, meTab: false };

const EV_ICON = { war: '⚔️', capture: '🚩', goal: '🎯', peace: '🕊️', alliance: '🤝', pact: '📜', trade: '📦', betrayal: '🗡️', eliminated: '💀', terms: '📋', offer: '✉️', alliance_end: '💔' };

function toast(msg, cls) {
  const t = document.createElement('div'); t.className = 'toast ' + (cls || ''); t.textContent = msg; $('toast').appendChild(t); setTimeout(() => t.remove(), 4200);
}
function showModal(html) { $('modalcard').innerHTML = html; $('modal').classList.remove('hidden'); }
function closeModal() { $('modal').classList.add('hidden'); }
$('modal').addEventListener('pointerdown', (e) => { if (e.target === $('modal')) closeModal(); });

// ---------------- khởi động ----------------
async function refreshSaves() {
  const r = await api.get('/api/saves'); const el = $('saveslist'); if (!r.ok || !r.saves.length) { el.innerHTML = ''; return; }
  el.innerHTML = '<b>Ván đã lưu</b>' + r.saves.map((s) => `<div class="sv"><span>${s.name}<br><small>${s.time.replace('T', ' ')}</small></span><button class="btn small" data-load="${s.name}">Chơi tiếp</button></div>`).join('');
  el.querySelectorAll('[data-load]').forEach((b) => b.onclick = () => loadGame(b.dataset.load));
}
$('rnd').onclick = () => { $('seed').value = Math.floor(Math.random() * 900000) + 1000; };
$('newgame').onclick = async () => {
  $('startmsg').textContent = 'Đang tạo thế giới…'; $('newgame').disabled = true;
  const r = await api.post('/api/new', { seed: +$('seed').value || 1, cells: +$('cells').value });
  $('newgame').disabled = false; if (!r.ok) { $('startmsg').textContent = r.error || 'Lỗi'; return; }
  await enterWorld(true);
};

async function enterWorld(pick) {
  $('startmsg').textContent = ''; const [m, w] = await Promise.all([api.get('/api/map'), api.get('/api/world')]);
  UI.info = w; $('start').classList.add('hidden'); $('game').classList.remove('hidden'); WorldView.player = w.player;
  WorldView.init(m, w); const st = await api.get('/api/state'); UI.state = st; WorldView.setOwners(st.owners); updateCityState(st);
  UI.seenEvent = st.events.length ? st.events[st.events.length - 1].week : -1; UI.sig = {};
  if (pick) startPick(); else startPlay();
}

// ---------------- chọn nước ----------------
function startPick() {
  UI.picking = true; $('pick').classList.remove('hidden'); $('topbar').style.visibility = 'hidden'; $('tabs').style.display = 'none'; setPickCountry(-1);
  const list = UI.info.countries.slice().sort((a, b) => b.pop - a.pop);
  $('panel').innerHTML = '<div class="card"><h4>Các quốc gia</h4><small>Bấm để xem; bấm vào bản đồ cũng được.</small></div>' + list.map((c) => `<div class="crow" data-c="${c.id}"><span class="dot" style="background:${countryColor(c.id)}"></span><span class="nm">${c.name}<br><small>${personality(c.personality)} · ${c.provinces} tỉnh · ${fmt(c.pop)} dân${c.port ? ' · có cảng' : ''}</small></span></div>`).join('');
  $('panel').querySelectorAll('[data-c]').forEach((el) => el.onclick = () => { setPickCountry(+el.dataset.c); const c = UI.info.countries[+el.dataset.c]; WorldView.focus(c.cx, c.cy, WorldView.minS * 2.2); });
  WorldView.onPick = (p) => { if (p.prov >= 0) { setPickCountry(WorldView.owners[p.prov]); } };
}
function personality(p) { return { Trade: 'Thương mại', Industrial: 'Công nghiệp', Military: 'Quân sự', Technology: 'Công nghệ', Neutral: 'Trung lập', Expansionist: 'Bành trướng', Balanced: 'Cân bằng' }[p] || p; }
function setPickCountry(id) {
  UI.pickCountry = id; WorldView.selCountry = id; WorldView.pulse = id >= 0; WorldView.dirty = true; const c = id >= 0 ? UI.info.countries[id] : null;
  $('pickinfo').innerHTML = c ? `<div style="margin:8px 0"><span class="dot" style="display:inline-block;width:16px;height:16px;border-radius:50%;border:3px solid #3b2a18;background:${countryColor(id)};vertical-align:middle"></span> <b style="font-size:18px">${c.name}</b><br><small>${personality(c.personality)} · ${c.provinces} tỉnh · ${fmt(c.pop)} dân${c.port ? ' · có cảng' : ' · không có cảng'}</small></div>` : '';
  $('pickgo').disabled = !c;
}
$('pickgo').onclick = async () => {
  const r = await api.post('/api/player', { country: UI.pickCountry }); if (!r.ok) { toast('Không chọn được nước này', 'bad'); return; }
  UI.info = await api.get('/api/world'); WorldView.player = UI.info.player; startPlay();
};

async function startPlay() {
  UI.picking = false; $('pick').classList.add('hidden'); $('topbar').style.visibility = 'visible'; $('tabs').style.display = 'flex'; WorldView.selCountry = -1; WorldView.pulse = false;
  WorldView.player = UI.info.player; const st = await api.get('/api/state'); applyState(st, true); const c = UI.info.countries[st.player]; WorldView.focus(c.cx, c.cy, WorldView.minS * 1.8);
  WorldView.onPick = onMapPick; setTab('nation'); setSpeed(0);
  let seen = false; try { seen = localStorage.getItem('vigo_tut') === '1'; } catch (e) { }
  if (!seen) {
    showModal(`<h3>Chào mừng, nhà lãnh đạo!</h3><p>Không có chiến thắng ở đây — chỉ có <b>cuộc sống của người dân</b> tốt hơn hay tệ hơn.</p>
    <p>🌾 Giữ cho dân đủ ăn · 🏫🏥 Đầu tư giáo dục, y tế · 🤝 Kết bạn, tránh chiến tranh vô ích · 🏙 Quản lý thành phố của bạn.</p>
    <p><small>Bấm ▶ để thời gian trôi (phím Space tạm dừng). Mất hết tỉnh là thua — nhưng bạn luôn có thể 💾 quay lại mốc cũ.</small></p><button class="btn big" id="tutok">Bắt đầu</button>`);
    $('tutok').onclick = () => { try { localStorage.setItem('vigo_tut', '1'); } catch (e) { } closeModal(); };
  }
}

// ---------------- vòng lặp thời gian ----------------
const SPEEDS = [0, 1100, 550, 220];
function setSpeed(i) {
  UI.speed = i; clearInterval(UI.timer); UI.timer = null; if (i > 0) UI.timer = setInterval(tick, SPEEDS[i]);
  ['bpause', 'bplay1', 'bplay2', 'bplay4'].forEach((id, k) => $(id).classList.toggle('on', k === i)); ['cpause', 'cplay1', 'cplay2'].forEach((id, k) => $(id).classList.toggle('on', k === i));
}
async function tick() {
  if (UI.busy || !UI.state || UI.state.gameOver) return; UI.busy = true;
  try { const r = await api.post('/api/step', { n: 1 }); if (r.ok) applyState(r.state); } finally { UI.busy = false; }
}
$('bpause').onclick = () => setSpeed(0); $('bplay1').onclick = () => setSpeed(1); $('bplay2').onclick = () => setSpeed(2); $('bplay4').onclick = () => setSpeed(3);
$('cpause').onclick = () => setSpeed(0); $('cplay1').onclick = () => setSpeed(1); $('cplay2').onclick = () => setSpeed(2);
document.addEventListener('keydown', (e) => { if (e.target.tagName === 'INPUT') return; if (e.key === ' ') { setSpeed(UI.speed ? 0 : 1); e.preventDefault(); } if (e.key === '1') setSpeed(1); if (e.key === '2') setSpeed(2); if (e.key === '3') setSpeed(3); });

function updateCityState(st) { WorldView.setCities(st.cities); WorldView.countryLive = st.countries; }

function applyState(st, quiet) {
  const prev = UI.state; UI.state = st; WorldView.setOwners(st.owners); updateCityState(st);
  $('cname').textContent = st.country; $('cdate').textContent = `Năm ${st.year} · tuần ${(st.week % 52) + 1}`;
  $('spop').textContent = fmt(st.me.pop); $('streas').textContent = fmt(st.me.treasury); $('sfood').textContent = pct(st.me.food); $('sqol').textContent = st.me.cityQoL > 0 ? Math.round(st.me.cityQoL * 100) : '–';
  $('streas').style.color = st.me.treasury < 0 ? '#d6246b' : ''; $('sfood').style.color = st.me.food < 0.85 ? '#d6246b' : '';
  const f = UI.info.countries[st.player].flag; $('myflag').style.background = flagCss(f, UI.info.countries[st.player].pattern);
  const ob = $('offerbadge'); ob.textContent = st.offers.length; ob.classList.toggle('hidden', st.offers.length === 0);
  // sự kiện mới liên quan đến mình
  if (!quiet) for (const e of st.events) { if (e.week > UI.seenEvent && (e.a === st.player || e.b === st.player)) toast((EV_ICON[e.kind] || '•') + ' ' + e.text, ['war', 'capture', 'betrayal', 'eliminated'].includes(e.kind) ? 'bad' : 'good'); }
  if (st.events.length) UI.seenEvent = Math.max(UI.seenEvent, st.events[st.events.length - 1].week);
  if (!UI.picking) renderPanel();
  if (CityView.running && prev && st.week !== prev.week) refreshCity();
  if (st.gameOver && !UI.over) { UI.over = true; setSpeed(0); gameOver(); }
}
function flagCss(f, pattern) {
  const c = (n) => '#' + n.toString(16).padStart(6, '0'); const [a, b, d] = f.map(c);
  switch (pattern) { case 0: return `linear-gradient(${a} 33%,${b} 33% 66%,${d} 66%)`; case 1: return `linear-gradient(90deg,${a} 33%,${b} 33% 66%,${d} 66%)`; case 3: return `linear-gradient(135deg,${a} 50%,${b} 50%)`; case 5: return `radial-gradient(circle,${d} 28%,${a} 30%)`; default: return `linear-gradient(${a} 50%,${b} 50%)`; }
}
async function refreshCity() { const r = await api.get('/api/city?id=' + CityView.id); if (r.ok) CityView.setData(r.city); }

function gameOver() {
  showModal(`<h3>💀 Đất nước đã sụp đổ</h3><p>${UI.state.country} không còn tỉnh nào. Lãnh đạo bị xử lý — ván chơi kết thúc.</p><p><small>Bạn có thể quay lại một mốc trước đó để chơi khác đi.</small></p><div class="acts"><button class="btn blue" id="goRewind">⏪ Quay lại mốc cũ</button><button class="btn" id="goNew">Ván mới</button></div>`);
  $('goRewind').onclick = () => { closeModal(); UI.over = false; openMenu(); }; $('goNew').onclick = () => location.reload();
}

// ---------------- bảng điều khiển ----------------
document.querySelectorAll('#tabs button').forEach((b) => b.onclick = () => setTab(b.dataset.tab));
function setTab(t) { UI.tab = t; UI.sig = {}; document.querySelectorAll('#tabs button').forEach((b) => b.classList.toggle('on', b.dataset.tab === t)); $('panel').innerHTML = ''; UI.built = false; renderPanel(); }
function renderPanel() { const t = UI.tab; if (t === 'nation') renderNation(); else if (t === 'diplo') renderDiplo(); else if (t === 'news') renderNews(); else renderCities(); }

const barRow = (id, n, c) => `<div class="barrow"><span>${n}</span><div class="bar"><i id="${id}" style="width:0;background:${c}"></i></div><b id="${id}v"></b></div>`;
function setBar(id, v, text) { const e = $(id); if (!e) return; e.style.width = Math.round(Math.max(0, Math.min(1, v)) * 100) + '%'; $(id + 'v').textContent = text !== undefined ? text : pct(v); }
function renderNation() {
  const st = UI.state, m = st.me;
  if (!UI.built) {
    UI.built = true;
    $('panel').innerHTML = `<div class="card"><h4>Đời sống người dân</h4>${barRow('b_food', 'Lương thực', '#4fcf3a')}${barRow('b_fuel', 'Nhiên liệu', '#ff9a3c')}${barRow('b_goods', 'Hàng tiêu dùng', '#ffc93c')}${barRow('b_edu', 'Giáo dục', '#4fb6ff')}${barRow('b_health', 'Y tế', '#ff5d8f')}${barRow('b_qol', 'QoL thành phố', '#7a5cff')}</div>
    <div class="card"><h4>Tài chính <small id="fin"></small></h4>
      <div class="slider"><span>Thuế</span><input type="range" id="s_tax" min="0" max="60" step="1"><b id="s_taxv"></b></div>
      <div style="font-size:12px;color:#5b5384;margin:2px 0 6px">Phần ngân sách còn lại (ngoài 4 khoản dưới) dùng cho bộ máy nhà nước.</div>
      <div class="slider"><span>Giáo dục</span><input type="range" id="s_edu" min="0" max="60" step="1"><b id="s_eduv"></b></div>
      <div class="slider"><span>Y tế</span><input type="range" id="s_health" min="0" max="60" step="1"><b id="s_healthv"></b></div>
      <div class="slider"><span>Đầu tư</span><input type="range" id="s_invest" min="0" max="60" step="1"><b id="s_investv"></b></div>
      <div class="slider"><span>Quốc phòng</span><input type="range" id="s_defense" min="0" max="60" step="1"><b id="s_defensev"></b></div>
      <div id="bsum" style="font-size:12px;text-align:right"></div></div>
    <div class="card"><h4>Quân đội</h4><div id="army"></div></div>`;
    ['tax', 'edu', 'health', 'invest', 'defense'].forEach((k) => { const el = $('s_' + k); el.oninput = () => { $('s_' + k + 'v').textContent = el.value + '%'; sumLabel(); UI.sliderTouched = performance.now(); clearTimeout(UI.sdeb); UI.sdeb = setTimeout(sendBudget, 350); }; });
    UI.sliderTouched = 0;
  }
  setBar('b_food', m.food); setBar('b_fuel', m.fuel); setBar('b_goods', m.goods); setBar('b_edu', m.edu); setBar('b_health', m.health); setBar('b_qol', m.cityQoL, m.cityQoL > 0 ? Math.round(m.cityQoL * 100) + '' : '–');
  $('fin').textContent = `thu ${fmt(m.taxRevenue)}/tuần`;
  if (performance.now() - UI.sliderTouched > 1500) {
    const set = (k, v) => { const el = $('s_' + k); el.value = Math.round(v * 100); $('s_' + k + 'v').textContent = Math.round(v * 100) + '%'; };
    set('tax', m.tax); set('edu', m.budget.edu); set('health', m.budget.health); set('invest', m.budget.invest); set('defense', m.budget.defense); sumLabel();
  }
  $('army').innerHTML = `<div class="barrow" style="grid-template-columns:1fr auto"><span>Quân số</span><b>${fmt(m.troops)}</b></div><div class="barrow" style="grid-template-columns:1fr auto"><span>Sức mạnh</span><b>${fmt(m.power)}</b></div><div class="barrow" style="grid-template-columns:1fr auto"><span>Chiến tranh · Đồng minh</span><b>${m.wars} · ${m.allies}</b></div>`;
}
function sumLabel() { const s = ['edu', 'health', 'invest', 'defense'].reduce((a, k) => a + +$('s_' + k).value, 0); const e = $('bsum'); if (e) { e.textContent = `Tổng 4 khoản: ${s}%` + (s > 100 ? ' (sẽ thu nhỏ lại cho vừa 100%)' : ''); e.style.color = s > 100 ? '#d6246b' : ''; } }
async function sendBudget() {
  const v = (k) => +$('s_' + k).value / 100; await api.post('/api/tax', { rate: v('tax') });
  const r = await api.post('/api/budget', { edu: v('edu'), health: v('health'), invest: v('invest'), defense: v('defense') }); UI.sliderTouched = 0; if (r.ok) UI.state = r.state;
}

function tagsFor(c) { let t = ''; if (c.atWar) t += '<span class="tag2 war">Chiến tranh</span>'; if (c.allied) t += '<span class="tag2 ally">Liên minh</span>'; if (c.pact) t += '<span class="tag2 pact">Hiệp ước</span>'; if (c.trade) t += '<span class="tag2 trade">Thương mại</span>'; return t; }
function renderDiplo() {
  const st = UI.state; const sig = JSON.stringify([st.offers, st.countries.map((c) => [c.opinion, c.allied, c.pact, c.trade, c.atWar, c.elim, c.prov])]) + UI.openC; if (UI.sig.d === sig) return; UI.sig.d = sig;
  const offerTxt = (o) => ['liên minh', 'hiệp ước không xâm lược', 'hiệp ước thương mại'][o.type];
  let h = '';
  if (st.offers.length) h += '<div class="card" style="background:#fff6c8"><h4>✉️ Đề nghị đến</h4>' + st.offers.map((o) => `<div style="margin:6px 0"><b>${UI.info.countries[o.from].name}</b> đề nghị ${offerTxt(o)}<div class="acts" style="margin-top:4px"><button class="btn" data-off="${o.id}" data-acc="1">Chấp nhận</button><button class="btn red" data-off="${o.id}" data-acc="0">Từ chối</button></div></div>`).join('') + '</div>';
  const list = st.countries.filter((c) => c.id !== st.player && !c.elim).sort((a, b) => (b.atWar - a.atWar) || (b.neighbor + b.sea - a.neighbor - a.sea) || b.pop - a.pop);
  h += list.map((c) => {
    const n = UI.info.countries[c.id]; const open = UI.openC === c.id; const rel = c.neighbor ? 'láng giềng' : c.sea ? 'kề biển' : 'xa';
    const op = Math.max(-100, Math.min(100, c.opinion)); const opcol = op >= 0 ? '#4fcf3a' : '#ff5d8f';
    let acts = '';
    if (open) {
      const can = c.neighbor || c.sea; acts = '<div class="acts" style="margin-top:8px">'
        + (c.atWar ? `<button class="btn blue" data-d="peace" data-t="${c.id}">🕊️ Xin hòa</button>` : (can ? `<button class="btn red" data-d="war" data-t="${c.id}">⚔️ Tuyên chiến</button>` : ''))
        + (!c.atWar && !c.allied ? `<button class="btn" data-d="alliance" data-t="${c.id}">🤝 Liên minh</button>` : '')
        + (c.allied ? `<button class="btn" data-d="breakAlliance" data-t="${c.id}">Rút liên minh</button>` : '')
        + (!c.atWar && !c.pact ? `<button class="btn" data-d="pact" data-t="${c.id}">📜 Hiệp ước</button>` : '')
        + (!c.atWar && !c.trade ? `<button class="btn" data-d="trade" data-t="${c.id}">📦 Thương mại</button>` : '') + '</div>';
    }
    return `<div class="crow" data-open="${c.id}" style="flex-wrap:wrap"><span class="dot" style="background:${countryColor(c.id)}"></span><span class="nm">${n.name}${tagsFor(c)}<br><small>${rel} · ${c.prov} tỉnh · sức mạnh ${fmt(c.power)}</small></span><div style="width:70px"><div class="bar" style="height:12px"><i style="width:${(op + 100) / 2}%;background:${opcol}"></i></div><small>${op > 0 ? '+' : ''}${op}</small></div>${acts ? '<div style="width:100%">' + acts + '</div>' : ''}</div>`;
  }).join('');
  $('panel').innerHTML = h;
  $('panel').querySelectorAll('[data-off]').forEach((b) => b.onclick = async (e) => { e.stopPropagation(); const r = await api.post('/api/offer', { id: +b.dataset.off, accept: +b.dataset.acc }); toast(r.ok ? (+b.dataset.acc ? '🤝 Đã chấp nhận' : 'Đã từ chối') : 'Đề nghị không còn hiệu lực', r.ok ? 'good' : 'bad'); if (r.state) applyState(r.state, true); });
  $('panel').querySelectorAll('[data-open]').forEach((el) => el.onclick = (e) => { if (e.target.closest('button')) return; UI.openC = UI.openC === +el.dataset.open ? -1 : +el.dataset.open; UI.sig.d = ''; renderDiplo(); });
  $('panel').querySelectorAll('[data-d]').forEach((b) => b.onclick = (e) => { e.stopPropagation(); diplo(b.dataset.d, +b.dataset.t); });
}
async function diplo(action, target) {
  const c = UI.state.countries[target]; const name = UI.info.countries[target].name;
  if (action === 'war') {
    const bind = c.allied || c.pact; const go = async () => { closeModal(); const r = await api.post('/api/diplo', { action, target, breakTreaties: bind ? 1 : 0 }); toast(r.ok ? `⚔️ Đã tuyên chiến với ${name}` : 'Không tuyên chiến được (cần giáp ranh hoặc có cảng nếu qua biển)', r.ok ? 'bad' : 'bad'); if (r.state) applyState(r.state, true); };
    showModal(`<h3>⚔️ Tuyên chiến với ${name}?</h3><p>Sức mạnh của bạn <b>${fmt(UI.state.me.power)}</b> so với <b>${fmt(c.power)}</b>.${bind ? '<br><b style="color:#d6246b">Bạn đang có hiệp ước/liên minh với họ — tuyên chiến là bội ước, cả thế giới sẽ bớt tin bạn.</b>' : ''}</p><p><small>Chiến tranh tốn quân và tiền, làm dân khổ. Hãy chắc chắn.</small></p><div class="acts"><button class="btn red" id="okwar">Tuyên chiến</button><button class="btn" id="nowar">Thôi</button></div>`);
    $('okwar').onclick = go; $('nowar').onclick = closeModal; return;
  }
  const r = await api.post('/api/diplo', { action, target });
  const okMsg = { peace: '🕊️ Hòa bình!', alliance: '🤝 Liên minh thành lập', pact: '📜 Đã ký hiệp ước', trade: '📦 Đã ký thương mại', breakAlliance: 'Đã rút khỏi liên minh' }[action];
  toast(r.ok ? okMsg : 'Họ từ chối (thiện cảm chưa đủ hoặc điều kiện chưa phù hợp)', r.ok ? 'good' : 'bad'); if (r.state) applyState(r.state, true);
}

function renderNews() {
  const st = UI.state; const sig = JSON.stringify(st.events.map((e) => e.week + e.kind + e.a + e.b)); if (UI.sig.n === sig) return; UI.sig.n = sig;
  const ev = st.events.slice().reverse(); $('panel').innerHTML = ev.length ? ev.map((e) => `<div class="news"><span class="ic">${EV_ICON[e.kind] || '•'}</span><div>${e.text}<br><small>Năm ${1 + Math.floor(e.week / 52)} · tuần ${(e.week % 52) + 1}</small></div></div>`).join('') : '<div class="card">Chưa có sự kiện nào. Thế giới đang yên bình.</div>';
}
function myCities() { const st = UI.state; return st.cities.filter((c) => c.owner === st.player).map((c) => ({ ...c, ...UI.info.cities[c.id] })).sort((a, b) => b.pop - a.pop); }
function renderCities() {
  const cs = myCities(); const sig = JSON.stringify(cs.map((c) => [c.id, c.pop, c.open])); if (UI.sig.c === sig) return; UI.sig.c = sig;
  $('panel').innerHTML = '<div class="card"><h4>Thành phố của bạn</h4><small>Mở một thành phố để quy hoạch đường, nhà ở, dịch vụ. Thành phố chạy cùng đồng hồ với thế giới; chất lượng sống của nó ảnh hưởng ngược lên tỉnh.</small></div>'
    + cs.slice(0, 40).map((c) => `<div class="crow" data-city="${c.id}"><span class="dot" style="background:${c.capital ? '#ffc93c' : '#fff'}"></span><span class="nm">${c.name}${c.capital ? ' ⭐' : ''}<br><small>${fmt(c.pop)} dân · ${UI.info.provinces[c.prov].name}${c.port ? ' · cảng' : ''}${c.open ? ' · 🏙 đang quản lý' : ''}</small></span><button class="btn small">${c.open ? 'Vào' : 'Mở'}</button></div>`).join('');
  $('panel').querySelectorAll('[data-city]').forEach((el) => el.onclick = () => openCity(+el.dataset.city));
}

// ---------------- chọn trên bản đồ ----------------
function onMapPick(p) {
  const st = UI.state; $('provcard').classList.add('hidden'); WorldView.selProv = -1;
  if (p.city >= 0) { const c = UI.info.cities[p.city]; const cs = st.cities[p.city]; return cityCard(c, cs); }
  if (p.prov >= 0) { WorldView.selProv = p.prov; provCard(p.prov); } WorldView.dirty = true;
}
function provCard(pid) {
  const st = UI.state, pr = UI.info.provinces[pid], o = WorldView.owners[pid], oc = UI.info.countries[o], live = st.countries[o]; const mine = o === st.player;
  const pops = st.cities.filter((c) => UI.info.cities[c.id].prov === pid).reduce((a, c) => a + c.pop, 0);
  $('provcard').innerHTML = `<h3>${pr.name}</h3><div class="kv"><span>Thuộc về</span><b>${oc.name}${mine ? ' (bạn)' : ''}</b></div><div class="kv"><span>Dân thành thị</span><b>${fmt(pops)}</b></div>`
    + (!mine ? `<div class="kv"><span>Quan hệ</span><b>${live.opinion > 0 ? '+' : ''}${live.opinion}${live.atWar ? ' · đang đánh nhau' : ''}</b></div><div class="acts"><button class="btn blue" id="pc_d">Ngoại giao</button></div>` : '');
  $('provcard').classList.remove('hidden'); const b = $('pc_d'); if (b) b.onclick = () => { UI.openC = o; setTab('diplo'); };
}
function cityCard(c, cs) {
  const mine = cs.owner === UI.state.player; const oc = UI.info.countries[cs.owner];
  $('provcard').innerHTML = `<h3>🏠 ${c.name}${c.capital ? ' ⭐' : ''}</h3><div class="kv"><span>Dân</span><b>${fmt(cs.pop)}</b></div><div class="kv"><span>Thuộc về</span><b>${oc.name}${mine ? ' (bạn)' : ''}</b></div>` + (mine ? `<div class="acts"><button class="btn" id="pc_o">${cs.open ? 'Vào thành phố' : 'Quản lý thành phố'}</button></div>` : '');
  $('provcard').classList.remove('hidden'); const b = $('pc_o'); if (b) b.onclick = () => openCity(c.id);
}

async function openCity(id) {
  const cs = UI.state.cities[id]; const wasOpen = cs.open; const was = UI.speed; setSpeed(0);
  if (wasOpen) { await CityView.open(id, false); setSpeed(was); return; }
  $('cityview').classList.remove('hidden'); $('cityname').textContent = '🏙 ' + UI.info.cities[id].name; $('cityprompt').classList.remove('hidden');
  const go = async (demo) => { $('cityprompt').classList.add('hidden'); const ok = await CityView.open(id, demo); if (ok) { const st = await api.get('/api/state'); applyState(st, true); } else $('cityview').classList.add('hidden'); };
  $('cdemo').onclick = () => go(true); $('cempty').onclick = () => go(false);
}
$('cityback').onclick = () => { CityView.close(); renderPanel(); };

// ---------------- lưu / nạp / quay lại ----------------
$('bmenu').onclick = () => openMenu();
async function openMenu() {
  setSpeed(0); const [sv, hist] = await Promise.all([api.get('/api/saves'), api.get('/api/history')]);
  const week = UI.state.week;
  showModal(`<h3>💾 Lưu · Nạp · Quay lại</h3>
  <div class="row"><input id="savename" value="vanchoi" style="flex:1;width:auto"><button class="btn" id="dosave">Lưu</button></div>
  <div class="card" style="margin-top:10px"><h4>Quay lại mốc cũ</h4><small>Hệ thống tự lưu mỗi quý. Chọn một mốc để chơi lại từ đó (các mốc muộn hơn sẽ mất).</small><div class="acts" style="margin-top:6px">${hist.weeks && hist.weeks.length ? hist.weeks.map((w) => `<button class="btn" data-rw="${w}">Năm ${1 + Math.floor(w / 52)} · t${(w % 52) + 1}</button>`).join('') : '<small>Chưa có mốc nào.</small>'}</div></div>
  <div class="card"><h4>Bản đã lưu</h4>${sv.saves && sv.saves.length ? sv.saves.map((s) => `<div class="row"><span style="flex:1">${s.name}</span><button class="btn small" data-ld="${s.name}">Nạp</button></div>`).join('') : '<small>Chưa có bản lưu.</small>'}</div>
  <div class="acts"><button class="btn" id="mclose">Đóng</button><button class="btn red" id="mnew">Về màn hình đầu</button></div>`);
  $('dosave').onclick = async () => { const r = await api.post('/api/save', { name: $('savename').value }); toast(r.ok ? '💾 Đã lưu: ' + r.name : 'Lưu thất bại', r.ok ? 'good' : 'bad'); closeModal(); };
  $('mclose').onclick = closeModal; $('mnew').onclick = () => location.reload();
  document.querySelectorAll('[data-rw]').forEach((b) => b.onclick = async () => { const r = await api.post('/api/rewind', { week: +b.dataset.rw }); closeModal(); if (r.ok) { UI.over = false; CityView.close(); await reloadAfterJump(r.state); toast('⏪ Đã quay lại', 'good'); } else toast('Không quay lại được', 'bad'); });
  document.querySelectorAll('[data-ld]').forEach((b) => b.onclick = () => { closeModal(); loadGame(b.dataset.ld); });
}
async function reloadAfterJump(state) { UI.info = await api.get('/api/world'); UI.sig = {}; UI.built = false; WorldView.player = UI.info.player; applyState(state, true); }
async function loadGame(name) {
  const r = await api.post('/api/load', { name }); if (!r.ok) { toast(r.error || 'Nạp thất bại', 'bad'); $('startmsg').textContent = r.error || ''; return; }
  UI.over = false; CityView.close(); await enterWorld(false); toast('📂 Đã nạp ' + name, 'good');
}

refreshSaves();
api.get('/api/hello').then((h) => { if (h.hasGame) { const b = document.createElement('button'); b.className = 'btn big blue'; b.textContent = 'Tiếp tục ván đang chạy'; b.onclick = async () => { await enterWorld(false); }; $('newgame').after(b); } });
