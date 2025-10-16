using System;
using Microsoft.Extensions.Options;

namespace Assignment.Services.PayOs
{
    public class PayOsOptionsPostConfigure : IPostConfigureOptions<PayOsOptions>
    {
        public void PostConfigure(string name, PayOsOptions options)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            options.ClientId = ResolveEnvironmentPlaceholder(options.ClientId);
            options.ApiKey = ResolveEnvironmentPlaceholder(options.ApiKey);
            options.ChecksumKey = ResolveEnvironmentPlaceholder(options.ChecksumKey);
            options.BaseUrl = ResolveEnvironmentPlaceholder(options.BaseUrl);
        }

        private static string ResolveEnvironmentPlaceholder(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            var trimmed = value.Trim();
            if (trimmed.Length >= 4 && trimmed.StartsWith("${", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
            {
                var key = trimmed.Substring(2, trimmed.Length - 3);
                if (!string.IsNullOrWhiteSpace(key))
                {
                    var environmentValue = GetEnvironmentVariableAcrossTargets(key);
                    if (!string.IsNullOrEmpty(environmentValue))
                    {
                        return environmentValue;
                    }
                }

                // If the placeholder cannot be resolved we fallback to the original value. This
                // makes it possible to provide concrete credentials directly in configuration
                // files, even when they still use the "${VALUE}" syntax (for example when a
                // developer edits the sample configuration in place). It also ensures that
                // existing configuration behaviour remains unchanged when environment variables
                // are not available.
                return trimmed;
            }

            return trimmed;
        }

        private static string? GetEnvironmentVariableAcrossTargets(string key)
        {
            // The default Environment.GetEnvironmentVariable call reads the process variables.
            var value = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            // On Windows the environment variables can be stored at the Machine/User level.
            // These targets are not supported on all platforms, so we guard against
            // PlatformNotSupportedException.
            foreach (EnvironmentVariableTarget target in Enum.GetValues(typeof(EnvironmentVariableTarget)))
            {
                try
                {
                    value = Environment.GetEnvironmentVariable(key, target);
                }
                catch (PlatformNotSupportedException)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }

            return null;
        }
    }
}
