using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

            if (order.OrderItems != null)
            {
                var items = new List<PayOsCreatePaymentRequestItem>();
                var index = 1;
                foreach (var orderItem in order.OrderItems)
                {
                    if (orderItem == null)
                    {
                        continue;
                    }

                    var price = Convert.ToInt64(Math.Round(orderItem.Price, MidpointRounding.AwayFromZero));
                    if (price <= 0)
                    {
                        continue;
                    }

                    var quantity = orderItem.Quantity <= 0 ? 1 : orderItem.Quantity;
                    var name = orderItem.Product?.Name ?? orderItem.Combo?.Name;

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        name = orderItem.ProductId.HasValue
                            ? $"Sản phẩm #{orderItem.ProductId.Value}"
                            : orderItem.ComboId.HasValue
                                ? $"Combo #{orderItem.ComboId.Value}"
                                : $"Mặt hàng #{index}";
                    }

                    items.Add(new PayOsCreatePaymentRequestItem
                    {
                        Name = name!,
                        Price = price,
                        Quantity = quantity
                    });

                    index++;
                }

                if (items.Count > 0)
                {
                    request.Items = items;
                }
            }

            if (!string.IsNullOrWhiteSpace(_options.ChecksumKey))
            {
                request.Signature = BuildSignature(request, _options.ChecksumKey);
            }

            try
            {
                using var response = await _httpClient.PostAsJsonAsync("v2/payment-requests", request, cancellationToken);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                PayOsCreatePaymentResponse? payload = null;

                try
                {
                    payload = JsonSerializer.Deserialize<PayOsCreatePaymentResponse>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        NumberHandling = JsonNumberHandling.AllowReadingFromString
                    });
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "Unable to parse PayOS response for order {OrderId}. Payload: {Payload}", order.Id, content);
                    throw new InvalidOperationException("Không thể phân tích phản hồi từ PayOS.", jsonEx);
                }

                if (payload is null)
                {
                    _logger.LogWarning("Empty PayOS response received for order {OrderId}.", order.Id);
                    throw new InvalidOperationException("Phản hồi từ PayOS bị trống.");
                }

                if (payload.Code != 0)
                {
                    var message = string.IsNullOrWhiteSpace(payload.Description)
                        ? $"PayOS trả về mã lỗi {payload.Code}."
                        : $"PayOS trả về mã lỗi {payload.Code}: {payload.Description}";

                    _logger.LogWarning("PayOS returned an error for order {OrderId}. Payload: {Payload}", order.Id, content);
                    throw new InvalidOperationException(message);
                }

                var checkoutUrl = payload.Data?.CheckoutUrl;
                if (string.IsNullOrWhiteSpace(checkoutUrl))
                {
                    checkoutUrl = payload.Data?.ShortLink;
                }

                if (!string.IsNullOrWhiteSpace(checkoutUrl))
                {
                    return checkoutUrl;
                }

                _logger.LogWarning("PayOS response did not contain a checkout URL for order {OrderId}. Payload: {Payload}", order.Id, content);
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
            // The PayOS signature must be generated from every field included in
            // the request payload (except for the signature itself) sorted in
            // ascending order by key. When optional buyer information is
            // present in the payload it also needs to participate in the
            // signature. Otherwise PayOS considers the payload tampered with and
            // returns error code 201 (invalid signature).
            var parts = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = request.Amount.ToString(),
                ["cancelUrl"] = request.CancelUrl,
                ["description"] = request.Description,
                ["orderCode"] = request.OrderCode.ToString(),
                ["returnUrl"] = request.ReturnUrl
            };

            if (!string.IsNullOrWhiteSpace(request.BuyerName))
            {
                parts["buyerName"] = request.BuyerName;
            }

            if (!string.IsNullOrWhiteSpace(request.BuyerEmail))
            {
                parts["buyerEmail"] = request.BuyerEmail;
            }

            if (!string.IsNullOrWhiteSpace(request.BuyerPhone))
            {
                parts["buyerPhone"] = request.BuyerPhone;
            }

            if (request.Items != null)
            {
                for (var i = 0; i < request.Items.Count; i++)
                {
                    var item = request.Items[i];
                    parts[$"items[{i}].name"] = item.Name;
                    parts[$"items[{i}].price"] = item.Price.ToString();
                    parts[$"items[{i}].quantity"] = item.Quantity.ToString();
                }
            }

            var payload = string.Join("|", parts.Select(kv => $"{kv.Key}={kv.Value}"));
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

            [JsonPropertyName("items")]
            public IReadOnlyList<PayOsCreatePaymentRequestItem>? Items { get; set; }
        }

        private sealed class PayOsCreatePaymentRequestItem
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("quantity")]
            public long Quantity { get; set; }

            [JsonPropertyName("price")]
            public long Price { get; set; }
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

            [JsonPropertyName("shortLink")]
            public string? ShortLink { get; set; }
        }
    }
}
