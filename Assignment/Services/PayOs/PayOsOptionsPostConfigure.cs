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
                    var environmentValue = Environment.GetEnvironmentVariable(key);
                    if (!string.IsNullOrEmpty(environmentValue))
                    {
                        return environmentValue;
                    }
                }
            }

            return value;
        }
    }
}
