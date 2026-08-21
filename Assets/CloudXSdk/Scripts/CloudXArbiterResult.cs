#nullable enable

using System.Collections.Generic;

namespace CloudX
{
    /// <summary>
    /// The result of a trusted-arbiter auction.
    /// </summary>
    /// <param name="Id">The auction identifier assigned by the SDK.</param>
    /// <param name="Platform">
    /// The platform whose bid won.
    /// <see cref="CloudXArbiterPlatform.None"/> when no bid was selected.
    /// </param>
    /// <param name="PlatformName">
    /// The name of the platform whose bid won.
    /// Matches <see cref="Platform"/> for built-in bid types and carries the originating
    /// mediator name when a custom bid wins.
    /// </param>
    /// <param name="BidId">
    /// The winning bid identifier.
    /// Null when <see cref="Platform"/> is <see cref="CloudXArbiterPlatform.None"/>.
    /// </param>
    /// <param name="Extras">
    /// Additional metadata returned by the winning network.
    /// Empty when no extras are provided.
    /// </param>
    public record CloudXArbiterResult(
        string Id,
        CloudXArbiterPlatform Platform,
        string PlatformName,
        string? BidId,
        IReadOnlyDictionary<string, string> Extras
    );
}
