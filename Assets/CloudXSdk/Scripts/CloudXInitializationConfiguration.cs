#nullable enable

using System;

namespace CloudX
{
    /// <summary>
    /// Configuration for SDK initialization. Use the builder pattern via Create() to construct.
    /// </summary>
    public sealed class CloudXInitializationConfiguration
    {
        /// <summary>
        /// The app key used for SDK authentication.
        /// </summary>
        public string AppKey { get; }

        private CloudXInitializationConfiguration(string appKey)
        {
            AppKey = appKey;
        }

        /// <summary>
        /// Creates a new builder for CloudXInitializationConfiguration.
        /// </summary>
        /// <param name="appKey">Your CloudX app key.</param>
        /// <returns>A builder instance to configure initialization options.</returns>
        public static Builder Create(string appKey)
        {
            if (string.IsNullOrEmpty(appKey))
            {
                throw new ArgumentException("App key cannot be null or empty", nameof(appKey));
            }
            return new Builder(appKey);
        }

        /// <summary>
        /// Builder for CloudXInitializationConfiguration.
        /// </summary>
        public sealed class Builder
        {
            private readonly string _appKey;

            internal Builder(string appKey)
            {
                _appKey = appKey;
            }

            /// <summary>
            /// Builds the CloudXInitializationConfiguration instance.
            /// </summary>
            /// <returns>A configured CloudXInitializationConfiguration instance.</returns>
            public CloudXInitializationConfiguration Build()
            {
                return new CloudXInitializationConfiguration(_appKey);
            }
        }
    }
}
