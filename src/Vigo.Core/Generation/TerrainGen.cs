using System;
using System.Collections.Generic;

namespace Vigo.Core
{
    internal static class Mathx
    {
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static float Smooth(float a, float b, float x)
        {
            float t = Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }
    }

    /// <summary>Bước 2–3: địa hình, mực nước biển theo tỉ lệ đất, loại đảo nhỏ, phân loại nước.</summary>
    internal static class TerrainGen
    {
        public static void Generate(WorldState w, int attempt)
        {
            var g = w.Grid; int n = g.Count; var cfg = w.Config;
            var rng = Rng.Stream(cfg.Seed, "terrain", attempt);
            var nWarp = new Noise2D(rng.NextU64()); var nCont = new Noise2D(rng.NextU64());
            var nHill = new Noise2D(rng.NextU64()); var nRidge = new Noise2D(rng.NextU64());

            var eBase = new float[n]; var mount = new float[n];
            for (int i = 0; i < n; i++)
            {
                float nx = g.X[i] / g.Width, ny = g.Y[i] / g.Height;
                float wx = nx + 0.12f * nWarp.Fbm(nx * 2.5f, ny * 2.5f, 3);
                float wy = ny + 0.12f * nWarp.Fbm(nx * 2.5f + 17.3f, ny * 2.5f + 9.1f, 3);
                float cwx = nx + 0.30f * nWarp.Fbm(nx * 1.8f + 40f, ny * 1.8f + 40f, 3);
                float cwy = ny + 0.30f * nWarp.Fbm(nx * 1.8f + 80f, ny * 1.8f + 80f, 3);
                float ex = (cwx - 0.5f) * 2f, ey = (cwy - 0.5f) * 2f;
                float rr = (float)Math.Sqrt(ex * ex * 0.85f + ey * ey * 1.15f);
                float falloff = 1f - Mathx.Smooth(0.20f, 1.05f, rr);
                float cont = 0.5f + 0.5f * nCont.Fbm(wx * 3.0f + 3.1f, wy * 3.0f + 7.7f, 5);
                float hills = 0.5f + 0.5f * nHill.Fbm(wx * 6f, wy * 6f, 4);
                eBase[i] = 0.36f * falloff + (0.42f * cont + 0.22f * hills) * (0.30f + 0.70f * falloff);
                float rd = nRidge.Ridged(wx * 4.5f + 5.5f, wy * 4.5f + 1.2f, 4);
                mount[i] = rd * rd;
            }

            // Mực nước biển = phân vị để đạt đúng LandRatio.
            var sorted = (float[])eBase.Clone();
            Array.Sort(sorted);
            float sea = sorted[(int)((1f - cfg.LandRatio) * (n - 1))];

            var elev = new float[n];
            for (int i = 0; i < n; i++)
            {
                float e = eBase[i] - sea;
                if (e > 0f)
                    e += cfg.MountainFactor * 0.55f * mount[i] * Mathx.Smooth(0.02f, 0.22f, e);
                elev[i] = e;
            }

            // Chuẩn hóa: đất theo phân vị 99,5% = 1; nước theo độ sâu lớn nhất = -1.
            float maxLand = 0f, minSea = 0f;
            var landVals = new List<float>();
            for (int i = 0; i < n; i++) { if (elev[i] > 0) landVals.Add(elev[i]); if (elev[i] < minSea) minSea = elev[i]; }
            landVals.Sort();
            maxLand = landVals.Count > 0 ? landVals[(int)(landVals.Count * 0.995f)] : 1f;
            if (maxLand < 1e-4f) maxLand = 1e-4f;
            for (int i = 0; i < n; i++)
                elev[i] = elev[i] > 0 ? elev[i] / maxLand : (minSea < 0 ? elev[i] / -minSea : elev[i]);
            w.Elevation = elev;
            var lv = new List<float>(); for (int i = 0; i < n; i++) if (elev[i] > 0) lv.Add(elev[i]);
            lv.Sort();
            w.HighlandElev = lv.Count > 0 ? lv[(int)(lv.Count * 0.80f)] : 0.5f;
            w.MountainElev = lv.Count > 0 ? lv[(int)(lv.Count * 0.94f)] : 0.8f;

            w.IsLand = new bool[n];
            for (int i = 0; i < n; i++) w.IsLand[i] = elev[i] > 0f;

            RemoveTinyIslands(w);
            ClassifyWater(w);
        }

        private static void RemoveTinyIslands(WorldState w)
        {
            int n = w.CellCount; var g = w.Grid;
            var comp = new int[n]; for (int i = 0; i < n; i++) comp[i] = -1;
            var sizes = new List<int>(); var stack = new Stack<int>();
            int landTotal = 0;
            for (int i = 0; i < n; i++)
            {
                if (!w.IsLand[i] || comp[i] >= 0) continue;
                int id = sizes.Count, size = 0;
                comp[i] = id; stack.Push(i);
                while (stack.Count > 0)
                {
                    int c = stack.Pop(); size++;
                    foreach (int nb in g.Neighbors(c))
                        if (w.IsLand[nb] && comp[nb] < 0) { comp[nb] = id; stack.Push(nb); }
                }
                sizes.Add(size); landTotal += size;
            }
            int minSize = Math.Max(15, (int)(landTotal * 0.012f));
            int removed = 0;
            for (int i = 0; i < n; i++)
                if (w.IsLand[i] && sizes[comp[i]] < minSize) { w.IsLand[i] = false; w.Elevation[i] = -0.02f; removed++; }
            if (removed > 0) w.GenerationNotes.Add("Loại " + removed + " ô đảo nhỏ.");
        }

        /// <summary>Nước nối với rìa bản đồ = Đại dương; nước kín = hồ/biển kín.</summary>
        private static void ClassifyWater(WorldState w)
        {
            int n = w.CellCount; var g = w.Grid;
            w.IsOcean = new bool[n]; w.IsLake = new bool[n];
            var stack = new Stack<int>();
            float m = g.Spacing * 1.5f;
            for (int i = 0; i < n; i++)
            {
                if (w.IsLand[i]) continue;
                if (g.X[i] < m || g.Y[i] < m || g.X[i] > g.Width - m || g.Y[i] > g.Height - m)
                { w.IsOcean[i] = true; stack.Push(i); }
            }
            while (stack.Count > 0)
            {
                int c = stack.Pop();
                foreach (int nb in g.Neighbors(c))
                    if (!w.IsLand[nb] && !w.IsOcean[nb]) { w.IsOcean[nb] = true; stack.Push(nb); }
            }
            for (int i = 0; i < n; i++) if (!w.IsLand[i] && !w.IsOcean[i]) w.IsLake[i] = true;
        }
    }

    /// <summary>Bước 4: nhiệt độ, độ ẩm (khoảng cách biển, gió, bóng mưa).</summary>
    internal static class ClimateGen
    {
        public static void Generate(WorldState w, int attempt)
        {
            var g = w.Grid; int n = g.Count; var cfg = w.Config;
            var rng = Rng.Stream(cfg.Seed, "climate", attempt);
            var nT = new Noise2D(rng.NextU64()); var nM = new Noise2D(rng.NextU64());
            w.Temperature = new float[n]; w.Moisture = new float[n];

            // Khoảng cách (bước nhảy) tới nước.
            var hops = new int[n]; var q = new Queue<int>();
            for (int i = 0; i < n; i++) { hops[i] = -1; if (!w.IsLand[i]) { hops[i] = 0; q.Enqueue(i); } }
            while (q.Count > 0)
            {
                int c = q.Dequeue();
                foreach (int nb in g.Neighbors(c))
                    if (hops[nb] < 0) { hops[nb] = hops[c] + 1; q.Enqueue(nb); }
            }

            float scale = g.Width / 2000f;
            float[] dists = { 15f * scale, 40f * scale, 80f * scale, 130f * scale };
            float wdx = 1f, wdy = 0.15f; float wl = (float)Math.Sqrt(wdx * wdx + wdy * wdy); wdx /= wl; wdy /= wl;

            for (int i = 0; i < n; i++)
            {
                float nx = g.X[i] / g.Width, ny = g.Y[i] / g.Height;
                float lat = Math.Abs(ny * 2f - 1f);
                float e = Math.Max(0f, w.Elevation[i]);
                float t = 1.05f - 1.05f * (float)Math.Pow(lat, 1.1) - 0.55f * e + 0.07f * nT.Fbm(nx * 5f, ny * 5f, 3);
                w.Temperature[i] = Mathx.Clamp01(t);

                float distSea = Math.Max(0, hops[i]) * g.Spacing * 1.1f;
                float m = (float)Math.Exp(-distSea / (0.11f * g.Width));
                m += 0.35f * nM.Fbm(nx * 4f + 11f, ny * 4f + 3f, 4);
                m += 0.10f * (1f - Mathx.Smooth(0.0f, 0.5f, lat));
                if (w.IsLand[i])
                {
                    float maxUp = float.MinValue, sumUp = 0;
                    foreach (float d in dists)
                    {
                        int j = g.Nearest(g.X[i] - wdx * d, g.Y[i] - wdy * d);
                        float ej = Math.Max(0f, w.Elevation[j]);
                        if (ej > maxUp) maxUp = ej; sumUp += ej;
                    }
                    if (maxUp - e > 0.25f) m *= 0.55f;                    // bóng mưa phía khuất gió
                    else if (e - sumUp / dists.Length > 0.10f) m *= 1.15f; // sườn đón gió
                }
                w.Moisture[i] = Mathx.Clamp01(m);
            }
        }
    }

    /// <summary>Bước 5: lấp trũng (priority-flood), hướng chảy, lưu lượng, sông, hồ.</summary>
    internal static class HydrologyGen
    {
        public static void Generate(WorldState w)
        {
            var g = w.Grid; int n = g.Count;
            w.FlowTo = new int[n]; w.Flux = new float[n]; w.FilledElevation = new float[n];
            w.IsRiver = new bool[n];
            for (int i = 0; i < n; i++) w.FlowTo[i] = -1;

            var visited = new bool[n]; var order = new List<int>(n); var heap = new MinHeap();
            for (int i = 0; i < n; i++)
                if (w.IsOcean[i])
                {
                    visited[i] = true; w.FilledElevation[i] = w.Elevation[i];
                    bool border = false;
                    foreach (int nb in g.Neighbors(i)) if (!w.IsOcean[nb]) { border = true; break; }
                    if (border) heap.Push(i, w.Elevation[i]);
                }
            while (heap.Count > 0)
            {
                int c = heap.Pop(out float _);
                order.Add(c);
                foreach (int nb in g.Neighbors(c))
                {
                    if (visited[nb]) continue;
                    visited[nb] = true;
                    float f = Math.Max(w.Elevation[nb], w.FilledElevation[c] + 1e-5f);
                    w.FilledElevation[nb] = f; w.FlowTo[nb] = c;
                    heap.Push(nb, f);
                }
            }

            // Lưu lượng: mưa mỗi ô đất, cộng dồn từ thượng nguồn xuống hạ nguồn.
            for (int i = 0; i < n; i++) w.Flux[i] = w.IsOcean[i] ? 0f : 0.2f + w.Moisture[i];
            for (int k = order.Count - 1; k >= 0; k--)
            {
                int c = order[k]; int to = w.FlowTo[c];
                if (to >= 0) w.Flux[to] += w.Flux[c];
            }

            // Hồ: trũng được lấp sâu, hoặc biển kín.
            for (int i = 0; i < n; i++)
            {
                if (w.IsOcean[i]) continue;
                if (w.FilledElevation[i] - w.Elevation[i] > 0.075f) w.IsLake[i] = true;
            }

            // Sông: các ô đất có lưu lượng vượt ngưỡng (phân vị ~96,5% trên đất).
            var vals = new List<float>();
            for (int i = 0; i < n; i++) if (w.IsLand[i] && !w.IsLake[i]) vals.Add(w.Flux[i]);
            vals.Sort();
            float thr = vals.Count > 0 ? vals[(int)(vals.Count * 0.945f)] : 3f;
            w.RiverThreshold = Math.Max(thr, 3f);
            for (int i = 0; i < n; i++)
                w.IsRiver[i] = w.IsLand[i] && !w.IsLake[i] && w.Flux[i] >= w.RiverThreshold;
        }
    }

    /// <summary>Bước 6: biome, độ dốc, độ phì nhiêu.</summary>
    internal static class BiomeGen
    {
        
        public static void Assign(WorldState w)
        {
            var g = w.Grid; int n = g.Count;
            w.Biomes = new byte[n]; w.Slope = new float[n]; w.Fertility = new float[n];
            for (int i = 0; i < n; i++)
            {
                float maxD = 0f;
                foreach (int nb in g.Neighbors(i))
                {
                    float d = w.Dist(i, nb);
                    if (d > 0) maxD = Math.Max(maxD, Math.Abs(w.Elevation[i] - w.Elevation[nb]) / d);
                }
                w.Slope[i] = Mathx.Clamp01(maxD * 60f);
            }
            for (int i = 0; i < n; i++)
            {
                Biome b = Classify(w, i);
                w.Biomes[i] = (byte)b;
                w.Fertility[i] = 0f;
            }
            for (int i = 0; i < n; i++)
            {
                var b = (Biome)w.Biomes[i];
                float f = BaseFertility(b);
                if (f > 0f)
                {
                    f *= 0.6f + 0.8f * w.Moisture[i];
                    bool nearWater = w.IsRiver[i];
                    if (!nearWater)
                        foreach (int nb in g.Neighbors(i)) if (w.IsRiver[nb] || w.IsLake[nb]) { nearWater = true; break; }
                    if (nearWater) f += 0.30f;
                    f *= 1f - 0.5f * w.Slope[i];
                }
                w.Fertility[i] = Mathx.Clamp01(f);
            }
        }

        private static float BaseFertility(Biome b)
        {
            switch (b)
            {
                case Biome.Grassland: return 0.85f; case Biome.Forest: return 0.60f; case Biome.Steppe: return 0.45f;
                case Biome.Savanna: return 0.50f; case Biome.TropicalForest: return 0.60f; case Biome.Wetland: return 0.45f;
                case Biome.Taiga: return 0.25f; case Biome.Tundra: return 0.08f; case Biome.Desert: return 0.06f;
                case Biome.Highland: return 0.25f; case Biome.Mountain: return 0.04f; default: return 0f;
            }
        }

        private static Biome Classify(WorldState w, int i)
        {
            if (w.IsOcean[i]) return Biome.Ocean;
            if (w.IsLake[i]) return Biome.Lake;
            float e = w.Elevation[i], t = w.Temperature[i], m = w.Moisture[i];
            if (e > w.MountainElev) return Biome.Mountain;
            if (t < 0.10f) return Biome.Ice;
            if (e > w.HighlandElev) return Biome.Highland;
            if (t < 0.22f) return Biome.Tundra;
            if (t < 0.40f) return m > 0.45f ? Biome.Taiga : Biome.Steppe;
            if (t < 0.68f)
            {
                if (m < 0.22f) return Biome.Desert;
                if (m < 0.42f) return Biome.Grassland;
                if (m < 0.78f) return Biome.Forest;
                return (e < 0.10f && w.Flux[i] > w.RiverThreshold * 0.5f) ? Biome.Wetland : Biome.Forest;
            }
            if (m < 0.20f) return Biome.Desert;
            if (m < 0.42f) return Biome.Savanna;
            return Biome.TropicalForest;
        }
    }
}
