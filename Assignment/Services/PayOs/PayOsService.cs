using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Assignment.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Assignment.Services.PayOs
{
    public class PayOsService : IPayOsService
    {
        private readonly HttpClient _httpClient;
        private readonly PayOsOptions _options;
        private readonly ILogger<PayOsService> _logger;

        public PayOsService(HttpClient httpClient, IOptions<PayOsOptions> options, ILogger<PayOsService> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<string> CreatePaymentUrlAsync(Order order, string successUrl, string cancelUrl, CancellationToken cancellationToken = default)
        {
            var amount = Convert.ToInt64(Math.Round(order.TotalBill, MidpointRounding.AwayFromZero));
            if (amount <= 0)
            {
                throw new InvalidOperationException("Order total must be greater than zero to initiate PayOS payment.");
            }

            var request = new PayOsCreatePaymentRequest
            {
                OrderCode = order.Id,
                Amount = amount,
                Description = $"Thanh toán đơn hàng #{order.Id}",
                ReturnUrl = successUrl,
                CancelUrl = cancelUrl,
                BuyerName = order.Name,
                BuyerEmail = order.Email,
                BuyerPhone = order.Phone,
            };

            if (!string.IsNullOrWhiteSpace(_options.ChecksumKey))
            {
                request.Signature = BuildSignature(request, _options.ChecksumKey);
            }

            try
            {
                using var response = await _httpClient.PostAsJsonAsync("v2/payment-requests", request, cancellationToken);
                response.EnsureSuccessStatusCode();

                var payload = await response.Content.ReadFromJsonAsync<PayOsCreatePaymentResponse>(cancellationToken: cancellationToken);
                if (payload?.Data?.CheckoutUrl is string checkout && !string.IsNullOrWhiteSpace(checkout))
                {
                    return checkout;
                }

                throw new InvalidOperationException("PayOS response did not contain a checkout URL.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create PayOS payment for order {OrderId}.", order.Id);
                throw;
            }
        }

        public bool ValidateRedirectSignature(IQueryCollection query)
        {
            if (string.IsNullOrWhiteSpace(_options.ChecksumKey))
            {
                // Without a checksum key we cannot validate the signature, so treat it as valid.
                return true;
            }

            if (!query.TryGetValue("signature", out var signatureValues))
            {
                return false;
            }

            var signature = signatureValues.ToString();
            var rawData = new SortedDictionary<string, string>();

            foreach (var key in query.Keys)
            {
                if (string.Equals(key, "signature", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                rawData[key] = query[key].ToString();
            }

            if (rawData.Count == 0)
            {
                return false;
            }

            var payload = string.Join("|", rawData.Select(kv => $"{kv.Key}={kv.Value}"));
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.ChecksumKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var calculatedSignature = Convert.ToHexString(hash).ToLowerInvariant();

            return string.Equals(signature, calculatedSignature, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildSignature(PayOsCreatePaymentRequest request, string checksumKey)
        {
            var fields = new List<string>
            {
                $"orderCode={request.OrderCode}",
                $"amount={request.Amount}",
                $"description={request.Description}",
                $"returnUrl={request.ReturnUrl}",
                $"cancelUrl={request.CancelUrl}"
            };

            if (!string.IsNullOrWhiteSpace(request.BuyerName))
            {
                fields.Add($"buyerName={request.BuyerName}");
            }

            if (!string.IsNullOrWhiteSpace(request.BuyerEmail))
            {
                fields.Add($"buyerEmail={request.BuyerEmail}");
            }

            if (!string.IsNullOrWhiteSpace(request.BuyerPhone))
            {
                fields.Add($"buyerPhone={request.BuyerPhone}");
            }

            var payload = string.Join("|", fields);
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(checksumKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private sealed class PayOsCreatePaymentRequest
        {
            [JsonPropertyName("orderCode")]
            public long OrderCode { get; set; }

            [JsonPropertyName("amount")]
            public long Amount { get; set; }

            [JsonPropertyName("description")]
            public string Description { get; set; } = string.Empty;

            [JsonPropertyName("returnUrl")]
            public string ReturnUrl { get; set; } = string.Empty;

            [JsonPropertyName("cancelUrl")]
            public string CancelUrl { get; set; } = string.Empty;

            [JsonPropertyName("buyerName")]
            public string? BuyerName { get; set; }

            [JsonPropertyName("buyerEmail")]
            public string? BuyerEmail { get; set; }

            [JsonPropertyName("buyerPhone")]
            public string? BuyerPhone { get; set; }

            [JsonPropertyName("signature")]
            public string? Signature { get; set; }
        }

        private sealed class PayOsCreatePaymentResponse
        {
            [JsonPropertyName("code")]
            public int Code { get; set; }

            [JsonPropertyName("desc")]
            public string? Description { get; set; }

            [JsonPropertyName("data")]
            public PayOsCreatePaymentResponseData? Data { get; set; }
        }

        private sealed class PayOsCreatePaymentResponseData
        {
            [JsonPropertyName("checkoutUrl")]
            public string? CheckoutUrl { get; set; }
        }
    }
}
