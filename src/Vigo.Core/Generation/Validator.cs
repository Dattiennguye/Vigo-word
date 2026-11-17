using System;
using System.Collections.Generic;

namespace Vigo.Core
{
    /// <summary>Kiểm tra hợp lệ thế giới (TDD mục 6.2) + bất biến dữ liệu (mục 12.3).</summary>
    public static class Validator
    {
        public static ValidationReport Validate(WorldState w)
        {
            var r = new ValidationReport();
            var cfg = w.Config; var g = w.Grid; int n = w.CellCount;

            if (w.Countries.Count != cfg.CountryCount) r.Fail("Số quốc gia " + w.Countries.Count + " != " + cfg.CountryCount);
            if (w.CountryOfCell == null) { r.Fail("Chưa sinh chính trị."); return r; }

            // Land ratio
            int land = 0; for (int i = 0; i < n; i++) if (w.IsLand[i]) land++;
            float ratio = (float)land / n;
            if (Math.Abs(ratio - cfg.LandRatio) > 0.12f) r.Fail("Tỉ lệ đất " + ratio.ToString("0.00") + " lệch quá xa " + cfg.LandRatio.ToString("0.00"));

            // Số nguyên không âm hợp lệ & quan hệ cha-con
            foreach (var c in w.Countries)
            {
                if (c.Provinces.Count == 0) { r.Fail(c.Name + ": không có tỉnh"); continue; }
                if (c.Provinces.Count < cfg.MinProvincesPerCountry || c.Provinces.Count > cfg.MaxProvincesPerCountry)
                    r.Fail(c.Name + ": " + c.Provinces.Count + " tỉnh ngoài khoảng [" + cfg.MinProvincesPerCountry + "," + cfg.MaxProvincesPerCountry + "]");
                if (c.Capital.IsNone) { r.Fail(c.Name + ": không có thủ đô"); continue; }
                var cap = w.GetCity(c.Capital);
                if (!cap.IsCapital) r.Fail(c.Name + ": cờ IsCapital sai");
                if (w.CountryOfCell[cap.Cell] != c.Id.Value) r.Fail(c.Name + ": thủ đô nằm ngoài lãnh thổ");
                int capCount = 0;
                foreach (var pid in c.Provinces) foreach (var cid in w.GetProvince(pid).Cities) if (w.GetCity(cid).IsCapital) capCount++;
                if (capCount != 1) r.Fail(c.Name + ": có " + capCount + " thủ đô (cần đúng 1)");

                // Khởi động được: lương thực + nước + năng lượng hoặc khả năng nhập khẩu.
                float food = 0, moist = 0; int cellN = 0; bool water = false;
                foreach (var pid in c.Provinces) food += w.GetProvince(pid).FoodPotential;
                foreach (var pid in c.Provinces) foreach (int cell in w.GetProvince(pid).Cells)
                {
                    if (w.IsRiver[cell] || w.IsLake[cell]) water = true;
                    moist += w.Moisture[cell]; cellN++;
                }
                // Nước: sông/hồ/biển, hoặc đủ mưa (độ ẩm TB) để có nước mưa/nước ngầm.
                if (!water && !c.HasPort && (cellN == 0 || moist / cellN < 0.22f)) r.Fail(c.Name + ": không có nguồn nước (không sông/hồ/biển, mưa thấp)");
                if (food < PoliticsGenConsts.MinFood) r.Fail(c.Name + ": lương thực nền quá thấp (" + food.ToString("0.0") + ")");
                bool energy = false;
                foreach (var d in w.Deposits)
                    if (!d.Offshore && w.CountryOfCell[d.Cell] == c.Id.Value && (d.Type == ResourceType.Coal || d.Type == ResourceType.Oil || d.Type == ResourceType.Gas)) { energy = true; break; }
                if (!energy && !c.HasPort && c.Neighbors.Count == 0) r.Fail(c.Name + ": không có năng lượng và không thể nhập khẩu");
            }

            // Tỉnh
            foreach (var p in w.Provinces)
            {
                if (p.Owner.IsNone || p.Owner.Value >= w.Countries.Count) { r.Fail(p.Name + ": chủ không hợp lệ"); continue; }
                if (!w.GetCountry(p.Owner).Provinces.Contains(p.Id)) r.Fail(p.Name + ": chỉ mục quốc gia→tỉnh không khớp");
                if (p.Cells.Length < cfg.MinCellsPerProvince && w.GetCountry(p.Owner).Provinces.Count > 1) r.Fail(p.Name + ": quá nhỏ (" + p.Cells.Length + " ô)");
                foreach (int c in p.Cells)
                {
                    if (w.ProvinceOfCell[c] != p.Id.Value) { r.Fail(p.Name + ": ProvinceOfCell không khớp"); break; }
                    if (w.CountryOfCell[c] != p.Owner.Value) { r.Fail(p.Name + ": ô thuộc tỉnh khác quốc gia"); break; }
                }
                if (!IsConnected(w, p.Cells)) r.Fail(p.Name + ": tỉnh không liên thông");
                if (p.Cities.Count < cfg.MinCitiesPerProvince || p.Cities.Count > cfg.MaxCitiesPerProvince)
                    r.Fail(p.Name + ": " + p.Cities.Count + " thành phố ngoài khoảng [" + cfg.MinCitiesPerProvince + "," + cfg.MaxCitiesPerProvince + "]");
                if (p.Population < 0) r.Fail(p.Name + ": dân số âm");
            }

            // Thành phố
            foreach (var c in w.Cities)
            {
                if (c.Province.IsNone) { r.Fail(c.Name + ": không có tỉnh"); continue; }
                var prov = w.GetProvince(c.Province);
                if (w.ProvinceOfCell[c.Cell] != prov.Id.Value) r.Fail(c.Name + ": ô thành phố không thuộc tỉnh của nó");
                if (!prov.Cities.Contains(c.Id)) r.Fail(c.Name + ": chỉ mục tỉnh→thành phố không khớp");
                if (!w.IsLand[c.Cell] || w.IsLake[c.Cell]) r.Fail(c.Name + ": nằm trên nước");
                if (w.Slope[c.Cell] > 0.97f) r.Fail(c.Name + ": nằm trên địa hình quá dốc");
                if (c.IsPort)
                {
                    bool nearWater = false; foreach (int nb in g.Neighbors(c.Cell)) if (w.IsOcean[nb]) { nearWater = true; break; }
                    if (!nearWater) r.Fail(c.Name + ": cảng không tiếp giáp biển");
                }
                if (float.IsNaN(c.X) || float.IsNaN(c.Y)) r.Fail(c.Name + ": toạ độ NaN");
            }

            // Mỗi ô tối đa một thành phố
            var seen = new HashSet<int>();
            foreach (var c in w.Cities) if (!seen.Add(c.Cell)) { r.Fail("Hai thành phố trùng ô " + c.Cell); break; }

            // Tên trùng
            var names = new HashSet<string>();
            foreach (var c in w.Countries) if (!names.Add(c.Name)) r.Fail("Trùng tên quốc gia: " + c.Name);

            // Dân số khớp
            foreach (var c in w.Countries)
            {
                long sum = 0; foreach (var pid in c.Provinces) sum += w.GetProvince(pid).Population;
                if (sum != c.Population) r.Fail(c.Name + ": tổng dân số tỉnh != dân số quốc gia");
            }
            return r;
        }

        private static bool IsConnected(WorldState w, int[] cells)
        {
            if (cells.Length == 0) return false;
            var set = new HashSet<int>(cells); var seen = new HashSet<int> { cells[0] }; var st = new Stack<int>(); st.Push(cells[0]);
            while (st.Count > 0)
            {
                int c = st.Pop();
                foreach (int nb in w.Grid.Neighbors(c)) if (set.Contains(nb) && seen.Add(nb)) st.Push(nb);
            }
            return seen.Count == set.Count;
        }
    }

    /// <summary>Hash trạng thái thế giới để kiểm tra tính tất định (cùng seed → cùng hash).</summary>
    public static class WorldHash
    {
        public static ulong Compute(WorldState w)
        {
            ulong h = 14695981039346656037UL;
            void Mix(long v) { unchecked { h ^= (ulong)v; h *= 1099511628211UL; } }
            Mix(w.CellCount);
            for (int i = 0; i < w.CellCount; i++)
            {
                Mix((long)Math.Round(w.Grid.X[i] * 100f)); Mix((long)Math.Round(w.Grid.Y[i] * 100f));
                Mix((long)Math.Round(w.Elevation[i] * 10000f));
                Mix(w.Biomes[i]); Mix(w.CountryOfCell[i]); Mix(w.ProvinceOfCell[i]); Mix(w.CityOfCell[i]);
                Mix(w.IsRiver[i] ? 1 : 0); Mix(w.IsRoad[i] ? 1 : 0);
            }
            foreach (var c in w.Countries) { Mix(c.SeedCell); Mix(c.Capital.Value); Mix(c.Population); Mix((long)c.Personality); }
            foreach (var p in w.Provinces) { Mix(p.Cells.Length); Mix(p.Population); }
            foreach (var c in w.Cities) { Mix(c.Cell); Mix(c.Population); }
            foreach (var d in w.Deposits) { Mix((long)d.Type); Mix(d.Cell); Mix((long)Math.Round(d.Richness * 1000f)); }
            return h;
        }
    }
}

namespace Vigo.Core
{
    internal static class PoliticsGenConsts { public const float MinFood = 30f; }
}
