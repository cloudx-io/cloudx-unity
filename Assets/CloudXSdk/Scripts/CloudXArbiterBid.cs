#nullable enable

using System.Collections.Generic;

namespace CloudX
{
    /// <summary>
    /// A bid in a trusted-arbiter auction.
    /// </summary>
    public abstract record CloudXArbiterBid
    {
        public abstract CloudXArbiterPlatform Platform { get; }

        /// <summary>
        /// A bid from a CloudX-mediated ad network.
        /// Pass the <see cref="CloudXAd"/> received in an <c>OnAdLoaded</c> callback directly —
        /// its <see cref="CloudXAd.AdValues"/> map carries the trusted-payload keys the server
        /// uses to validate the bid.
        /// </summary>
        public sealed record CloudX(CloudXAd Ad) : CloudXArbiterBid
        {
            public override CloudXArbiterPlatform Platform => CloudXArbiterPlatform.CloudX;
        }

        /// <summary>
        /// A bid from an IronSource LevelPlay ad network.
        /// </summary>
        public sealed record LevelPlay(
            string NetworkName,
            double Revenue,
            string Precision,
            IReadOnlyDictionary<string, string>? Extras = null
        ) : CloudXArbiterBid
        {
            public override CloudXArbiterPlatform Platform => CloudXArbiterPlatform.LevelPlay;
        }

        /// <summary>
        /// A bid from PubMatic OpenWrap.
        /// </summary>
        public sealed record PubMatic(
            double Price,
            string? PartnerName = null,
            IReadOnlyDictionary<string, string>? Extras = null
        ) : CloudXArbiterBid
        {
            public override CloudXArbiterPlatform Platform => CloudXArbiterPlatform.PubMatic;
        }

        /// <summary>
        /// A bid from a mediation platform without a dedicated bid type, such as AdMob.
        /// <paramref name="PlatformName"/> names the originating mediator and
        /// <paramref name="RevenuePerImpressionUSD"/> is revenue for the single impression, not a CPM.
        /// </summary>
        public sealed record Custom(
            string PlatformName,
            string NetworkName,
            double RevenuePerImpressionUSD,
            string Precision,
            IReadOnlyDictionary<string, string>? Extras = null
        ) : CloudXArbiterBid
        {
            public override CloudXArbiterPlatform Platform => CloudXArbiterPlatform.Custom;
        }

        /// <summary>
        /// A bid from an AdMob ad unit the publisher mediates themselves.
        /// No price is required: CloudX prices the bid from the realized revenue history it
        /// accumulates through <c>ReportRevenueData</c>. Supply
        /// <paramref name="ManualRevenuePerImpressionUSD"/> only to override that with your own
        /// per-impression estimate. <paramref name="NetworkName"/> may be set to the AdMob
        /// <c>responseInfo.loadedAdapterResponseInfo.adSourceName</c> value.
        /// A manual price of <c>0.0</c> is honored as a real price; negative and non-finite
        /// values are dropped by the native SDK and treated as if no manual price was supplied.
        /// </summary>
        public sealed record AdMob(
            string AdUnitId,
            string NetworkName = "admob",
            double? ManualRevenuePerImpressionUSD = null,
            IReadOnlyDictionary<string, string>? Extras = null
        ) : CloudXArbiterBid
        {
            public override CloudXArbiterPlatform Platform => CloudXArbiterPlatform.AdMob;
        }

        /// <summary>
        /// A bid from a Google Ad Manager ad unit the publisher mediates themselves.
        /// Priced exactly like <see cref="AdMob"/>: CloudX prices the bid from the realized
        /// revenue history it accumulates through <c>ReportRevenueData</c>, and
        /// <paramref name="ManualRevenuePerImpressionUSD"/> overrides that when supplied.
        /// A manual price of <c>0.0</c> is honored as a real price; negative and non-finite
        /// values are dropped by the native SDK and treated as if no manual price was supplied.
        /// AdMob and Ad Manager are separate demand sources and may bid in the same arbitration.
        /// </summary>
        public sealed record Gam(
            string AdUnitId,
            string NetworkName = "gam",
            double? ManualRevenuePerImpressionUSD = null,
            IReadOnlyDictionary<string, string>? Extras = null
        ) : CloudXArbiterBid
        {
            public override CloudXArbiterPlatform Platform => CloudXArbiterPlatform.Gam;
        }
    }
}
