using System;
using System.Collections.Generic;

namespace Vigo.Core
{
    /// <summary>Bước 8–12: quốc gia, tỉnh, thành phố, thủ đô.</summary>
    internal static class PoliticsGen
    {
        /// <summary>Lương thực nền tối thiểu để một quốc gia khởi động được (Validator dùng cùng giá trị).</summary>
        public const float MinFood = 30f;

        private static float StepCost(WorldState w, int a, int b, float riverPen, float mountPen)
        {
            float c = 1f + 3f * w.Slope[b];
            if (w.IsRiver[b]) c += riverPen;
            if (w.IsLake[b]) c += 6f;
            var bio = (Biome)w.Biomes[b];
            if (bio == Biome.Mountain) c += mountPen;
            else if (bio == Biome.Highland) c += mountPen * 0.4f;
            return w.Dist(a, b) * c;
        }

        // ------------------------------------------------------------------ Quốc gia
        public static bool GenerateCountries(WorldState w, int attempt)
        {
            var g = w.Grid; int n = g.Count; var cfg = w.Config;
            var rng = Rng.Stream(cfg.Seed, "countries", attempt);
            w.CountryOfCell = new int[n]; w.ProvinceOfCell = new int[n]; w.CityOfCell = new int[n];
            for (int i = 0; i < n; i++) { w.CountryOfCell[i] = -1; w.ProvinceOfCell[i] = -1; w.CityOfCell[i] = -1; }

            var cand = new List<int>();
            for (int i = 0; i < n; i++)
                if (w.IsLand[i] && !w.IsLake[i] && w.Fertility[i] > 0.15f) cand.Add(i);
            if (cand.Count < cfg.CountryCount * 20) { w.GenerationNotes.Add("Quá ít ô khả cư cho " + cfg.CountryCount + " quốc gia."); return false; }

            // Hạt giống: best-candidate theo (khoảng cách tới hạt đã chọn) × (khả cư).
            var seeds = new List<int>();
            double tot = 0; foreach (int c in cand) tot += w.Fertility[c];
            double pick = rng.NextDouble() * tot; double acc = 0; int first = cand[0];
            foreach (int c in cand) { acc += w.Fertility[c]; if (acc >= pick) { first = c; break; } }
            seeds.Add(first);
            while (seeds.Count < cfg.CountryCount)
            {
                int best = -1; float bestScore = -1f;
                for (int t = 0; t < 80; t++)
                {
                    int c = cand[rng.Range(0, cand.Count)];
                    float dmin = float.MaxValue;
                    foreach (int s in seeds) dmin = Math.Min(dmin, w.Dist(c, s));
                    float score = dmin * (0.4f + w.Fertility[c]);
                    if (score > bestScore) { bestScore = score; best = c; }
                }
                seeds.Add(best);
            }

            var names = new NameGen(Rng.Stream(cfg.Seed, "names-countries", attempt));
            var vigor = new float[seeds.Count];
            for (int k = 0; k < seeds.Count; k++)
            {
                vigor[k] = Math.Max(0.75f, Math.Min(1.7f, (float)Math.Exp(rng.Gaussian() * 0.28)));
                int culture = rng.Range(0, NameGen.CultureCount);
                w.Countries.Add(new Country { Id = new CountryId(k), SeedCell = seeds[k], Culture = culture });
            }

            // Mở rộng lãnh thổ: Dijkstra đa nguồn, núi/sông/hồ đắt → biên giới bám địa hình.
            // Quốc gia quá nhỏ (kẹp giữa các nước khác) được tăng "sức mở rộng" và chạy lại — tối đa 12 vòng.
            var dist = new float[n];
            var heap = new MinHeap();
            int minCountryCells = cfg.MinProvincesPerCountry * cfg.MinCellsPerProvince * 2;
            var sizes = new int[seeds.Count];
            for (int iter = 0; iter < 12; iter++)
            {
                for (int i = 0; i < n; i++) { dist[i] = float.MaxValue; w.CountryOfCell[i] = -1; }
                heap.Clear();
                for (int k = 0; k < seeds.Count; k++) { dist[seeds[k]] = 0; w.CountryOfCell[seeds[k]] = k; heap.Push(seeds[k], 0); }
                while (heap.Count > 0)
                {
                    int c = heap.Pop(out float d);
                    if (d > dist[c]) continue;
                    int owner = w.CountryOfCell[c];
                    foreach (int nb in g.Neighbors(c))
                    {
                        if (!w.IsLand[nb] || w.IsLake[nb]) continue; // hồ không thuộc lãnh thổ (Giai đoạn 1)
                        float nd = d + StepCost(w, c, nb, 1.5f, 3f) / vigor[owner];
                        if (nd < dist[nb]) { dist[nb] = nd; w.CountryOfCell[nb] = owner; heap.Push(nb, nd); }
                    }
                }
                Array.Clear(sizes, 0, sizes.Length);
                var food = new float[seeds.Count];
                for (int i = 0; i < n; i++)
                {
                    int o = w.CountryOfCell[i]; if (o < 0) continue;
                    if (!w.IsLake[i]) sizes[o]++;
                    food[o] += w.Fertility[i];
                }
                bool allOk = true;
                for (int k = 0; k < sizes.Length; k++)
                {
                    float need = Math.Max((float)minCountryCells / Math.Max(1, sizes[k]), MinFood / Math.Max(1f, food[k]));
                    if (need > 1f) { vigor[k] *= 1.0f + 0.35f * Math.Min(2f, need - 0.5f); allOk = false; }
                }
                if (allOk) break;
                if (iter == 11) w.GenerationNotes.Add("Cân bằng kích thước quốc gia chưa hội tụ sau 12 vòng.");
            }

            int unowned = 0;
            for (int i = 0; i < n; i++) if (w.IsLand[i] && !w.IsLake[i] && w.CountryOfCell[i] < 0) unowned++;
            if (unowned > 0) w.GenerationNotes.Add(unowned + " ô đất ở đảo/vùng bị hồ chia cắt chưa có chủ.");

            // Thống kê quốc gia.
            foreach (var c in w.Countries) { c.LandCells = 0; c.CentroidX = 0; c.CentroidY = 0; }
            for (int i = 0; i < n; i++)
            {
                int o = w.CountryOfCell[i]; if (o < 0) continue;
                var c = w.Countries[o]; c.LandCells++; c.CentroidX += g.X[i]; c.CentroidY += g.Y[i];
                foreach (int nb in g.Neighbors(i))
                {
                    int o2 = w.CountryOfCell[nb];
                    if (o2 >= 0 && o2 != o && !c.Neighbors.Contains(new CountryId(o2))) c.Neighbors.Add(new CountryId(o2));
                    if (w.IsOcean[nb]) c.HasPort = true;
                }
            }
            foreach (var c in w.Countries) if (c.LandCells > 0) { c.CentroidX /= c.LandCells; c.CentroidY /= c.LandCells; }

            AssignPersonalities(w, rng);
            foreach (var c in w.Countries)
            {
                c.Name = names.Country(c.Culture);
                c.Flag = names.Flag(c.Id.Value);
            }
            return true;
        }

        private static void AssignPersonalities(WorldState w, Rng rng)
        {
            var g = w.Grid; int nc = w.Countries.Count;
            var coast = new float[nc]; var mount = new float[nc]; var mineral = new float[nc];
            for (int i = 0; i < w.CellCount; i++)
            {
                int o = w.CountryOfCell[i]; if (o < 0) continue;
                foreach (int nb in g.Neighbors(i)) if (w.IsOcean[nb]) { coast[o]++; break; }
                var b = (Biome)w.Biomes[i]; if (b == Biome.Mountain || b == Biome.Highland) mount[o]++;
            }
            foreach (var d in w.Deposits)
            {
                if (d.Offshore) continue;
                int o = w.CountryOfCell[d.Cell]; if (o < 0) continue;
                if (d.Type == ResourceType.Iron || d.Type == ResourceType.Coal || d.Type == ResourceType.Copper) mineral[o] += d.Richness;
            }
            var wts = new double[7];
            for (int k = 0; k < nc; k++)
            {
                var c = w.Countries[k]; float cells = Math.Max(1, c.LandCells);
                wts[(int)Personality.Trade] = 0.6 + 4.0 * coast[k] / cells;
                wts[(int)Personality.Industrial] = 0.6 + 0.35 * mineral[k];
                wts[(int)Personality.Military] = 0.7 + 2.0 * mount[k] / cells + 0.15 * c.Neighbors.Count;
                wts[(int)Personality.Technology] = 0.6;
                wts[(int)Personality.Neutral] = 0.5 + (c.Neighbors.Count <= 1 ? 0.8 : 0.0);
                wts[(int)Personality.Expansionist] = 0.6 + 0.15 * c.Neighbors.Count;
                wts[(int)Personality.Balanced] = 1.0;
                double sum = 0; foreach (double x in wts) sum += x;
                double r = rng.NextDouble() * sum, a = 0; int pick = 0;
                for (int p = 0; p < wts.Length; p++) { a += wts[p]; if (r <= a) { pick = p; break; } }
                c.Personality = (Personality)pick;
            }
        }

        // ------------------------------------------------------------------ Tỉnh
        public static void GenerateProvinces(WorldState w, int attempt)
        {
            var g = w.Grid; int n = g.Count; var cfg = w.Config;
            var rng = Rng.Stream(cfg.Seed, "provinces", attempt);
            var names = new NameGen(Rng.Stream(cfg.Seed, "names-provinces", attempt));
            int maxCells = 1; foreach (var c in w.Countries) maxCells = Math.Max(maxCells, c.LandCells);

            var cellsOf = new List<int>[w.Countries.Count];
            for (int k = 0; k < cellsOf.Length; k++) cellsOf[k] = new List<int>();
            for (int i = 0; i < n; i++) if (w.CountryOfCell[i] >= 0) cellsOf[w.CountryOfCell[i]].Add(i);

            var local = new int[n]; var dist = new float[n];
            var heap = new MinHeap();

            foreach (var country in w.Countries)
            {
                var cells = cellsOf[country.Id.Value];
                if (cells.Count == 0) continue;
                float frac = (float)cells.Count / maxCells;
                int k = (int)Math.Round(cfg.MinProvincesPerCountry + (cfg.MaxProvincesPerCountry - cfg.MinProvincesPerCountry) * Math.Pow(frac, 0.6)) + rng.Range(-1, 2);
                k = Math.Max(cfg.MinProvincesPerCountry, Math.Min(cfg.MaxProvincesPerCountry, k));
                k = Math.Min(k, Math.Max(1, cells.Count / cfg.MinCellsPerProvince));

                int[] seeds = null; int[] sizes = null;
                bool done = false;
                for (; k >= 1 && !done; k--)
                {
                    // Với mỗi k, thử tối đa 10 bộ hạt giống khác nhau trước khi giảm k.
                    for (int attemptSeeds = 0; attemptSeeds < 10 && !done; attemptSeeds++)
                    {
                        seeds = PickProvinceSeeds(w, rng, cells, k);
                        for (int relax = 0; relax < 3; relax++)
                        {
                            Grow(w, cells, seeds, local, dist, heap);
                            if (relax < 2) seeds = Recenter(w, cells, seeds, local);
                        }
                        sizes = new int[k];
                        foreach (int c in cells) if (local[c] >= 0 && !w.IsLake[c]) sizes[local[c]]++;
                        int min = int.MaxValue; foreach (int s in sizes) min = Math.Min(min, s);
                        if (min >= cfg.MinCellsPerProvince || k == 1) done = true;
                    }
                }
                k++; // vòng for đã trừ thêm 1 sau khi đạt

                var provs = new Province[k];
                for (int p = 0; p < k; p++)
                {
                    provs[p] = new Province
                    {
                        Id = new ProvinceId(w.Provinces.Count), Owner = country.Id, OriginalOwner = country.Id,
                        SeedCell = seeds[p], Name = names.Place(country.Culture)
                    };
                    w.Provinces.Add(provs[p]); country.Provinces.Add(provs[p].Id);
                }
                var lists = new List<int>[k]; for (int p = 0; p < k; p++) lists[p] = new List<int>();
                foreach (int c in cells) if (local[c] >= 0) { lists[local[c]].Add(c); w.ProvinceOfCell[c] = provs[local[c]].Id.Value; }
                for (int p = 0; p < k; p++)
                {
                    provs[p].Cells = lists[p].ToArray();
                    float sx = 0, sy = 0, food = 0;
                    foreach (int c in provs[p].Cells) { sx += g.X[c]; sy += g.Y[c]; food += w.Fertility[c]; }
                    provs[p].CentroidX = sx / provs[p].Cells.Length; provs[p].CentroidY = sy / provs[p].Cells.Length;
                    provs[p].FoodPotential = food;
                }
            }
        }

        private static int[] PickProvinceSeeds(WorldState w, Rng rng, List<int> cells, int k)
        {
            var dry = new List<int>();
            foreach (int c in cells) if (!w.IsLake[c] && w.Slope[c] < 0.8f) dry.Add(c);
            if (dry.Count < k * 3) { dry.Clear(); foreach (int c in cells) if (!w.IsLake[c]) dry.Add(c); }
            if (dry.Count < k) dry = cells;
            var seeds = new List<int> { dry[rng.Range(0, dry.Count)] };
            while (seeds.Count < k)
            {
                int best = -1; float bestD = -1f;
                for (int t = 0; t < 60; t++)
                {
                    int c = dry[rng.Range(0, dry.Count)];
                    float dmin = float.MaxValue;
                    foreach (int s in seeds) dmin = Math.Min(dmin, w.Dist(c, s));
                    if (dmin > bestD) { bestD = dmin; best = c; }
                }
                seeds.Add(best);
            }
            return seeds.ToArray();
        }

        private static void Grow(WorldState w, List<int> cells, int[] seeds, int[] local, float[] dist, MinHeap heap)
        {
            var g = w.Grid; int country = w.CountryOfCell[cells[0]];
            foreach (int c in cells) { local[c] = -1; dist[c] = float.MaxValue; }
            heap.Clear();
            for (int p = 0; p < seeds.Length; p++) { local[seeds[p]] = p; dist[seeds[p]] = 0; heap.Push(seeds[p], 0); }
            while (heap.Count > 0)
            {
                int c = heap.Pop(out float d);
                if (d > dist[c]) continue;
                foreach (int nb in g.Neighbors(c))
                {
                    if (w.CountryOfCell[nb] != country) continue;
                    float nd = d + StepCost(w, c, nb, 0.6f, 1.0f);
                    if (nd < dist[nb]) { dist[nb] = nd; local[nb] = local[c]; heap.Push(nb, nd); }
                }
            }
        }

        private static int[] Recenter(WorldState w, List<int> cells, int[] seeds, int[] local)
        {
            var g = w.Grid; int k = seeds.Length;
            var sx = new float[k]; var sy = new float[k]; var cnt = new int[k];
            foreach (int c in cells) if (local[c] >= 0) { sx[local[c]] += g.X[c]; sy[local[c]] += g.Y[c]; cnt[local[c]]++; }
            var res = new int[k];
            for (int p = 0; p < k; p++)
            {
                if (cnt[p] == 0) { res[p] = seeds[p]; continue; }
                float cx = sx[p] / cnt[p], cy = sy[p] / cnt[p]; float bd = float.MaxValue; int best = seeds[p];
                foreach (int c in cells)
                {
                    if (local[c] != p) continue;
                    float dx = g.X[c] - cx, dy = g.Y[c] - cy, d2 = dx * dx + dy * dy;
                    if (d2 < bd) { bd = d2; best = c; }
                }
                res[p] = best;
            }
            return res;
        }

        // ------------------------------------------------------------------ Thành phố
        public static void GenerateCities(WorldState w, int attempt)
        {
            var g = w.Grid; var cfg = w.Config;
            var rng = Rng.Stream(cfg.Seed, "cities", attempt);
            var names = new NameGen(Rng.Stream(cfg.Seed, "names-cities", attempt));

            float maxFood = 1f; foreach (var p in w.Provinces) maxFood = Math.Max(maxFood, p.FoodPotential);
            float cellArea = g.Spacing * g.Spacing;

            foreach (var prov in w.Provinces)
            {
                var country = w.GetCountry(prov.Owner);
                float r = prov.FoodPotential / maxFood;
                int want = (int)Math.Round(cfg.MinCitiesPerProvince + (cfg.MaxCitiesPerProvince - cfg.MinCitiesPerProvince) * Math.Pow(r, 0.7)) + rng.Range(-1, 2);
                want = Math.Max(cfg.MinCitiesPerProvince, Math.Min(cfg.MaxCitiesPerProvince, want));
                want = Math.Min(want, Math.Max(1, prov.Cells.Length / 3));

                // Điểm phù hợp từng ô.
                var scored = new List<KeyValuePair<float, int>>();
                foreach (int c in prov.Cells)
                {
                    var b = (Biome)w.Biomes[c];
                    if (w.IsLake[c] || b == Biome.Mountain || b == Biome.Ice || w.Slope[c] > 0.8f) continue;
                    float s = w.Fertility[c] - 0.6f * w.Slope[c] + 0.1f * rng.NextFloat();
                    bool coast = false, riv = w.IsRiver[c], lake = false;
                    foreach (int nb in g.Neighbors(c))
                    {
                        if (w.IsOcean[nb]) coast = true;
                        if (w.IsRiver[nb]) riv = true;
                        if (w.IsLake[nb]) lake = true;
                    }
                    if (coast) s += 0.20f; if (riv) s += 0.30f; if (lake) s += 0.15f;
                    scored.Add(new KeyValuePair<float, int>(s, c));
                }
                if (scored.Count == 0)
                    foreach (int c in prov.Cells) if (!w.IsLake[c]) scored.Add(new KeyValuePair<float, int>(-w.Slope[c], c));
                scored.Sort((a, b) => b.Key.CompareTo(a.Key));

                float areaUnits = prov.Cells.Length * cellArea;
                float dmin = 0.6f * (float)Math.Sqrt(areaUnits / Math.Max(1, want));
                var chosen = new List<int>();
                for (int relax = 0; relax < 4 && chosen.Count < want; relax++)
                {
                    foreach (var kv in scored)
                    {
                        if (chosen.Count >= want) break;
                        int c = kv.Value; if (chosen.Contains(c)) continue;
                        bool ok = true; foreach (int o in chosen) if (w.Dist(c, o) < dmin) { ok = false; break; }
                        if (ok) chosen.Add(c);
                    }
                    dmin *= 0.65f;
                }

                if (chosen.Count < Math.Min(want, cfg.MinCitiesPerProvince))
                {
                    // Dự phòng: tỉnh nhỏ/dốc — chọn ô ít dốc nhất còn lại, không ràng buộc khoảng cách.
                    var rest = new List<KeyValuePair<float, int>>();
                    foreach (int c in prov.Cells) if (!w.IsLake[c] && !chosen.Contains(c)) rest.Add(new KeyValuePair<float, int>(w.Slope[c], c));
                    rest.Sort((a, b) => a.Key.CompareTo(b.Key));
                    foreach (var kv in rest) { if (chosen.Count >= cfg.MinCitiesPerProvince) break; chosen.Add(kv.Value); }
                }

                for (int i = 0; i < chosen.Count; i++)
                {
                    int c = chosen[i];
                    bool port = false; foreach (int nb in g.Neighbors(c)) if (w.IsOcean[nb]) { port = true; break; }
                    float pot = 0f; int cnt = 0;
                    foreach (int cc in prov.Cells) if (w.Dist(c, cc) < g.Spacing * 3.2f) { pot += w.Fertility[cc]; cnt++; }
                    double pop = pot * 650.0 * Math.Exp(rng.Gaussian() * 0.5) * (i == 0 ? 2.2 : 1.0) * (port ? 1.3 : 1.0);
                    var city = new City
                    {
                        Id = new CityId(w.Cities.Count), Name = names.Place(country.Culture), Province = prov.Id,
                        Cell = c, X = g.X[c], Y = g.Y[c], Population = Math.Max(2000L, (long)pop),
                        IsPort = port, IsProvincialSeat = i == 0
                    };
                    w.Cities.Add(city); prov.Cities.Add(city.Id); w.CityOfCell[c] = city.Id.Value;
                }

                // Dân số nông thôn + đô thị.
                double rural = 0; foreach (int c in prov.Cells) rural += w.Fertility[c] * 380.0 * (1.0 + 0.2 * rng.NextDouble());
                long urban = 0; foreach (var cid in prov.Cities) urban += w.GetCity(cid).Population;
                prov.Population = (long)rural + urban;
                country.Population += prov.Population;
            }
        }

        // ------------------------------------------------------------------ Thủ đô
        public static void AssignCapitals(WorldState w, int attempt)
        {
            var g = w.Grid; var rng = Rng.Stream(w.Config.Seed, "capitals", attempt);
            foreach (var country in w.Countries)
            {
                var cities = new List<City>();
                foreach (var pid in country.Provinces) foreach (var cid in w.GetProvince(pid).Cities) cities.Add(w.GetCity(cid));
                if (cities.Count == 0) continue;

                float maxD = 1f;
                foreach (var c in cities) maxD = Math.Max(maxD, Dist(c.X, c.Y, country.CentroidX, country.CentroidY));
                float radius = 0.25f * (float)Math.Sqrt(country.LandCells * g.Spacing * g.Spacing);
                float wCoast = country.Personality == Personality.Trade ? 0.5f : (country.Personality == Personality.Neutral ? 0.05f : 0.15f);
                float wCentral = country.Personality == Personality.Military ? 0.45f : 0.35f;
                float wHill = country.Personality == Personality.Military ? 0.2f : 0.0f;

                City best = null; float bestScore = float.MinValue;
                foreach (var c in cities)
                {
                    float central = 1f - Dist(c.X, c.Y, country.CentroidX, country.CentroidY) / maxD;
                    int hub = 0; foreach (var o in cities) if (o != c && Dist(c.X, c.Y, o.X, o.Y) < radius) hub++;
                    float hubN = Math.Min(1f, hub / 6f);
                    float score = wCentral * central + 0.25f * hubN + 0.20f * w.Fertility[c.Cell]
                                + (c.IsPort ? wCoast : 0f) + wHill * Mathx.Smooth(0.1f, 0.4f, w.Elevation[c.Cell])
                                + 0.08f * rng.NextFloat() + 0.05f * (float)Math.Log10(Math.Max(10, c.Population)) / 6f;
                    if (score > bestScore) { bestScore = score; best = c; }
                }
                best.IsCapital = true;
                country.Capital = best.Id; country.CapitalProvince = best.Province;
            }
        }

        private static float Dist(float x1, float y1, float x2, float y2)
        {
            float dx = x1 - x2, dy = y1 - y2; return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
