using System;
using System.Collections.Generic;

namespace Vigo.Core
{
    /// <summary>Bước 7: mỏ khoáng sản/năng lượng, phân bố KHÔNG đều (cụm theo noise + địa chất).</summary>
    internal static class ResourceGen
    {
        private struct Spec { public ResourceType Type; public int BaseCount; public float Exponent; }

        public static void Generate(WorldState w, int attempt)
        {
            var g = w.Grid; int n = g.Count; var cfg = w.Config;
            var rng = Rng.Stream(cfg.Seed, "resources", attempt);
            float countScale = cfg.ResourceRichness * n / 40000f;

            var specs = new[]
            {
                new Spec { Type = ResourceType.Coal, BaseCount = 38, Exponent = 3f },
                new Spec { Type = ResourceType.Iron, BaseCount = 40, Exponent = 3f },
                new Spec { Type = ResourceType.Copper, BaseCount = 28, Exponent = 3f },
                new Spec { Type = ResourceType.Aluminium, BaseCount = 22, Exponent = 3f },
                new Spec { Type = ResourceType.PreciousMetals, BaseCount = 16, Exponent = 4f },
                new Spec { Type = ResourceType.IndustrialMaterials, BaseCount = 60, Exponent = 2f },
                new Spec { Type = ResourceType.Oil, BaseCount = 24, Exponent = 3f },
                new Spec { Type = ResourceType.Gas, BaseCount = 22, Exponent = 3f },
            };

            // Khoảng cách tới đất liền (bước nhảy) cho thềm lục địa.
            var landHops = new int[n]; var q = new Queue<int>();
            for (int i = 0; i < n; i++) { landHops[i] = w.IsLand[i] ? 0 : -1; if (w.IsLand[i]) q.Enqueue(i); }
            while (q.Count > 0)
            {
                int c = q.Dequeue();
                foreach (int nb in g.Neighbors(c)) if (landHops[nb] < 0) { landHops[nb] = landHops[c] + 1; q.Enqueue(nb); }
            }

            foreach (var spec in specs)
            {
                var noise = new Noise2D(rng.NextU64());
                var score = new float[n];
                for (int i = 0; i < n; i++)
                {
                    float nx = g.X[i] / g.Width, ny = g.Y[i] / g.Height;
                    float nz = 0.5f + 0.5f * noise.Fbm(nx * 7f, ny * 7f, 3);
                    float e = w.Elevation[i] / Math.Max(1e-3f, w.MountainElev); // ~1 tại chân núi cao
                    bool land = w.IsLand[i] && !w.IsLake[i];
                    float s = 0f;
                    switch (spec.Type)
                    {
                        case ResourceType.Coal: if (land) s = nz * (0.3f + Mathx.Smooth(0.08f, 0.4f, e) * (1f - Mathx.Smooth(0.9f, 1.4f, e))); break;
                        case ResourceType.Iron: if (land) s = nz * Mathx.Smooth(0.2f, 0.7f, e); break;
                        case ResourceType.Copper: if (land) s = nz * Mathx.Smooth(0.4f, 0.9f, e); break;
                        case ResourceType.Aluminium: if (land) s = nz * Mathx.Smooth(0.45f, 0.7f, w.Temperature[i]) * Mathx.Smooth(0.35f, 0.6f, w.Moisture[i]); break;
                        case ResourceType.PreciousMetals: if (land) s = nz * Mathx.Smooth(0.6f, 1.1f, e); break;
                        case ResourceType.IndustrialMaterials: if (land) s = nz; break;
                        case ResourceType.Oil:
                        case ResourceType.Gas:
                            if (land && e < 0.40f) s = nz * (1f - Mathx.Smooth(0.05f, 0.40f, e));
                            else if (!w.IsLand[i] && w.IsOcean[i] && landHops[i] > 0 && landHops[i] <= 5) s = nz * 0.7f * (1f - landHops[i] / 6f);
                            break;
                    }
                    score[i] = (float)Math.Pow(s, spec.Exponent);
                }
                Place(w, rng, spec.Type, (int)Math.Round(spec.BaseCount * countScale), score);
            }
        }

        private static void Place(WorldState w, Rng rng, ResourceType type, int count, float[] score)
        {
            int n = w.CellCount; var g = w.Grid;
            var cum = new double[n]; double sum = 0;
            for (int i = 0; i < n; i++) { sum += score[i]; cum[i] = sum; }
            if (sum <= 0) return;
            float minDist = g.Spacing * 5f;
            var placed = new List<int>();
            int tries = 0;
            while (placed.Count < count && tries < count * 60)
            {
                tries++;
                double r = rng.NextDouble() * sum;
                int lo = 0, hi = n - 1;
                while (lo < hi) { int mid = (lo + hi) >> 1; if (cum[mid] < r) lo = mid + 1; else hi = mid; }
                int cell = lo;
                bool ok = true;
                foreach (int p in placed) if (w.Dist(cell, p) < minDist) { ok = false; break; }
                if (!ok) continue;
                placed.Add(cell);
                float richness = (float)Math.Exp(rng.Gaussian() * 0.55);
                richness = Math.Max(0.25f, Math.Min(3.5f, richness));
                w.Deposits.Add(new ResourceDeposit
                {
                    Type = type, Cell = cell, Richness = richness, Reserve = richness * 100f,
                    Offshore = !w.IsLand[cell]
                });
            }
        }
    }
}
