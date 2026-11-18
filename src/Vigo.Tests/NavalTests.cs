using System;
using System.Linq;
using Vigo.Core;
using Vigo.Core.Economy;
using Vigo.Core.Military;

namespace Vigo.Tests
{
    internal static class NavalTests
    {
        private static WorldState Gen(ulong seed) => WorldGenerator.Generate(new WorldConfig { Seed = seed, CellCount = 20000 });
        // tìm một thế giới có cặp nước kề biển mà cả hai không giáp đất liền, nước tấn công có cảng
        private static bool FindSeaPair(out WorldState w, out EconomySim e, out WarSim war, out int a, out int b)
        {
            foreach (ulong sd in new ulong[] { 7919, 31676, 23757, 15838 })
            {
                w = Gen(sd); e = new EconomySim(w); war = new WarSim(w, e);
                foreach (var c in w.Countries)
                    foreach (int j in war.SeaNeighbors(c.Id.Value))
                        if (c.HasPort && !c.Neighbors.Contains(new CountryId(j))) { a = c.Id.Value; b = j; return true; }
            }
            w = null; e = null; war = null; a = b = -1; return false;
        }

        public static void Register(Action<string, Action> add, Action<bool, string> assert)
        {
            add("Hải quân: liên kết biển đối xứng, chỉ giữa tỉnh khác nhau, không trùng láng giềng đất liền của nước (SeaNeighbors)", () =>
            {
                var w = Gen(7919); var e = new EconomySim(w); var war = new WarSim(w, e); int total = 0;
                for (int p = 0; p < w.Provinces.Count; p++) foreach (int q in war.SeaLinks[p]) { total++; assert(q != p, "liên kết tự thân"); assert(war.SeaLinks[q].Contains(p), "liên kết biển không đối xứng"); }
                assert(total > 0, "không có liên kết biển nào");
                foreach (var c in w.Countries) foreach (int j in war.SeaNeighbors(c.Id.Value)) assert(!c.Neighbors.Contains(new CountryId(j)) && j != c.Id.Value, "SeaNeighbors lẫn láng giềng đất liền");
            });

            add("Hải quân: tuyên chiến qua biển chỉ khi có cảng và kề biển; nước không kề biển/không giáp thì không", () =>
            {
                assert(FindSeaPair(out var w, out var e, out var war, out int a, out int b), "không tìm được cặp nước kề biển để thử");
                bool hadPort = w.Countries[a].HasPort; w.Countries[a].HasPort = false;
                assert(!war.DeclareWar(new CountryId(a), new CountryId(b)), "không có cảng mà vẫn đổ bộ");
                w.Countries[a].HasPort = true;
                // một nước không giáp đất liền, không kề biển
                var far = w.Countries.FirstOrDefault(c => c.Id.Value != a && c.Id.Value != b && !w.Countries[a].Neighbors.Contains(c.Id) && !war.SeaAdjacent(a, c.Id.Value));
                if (far != null) assert(!war.DeclareWar(new CountryId(a), far.Id), "tuyên chiến được với nước xa không kề");
                assert(war.DeclareWar(new CountryId(a), new CountryId(b)), "có cảng, kề biển mà không tuyên chiến được");
                assert(war.AtWar(a, b), "chưa vào chiến tranh");
            });

            add("Hải quân: đổ bộ kém hiệu quả — ngang sức thì không chiếm được tỉnh nào, áp đảo thì chiếm được; bất biến thế giới giữ nguyên", () =>
            {
                assert(FindSeaPair(out var w, out var e, out var war, out int a, out int b), "không có cặp nước kề biển");
                e.S.Troops[a] = 1e6; e.S.Troops[b] = 1e6; assert(war.DeclareWar(new CountryId(a), new CountryId(b)), "không tuyên chiến được"); war.Wars[0].Goal = 100;
                int pa = w.Countries[a].Provinces.Count, pb = w.Countries[b].Provinces.Count;
                for (int i = 0; i < 60; i++) { war.StepWeek(); e.S.Troops[a] = Math.Max(e.S.Troops[a], 1e6); e.S.Troops[b] = Math.Max(e.S.Troops[b], 1e6); }
                assert(w.Countries[b].Provinces.Count == pb, "ngang sức mà đổ bộ đã chiếm được tỉnh");
                var (w2, e2, war2) = (w, e, war);
                var w3 = Gen(w.Seed); var e3 = new EconomySim(w3); var war3 = new WarSim(w3, e3);
                e3.SetBudget(new CountryId(a), 0.05, 0.05, 0.05, 0.85); e3.SetBudget(new CountryId(b), 0.3, 0.3, 0.4, 0.0);
                e3.S.Troops[a] = 0.12 * w3.Countries[a].Population; e3.S.Troops[b] = 0.001 * w3.Countries[b].Population;
                war3.DeclareWar(new CountryId(a), new CountryId(b)); war3.Wars[0].Goal = 100;
                int before = w3.Countries[b].Provinces.Count;
                for (int i = 0; i < 600 && w3.Countries[b].Provinces.Count == before && !w3.Countries[b].Eliminated; i++) { e3.StepWeek(); war3.StepWeek(); }
                Console.Write($" [áp đảo: {before} → {w3.Countries[b].Provinces.Count} tỉnh; sự kiện: {string.Join(" / ", war3.Events.Select(x => x.Kind + ":" + x.Text).Take(6))}]");
                assert(w3.Countries[b].Provinces.Count < before || w3.Countries[b].Eliminated, "áp đảo mà không đổ bộ được");
                MilitaryTests.Invariants(w3, assert); MilitaryTests.Invariants(w, assert);
            });
        }
    }
}
