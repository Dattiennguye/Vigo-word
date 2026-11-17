using System;
using System.Collections.Generic;

namespace Vigo.Core.CityBuilder
{
    /// <summary>
    /// Trạng thái + lệnh của một thành phố (TDD mục 7). Đường là đồ thị (nút/cạnh), lô đất sinh dọc đường,
    /// công trình tự phát triển theo nhu cầu. Mọi lệnh đều được kiểm tra hợp lệ trước khi thay đổi trạng thái.
    /// </summary>
    public sealed partial class CitySim
    {
        public const float SnapNode = 25f, SnapEdge = 12f, MinSegment = 30f, MaxPiece = 250f, LotWidth = 16f, EndClear = 10f;

        public readonly float Size;
        public readonly ITerrain Terrain;
        public readonly List<RoadNode> Nodes = new List<RoadNode>();
        public readonly List<RoadEdge> Edges = new List<RoadEdge>();
        public readonly List<Parcel> Parcels = new List<Parcel>();
        public readonly List<Building> Buildings = new List<Building>();
        public int Week;
        private readonly Rng _rng;
        private readonly Dictionary<long, List<int>> _parcelHash = new Dictionary<long, List<int>>();

        public CitySim(ITerrain terrain, ulong seed, float size = 1000f)
        {
            Terrain = terrain; Size = size; _rng = new Rng(seed);
            // Điểm nối ra quốc lộ (nơi đường cao tốc quốc gia đi vào): ưu tiên giữa mép trái, nếu là nước/dốc thì tìm điểm khô gần nhất
            // trên các mép (trái, phải, trên, dưới).
            FindExitPoint(out float exX, out float exY);
            Nodes.Add(new RoadNode { Id = 0, X = exX, Y = exY, IsExit = true });
        }

        public int ExitNode => 0;

        private void FindExitPoint(out float x, out float y)
        {
            for (int edge = 0; edge < 4; edge++)
                for (int k = 0; k <= 48; k++)
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        float along = Size * 0.5f + sign * k * 20f; if (along < 0f || along > Size) continue;
                        float px = edge == 0 ? 0f : (edge == 1 ? Size : along), py = edge == 0 || edge == 1 ? along : (edge == 2 ? 0f : Size);
                        float ix = edge == 0 ? 20f : (edge == 1 ? Size - 20f : along), iy = edge == 0 || edge == 1 ? along : (edge == 2 ? 20f : Size - 20f);
                        if (Terrain.Buildable(px, py) && Terrain.Buildable(ix, iy)) { x = px; y = py; return; }
                    }
            x = 0f; y = Size * 0.5f; // không còn lựa chọn nào: thành phố bị nước/dốc bao kín
        }
        private bool InBounds(float x, float y) { return x >= 0 && y >= 0 && x <= Size && y <= Size; }

        // ------------------------------------------------------------------ hình học
        internal static float PointSegDist(float px, float py, float ax, float ay, float bx, float by, out float t)
        {
            float dx = bx - ax, dy = by - ay, l2 = dx * dx + dy * dy;
            t = l2 < 1e-6f ? 0f : ((px - ax) * dx + (py - ay) * dy) / l2;
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            float qx = ax + dx * t - px, qy = ay + dy * t - py;
            return (float)Math.Sqrt(qx * qx + qy * qy);
        }

        private static bool SegCross(float ax, float ay, float bx, float by, float cx, float cy, float dx, float dy, out float t)
        {
            t = 0f;
            float rx = bx - ax, ry = by - ay, sx = dx - cx, sy = dy - cy;
            float den = rx * sy - ry * sx; if (Math.Abs(den) < 1e-6f) return false;
            float tt = ((cx - ax) * sy - (cy - ay) * sx) / den, uu = ((cx - ax) * ry - (cy - ay) * rx) / den;
            if (tt <= 0.001f || tt >= 0.999f || uu <= 0.001f || uu >= 0.999f) return false;
            t = tt; return true;
        }

        private float Dist(float x1, float y1, float x2, float y2) { float dx = x1 - x2, dy = y1 - y2; return (float)Math.Sqrt(dx * dx + dy * dy); }

        // ------------------------------------------------------------------ lệnh: xây đường
        public Result BuildRoad(float x1, float y1, float x2, float y2, RoadType type)
        {
            if (!InBounds(x1, y1) || !InBounds(x2, y2)) return Result.Fail("Ngoài bản đồ thành phố");
            SnapPoint(ref x1, ref y1); SnapPoint(ref x2, ref y2);
            float len = Dist(x1, y1, x2, y2);
            if (len < MinSegment) return Result.Fail("Đoạn đường quá ngắn (< " + MinSegment + " m)");

            string terr = CheckTerrain(x1, y1, x2, y2, type);
            if (terr != null) return Result.Fail(terr);
            float half = RoadSpecs.Width(type) * 0.5f + 8f;
            foreach (var p in Parcels)
            {
                if (!p.Alive || p.Building < 0) continue;
                if (PointSegDist(p.X, p.Y, x1, y1, x2, y2, out float _) < half) return Result.Fail("Đường đè lên công trình đã xây");
            }

            // --- từ đây không thể thất bại nữa: thay đổi trạng thái ---
            int s = NodeAt(x1, y1), e = NodeAt(x2, y2);
            float sx = Nodes[s].X, sy = Nodes[s].Y, ex = Nodes[e].X, ey = Nodes[e].Y;
            var cross = new List<KeyValuePair<float, int>>();
            foreach (var ed in Edges)
            {
                if (!ed.Alive || ed.A == s || ed.B == s || ed.A == e || ed.B == e) continue;
                var a = Nodes[ed.A]; var b = Nodes[ed.B];
                if (SegCross(sx, sy, ex, ey, a.X, a.Y, b.X, b.Y, out float t)) cross.Add(new KeyValuePair<float, int>(t, ed.Id));
            }
            // Nút có sẵn nằm trên đường mới (giao đúng nút) cũng phải được nối vào.
            var onSeg = new List<KeyValuePair<float, int>>();
            foreach (var nd in Nodes)
            {
                if (nd.Id == s || nd.Id == e) continue;
                float dd = PointSegDist(nd.X, nd.Y, sx, sy, ex, ey, out float tn);
                if (dd < 10f && tn > 0.001f && tn < 0.999f) onSeg.Add(new KeyValuePair<float, int>(tn, nd.Id));
            }
            var order = new List<KeyValuePair<float, int>>(); // (t, id): id >= 0 là nút, id < 0 là cạnh (-id-1)
            foreach (var c in cross) order.Add(new KeyValuePair<float, int>(c.Key, -c.Value - 1));
            foreach (var o in onSeg) order.Add(o);
            order.Sort((p, q) => p.Key.CompareTo(q.Key));
            var chain = new List<int> { s };
            foreach (var o in order)
            {
                int n;
                if (o.Value >= 0) n = o.Value;
                else
                {
                    var ed = Edges[-o.Value - 1]; if (!ed.Alive) continue;
                    var a = Nodes[ed.A]; var b = Nodes[ed.B];
                    if (!SegCross(sx, sy, ex, ey, a.X, a.Y, b.X, b.Y, out float t)) continue;
                    n = SplitEdge(ed.Id, sx + (ex - sx) * t, sy + (ey - sy) * t);
                }
                if (n != chain[chain.Count - 1]) chain.Add(n);
            }
            if (e != chain[chain.Count - 1]) chain.Add(e);

            var created = new List<int>();
            for (int i = 1; i < chain.Count; i++) AddPieces(chain[i - 1], chain[i], type, created);

            // Xóa lô trống nằm trong hành lang đường mới; sinh lô cho đoạn mới.
            foreach (int id in created)
            {
                var ed = Edges[id]; var a = Nodes[ed.A]; var b = Nodes[ed.B]; float hw = RoadSpecs.Width(ed.Type) * 0.5f + 4f;
                foreach (var p in Parcels)
                    if (p.Alive && p.Building < 0 && PointSegDist(p.X, p.Y, a.X, a.Y, b.X, b.Y, out float _) < hw) p.Alive = false;
            }
            foreach (int id in created) GenerateParcels(Edges[id]);
            return Result.Success(created.Count);
        }

        private void SnapPoint(ref float x, ref float y)
        {
            foreach (var n in Nodes) if (Dist(x, y, n.X, n.Y) <= SnapNode) { x = n.X; y = n.Y; return; }
        }

        private string CheckTerrain(float x1, float y1, float x2, float y2, RoadType type)
        {
            float len = Dist(x1, y1, x2, y2); int steps = (int)Math.Ceiling(len / 10f);
            float waterRun = 0f, maxWater = 0f; bool prevWater = false;
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps, x = x1 + (x2 - x1) * t, y = y1 + (y2 - y1) * t;
                bool water = Terrain.IsWater(x, y);
                if (water)
                {
                    if (i == 0 || i == steps) return "Đầu đường nằm trên mặt nước";
                    waterRun = prevWater ? waterRun + len / steps : len / steps; maxWater = Math.Max(maxWater, waterRun);
                }
                else if (Terrain.Slope(x, y) >= TerrainExt.MaxBuildSlope) return "Địa hình quá dốc";
                prevWater = water;
            }
            if (maxWater > 0f)
            {
                if (type < RoadType.Avenue) return "Chỉ đại lộ/cao tốc mới được bắc cầu";
                if (maxWater > 80f) return "Nhịp cầu quá dài (" + (int)maxWater + " m > 80 m)";
            }
            return null;
        }

        /// <summary>Nút tại (x,y): dùng nút có sẵn, hoặc tách cạnh gần nhất, hoặc tạo nút mới.</summary>
        private int NodeAt(float x, float y)
        {
            foreach (var n in Nodes) if (Dist(x, y, n.X, n.Y) <= 1f) return n.Id;
            foreach (var ed in Edges)
            {
                if (!ed.Alive) continue;
                var a = Nodes[ed.A]; var b = Nodes[ed.B];
                float d = PointSegDist(x, y, a.X, a.Y, b.X, b.Y, out float t);
                if (d <= SnapEdge) return SplitEdge(ed.Id, a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
            }
            var node = new RoadNode { Id = Nodes.Count, X = x, Y = y }; Nodes.Add(node); return node.Id;
        }

        private int SplitEdge(int edgeId, float px, float py)
        {
            var old = Edges[edgeId]; var a = Nodes[old.A]; var b = Nodes[old.B];
            if (Dist(px, py, a.X, a.Y) < 15f) return a.Id;
            if (Dist(px, py, b.X, b.Y) < 15f) return b.Id;
            var n = new RoadNode { Id = Nodes.Count, X = px, Y = py }; Nodes.Add(n);
            old.Alive = false; a.Edges.Remove(old.Id); b.Edges.Remove(old.Id);
            var e1 = NewEdge(a.Id, n.Id, old.Type, old.IsBridge); var e2 = NewEdge(n.Id, b.Id, old.Type, old.IsBridge);
            foreach (var p in Parcels)
            {
                if (!p.Alive || p.EdgeId != old.Id) continue;
                float d1 = PointSegDist(p.X, p.Y, a.X, a.Y, n.X, n.Y, out float t1), d2 = PointSegDist(p.X, p.Y, n.X, n.Y, b.X, b.Y, out float t2);
                var e = d1 <= d2 ? e1 : e2; float t = d1 <= d2 ? t1 : t2;
                p.EdgeId = e.Id; AssignAccess(p, e, t);
            }
            return n.Id;
        }

        private RoadEdge NewEdge(int a, int b, RoadType type, bool bridge)
        {
            var e = new RoadEdge { Id = Edges.Count, A = a, B = b, Type = type, IsBridge = bridge, Length = Dist(Nodes[a].X, Nodes[a].Y, Nodes[b].X, Nodes[b].Y) };
            Edges.Add(e); Nodes[a].Edges.Add(e.Id); Nodes[b].Edges.Add(e.Id); return e;
        }

        private void AddPieces(int u, int v, RoadType type, List<int> created)
        {
            if (u == v) return;
            var nu = Nodes[u]; var nv = Nodes[v]; float len = Dist(nu.X, nu.Y, nv.X, nv.Y);
            int k = Math.Max(1, (int)Math.Ceiling(len / MaxPiece)); int prev = u;
            for (int i = 1; i <= k; i++)
            {
                int next = i == k ? v : NewNodeAt(nu.X + (nv.X - nu.X) * i / k, nu.Y + (nv.Y - nu.Y) * i / k);
                var pa = Nodes[prev]; var pb = Nodes[next];
                bool bridge = Terrain.IsWater((pa.X + pb.X) * 0.5f, (pa.Y + pb.Y) * 0.5f);
                RoadEdge existing = null;
                foreach (int eid in pa.Edges) { var ed = Edges[eid]; if (ed.Alive && ((ed.A == prev && ed.B == next) || (ed.A == next && ed.B == prev))) existing = ed; }
                if (existing != null) { if (type > existing.Type) existing.Type = type; }
                else created.Add(NewEdge(prev, next, type, bridge).Id);
                prev = next;
            }
        }

        private int NewNodeAt(float x, float y) { var n = new RoadNode { Id = Nodes.Count, X = x, Y = y }; Nodes.Add(n); return n.Id; }

        // ------------------------------------------------------------------ lô đất
        private static long HashKey(int cx, int cy) { return ((long)cx << 32) ^ (uint)cy; }

        private bool ParcelNear(float x, float y, float minDist)
        {
            int cx = (int)Math.Floor(x / 20f), cy = (int)Math.Floor(y / 20f);
            for (int ix = cx - 1; ix <= cx + 1; ix++)
                for (int iy = cy - 1; iy <= cy + 1; iy++)
                    if (_parcelHash.TryGetValue(HashKey(ix, iy), out var list))
                        foreach (int id in list) { var p = Parcels[id]; if (p.Alive && Dist(p.X, p.Y, x, y) < minDist) return true; }
            return false;
        }

        private void AssignAccess(Parcel p, RoadEdge e, float t)
        {
            p.AccessNode = t < 0.5f ? e.A : e.B; p.AccessDist = (t < 0.5f ? t : 1f - t) * e.Length;
        }

        private void GenerateParcels(RoadEdge e)
        {
            float depth = RoadSpecs.LotDepth(e.Type); if (depth <= 0f || e.Length < 2 * EndClear + LotWidth) return;
            var a = Nodes[e.A]; var b = Nodes[e.B]; float dx = (b.X - a.X) / e.Length, dy = (b.Y - a.Y) / e.Length;
            float nx = -dy, ny = dx, half = RoadSpecs.Width(e.Type) * 0.5f; int n = (int)Math.Floor((e.Length - 2 * EndClear) / LotWidth);
            for (int i = 0; i < n; i++)
            {
                float along = EndClear + LotWidth * (i + 0.5f), px = a.X + dx * along, py = a.Y + dy * along;
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = px + nx * side * (half + depth * 0.5f), y = py + ny * side * (half + depth * 0.5f);
                    if (!InBounds(x, y) || !Terrain.Buildable(x, y) || ParcelNear(x, y, LotWidth * 0.75f)) continue;
                    bool blocked = false;
                    foreach (var o in Edges)
                    {
                        if (!o.Alive || o.Id == e.Id) continue;
                        var oa = Nodes[o.A]; var ob = Nodes[o.B];
                        if (PointSegDist(x, y, oa.X, oa.Y, ob.X, ob.Y, out float _) < RoadSpecs.Width(o.Type) * 0.5f + depth * 0.5f - 2f) { blocked = true; break; }
                    }
                    if (blocked) continue;
                    var p = new Parcel { Id = Parcels.Count, X = x, Y = y, EdgeId = e.Id };
                    AssignAccess(p, e, along / e.Length);
                    p.AccessDist += half + depth * 0.5f;
                    Parcels.Add(p);
                    int cx = (int)Math.Floor(x / 20f), cy = (int)Math.Floor(y / 20f);
                    if (!_parcelHash.TryGetValue(HashKey(cx, cy), out var list)) { list = new List<int>(); _parcelHash[HashKey(cx, cy)] = list; }
                    list.Add(p.Id);
                }
            }
        }

        // ------------------------------------------------------------------ lệnh: phân khu, dịch vụ, phá bỏ
        public Result ZoneArea(float cx, float cy, float radius, Zone zone)
        {
            int n = 0;
            foreach (var p in Parcels)
            {
                if (!p.Alive || p.Building >= 0 || Dist(p.X, p.Y, cx, cy) > radius) continue;
                if (p.Zone != zone) { p.Zone = zone; n++; }
            }
            return Result.Success(n);
        }

        public Result ZoneParcel(int parcelId, Zone zone)
        {
            if (parcelId < 0 || parcelId >= Parcels.Count || !Parcels[parcelId].Alive) return Result.Fail("Lô đất không tồn tại");
            var p = Parcels[parcelId]; if (p.Building >= 0) return Result.Fail("Lô đã có công trình");
            p.Zone = zone; return Result.Success(1);
        }

        public Result PlaceService(int parcelId, ServiceKind kind)
        {
            if (parcelId < 0 || parcelId >= Parcels.Count || !Parcels[parcelId].Alive) return Result.Fail("Lô đất không tồn tại");
            var p = Parcels[parcelId]; if (p.Building >= 0) return Result.Fail("Lô đã có công trình");
            var b = new Building { Id = Buildings.Count, Kind = (BuildingKind)((int)BuildingKind.School + (int)kind), Parcel = p.Id, BuiltWeek = Week };
            Buildings.Add(b); p.Building = b.Id; p.Zone = Zone.None; return Result.Success(1);
        }

        public Result Demolish(int parcelId)
        {
            if (parcelId < 0 || parcelId >= Parcels.Count || !Parcels[parcelId].Alive) return Result.Fail("Lô đất không tồn tại");
            var p = Parcels[parcelId]; if (p.Building < 0) return Result.Fail("Lô không có công trình");
            Buildings[p.Building].Alive = false; p.Building = -1; return Result.Success(1);
        }

        /// <summary>Lô đất trống gần (x,y) nhất, tùy chọn chỉ lấy lô chưa phân khu.</summary>
        public int NearestFreeParcel(float x, float y, bool unzonedOnly = true)
        {
            int best = -1; float bd = float.MaxValue;
            foreach (var p in Parcels)
            {
                if (!p.Alive || p.Building >= 0 || (unzonedOnly && p.Zone != Zone.None)) continue;
                float d = Dist(p.X, p.Y, x, y); if (d < bd) { bd = d; best = p.Id; }
            }
            return best;
        }
    }
}
