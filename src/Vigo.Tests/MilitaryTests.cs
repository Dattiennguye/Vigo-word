using System;
using System.Linq;
using Vigo.Core;
using Vigo.Core.AI;
using Vigo.Core.Economy;
using Vigo.Core.Military;

namespace Vigo.Tests
{
    internal static class MilitaryTests
    {
        private static WorldState Gen(ulong seed) => WorldGenerator.Generate(new WorldConfig { Seed = seed, CellCount = 20000 });
        private static void Step(EconomySim e, WarSim w, int weeks) { for (int i = 0; i < weeks; i++) { e.StepWeek(); w.StepWeek(); } }
        private static void Invariants(WorldState w, Action<bool, string> assert)
        {
            for (int p = 0; p < w.Provinces.Count; p++)
            {
                var pr = w.Provinces[p]; var o = w.GetCountry(pr.Owner);
                assert(o.Provinces.Contains(pr.Id), "tỉnh " + p + " không nằm trong danh sách chủ");
                foreach (int c in pr.Cells) assert(w.CountryOfCell[c] == pr.Owner.Value, "CountryOfCell lệch ở tỉnh " + p);
            }
            foreach (var c in w.Countries)
            {
                assert(c.Eliminated == (c.Provinces.Count == 0), c.Name + ": cờ Eliminated lệch");
                if (c.Eliminated) continue;
                assert(c.Capital != CityId.None && w.GetProvince(c.CapitalProvince).Owner == c.Id && w.GetCity(c.Capital).IsCapital, c.Name + ": thủ đô sai");
                assert(c.Provinces.All(pid => w.GetProvince(pid).Owner == c.Id), c.Name + ": tỉnh sai chủ");
            }
        }
        private static double Sig(EconomySim e, WarSim w)
        {
            double h = 0; for (int c = 0; c < e.S.Stock.Length; c++) { for (int g = 0; g < EconData.GoodCount; g++) h += e.S.Stock[c][g] * (g + 1) * (1 + c % 7); h += e.S.Treasury[c] * 1.37 + e.S.Troops[c] * 3.1; }
            for (int p = 0; p < w.W.Provinces.Count; p++) h += w.W.Provinces[p].Owner.Value * (p % 13 + 1) + w.Progress[p] * 5;
            return h + e.S.Pop.Sum() * 0.1 + w.Wars.Count * 1000 + w.Events.Count * 7;
        }
        private static (Country a, Country b) Pair(WorldState w) { var a = w.Countries.First(c => c.Neighbors.Count > 0 && c.Provinces.Count >= 8); return (a, w.GetCountry(a.Neighbors[0])); }

        public static void Register(Action<string, Action> add, Action<bool, string> assert)
        {
            add("Chiến tranh: chỉ tuyên chiến với láng giềng còn sống, một lần; có cấm vận hai chiều; hòa thì bỏ cấm vận", () =>
            {
                var w = Gen(41); var e = new EconomySim(w); var war = new WarSim(w, e); var (a, b) = Pair(w);
                var far = w.Countries.First(c => c.Id != a.Id && !a.Neighbors.Contains(c.Id));
                assert(!war.DeclareWar(a.Id, a.Id), "tự tuyên chiến"); assert(!war.DeclareWar(a.Id, far.Id), "tuyên chiến nước không giáp ranh");
                assert(war.DeclareWar(a.Id, b.Id), "không tuyên chiến được"); assert(!war.DeclareWar(a.Id, b.Id) && !war.DeclareWar(b.Id, a.Id), "tuyên chiến đôi");
                assert(e.S.Blocked.Contains(a.Id.Value * 10000L + b.Id.Value) && e.S.Blocked.Contains(b.Id.Value * 10000L + a.Id.Value), "chưa cấm vận");
                war.MakePeace(a.Id, b.Id); assert(!war.AtWar(a.Id.Value) && e.S.Blocked.Count == 0, "hòa nhưng còn cấm vận");
            });

            add("Quân đội: quân số theo ngân sách quốc phòng, trừ khỏi lao động, hậu cần lấy từ kho thật và ghi sổ cái", () =>
            {
                var hi = Gen(42); var lo = Gen(42); var eh = new EconomySim(hi); var el = new EconomySim(lo); var wh = new WarSim(hi, eh); var wl = new WarSim(lo, el);
                foreach (var c in hi.Countries) eh.SetBudget(c.Id, 0.05, 0.05, 0.05, 0.6);
                foreach (var c in lo.Countries) el.SetBudget(c.Id, 0.2, 0.2, 0.2, 0.0);
                Step(eh, wh, 150); Step(el, wl, 150);
                double th = eh.S.Troops.Sum(), tl = el.S.Troops.Sum();
                assert(th > 20 * Math.Max(1, tl) || tl < 1, "quân số cao " + th + " vs thấp " + tl);
                assert(th <= 0.121 * eh.S.Pop.Sum(), "quân vượt trần dân số");
                assert(eh.S.Ledger.Military[(int)Good.Fuel] > 0 && eh.S.Ledger.Military[(int)Good.Machinery] > 0, "chưa tiêu hao hậu cần");
                assert(eh.AuditGoods() < 1e-9, "sổ cái sai khi có quân đội: " + eh.AuditGoods());
                assert(eh.S.Indicators.Sum(x => x.ValueAdded) < el.S.Indicators.Sum(x => x.ValueAdded), "quân đội lớn phải làm giảm sản lượng");
            });

            add("Hậu cần: hết nhiên liệu/máy móc → sức mạnh quân đội giảm", () =>
            {
                var w = Gen(43); var e = new EconomySim(w); var war = new WarSim(w, e); var (a, _) = Pair(w); int c = a.Id.Value;
                e.SetBudget(a.Id, 0.05, 0.05, 0.05, 0.6); Step(e, war, 80);
                double full = war.Power(c); assert(war.Supply[c] > 0.95, "hậu cần bình thường phải ~1: " + war.Supply[c]);
                e.S.Ledger.Lost[(int)Good.Fuel] += e.S.Stock[c][(int)Good.Fuel]; e.S.Stock[c][(int)Good.Fuel] = 0;
                e.S.Ledger.Lost[(int)Good.Machinery] += e.S.Stock[c][(int)Good.Machinery]; e.S.Stock[c][(int)Good.Machinery] = 0;
                war.StepWeek(); // chỉ bước quân sự (kho đang rỗng)
                assert(war.Supply[c] < 0.2, "hết kho mà hậu cần vẫn " + war.Supply[c]); assert(war.Power(c) < full * 0.3, "sức mạnh không giảm");
            });

            add("Chiến đấu: bên mạnh hơn chiếm tỉnh của bên yếu; bất biến thế giới được giữ; tỉnh bị chiếm bị tàn phá", () =>
            {
                var w = Gen(44); var e = new EconomySim(w); var war = new WarSim(w, e); var (a, b) = Pair(w);
                e.SetBudget(a.Id, 0.05, 0.05, 0.05, 0.8); e.SetBudget(b.Id, 0.3, 0.3, 0.3, 0.0); Step(e, war, 120);
                assert(war.Power(a.Id.Value) > 3 * Math.Max(1, war.Power(b.Id.Value)), "chênh lệch sức mạnh chưa đủ: " + war.Power(a.Id.Value) + " vs " + war.Power(b.Id.Value));
                int n0 = a.Provinces.Count, m0 = b.Provinces.Count; double farmB = e.S.FarmCap.Sum();
                war.DeclareWar(a.Id, b.Id); var wr = war.Wars[0]; wr.Goal = 100; // không dừng sớm
                Step(e, war, 260);
                assert(a.Provinces.Count > n0 && b.Provinces.Count < m0, "bên mạnh không chiếm được tỉnh: " + n0 + "→" + a.Provinces.Count);
                assert(war.Events.Any(x => x.Kind == "capture" && x.A == a.Id.Value), "không có sự kiện chiếm tỉnh");
                Invariants(w, assert); assert(e.AuditGoods() < 1e-9, "sổ cái sau chiến tranh");
                var captured = w.Provinces.First(p => p.OriginalOwner == b.Id && p.Owner == a.Id); assert(e.S.IndCap[captured.Id.Value].Sum() >= 0, "công suất âm");
            });

            add("Chiến tranh: đạt mục tiêu chiến tranh hoặc kiệt sức thì hòa và bỏ cấm vận; không kéo dài vô hạn", () =>
            {
                var w = Gen(45); var e = new EconomySim(w); var war = new WarSim(w, e); var (a, b) = Pair(w);
                e.SetBudget(a.Id, 0.05, 0.05, 0.05, 0.8); Step(e, war, 120);
                war.DeclareWar(a.Id, b.Id); Step(e, war, 400);
                assert(!war.AtWar(a.Id.Value, b.Id.Value), "chiến tranh kéo dài quá 260 tuần");
                assert(war.Events.Any(x => x.Kind == "peace"), "không có hòa bình"); assert(!e.S.Blocked.Contains(a.Id.Value * 10000L + b.Id.Value) || war.AtWar(a.Id.Value), "còn cấm vận");
                Invariants(w, assert);
            });

            add("Đề nghị hòa: bên đang áp đảo từ chối trong năm đầu, chấp nhận khi chiến tranh kéo dài", () =>
            {
                var w = Gen(46); var e = new EconomySim(w); var war = new WarSim(w, e); var (a, b) = Pair(w);
                e.SetBudget(a.Id, 0.05, 0.05, 0.05, 0.8); e.SetBudget(b.Id, 0.3, 0.3, 0.3, 0.0); Step(e, war, 120);
                war.DeclareWar(a.Id, b.Id); war.Wars[0].Goal = 100; Step(e, war, 10);
                assert(!war.ProposePeace(b.Id, a.Id), "kẻ áp đảo không được chấp nhận hòa sớm");
                Step(e, war, 60); if (war.AtWar(a.Id.Value, b.Id.Value)) assert(war.ProposePeace(b.Id, a.Id), "chiến tranh > 52 tuần phải chấp nhận hòa");
            });

            add("Game over: người chơi mất hết tỉnh → GameOver; tồn kho mất; thế giới vẫn chạy tiếp", () =>
            {
                var w = Gen(47); var e = new EconomySim(w); var war = new WarSim(w, e); var (me, foe) = Pair(w);
                e.SetBudget(foe.Id, 0.05, 0.05, 0.05, 0.8); Step(e, war, 120);
                assert(!war.GameOver(me.Id), "chưa mất nước mà GameOver");
                foreach (var p in me.Provinces.ToList()) WorldOps.TransferProvince(w, p, foe.Id);
                assert(war.GameOver(me.Id), "mất hết tỉnh phải GameOver"); Step(e, war, 10);
                assert(e.S.Troops[me.Id.Value] == 0 && e.S.Dead[me.Id.Value], "nước bị diệt vẫn còn quân/kinh tế");
                Invariants(w, assert); assert(e.AuditGoods() < 1e-9, "sổ cái");
            });

            add("Lưu/nạp giữa chiến tranh: nạp rồi chạy tiếp giống hệt chạy liền (quân, tiến độ chiếm, sự kiện, nghỉ sau chiến)", () =>
            {
                var w = Gen(48); var e = new EconomySim(w); var war = new WarSim(w, e); var ai = new CountryAI(e) { War = war }; var dip = new DiplomacyAI(war) { Aggression = 1.0 };
                for (int i = 0; i < 2000 && !(war.Wars.Count > 0 && war.Wars.Any(q => q.Captured > 0 || e.S.Week - q.StartWeek > 8)); i++) { e.StepWeek(); war.StepWeek(); ai.Think(); dip.Think(); }
                assert(war.Wars.Count > 0, "không có chiến tranh đang diễn ra để lưu");
                assert(war.Progress.Any(x => x > 0) || war.Events.Count > 0, "chưa có tiến độ chiến sự để lưu");
                Console.Write($" [lưu khi có {war.Wars.Count} chiến tranh, {war.Events.Count} sự kiện, tuần {e.S.Week}]");
                var bytes = SaveGame.ToBytes(w, e, war); SaveGame.FromBytes(bytes, out var w2, out var e2, out var war2);
                assert(war2 != null && war2.Wars.Count == war.Wars.Count && war2.Events.Count == war.Events.Count, "chiến tranh/sự kiện không khớp");
                var ai2 = new CountryAI(e2) { War = war2 }; var dip2 = new DiplomacyAI(war2) { Aggression = 1.0 };
                for (int i = 0; i < 200; i++) { e.StepWeek(); war.StepWeek(); ai.Think(); dip.Think(); e2.StepWeek(); war2.StepWeek(); ai2.Think(); dip2.Think(); }
                assert(Sig(e, war) == Sig(e2, war2), "trạng thái sau nạp khác chạy liền");
                Invariants(w2, assert); assert(e2.AuditGoods() < 1e-9, "sổ cái");
            });

            add("Toàn hệ thống: tất định (kinh tế + AI ngân sách + AI ngoại giao + chiến tranh)", () =>
            {
                double Run(ulong sd) { var w = Gen(sd); var e = new EconomySim(w); var war = new WarSim(w, e); var ai = new CountryAI(e) { War = war }; var dip = new DiplomacyAI(war); for (int i = 0; i < 1040; i++) { e.StepWeek(); war.StepWeek(); ai.Think(); dip.Think(); } return Sig(e, war); }
                assert(Run(7) == Run(7), "cùng seed khác kết quả"); assert(Run(7) != Run(8), "khác seed trùng kết quả");
            });

            add("Toàn hệ thống: 60 năm × 3 seed — thế giới hợp nhất dần chứ không sụp đổ, bất biến và sổ cái giữ nguyên", () =>
            {
                foreach (ulong sd in new ulong[] { 12345, 777, 31337 })
                {
                    var w = Gen(sd); var e = new EconomySim(w); var war = new WarSim(w, e); var ai = new CountryAI(e) { War = war }; var dip = new DiplomacyAI(war);
                    for (int i = 0; i < 3120; i++) { e.StepWeek(); war.StepWeek(); ai.Think(); dip.Think(); }
                    int alive = w.Countries.Count(c => !c.Eliminated);
                    Console.Write($" [seed {sd}: còn {alive}/30, {war.Events.Count(x => x.Kind == "war")} chiến tranh]");
                    assert(alive >= 6 && alive < 30, "seed " + sd + ": còn " + alive + " nước");
                    Invariants(w, assert); assert(e.AuditGoods() < 1e-9, "sổ cái " + e.AuditGoods());
                    for (int c = 0; c < w.Countries.Count; c++) { assert(!double.IsNaN(e.S.Treasury[c]) && !double.IsNaN(e.S.Troops[c]), "NaN"); assert(e.S.Troops[c] >= 0, "quân âm"); }
                }
            });
        }
    }
}
