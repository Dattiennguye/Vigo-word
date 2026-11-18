using System;
using System.Collections.Generic;

namespace Vigo.Core.AI
{
    /// <summary>
    /// AI điều hành ngân sách quốc gia (GĐ4a). Chạy mỗi 4 tuần, hoàn toàn tất định (không RNG), chỉ đọc chỉ số công khai
    /// của kinh tế và chỉ đưa lệnh qua API giống người chơi. Quốc gia người chơi bị loại khỏi AI.
    /// </summary>
    public sealed class CountryAI
    {
        private readonly Economy.EconomySim _eco;
        private readonly HashSet<int> _human = new HashSet<int>();
        public int Interval = 4;
        /// <summary>Nếu gán, nước đang có chiến tranh sẽ chuyển sang ngân sách thời chiến.</summary>
        public Military.WarSim War;
        public bool Austerity = true, SmartInvest = true, PersonalityBudget = true;

        public CountryAI(Economy.EconomySim eco) { _eco = eco; }
        public void SetHuman(CountryId c, bool human) { if (human) _human.Add(c.Value); else _human.Remove(c.Value); }
        public bool IsHuman(CountryId c) => _human.Contains(c.Value);

        /// <summary>Gọi sau mỗi StepWeek; chỉ hành động vào tuần chia hết Interval.</summary>
        public void Think()
        {
            if (_eco.S.Week % Interval != 0) return;
            foreach (var c in _eco.W.Countries)
            {
                if (c.Eliminated || _eco.S.Dead[c.Id.Value] || _human.Contains(c.Id.Value)) continue;
                Govern(c);
            }
        }

        private static double Clamp(double v, double lo, double hi) => v < lo ? lo : (v > hi ? hi : v);

        private void Govern(Country c)
        {
            var S = _eco.S; int i = c.Id.Value; var ind = S.Indicators[i];
            // Tính cách → mục tiêu ngân sách nền
            double edu = 0.20, health = 0.20, invest = 0.20, def = 0.15;
            switch (c.Personality)
            {
                case Personality.Military: def = 0.28; invest = 0.18; edu = 0.14; health = 0.16; break;
                case Personality.Technology: edu = 0.28; invest = 0.22; def = 0.10; break;
                case Personality.Industrial: invest = 0.32; def = 0.12; edu = 0.16; break;
                case Personality.Trade: invest = 0.24; def = 0.10; break;
                case Personality.Expansionist: def = 0.24; invest = 0.20; break;
            }
            if (!PersonalityBudget) { edu = 0.20; health = 0.20; invest = 0.20; def = 0.15; }
            double pva = Math.Max(1e-9, _eco.PotentialVA(i));
            double debt = Math.Max(0, -S.Treasury[i]);
            double debtWeeks = debt / Math.Max(1e-9, S.TaxRate[i] * pva); // nợ tính bằng số tuần thuế tiềm năng

            // 1) Thuế: giữ 25%. Phát hiện qua thử nghiệm: tăng thuế khi mắc nợ phản tác dụng trong mô hình
            //    (thuế >25% giảm sản lượng, chi tiêu tính theo tỉ lệ thuế nên chi tăng theo) → AI không dùng thuế để chữa nợ.
            double tax = 0.25;

            if (War != null && War.AtWar(i)) { def = 0.45; invest = 0.10; edu = 0.10; health = 0.15; }
            // 2) Khẩn khoản: nợ nặng hoặc đói → thắt lưng buộc bụng (cắt quốc phòng/đầu tư không thiết yếu, giữ y tế)
            if (Austerity && debtWeeks > 6) { def *= 0.4; edu *= 0.8; invest *= 0.6; }
            if (Austerity && ind.FoodSat < 0.95) { def *= 0.3; health = Math.Max(health, 0.2); }
            _eco.SetTaxRate(c.Id, tax);
            _eco.SetBudget(c.Id, edu, health, invest, def);

            // 3) Đầu tư: nhắm ngành đang khan hiếm (giá cao), nông nghiệp khi thiếu ăn; khai khoáng khi sắp cạn
            var w = new double[Economy.EconData.SectorCount];
            var outGood = new[] { Economy.Good.Food, Economy.Good.Wood, Economy.Good.Coal, Economy.Good.Steel, Economy.Good.Fuel, Economy.Good.Machinery, Economy.Good.Goods };
            for (int s = 0; s < w.Length; s++)
            {
                double pr = S.Price[i][(int)outGood[s]] / Economy.EconData.BasePrice[(int)outGood[s]];
                w[s] = Math.Max(0, Math.Min(4, pr - 0.5));
            }
            w[2] = 0;
            if (ind.FoodSat < 0.95) w[0] += 4;
            if (c.Personality == Personality.Industrial) { w[3] += 0.3; w[5] += 0.3; w[6] += 0.3; }
            if (c.Personality == Personality.Technology) w[5] += 0.5;
            double sum = 0; foreach (double v in w) sum += v;
            _eco.SetInvestWeights(c.Id, SmartInvest && sum > 1e-9 ? w : null);
        }
    }
}
