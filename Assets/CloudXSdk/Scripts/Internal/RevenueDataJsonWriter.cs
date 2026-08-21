#nullable enable

using System.Collections.Generic;

namespace CloudX
{
    internal static class RevenueDataJsonWriter
    {
        internal static bool TryWrite(CloudXRevenueData? data, out string? json)
        {
            json = null;
            if (data == null) return false;
            if (double.IsNaN(data.Revenue) || double.IsInfinity(data.Revenue)) return false;
            if (string.IsNullOrWhiteSpace(data.AdFormat)) return false;

            var platform = data.Platform?.Name;
            if (string.IsNullOrWhiteSpace(platform)) return false;

            string? precision = null;
            if (data.Precision.HasValue)
            {
                precision = ToWireString(data.Precision.Value);
            }

            var dict = new Dictionary<string, object>
            {
                ["platform"] = platform.Trim(),
                ["revenue"] = data.Revenue,
                ["adFormat"] = data.AdFormat.Trim(),
            };
            AddOptional(dict, "currencyCode", data.CurrencyCode);
            AddOptional(dict, "precision", precision);
            AddOptional(dict, "networkName", data.NetworkName);
            AddOptional(dict, "adUnitId", data.AdUnitId);
            AddOptional(dict, "thirdPartyAdPlacementId", data.ThirdPartyAdPlacementId);
            AddOptional(dict, "creativeId", data.CreativeId);
            AddOptional(dict, "networkPlacement", data.NetworkPlacement);
            AddOptional(dict, "countryCode", data.CountryCode);
            AddOptional(dict, "userSegment", data.UserSegment);

            json = Json.Write(dict);
            return true;
        }

        private static string? ToWireString(CloudXRevenuePrecision precision) => precision switch
        {
            CloudXRevenuePrecision.Exact => "exact",
            CloudXRevenuePrecision.Estimated => "estimated",
            CloudXRevenuePrecision.PublisherDefined => "publisher_defined",
            CloudXRevenuePrecision.Undefined => "undefined",
            _ => null,
        };

        private static void AddOptional(Dictionary<string, object> dict, string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) dict[key] = value.Trim();
        }
    }
}
