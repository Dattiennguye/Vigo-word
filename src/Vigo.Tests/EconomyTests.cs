using System;
using System.Linq;
using Vigo.Core;
using Vigo.Core.Economy;

namespace Vigo.Tests
{
    internal static class EconomyTests
    {
        private static WorldState Gen(ulong seed = 12345) => WorldGenerator.Generate(new WorldConfig { Seed = seed, CellCount = 20000 });
        private static void Run(EconomySim e, int weeks) { for (int i = 0; i < weeks; i++) e.StepWeek(); }
        private static double TotalVA(EconomySim e) => e.S.Indicators.Sum(x => x.ValueAdded);
        private static double Sig(EconomySim e) { double h = 0; for (int c = 0; c < e.S.Stock.Length; c++) { for (int g = 0; g < EconData.GoodCount; g++) h += e.S.Stock[c][g] * (g + 1) * (1 + c % 7); h += e.S.Treasury[c] * 1.37; } return h + e.S.Pop.Sum() * 0.1; }

        public static void Register(Action<string, Action> add, Action<bool, string> assert)
        {
            add("Kinh tế: sổ cái hàng hóa bảo toàn (3 seed × 10 năm)", () =>
            {
                foreach (ulong sd in new ulong[] { 12345, 777, 31337 })
                {
                    var e = new EconomySim(Gen(sd)); Run(e, 520);
                    double err = e.AuditGoods(); assert(err < 1e-9, "seed " + sd + ": sai lệch sổ cái " + err);
                    assert(e.S.Ledger.TradeVolume.Sum() > 0, "không có giao thương nào xảy ra");
                }
            });

            add("Kinh tế: giao thương không tạo/hủy hàng và tiền (zero-sum)", () =>
            {
                var e = new EconomySim(Gen()); Run(e, 60);
                // Ép có giao thương: xóa sạch hàng tiêu dùng của một nước giàu tiền để nó buộc phải nhập
                var rich = e.W.Countries.OrderByDescending(c => e.S.Treasury[c.Id.Value]).First().Id.Value;
                e.S.Ledger.Lost[(int)Good.Goods] += e.S.Stock[rich][(int)Good.Goods]; e.S.Stock[rich][(int)Good.Goods] = 0;
                double m0 = e.S.Treasury.Sum(); var g0 = new double[EconData.GoodCount]; for (int g = 0; g < g0.Length; g++) g0[g] = e.TotalStock((Good)g);
                double vol0 = e.S.Ledger.TradeVolume.Sum();
                e.RunTradeOnly();
                assert(e.S.Ledger.TradeVolume.Sum() - vol0 > 0, "kịch bản không tạo ra giao thương nên test vô nghĩa");
                assert(Math.Abs(e.S.Treasury.Sum() - m0) < 1e-6 * Math.Max(1, Math.Abs(m0)), "tổng tiền đổi sau giao thương");
                for (int g = 0; g < g0.Length; g++) assert(Math.Abs(e.TotalStock((Good)g) - g0[g]) < 1e-6 * Math.Max(1, g0[g]), "tổng hàng " + (Good)g + " đổi sau giao thương");
            });

            add("Kinh tế: tất định (cùng seed → cùng trạng thái; khác seed → khác)", () =>
            {
                var a = new EconomySim(Gen(5)); var b = new EconomySim(Gen(5)); var c = new EconomySim(Gen(6));
                Run(a, 260); Run(b, 260); Run(c, 260);
                assert(Sig(a) == Sig(b), "cùng seed nhưng khác kết quả"); assert(Sig(a) != Sig(c), "khác seed nhưng trùng kết quả");
            });

            add("Kinh tế: soak 30 nước × 30 năm — không NaN, không tồn kho âm, dân không nổ", () =>
            {
                var w = Gen(99); var e = new EconomySim(w); double p0 = e.S.Pop.Sum(); Run(e, 1560);
                for (int c = 0; c < w.Countries.Count; c++)
                {
                    for (int g = 0; g < EconData.GoodCount; g++)
                    {
                        double v = e.S.Stock[c][g]; assert(!double.IsNaN(v) && !double.IsInfinity(v), "NaN/Inf tồn kho"); assert(v > -1e-6, "tồn kho âm " + (Good)g + " " + v);
                        assert(e.S.Price[c][g] > 0 && e.S.Price[c][g] < 1000, "giá bất thường");
                    }
                    assert(!double.IsNaN(e.S.Treasury[c]), "NaN kho bạc");
                }
                double p1 = e.S.Pop.Sum(); assert(p1 > p0 * 0.9 && p1 < p0 * 3, "dân số " + p0 + " → " + p1);
                assert(e.AuditGoods() < 1e-9, "sổ cái");
            });

            add("Kinh tế: tăng thuế → thu nhiều hơn ngắn hạn, sản lượng thấp hơn dài hạn", () =>
            {
                var lo = new EconomySim(Gen()); var hi = new EconomySim(Gen());
                foreach (var c in hi.W.Countries) hi.SetTaxRate(c.Id, 0.5);
                Run(lo, 104); Run(hi, 104);
                double revLo = lo.S.Indicators.Sum(x => x.TaxRevenue), revHi = hi.S.Indicators.Sum(x => x.TaxRevenue);
                assert(revHi > revLo, "thuế cao phải thu nhiều hơn: " + revHi + " vs " + revLo);
                assert(TotalVA(hi) < TotalVA(lo), "thuế cao phải giảm sản lượng: " + TotalVA(hi) + " vs " + TotalVA(lo));
            });

            add("Kinh tế: ngân sách giáo dục/y tế kéo chỉ số lên và tăng sản lượng", () =>
            {
                var a = new EconomySim(Gen()); var b = new EconomySim(Gen());
                foreach (var c in a.W.Countries) a.SetBudget(c.Id, 0, 0, 0.2, 0.1);
                foreach (var c in b.W.Countries) b.SetBudget(c.Id, 0.3, 0.3, 0.2, 0.1);
                Run(a, 260); Run(b, 260);
                assert(a.S.Edu.Average() < 0.2 && b.S.Edu.Average() > 0.7, "Edu " + a.S.Edu.Average() + " / " + b.S.Edu.Average());
                assert(b.S.Health.Average() > a.S.Health.Average() + 0.4, "y tế");
                assert(TotalVA(b) > TotalVA(a), "giáo dục tốt phải tăng sản lượng");
            });

            add("Kinh tế: bị cấm vận + mất đất canh tác → thiếu ăn, giá lương thực tăng, dân giảm", () =>
            {
                var w = Gen(); var e = new EconomySim(w); Run(e, 26);
                var victim = w.Countries.OrderBy(c => c.Id.Value).First(); int v = victim.Id.Value; double pop0 = victim.Population;
                foreach (var c in w.Countries) if (c.Id != victim.Id) e.SetTradeBlock(victim.Id, c.Id, true);
                foreach (var p in victim.Provinces) e.DamageProvince(p, 0.9);
                Run(e, 52);
                var ind = e.S.Indicators[v];
                assert(ind.FoodSat < 0.7, "FoodSat " + ind.FoodSat);
                assert(e.S.Price[v][(int)Good.Food] > 1.5, "giá lương thực " + e.S.Price[v][(int)Good.Food]);
                assert(w.Countries[v].Population < pop0, "dân phải giảm vì đói");
                assert(e.AuditGoods() < 1e-9, "sổ cái");
            });

            add("Kinh tế: mỏ dầu cạn → sản lượng dầu về 0, nhiên liệu khan hiếm, giá tăng, trữ lượng không âm", () =>
            {
                var w = Gen(); var e = new EconomySim(w);
                for (int d = 0; d < w.Deposits.Count; d++) if (w.Deposits[d].Type == ResourceType.Oil) e.S.DepRemaining[d] = 1e-3 * e.S.DepRate[d];
                double oilBefore = e.S.Ledger.Produced[(int)Good.Oil];
                Run(e, 200);
                double produced = e.S.Ledger.Produced[(int)Good.Oil];
                double totalCap = Enumerable.Range(0, w.Deposits.Count).Where(d => w.Deposits[d].Type == ResourceType.Oil).Sum(d => e.S.DepRate[d]);
                assert(produced < totalCap * 0.05, "dầu sản xuất " + produced + " quá nhiều so với trữ lượng");
                for (int d = 0; d < w.Deposits.Count; d++) assert(e.S.DepRemaining[d] >= 0, "trữ lượng âm");
                double fuelSat = e.S.Indicators.Sum(x => x.FuelSat * x.Population) / e.S.Indicators.Sum(x => x.Population);
                assert(fuelSat < 0.5, "nhiên liệu phải khan hiếm, sat=" + fuelSat);
                assert(e.S.Price.Average(p => p[(int)Good.Fuel]) > EconData.BasePrice[(int)Good.Fuel] * 1.5, "giá nhiên liệu phải tăng");
                assert(e.AuditGoods() < 1e-9, "sổ cái");
            });

            add("Kinh tế: đầu tư máy móc mở rộng công suất (so với không đầu tư thì suy giảm)", () =>
            {
                var inv = new EconomySim(Gen()); var none = new EconomySim(Gen());
                foreach (var c in inv.W.Countries) inv.SetBudget(c.Id, 0.15, 0.15, 0.4, 0.05);
                foreach (var c in none.W.Countries) none.SetBudget(c.Id, 0.15, 0.15, 0.0, 0.05);
                Run(inv, 520); Run(none, 520);
                double Cap(EconomySim e) => e.S.FarmCap.Sum() + e.S.IndCap.Sum(a => a.Sum());
                assert(Cap(inv) > Cap(none) * 1.1, "công suất đầu tư " + Cap(inv) + " vs " + Cap(none));
                assert(Cap(none) < Cap(new EconomySim(Gen())), "không đầu tư phải khấu hao");
                assert(inv.S.Ledger.Invested[(int)Good.Machinery] > 0, "chưa dùng máy móc");
            });

            add("Chiến tranh: chuyển tỉnh giữ bất biến, năng lực chuyển theo chủ mới, diệt nước → mất hết tồn kho", () =>
            {
                var w = Gen(); var e = new EconomySim(w); Run(e, 26);
                var a = w.Countries[0]; var b = w.Countries[a.Neighbors.Count > 0 ? a.Neighbors[0].Value : 1];
                long popA = a.Population, popB = b.Population; var prov = a.Provinces.First(p => w.GetProvince(p).Id != a.CapitalProvince);
                double capBefore = e.S.IndCap[prov.Value].Sum(); long pp = w.GetProvince(prov).Population;
                assert(WorldOps.TransferProvince(w, prov, b.Id), "không chuyển được");
                assert(w.GetProvince(prov).Owner == b.Id && b.Provinces.Contains(prov) && !a.Provinces.Contains(prov), "danh sách tỉnh");
                foreach (int c in w.GetProvince(prov).Cells) assert(w.CountryOfCell[c] == b.Id.Value, "CountryOfCell");
                assert(a.Population + b.Population == popA + popB && a.Population == popA - pp, "dân số không bảo toàn");
                assert(e.S.IndCap[prov.Value].Sum() == capBefore, "công suất phải đi theo tỉnh");
                Run(e, 4);
                // Diệt hoàn toàn nước A
                foreach (var p in a.Provinces.ToList()) WorldOps.TransferProvince(w, p, b.Id);
                assert(a.Eliminated && a.Provinces.Count == 0 && a.LandCells == 0, "chưa bị xóa sổ");
                Run(e, 4);
                assert(e.S.Dead[a.Id.Value] && e.S.Stock[a.Id.Value].All(x => x == 0), "nước bị diệt phải mất tồn kho");
                assert(e.AuditGoods() < 1e-9, "sổ cái sau khi diệt nước");
                foreach (var city in w.Cities.Where(c => c.IsCapital)) assert(w.GetCountry(w.GetProvince(city.Province).Owner).Capital == city.Id, "thủ đô không khớp chủ");
                foreach (var c in w.Countries.Where(x => !x.Eliminated)) assert(c.Capital != CityId.None && w.GetProvince(c.CapitalProvince).Owner == c.Id, c.Name + " thủ đô ngoài lãnh thổ");
            });

            add("Kinh tế: 40 năm × 5 seed — không nước nào đói triền miên ở điều kiện mặc định", () =>
            {
                foreach (ulong sd in new ulong[] { 1, 2, 3, 4, 5 })
                {
                    var e = new EconomySim(Gen(sd * 101)); Run(e, 2080);
                    int starving = e.S.Indicators.Count(x => x.FoodSat < 0.8);
                    assert(starving == 0, "seed " + sd * 101 + ": " + starving + " nước đói");
                }
            });
        }
    }
}
