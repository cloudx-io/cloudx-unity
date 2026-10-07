#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace CloudX
{
    /// <summary>
    /// Configuration data returned after successful SDK initialization.
    /// </summary>
    public record CloudXSdkConfiguration
    {
        /// <summary>
        /// Session configuration keyed by CloudX ad unit ID. Currently supplied on Android.
        /// Missing entries mean unavailable, not disabled. iOS and Editor return an empty map.
        /// </summary>
        public IReadOnlyDictionary<string, CloudXAdUnitConfiguration> AdUnitConfigurations { get; }

        public CloudXSdkConfiguration() : this(new Dictionary<string, CloudXAdUnitConfiguration>()) { }

        public CloudXSdkConfiguration(IDictionary<string, CloudXAdUnitConfiguration> adUnitConfigurations)
        {
            if (adUnitConfigurations == null) throw new ArgumentNullException(nameof(adUnitConfigurations));
            AdUnitConfigurations = new ReadOnlyDictionary<string, CloudXAdUnitConfiguration>(
                new Dictionary<string, CloudXAdUnitConfiguration>(adUnitConfigurations, StringComparer.Ordinal));
        }

        // Summarize the map in ToString: the default prints only the dictionary's type name.
        protected virtual bool PrintMembers(StringBuilder builder)
        {
            var enabled = 0;
            foreach (var configuration in AdUnitConfigurations.Values)
            {
                if (configuration.IsOrchestratorEnabled) enabled++;
            }
            builder.Append($"AdUnitConfigurations = {AdUnitConfigurations.Count} units, {enabled} Orchestrator-enabled");
            return true;
        }
    }
}
