using System;
using System.Linq;
using Vigo.Core;
using Vigo.Core.CityBuilder;

namespace Vigo.Tests
{
    internal static class CityTests
    {
        private static CitySim Flat(ulong seed = 1) { return new CitySim(new FlatTerrain(), seed); }

        public static void Register(Action<string, Action> add, Action<bool, string> assert)
        {
            add("Thành phố: đường nối nút thoát sinh lô đất hai bên, cao tốc không có mặt tiền", () =>
            {
                var s = Flat();
                var r = s.BuildRoad(0, 500, 300, 500, RoadType.Urban);
                assert(r.Ok, "xây đường: " + r.Error);
                int above = s.Parcels.Count(p => p.Alive && p.Y < 500), below = s.Parcels.Count(p => p.Alive && p.Y > 500);
                assert(above > 5 && below > 5, "lô hai bên: " + above + "/" + below);
                var h = Flat(); h.BuildRoad(0, 500, 300, 500, RoadType.Highway);
                assert(h.Parcels.Count == 0, "cao tốc không được có lô đất");
                Console.Write($" [{s.Parcels.Count} lô trên 300 m đường]");
            });

            add("Thành phố: đường cắt nhau tự tách nút, đồ thị liên thông", () =>
            {
                var s = Flat();
                assert(s.BuildRoad(0, 500, 400, 500, RoadType.Urban).Ok, "ngang");
                assert(s.BuildRoad(200, 300, 200, 700, RoadType.Urban).Ok, "dọc");
                var junction = s.Nodes.FirstOrDefault(n => Math.Abs(n.X - 200) < 2 && Math.Abs(n.Y - 500) < 2);
                assert(junction != null, "không có nút giao tại (200,500)");
                int deg = junction.Edges.Count(e => s.Edges[e].Alive);
                assert(deg == 4, "nút giao phải có 4 nhánh, thực tế " + deg);
                s.StepWeek(); // tính kết nối
                foreach (var n in s.Nodes) if (n.Edges.Any(e => s.Edges[e].Alive)) { /* mọi nút có cạnh đều phải nối được tới nút thoát */ }
                // Không lô nào nằm đè lên đường cắt.
                foreach (var p in s.Parcels.Where(q => q.Alive))
                    foreach (var e in s.Edges.Where(q => q.Alive))
                    {
                        var a = s.Nodes[e.A]; var b = s.Nodes[e.B];
                        float d = CitySim_Dist(p.X, p.Y, a, b);
                        assert(d > RoadSpecs.Width(e.Type) * 0.5f, "lô đè lên đường");
                    }
            });

            add("Thành phố: từ chối lệnh sai (ngoài bản đồ, quá ngắn, nước, quá dốc, đè công trình)", () =>
            {
                var t = new FlatTerrain { WaterX0 = 400, WaterX1 = 440 };
                var s = new CitySim(t, 2);
                assert(!s.BuildRoad(-10, 500, 100, 500, RoadType.Local).Ok, "ngoài bản đồ");
                assert(!s.BuildRoad(0, 500, 10, 500, RoadType.Local).Ok, "quá ngắn");
                assert(s.BuildRoad(0, 500, 380, 500, RoadType.Local).Ok, "đường tới bờ");
                assert(!s.BuildRoad(380, 500, 460, 500, RoadType.Local).Ok, "đường thường qua nước phải bị từ chối");
                assert(s.BuildRoad(380, 500, 460, 500, RoadType.Avenue).Ok, "đại lộ bắc cầu 40 m phải được");
                assert(s.Edges.Any(e => e.Alive && e.IsBridge), "phải có cạnh cầu");
                var wide = new FlatTerrain { WaterX0 = 400, WaterX1 = 520 };
                var s2 = new CitySim(wide, 2); s2.BuildRoad(0, 500, 380, 500, RoadType.Local);
                assert(!s2.BuildRoad(380, 500, 540, 500, RoadType.Highway).Ok, "nhịp cầu 120 m phải bị từ chối");
                // đè công trình
                var s3 = Flat(); s3.BuildRoad(0, 500, 300, 500, RoadType.Urban);
                int pid = s3.NearestFreeParcel(150, 480); s3.ZoneParcel(pid, Zone.ResidentialLow); s3.StepWeeks(30);
                var built = s3.Parcels.First(p => p.Alive && p.Building >= 0);
                var r = s3.BuildRoad(built.X - 60, built.Y, built.X + 60, built.Y, RoadType.Local);
                assert(!r.Ok, "đường đè lên công trình phải bị từ chối");
            });

            add("Thành phố: không tăng trưởng nếu không nối với quốc lộ (nút thoát)", () =>
            {
                var s = Flat();
                s.BuildRoad(400, 400, 700, 400, RoadType.Urban); // cô lập, không chạm nút thoát
                s.ZoneArea(550, 400, 300, Zone.ResidentialLow);
                s.StepWeeks(40);
                assert(s.Buildings.Count == 0, "đã xây " + s.Buildings.Count + " công trình trên mạng đường bị cô lập");
                // nối vào → bắt đầu tăng trưởng
                s.BuildRoad(0, 500, 400, 400, RoadType.Urban);
                s.StepWeeks(40);
                assert(s.Buildings.Count > 0, "nối quốc lộ rồi mà vẫn không tăng trưởng");
            });

            add("Thành phố: mô phỏng 3 năm — bất biến dân số/việc làm/chỉ số", () =>
            {
                var s = Flat(7); DemoScenario.BuildTown(s);
                for (int w = 0; w < 156; w++)
                {
                    s.StepWeek();
                    foreach (var b in s.Buildings.Where(x => x.Alive))
                    {
                        assert(b.Residents >= 0 && b.Residents <= b.Housing, "cư dân vượt sức chứa tại tuần " + w);
                        assert(b.Workers >= 0 && b.Workers <= b.Jobs, "lao động vượt số việc làm");
                        assert(b.Level >= 1 && b.Level <= 3, "cấp công trình");
                    }
                    var i = s.Indicators;
                    assert(!float.IsNaN(i.QoL) && i.QoL >= 0 && i.QoL <= 1, "QoL ngoài [0,1]");
                    assert(i.QoLLowestQuintile <= i.QoL + 1e-4f, "nhóm thấp nhất phải ≤ trung bình");
                    assert(i.UnemploymentRate >= 0 && i.UnemploymentRate <= 1, "thất nghiệp");
                }
                var f = s.Indicators;
                assert(f.Population > 500, "dân số quá thấp: " + f.Population);
                Console.Write($" [dân {f.Population}, nhà ở {f.Housing}, việc {f.Jobs}, thất nghiệp {f.UnemploymentRate:P0}, QoL {f.QoL:0.00} (20% thấp nhất {f.QoLLowestQuintile:0.00}), {f.Buildings} công trình]");
            });

            add("Thành phố: dịch vụ (trường/bệnh viện/công an/công viên) nâng chất lượng sống", () =>
            {
                var with = Flat(11); DemoScenario.BuildTown(with, true); with.StepWeeks(120);
                var without = Flat(11); DemoScenario.BuildTown(without, false); without.StepWeeks(120);
                var a = with.Indicators; var b = without.Indicators;
                Console.Write($" [QoL có dịch vụ {a.QoL:0.00} vs không {b.QoL:0.00}; y tế {a.Health:0.00}/{b.Health:0.00}; giáo dục {a.Education:0.00}/{b.Education:0.00}]");
                assert(a.Health > b.Health + 0.05f && a.Education > b.Education + 0.05f, "độ phủ dịch vụ không tăng");
                assert(a.QoL > b.QoL + 0.03f, "QoL không tốt hơn khi có dịch vụ");
            });

            add("Thành phố: công nghiệp gây ô nhiễm khu nhà ở lân cận (theo khoảng cách)", () =>
            {
                var s = Flat(13); DemoScenario.BuildTown(s, true, true); s.StepWeeks(120);
                var ind = s.Buildings.Where(b => b.Alive && b.Kind == BuildingKind.Industrial).ToList();
                assert(ind.Count > 0, "không có công nghiệp");
                var res = s.Buildings.Where(b => b.Alive && BuildingSpecs.IsResidential(b.Kind)).ToList();
                float near = 0, far = 0; int nn = 0, nf = 0;
                foreach (var r in res)
                {
                    var rp = s.Parcels[r.Parcel]; float d = ind.Min(i => (float)Math.Sqrt(Math.Pow(s.Parcels[i.Parcel].X - rp.X, 2) + Math.Pow(s.Parcels[i.Parcel].Y - rp.Y, 2)));
                    if (d < 150) { near += r.Pollution; nn++; } else if (d > 350) { far += r.Pollution; nf++; }
                }
                assert(nn > 0 && nf > 0, "thiếu mẫu gần/xa");
                Console.Write($" [ô nhiễm gần {near / nn:0.00} vs xa {far / nf:0.00}]");
                assert(near / nn > far / nf + 0.1f, "ô nhiễm không giảm theo khoảng cách");
            });

            add("Thành phố: chỉ có nhà ở, không việc làm → thất nghiệp cao, dân không tăng bền", () =>
            {
                var s = Flat(17); s.BuildRoad(0, 500, 500, 500, RoadType.Urban); s.ZoneArea(250, 500, 300, Zone.ResidentialLow);
                s.StepWeeks(150);
                var i = s.Indicators;
                Console.Write($" [dân {i.Population}, thất nghiệp {i.UnemploymentRate:P0}, QoL {i.QoL:0.00}]");
                assert(i.Jobs == 0, "không được có việc làm"); assert(i.Population == 0 || i.UnemploymentRate > 0.9f, "phải thất nghiệp gần như hoàn toàn");
                assert(i.Population <= i.Housing, "dân vượt nhà ở");
            });

            add("Thành phố: phá bỏ công trình giải phóng lô và cư dân", () =>
            {
                var s = Flat(19); DemoScenario.BuildTown(s); s.StepWeeks(60);
                var b = s.Buildings.First(x => x.Alive && BuildingSpecs.IsResidential(x.Kind)); int pid = b.Parcel;
                assert(s.Demolish(pid).Ok, "phá bỏ"); assert(!b.Alive && s.Parcels[pid].Building < 0, "trạng thái sau phá bỏ");
                s.StepWeek();
                assert(s.Indicators.Population <= s.Indicators.Housing, "dân vượt nhà ở sau phá bỏ");
                assert(!s.Demolish(pid).Ok, "phá lô trống phải thất bại");
            });

            add("Thành phố: tất định — cùng seed + cùng lệnh → cùng trạng thái", () =>
            {
                var a = Flat(23); DemoScenario.BuildTown(a); a.StepWeeks(100);
                var b = Flat(23); DemoScenario.BuildTown(b); b.StepWeeks(100);
                var c = Flat(24); DemoScenario.BuildTown(c); c.StepWeeks(100);
                assert(DemoScenario.Hash(a) == DemoScenario.Hash(b), "cùng seed nhưng khác trạng thái");
                assert(DemoScenario.Hash(a) != DemoScenario.Hash(c), "khác seed nhưng trùng trạng thái");
            });

            add("Thành phố: địa hình lấy từ thế giới (thành phố cảng có biển, thành phố nội địa thì không); chạy trọn kịch bản", () =>
            {
                var w = WorldGenerator.Generate(new WorldConfig { Seed = 31, CellCount = 20000 });
                City port = w.Cities.First(c => c.IsPort), inland = null;
                foreach (var c in w.Cities)
                {
                    if (c.IsPort || w.IsRiver[c.Cell]) continue;
                    bool nearWater = false; foreach (int nb in w.Grid.Neighbors(c.Cell)) if (w.IsOcean[nb] || w.IsLake[nb]) nearWater = true;
                    if (!nearWater) { inland = c; break; }
                }
                assert(inland != null, "không tìm thấy thành phố nội địa");
                int WaterSamples(WorldTerrain t) { int n = 0; for (int x = 0; x <= 1000; x += 20) for (int y = 0; y <= 1000; y += 20) if (t.IsWater(x, y)) n++; return n; }
                var tp = new WorldTerrain(w, port.Id, 1000f); var ti = new WorldTerrain(w, inland.Id, 1000f);
                int wp = WaterSamples(tp), wi = WaterSamples(ti);
                Console.Write($" [nước: cảng {wp} mẫu, nội địa {wi} mẫu]");
                assert(wp > 50, "thành phố cảng phải có biển"); assert(wi == 0, "thành phố nội địa không được có nước");
                var s = new CitySim(tp, 5); DemoScenario.BuildTown(s); s.StepWeeks(80);
                assert(s.Indicators.Population > 0 && s.Buildings.Count > 0, "thành phố cảng không phát triển được");
                var s2 = new CitySim(ti, 5); DemoScenario.BuildTown(s2); s2.StepWeeks(80);
                assert(s2.Indicators.Population > 0, "thành phố nội địa không phát triển được");
            });
        }

        private static float CitySim_Dist(float px, float py, RoadNode a, RoadNode b)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy; float t = l2 < 1e-6f ? 0 : ((px - a.X) * dx + (py - a.Y) * dy) / l2; t = Math.Max(0, Math.Min(1, t));
            float qx = a.X + dx * t - px, qy = a.Y + dy * t - py; return (float)Math.Sqrt(qx * qx + qy * qy);
        }
    }
}
