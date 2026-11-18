using System;
using System.Collections.Generic;
using System.Linq;

namespace Vigo.Core.Military
{
    public enum TreatyType : byte { Alliance, NonAggression, Trade }

    public sealed class Treaty
    {
        public TreatyType Type; public int A, B, StartWeek, Expire; // Expire = -1: vô thời hạn
    }

    /// <summary>Quan hệ, hiệp ước, liên minh, kêu gọi đồng minh, hòa đàm có điều kiện (GĐ4c).</summary>
    public sealed partial class WarSim
    {
        public readonly List<Treaty> Treaties = new List<Treaty>();
        /// <summary>Thiện cảm động (i đối với j), phân rã dần về 0; thiện cảm = nền + động, kẹp ±100. Nền sinh tất định từ seed + tính cách.</summary>
        public double[,] Delta;
        private double[,] _base;
        public const int PactWeeks = 260;

        private void InitDiplomacy()
        {
            int NC = W.Countries.Count; Delta = new double[NC, NC]; _base = new double[NC, NC];
            for (int i = 0; i < NC; i++)
                for (int j = 0; j < NC; j++)
                {
                    if (i == j) continue;
                    var rng = Rng.Stream(W.Seed, "opinion", i * 1000 + j);
                    double v = (rng.NextDouble() - 0.5) * 40;
                    var pj = W.Countries[j].Personality;
                    if (pj == Personality.Trade || pj == Personality.Neutral || pj == Personality.Technology) v += 10;
                    if (pj == Personality.Military || pj == Personality.Expansionist) v -= 10;
                    _base[i, j] = v;
                }
        }

        public double GetOpinion(int i, int j) => i == j ? 100 : Math.Max(-100, Math.Min(100, _base[i, j] + Delta[i, j]));
        public void AdjustOpinion(int i, int j, double d) { Delta[i, j] += d; Delta[j, i] += d; }

        public bool Allied(int a, int b) => Treaties.Any(t => t.Type == TreatyType.Alliance && ((t.A == a && t.B == b) || (t.A == b && t.B == a)));
        public bool HasPact(int a, int b) => Treaties.Any(t => t.Type == TreatyType.NonAggression && ((t.A == a && t.B == b) || (t.A == b && t.B == a)));
        public bool HasTrade(int a, int b) => Treaties.Any(t => t.Type == TreatyType.Trade && ((t.A == a && t.B == b) || (t.A == b && t.B == a)));
        public List<int> TradePartners(int c) { var l = new List<int>(); foreach (var t in Treaties) if (t.Type == TreatyType.Trade) { if (t.A == c) l.Add(t.B); else if (t.B == c) l.Add(t.A); } return l; }
        public List<int> Allies(int c) { var l = new List<int>(); foreach (var t in Treaties) if (t.Type == TreatyType.Alliance) { if (t.A == c) l.Add(t.B); else if (t.B == c) l.Add(t.A); } return l; }
        private void RemoveTreaties(int a, int b) { Treaties.RemoveAll(t => (t.A == a && t.B == b) || (t.A == b && t.B == a)); }

        /// <summary>Sức mạnh cộng thêm từ đồng minh đang cùng đánh một kẻ thù, tối đa ×1.8 (đồng minh không mất lính; hạn chế ghi ở tài liệu).</summary>
        private double Support(int c, int enemy)
        {
            double extra = 0; foreach (int z in Allies(c)) if (z != enemy && AtWar(z, enemy)) extra += Power(z);
            return Math.Min(1.8, 1 + 0.3 * extra / Math.Max(1e-9, Power(c)));
        }
        /// <summary>Sức mạnh liên minh dùng cho đánh giá răn đe (AI nhìn cả đồng minh của đối thủ).</summary>
        public double CoalitionPower(int c) { double p = Power(c); foreach (int z in Allies(c)) p += 0.3 * Power(z); return p; }

        private bool Aggressive(int c) { var p = W.Countries[c].Personality; return p == Personality.Military || p == Personality.Expansionist; }
        public bool IsHumanControlled(int c) => HumanCountries.Contains(c);
        public readonly HashSet<int> HumanCountries = new HashSet<int>();

        // ---------- Đề nghị hiệp ước (bên nhận do AI quyết định; người chơi nhận thì luôn cần người chơi tự chấp nhận qua Accept*) ----------
        public bool AcceptsAlliance(int ai, int from)
        {
            if (ai == from || W.Countries[ai].Eliminated || W.Countries[from].Eliminated || Allied(ai, from) || AtWar(ai, from)) return false;
            if (GetOpinion(ai, from) < 25) return false;
            if (Allies(ai).Count >= 3) return false;
            // không liên minh với kẻ đang đánh đồng minh của mình
            foreach (int z in Allies(ai)) if (AtWar(from, z)) return false;
            if (Aggressive(ai) && Power(from) < 0.7 * Power(ai)) return false; // nước hiếu chiến chỉ kết với nước không quá yếu
            return true;
        }
        public bool AcceptsPact(int ai, int from)
        {
            if (ai == from || W.Countries[ai].Eliminated || W.Countries[from].Eliminated || HasPact(ai, from) || Allied(ai, from) || AtWar(ai, from)) return false;
            if (GetOpinion(ai, from) < -10) return false;
            if (Aggressive(ai) && Power(ai) > 1.8 * Power(from)) return false; // kẻ hiếu chiến đang thấy mồi ngon thì không ký
            return true;
        }

        public bool ProposeAlliance(CountryId from, CountryId to)
        {
            int f = from.Value, t = to.Value; if (!AcceptsAlliance(t, f)) return false;
            Treaties.Add(new Treaty { Type = TreatyType.Alliance, A = f, B = t, StartWeek = Eco.S.Week, Expire = -1 });
            AdjustOpinion(f, t, 20); Log("alliance", f, t, W.Countries[f].Name + " và " + W.Countries[t].Name + " lập liên minh"); return true;
        }

        public const int TradeWeeks = 260, MaxTradePartners = 4;
        public bool AcceptsTrade(int ai, int from)
        {
            if (ai == from || W.Countries[ai].Eliminated || W.Countries[from].Eliminated || HasTrade(ai, from) || AtWar(ai, from)) return false;
            return GetOpinion(ai, from) >= 0 && TradePartners(ai).Count < MaxTradePartners;
        }
        /// <summary>Hiệp ước thương mại: hai nước ưu tiên bán cho nhau khi hàng khan hiếm (xem EconomySim.TradePref). Không phải miễn thuế; hết hạn sau 260 tuần hoặc khi có chiến tranh.</summary>
        public bool ProposeTrade(CountryId from, CountryId to)
        {
            int f = from.Value, t = to.Value; if (!AcceptsTrade(t, f) || TradePartners(f).Count >= MaxTradePartners) return false;
            Treaties.Add(new Treaty { Type = TreatyType.Trade, A = f, B = t, StartWeek = Eco.S.Week, Expire = Eco.S.Week + TradeWeeks });
            AdjustOpinion(f, t, 5); Log("trade", f, t, W.Countries[f].Name + " và " + W.Countries[t].Name + " ký hiệp ước thương mại"); return true;
        }

        public bool ProposePact(CountryId from, CountryId to)
        {
            int f = from.Value, t = to.Value; if (!AcceptsPact(t, f)) return false;
            Treaties.Add(new Treaty { Type = TreatyType.NonAggression, A = f, B = t, StartWeek = Eco.S.Week, Expire = Eco.S.Week + PactWeeks });
            AdjustOpinion(f, t, 8); Log("pact", f, t, W.Countries[f].Name + " và " + W.Countries[t].Name + " ký hiệp ước không xâm lược"); return true;
        }

        public void BreakAlliance(CountryId a, CountryId b)
        {
            int x = a.Value, y = b.Value; if (!Allied(x, y)) return;
            RemoveTreaties(x, y); AdjustOpinion(y, x, -30); Delta[y, x] -= 0; Log("alliance_end", x, y, W.Countries[x].Name + " rút khỏi liên minh với " + W.Countries[y].Name);
        }

        // ---------- Kêu gọi đồng minh ----------
        private void CallAllies(int x, int y)
        {
            var S = Eco.S;
            foreach (int z in Allies(y)) // đồng minh của nước bị tấn công
            {
                if (z == x || AtWar(z, x) || Allied(z, x) || W.Countries[z].Eliminated || IsHumanControlled(z)) { if (IsHumanControlled(z) && !AtWar(z, x)) Log("call", y, z, W.Countries[y].Name + " kêu gọi " + W.Countries[z].Name + " vào cuộc"); continue; }
                bool dishonor = Power(x) > 3 * Power(z) && !Aggressive(z);
                if (dishonor) { AdjustOpinion(z, y, -20); Log("refuse", z, y, W.Countries[z].Name + " từ chối kêu gọi của " + W.Countries[y].Name); continue; }
                JoinWar(z, x, defender: true, masterA: x, masterB: y);
            }
            foreach (int z in Allies(x)) // đồng minh của kẻ tấn công: vào cuộc nếu không ưa mục tiêu
            {
                if (z == y || AtWar(z, y) || Allied(z, y) || W.Countries[z].Eliminated || IsHumanControlled(z)) continue;
                if (GetOpinion(z, y) >= 15) continue;
                if (!Rng.Stream(W.Seed, "callarms", S.Week * 4096 + z).Chance(0.6)) continue;
                JoinWar(z, y, defender: false, masterA: x, masterB: y);
            }
        }

        /// <summary>Đồng minh tham chiến (người chơi cũng dùng được khi được kêu gọi). Chiến tranh phụ gắn với cuộc chiến chính để cùng kết thúc.</summary>
        public bool JoinWar(int ally, int enemy, bool defender, int masterA, int masterB)
        {
            if (ally == enemy || AtWar(ally, enemy) || W.Countries[ally].Eliminated || W.Countries[enemy].Eliminated) return false;
            // chiến tranh phụ chỉ có nghĩa khi có thể giáp mặt hoặc đóng góp qua Support; luôn cho phép (đồng minh viễn chinh)
            Wars.Add(new War { A = defender ? enemy : ally, B = defender ? ally : enemy, StartWeek = Eco.S.Week, MasterA = masterA, MasterB = masterB,
                               Goal = Math.Max(1, (int)Math.Ceiling(0.15 * W.Countries[defender ? ally : enemy].Provinces.Count)) });
            Eco.SetTradeBlock(new CountryId(ally), new CountryId(enemy), true);
            Log("join", ally, enemy, W.Countries[ally].Name + " tham chiến chống " + W.Countries[enemy].Name);
            return true;
        }

        // ---------- Hòa đàm có điều kiện ----------
        /// <summary>
        /// Bên thắng đưa điều kiện: nhượng tỉnh + bồi thường tiền. Bên thua (AI) chấp nhận nếu bị áp đảo ≥1.3× và điều kiện không quá nặng:
        /// số tỉnh nhượng ≤ max(1, 12% × (tỉ lệ − 1) × số tỉnh), thủ đô chỉ khi tỉ lệ ≥ 3, bồi thường ≤ 10 tuần thuế tiềm năng.
        /// Tiền chuyển zero-sum giữa hai kho bạc.
        /// </summary>
        public bool ProposeTerms(CountryId winner, CountryId loser, IList<ProvinceId> cede, double reparations)
        {
            int x = winner.Value, y = loser.Value;
            var war = Wars.FirstOrDefault(q => (q.A == x && q.B == y) || (q.A == y && q.B == x)); if (war == null) return false;
            double ratio = Power(x) / Math.Max(1e-9, Power(y)); if (ratio < 1.3) return false;
            var L = W.Countries[y];
            if (cede.Any(p => W.GetProvince(p).Owner != loser)) return false;
            int maxCede = Math.Max(1, (int)Math.Floor(0.12 * (ratio - 1) * L.Provinces.Count));
            if (cede.Count > maxCede) return false;
            if (cede.Any(p => p == L.CapitalProvince) && ratio < 3) return false;
            if (cede.Count >= L.Provinces.Count) return false; // không xóa sổ qua hòa đàm
            double cap = 10 * Eco.S.TaxRate[y] * Eco.PotentialVA(y); if (reparations < 0 || reparations > cap) return false;
            foreach (var p in cede.ToList()) WorldOps.TransferProvince(W, p, winner);
            Eco.S.Treasury[y] -= reparations; Eco.S.Treasury[x] += reparations;
            Log("terms", x, y, W.Countries[x].Name + " nhận " + cede.Count + " tỉnh và " + (int)reparations + " tiền bồi thường từ " + L.Name);
            MakePeace(winner, loser); return true;
        }

        /// <summary>Đặt lại ưu tiên thương mại của kinh tế từ hiệp ước hiện có (gọi mỗi tuần và sau khi nạp bản lưu).</summary>
        public void RefreshTradePref()
        {
            int NC = W.Countries.Count; var pref = Eco.TradePref; for (int i = 0; i < NC; i++) for (int j = 0; j < NC; j++) pref[i, j] = 1;
            foreach (var t in Treaties) { double m = t.Type == TreatyType.Trade ? 3 : t.Type == TreatyType.Alliance ? 2 : 1; if (m > pref[t.A, t.B]) { pref[t.A, t.B] = m; pref[t.B, t.A] = m; } }
        }

        // ---------- Vòng tuần của ngoại giao ----------
        private void DiplomacyStep()
        {
            int NC = W.Countries.Count; var S = Eco.S;
            for (int i = 0; i < NC; i++) for (int j = 0; j < NC; j++) Delta[i, j] *= 0.995;
            Treaties.RemoveAll(t => (t.Expire >= 0 && S.Week >= t.Expire) || W.Countries[t.A].Eliminated || W.Countries[t.B].Eliminated);
            Treaties.RemoveAll(t => t.Type == TreatyType.Trade && AtWar(t.A, t.B));
            // ưu tiên thương mại của nền kinh tế: hiệp ước thương mại ×3, liên minh ×2
RefreshTradePref();
            if (S.Week % 4 != 0) return;
            // láng giềng hòa bình quen biết lâu dần thân thiện (trừ khi đối phương hiếu chiến); cân bằng ≈ +25 nhờ phân rã
            foreach (var c in W.Countries)
            {
                if (c.Eliminated) continue;
                foreach (var nid in c.Neighbors) { int j = nid.Value; if (W.Countries[j].Eliminated || Aggressive(j) || AtWar(c.Id.Value, j)) continue; Delta[c.Id.Value, j] += 0.5; }
            }
            // cùng đánh một kẻ thù → mến nhau; cùng bị một bên đánh
            var enemies = new List<int>[NC]; for (int i = 0; i < NC; i++) enemies[i] = new List<int>();
            foreach (var w in Wars) { enemies[w.A].Add(w.B); enemies[w.B].Add(w.A); }
            for (int i = 0; i < NC; i++) for (int j = i + 1; j < NC; j++) if (enemies[i].Intersect(enemies[j]).Any()) AdjustOpinion(i, j, 1.2);
            // liên minh mà hai bên đánh nhau → tan vỡ
            foreach (var t in Treaties.Where(t => t.Type == TreatyType.Alliance && AtWar(t.A, t.B)).ToList()) { Treaties.Remove(t); Log("alliance_end", t.A, t.B, "Liên minh " + W.Countries[t.A].Name + "–" + W.Countries[t.B].Name + " tan vỡ vì chiến tranh"); }
        }
    }
}
