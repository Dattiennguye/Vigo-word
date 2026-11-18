using System;
using System.Collections.Generic;
using System.Linq;

namespace Vigo.Core.Economy
{
    /// <summary>Khởi tạo kinh tế từ dữ liệu thế giới thật (đất đai, rừng, mỏ, dân, tính cách quốc gia).</summary>
    public static class EconomyInit
    {
        public static EconomyState Create(WorldState w)
        {
            int NC = w.Countries.Count, NP = w.Provinces.Count, G = EconData.GoodCount;
            var s = new EconomyState();
            s.Pop = new double[NP]; s.FarmCap = new double[NP]; s.ForestCap = new double[NP];
            s.MineBase = new double[NP]; s.MineBonus = new double[NP]; s.Util = new double[NP];
            s.IndCap = new double[NP][];
            for (int p = 0; p < NP; p++) { s.IndCap[p] = new double[EconData.IndustrySectors]; s.Pop[p] = w.Provinces[p].Population; }
            double P = s.Pop.Sum();

            // ---- Nhu cầu thế giới theo tuần (lan truyền ngược chuỗi chế biến) ----
            var need = new double[G];
            for (int g = 0; g < G; g++) need[g] = P * EconData.HouseholdNeed[g];
            need[(int)Good.Machinery] += P * EconData.MachineryPerCapita;
            foreach (int si in new[] { 3, 2, 0, 1 }) // Goods, Machinery, Steel, Fuel (Steel sau Goods/Machinery)
            {
                var r = EconData.Industry[si];
                double o = need[(int)r.Out];
                for (int i = 0; i < r.In.Length; i++) need[(int)r.In[i]] += o * r.Qty[i];
            }

            // ---- Nông nghiệp & lâm nghiệp từ độ phì và biome ----
            var farmW = new double[NP]; var forestW = new double[NP];
            for (int p = 0; p < NP; p++)
                foreach (int c in w.Provinces[p].Cells)
                {
                    farmW[p] += w.Fertility[c];
                    var b = (Biome)w.Biomes[c];
                    forestW[p] += (b == Biome.Forest || b == Biome.TropicalForest || b == Biome.Taiga) ? 1.0 : 0.1 * w.Fertility[c];
                }
            double fs = farmW.Sum(), ws = forestW.Sum();
            for (int p = 0; p < NP; p++)
            {
                s.FarmCap[p] = fs > 0 ? farmW[p] / fs * need[(int)Good.Food] * EconData.FoodMargin : 0;
                s.ForestCap[p] = ws > 0 ? forestW[p] / ws * need[(int)Good.Wood] * EconData.CapacityMargin : 0;
            }

            // ---- Mỏ: gán tỉnh (mỏ ngoài khơi → tỉnh ven bờ gần nhất theo BFS) ----
            int ND = w.Deposits.Count;
            s.DepProvince = new int[ND]; s.DepRate = new double[ND]; s.DepRemaining = new double[ND];
            s.DepsOfProvince = new List<int>[NP]; for (int p = 0; p < NP; p++) s.DepsOfProvince[p] = new List<int>();
            var richSum = new double[G]; var resSum = new double[G];
            for (int d = 0; d < ND; d++)
            {
                s.DepProvince[d] = ProvinceNear(w, w.Deposits[d].Cell);
                int g = (int)EconData.ToGood(w.Deposits[d].Type);
                richSum[g] += Math.Max(1e-6, w.Deposits[d].Richness);
                resSum[g] += Math.Max(1e-6, w.Deposits[d].Reserve);
            }
            for (int d = 0; d < ND; d++)
            {
                var dep = w.Deposits[d]; int g = (int)EconData.ToGood(dep.Type);
                s.DepRate[d] = Math.Max(1e-6, dep.Richness) / richSum[g] * need[g] * EconData.CapacityMargin;
                // trữ lượng quy đổi: tổng thế giới = 120 năm nhu cầu hiện tại (hằng số mô hình, chỉnh được)
                s.DepRemaining[d] = Math.Max(1e-6, dep.Reserve) / resSum[g] * need[g] * 52.0 * 120.0;
                int p = s.DepProvince[d];
                if (p >= 0) { s.DepsOfProvince[p].Add(d); s.MineBase[p] += s.DepRate[d]; }
            }

            // ---- Công nghiệp chế biến: tỉ lệ theo dân × tính cách quốc gia ----
            var indW = new double[NP];
            for (int p = 0; p < NP; p++)
            {
                double f = 1;
                switch (w.GetCountry(w.Provinces[p].Owner).Personality)
                {
                    case Personality.Industrial: f = 1.6; break;
                    case Personality.Technology: f = 1.2; break;
                    case Personality.Military: f = 1.1; break;
                }
                indW[p] = s.Pop[p] * f;
            }
            double iw = indW.Sum();
            for (int si = 0; si < EconData.IndustrySectors; si++)
            {
                double total = need[(int)EconData.Industry[si].Out] * EconData.CapacityMargin;
                for (int p = 0; p < NP; p++) s.IndCap[p][si] = iw > 0 ? indW[p] / iw * total : 0;
            }

            // ---- Quốc gia ----
            s.Stock = new double[NC][]; s.Price = new double[NC][]; s.InvestWeight = new double[NC][];
            s.Treasury = new double[NC]; s.TaxRate = new double[NC]; s.ShareEdu = new double[NC]; s.ShareHealth = new double[NC];
            s.ShareInvest = new double[NC]; s.ShareDefense = new double[NC]; s.Edu = new double[NC]; s.Health = new double[NC];
            s.Dead = new bool[NC]; s.DefenseSpending = new double[NC]; s.Indicators = new CountryIndicators[NC];
            var cpop = new double[NC];
            for (int p = 0; p < NP; p++) { var o = w.Provinces[p].Owner; if (!o.IsNone) cpop[o.Value] += s.Pop[p]; }
            for (int c = 0; c < NC; c++)
            {
                s.Stock[c] = new double[G]; s.Price[c] = (double[])EconData.BasePrice.Clone();
                double share = P > 0 ? cpop[c] / P : 0;
                for (int g = 0; g < G; g++) { s.Stock[c][g] = share * need[g] * 4.0; s.Ledger.Initial[g] += s.Stock[c][g]; }
                s.TaxRate[c] = 0.25; s.ShareEdu[c] = 0.20; s.ShareHealth[c] = 0.20; s.ShareInvest[c] = 0.20; s.ShareDefense[c] = 0.15;
                s.Edu[c] = 0.5; s.Health[c] = 0.5; s.Treasury[c] = 2.0 * cpop[c]; // ~4 tuần thuế
                s.Dead[c] = w.Countries[c].Eliminated;
            }
            return s;
        }

        /// <summary>Tỉnh sở hữu ô, hoặc tỉnh của ô đất gần nhất (BFS) — dùng cho mỏ ngoài khơi/ven hồ.</summary>
        public static int ProvinceNear(WorldState w, int cell)
        {
            if (w.ProvinceOfCell[cell] >= 0) return w.ProvinceOfCell[cell];
            var seen = new HashSet<int> { cell }; var q = new Queue<int>(); q.Enqueue(cell);
            while (q.Count > 0 && seen.Count < 4000)
            {
                int c = q.Dequeue();
                foreach (int nb in w.Grid.Neighbors(c))
                {
                    if (!seen.Add(nb)) continue;
                    if (w.ProvinceOfCell[nb] >= 0) return w.ProvinceOfCell[nb];
                    q.Enqueue(nb);
                }
            }
            return -1;
        }
    }
}
