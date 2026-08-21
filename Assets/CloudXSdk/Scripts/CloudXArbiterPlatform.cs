#nullable enable

namespace CloudX
{
    /// <summary>
    /// The ad mediation platform that won a trusted-arbiter auction.
    /// </summary>
    public enum CloudXArbiterPlatform
    {
        CloudX,
        LevelPlay,
        PubMatic,
        Custom,
        None,
        AdMob,
        Gam,
    }

    internal static class ArbiterPlatformExtensions
    {
        /// <summary>
        /// Serializes the platform to the uppercase string token used in JSON payloads
        /// exchanged with the native Android and iOS bridges (e.g. "CLOUDX", "LEVELPLAY").
        /// </summary>
        internal static string ToWireString(this CloudXArbiterPlatform platform) => platform switch
        {
            CloudXArbiterPlatform.CloudX => "CLOUDX",
            CloudXArbiterPlatform.LevelPlay => "LEVELPLAY",
            CloudXArbiterPlatform.PubMatic => "PUBMATIC",
            CloudXArbiterPlatform.Custom => "CUSTOM",
            CloudXArbiterPlatform.AdMob => "ADMOB",
            CloudXArbiterPlatform.Gam => "GAM",
            _ => "NONE",
        };
    }

    internal static class ArbiterPlatformParser
    {
        /// <summary>
        /// Parses a wire-format platform token into a <see cref="CloudXArbiterPlatform"/>.
        /// Comparison is case-insensitive. Unknown or null values return <see cref="CloudXArbiterPlatform.None"/>.
        /// </summary>
        internal static CloudXArbiterPlatform Parse(string? name) => name?.ToUpperInvariant() switch
        {
            "CLOUDX" => CloudXArbiterPlatform.CloudX,
            "LEVELPLAY" => CloudXArbiterPlatform.LevelPlay,
            "PUBMATIC" => CloudXArbiterPlatform.PubMatic,
            "CUSTOM" => CloudXArbiterPlatform.Custom,
            "ADMOB" => CloudXArbiterPlatform.AdMob,
            "GAM" => CloudXArbiterPlatform.Gam,
            _ => CloudXArbiterPlatform.None,
        };
    }
}
