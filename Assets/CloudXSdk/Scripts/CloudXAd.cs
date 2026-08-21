// CloudXAd.cs

#nullable enable

using System.Collections.Generic;

namespace CloudX
{
    /// <summary>
    /// Metadata about a loaded or displayed ad.
    /// Passed to listener callbacks and provides details about the ad format,
    /// winning network, and impression-level revenue.
    /// </summary>
    /// <param name="AdFormat">The ad format (Banner, Mrec, Interstitial, Rewarded, Native).</param>
    /// <param name="AdUnitId">The CloudX ad unit identifier.</param>
    /// <param name="Placement">Optional placement identifier set via SetPlacement. Null when not set.</param>
    /// <param name="NetworkName">Name of the ad network that won the auction.</param>
    /// <param name="NetworkPlacement">Network-specific placement identifier, if available. Null otherwise.</param>
    /// <param name="Revenue">Bid-time revenue estimate in USD.</param>
    /// <param name="AdValues">
    /// SDK-defined metadata values associated with this loaded ad.
    /// Contains opaque payload strings used for server-side Trusted Arbiter validation
    /// (keys: auction_payload, bid_payload, arbiter_auction_payload, arbiter_bid_payload).
    /// Null or empty for non-CloudX network wins and when no metadata is available.
    /// </param>
    public record CloudXAd(
        CloudXAdFormat AdFormat,
        string AdUnitId,
        string? Placement,
        string NetworkName,
        string? NetworkPlacement,
        double Revenue,
        IReadOnlyDictionary<string, string>? AdValues = null
    );
}
