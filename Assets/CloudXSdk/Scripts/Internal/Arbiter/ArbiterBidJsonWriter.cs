#nullable enable

using System.Collections.Generic;

namespace CloudX
{
    internal static class ArbiterBidJsonWriter
    {
        internal static string Write(IReadOnlyList<CloudXArbiterBid> bids)
        {
            var list = new List<object>(bids.Count);
            foreach (var bid in bids)
                list.Add(BidToDict(bid));
            return Json.Write(list);
        }

        private static Dictionary<string, object> BidToDict(CloudXArbiterBid bid)
        {
            return bid switch
            {
                CloudXArbiterBid.CloudX cx => CloudXBidToDict(cx),
                CloudXArbiterBid.LevelPlay lp => LevelPlayBidToDict(lp),
                CloudXArbiterBid.PubMatic pm => PubMaticBidToDict(pm),
                CloudXArbiterBid.Custom c => CustomBidToDict(c),
                CloudXArbiterBid.AdMob am => AdMobBidToDict(am),
                CloudXArbiterBid.Gam gam => GamBidToDict(gam),
                _ => new Dictionary<string, object> { ["platform"] = bid.Platform.ToWireString() }
            };
        }

        private static Dictionary<string, object> CloudXBidToDict(CloudXArbiterBid.CloudX bid)
        {
            var ad = bid.Ad;
            var d = new Dictionary<string, object>
            {
                ["platform"] = "CLOUDX",
                ["adFormat"] = AdFormatToWireString(ad.AdFormat),
                ["adUnitId"] = ad.AdUnitId,
                ["networkName"] = ad.NetworkName,
                ["revenue"] = ad.Revenue,
            };
            if (ad.Placement != null) d["placement"] = ad.Placement;
            if (ad.NetworkPlacement != null) d["networkPlacement"] = ad.NetworkPlacement;
            if (ad.AdValues != null && ad.AdValues.Count > 0)
                d["adValues"] = StringMapToDict(ad.AdValues);
            return d;
        }

        private static Dictionary<string, object> LevelPlayBidToDict(CloudXArbiterBid.LevelPlay bid)
        {
            var d = new Dictionary<string, object>
            {
                ["platform"] = "LEVELPLAY",
                ["networkName"] = bid.NetworkName,
                ["revenue"] = bid.Revenue,
                ["precision"] = bid.Precision,
            };
            if (bid.Extras != null && bid.Extras.Count > 0)
                d["extras"] = StringMapToDict(bid.Extras);
            return d;
        }

        private static Dictionary<string, object> PubMaticBidToDict(CloudXArbiterBid.PubMatic bid)
        {
            var d = new Dictionary<string, object>
            {
                ["platform"] = "PUBMATIC",
                ["price"] = bid.Price,
            };
            if (bid.PartnerName != null) d["partnerName"] = bid.PartnerName;
            if (bid.Extras != null && bid.Extras.Count > 0)
                d["extras"] = StringMapToDict(bid.Extras);
            return d;
        }

        private static Dictionary<string, object> CustomBidToDict(CloudXArbiterBid.Custom bid)
        {
            var d = new Dictionary<string, object>
            {
                ["platform"] = "CUSTOM",
                ["platformName"] = bid.PlatformName,
                ["networkName"] = bid.NetworkName,
                ["revenuePerImpressionUSD"] = bid.RevenuePerImpressionUSD,
                ["precision"] = bid.Precision,
            };
            if (bid.Extras != null && bid.Extras.Count > 0)
                d["extras"] = StringMapToDict(bid.Extras);
            return d;
        }

        // Realized-price fields (lastRealizedEcpm/lastRealizedPrecision) are deliberately absent:
        // the realized store lives natively, so the native bridge attaches them at serialization.
        private static Dictionary<string, object> AdMobBidToDict(CloudXArbiterBid.AdMob bid)
        {
            var d = new Dictionary<string, object>
            {
                ["platform"] = "ADMOB",
                ["adUnitId"] = bid.AdUnitId,
                ["networkName"] = bid.NetworkName,
            };
            if (bid.ManualRevenuePerImpressionUSD.HasValue)
                d["manualRevenuePerImpressionUSD"] = bid.ManualRevenuePerImpressionUSD.Value;
            if (bid.Extras != null && bid.Extras.Count > 0)
                d["extras"] = StringMapToDict(bid.Extras);
            return d;
        }

        // Same shape and same native-attached realized fields as the AdMob dict.
        private static Dictionary<string, object> GamBidToDict(CloudXArbiterBid.Gam bid)
        {
            var d = new Dictionary<string, object>
            {
                ["platform"] = "GAM",
                ["adUnitId"] = bid.AdUnitId,
                ["networkName"] = bid.NetworkName,
            };
            if (bid.ManualRevenuePerImpressionUSD.HasValue)
                d["manualRevenuePerImpressionUSD"] = bid.ManualRevenuePerImpressionUSD.Value;
            if (bid.Extras != null && bid.Extras.Count > 0)
                d["extras"] = StringMapToDict(bid.Extras);
            return d;
        }

        private static Dictionary<string, object> StringMapToDict(IReadOnlyDictionary<string, string> map)
        {
            var d = new Dictionary<string, object>(map.Count);
            foreach (var kvp in map)
                d[kvp.Key] = kvp.Value;
            return d;
        }

        private static string AdFormatToWireString(CloudXAdFormat format) =>
            format == CloudXAdFormat.AppOpen ? "APP_OPEN" : format.ToString().ToUpperInvariant();
    }
}
