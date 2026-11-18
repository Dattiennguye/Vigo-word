using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Vigo.Core;

namespace Vigo.Cli
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            ulong seed = 12345; int cells = 40000, width = 1400, countries = 30; string outDir = "out";
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--seed") seed = ulong.Parse(args[i + 1]);
                if (args[i] == "--cells") cells = int.Parse(args[i + 1]);
                if (args[i] == "--out") outDir = args[i + 1];
                if (args[i] == "--width") width = int.Parse(args[i + 1]);
                if (args[i] == "--countries") countries = int.Parse(args[i + 1]);
            }
            for (int i = 0; i < args.Length; i++)
                if (args[i] == "--diag")
                {
                    int count = int.Parse(args[i + 1]); var tally = new Dictionary<string, int>(); int ok = 0;
                    for (int sd = 1; sd <= count; sd++)
                    {
                        var wd = WorldGenerator.Generate(new WorldConfig { Seed = (ulong)(sd * 7919), CellCount = cells, CountryCount = countries, MaxAttempts = 1 });
                        if (wd.Validation.IsValid) { ok++; continue; }
                        foreach (var e in wd.Validation.Errors) if (e.Contains("tỉnh ngoài")) { var cn = wd.Countries.Find(x => e.StartsWith(x.Name + ":")); Console.WriteLine($"  seed {sd*7919}: {e} | cells={cn?.LandCells} lake={(cn==null?0:System.Linq.Enumerable.Count(wd.Provinces.FindAll(pp=>pp.Owner==cn.Id), _=>true)) }");
                            if (cn != null) { int lk=0; for (int ci=0; ci<wd.CellCount; ci++) if (wd.CountryOfCell[ci]==cn.Id.Value && wd.IsLake[ci]) lk++; Console.WriteLine("     lakeCells="+lk+" provinceSizes="+string.Join(",", wd.Provinces.FindAll(pp=>pp.Owner==cn.Id).ConvertAll(pp=>pp.Cells.Length))); } }
                        var cats = new HashSet<string>();
                        foreach (var e in wd.Validation.Errors) { int ix = e.IndexOf(':'); string cat = ix >= 0 ? e.Substring(ix + 1).Trim() : e; cat = System.Text.RegularExpressions.Regex.Replace(cat, "[0-9.]+", "#"); cats.Add(cat); }
                        foreach (var c in cats) tally[c] = tally.TryGetValue(c, out int v) ? v + 1 : 1;
                    }
                    Console.WriteLine($"1 lần thử: {ok}/{count} hợp lệ. Lỗi theo loại (số thế giới bị ảnh hưởng):");
                    foreach (var kv in System.Linq.Enumerable.OrderByDescending(tally, x => x.Value)) Console.WriteLine($"  {kv.Value,3}  {kv.Key}");
                    return 0;
                }
            for (int i = 0; i < args.Length; i++)
                if (args[i] == "--econ")
                {
                    int years = int.Parse(args[i + 1]);
                    var world = WorldGenerator.Generate(new WorldConfig { Seed = seed, CellCount = 20000 });
                    var eco = new Vigo.Core.Economy.EconomySim(world);
                    var swE = Stopwatch.StartNew();
                    Console.WriteLine("năm  dân(tr)  thiếu_ăn(nước)  ăn%   nhiên_liệu%  hàng%  GDP/người  giá_TB  giao_thương  audit");
                    for (int wk = 1; wk <= years * 52; wk++)
                    {
                        eco.StepWeek();
                        if (wk % 52 == 0)
                        {
                            double pop = 0, va = 0, fs = 0, fu = 0, gs = 0, pi = 0; int hungry = 0, n = 0;
                            for (int c = 0; c < world.Countries.Count; c++) { if (eco.S.Dead[c]) continue; var d = eco.S.Indicators[c]; n++; pop += d.Population; va += d.ValueAdded; fs += d.FoodSat * d.Population; fu += d.FuelSat * d.Population; gs += d.GoodsSat * d.Population; pi += d.PriceIndex; if (d.FoodSat < 0.9) hungry++; }
                            double tv = 0; foreach (var v in eco.S.Ledger.TradeVolume) tv += v;
                            Console.WriteLine($"{wk / 52,3} {pop / 1e6,8:0.00} {hungry,10}/{n}   {fs / pop,5:P0}   {fu / pop,6:P0}   {gs / pop,5:P0}   {va / pop,7:0.00}  {pi / n,6:0.00}  {tv / 52,10:0}   {eco.AuditGoods():E1}");
                        }
                    }
                    var Lg = eco.S.Ledger; Console.WriteLine("hàng      tồn_kho   sản_xuất/năm  hộ/năm  chế_biến/năm  mỏ_còn%  sai_lệch");
                    for (int g = 0; g < Vigo.Core.Economy.EconData.GoodCount; g++) { double st = eco.TotalStock((Vigo.Core.Economy.Good)g); double ex = Lg.Initial[g] + Lg.Produced[g] - Lg.Household[g] - Lg.Industry[g] - Lg.Invested[g] - Lg.Lost[g]; Console.WriteLine($"{(Vigo.Core.Economy.Good)g,-10}{st,10:0}{Lg.Produced[g] / years,12:0}{Lg.Household[g] / years,10:0}{Lg.Industry[g] / years,12:0}   {(st - ex),10:E1}"); }
                    { var capSum = new double[Vigo.Core.Economy.EconData.GoodCount]; for (int d = 0; d < world.Deposits.Count; d++) capSum[(int)Vigo.Core.Economy.EconData.ToGood(world.Deposits[d].Type)] += eco.S.DepRate[d] * 52;
                      double us = 0, un = 0; for (int p = 0; p < world.Provinces.Count; p++) { us += eco.S.Util[p] * eco.S.Pop[p]; un += eco.S.Pop[p]; }
                      Console.Write("công suất mỏ/năm: "); for (int g = 2; g <= 9; g++) Console.Write($"{(Vigo.Core.Economy.Good)g}={capSum[g]:0} "); Console.WriteLine($"| util dân quyền TB={us / un:P0}"); }
                    Console.WriteLine("nước thiếu ăn: tên, dân(tr), ăn%, kho bạc/người, giá ăn, FarmCap/dân, tỉnh, đất(ô)");
                    foreach (var c in world.Countries) { int ci = c.Id.Value; var dd = eco.S.Indicators[ci]; if (eco.S.Dead[ci] || dd.FoodSat >= 0.95) continue; double fc = 0; foreach (var pid in c.Provinces) fc += eco.S.FarmCap[pid.Value];
                      Console.WriteLine($"  {c.Name,-12}{dd.Population / 1e6,6:0.00} {dd.FoodSat,6:P0} {eco.S.Treasury[ci] / dd.Population,8:0.0} {eco.S.Price[ci][0],6:0.00} {fc / dd.Population,7:0.00} {c.Provinces.Count} {c.LandCells}"); }
                    Console.WriteLine("quốc gia thiếu hàng nhất: tên, dân(tr), hàng%, kho bạc/người, công suất Goods/người, kho đầu vào Goods (Steel IndMat Wood Prec Cu)");
                    foreach (var c in System.Linq.Enumerable.Take(System.Linq.Enumerable.OrderBy(world.Countries, x => eco.S.Indicators[x.Id.Value].GoodsSat), 6))
                    { int ci = c.Id.Value; double gc = 0; foreach (var pid in c.Provinces) gc += eco.S.IndCap[pid.Value][3]; var st = eco.S.Stock[ci]; var dd = eco.S.Indicators[ci];
                      Console.WriteLine($"  {c.Name,-12}{dd.Population / 1e6,6:0.00} {dd.GoodsSat,6:P0} {eco.S.Treasury[ci] / dd.Population,8:0.0} {gc / dd.Population,8:0.000}  {st[10]:0} {st[9]:0} {st[1]:0} {st[8]:0} {st[6]:0}"); }
                    Console.WriteLine($"{years} năm trong {swE.ElapsedMilliseconds} ms");
                    return 0;
                }
            for (int i = 0; i < args.Length; i++)
                if (args[i] == "--city")
                {
                    var wc = new WorldConfig { Seed = seed, CellCount = 20000 };
                    var world = WorldGenerator.Generate(wc);
                    Directory.CreateDirectory(outDir);
                    string which = args[i + 1];
                    var pick = which == "port" ? world.Cities.Find(c => c.IsPort && c.Population > 20000) ?? world.Cities.Find(c => c.IsPort) : world.GetCity(world.Countries[0].Capital);
                    var terr = new Vigo.Core.CityBuilder.WorldTerrain(world, pick.Id, 1000f);
                    var sim = new Vigo.Core.CityBuilder.CitySim(terr, seed);
                    Vigo.Core.CityBuilder.DemoScenario.BuildTown(sim);
                    Console.WriteLine($"Thành phố {pick.Name} (cảng={pick.IsPort}), {sim.Edges.FindAll(e => e.Alive).Count} đoạn đường, {sim.Parcels.FindAll(q => q.Alive).Count} lô đất");
                    Console.WriteLine("tuần  dân    nhà ở  việc  thấtnghiệp  QoL   QoL20%  yếu tố: việc làm/y tế/giáo dục/an ninh/đi lại/môi trường/giải trí");
                    for (int wk = 1; wk <= 208; wk++)
                    {
                        sim.StepWeek();
                        if (wk % 26 == 0) { var d = sim.Indicators; Console.WriteLine($"{wk,4} {d.Population,6} {d.Housing,6} {d.Jobs,5}   {d.UnemploymentRate,6:P0}   {d.QoL,5:0.00}  {d.QoLLowestQuintile,5:0.00}    {d.Employment:0.00}/{d.Health:0.00}/{d.Education:0.00}/{d.Safety:0.00}/{d.Commute:0.00}/{d.Environment:0.00}/{d.Leisure:0.00}"); }
                    }
                    CityRenderer.Render(sim, Path.Combine(outDir, "city_" + which + ".png"));
                    return 0;
                }
            Directory.CreateDirectory(outDir);
            var cfg = new WorldConfig { Seed = seed, CellCount = cells, CountryCount = countries };
            var sw = Stopwatch.StartNew();
            var w = WorldGenerator.Generate(cfg);
            sw.Stop();
            Console.WriteLine($"seed={seed} cells={w.CellCount} attempt={w.Attempt} gen={sw.ElapsedMilliseconds}ms valid={w.Validation.IsValid} hash={WorldHash.Compute(w):X16}");
            Console.WriteLine($"countries={w.Countries.Count} provinces={w.Provinces.Count} cities={w.Cities.Count} deposits={w.Deposits.Count} roads={w.Roads.Count}");
            foreach (var e in w.Validation.Errors.GetRange(0, Math.Min(15, w.Validation.Errors.Count))) Console.WriteLine("  LỖI: " + e);
            foreach (var n in w.GenerationNotes) Console.WriteLine("  ghi chú: " + n);

            var sw2 = Stopwatch.StartNew();
            var r = new MapRenderer(w, width);
            r.Render(Path.Combine(outDir, "political.png"), "political");
            r.Render(Path.Combine(outDir, "terrain.png"), "terrain");
            r.Render(Path.Combine(outDir, "resources.png"), "resources");
            Console.WriteLine($"render={sw2.ElapsedMilliseconds}ms -> {outDir}");
            return w.Validation.IsValid ? 0 : 1;
        }
    }
}
