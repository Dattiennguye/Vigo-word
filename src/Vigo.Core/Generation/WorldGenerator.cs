using System;

namespace Vigo.Core
{
    /// <summary>Pipeline sinh thế giới (TDD mục 6). Cùng seed + cấu hình → cùng thế giới.</summary>
    public static class WorldGenerator
    {
        public static WorldState Generate(WorldConfig cfg, Action<string, float> progress = null)
        {
            WorldState last = null;
            int attempts = Math.Max(1, cfg.MaxAttempts);
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                var w = GenerateOnce(cfg, attempt, progress);
                last = w;
                if (w.Validation.IsValid) return w;
            }
            return last;
        }

        private static WorldState GenerateOnce(WorldConfig cfg, int attempt, Action<string, float> progress)
        {
            var w = new WorldState { Config = cfg, Seed = cfg.Seed, Attempt = attempt };
            void P(string s, float f) { if (progress != null) progress(s, f); }

            P("Đồ thị ô", 0.02f);
            w.Grid = CellGrid.Generate(Rng.Stream(cfg.Seed, "cells", attempt), cfg.MapWidth, cfg.MapHeight, cfg.CellCount);
            P("Địa hình", 0.15f); TerrainGen.Generate(w, attempt);
            P("Khí hậu", 0.30f); ClimateGen.Generate(w, attempt);
            P("Thủy văn", 0.42f); HydrologyGen.Generate(w);
            P("Biome", 0.52f); BiomeGen.Assign(w);
            P("Tài nguyên", 0.60f); ResourceGen.Generate(w, attempt);
            P("Quốc gia", 0.68f);
            if (!PoliticsGen.GenerateCountries(w, attempt))
            {
                w.CountryOfCell = w.CountryOfCell ?? new int[w.CellCount];
                w.Validation = new ValidationReport(); w.Validation.Fail("Không sinh được quốc gia (xem GenerationNotes)");
                return w;
            }
            P("Tỉnh", 0.78f); PoliticsGen.GenerateProvinces(w, attempt);
            P("Thành phố", 0.86f); PoliticsGen.GenerateCities(w, attempt);
            P("Thủ đô", 0.90f); PoliticsGen.AssignCapitals(w, attempt);
            P("Đường", 0.94f); RoadGen.Generate(w, attempt);
            P("Kiểm tra", 0.99f);
            w.Validation = Validator.Validate(w);
            w.GenesisHash = WorldHash.Compute(w);
            P("Xong", 1f);
            return w;
        }
    }
}
