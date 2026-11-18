using System;
using System.Linq;
using Vigo.Core;
using Vigo.Core.CityBuilder;
using Vigo.Core.Military;

namespace Vigo.Tests
{
    internal static class SessionTests
    {
        private static GameSession New(ulong seed, int player = 2) => GameSession.New(new WorldConfig { Seed = seed, CellCount = 20000 }, new CountryId(player));
        private static string Sig(GameSession s)
        {
            string c = string.Join(";", s.Cities.Select(x => x.Id.Value + ":" + x.Sim.Week + ":" + x.Sim.Indicators.Population + ":" + x.Sim.Indicators.QoL.ToString("R")));
            return MilitaryTests.Sig(s.Eco, s.War) + "|" + c + "|" + s.War.Offers.Count + "|" + s.Eco.S.Treasury[s.Player.Value].ToString("R");
        }
        private static void Prep(GameSession s)
        {
            var own = s.World.GetCountry(s.Player); var cid = s.World.GetProvince(own.CapitalProvince).Cities.FirstOrDefault();
            var sim = s.OpenCity(s.World.GetCountry(s.Player).Capital, 5); DemoScenario.BuildTown(sim);
        }

        public static void Register(Action<string, Action> add, Action<bool, string> assert)
        {
            add("Phiên chơi: chạy 3 năm với thành phố chi tiết, trạng thái hợp lý, lệnh thuế/ngân sách của người chơi có tác dụng", () =>
            {
                var s = New(91); Prep(s); s.StepWeeks(156);
                var st = s.Status(); Console.Write($" [tuần {st.Week}, dân {st.Population}, ngân khố {st.Treasury:0}, tỉnh {st.Provinces}, QoL thành phố {st.AvgCityQoL:0.00}]");
                assert(st.Week == 156 && !st.GameOver && st.Population > 0 && st.Cities == 1 && st.AvgCityQoL > 0.05, "trạng thái phiên chơi vô lý");
                var a = New(92); var b = New(92); a.SetTax(0.10); b.SetTax(0.50);
                a.StepWeeks(8); b.StepWeeks(8);
                assert(Math.Abs(a.Eco.S.TaxRate[a.Player.Value] - 0.10) < 0.05 && b.Eco.S.TaxRate[b.Player.Value] > 0.4, "thuế người chơi bị AI ghi đè");
                assert(b.Status().Treasury != a.Status().Treasury, "thuế khác nhau mà ngân khố giống hệt");
            });

            add("Phiên chơi: lưu giữa chừng → nạp → chạy tiếp giống hệt chạy liền (cả thành phố, chiến tranh, đề nghị)", () =>
            {
                var s = New(93); Prep(s); s.StepWeeks(120);
                var loaded = GameSession.FromBytes(s.ToBytes());
                assert(loaded.Player == s.Player && loaded.Cities.Count == 1, "mất nước người chơi/thành phố khi nạp");
                assert(Sig(loaded) == Sig(s), "ngay sau nạp đã lệch");
                s.StepWeeks(150); loaded.StepWeeks(150);
                assert(Sig(loaded) == Sig(s), "chạy tiếp sau nạp lệch so với chạy liền");
                string path = System.IO.Path.GetTempFileName(); try { s.Save(path); var again = GameSession.Load(path); assert(Sig(again) == Sig(s), "lưu file rồi nạp lệch"); } finally { System.IO.File.Delete(path); }
            });

            add("Phiên chơi: quay lại mốc tự động rồi chơi lại cho kết quả y hệt lần đầu", () =>
            {
                var s = New(94); Prep(s); s.AutosaveEvery = 13; s.StepWeeks(100); string at100 = Sig(s);
                assert(s.Rewind(40), "không có mốc để quay lại");
                int wk = s.Status().Week; assert(wk <= 40 && wk >= 26, "mốc quay lại sai tuần: " + wk);
                s.StepWeeks(100 - wk); assert(Sig(s) == at100, "chơi lại sau khi quay lại không giống lần đầu");
                assert(!s.Rewind(-5), "quay về tuần âm phải thất bại");
            });

            add("Phiên chơi: chỉ lệnh cho nước mình — không chấp nhận đề nghị của nước khác, không mở thành phố nước khác; mất nước thì dừng", () =>
            {
                var s = New(95); var other = s.World.Countries.First(c => c.Id != s.Player);
                var foreign = s.World.GetProvince(other.CapitalProvince).Cities.First();
                bool threw = false; try { s.OpenCity(foreign); } catch (InvalidOperationException) { threw = true; }
                assert(threw, "mở được thành phố của nước khác");
                var third = s.World.Countries.First(c => c.Id != s.Player && c.Id != other.Id);
                s.War.Delta[other.Id.Value, third.Id.Value] = 100;
                assert(s.AcceptOffer(12345) == false && s.DeclineOffer(12345) == false, "chấp nhận đề nghị không tồn tại");
                s.Diplomacy.SetHuman(third.Id, true); s.War.SendOffer(other.Id, third.Id, TreatyType.NonAggression);
                var o = s.War.Offers.FirstOrDefault(x => x.To == third.Id.Value);
                assert(o != null && !s.AcceptOffer(o.Id), "người chơi chấp nhận hộ đề nghị của người khác");
                s.World.GetCountry(s.Player).Eliminated = true;
                assert(s.GameOver && !s.StepWeek(), "mất nước mà vẫn chạy tiếp");
            });
        }
    }
}
