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
        public void SetHuman(CountryId c, bool human) { if (human) { _human.Add(c.Value); _war.HumanCountries.Add(c.Value); } else { _human.Remove(c.Value); _war.HumanCountries.Remove(c.Value); } }
        /// <summary>Bật/tắt AI tự lập liên minh và hiệp ước (để so sánh trong thử nghiệm).</summary>
        public bool Treaties = true;

        public void Think()
        {
            var S = _war.Eco.S; var W = _war.W;
            if (Treaties && S.Week % 26 == 0) ThinkTreaties();
            if (S.Week % Interval != 0) return;
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
                    double ratio = _war.CoalitionPower(i) / Math.Max(1e-9, _war.CoalitionPower(n.Id.Value)); // đồng minh của mục tiêu răn đe
                    if (ratio >= 1.8 && ratio > best) { best = ratio; target = n; }
                }
                if (target != null) _war.DeclareWar(c.Id, target.Id);
            }
        }

        /// <summary>
        /// Mỗi 26 tuần: ký hiệp ước không xâm lược với láng giềng hiếu chiến mạnh hơn mình; lập liên minh với láng giềng mình mến
        /// (thiện cảm ≥ 30) — ưu tiên nước có chung kẻ thù/chung láng giềng hiếu chiến. Bên kia (AI) tự quyết theo AcceptsAlliance/AcceptsPact.
        /// </summary>
        private void ThinkTreaties()
        {
            var W = _war.W; var S = _war.Eco.S;
            foreach (var c in W.Countries)
            {
                int i = c.Id.Value; if (c.Eliminated || _human.Contains(i) || _war.AtWar(i)) continue;
                // 1) hiệp ước với kẻ hiếu chiến mạnh hơn
                foreach (var nid in c.Neighbors.ToList())
                {
                    var n = W.GetCountry(nid); int j = n.Id.Value; if (n.Eliminated || _human.Contains(j)) continue;
                    bool agg = n.Personality == Personality.Military || n.Personality == Personality.Expansionist;
                    if (agg && _war.Power(j) > 1.5 * _war.Power(i) && _war.ProposePact(c.Id, n.Id)) break;
                }
                // 2) liên minh với láng giềng mến nhất (chia sẻ mối đe dọa càng tốt)
                if (_war.Allies(i).Count >= 2) continue;
                Country best = null; double bestScore = 0;
                foreach (var nid in c.Neighbors)
                {
                    var n = W.GetCountry(nid); int j = n.Id.Value; if (n.Eliminated || _human.Contains(j) || _war.Allied(i, j) || _war.AtWar(i, j)) continue;
                    double score = _war.GetOpinion(i, j); if (score < 30) continue;
                    foreach (var t in W.Countries) if (t.Id != c.Id && t.Id != n.Id && !t.Eliminated && c.Neighbors.Contains(t.Id) && n.Neighbors.Contains(t.Id) && (t.Personality == Personality.Military || t.Personality == Personality.Expansionist)) score += 15;
                    if (score > bestScore) { bestScore = score; best = n; }
                }
                if (best != null) _war.ProposeAlliance(c.Id, best.Id);
            }
        }
    }
}
