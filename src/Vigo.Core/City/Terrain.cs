using System;

namespace Vigo.Core.CityBuilder
{
    public interface ITerrain
    {
        bool IsWater(float x, float y);
        float Slope(float x, float y);
    }

    public static class TerrainExt
    {
        public const float MaxBuildSlope = 0.30f;
        public static bool Buildable(this ITerrain t, float x, float y) { return !t.IsWater(x, y) && t.Slope(x, y) < MaxBuildSlope; }
    }

    /// <summary>Địa hình phẳng (dùng cho test), có thể thêm một dải nước hình chữ nhật.</summary>
    public sealed class FlatTerrain : ITerrain
    {
        public float WaterX0 = -1, WaterX1 = -1; // dải nước dọc theo trục Y
        public bool IsWater(float x, float y) { return x >= WaterX0 && x <= WaterX1; }
        public float Slope(float x, float y) { return 0f; }
    }

    /// <summary>
    /// Địa hình cục bộ của một thành phố, suy ra từ dữ liệu thế giới: độ dốc ô → độ gồ ghề; sông ở ô sông chảy theo hướng thật (FlowTo);
    /// thành phố cảng có biển phía hướng đại dương; ô kề hồ có hồ. Chi tiết nhỏ do noise tất định theo seed + ô.
    /// </summary>
    public sealed class WorldTerrain : ITerrain
    {
        private readonly float _size; private readonly Noise2D _noise; private readonly float _rough;
        private readonly bool _hasRiver, _hasSea, _hasLake;
        private readonly float _rdx, _rdy, _riverW, _sx, _sy, _lx, _ly;

        public WorldTerrain(WorldState w, CityId cityId, float sizeMeters)
        {
            var city = w.GetCity(cityId); int c = city.Cell; var g = w.Grid; _size = sizeMeters;
            _noise = new Noise2D(w.Seed ^ ((ulong)(uint)c * 0x9E3779B97F4A7C15UL));
            _rough = 0.3f + 3f * w.Slope[c];

            if (w.IsRiver[c] && w.FlowTo[c] >= 0)
            {
                int to = w.FlowTo[c]; float dx = g.X[to] - g.X[c], dy = g.Y[to] - g.Y[c]; float l = (float)Math.Sqrt(dx * dx + dy * dy);
                if (l > 1e-3f) { _hasRiver = true; _rdx = dx / l; _rdy = dy / l; _riverW = 22f + 10f * Math.Min(3f, w.Flux[c] / Math.Max(1f, w.RiverThreshold)); }
            }
            float ox = 0, oy = 0, lx = 0, ly = 0; int oc = 0, lc = 0;
            foreach (int nb in g.Neighbors(c))
            {
                float dx = g.X[nb] - g.X[c], dy = g.Y[nb] - g.Y[c];
                if (w.IsOcean[nb]) { ox += dx; oy += dy; oc++; }
                if (w.IsLake[nb]) { lx += dx; ly += dy; lc++; }
            }
            if (city.IsPort && oc > 0) { float l = (float)Math.Sqrt(ox * ox + oy * oy); if (l > 1e-3f) { _hasSea = true; _sx = ox / l; _sy = oy / l; } }
            if (lc > 0) { float l = (float)Math.Sqrt(lx * lx + ly * ly); if (l > 1e-3f) { _hasLake = true; _lx = lx / l; _ly = ly / l; } }
        }

        public bool IsWater(float x, float y)
        {
            float cx = x - _size * 0.5f, cy = y - _size * 0.5f;
            if (_hasRiver)
            {
                float along = cx * _rdx + cy * _rdy, perp = cx * -_rdy + cy * _rdx;
                perp += 35f * (float)Math.Sin(along / 140f) + 15f * _noise.Value(x / 200f, y / 200f);
                if (Math.Abs(perp) < _riverW * 0.5f) return true;
            }
            if (_hasSea)
            {
                float d = cx * _sx + cy * _sy;
                if (d > 0.22f * _size + 60f * _noise.Value(x / 250f + 9f, y / 250f)) return true;
            }
            if (_hasLake)
            {
                float lx = cx - _lx * 0.28f * _size, ly = cy - _ly * 0.28f * _size;
                float r = 170f + 40f * _noise.Value(x / 150f + 4f, y / 150f);
                if (lx * lx + ly * ly < r * r) return true;
            }
            return false;
        }

        private float Height(float x, float y) { return 40f * _rough * _noise.Fbm(x / 350f, y / 350f, 3); }

        public float Slope(float x, float y)
        {
            const float e = 8f;
            float gx = (Height(x + e, y) - Height(x - e, y)) / (2 * e), gy = (Height(x, y + e) - Height(x, y - e)) / (2 * e);
            return (float)Math.Sqrt(gx * gx + gy * gy);
        }
    }
}
