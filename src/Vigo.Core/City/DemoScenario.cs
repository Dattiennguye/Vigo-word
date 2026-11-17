using System;

namespace Vigo.Core.CityBuilder
{
    /// <summary>Kịch bản người chơi mẫu (chỉ dùng lệnh công khai): đường → dịch vụ → phân khu. Dùng cho test và ảnh minh họa.</summary>
    public static class DemoScenario
    {
        /// <summary>Tâm xây dựng: điểm xây được gần giữa bản đồ nhất (xoắn ốc), để kịch bản chạy được trên địa hình bất kỳ.</summary>
        public static void FindCenter(CitySim s, out float cx, out float cy)
        {
            float c = s.Size * 0.5f;
            for (int r = 0; r <= 420; r += 20)
                for (int a = 0; a < (r == 0 ? 1 : 16); a++)
                {
                    float ang = a * (float)Math.PI / 8f, x = c + r * (float)Math.Cos(ang), y = c + r * (float)Math.Sin(ang);
                    if (s.Terrain.Buildable(x, y) && s.Terrain.Buildable(x + 60, y) && s.Terrain.Buildable(x - 60, y) && s.Terrain.Buildable(x, y + 60) && s.Terrain.Buildable(x, y - 60)) { cx = x; cy = y; return; }
                }
            cx = c; cy = c;
        }

        /// <summary>Xây một đoạn; nếu bị từ chối (nước/dốc) thì co ngắn dần hai đầu về phía giữa, như người chơi thật.</summary>
        public static bool BuildSpan(CitySim s, float x1, float y1, float x2, float y2, RoadType type)
        {
            for (int i = 0; i < 6; i++)
            {
                float f = i * 0.08f;
                float ax = x1 + (x2 - x1) * f, ay = y1 + (y2 - y1) * f, bx = x2 - (x2 - x1) * f, by = y2 - (y2 - y1) * f;
                if (s.BuildRoad(ax, ay, bx, by, type).Ok) return true;
                // thử riêng từng đầu
                if (s.BuildRoad(ax, ay, (ax + bx) * 0.5f, (ay + by) * 0.5f, type).Ok) { s.BuildRoad((ax + bx) * 0.5f, (ay + by) * 0.5f, bx, by, type); return true; }
                if (s.BuildRoad((ax + bx) * 0.5f, (ay + by) * 0.5f, bx, by, type).Ok) return true;
            }
            return false;
        }

        public static void BuildTown(CitySim s, bool withServices = true, bool withIndustry = true)
        {
            FindCenter(s, out float cx, out float cy);
            var exit = s.Nodes[s.ExitNode];
            s.BuildRoad(exit.X, exit.Y, cx, cy, RoadType.Highway);
            float L = s.Size;
            float x0 = Math.Max(10f, cx - 280f), x1 = Math.Min(L - 10f, cx + 280f), y0 = Math.Max(10f, cy - 300f), y1 = Math.Min(L - 10f, cy + 300f);
            BuildSpan(s, x0, cy, x1, cy, RoadType.Avenue);
            BuildSpan(s, cx, y0, cx, y1, RoadType.Urban);
            foreach (float dy in new[] { -200f, -100f, 100f, 200f }) BuildSpan(s, x0 + 20, cy + dy, x1 - 20, cy + dy, RoadType.Local);
            foreach (float dx in new[] { -150f, 150f, 250f }) BuildSpan(s, cx + dx, y0 + 20, cx + dx, y1 - 20, RoadType.Local);

            if (withServices)
            {
                s.PlaceService(s.NearestFreeParcel(cx - 50, cy - 120), ServiceKind.School);
                s.PlaceService(s.NearestFreeParcel(cx + 50, cy + 120), ServiceKind.Hospital);
                s.PlaceService(s.NearestFreeParcel(cx, cy - 80), ServiceKind.Police);
                s.PlaceService(s.NearestFreeParcel(cx - 120, cy + 100), ServiceKind.Park);
                s.PlaceService(s.NearestFreeParcel(cx + 120, cy - 120), ServiceKind.Park);
            }
            s.ZoneArea(cx, cy, 450f, Zone.ResidentialLow);
            s.ZoneArea(cx, cy, 150f, Zone.ResidentialHigh);
            s.ZoneArea(cx - 90, cy, 80f, Zone.Commercial);
            s.ZoneArea(cx + 90, cy, 80f, Zone.Commercial);
            if (withIndustry) s.ZoneArea(cx + 200, cy + 200, 130f, Zone.Industrial);
        }

        public static ulong Hash(CitySim s)
        {
            ulong h = 14695981039346656037UL;
            void Mix(long v) { unchecked { h ^= (ulong)v; h *= 1099511628211UL; } }
            Mix(s.Nodes.Count); Mix(s.Edges.Count); Mix(s.Parcels.Count);
            foreach (var b in s.Buildings) { Mix(b.Alive ? 1 : 0); Mix((long)b.Kind); Mix(b.Parcel); Mix(b.Level); Mix(b.Residents); Mix(b.Workers); }
            return h;
        }
    }
}
