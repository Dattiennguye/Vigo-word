using System;
using System.Collections.Generic;

namespace Vigo.Core.CityBuilder
{
    public sealed partial class CitySim
    {
        public CityIndicators Indicators { get; private set; }

        // Trọng số chất lượng sống cho các thành phần ĐÃ được mô phỏng. Thành phần chưa có (lương thực/nước, thu nhập, nhà ở giá rẻ,
        // hòa bình) sẽ được thêm khi kinh tế (GĐ 3) và chiến tranh (GĐ 7) hoạt động; trọng số sẽ được chuẩn hóa lại.
        public static readonly string[] ModeledQoLComponents = { "Việc làm", "Y tế", "Giáo dục", "An ninh", "Đi lại", "Môi trường", "Giải trí" };
        private const float WEmp = 0.20f, WHealth = 0.15f, WEdu = 0.12f, WSafe = 0.15f, WCommute = 0.10f, WEnv = 0.15f, WLeis = 0.13f;

        private bool[] _connected; // nút có nối tới nút thoát (quốc lộ) hay không
        private float _unemployment;

        /// <summary>Một tuần game. Thứ tự: kết nối → phủ dịch vụ/chỉ số → nhu cầu → xây dựng → dân cư → việc làm → chỉ số mới.</summary>
        public void StepWeek()
        {
            Week++;
            ComputeConnectivity();
            ComputeCoverage();
            var ind = ComputeIndicators();
            Grow(ind);
            MovePopulation(ind);
            AssignJobs();
            ComputeCoverage();
            Indicators = ComputeIndicators();
        }

        public void StepWeeks(int n) { for (int i = 0; i < n; i++) StepWeek(); }

        // ------------------------------------------------------------------ kết nối
        private void ComputeConnectivity()
        {
            _connected = new bool[Nodes.Count];
            var st = new Stack<int>(); st.Push(ExitNode); _connected[ExitNode] = true;
            while (st.Count > 0)
            {
                int u = st.Pop();
                foreach (int eid in Nodes[u].Edges)
                {
                    var e = Edges[eid]; if (!e.Alive) continue;
                    int v = e.A == u ? e.B : e.A;
                    if (!_connected[v]) { _connected[v] = true; st.Push(v); }
                }
            }
        }

        private bool IsConnected(Parcel p) { return p.AccessNode < _connected.Length && _connected[p.AccessNode]; }

        /// <summary>Dijkstra trên đồ thị đường từ một nút, giới hạn quãng đường (m).</summary>
        private float[] RoadDistances(int source, float initial, float limit)
        {
            var dist = new float[Nodes.Count]; for (int i = 0; i < dist.Length; i++) dist[i] = float.MaxValue;
            var heap = new MinHeap(); dist[source] = initial; heap.Push(source, initial);
            while (heap.Count > 0)
            {
                int u = heap.Pop(out float d); if (d > dist[u]) continue;
                foreach (int eid in Nodes[u].Edges)
                {
                    var e = Edges[eid]; if (!e.Alive) continue;
                    int v = e.A == u ? e.B : e.A; float nd = d + e.Length;
                    if (nd <= limit && nd < dist[v]) { dist[v] = nd; heap.Push(v, nd); }
                }
            }
            return dist;
        }

        // ------------------------------------------------------------------ phủ dịch vụ, ô nhiễm, quãng đường đi làm
        private void ComputeCoverage()
        {
            var res = new List<Building>(); var services = new List<Building>(); var jobs = new List<Building>(); var industry = new List<Building>();
            foreach (var b in Buildings)
            {
                if (!b.Alive) continue;
                if (BuildingSpecs.IsResidential(b.Kind)) res.Add(b);
                else if (BuildingSpecs.IsService(b.Kind)) services.Add(b);
                if (b.Kind == BuildingKind.Commercial || b.Kind == BuildingKind.Industrial) jobs.Add(b);
                if (b.Kind == BuildingKind.Industrial) industry.Add(b);
            }
            foreach (var r in res) { r.CovEdu = r.CovHealth = r.CovSafety = r.CovLeisure = 0f; }

            foreach (var s in services)
            {
                var sp = Parcels[s.Parcel]; if (!IsConnected(sp)) continue;
                float reach = BuildingSpecs.Reach(s.Kind);
                var dist = RoadDistances(sp.AccessNode, sp.AccessDist, reach);
                float load = 0f; var contrib = new List<KeyValuePair<Building, float>>();
                foreach (var r in res)
                {
                    var rp = Parcels[r.Parcel]; float d = dist[rp.AccessNode];
                    if (d == float.MaxValue) continue; d += rp.AccessDist; if (d > reach) continue;
                    load += r.Residents; contrib.Add(new KeyValuePair<Building, float>(r, 1f - d / reach));
                }
                float strength = Math.Min(1f, BuildingSpecs.Capacity(s.Kind) / Math.Max(1f, load));
                foreach (var kv in contrib)
                {
                    float c = kv.Value * strength; var r = kv.Key;
                    switch (s.Kind)
                    {
                        case BuildingKind.School: r.CovEdu = Math.Min(1f, r.CovEdu + c); break;
                        case BuildingKind.Hospital: r.CovHealth = Math.Min(1f, r.CovHealth + c); break;
                        case BuildingKind.Police: r.CovSafety = Math.Min(1f, r.CovSafety + c); break;
                        default: r.CovLeisure = Math.Min(1f, r.CovLeisure + c); break;
                    }
                }
            }

            // Ô nhiễm từ công nghiệp theo khoảng cách thẳng.
            foreach (var r in res)
            {
                var rp = Parcels[r.Parcel]; float pol = 0f;
                foreach (var i in industry)
                {
                    var ip = Parcels[i.Parcel]; float d = Dist(rp.X, rp.Y, ip.X, ip.Y);
                    if (d < 300f) pol += i.Level * 0.5f * (1f - d / 300f);
                }
                r.Pollution = Math.Min(1f, pol);
            }

            // Quãng đường đi làm: nguồn đa điểm từ mọi nơi có việc làm, đo trên mạng đường (tốc độ tự do — chưa mô phỏng ùn tắc).
            var commute = new float[Nodes.Count]; for (int i = 0; i < commute.Length; i++) commute[i] = float.MaxValue;
            var heap = new MinHeap();
            foreach (var j in jobs)
            {
                var jp = Parcels[j.Parcel]; if (!IsConnected(jp)) continue;
                if (jp.AccessDist < commute[jp.AccessNode]) { commute[jp.AccessNode] = jp.AccessDist; heap.Push(jp.AccessNode, jp.AccessDist); }
            }
            while (heap.Count > 0)
            {
                int u = heap.Pop(out float d); if (d > commute[u]) continue;
                foreach (int eid in Nodes[u].Edges)
                {
                    var e = Edges[eid]; if (!e.Alive) continue;
                    int v = e.A == u ? e.B : e.A; float nd = d + e.Length;
                    if (nd < commute[v]) { commute[v] = nd; heap.Push(v, nd); }
                }
            }
            foreach (var r in res)
            {
                var rp = Parcels[r.Parcel]; float d = commute[rp.AccessNode];
                r.CommuteDist = d == float.MaxValue ? float.MaxValue : d + rp.AccessDist;
            }
        }

        private static float C01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }

        /// <summary>Chất lượng sống của một khu nhà ở (0..1) từ các thành phần đã mô phỏng.</summary>
        private float BuildingQuality(Building r, float unemployment)
        {
            float emp = 1f - C01(unemployment / 0.25f);
            float crime = C01(0.15f + 0.5f * unemployment - 0.45f * r.CovSafety);
            float commute = r.CommuteDist == float.MaxValue ? 0f : 1f - C01(r.CommuteDist / 2500f);
            float env = 1f - r.Pollution;
            return WEmp * emp + WHealth * r.CovHealth + WEdu * r.CovEdu + WSafe * (1f - crime) + WCommute * commute + WEnv * env + WLeis * r.CovLeisure;
        }

        private CityIndicators ComputeIndicators()
        {
            var ind = new CityIndicators { Week = Week };
            int pop = 0, housing = 0, jobs = 0; var res = new List<Building>();
            foreach (var b in Buildings)
            {
                if (!b.Alive) continue; ind.Buildings++;
                if (BuildingSpecs.IsResidential(b.Kind)) { pop += b.Residents; housing += b.Housing; res.Add(b); }
                jobs += b.Jobs;
            }
            foreach (var p in Parcels)
                if (p.Alive && p.Zone != Zone.None) { ind.ZonedParcels++; if (p.Building < 0) ind.EmptyZonedParcels++; if (IsConnected(p)) ind.ConnectedParcels++; }
            ind.Population = pop; ind.Housing = housing; ind.Jobs = jobs;
            float workforce = pop * 0.6f; int employed = (int)Math.Min(workforce, jobs);
            ind.Employed = employed; ind.UnemploymentRate = workforce > 0 ? (workforce - employed) / workforce : 0f;
            _unemployment = ind.UnemploymentRate;

            float sumQ = 0, sEmp = 0, sH = 0, sE = 0, sS = 0, sC = 0, sEnv = 0, sL = 0, sPol = 0, sCr = 0;
            var qs = new List<KeyValuePair<float, int>>();
            foreach (var r in res)
            {
                r.Quality = BuildingQuality(r, ind.UnemploymentRate);
                if (r.Residents <= 0) continue;
                float w = r.Residents, crime = C01(0.15f + 0.5f * ind.UnemploymentRate - 0.45f * r.CovSafety);
                sumQ += w * r.Quality; sEmp += w * (1f - C01(ind.UnemploymentRate / 0.25f)); sH += w * r.CovHealth; sE += w * r.CovEdu; sS += w * (1f - crime);
                sC += w * (r.CommuteDist == float.MaxValue ? 0f : 1f - C01(r.CommuteDist / 2500f)); sEnv += w * (1f - r.Pollution); sL += w * r.CovLeisure; sPol += w * r.Pollution; sCr += w * crime;
                qs.Add(new KeyValuePair<float, int>(r.Quality, r.Residents));
            }
            if (pop > 0)
            {
                ind.QoL = sumQ / pop; ind.Employment = sEmp / pop; ind.Health = sH / pop; ind.Education = sE / pop; ind.Safety = sS / pop;
                ind.Commute = sC / pop; ind.Environment = sEnv / pop; ind.Leisure = sL / pop; ind.Pollution = sPol / pop; ind.Crime = sCr / pop;
                qs.Sort((a, b) => a.Key.CompareTo(b.Key));
                float target = pop * 0.2f, acc = 0f, sum = 0f;
                foreach (var kv in qs) { float take = Math.Min(kv.Value, target - acc); if (take <= 0) break; sum += take * kv.Key; acc += take; }
                ind.QoLLowestQuintile = acc > 0 ? sum / acc : 0f;
            }

            // Nhu cầu R/C/I.
            float jobPull = jobs > 0 || pop > 0 ? Math.Max(-1f, Math.Min(1f, (jobs - workforce) / Math.Max(20f, jobs))) : 0f;
            float vacancy = housing > 0 ? (housing - pop) / (float)housing : 0f; // chưa có nhà nào ≠ dư nhà
            float quality = pop > 0 ? ind.QoL : 0.5f;
            ind.DemandR = C01(0.35f + 0.5f * jobPull - 0.6f * vacancy + 0.2f * (quality - 0.5f));
            int comJobs = 0; foreach (var b in Buildings) if (b.Alive && b.Kind == BuildingKind.Commercial) comJobs += b.Jobs;
            ind.DemandC = C01(0.3f + (pop / 10f - comJobs) / Math.Max(10f, pop / 10f));
            ind.DemandI = C01(0.3f + 1.5f * ind.UnemploymentRate + (pop > 0 ? 0f : 0.2f) - 0.5f * ind.Pollution);
            return ind;
        }

        // ------------------------------------------------------------------ xây dựng / nâng cấp
        private void Grow(CityIndicators ind)
        {
            GrowZone(Zone.ResidentialLow, ind.DemandR);
            GrowZone(Zone.ResidentialHigh, ind.DemandR * C01(((ind.Population > 0 ? ind.QoL : 0.5f) - 0.35f) * 3f));
            GrowZone(Zone.Commercial, ind.DemandC);
            GrowZone(Zone.Industrial, ind.DemandI);

            foreach (var b in Buildings)
            {
                if (!b.Alive || BuildingSpecs.IsService(b.Kind) || b.Level >= BuildingSpecs.MaxLevel) continue;
                float demand = b.Kind == BuildingKind.Commercial ? ind.DemandC : (b.Kind == BuildingKind.Industrial ? ind.DemandI : ind.DemandR);
                bool okQuality = !BuildingSpecs.IsResidential(b.Kind) || (b.CovHealth + b.CovEdu + b.CovSafety) / 3f >= 0.3f;
                bool full = !BuildingSpecs.IsResidential(b.Kind) || b.Residents >= b.Housing * 0.9f;
                if (demand > 0.55f && okQuality && full && _rng.Chance(0.04)) b.Level++;
            }
        }

        private void GrowZone(Zone zone, float demand)
        {
            if (demand <= 0.01f) return;
            var empty = new List<Parcel>();
            foreach (var p in Parcels) if (p.Alive && p.Zone == zone && p.Building < 0 && IsConnected(p)) empty.Add(p);
            if (empty.Count == 0) return;
            int n = (int)Math.Floor(demand * Math.Min(empty.Count, 10) + _rng.NextFloat());
            var existing = new List<Building>(); foreach (var b in Buildings) if (b.Alive) existing.Add(b);
            for (int i = 0; i < n && empty.Count > 0; i++)
            {
                // Ưu tiên lô gần công trình hiện có (thành phố lan dần từ lõi), có yếu tố ngẫu nhiên.
                float total = 0f; var w = new float[empty.Count];
                for (int k = 0; k < empty.Count; k++)
                {
                    float d = existing.Count == 0 ? Dist(empty[k].X, empty[k].Y, Size * 0.5f, Size * 0.5f) : 1e9f;
                    foreach (var b in existing) { var bp = Parcels[b.Parcel]; d = Math.Min(d, Dist(empty[k].X, empty[k].Y, bp.X, bp.Y)); }
                    w[k] = 1f / (1f + d / 60f); total += w[k];
                }
                float r = _rng.NextFloat() * total, acc = 0f; int pick = empty.Count - 1;
                for (int k = 0; k < empty.Count; k++) { acc += w[k]; if (acc >= r) { pick = k; break; } }
                var parcel = empty[pick]; empty.RemoveAt(pick);
                var bld = new Building { Id = Buildings.Count, Kind = BuildingSpecs.KindOf(zone), Parcel = parcel.Id, BuiltWeek = Week };
                Buildings.Add(bld); parcel.Building = bld.Id; existing.Add(bld);
            }
        }

        // ------------------------------------------------------------------ dân cư
        private void MovePopulation(CityIndicators ind)
        {
            var res = new List<Building>();
            foreach (var b in Buildings) if (b.Alive && BuildingSpecs.IsResidential(b.Kind)) res.Add(b);
            // Nhà nằm ngoài mạng kết nối: cư dân mất đường sống, bỏ đi dần.
            foreach (var r in res) if (!IsConnected(Parcels[r.Parcel]) && r.Residents > 0) r.Residents = (int)(r.Residents * 0.8f);

            int pop = 0; foreach (var r in res) pop += r.Residents;
            float attract = pop > 0 ? ind.QoL : 0.5f;
            float jobPull = Math.Max(-1f, Math.Min(1f, (ind.Jobs - pop * 0.6f) / Math.Max(20f, ind.Jobs)));

            var open = new List<Building>(); int vacancy = 0;
            foreach (var r in res) { int v = r.Housing - r.Residents; if (v > 0 && IsConnected(Parcels[r.Parcel])) { open.Add(r); vacancy += v; } }
            int incoming = (int)Math.Ceiling(vacancy * 0.10f * attract * (0.3f + 0.7f * C01(0.5f + jobPull)) * (1f - C01(ind.UnemploymentRate)));
            if (vacancy == 0) incoming = 0;
            open.Sort((a, b) => b.Quality.CompareTo(a.Quality));
            foreach (var r in open)
            {
                if (incoming <= 0) break;
                int take = Math.Min(incoming, r.Housing - r.Residents); r.Residents += take; incoming -= take;
            }

            // Di cư ra khi chất lượng sống thấp hoặc thất nghiệp cao.
            if (pop > 0 && (ind.QoL < 0.30f || ind.UnemploymentRate > 0.35f))
            {
                int leave = (int)Math.Ceiling(pop * 0.02f);
                res.Sort((a, b) => a.Quality.CompareTo(b.Quality));
                foreach (var r in res) { if (leave <= 0) break; int take = Math.Min(leave, r.Residents); r.Residents -= take; leave -= take; }
            }
        }

        private void AssignJobs()
        {
            int pop = 0, jobs = 0;
            foreach (var b in Buildings)
            {
                if (!b.Alive) continue;
                if (BuildingSpecs.IsResidential(b.Kind)) pop += b.Residents;
                jobs += b.Jobs;
            }
            float fill = jobs > 0 ? Math.Min(1f, pop * 0.6f / jobs) : 0f;
            foreach (var b in Buildings) if (b.Alive) b.Workers = (int)Math.Round(b.Jobs * fill);
        }
    }
}
