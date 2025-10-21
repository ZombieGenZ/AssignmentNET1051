using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Assignment.Models;
using Assignment.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Assignment.Services.Payments
{
    public class PayOsService : IPayOsService
    {
        private const string PaymentRequestEndpoint = "payment-requests";
        private static readonly string[] KnownBaseUrls =
        {
            "https://api-merchant.payos.vn/v2/",
            "https://api.payos.vn/v2/"
        };

        private readonly HttpClient _httpClient;
        private readonly ILogger<PayOsService> _logger;
        private readonly PayOsOptions _options;

        public PayOsService(HttpClient httpClient, IOptions<PayOsOptions> options, ILogger<PayOsService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _options = options.Value;

            var resolvedBaseUrl = ResolveBaseUrl(_options.BaseUrl);
            EnsureBaseAddress(resolvedBaseUrl);

            ConfigureDefaultHeaders();
        }

        public async Task<PayOsPaymentLink?> CreatePaymentLinkAsync(Order order, string description, string returnUrl, string cancelUrl, CancellationToken cancellationToken = default)
        {
            if (!AreCredentialsConfigured())
            {
                _logger.LogError("PayOS credentials are not fully configured. Cannot create payment link for order {OrderId}.", order.Id);
                return null;
            }

            var amount = Convert.ToInt64(Math.Round(order.TotalBill, MidpointRounding.AwayFromZero));
            if (amount <= 0)
            {
                _logger.LogWarning("Attempted to create PayOS payment link for order {OrderId} with invalid amount {Amount}", order.Id, amount);
                return null;
            }

            var sanitizedDescription = string.IsNullOrWhiteSpace(description)
                ? $"Thanh toán đơn hàng #{order.Id}"
                : description.Trim();
            var normalizedReturnUrl = NormalizeUrl(returnUrl);
            var normalizedCancelUrl = NormalizeUrl(cancelUrl);

            var request = new PayOsCreatePaymentRequest
            {
                OrderCode = order.Id,
                Amount = amount,
                Description = sanitizedDescription,
                ReturnUrl = normalizedReturnUrl,
                CancelUrl = normalizedCancelUrl,
                Items = order.OrderItems?.Select(item => new PayOsItem
                {
                    Name = NormalizeItemName(item),
                    Quantity = item.Quantity,
                    Price = Convert.ToInt64(Math.Round(item.Price, MidpointRounding.AwayFromZero))
                }).ToList(),
                BuyerInfo = new PayOsBuyerInfo
                {
                    Name = NormalizeInput(order.Name),
                    Phone = NormalizeInput(order.Phone),
                    Email = NormalizeInput(order.Email)
                }
            };

            request.Signature = GenerateSignature(request.OrderCode, request.Amount, request.Description, request.ReturnUrl, request.CancelUrl);

            try
            {
                var response = await _httpClient.PostAsJsonAsync(PaymentRequestEndpoint, request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var responseContent = await SafeReadContentAsync(response, cancellationToken);
                    _logger.LogError("PayOS create payment request failed for order {OrderId} with status code {StatusCode}. Response: {Response}", order.Id, response.StatusCode, responseContent);
                    return null;
                }

                var payload = await response.Content.ReadFromJsonAsync<PayOsResponse<PayOsPaymentLink>>(cancellationToken: cancellationToken);
                if (payload == null)
                {
                    _logger.LogError("PayOS create payment request returned an empty payload for order {OrderId}", order.Id);
                    return null;
                }

                if (payload.Code != 0)
                {
                    _logger.LogError("PayOS create payment request returned error code {Code} for order {OrderId}: {Message}", payload.Code, order.Id, payload.Message ?? payload.Description);
                    return null;
                }

                if (payload.Data == null)
                {
                    _logger.LogError("PayOS create payment request returned no data for order {OrderId}", order.Id);
                    return null;
                }

                return payload.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling PayOS to create payment link for order {OrderId}", order.Id);
                return null;
            }
        }

        public async Task<PayOsPaymentDetails?> GetPaymentDetailsAsync(long orderCode, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{PaymentRequestEndpoint}/{orderCode}", cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to retrieve PayOS payment details for order code {OrderCode}. StatusCode: {StatusCode}", orderCode, response.StatusCode);
                    return null;
                }

                var payload = await response.Content.ReadFromJsonAsync<PayOsResponse<PayOsPaymentDetails>>(cancellationToken: cancellationToken);
                return payload?.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving PayOS payment details for order code {OrderCode}", orderCode);
                return null;
            }
        }

        private void ConfigureDefaultHeaders()
        {
            _httpClient.DefaultRequestHeaders.Remove("x-client-id");
            _httpClient.DefaultRequestHeaders.Remove("x-api-key");

            var clientId = NormalizeInput(_options.ClientId);
            var apiKey = NormalizeInput(_options.ApiKey);

            if (!string.IsNullOrEmpty(clientId))
            {
                _httpClient.DefaultRequestHeaders.Add("x-client-id", clientId);
            }

            if (!string.IsNullOrEmpty(apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
            }
        }

        private string GenerateSignature(long orderCode, long amount, string description, string returnUrl, string cancelUrl)
        {
            var checksumKey = NormalizeInput(_options.ChecksumKey) ?? string.Empty;
            var data = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = amount.ToString(CultureInfo.InvariantCulture),
                ["cancelUrl"] = cancelUrl ?? string.Empty,
                ["description"] = description ?? string.Empty,
                ["orderCode"] = orderCode.ToString(CultureInfo.InvariantCulture),
                ["returnUrl"] = returnUrl ?? string.Empty
            };

            var rawData = string.Join("&", data.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(checksumKey));
            var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawData));

            var builder = new StringBuilder(hashBytes.Length * 2);
            foreach (var b in hashBytes)
            {
                builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private void EnsureBaseAddress(string baseUrl)
        {
            var normalizedBase = EnsureTrailingSlash(baseUrl);
            if (_httpClient.BaseAddress?.AbsoluteUri != normalizedBase)
            {
                _httpClient.BaseAddress = new Uri(normalizedBase);
            }
        }

        private string ResolveBaseUrl(string? configuredBaseUrl)
        {
            var candidates = BuildBaseUrlCandidates(configuredBaseUrl);

            foreach (var candidate in candidates)
            {
                if (!Uri.TryCreate(EnsureTrailingSlash(candidate), UriKind.Absolute, out var candidateUri))
                {
                    _logger.LogWarning("Ignoring invalid PayOS base URL '{BaseUrl}'.", candidate);
                    continue;
                }

                if (!CanResolveHost(candidateUri.Host))
                {
                    _logger.LogWarning("PayOS base URL '{BaseUrl}' could not be resolved via DNS. Trying next candidate.", candidateUri.AbsoluteUri);
                    continue;
                }

                return candidateUri.AbsoluteUri;
            }

            var fallback = EnsureTrailingSlash(KnownBaseUrls[0]);
            _logger.LogWarning("Falling back to default PayOS base URL '{BaseUrl}' after all candidates failed.", fallback);
            return fallback;
        }

        private static IEnumerable<string> BuildBaseUrlCandidates(string? configuredBaseUrl)
        {
            if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
            {
                yield return configuredBaseUrl.Trim();
            }

            foreach (var baseUrl in KnownBaseUrls)
            {
                yield return baseUrl;
            }
        }

        private static bool CanResolveHost(string host)
        {
            try
            {
                return Dns.GetHostAddresses(host).Length > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string EnsureTrailingSlash(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            return url.EndsWith("/", StringComparison.Ordinal) ? url : url + "/";
        }

        private static string? NormalizeInput(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string NormalizeItemName(OrderItem item)
        {
            if (item == null)
            {
                return "";
            }

            var name = item.Product?.Name ?? item.Combo?.Name ?? $"Sản phẩm #{item.ProductId ?? item.ComboId}";
            return NormalizeInput(name) ?? string.Empty;
        }

        private static string NormalizeUrl(string url)
        {
            return string.IsNullOrWhiteSpace(url) ? string.Empty : url.Trim();
        }

        private bool AreCredentialsConfigured()
        {
            return !string.IsNullOrWhiteSpace(_options.ClientId)
                   && !string.IsNullOrWhiteSpace(_options.ApiKey)
                   && !string.IsNullOrWhiteSpace(_options.ChecksumKey);
        }

        private static async Task<string?> SafeReadContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            try
            {
                return await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch
            {
                return null;
            }
        }
    }
}
