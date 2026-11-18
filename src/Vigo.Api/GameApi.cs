using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Vigo.Core;
using Vigo.Core.CityBuilder;
using Vigo.Core.Economy;
using Vigo.Core.Military;

namespace Vigo.Api
{
    /// <summary>
    /// API trò chơi không phụ thuộc máy chủ web: nhận (method, đường dẫn, JSON) và trả JSON.
    /// Dùng chung cho bản chạy trình duyệt (Kestrel) và bản Android (WebView gọi thẳng, không cần mạng).
    /// </summary>
    public sealed class GameApi
    {
        private readonly object _gate = new object();
        private GameSession _game;
        private readonly string _saveDir;
        private readonly Dictionary<string, Func<JsonElement, object>> Post = new Dictionary<string, Func<JsonElement, object>>();
        private readonly Dictionary<string, Func<Dictionary<string, string>, object>> Get = new Dictionary<string, Func<Dictionary<string, string>, object>>();
        private static readonly JsonSerializerOptions Opt = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        private static double R1(double v) => Math.Round(v, 1);
        private static double R3(double v) => Math.Round(v, 3);

        public GameApi(string saveDir)
        {
            _saveDir = saveDir; var saveDirL = saveDir;
            GameSession G() { if (_game == null) throw new InvalidOperationException("chưa có ván chơi"); return _game; }
            Func<GameSession> get = () => _game; Action<GameSession> set = g => _game = g;
            double D(JsonElement j, string k, double d = 0) => j.ValueKind == JsonValueKind.Object && j.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : d;
            string S(JsonElement j, string k) => j.ValueKind == JsonValueKind.Object && j.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            string saveDir2 = saveDirL;

            Get["/api/hello"] = q => new { ok = true, hasGame = get() != null };

            Post["/api/new"] = body =>
            {
                ulong seed = (ulong)D(body, "seed", 12345); int cells = (int)D(body, "cells", 20000); int nc = (int)D(body, "countries", 30);
                cells = Math.Max(4000, Math.Min(60000, cells));
                var g = GameSession.New(new WorldConfig { Seed = seed, CellCount = cells, CountryCount = nc }, new CountryId(0));
                set(g); return new { ok = true };
            };

            Get["/api/map"] = q =>
            {
                var w = G().World; int n = w.CellCount; var gr = w.Grid;
                var x = new double[n]; var y = new double[n]; var elev = new int[n]; var flags = new int[n];
                for (int i = 0; i < n; i++)
                {
                    x[i] = R1(gr.X[i]); y[i] = R1(gr.Y[i]); elev[i] = (int)Math.Round(w.Elevation[i] * 100);
                    flags[i] = (w.IsOcean[i] ? 1 : 0) | (w.IsLake[i] ? 2 : 0) | (w.IsRiver[i] ? 4 : 0) | (w.IsRoad[i] ? 8 : 0);
                }
                return new { ok = true, width = w.Config.MapWidth, height = w.Config.MapHeight, spacing = gr.Spacing, n, x, y, elev, biome = w.Biomes.Select(b => (int)b).ToArray(), flags, province = w.ProvinceOfCell };
            };

            Get["/api/world"] = q =>
            {
                var g = G(); var w = g.World;
                return new
                {
                    ok = true, player = g.Player.Value, week = g.Eco.S.Week,
                    countries = w.Countries.Select(c => new { id = c.Id.Value, name = c.Name, personality = c.Personality.ToString(), pop = c.Population, provinces = c.Provinces.Count, port = c.HasPort, cx = R1(c.CentroidX), cy = R1(c.CentroidY), flag = new[] { c.Flag.Primary, c.Flag.Secondary, c.Flag.Accent }, pattern = (int)c.Flag.Pattern }),
                    provinces = w.Provinces.Select(p => new { id = p.Id.Value, name = p.Name, cx = R1(p.CentroidX), cy = R1(p.CentroidY) }),
                    cities = w.Cities.Select(c => new { id = c.Id.Value, name = c.Name, x = R1(c.X), y = R1(c.Y), prov = c.Province.Value, port = c.IsPort, capital = c.IsCapital, seat = c.IsProvincialSeat })
                };
            };

            Post["/api/player"] = body => new { ok = G().ChoosePlayer(new CountryId((int)D(body, "country"))) };

            Get["/api/state"] = q => State(G());

            Post["/api/step"] = body =>
            {
                var g = G(); int n = Math.Max(1, Math.Min(260, (int)D(body, "n", 1))); int done = 0;
                int before = g.Eco.S.Week / 208; for (int i = 0; i < n; i++) { if (!g.StepWeek()) break; done++; }
                if (g.Eco.S.Week / 208 != before && !g.GameOver) { Directory.CreateDirectory(saveDir2); g.Save(Path.Combine(saveDir2, "tudongluu.vigo")); } // tự lưu mỗi 4 năm
                return new { ok = true, done, state = State(g) };
            };

            Post["/api/tax"] = body => { var g = G(); g.SetTax(D(body, "rate", 0.25)); return new { ok = true, state = State(g) }; };
            Post["/api/budget"] = body =>
            {
                var g = G(); g.SetBudget(D(body, "edu"), D(body, "health"), D(body, "invest"), D(body, "defense")); return new { ok = true, state = State(g) };
            };

            Post["/api/diplo"] = body =>
            {
                var g = G(); string a = S(body, "action"); var t = new CountryId((int)D(body, "target", -1)); bool ok;
                switch (a)
                {
                    case "war": ok = g.DeclareWar(t, D(body, "breakTreaties") > 0); break;
                    case "peace": ok = g.ProposePeace(t); break;
                    case "alliance": ok = g.ProposeAlliance(t); break;
                    case "pact": ok = g.ProposePact(t); break;
                    case "trade": ok = g.ProposeTrade(t); break;
                    case "breakAlliance": g.BreakAlliance(t); ok = true; break;
                    default: throw new InvalidOperationException("lệnh ngoại giao không biết: " + a);
                }
                return new { ok, state = State(g) };
            };

            Post["/api/offer"] = body =>
            {
                var g = G(); int id = (int)D(body, "id"); bool ok = D(body, "accept") > 0 ? g.AcceptOffer(id) : g.DeclineOffer(id);
                return new { ok, state = State(g) };
            };

            Get["/api/saves"] = q =>
            {
                Directory.CreateDirectory(saveDir2);
                return new { ok = true, saves = new DirectoryInfo(saveDir2).GetFiles("*.vigo").OrderByDescending(f => f.LastWriteTimeUtc).Select(f => new { name = Path.GetFileNameWithoutExtension(f.Name), size = f.Length, time = f.LastWriteTimeUtc.ToString("s") }) };
            };
            Post["/api/save"] = body =>
            {
                var g = G(); string name = SafeName(S(body, "name") ?? "autosave"); Directory.CreateDirectory(saveDir2); g.Save(Path.Combine(saveDir2, name + ".vigo")); return new { ok = true, name };
            };
            Post["/api/load"] = body =>
            {
                string name = SafeName(S(body, "name") ?? ""); var path = Path.Combine(saveDir2, name + ".vigo"); if (!File.Exists(path)) throw new InvalidOperationException("không có bản lưu " + name);
                var g = GameSession.Load(path); set(g); return new { ok = true };
            };
            Post["/api/rewind"] = body => { var g = G(); bool ok = g.Rewind((int)D(body, "week")); return new { ok, state = State(g) }; };
            Get["/api/history"] = q => new { ok = true, weeks = G().History.Weeks() };

            // ---------------- Thành phố chi tiết ----------------
            Post["/api/city/open"] = body =>
            {
                var g = G(); var id = new CityId((int)D(body, "id")); g.OpenCity(id, (ulong)(id.Value + 1));
                var sim = g.Cities.First(c => c.Id == id).Sim; if (sim.Buildings.Count == 0 && sim.Edges.Count == 0 && D(body, "demo") > 0) DemoScenario.BuildTown(sim);
                return new { ok = true, city = CityJson(g, id) };
            };
            Get["/api/city"] = q => { var g = G(); return new { ok = true, city = CityJson(g, new CityId(int.Parse(q["id"]))) }; };
            Post["/api/city/cmd"] = body =>
            {
                var g = G(); var id = new CityId((int)D(body, "id")); var ent = g.Cities.FirstOrDefault(c => c.Id == id); if (ent == null) throw new InvalidOperationException("thành phố chưa mở");
                var sim = ent.Sim; string cmd = S(body, "cmd"); Result r;
                switch (cmd)
                {
                    case "road": r = sim.BuildRoad((float)D(body, "x1"), (float)D(body, "y1"), (float)D(body, "x2"), (float)D(body, "y2"), (RoadType)(int)D(body, "type")); break;
                    case "zone": r = sim.ZoneArea((float)D(body, "x"), (float)D(body, "y"), (float)D(body, "r", 40), (Zone)(int)D(body, "zone")); break;
                    case "service": { int pid = sim.NearestFreeParcel((float)D(body, "x"), (float)D(body, "y"), false); r = pid < 0 ? Result.Fail("Không có lô trống gần đó") : sim.PlaceService(pid, (ServiceKind)(int)D(body, "kind")); break; }
                    case "demolish": { int pid = NearestParcel(sim, (float)D(body, "x"), (float)D(body, "y")); r = pid < 0 ? Result.Fail("Không có gì ở đó") : sim.Demolish(pid); break; }
                    default: throw new InvalidOperationException("lệnh thành phố không biết: " + cmd);
                }
                return new { ok = r.Ok, error = r.Error, count = r.Count, city = CityJson(g, id) };
            };
        }

        /// <summary>Xử lý một yêu cầu. Trả (mã trạng thái, JSON).</summary>
        public (int status, string json) Handle(string method, string url, string bodyJson)
        {
            string path = url, query = ""; int qi = url.IndexOf('?'); if (qi >= 0) { path = url.Substring(0, qi); query = url.Substring(qi + 1); }
            lock (_gate)
            {
                try
                {
                    object r;
                    if (method == "GET")
                    {
                        if (!Get.TryGetValue(path, out var h)) return (404, "{\"ok\":false,\"error\":\"không có đường dẫn\"}");
                        var q = new Dictionary<string, string>(); foreach (var kv in query.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries)) { var p = kv.Split('='); q[Uri.UnescapeDataString(p[0])] = p.Length > 1 ? Uri.UnescapeDataString(p[1]) : ""; }
                        r = h(q);
                    }
                    else
                    {
                        if (!Post.TryGetValue(path, out var h)) return (404, "{\"ok\":false,\"error\":\"không có đường dẫn\"}");
                        JsonElement body = default; if (!string.IsNullOrWhiteSpace(bodyJson)) { using (var doc = JsonDocument.Parse(bodyJson)) body = doc.RootElement.Clone(); }
                        r = h(body);
                    }
                    return (200, JsonSerializer.Serialize(r, Opt));
                }
                catch (Exception e) { return (400, JsonSerializer.Serialize(new { ok = false, error = e.Message }, Opt)); }
            }
        }

        private static string SafeName(string s) { var ok = new string((s ?? "").Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray()); return ok.Length == 0 ? "save" : ok.Substring(0, Math.Min(40, ok.Length)); }

        private static int NearestParcel(CitySim sim, float x, float y)
        {
            int best = -1; float bd = 30 * 30;
            foreach (var p in sim.Parcels) { if (!p.Alive) continue; float dx = p.X - x, dy = p.Y - y, d = dx * dx + dy * dy; if (d < bd && (p.Building >= 0 || p.Zone != Zone.None)) { bd = d; best = p.Id; } }
            return best;
        }

        public static object CityJson(GameSession g, CityId id)
        {
            var ent = g.Cities.First(c => c.Id == id); var sim = ent.Sim; var city = g.World.GetCity(id); var i = sim.Indicators;
            return new
            {
                id = id.Value, name = city.Name, size = sim.Size, week = sim.Week,
                water = Water(sim), slope = Slope(sim), seed = id.Value * 7919 + 13,
                nodes = sim.Nodes.Select(n => new[] { R1(n.X), R1(n.Y) }),
                edges = sim.Edges.Where(e => e.Alive).Select(e => new[] { e.A, e.B, (int)e.Type, e.IsBridge ? 1 : 0, e.Id }),
                parcels = sim.Parcels.Where(p => p.Alive).Select(p => new double[] { R1(p.X), R1(p.Y), (int)p.Zone, p.Building, p.EdgeId }),
                buildings = sim.Buildings.Where(b => b.Alive).Select(b => new[] { b.Id, (int)b.Kind, b.Level, b.Parcel, b.Residents, b.Workers }),
                indicators = new { pop = i.Population, housing = i.Housing, jobs = i.Jobs, employed = i.Employed, unemployment = R3(i.UnemploymentRate), demandR = R3(i.DemandR), demandC = R3(i.DemandC), demandI = R3(i.DemandI), qol = R3(i.QoL), qolLow = R3(i.QoLLowestQuintile), health = R3(i.Health), education = R3(i.Education), safety = R3(i.Safety), commute = R3(i.Commute), environment = R3(i.Environment), leisure = R3(i.Leisure), pollution = R3(i.Pollution), crime = R3(i.Crime) }
            };
        }

        // Dò vùng nước (lưới 20 m) để vẽ sông/biển trong thành phố, gửi dạng danh sách ô có nước.
        private static object Water(CitySim sim)
        {
            var l = new List<int>(); int step = 20; int n = (int)(sim.Size / step);
            for (int gy = 0; gy < n; gy++) for (int gx = 0; gx < n; gx++) if (sim.Terrain.IsWater(gx * step + step / 2f, gy * step + step / 2f)) l.Add(gy * n + gx);
            return new { step, n, cells = l };
        }

        // Độ dốc địa hình (lưới 20 m, 0..100) để vẽ đồi núi; chỉ phục vụ hiển thị.
        private static int[] Slope(CitySim sim)
        {
            int step = 20, n = (int)(sim.Size / step); var a = new int[n * n];
            for (int gy = 0; gy < n; gy++) for (int gx = 0; gx < n; gx++) a[gy * n + gx] = (int)Math.Round(100 * Math.Max(0f, Math.Min(1f, sim.Terrain.Slope(gx * step + step / 2f, gy * step + step / 2f))));
            return a;
        }

        public static object State(GameSession g)
        {
            var w = g.World; var eco = g.Eco; var war = g.War; int me = g.Player.Value; var st = g.Status(); var ind = eco.S.Indicators[me];
            var owners = new int[w.Provinces.Count]; for (int i = 0; i < owners.Length; i++) owners[i] = w.Provinces[i].Owner.Value;
            return new
            {
                ok = true, week = st.Week, year = 1 + st.Week / 52, gameOver = st.GameOver, player = me, country = st.Country,
                me = new
                {
                    pop = st.Population, treasury = Math.Round(st.Treasury), tax = R3(st.TaxRate), provinces = st.Provinces, wars = st.Wars, allies = st.Allies, offers = st.PendingOffers,
                    food = R3(ind.FoodSat), fuel = R3(ind.FuelSat), goods = R3(ind.GoodsSat), edu = R3(ind.Edu), health = R3(ind.Health), valueAdded = Math.Round(ind.ValueAdded), taxRevenue = Math.Round(ind.TaxRevenue),
                    troops = Math.Round(eco.S.Troops[me]), power = Math.Round(war.Power(me)), cityQoL = R3(st.AvgCityQoL),
                    budget = new { edu = R3(eco.S.ShareEdu[me]), health = R3(eco.S.ShareHealth[me]), invest = R3(eco.S.ShareInvest[me]), defense = R3(eco.S.ShareDefense[me]) }
                },
                owners,
                countries = w.Countries.Select(c => new
                {
                    id = c.Id.Value, pop = c.Population, prov = c.Provinces.Count, elim = c.Eliminated,
                    opinion = Math.Round(war.GetOpinion(c.Id.Value, me)), allied = war.Allied(me, c.Id.Value), pact = war.HasPact(me, c.Id.Value), trade = war.HasTrade(me, c.Id.Value), atWar = war.AtWar(me, c.Id.Value),
                    neighbor = w.GetCountry(g.Player).Neighbors.Contains(c.Id), sea = war.SeaAdjacent(me, c.Id.Value), power = Math.Round(war.Power(c.Id.Value))
                }),
                wars = war.Wars.Select(x => new { a = x.A, b = x.B, start = x.StartWeek, goal = x.Goal, captured = x.Captured }),
                treaties = war.Treaties.Select(t => new { type = (int)t.Type, a = t.A, b = t.B, expire = t.Expire }),
                offers = g.Inbox().Select(o => new { id = o.Id, type = (int)o.Type, from = o.From, expire = o.Expire }),
                events = war.Events.Skip(Math.Max(0, war.Events.Count - 25)).Select(e => new { week = e.Week, kind = e.Kind, a = e.A, b = e.B, text = e.Text }),
                cities = w.Cities.Select(c => new { id = c.Id.Value, pop = c.Population, owner = w.GetProvince(c.Province).Owner.Value, open = g.Cities.Any(x => x.Id == c.Id) })
            };
        }
    }
}
