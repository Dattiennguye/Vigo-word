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
