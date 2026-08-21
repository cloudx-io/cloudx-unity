#nullable enable

using System;

// ReSharper disable once CheckNamespace
namespace CloudX
{
    public sealed class CloudXRevenuePlatform : IEquatable<CloudXRevenuePlatform>
    {
        public static readonly CloudXRevenuePlatform AdMob = new("AdMob");
        public static readonly CloudXRevenuePlatform Gam = new("GAM");
        public static readonly CloudXRevenuePlatform InMobi = new("InMobi");
        public static readonly CloudXRevenuePlatform TopOn = new("TopOn");

        private CloudXRevenuePlatform(string name)
        {
            Name = NormalizeName(name);
        }

        public string Name { get; }

        public static CloudXRevenuePlatform Custom(string name)
        {
            return new CloudXRevenuePlatform(name);
        }

        public bool Equals(CloudXRevenuePlatform? other)
        {
            return other != null && string.Equals(Name, other.Name, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as CloudXRevenuePlatform);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Name);
        }

        public static bool operator ==(CloudXRevenuePlatform? left, CloudXRevenuePlatform? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(CloudXRevenuePlatform? left, CloudXRevenuePlatform? right)
        {
            return !Equals(left, right);
        }

        public override string ToString()
        {
            return Name;
        }

        private static string NormalizeName(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            var trimmed = name.Trim();
            if (trimmed.Length == 0)
            {
                throw new ArgumentException("Revenue platform name cannot be blank.", nameof(name));
            }
            return trimmed;
        }
    }

    public enum CloudXRevenuePrecision
    {
        Exact = 1,
        Estimated = 2,
        PublisherDefined = 3,
        Undefined = 4,
    }

    public sealed record CloudXRevenueData(
        CloudXRevenuePlatform Platform,
        double Revenue,
        string AdFormat,
        string? CurrencyCode = null,
        CloudXRevenuePrecision? Precision = null,
        string? NetworkName = null,
        string? AdUnitId = null,
        string? ThirdPartyAdPlacementId = null,
        string? CreativeId = null,
        string? NetworkPlacement = null,
        string? CountryCode = null,
        string? UserSegment = null
    );
}
