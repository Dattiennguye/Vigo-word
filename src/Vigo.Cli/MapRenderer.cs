using System;
using Vigo.Core;

namespace Vigo.Cli
{
    internal sealed class MapRenderer
    {
        private readonly WorldState _w; private readonly int _W, _H; private readonly float _sx;
        private readonly int[] _pix; // ô gần nhất của mỗi pixel
        private byte[] _img;

        public MapRenderer(WorldState w, int width)
        {
            _w = w; _W = width; _sx = w.Grid.Width / width; _H = (int)(w.Grid.Height / _sx);
            _pix = new int[_W * _H];
            for (int y = 0; y < _H; y++) for (int x = 0; x < _W; x++) _pix[y * _W + x] = w.Grid.Nearest((x + 0.5f) * _sx, (y + 0.5f) * _sx);
        }

        private void Set(int x, int y, int rgb)
        {
            if (x < 0 || y < 0 || x >= _W || y >= _H) return;
            int i = (y * _W + x) * 3; _img[i] = (byte)(rgb >> 16); _img[i + 1] = (byte)(rgb >> 8); _img[i + 2] = (byte)rgb;
        }
        private static int Mix(int a, int b, float t)
        {
            int r = (int)(((a >> 16) & 255) * (1 - t) + ((b >> 16) & 255) * t), g = (int)(((a >> 8) & 255) * (1 - t) + ((b >> 8) & 255) * t), bl = (int)((a & 255) * (1 - t) + (b & 255) * t);
            return (r << 16) | (g << 8) | bl;
        }
        private static int BiomeColor(Biome b)
        {
            switch (b)
            {
                case Biome.Ocean: return 0x2B5C8A; case Biome.Lake: return 0x4F8FC0; case Biome.Ice: return 0xF0F4F8;
                case Biome.Tundra: return 0xB8C4B0; case Biome.Taiga: return 0x4E7A5A; case Biome.Steppe: return 0xC8C98A;
                case Biome.Grassland: return 0x8FBF5A; case Biome.Forest: return 0x3E8A3E; case Biome.Wetland: return 0x5E9A8A;
                case Biome.Desert: return 0xE0C98A; case Biome.Savanna: return 0xC9B25A; case Biome.TropicalForest: return 0x1F7A3A;
                case Biome.Highland: return 0x9A8A6A; default: return 0x7A7270;
            }
        }
        private int Base(int cell, bool political)
        {
            var b = (Biome)_w.Biomes[cell];
            if (b == Biome.Ocean)
            {
                float d = Math.Min(1f, -_w.Elevation[cell]);
                return Mix(0x4A86B8, 0x153A63, d);
            }
            int col = BiomeColor(b);
            float shade = 0.85f + 0.3f * Math.Max(0f, _w.Elevation[cell]);
            if (political && _w.CountryOfCell[cell] >= 0)
            {
                int cc = NameHue(_w.CountryOfCell[cell]);
                col = Mix(col, cc, 0.62f);
            }
            return Mix(0x000000, col, Math.Min(1.1f, shade)) ;
        }
        private static int NameHue(int k) => Vigo.Core.NameGenAccess.Hsv((k * 0.61803398875) % 1.0, 0.65, 0.95);

        public void Render(string path, string mode)
        {
            _img = new byte[_W * _H * 3];
            bool political = mode == "political";
            for (int y = 0; y < _H; y++)
                for (int x = 0; x < _W; x++)
                {
                    int c = _pix[y * _W + x];
                    int col = Base(c, political);
                    if (political && _w.IsLand[c])
                    {
                        int cr = x + 1 < _W ? _pix[y * _W + x + 1] : c, cd = y + 1 < _H ? _pix[(y + 1) * _W + x] : c;
                        bool cb = _w.CountryOfCell[c] != _w.CountryOfCell[cr] || _w.CountryOfCell[c] != _w.CountryOfCell[cd];
                        bool pb = _w.ProvinceOfCell[c] != _w.ProvinceOfCell[cr] || _w.ProvinceOfCell[c] != _w.ProvinceOfCell[cd];
                        if (cb) col = 0x1A1A1A; else if (pb) col = Mix(col, 0x222222, 0.45f);
                    }
                    Set(x, y, col);
                }
            // Sông
            var g = _w.Grid;
            for (int i = 0; i < _w.CellCount; i++)
            {
                if (!_w.IsRiver[i]) continue;
                int to = _w.FlowTo[i]; if (to < 0) continue;
                Line(g.X[i] / _sx, g.Y[i] / _sx, g.X[to] / _sx, g.Y[to] / _sx, 0x3C7FD0, _w.Flux[i] > _w.RiverThreshold * 2.5f ? 1 : 0);
            }
            // Đường
            foreach (var r in _w.Roads)
            {
                int col = r.Class == RoadClass.Highway ? 0xFFE08A : 0xEDEDED;
                for (int i = 1; i < r.Cells.Length; i++)
                    Line(g.X[r.Cells[i - 1]] / _sx, g.Y[r.Cells[i - 1]] / _sx, g.X[r.Cells[i]] / _sx, g.Y[r.Cells[i]] / _sx, col, 0);
            }
            if (mode == "resources")
                foreach (var d in _w.Deposits)
                    Disc(g.X[d.Cell] / _sx, g.Y[d.Cell] / _sx, 2f + d.Richness * 1.3f, ResColor(d.Type), 0x101010);
            else
                foreach (var c in _w.Cities)
                {
                    float rad = c.IsCapital ? 4.6f : (c.IsProvincialSeat ? 2.6f : 1.5f);
                    Disc(c.X / _sx, c.Y / _sx, rad, c.IsCapital ? 0xFF2D2D : 0xFFFFFF, 0x101010);
                }
            Png.Save(path, _img, _W, _H);
        }

        private static int ResColor(ResourceType t)
        {
            switch (t)
            {
                case ResourceType.Coal: return 0x222222; case ResourceType.Oil: return 0xB03A8A; case ResourceType.Gas: return 0x40C0C0;
                case ResourceType.Iron: return 0xB8501E; case ResourceType.Copper: return 0xE08A3A; case ResourceType.Aluminium: return 0xCFCFD8;
                case ResourceType.PreciousMetals: return 0xFFD700; default: return 0x8E8E5A;
            }
        }

        private void Disc(float cx, float cy, float r, int fill, int outline)
        {
            int r0 = (int)Math.Ceiling(r + 1);
            for (int dy = -r0; dy <= r0; dy++) for (int dx = -r0; dx <= r0; dx++)
                {
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (d <= r) Set((int)cx + dx, (int)cy + dy, fill); else if (d <= r + 1f) Set((int)cx + dx, (int)cy + dy, outline);
                }
        }
        private void Line(float x0, float y0, float x1, float y1, int col, int thick)
        {
            float dx = x1 - x0, dy = y1 - y0; int steps = (int)Math.Max(Math.Abs(dx), Math.Abs(dy)) + 1;
            for (int s = 0; s <= steps; s++)
            {
                float t = (float)s / steps; int px = (int)(x0 + dx * t), py = (int)(y0 + dy * t);
                Set(px, py, col); if (thick > 0) { Set(px + 1, py, col); Set(px, py + 1, col); }
            }
        }
    }
}
