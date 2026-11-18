using System;
using System.Linq;

namespace Vigo.Core.Military
{
    /// <summary>AI tuyên chiến theo tính cách và so sánh sức mạnh; kiểm tra mỗi 13 tuần, tất định theo (seed, tuần, nước).</summary>
    public sealed class DiplomacyAI
    {
        private readonly WarSim _war; private readonly System.Collections.Generic.HashSet<int> _human = new System.Collections.Generic.HashSet<int>();
        public int Interval = 13; public double Aggression = 0.25; public int PeaceCooldown = 104, AggressorRest = 156;
        public DiplomacyAI(WarSim war) { _war = war; }
        public void SetHuman(CountryId c, bool human) { if (human) _human.Add(c.Value); else _human.Remove(c.Value); }

        public void Think()
        {
            var S = _war.Eco.S; var W = _war.W; if (S.Week % Interval != 0) return;
            foreach (var c in W.Countries)
            {
                int i = c.Id.Value;
                if (c.Eliminated || _human.Contains(i)) continue;
                if (c.Personality != Personality.Military && c.Personality != Personality.Expansionist) continue;
                if (_war.LastEnd.TryGetValue(i, out int le) && S.Week - le < AggressorRest) continue;
                if (_war.AtWar(i) || S.Troops[i] < 0.004 * c.Population) continue;
                var rng = Rng.Stream(W.Seed, "diplomacy", S.Week * 4096 + i);
                if (!rng.Chance(Aggression)) continue;
                // chọn láng giềng yếu nhất so với mình (≥1.8×), không vừa hòa gần đây
                Country target = null; double best = 0;
                foreach (var nid in c.Neighbors)
                {
                    var n = W.GetCountry(nid); if (n.Eliminated || _war.AtWar(n.Id.Value)) continue;
                    if (_war.LastPeace.TryGetValue(i < n.Id.Value ? i * 10000L + n.Id.Value : n.Id.Value * 10000L + i, out int lp) && S.Week - lp < PeaceCooldown) continue;
                    double ratio = _war.Power(i) / Math.Max(1e-9, _war.Power(n.Id.Value));
                    if (ratio >= 1.8 && ratio > best) { best = ratio; target = n; }
                }
                if (target != null) _war.DeclareWar(c.Id, target.Id);
            }
        }
    }
}
