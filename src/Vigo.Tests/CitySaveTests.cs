using System;
using System.Collections.Generic;
using System.IO;
using Vigo.Core;
using Vigo.Core.CityBuilder;
using Vigo.Core.Economy;

namespace Vigo.Tests
{
    internal static class CitySaveTests
    {
        private static WorldState Gen(ulong seed) => WorldGenerator.Generate(new WorldConfig { Seed = seed, CellCount = 20000 });
        private static string Sig(CitySim s)
        {
            var i = s.Indicators;
            int alive = 0; foreach (var b in s.Buildings) if (b.Alive) alive++;
            return $"{s.Week}|{i.Population}|{i.Jobs}|{i.Employed}|{i.Buildings}|{i.QoL:R}|{i.Crime:R}|{i.UnemploymentRate:R}|{s.Nodes.Count}|{s.Edges.Count}|{s.Parcels.Count}|{s.Buildings.Count}|{alive}";
        }

        public static void Register(Action<string, Action> add, Action<bool, string> assert)
        {
            add("Lưu thành phố (đất phẳng): lưu giữa chừng → nạp → chạy tiếp cho kết quả y hệt chạy liền", () =>
            {
                var w = Gen(3); var eco = new EconomySim(w);
                var a = new CitySim(new FlatTerrain { WaterX0 = 400, WaterX1 = 430 }, 9); DemoScenario.BuildTown(a);
                a.StepWeeks(60);
                var data = SaveGame.ToBytes(w, eco, null, new List<CityEntry> { new CityEntry(w.Cities[0].Id, a) });
                SaveGame.FromBytes(data, out var w2, out var eco2, out var war2, out var cs);
                assert(cs.Count == 1 && cs[0].Id.Value == w.Cities[0].Id.Value, "không nạp lại được thành phố");
                var b = cs[0].Sim;
                assert(Sig(a) == Sig(b), "ngay sau nạp đã lệch: " + Sig(a) + " vs " + Sig(b));
                a.StepWeeks(120); b.StepWeeks(120);
                assert(Sig(a) == Sig(b), "chạy tiếp lệch: " + Sig(a) + " vs " + Sig(b));
                Console.Write($" [dân {a.Indicators.Population}, {a.Buildings.Count} công trình, {data.Length / 1024} KB]");
            });

            add("Lưu thành phố (địa hình thật + bối cảnh quốc gia áp lại): nạp rồi chạy tiếp y hệt", () =>
            {
                var w = Gen(11); var eco = new EconomySim(w); var cap = w.GetCity(w.Countries[0].Capital);
                var a = new CitySim(new WorldTerrain(w, cap.Id, 1000f), 5); DemoScenario.BuildTown(a);
                var ctx = new NationalContext { FoodSat = 0.6f, HealthQuality = 0.8f, EduQuality = 0.7f };
                a.National = ctx; a.StepWeeks(40);
                var data = SaveGame.ToBytes(w, eco, null, new List<CityEntry> { new CityEntry(cap.Id, a) });
                SaveGame.FromBytes(data, out var w2, out var eco2, out var war2, out var cs);
                var b = cs[0].Sim; b.National = ctx;
                a.StepWeeks(100); b.StepWeeks(100);
                assert(Sig(a) == Sig(b), "lệch sau nạp: " + Sig(a) + " vs " + Sig(b));
            });

            add("Lưu thành phố: người chơi xây thêm sau khi nạp vẫn hợp lệ (đường/lô đất/băm không gian được dựng lại)", () =>
            {
                var w = Gen(3); var eco = new EconomySim(w);
                var a = new CitySim(new FlatTerrain(), 4); DemoScenario.BuildTown(a); a.StepWeeks(30);
                SaveGame.FromBytes(SaveGame.ToBytes(w, eco, null, new List<CityEntry> { new CityEntry(w.Cities[0].Id, a) }), out var w2, out var e2, out var wr, out var cs);
                var b = cs[0].Sim;
                var ra = a.BuildRoad(100, 100, 100, 300, RoadType.Local); var rb = b.BuildRoad(100, 100, 100, 300, RoadType.Local);
                assert(ra.Ok == rb.Ok, "đặt đường cho kết quả khác nhau sau nạp");
                a.StepWeeks(40); b.StepWeeks(40);
                assert(Sig(a) == Sig(b), "lệch sau khi xây thêm: " + Sig(a) + " vs " + Sig(b));
            });

            add("Lưu thành phố: bản lưu cũ không có thành phố vẫn nạp được → danh sách rỗng; nhiều thành phố giữ đúng thứ tự", () =>
            {
                var w = Gen(3); var eco = new EconomySim(w);
                SaveGame.FromBytes(SaveGame.ToBytes(w, eco), out var w2, out var e2, out var wr, out var none);
                assert(none != null && none.Count == 0, "danh sách phải rỗng");
                var l = new List<CityEntry>();
                for (int k = 0; k < 3; k++) { var s = new CitySim(new FlatTerrain(), (ulong)(20 + k)); DemoScenario.BuildTown(s); s.StepWeeks(10 + k * 5); l.Add(new CityEntry(w.Cities[k].Id, s)); }
                SaveGame.FromBytes(SaveGame.ToBytes(w, eco, null, l), out w2, out e2, out wr, out var back);
                assert(back.Count == 3, "thiếu thành phố");
                for (int k = 0; k < 3; k++) assert(back[k].Id.Value == l[k].Id.Value && Sig(back[k].Sim) == Sig(l[k].Sim), "thành phố " + k + " lệch");
            });

            add("Lưu thành phố: dữ liệu hỏng/cắt cụt bị từ chối bằng SaveException, địa hình tự định nghĩa bị báo rõ", () =>
            {
                var w = Gen(3); var eco = new EconomySim(w);
                var s = new CitySim(new FlatTerrain(), 4); DemoScenario.BuildTown(s); s.StepWeeks(20);
                var data = SaveGame.ToBytes(w, eco, null, new List<CityEntry> { new CityEntry(w.Cities[0].Id, s) });
                var cut = new byte[data.Length / 2]; Array.Copy(data, cut, cut.Length);
                bool threw = false; try { SaveGame.FromBytes(cut, out var a1, out var a2, out var a3, out var a4); } catch (SaveGame.SaveException) { threw = true; }
                assert(threw, "bản cắt cụt không bị từ chối");
                var flip = (byte[])data.Clone(); flip[flip.Length / 2] ^= 0x55; threw = false;
                try { SaveGame.FromBytes(flip, out var a1, out var a2, out var a3, out var a4); } catch (SaveGame.SaveException) { threw = true; }
                assert(threw, "bản bị lật bit không bị từ chối");
                threw = false;
                try { SaveGame.ToBytes(w, eco, null, new List<CityEntry> { new CityEntry(w.Cities[0].Id, new CitySim(new CustomTerrain(), 1)) }); } catch (SaveGame.SaveException) { threw = true; }
                assert(threw, "địa hình tự định nghĩa phải bị báo không hỗ trợ");
            });

            add("Quay lại (RewindBuffer) giữ cả thành phố: quay về mốc cũ rồi chơi lại ra kết quả y hệt", () =>
            {
                var w = Gen(3); var eco = new EconomySim(w);
                var s = new CitySim(new FlatTerrain(), 4); DemoScenario.BuildTown(s);
                var rb = new RewindBuffer(5); var ents = new List<CityEntry> { new CityEntry(w.Cities[0].Id, s) };
                s.StepWeeks(20); eco.StepWeek(); rb.Push(w, eco, null, ents); int wk = eco.S.Week;
                s.StepWeeks(30); string forward = Sig(s);
                assert(rb.Rewind(wk, out var w2, out var e2, out var wr, out var cs), "không quay lại được");
                var s2 = cs[0].Sim; s2.StepWeeks(30);
                assert(Sig(s2) == forward, "chơi lại sau khi quay lại không giống: " + forward + " vs " + Sig(s2));
            });
        }

        private sealed class CustomTerrain : ITerrain
        {
            public bool IsWater(float x, float y) { return false; }
            public float Slope(float x, float y) { return 0f; }
        }
    }
}
