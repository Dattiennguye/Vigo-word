using System;
using System.Collections.Generic;
using System.Linq;
using Vigo.Core.AI;
using Vigo.Core.CityBuilder;
using Vigo.Core.Economy;
using Vigo.Core.Military;

namespace Vigo.Core
{
    /// <summary>Ảnh chụp trạng thái cho giao diện (Unity chỉ đọc cấu trúc này, không đụng vào mô phỏng).</summary>
    public struct SessionStatus
    {
        public int Week; public bool GameOver; public string Country;
        public long Population; public double Treasury, TaxRate, FoodSat, Health, Edu; public int Provinces, Wars, Allies, PendingOffers, Cities;
        public double AvgCityQoL; // trung bình QoL các thành phố chi tiết đang mô phỏng (0 nếu chưa có)
    }

    /// <summary>
    /// Lớp lệnh/vòng lặp thống nhất mà Unity gọi: sở hữu thế giới, kinh tế, chiến tranh, AI, thành phố chi tiết và các mốc quay lại.
    /// Thứ tự mỗi tuần cố định: kinh tế → chiến tranh → AI ngân sách → AI ngoại giao → thành phố (nhận bối cảnh quốc gia, chạy, báo ngược).
    /// Mọi lệnh người chơi chỉ tác động tới nước của người chơi.
    /// </summary>
    public sealed class GameSession
    {
        public WorldState World { get; private set; }
        public EconomySim Eco { get; private set; }
        public WarSim War { get; private set; }
        public CountryAI Budget { get; private set; }
        public DiplomacyAI Diplomacy { get; private set; }
        public CityLink Link { get; private set; }
        public List<CityEntry> Cities { get; private set; } = new List<CityEntry>();
        public RewindBuffer History { get; private set; }
        public CountryId Player { get; private set; }
        /// <summary>Tự động lưu mốc quay lại mỗi N tuần (mặc định 13 ≈ một quý), giữ tối đa HistoryCapacity mốc.</summary>
        public int AutosaveEvery = 13;
        public const int HistoryCapacity = 40;

        private GameSession() { }

        public static GameSession New(WorldConfig cfg, CountryId player)
        {
            var w = WorldGenerator.Generate(cfg); var eco = new EconomySim(w); var war = new WarSim(w, eco);
            var s = new GameSession(); s.Attach(w, eco, war, new List<CityEntry>(), player); return s;
        }

        private void Attach(WorldState w, EconomySim eco, WarSim war, List<CityEntry> cities, CountryId player)
        {
            World = w; Eco = eco; War = war; Cities = cities; Player = player;
            Budget = new CountryAI(eco) { War = war }; Diplomacy = new DiplomacyAI(war); Link = new CityLink(w, eco, war);
            Budget.SetHuman(player, true); Diplomacy.SetHuman(player, true);
            if (History == null) History = new RewindBuffer(HistoryCapacity);
            // ảnh hưởng ngược của thành phố không nằm trong bản lưu: dựng lại từ QoL hiện tại để tuần kế tiếp y hệt chạy liền
            Link.ResetReports(); foreach (var c in Cities) Link.Report(c.Sim, c.Id);
        }

        public bool GameOver => World.GetCountry(Player).Eliminated;

        /// <summary>Chạy một tuần. Sau khi mất nước (GameOver) không chạy tiếp; trả về false.</summary>
        public bool StepWeek()
        {
            if (GameOver) return false;
            if (AutosaveEvery > 0 && Eco.S.Week % AutosaveEvery == 0) History.Push(World, Eco, War, Cities);
            Eco.StepWeek(); War.StepWeek(); Budget.Think(); Diplomacy.Think();
            Link.ResetReports();
            foreach (var c in Cities) { Link.Apply(c.Sim, c.Id); c.Sim.StepWeek(); Link.Report(c.Sim, c.Id); }
            return true;
        }
        public void StepWeeks(int n) { for (int i = 0; i < n && StepWeek(); i++) { } }

        // ---------------- Thành phố chi tiết ----------------
        /// <summary>Mở mô phỏng chi tiết cho một thành phố của người chơi (địa hình lấy từ thế giới thật).</summary>
        public CitySim OpenCity(CityId id, ulong seed = 1, float size = 1000f)
        {
            var existing = Cities.FirstOrDefault(c => c.Id == id); if (existing != null) return existing.Sim;
            var city = World.GetCity(id); if (World.GetProvince(city.Province).Owner != Player) throw new InvalidOperationException("chỉ mở thành phố của nước mình");
            var sim = new CitySim(new WorldTerrain(World, id, size), seed, size); Cities.Add(new CityEntry(id, sim)); return sim;
        }

        // ---------------- Lệnh người chơi (ngân sách) ----------------
        public void SetTax(double rate) { Eco.SetTaxRate(Player, rate); }
        public void SetBudget(double edu, double health, double invest, double defense) { Eco.SetBudget(Player, edu, health, invest, defense); }

        // ---------------- Lệnh người chơi (ngoại giao) ----------------
        public bool DeclareWar(CountryId target, bool breakTreaties = false) { return War.DeclareWar(Player, target, breakTreaties); }
        public bool ProposePeace(CountryId other) { return War.ProposePeace(Player, other); }
        public bool ProposeAlliance(CountryId other) { return War.ProposeAlliance(Player, other); }
        public bool ProposePact(CountryId other) { return War.ProposePact(Player, other); }
        public bool ProposeTrade(CountryId other) { return War.ProposeTrade(Player, other); }
        public void BreakAlliance(CountryId other) { War.BreakAlliance(Player, other); }
        public IReadOnlyList<Offer> Inbox() { return War.OffersFor(Player.Value).ToList(); }
        public bool AcceptOffer(int offerId) { var o = War.Offers.FirstOrDefault(x => x.Id == offerId); return o != null && o.To == Player.Value && War.AcceptOffer(offerId); }
        public bool DeclineOffer(int offerId) { var o = War.Offers.FirstOrDefault(x => x.Id == offerId); return o != null && o.To == Player.Value && War.DeclineOffer(offerId); }

        // ---------------- Lưu / nạp / quay lại ----------------
        public byte[] ToBytes() { return SaveGame.ToBytes(World, Eco, War, Cities); }
        public void Save(string path) { SaveGame.Save(path, World, Eco, War, Cities); }
        public static GameSession FromBytes(byte[] data)
        {
            SaveGame.FromBytes(data, out var w, out var eco, out var war, out var cities);
            if (war == null || war.HumanCountries.Count == 0) throw new SaveGame.SaveException("bản lưu không có nước người chơi");
            var s = new GameSession(); s.Attach(w, eco, war, cities, new CountryId(war.HumanCountries.Min())); return s;
        }
        public static GameSession Load(string path) { return FromBytes(System.IO.File.ReadAllBytes(path)); }

        /// <summary>Quay về mốc tự động gần nhất không muộn hơn tuần yêu cầu; các mốc muộn hơn bị bỏ. Trả về false nếu không có mốc phù hợp.</summary>
        public bool Rewind(int week)
        {
            if (!History.Rewind(week, out var w, out var eco, out var war, out var cities)) return false;
            Attach(w, eco, war, cities, Player); return true;
        }

        // ---------------- Cho giao diện ----------------
        public SessionStatus Status()
        {
            var c = World.GetCountry(Player); var ind = Eco.S.Indicators[Player.Value];
            return new SessionStatus
            {
                Week = Eco.S.Week, GameOver = c.Eliminated, Country = c.Name, Population = c.Population, Treasury = Eco.S.Treasury[Player.Value], TaxRate = Eco.S.TaxRate[Player.Value],
                FoodSat = ind.FoodSat, Health = ind.Health, Edu = ind.Edu, Provinces = c.Provinces.Count, Wars = War.Wars.Count(x => x.A == Player.Value || x.B == Player.Value),
                Allies = War.Allies(Player.Value).Count, PendingOffers = War.OffersFor(Player.Value).Count(), Cities = Cities.Count,
                AvgCityQoL = Cities.Count == 0 ? 0 : Cities.Average(x => x.Sim.Indicators.QoL)
            };
        }
    }
}
