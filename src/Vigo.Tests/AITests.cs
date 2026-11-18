using System;
using System.Linq;
using Vigo.Core;
using Vigo.Core.AI;
using Vigo.Core.Economy;

namespace Vigo.Tests
{
    internal static class AITests
    {
        private static WorldState Gen(ulong seed) => WorldGenerator.Generate(new WorldConfig { Seed = seed, CellCount = 20000 });
        private static void Run(EconomySim e, CountryAI ai, int weeks) { for (int i = 0; i < weeks; i++) { e.StepWeek(); ai?.Think(); } }
        public static double Debt(EconomySim e) { double d = 0; for (int c = 0; c < e.S.Treasury.Length; c++) if (!e.S.Dead[c]) d += Math.Max(0, -e.S.Treasury[c]) / Math.Max(1e-9, e.S.Indicators[c].Population); return d; }

        public static void Register(Action<string, Action> add, Action<bool, string> assert)
        {
            add("AI quốc gia: tất định và chỉ ra lệnh hợp lệ (thuế 0–40%, tổng chia ngân sách ≤ 100%)", () =>
            {
                EconomySim a = new EconomySim(Gen(8)), b = new EconomySim(Gen(8));
                var ai1 = new CountryAI(a); var ai2 = new CountryAI(b);
                Run(a, ai1, 520); Run(b, ai2, 520);
                for (int c = 0; c < a.S.TaxRate.Length; c++)
                {
                    assert(a.S.TaxRate[c] >= 0 && a.S.TaxRate[c] <= 0.4 + 1e-9, "thuế ngoài khoảng " + a.S.TaxRate[c]);
                    double sh = a.S.ShareEdu[c] + a.S.ShareHealth[c] + a.S.ShareInvest[c] + a.S.ShareDefense[c];
                    assert(sh <= 1 + 1e-9, "tổng chia ngân sách " + sh);
                    assert(a.S.TaxRate[c] == b.S.TaxRate[c] && a.S.Treasury[c] == b.S.Treasury[c], "không tất định ở nước " + c);
                }
                assert(a.AuditGoods() < 1e-9, "sổ cái");
            });

            add("AI quốc gia: không động vào nước của người chơi", () =>
            {
                var e = new EconomySim(Gen(9)); var ai = new CountryAI(e); var me = e.W.Countries[3].Id; ai.SetHuman(me, true);
                e.SetTaxRate(me, 0.33); e.SetBudget(me, 0.1, 0.2, 0.3, 0.05);
                Run(e, ai, 260);
                assert(e.S.TaxRate[me.Value] == 0.33 && e.S.ShareInvest[me.Value] == 0.3, "AI đã sửa chính sách của người chơi");
                assert(Enumerable.Range(0, e.S.TaxRate.Length).Any(c => c != me.Value && e.S.ShareDefense[c] != 0.15), "AI không làm gì với nước khác");
            });

            add("AI quốc gia: mỗi tính cách một chính sách khác nhau (quân sự chi quốc phòng cao, công nghệ chi giáo dục cao)", () =>
            {
                var e = new EconomySim(Gen(10)); var ai = new CountryAI(e); Run(e, ai, 52);
                double Avg(Personality p, Func<int, double> f) { var l = e.W.Countries.Where(c => c.Personality == p).Select(c => f(c.Id.Value)).ToList(); return l.Count == 0 ? double.NaN : l.Average(); }
                double milDef = Avg(Personality.Military, c => e.S.ShareDefense[c]), techDef = Avg(Personality.Technology, c => e.S.ShareDefense[c]);
                double techEdu = Avg(Personality.Technology, c => e.S.ShareEdu[c]), milEdu = Avg(Personality.Military, c => e.S.ShareEdu[c]);
                if (!double.IsNaN(milDef) && !double.IsNaN(techDef)) assert(milDef > techDef + 0.05, "quân sự " + milDef + " vs công nghệ " + techDef);
                if (!double.IsNaN(techEdu) && !double.IsNaN(milEdu)) assert(techEdu > milEdu + 0.05, "giáo dục công nghệ " + techEdu + " vs quân sự " + milEdu);
            });

            add("AI quốc gia: khi thiếu ăn thì cắt quốc phòng và dồn đầu tư vào nông nghiệp", () =>
            {
                var w = Gen(11); var e = new EconomySim(w); var ai = new CountryAI(e); Run(e, ai, 52);
                var v = w.Countries.First(c => c.Personality == Personality.Military || c.Personality == Personality.Expansionist);
                double defBefore = e.S.ShareDefense[v.Id.Value];
                foreach (var o in w.Countries) if (o.Id != v.Id) e.SetTradeBlock(v.Id, o.Id, true);
                foreach (var p in v.Provinces) e.DamageProvince(p, 0.7);
                Run(e, ai, 40);
                assert(e.S.Indicators[v.Id.Value].FoodSat < 0.95, "kịch bản không gây thiếu ăn, FoodSat=" + e.S.Indicators[v.Id.Value].FoodSat);
                assert(e.S.ShareDefense[v.Id.Value] < defBefore * 0.5, "chưa cắt quốc phòng: " + defBefore + " → " + e.S.ShareDefense[v.Id.Value]);
                var iw = e.S.InvestWeight[v.Id.Value]; assert(iw != null && iw[0] == iw.Max(), "đầu tư chưa ưu tiên nông nghiệp");
            });

            add("AI quốc gia: không làm mất ổn định thế giới (so với chính sách cố định: không thêm nước đói, đời sống không tụt quá 0.08)", () =>
            {
                double lifeNo = 0, lifeAi = 0; int hungryNo = 0, hungryAi = 0;
                foreach (ulong sd in new ulong[] { 101, 202, 303 })
                {
                    var no = new EconomySim(Gen(sd)); var yes = new EconomySim(Gen(sd)); var ai = new CountryAI(yes);
                    Run(no, null, 1560); Run(yes, ai, 1560);
                    hungryNo += no.S.Indicators.Count(x => x.FoodSat < 0.9); hungryAi += yes.S.Indicators.Count(x => x.FoodSat < 0.9);
                    lifeNo += no.S.Indicators.Average(x => (x.FoodSat + x.FuelSat + x.GoodsSat + x.Health + x.Edu) / 5) / 3; lifeAi += yes.S.Indicators.Average(x => (x.FoodSat + x.FuelSat + x.GoodsSat + x.Health + x.Edu) / 5) / 3;
                    assert(yes.AuditGoods() < 1e-9, "sổ cái");
                }
                Console.Write($" [đời sống {lifeNo:0.00}→{lifeAi:0.00}; nước đói {hungryNo}→{hungryAi}]");
                assert(hungryAi <= hungryNo, "AI làm tăng số nước đói"); assert(lifeAi >= lifeNo - 0.08, "đời sống tụt: " + lifeNo + " → " + lifeAi);
            });
        }
    }
}
