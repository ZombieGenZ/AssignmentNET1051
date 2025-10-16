using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Assignment.Services.PayOs
{
    /// <summary>
    /// Binds <see cref="PayOsOptions"/> from configuration and post-configures
    /// the values so they are always normalized.
    /// </summary>
    public sealed class PayOsOptionsSetup : IConfigureOptions<PayOsOptions>, IPostConfigureOptions<PayOsOptions>
    {
        private readonly IConfiguration _configuration;

        public PayOsOptionsSetup(IConfiguration configuration)
            => _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        public void Configure(PayOsOptions options)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            _configuration.GetSection(PayOsOptions.SectionName).Bind(options);
        }

        public void PostConfigure(string? name, PayOsOptions options)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            options.ClientId = Normalize(options.ClientId);
            options.ApiKey = Normalize(options.ApiKey);
            options.ChecksumKey = Normalize(options.ChecksumKey);
            options.BaseUrl = Normalize(options.BaseUrl);
        }

        private static string Normalize(string? value)
            => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
