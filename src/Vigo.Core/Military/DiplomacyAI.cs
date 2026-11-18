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
        /// <summary>Bật/tắt việc AI thắng thế tự áp điều kiện hòa đàm (nhượng tỉnh biên giới + bồi thường) qua WarSim.ProposeTerms.</summary>
        public bool Terms = true;

        public void Think()
        {
            var S = _war.Eco.S; var W = _war.W;
            _war.PruneOffers();
            if (Treaties && S.Week % 26 == 0) { ThinkTreaties(); ThinkOffers(); }
            if (Terms && S.Week % 13 == 6) ThinkTerms();
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
        /// Mỗi 13 tuần: bên AI áp đảo (sức mạnh ≥ 1,5× đối phương, chiến tranh đã ≥ 26 tuần) đòi các tỉnh biên giới của đối phương
        /// (không đòi thủ đô nếu tỷ lệ &lt; 3) với số tỉnh ≤ trần cho phép và bồi thường 50% trần. Không áp lên nước người chơi.
        /// </summary>
        private void ThinkTerms()
        {
            var W = _war.W; var S = _war.Eco.S;
            foreach (var war in _war.Wars.ToList())
            {
                if (!_war.Wars.Contains(war) || S.Week - war.StartWeek < 26) continue;
                int x = war.A, y = war.B;
                if (_war.Power(y) > _war.Power(x)) { int t = x; x = y; y = t; }
                if (_human.Contains(x) || _human.Contains(y) || W.Countries[x].Eliminated || W.Countries[y].Eliminated) continue;
                double ratio = _war.Power(x) / Math.Max(1e-9, _war.Power(y)); if (ratio < 1.5) continue;
                var L = W.Countries[y]; int maxCede = Math.Max(1, (int)Math.Floor(0.12 * (ratio - 1) * L.Provinces.Count));
                var border = L.Provinces.Where(p => _war.ProvNeighbors[p.Value].Any(n => W.Provinces[n].Owner.Value == x) && (p != L.CapitalProvince || ratio >= 3)).Take(Math.Min(maxCede, 3)).ToList();
                if (border.Count == 0 || border.Count >= L.Provinces.Count) continue;
                double cap = 10 * S.TaxRate[y] * _war.Eco.PotentialVA(y);
                _war.ProposeTerms(new CountryId(x), new CountryId(y), border, 0.5 * cap);
            }
        }

        /// <summary>
        /// Mỗi 26 tuần: AI gửi đề nghị tới người chơi. Liên minh: AI mến người chơi (thiện cảm ≥ 30), là láng giềng, AI chưa đủ 3 đồng minh;
        /// hiệp ước không xâm lược: AI hiếu chiến yếu hơn nhiều so với người chơi (sợ bị tấn công) hoặc thiện cảm ≥ 10. Mỗi AI tối đa 1 đề nghị chờ tới 1 người chơi.
        /// </summary>
        private void ThinkOffers()
        {
            var W = _war.W;
            foreach (int h in _human.OrderBy(v => v))
            {
                var hc = W.Countries[h]; if (hc.Eliminated) continue;
                foreach (var nid in hc.Neighbors)
                {
                    var c = W.GetCountry(nid); int i = c.Id.Value; if (c.Eliminated || _human.Contains(i) || _war.AtWar(i) ) continue;
                    if (_war.OffersFor(h).Any(o => o.From == i)) continue;
                    double op = _war.GetOpinion(i, h);
                    if (op >= 30 && _war.Allies(i).Count < 3 && _war.SendOffer(c.Id, hc.Id, TreatyType.Alliance)) continue;
                    bool scared = _war.Power(h) > 1.5 * _war.Power(i);
                    if ((scared || op >= 10) && op > -10) _war.SendOffer(c.Id, hc.Id, TreatyType.NonAggression);
                }
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
