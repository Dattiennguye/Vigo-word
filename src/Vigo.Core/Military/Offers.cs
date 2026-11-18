using System;
using System.Collections.Generic;
using System.Linq;

namespace Vigo.Core.Military
{
    /// <summary>Đề nghị ngoại giao từ AI gửi tới nước do người chơi điều khiển; người chơi tự chấp nhận hoặc từ chối.</summary>
    public sealed class Offer
    {
        public int Id; public TreatyType Type; public int From, To, Week, Expire;
    }

    public sealed partial class WarSim
    {
        public const int OfferWeeks = 26;
        public readonly List<Offer> Offers = new List<Offer>();
        public int NextOfferId = 1;

        public IEnumerable<Offer> OffersFor(int human) { return Offers.Where(o => o.To == human); }
        public bool HasOffer(int from, int to, TreatyType type) { return Offers.Any(o => o.From == from && o.To == to && o.Type == type); }

        /// <summary>AI gửi đề nghị tới người chơi. Trả về false nếu người nhận không phải người chơi, trùng đề nghị đang chờ hoặc không còn hợp lệ.</summary>
        public bool SendOffer(CountryId from, CountryId to, TreatyType type)
        {
            int f = from.Value, t = to.Value;
            if (!HumanCountries.Contains(t) || HumanCountries.Contains(f) || HasOffer(f, t, type) || !CanSign(f, t, type)) return false;
            Offers.Add(new Offer { Id = NextOfferId++, Type = type, From = f, To = t, Week = Eco.S.Week, Expire = Eco.S.Week + OfferWeeks });
            Log("offer", f, t, W.Countries[f].Name + " đề nghị " + (type == TreatyType.Alliance ? "liên minh" : "hiệp ước không xâm lược") + " với " + W.Countries[t].Name);
            return true;
        }

        private bool CanSign(int a, int b, TreatyType type)
        {
            if (a == b || W.Countries[a].Eliminated || W.Countries[b].Eliminated || AtWar(a, b)) return false;
            if (type == TreatyType.Alliance)
            {
                if (Allied(a, b) || Allies(a).Count >= 3 || Allies(b).Count >= 3) return false;
                foreach (int z in Allies(a)) if (AtWar(b, z)) return false;
                foreach (int z in Allies(b)) if (AtWar(a, z)) return false;
                return true;
            }
            return !HasPact(a, b) && !Allied(a, b);
        }

        /// <summary>Người chơi chấp nhận: ký hiệp ước nếu còn hợp lệ (tình hình có thể đã đổi), ngược lại đề nghị bị hủy và trả false.</summary>
        public bool AcceptOffer(int offerId)
        {
            var o = Offers.FirstOrDefault(x => x.Id == offerId); if (o == null) return false;
            Offers.Remove(o);
            if (!CanSign(o.From, o.To, o.Type)) return false;
            Treaties.Add(new Treaty { Type = o.Type, A = o.From, B = o.To, StartWeek = Eco.S.Week, Expire = o.Type == TreatyType.Alliance ? -1 : Eco.S.Week + PactWeeks });
            AdjustOpinion(o.From, o.To, o.Type == TreatyType.Alliance ? 20 : 8);
            Log(o.Type == TreatyType.Alliance ? "alliance" : "pact", o.From, o.To, W.Countries[o.From].Name + " và " + W.Countries[o.To].Name + (o.Type == TreatyType.Alliance ? " lập liên minh" : " ký hiệp ước không xâm lược"));
            return true;
        }

        /// <summary>Từ chối: bên gửi mất chút thiện cảm với người chơi (−3).</summary>
        public bool DeclineOffer(int offerId)
        {
            var o = Offers.FirstOrDefault(x => x.Id == offerId); if (o == null) return false;
            Offers.Remove(o); AdjustOpinion(o.From, o.To, -3); return true;
        }

        /// <summary>Gọi mỗi tuần: bỏ đề nghị hết hạn hoặc không còn hợp lệ (bên gửi bị diệt, đã có hiệp ước, đang chiến tranh…).</summary>
        public void PruneOffers()
        {
            Offers.RemoveAll(o => Eco.S.Week >= o.Expire || !CanSign(o.From, o.To, o.Type));
        }
    }
}
