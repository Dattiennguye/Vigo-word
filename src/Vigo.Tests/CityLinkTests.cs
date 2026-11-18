using System;
using System.Linq;
using Vigo.Core;
using Vigo.Core.CityBuilder;
using Vigo.Core.Economy;
using Vigo.Core.Military;

namespace Vigo.Tests
{
    internal static class CityLinkTests
    {
        private static WorldState Gen(ulong seed) => WorldGenerator.Generate(new WorldConfig { Seed = seed, CellCount = 20000 });
        private static CitySim Town(NationalContext ctx, ulong seed = 5) { var s = new CitySim(new FlatTerrain(), seed); DemoScenario.BuildTown(s); s.National = ctx; return s; }

        public static void Register(Action<string, Action> add, Action<bool, string> assert)
        {
            add("Thành phố ↔ quốc gia: nạn đói kéo QoL và dân số thành phố xuống", () =>
            {
                var ok = Town(new NationalContext()); var famine = Town(new NationalContext { FoodSat = 0.3f });
                ok.StepWeeks(156); famine.StepWeeks(156);
                float qOk = ok.Indicators.QoL, qFam = famine.Indicators.QoL;
                Console.Write($" [QoL {qOk:0.00} vs đói {qFam:0.00}; dân {ok.Indicators.Population} vs {famine.Indicators.Population}]");
                assert(qFam < qOk - 0.1f, "đói mà QoL không giảm đủ: " + qOk + " vs " + qFam);
                assert(famine.Indicators.Population <= ok.Indicators.Population, "đói mà dân thành phố vẫn đông hơn");
            });

            add("Thành phố ↔ quốc gia: ngân sách y tế/giáo dục quốc gia nhân với độ phủ dịch vụ; tàn phá chiến tranh tăng tội phạm", () =>
            {
                var good = Town(new NationalContext { HealthQuality = 1, EduQuality = 1 }); var bad = Town(new NationalContext { HealthQuality = 0, EduQuality = 0 });
                good.StepWeeks(104); bad.StepWeeks(104);
                assert(good.Indicators.Health > 0.05f, "kịch bản không có phủ y tế để so");
                assert(Math.Abs(bad.Indicators.Health - 0.5f * good.Indicators.Health) < 0.05f * Math.Max(1f, good.Indicators.Health) + 0.02f, "y tế quốc gia kém phải giảm một nửa hiệu quả: " + good.Indicators.Health + " vs " + bad.Indicators.Health);
                assert(bad.Indicators.Education <= good.Indicators.Education + 1e-4f, "giáo dục");
                var calm = Town(new NationalContext()); var war = Town(new NationalContext { Devastation = 1f });
                calm.StepWeeks(52); war.StepWeeks(52);
                assert(war.Indicators.Crime > calm.Indicators.Crime + 0.1f && war.Indicators.Safety < calm.Indicators.Safety, "tàn phá không làm tăng tội phạm: " + calm.Indicators.Crime + " vs " + war.Indicators.Crime);
            });

            add("Thành phố ↔ quốc gia: không gán bối cảnh → như GĐ2; có bối cảnh hoàn hảo → QoL không thấp hơn", () =>
            {
                var alone = Town(null); var perfect = Town(new NationalContext());
                alone.StepWeeks(104); perfect.StepWeeks(104);
                assert(perfect.Indicators.QoL >= alone.Indicators.QoL - 1e-4f, "bối cảnh hoàn hảo làm QoL giảm: " + alone.Indicators.QoL + " → " + perfect.Indicators.QoL);
            });

            add("CityLink: nạn đói thật trong kinh tế quốc gia (cấm vận + phá đất canh tác) làm thành phố của nước đó khổ hơn", () =>
            {
                float Run(bool famine)
                {
                    var w = Gen(81); var e = new EconomySim(w); var war = new WarSim(w, e);
                    var country = w.Countries[0]; var capital = w.GetCity(country.Capital);
                    for (int i = 0; i < 30; i++) { e.StepWeek(); war.StepWeek(); }
                    if (famine) { foreach (var o in w.Countries) if (o.Id != country.Id) e.SetTradeBlock(country.Id, o.Id, true); foreach (var p in country.Provinces) e.DamageProvince(p, 0.9); }
                    var sim = new CitySim(new WorldTerrain(w, capital.Id, 1000f), 5); DemoScenario.BuildTown(sim); var link = new CityLink(w, e, war);
                    for (int i = 0; i < 104; i++) { e.StepWeek(); war.StepWeek(); link.Apply(sim, capital.Id); sim.StepWeek(); }
                    return sim.Indicators.QoL;
                }
                float calm = Run(false), hungry = Run(true);
                Console.Write($" [QoL thành phố thủ đô: yên {calm:0.00} vs đói {hungry:0.00}]");
                assert(hungry < calm - 0.05f, "nạn đói quốc gia không ảnh hưởng thành phố: " + calm + " vs " + hungry);
            });

            add("CityLink: thành phố bị chiếm chịu kinh tế của chủ mới và tàn phá chiến tranh giảm dần theo thời gian", () =>
            {
                var w = Gen(82); var e = new EconomySim(w); var war = new WarSim(w, e);
                var (a, b) = MilitaryTests.Pair(w); var prov = a.Provinces.First(p => p != a.CapitalProvince); var city = w.GetProvince(prov).Cities[0];
                for (int i = 0; i < 10; i++) { e.StepWeek(); war.StepWeek(); }
                e.S.Indicators[a.Id.Value].FoodSat = 0.9; e.S.Indicators[b.Id.Value].FoodSat = 0.4;
                var link = new CityLink(w, e, war); assert(Math.Abs(link.Build(city).FoodSat - 0.9f) < 1e-6f, "chủ cũ");
                WorldOps.TransferProvince(w, prov, b.Id); war.CapturedWeek[prov.Value] = e.S.Week;
                var ctx = link.Build(city); assert(Math.Abs(ctx.FoodSat - 0.4f) < 1e-6f, "thành phố không chuyển sang kinh tế chủ mới: " + ctx.FoodSat);
                assert(ctx.Devastation > 0.55f, "vừa bị chiếm mà tàn phá chỉ " + ctx.Devastation);
                e.S.Week += 26; assert(link.Build(city).Devastation < ctx.Devastation && link.Build(city).Devastation > 0, "tàn phá phải giảm dần");
                e.S.Week += 40; assert(link.Build(city).Devastation == 0, "sau 52 tuần tàn phá phải hết");
            });

            add("CityLink: tất định và bản lưu giữ mốc chiếm tỉnh (CapturedWeek)", () =>
            {
                float Run() { var w = Gen(83); var e = new EconomySim(w); var war = new WarSim(w, e); var cap = w.GetCity(w.Countries[1].Capital); var sim = new CitySim(new WorldTerrain(w, cap.Id, 1000f), 5); DemoScenario.BuildTown(sim); var link = new CityLink(w, e, war);
                    for (int i = 0; i < 60; i++) { e.StepWeek(); war.StepWeek(); link.Apply(sim, cap.Id); sim.StepWeek(); } return sim.Indicators.QoL; }
                assert(Run() == Run(), "không tất định");
                var w2 = Gen(84); var e2 = new EconomySim(w2); var war2 = new WarSim(w2, e2); war2.CapturedWeek[3] = 17;
                SaveGame.FromBytes(SaveGame.ToBytes(w2, e2, war2), out var w3, out var e3, out var war3); assert(war3.CapturedWeek[3] == 17 && war3.CapturedWeek[4] == -1, "CapturedWeek không được lưu");
            });
        }
    }
}
