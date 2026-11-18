using System;
using System.Linq;

namespace Vigo.Core
{
    /// <summary>Thao tác thay đổi thế giới giữ nguyên bất biến phân cấp (chiếm/nhượng tỉnh).</summary>
    public static class WorldOps
    {
        /// <summary>Chuyển một tỉnh sang nước khác. Trả false nếu không đổi. Nước mất hết tỉnh → Eliminated.</summary>
        public static bool TransferProvince(WorldState w, ProvinceId pid, CountryId to)
        {
            var p = w.GetProvince(pid);
            if (p.Owner == to || to.IsNone) return false;
            var from = w.GetCountry(p.Owner); var dst = w.GetCountry(to);
            if (dst.Eliminated) return false;
            from.Provinces.Remove(pid); dst.Provinces.Add(pid);
            p.Owner = to;
            foreach (int c in p.Cells) w.CountryOfCell[c] = to.Value;
            from.LandCells -= p.Cells.Length; dst.LandCells += p.Cells.Length;
            from.Population -= p.Population; dst.Population += p.Population;
            if (from.CapitalProvince == pid)
            {
                if (!from.Capital.IsNone) w.GetCity(from.Capital).IsCapital = false;
                from.Capital = CityId.None; from.CapitalProvince = ProvinceId.None;
                if (from.Provinces.Count > 0)
                {
                    var best = from.Provinces.Select(x => w.GetProvince(x)).OrderByDescending(x => x.Population).ThenBy(x => x.Id.Value).First();
                    var city = best.Cities.Select(x => w.GetCity(x)).OrderByDescending(x => x.Population).ThenBy(x => x.Id.Value).First();
                    city.IsCapital = true; from.Capital = city.Id; from.CapitalProvince = best.Id;
                }
            }
            if (from.Provinces.Count == 0) from.Eliminated = true;
            RecomputeDerived(w);
            return true;
        }

        public static void RecomputeDerived(WorldState w)
        {
            foreach (var c in w.Countries) { c.Neighbors.Clear(); c.HasPort = false; }
            var seen = new System.Collections.Generic.HashSet<long>();
            for (int i = 0; i < w.CellCount; i++)
            {
                int a = w.CountryOfCell[i]; if (a < 0) continue;
                foreach (int nb in w.Grid.Neighbors(i))
                {
                    int b = w.CountryOfCell[nb]; if (b < 0 || b == a) continue;
                    if (seen.Add((long)a * 100000 + b)) w.Countries[a].Neighbors.Add(new CountryId(b));
                }
            }
            foreach (var city in w.Cities) if (city.IsPort) { var o = w.GetProvince(city.Province).Owner; if (!o.IsNone) w.GetCountry(o).HasPort = true; }
        }
    }
}
