using System;
using Vigo.Core.Economy;
using Vigo.Core.Military;

namespace Vigo.Core.CityBuilder
{
    /// <summary>
    /// Nối thành phố chi tiết với quốc gia sở hữu nó (GĐ5, một chiều quốc gia → thành phố).
    /// Mỗi tuần: CityLink.Apply(sim, cityId) rồi sim.StepWeek(). Chủ sở hữu được tra lại mỗi lần nên thành phố bị chiếm
    /// sẽ chịu kinh tế/chiến sự của chủ mới.
    /// </summary>
    public sealed class CityLink
    {
        private readonly WorldState _w; private readonly EconomySim _eco; private readonly WarSim _war;
        public CityLink(WorldState w, EconomySim eco, WarSim war = null) { _w = w; _eco = eco; _war = war; }

        public NationalContext Build(CityId city)
        {
            var c = _w.GetCity(city); var prov = _w.GetProvince(c.Province); int owner = prov.Owner.Value;
            var ind = _eco.S.Indicators[owner];
            var ctx = new NationalContext
            {
                FoodSat = Clamp(ind.FoodSat), FuelSat = Clamp(ind.FuelSat), GoodsSat = Clamp(ind.GoodsSat),
                HealthQuality = Clamp(ind.Health), EduQuality = Clamp(ind.Edu),
                Devastation = _war != null ? Clamp(_war.Devastation(prov.Id.Value)) : 0f
            };
            return ctx;
        }

        /// <summary>Hằng số chiều ngược (tham số tôi đặt, chưa hiệu chỉnh): QoL 1.0 → +30% năng suất, +0.1%/tuần dân; QoL 0 → −30%, −0.1%/tuần; tất cả nhân với tỷ trọng dân thành phố trong tỉnh.</summary>
        public const double MaxProductivityEffect = 0.3, MaxGrowthEffect = 0.001;

        /// <summary>Tỷ trọng thành phố trong tỉnh = dân thành phố (bản ghi thế giới) / dân tỉnh theo kinh tế.</summary>
        public double Weight(CityId city)
        {
            var c = _w.GetCity(city); int p = c.Province.Value; double pop = _eco.S.Pop[p];
            return pop <= 0 ? 0 : Math.Min(1.0, c.Population / pop);
        }

        /// <summary>Chiều ngược thành phố → quốc gia: gọi sau citySim.StepWeek() và trước eco.StepWeek() tuần sau. Thành phố có QoL cao làm tỉnh làm việc hiệu quả hơn và hút dân; QoL thấp thì ngược lại.
        /// Chỉ tác động theo tỷ trọng thật của thành phố trong tỉnh. Nhiều thành phố cùng tỉnh: gọi Report cho từng cái, hiệu ứng cộng dồn.</summary>
        public void Report(CitySim sim, CityId city) { Report(sim.Indicators.QoL, city); }
        public void Report(float qol, CityId city)
        {
            var c = _w.GetCity(city); int p = c.Province.Value; double wgt = Weight(city);
            double centred = 2.0 * Math.Max(0, Math.Min(1, qol)) - 1.0; // −1..+1
            _eco.CityLabourMod[p] += wgt * MaxProductivityEffect * centred;
            _eco.CityGrowthAdj[p] += wgt * MaxGrowthEffect * centred;
        }

        /// <summary>Xóa ảnh hưởng ngược (đầu mỗi tuần, trước khi các Report mới được cộng dồn).</summary>
        public void ResetReports() { for (int i = 0; i < _eco.CityLabourMod.Length; i++) { _eco.CityLabourMod[i] = 1; _eco.CityGrowthAdj[i] = 0; } }

        public void Apply(CitySim sim, CityId city) { sim.National = Build(city); }
        private static float Clamp(double v) { return v < 0 ? 0f : (v > 1 ? 1f : (float)v); }
    }
}
