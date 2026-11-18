using System;
using System.Collections.Generic;
using System.Linq;
using Vigo.Core.Economy;

namespace Vigo.Core.Military
{
    public sealed class War
    {
        public int A, B;           // A là bên tuyên chiến
        public int StartWeek;
        /// <summary>Mục tiêu chiến tranh của bên tuyên chiến: số tỉnh cần chiếm (thủ đô tính 3). Đạt → hòa.</summary>
        public int Goal, Captured;
        /// <summary>Chiến tranh do đồng minh tham gia thuộc cuộc chiến chính (MasterA, MasterB); -1 nếu là chiến tranh chính.</summary>
        public int MasterA = -1, MasterB = -1;
    }

    public sealed class GameEvent
    {
        public int Week; public string Kind; public int A, B; public string Text;
    }

    /// <summary>
    /// Quân đội, ngoại giao và chiến tranh (GĐ4b). Chạy sau EconomySim.StepWeek.
    /// Quân số lấy từ ngân sách quốc phòng, hậu cần (nhiên liệu, máy móc) lấy từ tồn kho thật và ghi sổ cái;
    /// chiến sự phá hủy công suất tỉnh, giết dân, và chiếm tỉnh qua WorldOps.TransferProvince.
    /// </summary>
    public sealed partial class WarSim
    {
        public readonly WorldState W; public readonly EconomySim Eco;
        public readonly List<War> Wars = new List<War>();
        public readonly List<GameEvent> Events = new List<GameEvent>();
        public readonly Dictionary<long, int> LastPeace = new Dictionary<long, int>();
        /// <summary>Tuần kết thúc cuộc chiến gần nhất của mỗi nước (AI nghỉ sau chiến tranh).</summary>
        public readonly Dictionary<int, int> LastEnd = new Dictionary<int, int>();
        public double[] Progress; public int[] ProgressBy;   // tiến độ chiếm tỉnh theo kẻ tấn công
        /// <summary>Tuần chiếm gần nhất của mỗi tỉnh (-1 chưa từng); dùng để tính tàn phá cho thành phố.</summary>
        public int[] CapturedWeek;
        public double[] Supply;                              // hệ số hậu cần 0..1 tuần gần nhất
        public readonly List<int>[] ProvNeighbors; private readonly double[] _terrain;

        public const double TroopWeeklyCost = 4.0;   // tiền/tuần/lính
        public const double MaxArmyShare = 0.12;
        public const double FuelPerTroop = 0.05, MachineryPerTroop = 0.003;

        public WarSim(WorldState w, EconomySim eco)
        {
            W = w; Eco = eco; eco.BeforeTrade = RefreshTradePref; int NP = w.Provinces.Count, NC = w.Countries.Count;
            Progress = new double[NP]; ProgressBy = new int[NP]; CapturedWeek = new int[NP]; for (int i = 0; i < NP; i++) CapturedWeek[i] = -1; for (int i = 0; i < NP; i++) ProgressBy[i] = -1;
            Supply = new double[NC]; for (int i = 0; i < NC; i++) Supply[i] = 1;
            ProvNeighbors = new List<int>[NP]; for (int i = 0; i < NP; i++) ProvNeighbors[i] = new List<int>();
            var seen = new HashSet<long>();
            for (int c = 0; c < w.CellCount; c++)
            {
                int a = w.ProvinceOfCell[c]; if (a < 0) continue;
                foreach (int nb in w.Grid.Neighbors(c)) { int b = w.ProvinceOfCell[nb]; if (b < 0 || b == a) continue; if (seen.Add((long)a * 100000 + b)) ProvNeighbors[a].Add(b); }
            }
            InitDiplomacy();
            _terrain = new double[NP];
            for (int p = 0; p < NP; p++)
            {
                int high = 0; foreach (int c in w.Provinces[p].Cells) if (w.Elevation[c] >= w.HighlandElev) high++;
                _terrain[p] = 1 + 0.5 * high / Math.Max(1, w.Provinces[p].Cells.Length);
            }
        }

        private static long Key(int a, int b) => a < b ? a * 10000L + b : b * 10000L + a;
        public bool AtWar(int a, int b) => Wars.Any(x => (x.A == a && x.B == b) || (x.A == b && x.B == a));
        public bool AtWar(int a) => Wars.Any(x => x.A == a || x.B == a);
        public double Power(int c) { var S = Eco.S; return S.Troops[c] * (0.7 + 0.6 * S.Edu[c]) * Supply[c]; }
        /// <summary>Mức tàn phá chiến tranh của một tỉnh (0..1): vừa bị chiếm (giảm dần trong 52 tuần) hoặc đang là tiền tuyến/bị tấn công.</summary>
        public double Devastation(int province)
        {
            double d = 0; int age = Eco.S.Week - CapturedWeek[province];
            if (CapturedWeek[province] >= 0 && age < 52) d = 0.6 * (1 - age / 52.0);
            int owner = W.Provinces[province].Owner.Value;
            if (AtWar(owner) && ProvNeighbors[province].Any(n => AtWar(owner, W.Provinces[n].Owner.Value))) d = Math.Max(d, 0.3 + 0.7 * Math.Min(1, Progress[province]));
            return d;
        }
        public bool GameOver(CountryId human) => W.GetCountry(human).Eliminated;
        private void Log(string kind, int a, int b, string text) { Events.Add(new GameEvent { Week = Eco.S.Week, Kind = kind, A = a, B = b, Text = text }); if (Events.Count > 500) Events.RemoveAt(0); }

        // ================= Lệnh =================
        /// <summary>
        /// Hai nước phải giáp nhau, không đang chiến tranh, cả hai còn sống. Có hiệp ước/liên minh giữa hai nước thì chỉ được
        /// tuyên chiến khi breakTreaties = true (bội ước: mất uy tín với cả thế giới). Đồng minh của hai bên bị kêu gọi vào cuộc.
        /// </summary>
        public bool DeclareWar(CountryId a, CountryId b, bool breakTreaties = false)
        {
            int x = a.Value, y = b.Value; if (x == y || W.Countries[x].Eliminated || W.Countries[y].Eliminated || AtWar(x, y)) return false;
            if (!W.Countries[x].Neighbors.Contains(b)) return false;
            if (Allied(x, y) || HasPact(x, y))
            {
                if (!breakTreaties) return false;
                bool wasAlly = Allied(x, y); RemoveTreaties(x, y);
                AdjustOpinion(y, x, wasAlly ? -80 : -50);
                for (int c = 0; c < W.Countries.Count; c++) if (c != x && c != y) Delta[c, x] -= 20; // bội ước
                Log("betrayal", x, y, W.Countries[x].Name + " bội ước với " + W.Countries[y].Name);
            }
            Wars.Add(new War { A = x, B = y, StartWeek = Eco.S.Week, Goal = Math.Max(2, (int)Math.Ceiling(0.25 * W.Countries[y].Provinces.Count)) });
            Eco.SetTradeBlock(a, b, true);
            Log("war", x, y, W.Countries[x].Name + " tuyên chiến với " + W.Countries[y].Name);
            AdjustOpinion(y, x, -60);
            for (int c = 0; c < W.Countries.Count; c++) if (c != x && c != y && !Allied(c, x)) Delta[c, x] -= 8; // gây chiến làm mất thiện cảm chung
            CallAllies(x, y);
            return true;
        }

        public void MakePeace(CountryId a, CountryId b)
        {
            int x = a.Value, y = b.Value; var w = Wars.FirstOrDefault(q => (q.A == x && q.B == y) || (q.A == y && q.B == x)); if (w == null) return;
            Wars.Remove(w); Eco.SetTradeBlock(a, b, false); LastPeace[Key(x, y)] = Eco.S.Week; LastEnd[x] = Eco.S.Week; LastEnd[y] = Eco.S.Week;
            foreach (var jw in Wars.Where(q => q.MasterA == w.A && q.MasterB == w.B).ToList()) { Wars.Remove(jw); Eco.SetTradeBlock(new CountryId(jw.A), new CountryId(jw.B), false); LastEnd[jw.A] = Eco.S.Week; LastEnd[jw.B] = Eco.S.Week; }
            Log("peace", x, y, "Hòa bình giữa " + W.Countries[x].Name + " và " + W.Countries[y].Name);
        }

        /// <summary>Đề nghị hòa: bên AI chấp nhận nếu không đang áp đảo (≤1.5×) hoặc chiến tranh kéo dài > 52 tuần.</summary>
        public bool ProposePeace(CountryId from, CountryId to)
        {
            int x = from.Value, y = to.Value; var w = Wars.FirstOrDefault(q => (q.A == x && q.B == y) || (q.A == y && q.B == x)); if (w == null) return false;
            double ratio = Power(y) / Math.Max(1e-9, Power(x));
            if (ratio <= 1.5 || Eco.S.Week - w.StartWeek > 52) { MakePeace(from, to); return true; }
            return false;
        }

        // ================= Vòng tuần =================
        public void StepWeek()
        {
            int NC = W.Countries.Count; var S = Eco.S;
            Mobilize(NC); Upkeep(NC);
            Fight(); CheckPeace(); DiplomacyStep();
            for (int c = 0; c < NC; c++) if (W.Countries[c].Eliminated) { S.Troops[c] = 0; }
            Wars.RemoveAll(q => W.Countries[q.A].Eliminated || W.Countries[q.B].Eliminated);
        }

        private void Mobilize(int NC)
        {
            var S = Eco.S;
            for (int c = 0; c < NC; c++)
            {
                if (S.Dead[c]) continue;
                double pop = W.Countries[c].Population; double target = Math.Min(MaxArmyShare * pop, S.DefenseSpending[c] / TroopWeeklyCost);
                if (S.Troops[c] < target) S.Troops[c] = Math.Min(target, S.Troops[c] + 0.015 * pop); else S.Troops[c] = Math.Max(target, S.Troops[c] * 0.97);
            }
        }

        /// <summary>Nhiên liệu và máy móc cho quân đội lấy từ tồn kho quốc gia (ghi sổ cái); thiếu → hậu cần yếu.</summary>
        private void Upkeep(int NC)
        {
            var S = Eco.S; int fuel = (int)Good.Fuel, mach = (int)Good.Machinery;
            for (int c = 0; c < NC; c++)
            {
                if (S.Dead[c] || S.Troops[c] <= 0) { Supply[c] = 1; continue; }
                double nf = S.Troops[c] * FuelPerTroop, nm = S.Troops[c] * MachineryPerTroop;
                double gf = Math.Min(nf, S.Stock[c][fuel]), gm = Math.Min(nm, S.Stock[c][mach]);
                S.Stock[c][fuel] -= gf; S.Stock[c][mach] -= gm; S.Ledger.Military[fuel] += gf; S.Ledger.Military[mach] += gm;
                Supply[c] = 0.5 * (nf > 0 ? gf / nf : 1) + 0.5 * (nm > 0 ? gm / nm : 1);
            }
        }

        private void Fight()
        {
            var S = Eco.S; var captures = new List<KeyValuePair<int, int>>();
            var lossT = new double[W.Countries.Count]; var civLoss = new Dictionary<int, double>();
            foreach (var war in Wars.ToList())
            {
                foreach (var pair in new[] { new[] { war.A, war.B }, new[] { war.B, war.A } }) // [kẻ tấn công, kẻ phòng thủ]
                {
                    int at = pair[0], df = pair[1];
                    var fronts = new List<int>();
                    foreach (var pid in W.Countries[df].Provinces) { int p = pid.Value; if (ProvNeighbors[p].Any(n => W.Provinces[n].Owner.Value == at)) fronts.Add(p); }
                    if (fronts.Count == 0) continue;
                    var defThreat = new HashSet<int>(); foreach (var pid in W.Countries[df].Provinces) if (ProvNeighbors[pid.Value].Any(n => W.Provinces[n].Owner.Value == at)) defThreat.Add(pid.Value);
                    double supAt = Support(at, df), supDf = Support(df, at);
                    double fa = S.Troops[at] / fronts.Count / Math.Max(1, ActiveEnemies(at)), fd = S.Troops[df] / Math.Max(1, defThreat.Count) / Math.Max(1, ActiveEnemies(df));
                    foreach (int p in fronts)
                    {
                        double att = fa * (0.7 + 0.6 * S.Edu[at]) * Supply[at] * supAt;
                        double def = fd * (0.7 + 0.6 * S.Edu[df]) * Supply[df] * _terrain[p] * 1.15 * supDf;
                        if (att <= 1e-6) continue;
                        double tot = att + def;
                        lossT[at] += fa * 0.03 * 2 * def / tot; lossT[df] += fd * 0.03 * 2 * att / tot;
                        Eco.DamageProvince(W.Provinces[p].Id, 0.01);
                        civLoss[p] = (civLoss.TryGetValue(p, out var cl) ? cl : 0) + 0.004;
                        if (ProgressBy[p] != at) { ProgressBy[p] = at; Progress[p] = 0; }
                        Progress[p] = Math.Max(0, Progress[p] + Math.Max(-0.03, Math.Min(0.06, (att / Math.Max(1e-9, def) - 1) * 0.03)));
                        if (Progress[p] >= 1) captures.Add(new KeyValuePair<int, int>(p, at));
                    }
                }
            }
            for (int c = 0; c < lossT.Length; c++) { S.Troops[c] = Math.Max(0, S.Troops[c] - lossT[c]); if (lossT[c] > 0) { double f = Math.Min(0.5, lossT[c] / Math.Max(1, W.Countries[c].Population)); ScalePop(c, 1 - f); } }
            foreach (var kv in civLoss) S.Pop[kv.Key] *= 1 - kv.Value;
            foreach (var cap in captures)
            {
                int p = cap.Key, at = cap.Value; var prov = W.Provinces[p]; if (prov.Owner.Value == at) continue;
                int from = prov.Owner.Value; bool wasCapital = W.Countries[from].CapitalProvince.Value == p;
                if (WorldOps.TransferProvince(W, prov.Id, new CountryId(at)))
                {
                    Progress[p] = 0; ProgressBy[p] = -1; CapturedWeek[p] = Eco.S.Week;
                    Eco.DamageProvince(prov.Id, 0.4); Eco.S.Pop[p] *= 0.95; // hạ tầng bị tàn phá khi bị chiếm
                    var wr = Wars.FirstOrDefault(q => (q.A == at && q.B == from) || (q.A == from && q.B == at)); if (wr != null && wr.A == at) wr.Captured += wasCapital ? 3 : 1;
                    Log("capture", at, from, W.Countries[at].Name + " chiếm " + prov.Name + (wasCapital ? " (thủ đô)" : "") + " của " + W.Countries[from].Name);
                    if (W.Countries[from].Eliminated) Log("eliminated", at, from, W.Countries[from].Name + " bị xóa sổ");
                }
            }
        }

        private int ActiveEnemies(int c) => Wars.Count(q => q.A == c || q.B == c);
        private void ScalePop(int c, double k) { foreach (var pid in W.Countries[c].Provinces) Eco.S.Pop[pid.Value] *= k; }

        private void CheckPeace()
        {
            foreach (var war in Wars.ToList())
            {
                int dur = Eco.S.Week - war.StartWeek; if (dur < 26) continue;
                double pa = Power(war.A), pb = Power(war.B);
                bool exhaustedA = pa < 0.35 * pb, exhaustedB = pb < 0.35 * pa;
                if (war.Captured >= war.Goal) { Log("goal", war.A, war.B, W.Countries[war.A].Name + " đạt mục tiêu chiến tranh"); MakePeace(new CountryId(war.A), new CountryId(war.B)); continue; }
                if (exhaustedA || exhaustedB || dur > 260) MakePeace(new CountryId(war.A), new CountryId(war.B));
            }
        }
    }
}
