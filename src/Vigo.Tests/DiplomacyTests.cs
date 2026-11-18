using System;
using System.Linq;
using Vigo.Core;
using Vigo.Core.AI;
using Vigo.Core.Economy;
using Vigo.Core.Military;

namespace Vigo.Tests
{
    internal static class DiplomacyTests
    {
        private static WorldState Gen(ulong seed) => WorldGenerator.Generate(new WorldConfig { Seed = seed, CellCount = 20000 });
        private static (WorldState w, EconomySim e, WarSim war) Make(ulong seed) { var w = Gen(seed); var e = new EconomySim(w); return (w, e, new WarSim(w, e)); }
        private static Country Other(WorldState w, params Country[] not) => w.Countries.First(c => !not.Contains(c) && !c.Eliminated);
        private static void Make2(WarSim war, int a, int b) { war.Delta[a, b] = 100; war.Delta[b, a] = 100; }

        public static void Register(Action<string, Action> add, Action<bool, string> assert)
        {
            add("Quan hệ: tất định, trong ±100; tuyên chiến làm nạn nhân ghét mạnh và thế giới giảm thiện cảm với kẻ gây chiến", () =>
            {
                var (w, e, war) = Make(61); var (w2, e2, war2) = Make(61);
                for (int i = 0; i < w.Countries.Count; i++) for (int j = 0; j < w.Countries.Count; j++) { assert(war.GetOpinion(i, j) == war2.GetOpinion(i, j), "nền thiện cảm không tất định"); assert(Math.Abs(war.GetOpinion(i, j)) <= 100, "ngoài ±100"); }
                var (a, b) = MilitaryTests.Pair(w); var o = Other(w, a, b); double vb = war.GetOpinion(b.Id.Value, a.Id.Value), vo = war.GetOpinion(o.Id.Value, a.Id.Value);
                assert(war.DeclareWar(a.Id, b.Id), "không tuyên chiến được");
                assert(war.GetOpinion(b.Id.Value, a.Id.Value) <= vb - 50 || war.GetOpinion(b.Id.Value, a.Id.Value) == -100, "nạn nhân chưa ghét kẻ xâm lược");
                assert(war.GetOpinion(o.Id.Value, a.Id.Value) < vo, "thế giới không giảm thiện cảm");
            });

            add("Liên minh: chỉ lập khi thiện cảm đủ cao, không với kẻ đang đánh đồng minh, tối đa 3, đối xứng", () =>
            {
                var (w, e, war) = Make(62); var (a, b) = MilitaryTests.Pair(w); int x = a.Id.Value, y = b.Id.Value;
                war.Delta[y, x] = -100; assert(!war.ProposeAlliance(a.Id, b.Id), "thiện cảm thấp mà vẫn lập liên minh");
                war.Delta[y, x] = 100; if (!(w.Countries[y].Personality == Personality.Military || w.Countries[y].Personality == Personality.Expansionist)) assert(war.ProposeAlliance(a.Id, b.Id), "thiện cảm cao mà bị từ chối");
                else { e.S.Troops[x] = 1e6; e.S.Troops[y] = 1e6; assert(war.ProposeAlliance(a.Id, b.Id), "bị từ chối"); }
                assert(war.Allied(x, y) && war.Allied(y, x), "liên minh không đối xứng"); assert(!war.ProposeAlliance(a.Id, b.Id), "lập liên minh hai lần");
                // trần 3 liên minh
                var third = w.Countries.Where(c => c.Id.Value != x && c.Id.Value != y).Take(5).ToList(); int made = 0;
                foreach (var c in third) { war.Delta[y, c.Id.Value] = 100; e.S.Troops[c.Id.Value] = 1e6; e.S.Troops[y] = 1e6; if (war.ProposeAlliance(c.Id, b.Id)) made++; }
                assert(war.Allies(y).Count <= 3, "vượt 3 liên minh: " + war.Allies(y).Count);
                // không lập với kẻ đang đánh đồng minh
                var (w3, e3, war3) = Make(63); var (p, q) = MilitaryTests.Pair(w3); var r = Other(w3, p, q);
                war3.Delta[q.Id.Value, r.Id.Value] = 100; war3.Delta[r.Id.Value, q.Id.Value] = 100; e3.S.Troops[q.Id.Value] = 1e6; e3.S.Troops[r.Id.Value] = 1e6; e3.S.Troops[p.Id.Value] = 1e6;
                war3.Treaties.Add(new Treaty { Type = TreatyType.Alliance, A = q.Id.Value, B = p.Id.Value, StartWeek = 0, Expire = -1 });
                war3.Wars.Add(new War { A = r.Id.Value, B = p.Id.Value, StartWeek = 0 });
                assert(!war3.ProposeAlliance(r.Id, q.Id), "lập liên minh với kẻ đang đánh đồng minh của mình");
            });

            add("Hiệp ước/liên minh chặn tuyên chiến; bội ước (breakTreaties) phải trả giá uy tín", () =>
            {
                var (w, e, war) = Make(64); var (a, b) = MilitaryTests.Pair(w); int x = a.Id.Value, y = b.Id.Value;
                war.Treaties.Add(new Treaty { Type = TreatyType.NonAggression, A = x, B = y, StartWeek = 0, Expire = 1000 });
                assert(!war.DeclareWar(a.Id, b.Id), "tuyên chiến dù có hiệp ước"); assert(war.HasPact(x, y), "hiệp ước biến mất");
                var o = Other(w, a, b); double before = war.GetOpinion(o.Id.Value, x);
                assert(war.DeclareWar(a.Id, b.Id, breakTreaties: true), "bội ước không được"); assert(!war.HasPact(x, y), "hiệp ước còn sau bội ước");
                assert(war.Events.Any(ev => ev.Kind == "betrayal"), "không ghi sự kiện bội ước"); assert(war.GetOpinion(o.Id.Value, x) <= before - 25 || war.GetOpinion(o.Id.Value, x) == -100, "bội ước không mất uy tín với bên thứ ba");
                var (w2, e2, war2) = Make(64); var (a2, b2) = MilitaryTests.Pair(w2);
                war2.Treaties.Add(new Treaty { Type = TreatyType.Alliance, A = a2.Id.Value, B = b2.Id.Value, StartWeek = 0, Expire = -1 });
                assert(!war2.DeclareWar(a2.Id, b2.Id), "tuyên chiến với đồng minh");
                // hiệp ước hết hạn
                var (w3, e3, war3) = Make(64); war3.Treaties.Add(new Treaty { Type = TreatyType.NonAggression, A = 0, B = 1, StartWeek = 0, Expire = 3 }); MilitaryTests.Step(e3, war3, 5);
                assert(war3.Treaties.Count == 0, "hiệp ước hết hạn vẫn còn");
            });

            add("Kêu gọi đồng minh: đồng minh của nạn nhân vào cuộc (trừ khi sợ kẻ quá mạnh); chiến tranh phụ kết thúc cùng cuộc chiến chính", () =>
            {
                var (w, e, war) = Make(65); var (a, b) = MilitaryTests.Pair(w); int x = a.Id.Value, y = b.Id.Value;
                var c = w.Countries.First(k => k.Id.Value != x && k.Id.Value != y && !(k.Personality == Personality.Military || k.Personality == Personality.Expansionist) && !(k.Neighbors.Contains(a.Id) && false)); int z = c.Id.Value;
                foreach (var k in w.Countries) { e.S.Troops[k.Id.Value] = 1e6; }
                war.Treaties.Add(new Treaty { Type = TreatyType.Alliance, A = y, B = z, StartWeek = 0, Expire = -1 });
                assert(war.DeclareWar(a.Id, b.Id), "tuyên chiến"); assert(war.AtWar(z, x), "đồng minh không vào cuộc");
                var jw = war.Wars.First(q => q.A == x && q.B == z); assert(jw.MasterA == x && jw.MasterB == y, "chiến tranh phụ không gắn với chiến tranh chính");
                assert(e.S.Blocked.Contains(x * 10000L + z), "không cấm vận với đồng minh của địch");
                war.MakePeace(a.Id, b.Id); assert(!war.AtWar(z, x) && !war.AtWar(x, y), "chiến tranh phụ không kết thúc cùng chiến tranh chính"); assert(e.S.Blocked.Count == 0, "còn cấm vận");
                // sợ kẻ quá mạnh: từ chối
                var (w2, e2, war2) = Make(65); var (a2, b2) = MilitaryTests.Pair(w2); int x2 = a2.Id.Value, y2 = b2.Id.Value;
                var c2 = w2.Countries.First(k => k.Id.Value != x2 && k.Id.Value != y2 && !(k.Personality == Personality.Military || k.Personality == Personality.Expansionist)); int z2 = c2.Id.Value;
                e2.S.Troops[x2] = 1e7; e2.S.Troops[z2] = 1e5; e2.S.Troops[y2] = 1e5;
                war2.Treaties.Add(new Treaty { Type = TreatyType.Alliance, A = y2, B = z2, StartWeek = 0, Expire = -1 });
                assert(war2.DeclareWar(a2.Id, b2.Id), "tuyên chiến"); assert(!war2.AtWar(z2, x2), "đồng minh yếu vẫn vào cuộc chống kẻ quá mạnh"); assert(war2.Events.Any(ev => ev.Kind == "refuse"), "không ghi sự từ chối");
            });

            add("Răn đe: AI không tấn công mục tiêu có đồng minh mạnh (tính sức mạnh liên minh), nhưng tấn công khi không có", () =>
            {
                int Attacks(bool withAlliance)
                {
                    var (w, e, war) = Make(66); var (a, b) = MilitaryTests.Pair(w); int x = a.Id.Value, y = b.Id.Value;
                    var c = w.Countries.First(k => k.Id.Value != x && k.Id.Value != y); int z = c.Id.Value;
                    foreach (var k in w.Countries) e.S.Troops[k.Id.Value] = 1e7; e.S.Troops[x] = 2e6; e.S.Troops[y] = 2e5; e.S.Troops[z] = 5e6;
                    foreach (var k in w.Countries) if (k.Id.Value != x) k.Personality = Personality.Neutral; a.Personality = Personality.Military;
                    if (withAlliance) war.Treaties.Add(new Treaty { Type = TreatyType.Alliance, A = y, B = z, StartWeek = 0, Expire = -1 });
                    var dip = new DiplomacyAI(war) { Aggression = 1.0, Treaties = false }; e.S.Week = 13; dip.Think();
                    return war.AtWar(x, y) ? 1 : 0;
                }
                assert(Attacks(false) == 1, "không có liên minh mà AI vẫn không tấn công kẻ yếu (ratio 10×)");
                assert(Attacks(true) == 0, "AI vẫn tấn công mục tiêu có đồng minh mạnh");
            });

            add("Hòa đàm có điều kiện: nhượng tỉnh + bồi thường chuyển zero-sum, AI thua từ chối điều kiện quá nặng, không xóa sổ qua hòa đàm", () =>
            {
                var (w, e, war) = Make(67); var (a, b) = MilitaryTests.Pair(w); int x = a.Id.Value, y = b.Id.Value;
                e.S.Troops[x] = 1e6; e.S.Troops[y] = 1e6; war.DeclareWar(a.Id, b.Id); war.Wars[0].Goal = 100;
                var cede = b.Provinces.Where(p => p != b.CapitalProvince).Take(1).ToList();
                assert(!war.ProposeTerms(a.Id, b.Id, cede, 0), "ratio 1.0 mà bên thua vẫn nhượng");
                e.S.Troops[x] = 2e6; e.S.Troops[y] = 1e6; // ratio 2
                assert(!war.ProposeTerms(a.Id, b.Id, b.Provinces.Take(b.Provinces.Count - 1).ToList(), 0), "nhượng quá nhiều tỉnh");
                assert(!war.ProposeTerms(a.Id, b.Id, new[] { b.CapitalProvince }, 0), "nhượng thủ đô khi ratio<3");
                double cap = 10 * e.S.TaxRate[y] * e.PotentialVA(y); assert(!war.ProposeTerms(a.Id, b.Id, cede, cap * 2), "bồi thường vượt trần");
                double money = e.S.Treasury[x] + e.S.Treasury[y]; int na = a.Provinces.Count, nb = b.Provinces.Count; double rep = cap * 0.5;
                assert(war.ProposeTerms(a.Id, b.Id, cede, rep), "điều kiện hợp lý mà bị từ chối");
                assert(Math.Abs(e.S.Treasury[x] + e.S.Treasury[y] - money) < 1e-6 * Math.Max(1, Math.Abs(money)), "tiền bồi thường không zero-sum");
                assert(a.Provinces.Count == na + 1 && b.Provinces.Count == nb - 1 && !war.AtWar(x, y), "tỉnh/hòa không đúng");
                MilitaryTests.Invariants(w, assert);
                // ratio 5: được đòi thủ đô
                var (w2, e2, war2) = Make(67); var (a2, b2) = MilitaryTests.Pair(w2); e2.S.Troops[a2.Id.Value] = 5e6; e2.S.Troops[b2.Id.Value] = 1e6; war2.DeclareWar(a2.Id, b2.Id);
                assert(war2.ProposeTerms(a2.Id, b2.Id, new[] { b2.CapitalProvince }, 0), "ratio 5 phải chấp nhận nhượng thủ đô");
                MilitaryTests.Invariants(w2, assert);
            });

            add("Lưu/nạp: liên minh, hiệp ước, thiện cảm, chiến tranh phụ — nạp rồi chạy tiếp giống hệt chạy liền", () =>
            {
                var (w, e, war) = Make(68); var ai = new CountryAI(e) { War = war }; var dip = new DiplomacyAI(war) { Aggression = 0.6 };
                var human = w.Countries[2]; dip.SetHuman(human.Id, true);
                for (int i = 0; i < 2600; i++) { e.StepWeek(); war.StepWeek(); ai.Think(); dip.Think(); }
                assert(war.Treaties.Count > 0, "chưa có hiệp ước nào để lưu");
                var bytes = SaveGame.ToBytes(w, e, war); SaveGame.FromBytes(bytes, out var w2, out var e2, out var war2);
                assert(war2.Treaties.Count == war.Treaties.Count && war2.HumanCountries.SequenceEqual(war.HumanCountries), "hiệp ước/người chơi không khớp");
                var ai2 = new CountryAI(e2) { War = war2 }; var dip2 = new DiplomacyAI(war2) { Aggression = 0.6 }; dip2.SetHuman(human.Id, true);
                for (int i = 0; i < 300; i++) { e.StepWeek(); war.StepWeek(); ai.Think(); dip.Think(); e2.StepWeek(); war2.StepWeek(); ai2.Think(); dip2.Think(); }
                assert(MilitaryTests.Sig(e, war) == MilitaryTests.Sig(e2, war2), "trạng thái sau nạp khác chạy liền");
                Console.Write($" [lưu với {war.Treaties.Count} hiệp ước, {war.Events.Count} sự kiện]");
            });

            add("Toàn hệ thống 60 năm × 3 seed: có liên minh/hiệp ước, bất biến và sổ cái giữ nguyên, thế giới không sụp (so với không liên minh)", () =>
            {
                int aliveT = 0, aliveN = 0;
                foreach (ulong sd in new ulong[] { 12345, 777, 31337 })
                {
                    foreach (bool tr in new[] { true, false })
                    {
                        var (w, e, war) = Make(sd); var ai = new CountryAI(e) { War = war }; var dip = new DiplomacyAI(war) { Treaties = tr };
                        int maxAl = 0; for (int i = 0; i < 3120; i++) { e.StepWeek(); war.StepWeek(); ai.Think(); dip.Think(); if (i % 52 == 0) maxAl = Math.Max(maxAl, war.Treaties.Count(t => t.Type == TreatyType.Alliance)); }
                        int alive = w.Countries.Count(c => !c.Eliminated); if (tr) aliveT += alive; else aliveN += alive;
                        MilitaryTests.Invariants(w, assert); assert(e.AuditGoods() < 1e-9, "sổ cái");
                        assert(alive >= 6 && alive < 30, "seed " + sd + (tr ? " (có liên minh)" : " (không)") + ": còn " + alive);
                        if (tr) { assert(war.Events.Any(ev => ev.Kind == "pact" || ev.Kind == "alliance"), "AI không lập hiệp ước/liên minh nào, seed " + sd); Console.Write($" [seed {sd}: {alive} nước còn, liên minh tối đa {maxAl}]"); }
                        for (int c = 0; c < w.Countries.Count; c++) for (int d = 0; d < w.Countries.Count; d++) assert(Math.Abs(war.GetOpinion(c, d)) <= 100 && !double.IsNaN(war.Delta[c, d]), "thiện cảm lỗi");
                    }
                }
                Console.Write($" [tổng nước còn sau 60 năm: có liên minh {aliveT} vs không {aliveN}]");
            });

            add("AI áp điều kiện hòa đàm: bên thắng thế nhận tỉnh biên giới + bồi thường; bất biến thế giới giữ nguyên; không áp lên người chơi; tắt Terms thì không có", () =>
            {
                int Run(bool terms, bool human, out int humanLoss)
                {
                    var (w, e, war) = Make(75); var ai = new CountryAI(e) { War = war }; var dip = new DiplomacyAI(war) { Aggression = 0.6, Terms = terms };
                    var h = w.Countries[2]; if (human) dip.SetHuman(h.Id, true); int hp = h.Provinces.Count; int n = 0, seenTerms = 0;
                    for (int i = 0; i < 1500; i++) { e.StepWeek(); war.StepWeek(); ai.Think(); dip.Think(); }
                    foreach (var ev in war.Events) if (ev.Kind == "terms") { seenTerms++; if (human && ev.B == h.Id.Value) n++; }
                    MilitaryTests.Invariants(w, assert); humanLoss = n; return seenTerms;
                }
                int on = Run(true, true, out int hl), off = Run(false, true, out int hl2);
                Console.Write($" [điều kiện hòa đàm: bật {on}, tắt {off}]");
                assert(on > 0, "AI không bao giờ áp điều kiện hòa đàm");
                assert(off == 0, "tắt Terms mà vẫn có điều kiện hòa đàm (từ nguồn khác?)");
                assert(hl == 0, "AI áp điều kiện hòa đàm lên nước người chơi");
            });

            add("Hiệp ước thương mại: cần thiện cảm ≥ 0, tối đa 4 đối tác, tan khi có chiến tranh; đối tác được ưu tiên khi hàng khan hiếm", () =>
            {
                var (w, e, war) = Make(76); var (a, b) = MilitaryTests.Pair(w); int x = a.Id.Value, y = b.Id.Value;
                war.Delta[y, x] = -100; assert(!war.ProposeTrade(a.Id, b.Id), "thiện cảm thấp mà vẫn ký thương mại");
                war.Delta[y, x] = 100; assert(war.ProposeTrade(a.Id, b.Id) && war.HasTrade(x, y) && war.HasTrade(y, x), "không ký được/không đối xứng");
                assert(!war.ProposeTrade(a.Id, b.Id), "ký hai lần");
                war.StepWeek(); e.StepWeek(); war.StepWeek();
                assert(e.TradePref[x, y] >= 3 && e.TradePref[y, x] >= 3, "ưu tiên thương mại chưa được áp: " + e.TradePref[x, y]);
                assert(war.DeclareWar(a.Id, b.Id, true), "không tuyên chiến được"); war.StepWeek(); e.StepWeek(); war.StepWeek(); war.StepWeek(); war.StepWeek();
                assert(!war.HasTrade(x, y), "chiến tranh mà hiệp ước thương mại vẫn còn");
                // trần 4 đối tác
                var (w2, e2, war2) = Make(76); var c = w2.Countries[0]; int made = 0;
                foreach (var o in w2.Countries.Skip(1).Take(8)) { war2.Delta[o.Id.Value, 0] = 100; war2.Delta[0, o.Id.Value] = 100; if (war2.ProposeTrade(c.Id, o.Id)) made++; }
                assert(made == WarSim.MaxTradePartners && war2.TradePartners(0).Count == WarSim.MaxTradePartners, "vượt trần đối tác: " + made);
            });

            add("Hiệp ước thương mại: khi lương thực khan hiếm, nước có hiệp ước với nhà xuất khẩu nhận nhiều hơn khi không có hiệp ước", () =>
            {
                double Run(bool treaty, double scale, out double rival)
                {
                    var w = Gen(77); var e = new EconomySim(w); var war = new WarSim(w, e);
                    var ordered = w.Countries.OrderByDescending(c => e.S.Stock[c.Id.Value][0]).ToList();
                    var exporter = ordered[0]; var buyer = ordered[ordered.Count - 1]; var rivalC = ordered[ordered.Count - 2];
                    int pi = exporter.Id.Value, bi = buyer.Id.Value, ri = rivalC.Id.Value;
                    foreach (var c in w.Countries) if (c.Id.Value != pi) e.S.Stock[c.Id.Value][0] = 0;
                    e.S.Stock[pi][0] *= scale; foreach (var c in w.Countries) e.S.Treasury[c.Id.Value] = Math.Max(e.S.Treasury[c.Id.Value], 1e9);
                    if (treaty) { war.Delta[pi, bi] = 100; war.Delta[bi, pi] = 100; assert(war.ProposeTrade(buyer.Id, exporter.Id), "không ký được"); }
                    war.RefreshTradePref(); e.RunTradeOnly();
                    rival = e.S.Stock[ri][0]; return e.S.Stock[bi][0];
                }
                // mức khan hiếm phụ thuộc kho nhà xuất khẩu: thử nhiều mức, hiệp ước không được làm người mua thiệt ở mức nào và phải có lợi ở ít nhất một mức khan hiếm
                bool gain = false; double lastW = 0, lastWo = 0;
                foreach (double sc in new[] { 0.8, 1.0, 1.2, 1.5, 2.0, 3.0 })
                {
                    double with = Run(true, sc, out double rw), without = Run(false, sc, out double rwo);
                    assert(with >= without - 1e-9 * Math.Max(1, without), "hiệp ước làm người mua nhận ít hơn ở mức " + sc + ": " + with + " vs " + without);
                    if (with > without * 1.001 + 1) { gain = true; lastW = with; lastWo = without; }
                }
                Console.Write($" [có lợi rõ ở ít nhất một mức khan hiếm: {gain}; ví dụ kho người mua {lastW:0} vs {lastWo:0}]");
                assert(gain, "hiệp ước thương mại không giúp người mua ở bất kỳ mức khan hiếm nào");
            });
        }
    }
}
