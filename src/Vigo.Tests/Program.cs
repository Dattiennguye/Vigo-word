using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Vigo.Core;

namespace Vigo.Tests
{
    /// <summary>Bộ test tối giản (không cần NuGet): chạy `dotnet run --project src/Vigo.Tests [-- --seeds N]`.</summary>
    internal static class Program
    {
        private static readonly List<KeyValuePair<string, Action>> Tests = new List<KeyValuePair<string, Action>>();
        private static int _sweepSeeds = 60;

        private static void Add(string name, Action a) { Tests.Add(new KeyValuePair<string, Action>(name, a)); }
        private static void Assert(bool cond, string msg) { if (!cond) throw new Exception(msg); }

        private static WorldConfig SmallConfig(ulong seed) => new WorldConfig { Seed = seed, CellCount = 20000 };

        private static int Main(string[] args)
        {
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "--seeds") _sweepSeeds = int.Parse(args[i + 1]);

            Add("Rng: cùng luồng → cùng chuỗi; khác tên → khác chuỗi", () =>
            {
                var a = Rng.Stream(1, "terrain"); var b = Rng.Stream(1, "terrain"); var c = Rng.Stream(1, "climate");
                bool same = true, diff = false;
                for (int i = 0; i < 100; i++) { ulong x = a.NextU64(), y = b.NextU64(), z = c.NextU64(); if (x != y) same = false; if (x != z) diff = true; }
                Assert(same, "cùng luồng phải ra cùng chuỗi"); Assert(diff, "khác tên luồng phải khác chuỗi");
            });

            Add("Đồ thị ô: kề đối xứng, liên thông, không lưới vuông", () =>
            {
                var w = WorldGenerator.Generate(SmallConfig(5));
                var g = w.Grid;
                for (int i = 0; i < g.Count; i++)
                    foreach (int nb in g.Neighbors(i)) { bool back = false; foreach (int x in g.Neighbors(nb)) if (x == i) back = true; Assert(back, "kề không đối xứng " + i + "-" + nb); }
                var seen = new bool[g.Count]; var st = new Stack<int>(); st.Push(0); seen[0] = true; int cnt = 1;
                while (st.Count > 0) { int c = st.Pop(); foreach (int nb in g.Neighbors(c)) if (!seen[nb]) { seen[nb] = true; cnt++; st.Push(nb); } }
                Assert(cnt == g.Count, "đồ thị ô không liên thông: " + cnt + "/" + g.Count);
                // Số láng giềng trung bình ~ 4–6 (lưới vuông cứng sẽ chính xác 4 hoặc 8 cho mọi ô).
                double avg = (double)g.Nb.Length / g.Count; Assert(avg > 3.5 && avg < 6.5, "độ kề trung bình bất thường " + avg);
                var degs = new HashSet<int>(); for (int i = 0; i < g.Count; i++) degs.Add(g.NbStart[i + 1] - g.NbStart[i]);
                Assert(degs.Count >= 4, "độ kề quá đồng nhất → giống lưới cứng");
                Console.Write($" [kề TB {avg:0.00}, {degs.Count} bậc khác nhau]");
            });

            Add("Tất định: cùng seed → cùng hash; khác seed → khác hash", () =>
            {
                ulong h1 = WorldHash.Compute(WorldGenerator.Generate(SmallConfig(42)));
                ulong h2 = WorldHash.Compute(WorldGenerator.Generate(SmallConfig(42)));
                ulong h3 = WorldHash.Compute(WorldGenerator.Generate(SmallConfig(43)));
                Assert(h1 == h2, "cùng seed nhưng hash khác"); Assert(h1 != h3, "khác seed nhưng hash trùng");
            });

            Add("Thế giới mặc định (40k ô, 30 quốc gia): qua Validator + thời gian", () =>
            {
                var sw = Stopwatch.StartNew();
                var w = WorldGenerator.Generate(new WorldConfig { Seed = 12345 });
                sw.Stop();
                Assert(w.Validation.IsValid, "Validator: " + string.Join("; ", w.Validation.Errors.Take(5)));
                Assert(w.Countries.Count == 30, "số quốc gia");
                Console.Write($" [{sw.ElapsedMilliseconds} ms, {w.Provinces.Count} tỉnh, {w.Cities.Count} thành phố, {w.Roads.Count} đường, thử lần {w.Attempt + 1}]");
                Assert(sw.ElapsedMilliseconds < 30000, "quá 30 giây");
            });

            Add("Bất biến phân cấp (kiểm tra độc lập với Validator)", () =>
            {
                var w = WorldGenerator.Generate(SmallConfig(7));
                for (int i = 0; i < w.CellCount; i++)
                {
                    int p = w.ProvinceOfCell[i], c = w.CountryOfCell[i];
                    Assert((p < 0) == (c < 0), "ô có tỉnh mà không có nước (hoặc ngược lại)");
                    if (p >= 0) { Assert(w.Provinces[p].Owner.Value == c, "chủ ô != chủ tỉnh"); Assert(w.IsLand[i], "ô nước thuộc tỉnh"); }
                }
                foreach (var p in w.Provinces) foreach (int c in p.Cells) Assert(w.ProvinceOfCell[c] == p.Id.Value, "Cells của tỉnh không khớp ProvinceOfCell");
                foreach (var city in w.Cities) { Assert(w.CityOfCell[city.Cell] == city.Id.Value, "CityOfCell"); Assert(w.GetProvince(city.Province).Cities.Contains(city.Id), "tỉnh không biết thành phố"); }
                foreach (var c in w.Countries)
                {
                    int caps = 0; foreach (var pid in c.Provinces) foreach (var cid in w.GetProvince(pid).Cities) if (w.GetCity(cid).IsCapital) caps++;
                    Assert(caps == 1, c.Name + " có " + caps + " thủ đô");
                    Assert(c.Provinces.Count >= 8 && c.Provinces.Count <= 20, c.Name + " số tỉnh " + c.Provinces.Count);
                }
                foreach (var p in w.Provinces) Assert(p.Cities.Count >= 2 && p.Cities.Count <= 8, p.Name + " số thành phố " + p.Cities.Count);
            });

            Add("Tỉnh liên thông (BFS độc lập)", () =>
            {
                var w = WorldGenerator.Generate(SmallConfig(9));
                foreach (var p in w.Provinces)
                {
                    var set = new HashSet<int>(p.Cells); var seen = new HashSet<int> { p.Cells[0] }; var st = new Stack<int>(); st.Push(p.Cells[0]);
                    while (st.Count > 0) { int c = st.Pop(); foreach (int nb in w.Grid.Neighbors(c)) if (set.Contains(nb) && seen.Add(nb)) st.Push(nb); }
                    Assert(seen.Count == set.Count, p.Name + " không liên thông");
                }
            });

            Add("Logic địa lý: thành phố gần nước, cảng ven biển, thủ đô trong lãnh thổ", () =>
            {
                var w = WorldGenerator.Generate(SmallConfig(11));
                int nearWater = 0;
                foreach (var city in w.Cities)
                {
                    Assert(w.IsLand[city.Cell] && !w.IsLake[city.Cell], city.Name + " trên nước");
                    bool near = false; var cell = city.Cell;
                    foreach (int nb in w.Grid.Neighbors(cell)) if (w.IsOcean[nb] || w.IsLake[nb] || w.IsRiver[nb]) near = true;
                    if (w.IsRiver[cell]) near = true;
                    if (near) nearWater++;
                    if (city.IsPort) { bool oc = false; foreach (int nb in w.Grid.Neighbors(cell)) if (w.IsOcean[nb]) oc = true; Assert(oc, city.Name + " cảng không giáp biển"); }
                }
                // Tỉ lệ thành phố gần nước phải cao hơn hẳn tỉ lệ ô đất gần nước (thiên lệch địa lý có thật).
                int landNear = 0, landTot = 0;
                for (int i = 0; i < w.CellCount; i++)
                {
                    if (!w.IsLand[i] || w.IsLake[i]) continue; landTot++;
                    bool near = w.IsRiver[i]; foreach (int nb in w.Grid.Neighbors(i)) if (w.IsOcean[nb] || w.IsLake[nb] || w.IsRiver[nb]) near = true;
                    if (near) landNear++;
                }
                double cityShare = (double)nearWater / w.Cities.Count, landShare = (double)landNear / landTot;
                Console.Write($" [gần nước: thành phố {cityShare:P0} vs đất {landShare:P0}]");
                Assert(cityShare > landShare * 1.3, "thành phố không thiên về gần nước");
            });

            Add("Đường: ô liên tiếp kề nhau, trong một nước, cầu ở ô sông", () =>
            {
                var w = WorldGenerator.Generate(SmallConfig(13));
                Assert(w.Roads.Count > 0, "không có đường");
                foreach (var r in w.Roads)
                {
                    int country = w.CountryOfCell[r.Cells[0]];
                    for (int i = 1; i < r.Cells.Length; i++)
                    {
                        bool adj = false; foreach (int nb in w.Grid.Neighbors(r.Cells[i - 1])) if (nb == r.Cells[i]) adj = true;
                        Assert(adj, "đường đứt đoạn"); Assert(w.CountryOfCell[r.Cells[i]] == country, "đường vượt biên giới");
                    }
                    foreach (int b in r.BridgeCells) Assert(w.IsRiver[b], "cầu không ở ô sông");
                }
            });

            Add("Tài nguyên: phân bố KHÔNG đều giữa các quốc gia", () =>
            {
                var w = WorldGenerator.Generate(SmallConfig(17));
                double giniSum = 0; int types = 0;
                foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
                {
                    var counts = new double[w.Countries.Count];
                    foreach (var d in w.Deposits) if (d.Type == t && !d.Offshore && w.CountryOfCell[d.Cell] >= 0) counts[w.CountryOfCell[d.Cell]] += d.Reserve;
                    if (counts.Sum() <= 0) continue;
                    giniSum += Gini(counts); types++;
                }
                double avg = giniSum / types; Console.Write($" [Gini TB {avg:0.00}]");
                Assert(avg > 0.30, "phân bố quá đều (Gini " + avg + ")");
                // Có quốc gia thiếu hẳn ít nhất một loại.
                bool someMissing = false;
                foreach (ResourceType t in new[] { ResourceType.Coal, ResourceType.Iron, ResourceType.PreciousMetals })
                {
                    var has = new bool[w.Countries.Count];
                    foreach (var d in w.Deposits) if (d.Type == t && !d.Offshore && w.CountryOfCell[d.Cell] >= 0) has[w.CountryOfCell[d.Cell]] = true;
                    if (has.Any(h => !h)) someMissing = true;
                }
                Assert(someMissing, "mọi quốc gia đều có đủ mọi loại → không khan hiếm");
            });

            Add("Cấu hình điều chỉnh được: 12 quốc gia, 5–9 tỉnh", () =>
            {
                var cfg = new WorldConfig { Seed = 3, CellCount = 14000, CountryCount = 12, MinProvincesPerCountry = 5, MaxProvincesPerCountry = 9, LandRatio = 0.5f };
                var w = WorldGenerator.Generate(cfg);
                Assert(w.Validation.IsValid, string.Join("; ", w.Validation.Errors.Take(4)));
                Assert(w.Countries.Count == 12, "số quốc gia");
                foreach (var c in w.Countries) Assert(c.Provinces.Count >= 5 && c.Provinces.Count <= 9, "số tỉnh " + c.Provinces.Count);
            });

            Add("Tỉ lệ đất khớp cấu hình (±0,05)", () =>
            {
                var w = WorldGenerator.Generate(SmallConfig(21));
                int land = 0; for (int i = 0; i < w.CellCount; i++) if (w.IsLand[i]) land++;
                double ratio = (double)land / w.CellCount; Console.Write($" [{ratio:0.000} vs {w.Config.LandRatio:0.000}]");
                Assert(Math.Abs(ratio - w.Config.LandRatio) < 0.05, "tỉ lệ đất " + ratio);
            });

            Add("Quét nhiều seed (20k ô, 30 quốc gia): tỉ lệ qua Validator", () =>
            {
                int ok = 0, firstTry = 0, within3 = 0; var fails = new List<string>(); var sw = Stopwatch.StartNew();
                for (int s = 1; s <= _sweepSeeds; s++)
                {
                    var w = WorldGenerator.Generate(SmallConfig((ulong)(s * 7919)));
                    if (w.Validation.IsValid) { ok++; if (w.Attempt == 0) firstTry++; if (w.Attempt < 3) within3++; }
                    else fails.Add("seed " + (s * 7919) + ": " + w.Validation.Errors.First());
                }
                sw.Stop();
                Console.Write($" [{ok}/{_sweepSeeds} hợp lệ; ngay lần đầu {firstTry}; trong ≤3 lần {within3}; TB {sw.ElapsedMilliseconds / _sweepSeeds} ms/thế giới]");
                foreach (var f in fails.Take(5)) Console.Write("\n      thất bại: " + f);
                Assert(ok >= _sweepSeeds * 0.99, "dưới 99% seed hợp lệ");
            });

            CityTests.Register(Add, Assert);
            EconomyTests.Register(Add, Assert);

            int pass = 0, fail = 0;
            foreach (var t in Tests)
            {
                Console.Write("• " + t.Key + " ... ");
                try { t.Value(); Console.WriteLine("  PASS"); pass++; }
                catch (Exception e) { Console.WriteLine("  FAIL: " + e.Message); fail++; }
            }
            Console.WriteLine($"\nKết quả: {pass} đạt, {fail} lỗi.");
            return fail == 0 ? 0 : 1;
        }

        private static double Gini(double[] v)
        {
            var s = v.OrderBy(x => x).ToArray(); double n = s.Length, sum = s.Sum(), acc = 0;
            for (int i = 0; i < s.Length; i++) acc += (i + 1) * s[i];
            return sum <= 0 ? 0 : (2 * acc) / (n * sum) - (n + 1) / n;
        }
    }
}
