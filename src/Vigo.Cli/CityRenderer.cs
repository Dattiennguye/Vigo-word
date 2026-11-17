using System;
using Vigo.Core.CityBuilder;

namespace Vigo.Cli
{
    internal static class CityRenderer
    {
        public static void Render(CitySim s, string path, int px = 1000)
        {
            float k = s.Size / px; var img = new byte[px * px * 3];
            void Set(int x, int y, int rgb) { if (x < 0 || y < 0 || x >= px || y >= px) return; int i = (y * px + x) * 3; img[i] = (byte)(rgb >> 16); img[i + 1] = (byte)(rgb >> 8); img[i + 2] = (byte)rgb; }
            for (int y = 0; y < px; y++)
                for (int x = 0; x < px; x++)
                {
                    float wx = (x + 0.5f) * k, wy = (y + 0.5f) * k;
                    int col = s.Terrain.IsWater(wx, wy) ? 0x4F8FC0 : (s.Terrain.Slope(wx, wy) >= TerrainExt.MaxBuildSlope ? 0xA89F86 : 0xCFD9B8);
                    Set(x, y, col);
                }
            void Rect(float cx, float cy, float r, int fill) { for (int y = (int)((cy - r) / k); y <= (int)((cy + r) / k); y++) for (int x = (int)((cx - r) / k); x <= (int)((cx + r) / k); x++) Set(x, y, fill); }
            foreach (var p in s.Parcels)
            {
                if (!p.Alive) continue;
                int c = 0xE6E6E0;
                switch (p.Zone) { case Zone.ResidentialLow: c = 0xB7E0A1; break; case Zone.ResidentialHigh: c = 0x7FC77A; break; case Zone.Commercial: c = 0x9CC3F0; break; case Zone.Industrial: c = 0xF0D58C; break; }
                Rect(p.X, p.Y, 7f, c);
            }
            foreach (var e in s.Edges)
            {
                if (!e.Alive) continue;
                var a = s.Nodes[e.A]; var b = s.Nodes[e.B]; float w = RoadSpecs.Width(e.Type) / k; int col = e.IsBridge ? 0xC0392B : (e.Type == RoadType.Highway ? 0xE8B04A : (e.Type == RoadType.Avenue ? 0xF2F2F2 : 0xFFFFFF));
                int steps = (int)(Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y)) / k) + 1;
                for (int i = 0; i <= steps; i++)
                {
                    float t = (float)i / steps, x = (a.X + (b.X - a.X) * t) / k, y = (a.Y + (b.Y - a.Y) * t) / k;
                    int r = Math.Max(1, (int)(w * 0.5f));
                    for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++) Set((int)x + dx, (int)y + dy, 0x4A4A4A);
                }
            }
            foreach (var b in s.Buildings)
            {
                if (!b.Alive) continue; var p = s.Parcels[b.Parcel]; int c; float r = 4f + b.Level * 1.5f;
                switch (b.Kind)
                {
                    case BuildingKind.ResLow: c = 0x2E8B57; break; case BuildingKind.ResHigh: c = 0x1B5E3A; break; case BuildingKind.Commercial: c = 0x2F6DB5; break;
                    case BuildingKind.Industrial: c = 0xB8860B; break; case BuildingKind.School: c = 0xFFD700; r = 9; break; case BuildingKind.Hospital: c = 0xE53935; r = 10; break;
                    case BuildingKind.Police: c = 0x303F9F; r = 9; break; default: c = 0x66BB6A; r = 9; break;
                }
                Rect(p.X, p.Y, r, c);
            }
            Png.Save(path, img, px, px);
        }
    }
}
