using System;
using System.Collections.Generic;

namespace Vigo.Core
{
    /// <summary>Bước 13: mạng đường nền trong từng quốc gia — MST giữa các thành phố + vài đường vòng, A* tránh dốc/sông.</summary>
    internal static class RoadGen
    {
        public static void Generate(WorldState w, int attempt)
        {
            int n = w.CellCount;
            w.IsRoad = new bool[n]; w.IsBridge = new bool[n];
            var gScore = new float[n]; var parent = new int[n]; var stamp = new int[n]; int curStamp = 0;
            var heap = new MinHeap();

            foreach (var country in w.Countries)
            {
                var cities = new List<City>();
                foreach (var pid in country.Provinces) foreach (var cid in w.GetProvince(pid).Cities) cities.Add(w.GetCity(cid));
                int m = cities.Count; if (m < 2) continue;

                var edges = new HashSet<long>();
                var list = new List<int[]>();
                // Prim MST theo khoảng cách Euclid.
                var inTree = new bool[m]; var best = new float[m]; var from = new int[m];
                for (int i = 0; i < m; i++) { best[i] = float.MaxValue; from[i] = -1; }
                best[0] = 0;
                for (int it = 0; it < m; it++)
                {
                    int u = -1; for (int i = 0; i < m; i++) if (!inTree[i] && (u < 0 || best[i] < best[u])) u = i;
                    inTree[u] = true;
                    if (from[u] >= 0) AddEdge(edges, list, from[u], u, m);
                    for (int v = 0; v < m; v++)
                        if (!inTree[v]) { float d = Euclid(cities[u], cities[v]); if (d < best[v]) { best[v] = d; from[v] = u; } }
                }
                // Đường vòng: mỗi thành phố nối thêm tới láng giềng gần thứ hai nếu chưa có.
                for (int i = 0; i < m; i++)
                {
                    int n1 = -1, n2 = -1; float d1 = float.MaxValue, d2 = float.MaxValue;
                    for (int j = 0; j < m; j++)
                    {
                        if (j == i) continue; float d = Euclid(cities[i], cities[j]);
                        if (d < d1) { d2 = d1; n2 = n1; d1 = d; n1 = j; } else if (d < d2) { d2 = d; n2 = j; }
                    }
                    if (n2 >= 0 && d2 < d1 * 1.8f) AddEdge(edges, list, i, n2, m);
                }

                foreach (var e in list)
                {
                    var a = cities[e[0]]; var b = cities[e[1]];
                    curStamp++;
                    var path = AStar(w, a.Cell, b.Cell, country.Id.Value, gScore, parent, stamp, curStamp, heap);
                    if (path == null) { w.GenerationNotes.Add("Không nối được đường " + a.Name + " – " + b.Name); continue; }
                    var seg = new RoadSegment
                    {
                        A = a.Id, B = b.Id, Cells = path,
                        Class = (a.Population > 150000 && b.Population > 150000) ? RoadClass.Highway : RoadClass.National
                    };
                    for (int i = 0; i < path.Length; i++)
                    {
                        int c = path[i]; w.IsRoad[c] = true;
                        if (w.IsRiver[c]) { w.IsBridge[c] = true; seg.BridgeCells.Add(c); }
                    }
                    w.Roads.Add(seg);
                }
            }
        }

        private static void AddEdge(HashSet<long> set, List<int[]> list, int a, int b, int m)
        {
            int lo = Math.Min(a, b), hi = Math.Max(a, b); long key = (long)lo * m + hi;
            if (set.Add(key)) list.Add(new[] { lo, hi });
        }

        private static float Euclid(City a, City b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y; return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private static int[] AStar(WorldState w, int start, int goal, int country, float[] gScore, int[] parent, int[] stamp, int cur, MinHeap heap)
        {
            var g = w.Grid; heap.Clear();
            gScore[start] = 0; parent[start] = -1; stamp[start] = cur; heap.Push(start, w.Dist(start, goal) * 0.35f);
            while (heap.Count > 0)
            {
                int c = heap.Pop(out float f);
                if (c == goal) break;
                float gc = gScore[c];
                if (f - w.Dist(c, goal) * 0.35f > gc + 1e-3f) continue; // mục cũ
                foreach (int nb in g.Neighbors(c))
                {
                    if (w.CountryOfCell[nb] != country || w.IsLake[nb]) continue;
                    var bio = (Biome)w.Biomes[nb];
                    float cost = 1f + 4f * w.Slope[nb] + (w.IsRiver[nb] ? 3f : 0f) + (bio == Biome.Mountain ? 3f : 0f);
                    if (w.IsRoad[nb]) cost *= 0.35f; // tận dụng đường có sẵn
                    float ng = gc + w.Dist(c, nb) * cost;
                    if (stamp[nb] != cur || ng < gScore[nb])
                    {
                        stamp[nb] = cur; gScore[nb] = ng; parent[nb] = c;
                        heap.Push(nb, ng + w.Dist(nb, goal) * 0.35f);
                    }
                }
            }
            if (stamp[goal] != cur) return null;
            var path = new List<int>(); int x = goal;
            while (x >= 0) { path.Add(x); x = parent[x]; }
            path.Reverse();
            return path.ToArray();
        }
    }
}
