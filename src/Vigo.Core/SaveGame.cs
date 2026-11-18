using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Vigo.Core.Economy;
using Vigo.Core.Military;

namespace Vigo.Core
{
    /// <summary>
    /// Lưu / nạp / quay lại (TDD 12.4). Thế giới sinh tất định theo seed nên bản lưu chỉ chứa:
    /// cấu hình + hash gốc, phần thế giới đã đổi (chủ tỉnh, dân, thủ đô, danh sách tỉnh) và toàn bộ trạng thái kinh tế.
    /// Nạp = sinh lại thế giới từ seed (≈0.5–1 s), kiểm hash gốc, phủ lớp thay đổi. Bản lưu có phiên bản và tổng kiểm.
    /// </summary>
    public static class SaveGame
    {
        public const int Version = 2;
        private const uint Magic = 0x4F474956; // "VIGO"

        public sealed class SaveException : Exception { public SaveException(string m) : base(m) { } }

        public static byte[] ToBytes(WorldState w, EconomySim eco, WarSim war = null)
        {
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, CompressionLevel.Optimal, true))
                using (var bw = new BinaryWriter(gz))
                {
                    var body = new MemoryStream(); var b = new BinaryWriter(body);
                    WriteBody(b, w, eco, war); b.Flush();
                    var bytes = body.ToArray();
                    bw.Write(Magic); bw.Write(Version); bw.Write(Checksum(bytes)); bw.Write(bytes.Length); bw.Write(bytes);
                }
                return ms.ToArray();
            }
        }

        public static void Save(string path, WorldState w, EconomySim eco, WarSim war = null)
        {
            var tmp = path + ".tmp"; File.WriteAllBytes(tmp, ToBytes(w, eco, war));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path); // ghi tạm rồi đổi tên: mất điện giữa chừng không làm hỏng bản cũ
        }

        public static void Load(string path, out WorldState w, out EconomySim eco) { FromBytes(File.ReadAllBytes(path), out w, out eco, out var _war); }
        public static void Load(string path, out WorldState w, out EconomySim eco, out WarSim war) { FromBytes(File.ReadAllBytes(path), out w, out eco, out war); }

        public static void FromBytes(byte[] data, out WorldState w, out EconomySim eco) { FromBytes(data, out w, out eco, out var _war); }
        public static void FromBytes(byte[] data, out WorldState w, out EconomySim eco, out WarSim war)
        {
            byte[] body;
            try
            {
                using (var gz = new GZipStream(new MemoryStream(data), CompressionMode.Decompress))
                using (var br = new BinaryReader(gz))
                {
                    if (br.ReadUInt32() != Magic) throw new SaveException("không phải bản lưu VIGO");
                    int ver = br.ReadInt32(); if (ver != Version) throw new SaveException("phiên bản bản lưu " + ver + " không được hỗ trợ (hiện tại " + Version + ")");
                    ulong sum = br.ReadUInt64(); int len = br.ReadInt32();
                    body = br.ReadBytes(len);
                    if (body.Length != len || Checksum(body) != sum) throw new SaveException("bản lưu bị hỏng (sai tổng kiểm)");
                }
            }
            catch (InvalidDataException) { throw new SaveException("bản lưu bị hỏng (không giải nén được)"); }
            catch (EndOfStreamException) { throw new SaveException("bản lưu bị cắt cụt"); }
            ReadBody(new BinaryReader(new MemoryStream(body)), out w, out eco, out war);
        }

        private static ulong Checksum(byte[] d) { ulong h = 14695981039346656037UL; unchecked { for (int i = 0; i < d.Length; i++) { h ^= d[i]; h *= 1099511628211UL; } } return h; }

        // ---------------- ghi ----------------
        private static void WriteBody(BinaryWriter b, WorldState w, EconomySim eco, WarSim war)
        {
            var c = w.Config;
            b.Write(c.Seed); b.Write(c.MapWidth); b.Write(c.MapHeight); b.Write(c.CellCount); b.Write(c.CountryCount);
            b.Write(c.MinProvincesPerCountry); b.Write(c.MaxProvincesPerCountry); b.Write(c.MinCitiesPerProvince); b.Write(c.MaxCitiesPerProvince);
            b.Write(c.LandRatio); b.Write(c.MountainFactor); b.Write(c.ResourceRichness); b.Write(c.MinCellsPerProvince); b.Write(c.MaxAttempts);
            b.Write(w.GenesisHash);
            b.Write(w.Provinces.Count); foreach (var p in w.Provinces) { b.Write(p.Owner.Value); b.Write(p.Population); }
            b.Write(w.Countries.Count);
            foreach (var k in w.Countries)
            {
                b.Write(k.Population); b.Write(k.Capital.Value); b.Write(k.CapitalProvince.Value); b.Write(k.Eliminated);
                b.Write(k.LandCells); b.Write(k.Provinces.Count); foreach (var p in k.Provinces) b.Write(p.Value);
            }
            var S = eco.S;
            b.Write(S.Week);
            Arr(b, S.Pop); Arr(b, S.FarmCap); Arr(b, S.ForestCap); Arr(b, S.MineBase); Arr(b, S.MineBonus); Arr(b, S.Util);
            b.Write(S.IndCap.Length); foreach (var a in S.IndCap) Arr(b, a);
            Arr(b, S.DepRate); Arr(b, S.DepRemaining); b.Write(S.DepProvince.Length); foreach (int v in S.DepProvince) b.Write(v);
            b.Write(S.Stock.Length);
            for (int i = 0; i < S.Stock.Length; i++)
            {
                Arr(b, S.Stock[i]); Arr(b, S.Price[i]);
                b.Write(S.InvestWeight[i] != null); if (S.InvestWeight[i] != null) Arr(b, S.InvestWeight[i]);
                b.Write(S.Dead[i]);
                var ind = S.Indicators[i];
                b.Write(ind.Population); b.Write(ind.ValueAdded); b.Write(ind.TaxRevenue); b.Write(ind.Treasury); b.Write(ind.FoodSat); b.Write(ind.FuelSat);
                b.Write(ind.GoodsSat); b.Write(ind.Edu); b.Write(ind.Health); b.Write(ind.LabourUtil); b.Write(ind.PriceIndex);
            }
            Arr(b, S.Treasury); Arr(b, S.TaxRate); Arr(b, S.ShareEdu); Arr(b, S.ShareHealth); Arr(b, S.ShareInvest); Arr(b, S.ShareDefense);
            Arr(b, S.Edu); Arr(b, S.Health); Arr(b, S.DefenseSpending); Arr(b, S.Troops);
            b.Write(S.Blocked.Count); var keys = new List<long>(S.Blocked); keys.Sort(); foreach (long k in keys) b.Write(k);
            var L = S.Ledger; Arr(b, L.Initial); Arr(b, L.Produced); Arr(b, L.Household); Arr(b, L.Industry); Arr(b, L.Invested); Arr(b, L.Military); Arr(b, L.Lost); Arr(b, L.TradeVolume); b.Write(L.TradeMoneyError);
            b.Write(war != null);
            if (war != null)
            {
                b.Write(war.Wars.Count); foreach (var q in war.Wars) { b.Write(q.A); b.Write(q.B); b.Write(q.StartWeek); b.Write(q.Goal); b.Write(q.Captured); }
                Arr(b, war.Progress); b.Write(war.ProgressBy.Length); foreach (int v in war.ProgressBy) b.Write(v); Arr(b, war.Supply);
                var lp = new List<KeyValuePair<long, int>>(war.LastPeace); lp.Sort((x, y) => x.Key.CompareTo(y.Key)); b.Write(lp.Count); foreach (var kv in lp) { b.Write(kv.Key); b.Write(kv.Value); }
                var le = new List<KeyValuePair<int, int>>(war.LastEnd); le.Sort((x, y) => x.Key.CompareTo(y.Key)); b.Write(le.Count); foreach (var kv in le) { b.Write(kv.Key); b.Write(kv.Value); }
                b.Write(war.Events.Count); foreach (var ev in war.Events) { b.Write(ev.Week); b.Write(ev.Kind); b.Write(ev.A); b.Write(ev.B); b.Write(ev.Text); }
            }
        }

        private static void Arr(BinaryWriter b, double[] a) { b.Write(a.Length); foreach (double v in a) b.Write(v); }
        private static double[] ArrR(BinaryReader r) { int n = r.ReadInt32(); if (n < 0 || n > 50_000_000) throw new SaveException("độ dài mảng không hợp lệ"); var a = new double[n]; for (int i = 0; i < n; i++) a[i] = r.ReadDouble(); return a; }

        // ---------------- đọc ----------------
        private static void ReadBody(BinaryReader r, out WorldState w, out EconomySim eco, out WarSim war)
        {
            var c = new WorldConfig
            {
                Seed = r.ReadUInt64(), MapWidth = r.ReadSingle(), MapHeight = r.ReadSingle(), CellCount = r.ReadInt32(), CountryCount = r.ReadInt32(),
                MinProvincesPerCountry = r.ReadInt32(), MaxProvincesPerCountry = r.ReadInt32(), MinCitiesPerProvince = r.ReadInt32(), MaxCitiesPerProvince = r.ReadInt32(),
                LandRatio = r.ReadSingle(), MountainFactor = r.ReadSingle(), ResourceRichness = r.ReadSingle(), MinCellsPerProvince = r.ReadInt32(), MaxAttempts = r.ReadInt32()
            };
            ulong genesis = r.ReadUInt64();
            w = WorldGenerator.Generate(c);
            if (w.GenesisHash != genesis) throw new SaveException("thế giới sinh lại không khớp bản lưu (khác phiên bản bộ sinh hoặc nền tảng)");
            int np = r.ReadInt32(); if (np != w.Provinces.Count) throw new SaveException("số tỉnh không khớp");
            var owners = new int[np];
            for (int i = 0; i < np; i++) { owners[i] = r.ReadInt32(); w.Provinces[i].Population = r.ReadInt64(); }
            int nc = r.ReadInt32(); if (nc != w.Countries.Count) throw new SaveException("số quốc gia không khớp");
            foreach (var cn in w.Countries) cn.Provinces.Clear();
            foreach (var city in w.Cities) city.IsCapital = false;
            for (int i = 0; i < nc; i++)
            {
                var k = w.Countries[i];
                k.Population = r.ReadInt64(); k.Capital = new CityId(r.ReadInt32()); k.CapitalProvince = new ProvinceId(r.ReadInt32()); k.Eliminated = r.ReadBoolean();
                k.LandCells = r.ReadInt32(); int n = r.ReadInt32();
                for (int j = 0; j < n; j++) k.Provinces.Add(new ProvinceId(r.ReadInt32()));
                if (!k.Capital.IsNone) w.GetCity(k.Capital).IsCapital = true;
            }
            for (int i = 0; i < np; i++)
            {
                var p = w.Provinces[i]; p.Owner = new CountryId(owners[i]);
                foreach (int cell in p.Cells) w.CountryOfCell[cell] = owners[i];
            }
            WorldOps.RecomputeDerived(w);

            var S = new EconomyState();
            S.Week = r.ReadInt32();
            S.Pop = ArrR(r); S.FarmCap = ArrR(r); S.ForestCap = ArrR(r); S.MineBase = ArrR(r); S.MineBonus = ArrR(r); S.Util = ArrR(r);
            int ni = r.ReadInt32(); S.IndCap = new double[ni][]; for (int i = 0; i < ni; i++) S.IndCap[i] = ArrR(r);
            S.DepRate = ArrR(r); S.DepRemaining = ArrR(r); int nd = r.ReadInt32(); S.DepProvince = new int[nd]; for (int i = 0; i < nd; i++) S.DepProvince[i] = r.ReadInt32();
            if (S.Pop.Length != np || nd != w.Deposits.Count) throw new SaveException("kích thước dữ liệu kinh tế không khớp thế giới");
            S.DepsOfProvince = new List<int>[np]; for (int p = 0; p < np; p++) S.DepsOfProvince[p] = new List<int>();
            for (int d = 0; d < nd; d++) if (S.DepProvince[d] >= 0) S.DepsOfProvince[S.DepProvince[d]].Add(d);
            int ns = r.ReadInt32(); if (ns != nc) throw new SaveException("số quốc gia kinh tế không khớp");
            S.Stock = new double[ns][]; S.Price = new double[ns][]; S.InvestWeight = new double[ns][]; S.Dead = new bool[ns]; S.Indicators = new CountryIndicators[ns];
            for (int i = 0; i < ns; i++)
            {
                S.Stock[i] = ArrR(r); S.Price[i] = ArrR(r); if (r.ReadBoolean()) S.InvestWeight[i] = ArrR(r); S.Dead[i] = r.ReadBoolean();
                S.Indicators[i] = new CountryIndicators
                {
                    Population = r.ReadDouble(), ValueAdded = r.ReadDouble(), TaxRevenue = r.ReadDouble(), Treasury = r.ReadDouble(), FoodSat = r.ReadDouble(), FuelSat = r.ReadDouble(),
                    GoodsSat = r.ReadDouble(), Edu = r.ReadDouble(), Health = r.ReadDouble(), LabourUtil = r.ReadDouble(), PriceIndex = r.ReadDouble()
                };
            }
            S.Treasury = ArrR(r); S.TaxRate = ArrR(r); S.ShareEdu = ArrR(r); S.ShareHealth = ArrR(r); S.ShareInvest = ArrR(r); S.ShareDefense = ArrR(r);
            S.Edu = ArrR(r); S.Health = ArrR(r); S.DefenseSpending = ArrR(r); S.Troops = ArrR(r);
            int nb = r.ReadInt32(); for (int i = 0; i < nb; i++) S.Blocked.Add(r.ReadInt64());
            var L = S.Ledger; L.Initial = ArrR(r); L.Produced = ArrR(r); L.Household = ArrR(r); L.Industry = ArrR(r); L.Invested = ArrR(r); L.Military = ArrR(r); L.Lost = ArrR(r); L.TradeVolume = ArrR(r); L.TradeMoneyError = r.ReadDouble();
            eco = new EconomySim(w, S);
            war = null;
            if (r.ReadBoolean())
            {
                war = new WarSim(w, eco);
                int nw = r.ReadInt32(); for (int i = 0; i < nw; i++) war.Wars.Add(new War { A = r.ReadInt32(), B = r.ReadInt32(), StartWeek = r.ReadInt32(), Goal = r.ReadInt32(), Captured = r.ReadInt32() });
                war.Progress = ArrR(r); int npb = r.ReadInt32(); war.ProgressBy = new int[npb]; for (int i = 0; i < npb; i++) war.ProgressBy[i] = r.ReadInt32(); war.Supply = ArrR(r);
                int nl = r.ReadInt32(); for (int i = 0; i < nl; i++) war.LastPeace[r.ReadInt64()] = r.ReadInt32();
                int nle = r.ReadInt32(); for (int i = 0; i < nle; i++) war.LastEnd[r.ReadInt32()] = r.ReadInt32();
                int ne = r.ReadInt32(); for (int i = 0; i < ne; i++) war.Events.Add(new GameEvent { Week = r.ReadInt32(), Kind = r.ReadString(), A = r.ReadInt32(), B = r.ReadInt32(), Text = r.ReadString() });
            }
        }
    }

    /// <summary>Vòng bản lưu tự động trong bộ nhớ để "quay lại" khi chọn sai (đĩa: ghi các mốc ra file bằng SaveGame.Save).</summary>
    public sealed class RewindBuffer
    {
        private readonly int _capacity; private readonly LinkedList<KeyValuePair<int, byte[]>> _snaps = new LinkedList<KeyValuePair<int, byte[]>>();
        public RewindBuffer(int capacity) { _capacity = Math.Max(1, capacity); }
        public int Count => _snaps.Count;
        public void Push(WorldState w, EconomySim eco, WarSim war = null) { _snaps.AddLast(new KeyValuePair<int, byte[]>(eco.S.Week, SaveGame.ToBytes(w, eco, war))); while (_snaps.Count > _capacity) _snaps.RemoveFirst(); }
        public int[] Weeks() { var l = new List<int>(); foreach (var kv in _snaps) l.Add(kv.Key); return l.ToArray(); }
        /// <summary>Quay về mốc gần nhất không muộn hơn tuần yêu cầu; các mốc muộn hơn bị bỏ (nhánh lịch sử mới).</summary>
        public bool Rewind(int week, out WorldState w, out EconomySim eco) { return Rewind(week, out w, out eco, out var _war); }
        public bool Rewind(int week, out WorldState w, out EconomySim eco, out WarSim war)
        {
            w = null; eco = null; war = null; var node = _snaps.Last;
            while (node != null && node.Value.Key > week) node = node.Previous;
            if (node == null) return false;
            while (_snaps.Last != node) _snaps.RemoveLast();
            SaveGame.FromBytes(node.Value.Value, out w, out eco, out war); return true;
        }
    }
}
