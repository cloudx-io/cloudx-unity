#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CloudX
{
/// <summary>
/// Extension methods for converting AndroidJavaObject instances to C# objects.
/// </summary>
internal static class AndroidJavaObjectExtensions
{
    public static CloudXAd ToCloudXAd(this AndroidJavaObject javaObject)
    {
        var adFormatEnum = javaObject.Call<AndroidJavaObject>("getAdFormat");
        var adFormatName = adFormatEnum.Call<string>("getName");
        var adFormat = ParseCloudXAdFormat(adFormatName);
        var adUnitId = javaObject.Call<string>("getAdUnitId");
        var placement = javaObject.Call<string>("getPlacement");
        var networkName = javaObject.Call<string>("getNetworkName");
        var networkPlacement = javaObject.Call<string>("getNetworkPlacement");
        var revenue = javaObject.Call<double>("getRevenue");
        var adValuesJava = javaObject.Call<AndroidJavaObject>("getAdValues");
        var adValues = adValuesJava?.ToStringDictionary();

        return new CloudXAd(adFormat, adUnitId, placement, networkName, networkPlacement, revenue, adValues);
    }

    private static CloudXAdFormat ParseCloudXAdFormat(string adFormatName)
    {
        if (string.Equals(adFormatName, "APP_OPEN", StringComparison.OrdinalIgnoreCase))
        {
            return CloudXAdFormat.AppOpen;
        }

        return Enum.Parse<CloudXAdFormat>(adFormatName, ignoreCase: true);
    }

    /*
     * Converts a java.util.Map<String, String> AndroidJavaObject to a C# IReadOnlyDictionary.
     */
    private static IReadOnlyDictionary<string, string> ToStringDictionary(this AndroidJavaObject javaMap)
    {
        var result = new Dictionary<string, string>();
        var entrySet = javaMap.Call<AndroidJavaObject>("entrySet");
        var iterator = entrySet.Call<AndroidJavaObject>("iterator");
        while (iterator.Call<bool>("hasNext"))
        {
            var entry = iterator.Call<AndroidJavaObject>("next");
            var key = entry.Call<string>("getKey");
            var value = entry.Call<string>("getValue");
            if (!string.IsNullOrEmpty(key) && value != null)
                result[key] = value;
        }
        return result;
    }

    public static CloudXError ToCloudXError(this AndroidJavaObject javaObject)
    {
        var errorCodeObj = javaObject.Call<AndroidJavaObject>("getCode");
        var errorCodeName = errorCodeObj.Call<string>("getName");
        var errorCodeValue = errorCodeObj.Call<int>("getCode");
        var message = javaObject.Call<string>("getMessage");

        return new CloudXError(errorCodeName, errorCodeValue, message);
    }

    public static CloudXReward ToCloudXReward(this AndroidJavaObject javaObject)
    {
        var amount = javaObject.Call<int>("getAmount");
        var label = javaObject.Call<string>("getLabel");

        return new CloudXReward(amount, label);
    }

    public static CloudXArbiterResult ToCloudXArbiterResult(this AndroidJavaObject javaObject)
    {
        var id = javaObject.Call<string>("getId");
        var platformObj = javaObject.Call<AndroidJavaObject>("getPlatform");
        var platformWireName = platformObj.Call<string>("getName");
        var platform = ArbiterPlatformParser.Parse(platformWireName);
        var platformName = javaObject.Call<string>("getPlatformName");
        if (string.IsNullOrEmpty(platformName))
            platformName = string.IsNullOrEmpty(platformWireName) ? platform.ToWireString() : platformWireName;
        var bidId = javaObject.Call<string>("getBidId");
        var extrasJava = javaObject.Call<AndroidJavaObject>("getExtras");
        var extras = extrasJava != null ? extrasJava.ToStringDictionary() : new Dictionary<string, string>();
        return new CloudXArbiterResult(id, platform, platformName, bidId, extras);
    }

    public static CloudXSdkConfiguration ToCloudXSdkConfiguration(this AndroidJavaObject javaObject)
    {
        // CloudXSdkConfiguration is an empty marker interface on Android
        return new CloudXSdkConfiguration();
    }
}
}
