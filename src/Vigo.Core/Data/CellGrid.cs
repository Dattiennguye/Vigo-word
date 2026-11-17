using System;
using System.Collections.Generic;

namespace Vigo.Core
{
    /// <summary>
    /// Đồ thị ô bất quy tắc: điểm Poisson-disk (blue noise, không phải lưới vuông) + liên kết kề
    /// theo đồ thị Gabriel (tập con của Delaunay, liên thông, phẳng). Mỗi điểm đại diện một ô Voronoi ngầm định.
    /// </summary>
    public sealed class CellGrid
    {
        public int Count;
        public float[] X, Y;
        public float Width, Height, Spacing;
        public int[] NbStart, Nb;

        private int _gw, _gh;
        private float _gs;
        private int[] _gStart, _gItems;

        public ReadOnlySpan<int> Neighbors(int i) => new ReadOnlySpan<int>(Nb, NbStart[i], NbStart[i + 1] - NbStart[i]);

        public static CellGrid Generate(Rng rng, float width, float height, int targetCount)
        {
            float area = width * height;
            float r = (float)Math.Sqrt(area / targetCount) * 1.0f;
            var xs = new List<float>(); var ys = new List<float>();
            for (int pass = 0; pass < 3; pass++)
            {
                xs.Clear(); ys.Clear();
                Poisson(new Rng(rng.NextU64()), width, height, r, xs, ys);
                int c = xs.Count;
                float err = Math.Abs(c - targetCount) / (float)targetCount;
                if (err < 0.04f) break;
                r *= (float)Math.Sqrt((double)c / targetCount);
            }
            var g = new CellGrid { Count = xs.Count, X = xs.ToArray(), Y = ys.ToArray(), Width = width, Height = height, Spacing = r };
            g.BuildGrid(r);
            g.BuildNeighbors();
            return g;
        }

        private static void Poisson(Rng rng, float w, float h, float r, List<float> xs, List<float> ys)
        {
            float cs = r / 1.41421356f;
            int gw = (int)Math.Ceiling(w / cs), gh = (int)Math.Ceiling(h / cs);
            var grid = new int[gw * gh];
            for (int i = 0; i < grid.Length; i++) grid[i] = -1;
            var active = new List<int>();
            float r2 = r * r;

            void Add(float x, float y)
            {
                int idx = xs.Count; xs.Add(x); ys.Add(y);
                int cx = Math.Min(gw - 1, (int)(x / cs)), cy = Math.Min(gh - 1, (int)(y / cs));
                grid[cy * gw + cx] = idx; active.Add(idx);
            }

            Add(rng.Range(0f, w), rng.Range(0f, h));
            while (active.Count > 0)
            {
                int ai = rng.Range(0, active.Count);
                int pi = active[ai];
                float px = xs[pi], py = ys[pi];
                bool found = false;
                for (int k = 0; k < 30 && !found; k++)
                {
                    double ang = rng.NextDouble() * 2.0 * Math.PI;
                    float rad = r * (1f + rng.NextFloat());
                    float nx = px + (float)Math.Cos(ang) * rad, ny = py + (float)Math.Sin(ang) * rad;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int cx = Math.Min(gw - 1, (int)(nx / cs)), cy = Math.Min(gh - 1, (int)(ny / cs));
                    bool ok = true;
                    for (int yy = Math.Max(0, cy - 2); yy <= Math.Min(gh - 1, cy + 2) && ok; yy++)
                        for (int xx = Math.Max(0, cx - 2); xx <= Math.Min(gw - 1, cx + 2); xx++)
                        {
                            int o = grid[yy * gw + xx];
                            if (o < 0) continue;
                            float dx = xs[o] - nx, dy = ys[o] - ny;
                            if (dx * dx + dy * dy < r2) { ok = false; break; }
                        }
                    if (ok) { Add(nx, ny); found = true; }
                }
                if (!found) { active[ai] = active[active.Count - 1]; active.RemoveAt(active.Count - 1); }
            }
        }

        private void BuildGrid(float cellSize)
        {
            _gs = cellSize;
            _gw = (int)Math.Ceiling(Width / _gs) + 1; _gh = (int)Math.Ceiling(Height / _gs) + 1;
            _gStart = new int[_gw * _gh + 1];
            var cellOf = new int[Count];
            for (int i = 0; i < Count; i++)
            {
                int c = Math.Min(_gh - 1, (int)(Y[i] / _gs)) * _gw + Math.Min(_gw - 1, (int)(X[i] / _gs));
                cellOf[i] = c; _gStart[c + 1]++;
            }
            for (int i = 0; i < _gw * _gh; i++) _gStart[i + 1] += _gStart[i];
            var fill = new int[_gw * _gh];
            _gItems = new int[Count];
            for (int i = 0; i < Count; i++) { int c = cellOf[i]; _gItems[_gStart[c] + fill[c]++] = i; }
        }

        private void BuildNeighbors()
        {
            var lists = new List<int>[Count];
            for (int i = 0; i < Count; i++) lists[i] = new List<int>(7);
            float maxD = _gs * 2.4f, maxD2 = maxD * maxD;
            for (int i = 0; i < Count; i++)
            {
                int cx = Math.Min(_gw - 1, (int)(X[i] / _gs)), cy = Math.Min(_gh - 1, (int)(Y[i] / _gs));
                for (int yy = Math.Max(0, cy - 3); yy <= Math.Min(_gh - 1, cy + 3); yy++)
                    for (int xx = Math.Max(0, cx - 3); xx <= Math.Min(_gw - 1, cx + 3); xx++)
                    {
                        int c = yy * _gw + xx;
                        for (int s = _gStart[c]; s < _gStart[c + 1]; s++)
                        {
                            int j = _gItems[s];
                            if (j <= i) continue;
                            float dx = X[j] - X[i], dy = Y[j] - Y[i];
                            float d2 = dx * dx + dy * dy;
                            if (d2 > maxD2) continue;
                            if (IsGabriel(i, j, d2)) { lists[i].Add(j); lists[j].Add(i); }
                        }
                    }
            }
            NbStart = new int[Count + 1];
            for (int i = 0; i < Count; i++) { lists[i].Sort(); NbStart[i + 1] = NbStart[i] + lists[i].Count; }
            Nb = new int[NbStart[Count]];
            for (int i = 0; i < Count; i++) lists[i].CopyTo(Nb, NbStart[i]);
        }

        private bool IsGabriel(int i, int j, float d2)
        {
            float mx = (X[i] + X[j]) * 0.5f, my = (Y[i] + Y[j]) * 0.5f;
            float rad2 = d2 * 0.25f * 0.9999f;
            int cx = Math.Min(_gw - 1, Math.Max(0, (int)(mx / _gs))), cy = Math.Min(_gh - 1, Math.Max(0, (int)(my / _gs)));
            for (int yy = Math.Max(0, cy - 2); yy <= Math.Min(_gh - 1, cy + 2); yy++)
                for (int xx = Math.Max(0, cx - 2); xx <= Math.Min(_gw - 1, cx + 2); xx++)
                {
                    int c = yy * _gw + xx;
                    for (int s = _gStart[c]; s < _gStart[c + 1]; s++)
                    {
                        int k = _gItems[s];
                        if (k == i || k == j) continue;
                        float dx = X[k] - mx, dy = Y[k] - my;
                        if (dx * dx + dy * dy < rad2) return false;
                    }
                }
            return true;
        }

        /// <summary>Ô gần nhất với tọa độ (x, y).</summary>
        public int Nearest(float x, float y)
        {
            int cx = Math.Min(_gw - 1, Math.Max(0, (int)(x / _gs))), cy = Math.Min(_gh - 1, Math.Max(0, (int)(y / _gs)));
            int best = -1; float bd = float.MaxValue;
            int maxRing = Math.Max(_gw, _gh);
            for (int ring = 0; ring <= maxRing; ring++)
            {
                for (int yy = cy - ring; yy <= cy + ring; yy++)
                {
                    if (yy < 0 || yy >= _gh) continue;
                    bool edgeRow = (yy == cy - ring || yy == cy + ring);
                    int step = edgeRow ? 1 : Math.Max(1, 2 * ring);
                    for (int xx = cx - ring; xx <= cx + ring; xx += step)
                    {
                        if (xx < 0 || xx >= _gw) continue;
                        int c = yy * _gw + xx;
                        for (int s = _gStart[c]; s < _gStart[c + 1]; s++)
                        {
                            int k = _gItems[s];
                            float dx = X[k] - x, dy = Y[k] - y, d2 = dx * dx + dy * dy;
                            if (d2 < bd) { bd = d2; best = k; }
                        }
                    }
                }
                float lim = ring * _gs;
                if (best >= 0 && bd <= lim * lim) break;
            }
            return best;
        }
    }
}
