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

        public void Apply(CitySim sim, CityId city) { sim.National = Build(city); }
        private static float Clamp(double v) { return v < 0 ? 0f : (v > 1 ? 1f : (float)v); }
    }
}
