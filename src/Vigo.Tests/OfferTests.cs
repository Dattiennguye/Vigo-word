using System;
using System.Linq;
using Vigo.Core;
using Vigo.Core.Economy;
using Vigo.Core.Military;

namespace Vigo.Tests
{
    internal static class OfferTests
    {
        private static WorldState Gen(ulong seed) => WorldGenerator.Generate(new WorldConfig { Seed = seed, CellCount = 20000 });
        private static (WorldState w, EconomySim e, WarSim war, DiplomacyAI dip, Country human, Country ai) Setup(ulong seed)
        {
            var w = Gen(seed); var e = new EconomySim(w); var war = new WarSim(w, e); var dip = new DiplomacyAI(war);
            var (a, b) = MilitaryTests.Pair(w); dip.SetHuman(a.Id, true);
            return (w, e, war, dip, a, b);
        }
        private static void Tick(EconomySim e, DiplomacyAI dip, int week) { e.S.Week = week; dip.Think(); }

        public static void Register(Action<string, Action> add, Action<bool, string> assert)
        {
            add("Đề nghị: AI mến người chơi (láng giềng) gửi đề nghị liên minh; chưa mến thì không; không tự ký thay người chơi", () =>
            {
                var (w, e, war, dip, h, c) = Setup(71); int hi = h.Id.Value, ci = c.Id.Value;
                war.Delta[ci, hi] = -100; Tick(e, dip, 26);
                assert(!war.Offers.Any(o => o.From == ci && o.Type == TreatyType.Alliance), "AI ghét người chơi mà vẫn đề nghị liên minh");
                war.Offers.Clear(); war.Delta[ci, hi] = 100; Tick(e, dip, 52);
                var o = war.OffersFor(hi).FirstOrDefault(x => x.From == ci);
                assert(o != null && o.Type == TreatyType.Alliance, "AI mến người chơi mà không đề nghị liên minh");
                assert(!war.Allied(hi, ci), "AI tự ký liên minh thay người chơi");
                int count = war.Offers.Count; Tick(e, dip, 78);
                assert(war.Offers.Count(x => x.From == ci && x.To == hi) <= 1, "gửi trùng đề nghị");
            });

            add("Đề nghị: chấp nhận → ký thật, đối xứng; từ chối → mất chút thiện cảm; hết hạn thì tự bỏ; mất hiệu lực khi đang chiến tranh", () =>
            {
                var (w, e, war, dip, h, c) = Setup(72); int hi = h.Id.Value, ci = c.Id.Value;
                war.Delta[ci, hi] = 100; Tick(e, dip, 26);
                var o = war.OffersFor(hi).First(x => x.From == ci && x.Type == TreatyType.Alliance);
                assert(war.AcceptOffer(o.Id) && war.Allied(hi, ci) && war.Allied(ci, hi), "chấp nhận mà không thành liên minh");
                assert(!war.AcceptOffer(o.Id), "chấp nhận lại cùng đề nghị");
                // từ chối
                var (w2, e2, war2, dip2, h2, c2) = Setup(72); int h2i = h2.Id.Value, c2i = c2.Id.Value;
                war2.Delta[c2i, h2i] = 100; Tick(e2, dip2, 26); var o2 = war2.OffersFor(h2i).First(x => x.From == c2i);
                double before = war2.GetOpinion(c2i, h2i); assert(war2.DeclineOffer(o2.Id), "từ chối thất bại");
                assert(war2.GetOpinion(c2i, h2i) < before && !war2.Allied(h2i, c2i), "từ chối phải giảm thiện cảm và không ký");
                // hết hạn
                war2.Offers.Clear(); war2.SendOffer(c2.Id, h2.Id, TreatyType.NonAggression); assert(war2.Offers.Count == 1, "không gửi được đề nghị hiệp ước");
                e2.S.Week += WarSim.OfferWeeks; war2.PruneOffers(); assert(war2.Offers.Count == 0, "đề nghị hết hạn không bị bỏ");
                // chiến tranh làm đề nghị mất hiệu lực
                war2.SendOffer(c2.Id, h2.Id, TreatyType.NonAggression); war2.DeclareWar(h2.Id, c2.Id); war2.PruneOffers();
                assert(war2.Offers.Count == 0, "đề nghị còn sống khi hai nước đang đánh nhau");
            });

            add("Đề nghị: AI không bao giờ gửi tới nước không phải người chơi; đề nghị chờ được lưu/nạp nguyên vẹn", () =>
            {
                var (w, e, war, dip, h, c) = Setup(73); int hi = h.Id.Value, ci = c.Id.Value;
                assert(!war.SendOffer(h.Id, c.Id, TreatyType.Alliance), "gửi tới nước AI");
                war.Delta[ci, hi] = 100; Tick(e, dip, 26);
                assert(war.Offers.All(o => war.HumanCountries.Contains(o.To)), "có đề nghị tới nước AI");
                assert(war.Offers.Count > 0, "kịch bản không có đề nghị để lưu");
                var data = SaveGame.ToBytes(w, e, war); SaveGame.FromBytes(data, out var w2, out var e2, out var war2);
                assert(war2.Offers.Count == war.Offers.Count && war2.NextOfferId == war.NextOfferId, "đề nghị mất sau khi nạp");
                for (int k = 0; k < war.Offers.Count; k++) { var a = war.Offers[k]; var b = war2.Offers[k]; assert(a.Id == b.Id && a.Type == b.Type && a.From == b.From && a.To == b.To && a.Expire == b.Expire, "đề nghị " + k + " lệch"); }
                var o = war2.Offers[0]; assert(war2.AcceptOffer(o.Id), "đề nghị nạp lại không chấp nhận được"); assert(war2.Treaties.Any(t => t.Type == o.Type && ((t.A == o.From && t.B == o.To) || (t.A == o.To && t.B == o.From))), "chấp nhận mà không có hiệp ước");
            });

            add("Đề nghị: AI sợ người chơi hùng mạnh thì đề nghị hiệp ước không xâm lược", () =>
            {
                var (w, e, war, dip, h, c) = Setup(74); int hi = h.Id.Value, ci = c.Id.Value;
                war.Delta[ci, hi] = 0; e.S.Troops[hi] = 1e7; e.S.Troops[ci] = 0; Tick(e, dip, 26);
                var pact = war.OffersFor(hi).Any(o => o.From == ci && o.Type == TreatyType.NonAggression);
                var ally = war.OffersFor(hi).Any(o => o.From == ci && o.Type == TreatyType.Alliance);
                Console.Write($" [thiện cảm {war.GetOpinion(ci, hi):0}; hiệp ước {pact}; liên minh {ally}]");
                assert(pact || ally, "AI yếu bên cạnh người chơi mạnh không gửi đề nghị nào");
            });
        }
    }
}
