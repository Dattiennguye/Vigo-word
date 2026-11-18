using System;
using System.Collections.Generic;
using System.Linq;

namespace Vigo.Core.Economy
{
    /// <summary>
    /// Kinh tế theo tuần: sản xuất (thô → chế biến) → tiêu dùng hộ → dân số → tài chính/đầu tư → giá → thương mại.
    /// Mọi hàng hóa đi qua sổ cái nên kiểm toán được (AuditGoods). Tất định theo seed + chuỗi lệnh.
    /// </summary>
    public sealed class EconomySim
    {
        public readonly WorldState W;
        public readonly EconomyState S;
        private readonly ulong _seed;
        private const double TargetCoverFood = 6, TargetCover = 4;

        public EconomySim(WorldState w) { W = w; _seed = w.Seed; S = EconomyInit.Create(w); }
        /// <summary>Dùng khi nạp bản lưu.</summary>
        public EconomySim(WorldState w, EconomyState state) { W = w; _seed = w.Seed; S = state; }

        // ================= Lệnh (lớp Command sẽ gọi các hàm này ở GĐ sau) =================
        public void SetTaxRate(CountryId c, double rate) { S.TaxRate[c.Value] = Math.Max(0, Math.Min(0.8, rate)); }
        public void SetBudget(CountryId c, double edu, double health, double invest, double defense)
        {
            double sum = edu + health + invest + defense; if (sum > 1) { edu /= sum; health /= sum; invest /= sum; defense /= sum; }
            int i = c.Value; S.ShareEdu[i] = Math.Max(0, edu); S.ShareHealth[i] = Math.Max(0, health); S.ShareInvest[i] = Math.Max(0, invest); S.ShareDefense[i] = Math.Max(0, defense);
        }
        public void SetInvestWeights(CountryId c, double[] weights) { S.InvestWeight[c.Value] = weights == null ? null : (double[])weights.Clone(); }
        public void SetTradeBlock(CountryId a, CountryId b, bool blocked)
        {
            long k1 = a.Value * 10000L + b.Value, k2 = b.Value * 10000L + a.Value;
            if (blocked) { S.Blocked.Add(k1); S.Blocked.Add(k2); } else { S.Blocked.Remove(k1); S.Blocked.Remove(k2); }
        }
        /// <summary>Phá hủy (chiến tranh/thiên tai): giảm công suất của tỉnh theo tỉ lệ 0..1.</summary>
        public void DamageProvince(ProvinceId p, double fraction)
        {
            double k = 1 - Math.Max(0, Math.Min(1, fraction)); int i = p.Value;
            S.FarmCap[i] *= k; S.ForestCap[i] *= k; S.MineBase[i] *= k; S.MineBonus[i] *= k;
            for (int s = 0; s < EconData.IndustrySectors; s++) S.IndCap[i][s] *= k;
        }
        private bool Blocked(int a, int b) => S.Blocked.Count > 0 && S.Blocked.Contains(a * 10000L + b);

        // ================= Kiểm toán =================
        /// <summary>Sai lệch lớn nhất giữa tồn kho thật và sổ cái (phải ≈ 0).</summary>
        public double AuditGoods()
        {
            double worst = 0; var L = S.Ledger;
            for (int g = 0; g < EconData.GoodCount; g++)
            {
                double stock = 0; for (int c = 0; c < S.Stock.Length; c++) stock += S.Stock[c][g];
                double expect = L.Initial[g] + L.Produced[g] - L.Household[g] - L.Industry[g] - L.Invested[g] - L.Military[g] - L.Lost[g];
                worst = Math.Max(worst, Math.Abs(stock - expect) / Math.Max(1, L.Initial[g] + L.Produced[g]));
            }
            return worst;
        }

        public double TotalStock(Good g) { double t = 0; for (int c = 0; c < S.Stock.Length; c++) t += S.Stock[c][(int)g]; return t; }

        // ================= Vòng tuần =================
        public void StepWeek()
        {
            S.Week++;
            int NC = W.Countries.Count, NP = W.Provinces.Count, G = EconData.GoodCount;
            SyncOwnership();
            var rng = Rng.Stream(_seed, "econ-weather", S.Week);
            var weather = new double[NC];
            for (int c = 0; c < NC; c++) weather[c] = Math.Max(0.7, Math.Min(1.3, 1 + 0.06 * rng.Gaussian()));

            var va = new double[NC]; var hhNeed = new double[NC][]; var hhGot = new double[NC][];
            var mult = new double[NP]; var labUsed = new double[NC]; var labAvail = new double[NC];
            for (int c = 0; c < NC; c++) { hhNeed[c] = new double[G]; hhGot[c] = new double[G]; }

            // Điều tiết sản lượng: tồn kho vượt xa nhu cầu → doanh nghiệp giảm sản xuất (không bao giờ tăng quá công suất)
            var throttle = new double[NC][];
            for (int c = 0; c < NC; c++)
            {
                throttle[c] = new double[G]; for (int g = 0; g < G; g++) throttle[c][g] = 1;
                if (S.Dead[c]) continue;
                var dm = Demand(c);
                for (int g = 0; g < G; g++)
                {
                    double t2 = 2 * (g == 0 ? TargetCoverFood : TargetCover), cover = S.Stock[c][g] / Math.Max(1e-6, dm[g]);
                    throttle[c][g] = cover <= t2 ? 1 : Math.Max(0.1, t2 / cover);
                }
            }
            _throttle = throttle;

            // 0) Khấu hao công suất (cần đầu tư máy móc để duy trì)
            for (int p = 0; p < NP; p++)
            {
                if (OwnerOf(p) < 0) continue;
                double k = 1 - EconData.Depreciation;
                S.FarmCap[p] *= k; S.ForestCap[p] *= k; S.MineBonus[p] *= k;
                for (int s = 0; s < EconData.IndustrySectors; s++) S.IndCap[p][s] *= k;
            }

            // 1) Lao động & hệ số năng suất theo tỉnh (quân nhân không đi làm)
            var cpopArr = new double[NC]; for (int c = 0; c < NC; c++) cpopArr[c] = CountryPop(c);
            for (int p = 0; p < NP; p++)
            {
                int c = OwnerOf(p); if (c < 0) { S.Util[p] = 0; continue; }
                double soldiers = cpopArr[c] > 0 ? Math.Min(S.Troops[c], 0.9 * cpopArr[c]) * S.Pop[p] / cpopArr[c] : 0;
                double lab = Math.Max(0, EconData.LabourShare * S.Pop[p] * (0.85 + 0.3 * S.Health[c]) - soldiers);
                double req = S.FarmCap[p] * EconData.LabourPerUnit[0] + S.ForestCap[p] * EconData.LabourPerUnit[1]
                           + (S.MineBase[p] + S.MineBonus[p]) * EconData.LabourPerUnit[2];
                for (int s = 0; s < EconData.IndustrySectors; s++) req += S.IndCap[p][s] * EconData.LabourPerUnit[3 + s];
                S.Util[p] = req <= 1e-9 ? 0 : Math.Min(1, lab / req);
                double taxF = 1 - 0.8 * Math.Max(0, S.TaxRate[c] - 0.25);
                mult[p] = (0.8 + 0.4 * S.Edu[c]) * taxF;
                labAvail[c] += lab; labUsed[c] += Math.Min(lab, req);
            }

            // 2) Sản xuất nguyên liệu thô
            for (int p = 0; p < NP; p++)
            {
                int c = OwnerOf(p); if (c < 0) continue;
                double u = S.Util[p] * mult[p];
                Produce(c, Good.Food, S.FarmCap[p] * u * weather[c], va);
                Produce(c, Good.Wood, S.ForestCap[p] * u, va);
                double boost = S.MineBase[p] > 1e-12 ? 1 + S.MineBonus[p] / S.MineBase[p] : 1;
                foreach (int d in S.DepsOfProvince[p])
                {
                    double ext = Math.Min(S.DepRemaining[d], S.DepRate[d] * boost * u * _throttle[c][(int)EconData.ToGood(W.Deposits[d].Type)]);
                    if (ext <= 0) continue;
                    S.DepRemaining[d] -= ext;
                    Produce(c, EconData.ToGood(W.Deposits[d].Type), ext, va, false);
                }
            }
            // 3) Chế biến (thứ tự cố định: Steel → Fuel → Machinery → Goods; trong ngành theo id tỉnh)
            var shortage = new double[NC][]; for (int c = 0; c < NC; c++) shortage[c] = new double[G];
            for (int si = 0; si < EconData.IndustrySectors; si++)
            {
                var r = EconData.Industry[si];
                for (int p = 0; p < NP; p++)
                {
                    int c = OwnerOf(p); if (c < 0) continue;
                    double cap = S.IndCap[p][si] * S.Util[p] * mult[p] * _throttle[c][(int)r.Out]; if (cap <= 0) continue;
                    double runs = cap;
                    for (int i = 0; i < r.In.Length; i++) runs = Math.Min(runs, S.Stock[c][(int)r.In[i]] / r.Qty[i]);
                    runs = Math.Max(0, runs);
                    double inCost = 0;
                    for (int i = 0; i < r.In.Length; i++)
                    {
                        double use = runs * r.Qty[i]; S.Stock[c][(int)r.In[i]] -= use; S.Ledger.Industry[(int)r.In[i]] += use;
                        inCost += use * EconData.BasePrice[(int)r.In[i]];
                        shortage[c][(int)r.In[i]] += (cap - runs) * r.Qty[i];
                    }
                    va[c] -= inCost; Produce(c, r.Out, runs, va, false);
                    S.Treasury[c] += inCost; // doanh thu bán hàng trang trải đầu vào trung gian (doanh nghiệp không trả bằng ngân sách thuế)
                }
            }

            // 4) Tiêu dùng hộ gia đình
            for (int c = 0; c < NC; c++)
            {
                if (S.Dead[c]) continue;
                double pop = CountryPop(c);
                for (int g = 0; g < G; g++)
                {
                    double n = pop * EconData.HouseholdNeed[g]; hhNeed[c][g] = n; if (n <= 0) continue;
                    double got = Math.Min(S.Stock[c][g], n); S.Stock[c][g] -= got; hhGot[c][g] = got; S.Ledger.Household[g] += got;
                }
            }

            // 4b) Hao hụt tồn kho
            for (int c = 0; c < NC; c++)
            {
                if (S.Dead[c]) continue;
                for (int g = 0; g < G; g++) { double sp = S.Stock[c][g] * EconData.Spoilage[g]; if (sp > 0) { S.Stock[c][g] -= sp; S.Ledger.Lost[g] += sp; } }
            }

            // 5) Dân số: sinh − tử (cải thiện theo y tế) − nạn đói
            for (int p = 0; p < NP; p++)
            {
                int c = OwnerOf(p); if (c < 0) continue;
                double foodSat = hhNeed[c][0] > 0 ? hhGot[c][0] / hhNeed[c][0] : 1;
                double rate = 0.00022 - 0.00012 * (1.4 - 0.5 * S.Health[c]) - (1 - foodSat) * 0.01;
                S.Pop[p] = Math.Max(0, S.Pop[p] * (1 + rate));
                var pr = W.Provinces[p]; pr.Population = (long)S.Pop[p];
            }
            for (int c = 0; c < NC; c++) { double t = 0; foreach (var pid in W.Countries[c].Provinces) t += W.GetProvince(pid).Population; W.Countries[c].Population = (long)t; }

            // 6) Tài chính: thuế, giáo dục, y tế, đầu tư (mua máy móc → thêm công suất), quốc phòng
            for (int c = 0; c < NC; c++)
            {
                if (S.Dead[c]) continue;
                double tax = S.TaxRate[c] * Math.Max(0, va[c]);
                double spendEdu = tax * S.ShareEdu[c], spendHealth = tax * S.ShareHealth[c], spendInv = tax * S.ShareInvest[c], spendDef = tax * S.ShareDefense[c];
                double planned = spendEdu + spendHealth + spendInv + spendDef;
                double interest = 0.0005 * Math.Max(0, -S.Treasury[c]);
                double avail = Math.Max(0, S.Treasury[c]) + tax - interest; if (avail < 0) avail = 0;
                double k = planned > avail ? (planned > 0 ? avail / planned : 0) : 1;
                spendEdu *= k; spendHealth *= k; spendInv *= k; spendDef *= k;
                double ideal = 0.06 * Math.Max(1e-9, va[c]);
                S.Edu[c] += (Math.Min(1, spendEdu / ideal) - S.Edu[c]) * 0.02;
                S.Health[c] += (Math.Min(1, spendHealth / ideal) - S.Health[c]) * 0.02;
                double spent = spendEdu + spendHealth + spendDef;
                spent += Invest(c, spendInv);
                S.DefenseSpending[c] = spendDef;
                S.Treasury[c] += tax - spent - interest; // lãi nợ là khoản chi hủy tiền (chưa chuyển cho chủ nợ)
                // Rò rỉ: số dư vượt 30 tuần thuế tiềm năng bị chi tiêu ngoài mô hình (tiêu dùng tư nhân/cất trữ) 2%/tuần
                double excess = S.Treasury[c] - 30 * S.TaxRate[c] * PotentialVA(c);
                if (excess > 0) S.Treasury[c] -= 0.02 * excess;
                var ind = S.Indicators[c];
                ind.TaxRevenue = tax;
                S.Indicators[c] = ind;
            }

            // 7) Giá quốc gia theo độ che phủ tồn kho
            for (int c = 0; c < NC; c++)
            {
                if (S.Dead[c]) continue;
                var dem = Demand(c);
                for (int g = 0; g < G; g++)
                {
                    double target = g == 0 ? TargetCoverFood : TargetCover;
                    double cover = S.Stock[c][g] / Math.Max(1e-6, dem[g]);
                    double goal = EconData.BasePrice[g] * Math.Max(0.3, Math.Min(6, Math.Pow(target / Math.Max(0.05, cover), 0.6)));
                    if (dem[g] <= 1e-9) goal = EconData.BasePrice[g] * 0.5;
                    S.Price[c][g] += (goal - S.Price[c][g]) * 0.3;
                }
            }

            // 8) Thương mại thế giới
            Trade();

            // 9) Chỉ số
            for (int c = 0; c < NC; c++)
            {
                var ind = S.Indicators[c];
                ind.Population = W.Countries[c].Population; ind.ValueAdded = va[c]; ind.Treasury = S.Treasury[c];
                ind.FoodSat = Sat(hhGot[c][(int)Good.Food], hhNeed[c][(int)Good.Food]);
                ind.FuelSat = Sat(hhGot[c][(int)Good.Fuel], hhNeed[c][(int)Good.Fuel]);
                ind.GoodsSat = Sat(hhGot[c][(int)Good.Goods], hhNeed[c][(int)Good.Goods]);
                ind.Edu = S.Edu[c]; ind.Health = S.Health[c];
                ind.LabourUtil = labAvail[c] > 0 ? labUsed[c] / labAvail[c] : 0;
                double pi = 0; for (int g = 0; g < G; g++) pi += S.Price[c][g] / EconData.BasePrice[g]; ind.PriceIndex = pi / G;
                S.Indicators[c] = ind;
            }
        }

        private double[][] _throttle;
        private static double Sat(double got, double need) => need > 0 ? got / need : 1;

        private void Produce(int c, Good g, double qty, double[] va, bool throttle = true)
        {
            if (qty <= 0) return;
            if (throttle) qty *= _throttle[c][(int)g];
            S.Stock[c][(int)g] += qty; S.Ledger.Produced[(int)g] += qty; va[c] += qty * EconData.BasePrice[(int)g];
        }

        private int OwnerOf(int p) { var o = W.Provinces[p].Owner; return (o.IsNone || S.Dead[o.Value]) ? -1 : o.Value; }

        private double CountryPop(int c) { double t = 0; foreach (var pid in W.Countries[c].Provinces) t += S.Pop[pid.Value]; return t; }

        /// <summary>Nước bị xóa sổ: hàng tồn mất khỏi thế giới (ghi sổ "Lost").</summary>
        private void SyncOwnership()
        {
            for (int c = 0; c < W.Countries.Count; c++)
            {
                if (S.Dead[c] || !W.Countries[c].Eliminated) continue;
                S.Dead[c] = true;
                for (int g = 0; g < EconData.GoodCount; g++) { S.Ledger.Lost[g] += S.Stock[c][g]; S.Stock[c][g] = 0; }
                S.Treasury[c] = 0;
            }
        }

        /// <summary>Nhu cầu tuần của quốc gia: hộ + đầu vào ngành ở công suất đầy đủ + máy móc đầu tư.</summary>
        private double[] Demand(int c)
        {
            var d = new double[EconData.GoodCount];
            double pop = CountryPop(c);
            for (int g = 0; g < d.Length; g++) d[g] = pop * EconData.HouseholdNeed[g];
            foreach (var pid in W.Countries[c].Provinces)
                for (int si = 0; si < EconData.IndustrySectors; si++)
                {
                    var r = EconData.Industry[si]; double cap = S.IndCap[pid.Value][si];
                    for (int i = 0; i < r.In.Length; i++) d[(int)r.In[i]] += cap * r.Qty[i];
                }
            d[(int)Good.Machinery] += S.Indicators[c].TaxRevenue * S.ShareInvest[c] / Math.Max(1, S.Price[c][(int)Good.Machinery]);
            return d;
        }

        /// <summary>Chi tiền mua máy móc trong nước và thêm công suất. Trả về số tiền thực chi.</summary>
        private double Invest(int c, double money)
        {
            int mi = (int)Good.Machinery;
            double qty = Math.Min(S.Stock[c][mi], money / Math.Max(1e-6, S.Price[c][mi]));
            if (qty <= 1e-12) return 0;
            S.Stock[c][mi] -= qty; S.Ledger.Invested[mi] += qty;
            var w = S.InvestWeight[c];
            if (w == null)
            {
                w = new double[EconData.SectorCount];
                var outGood = new[] { Good.Food, Good.Wood, Good.Coal, Good.Steel, Good.Fuel, Good.Machinery, Good.Goods };
                for (int s = 0; s < w.Length; s++)
                {
                    double pr = S.Price[c][(int)outGood[s]] / EconData.BasePrice[(int)outGood[s]];
                    w[s] = Math.Max(0, Math.Min(4, pr - 0.5));  // giá dưới 50% giá gốc (dư thừa) → không đầu tư
                }
                w[2] = 0; // khai khoáng chỉ mở rộng khi đặt ưu tiên rõ ràng (mỏ có hạn)
            }
            double ws = w.Sum(); if (ws <= 1e-9) { S.Stock[c][mi] += qty; S.Ledger.Invested[mi] -= qty; return 0; }
            double pop = CountryPop(c); if (pop <= 0) return 0;
            for (int s = 0; s < w.Length; s++)
            {
                double units = qty * w[s] / ws / EconData.CapexPerUnit[s];
                foreach (var pid in W.Countries[c].Provinces)
                {
                    int p = pid.Value; double frac = S.Pop[p] / pop; double add = units * frac;
                    switch ((Sector)s)
                    {
                        case Sector.Farm: S.FarmCap[p] += add; break;
                        case Sector.Forestry: S.ForestCap[p] += add; break;
                        case Sector.Mining: if (S.MineBase[p] > 0) S.MineBonus[p] += add; else S.ForestCap[p] += 0; break;
                        default: S.IndCap[p][s - 3] += add; break;
                    }
                }
            }
            return qty * S.Price[c][mi];
        }

        /// <summary>Giá trị gia tăng tiềm năng/tuần ở công suất đầy đủ (giá gốc) — cơ sở hạn mức tín dụng.</summary>
        public double PotentialVA(int c)
        {
            double v = 0;
            foreach (var pid in W.Countries[c].Provinces)
            {
                int p = pid.Value;
                v += S.FarmCap[p] * EconData.BasePrice[0] + S.ForestCap[p] * EconData.BasePrice[1] + (S.MineBase[p] + S.MineBonus[p]) * 4.0;
                for (int si = 0; si < EconData.IndustrySectors; si++)
                {
                    var r = EconData.Industry[si]; double unit = EconData.BasePrice[(int)r.Out];
                    for (int i = 0; i < r.In.Length; i++) unit -= r.Qty[i] * EconData.BasePrice[(int)r.In[i]];
                    v += S.IndCap[p][si] * Math.Max(0, unit);
                }
            }
            return v;
        }
        /// <summary>Hạn mức vay = 10 tuần thuế tiềm năng. Nợ = kho bạc âm; tiền nhập khẩu vẫn đối ứng đủ với chủ nợ.</summary>
        public double CreditLimit(int c) => 10 * S.TaxRate[c] * PotentialVA(c);

        // ================= Thương mại =================
        /// <summary>Chỉ chạy bước thương mại (dùng cho kiểm thử tổng tiền bằng 0).</summary>
        public void RunTradeOnly() { Trade(); }
        private void Trade()
        {
            int NC = W.Countries.Count, G = EconData.GoodCount;
            var budget = new double[NC]; var dem = new double[NC][];
            for (int c = 0; c < NC; c++) { if (S.Dead[c]) continue; budget[c] = Math.Max(0, S.Treasury[c] + CreditLimit(c)) * 0.5; dem[c] = Demand(c); }
            for (int g = 0; g < G; g++)
            {
                double target = g == 0 ? TargetCoverFood : TargetCover;
                double sumPS = 0, sumS = 0;
                for (int c = 0; c < NC; c++) { if (S.Dead[c]) continue; sumPS += S.Price[c][g] * S.Stock[c][g]; sumS += S.Stock[c][g]; }
                double pw = sumS > 0 ? sumPS / sumS : EconData.BasePrice[g];
                var exp = new double[NC]; var imp = new double[NC]; double totalExp = 0;
                for (int c = 0; c < NC; c++)
                {
                    if (S.Dead[c]) continue;
                    double want = target * dem[c][g];
                    exp[c] = Math.Max(0, S.Stock[c][g] - 1.5 * want); totalExp += exp[c];
                    double need = Math.Min(Math.Max(0, want - S.Stock[c][g]), 2 * dem[c][g]);
                    imp[c] = g == 0 ? need : Math.Min(need, budget[c] / pw); // lương thực: vay cứu đói, không giới hạn bởi hạn mức
                }
                if (totalExp <= 1e-12) continue;
                // Ma trận yêu cầu x[i,j] (i nhập từ j), chia theo tỉ lệ nguồn cung, bỏ cặp bị cấm.
                var x = new double[NC, NC]; var colSum = new double[NC];
                for (int i = 0; i < NC; i++)
                {
                    if (imp[i] <= 1e-12) continue;
                    double avail = 0; for (int j = 0; j < NC; j++) if (exp[j] > 0 && !Blocked(i, j)) avail += exp[j];
                    if (avail <= 1e-12) continue;
                    for (int j = 0; j < NC; j++) if (exp[j] > 0 && !Blocked(i, j)) { x[i, j] = imp[i] * exp[j] / avail; colSum[j] += x[i, j]; }
                }
                for (int j = 0; j < NC; j++)
                {
                    double f = colSum[j] > exp[j] ? exp[j] / colSum[j] : 1;
                    if (colSum[j] <= 0) continue;
                    for (int i = 0; i < NC; i++)
                    {
                        double q = x[i, j] * f; if (q <= 1e-12) continue;
                        double pay = q * pw;
                        S.Stock[j][g] -= q; S.Stock[i][g] += q;
                        S.Treasury[i] -= pay; S.Treasury[j] += pay; budget[i] -= pay;
                        S.Ledger.TradeVolume[g] += q;
                    }
                }
            }
            // kiểm tra tổng tiền: giao thương không tạo/hủy tiền (Σ thay đổi = 0 theo thiết kế; sai số số học ghi lại)
        }
    }
}
