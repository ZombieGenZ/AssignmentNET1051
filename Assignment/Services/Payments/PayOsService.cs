using System;
using System.Linq;
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
        private readonly HttpClient _httpClient;
        private readonly ILogger<PayOsService> _logger;
        private readonly PayOsOptions _options;

        public PayOsService(HttpClient httpClient, IOptions<PayOsOptions> options, ILogger<PayOsService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _options = options.Value;

            var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl) ? "https://api.payos.vn/v2/" : _options.BaseUrl.Trim();
            if (!baseUrl.EndsWith("/", StringComparison.Ordinal))
            {
                baseUrl += "/";
            }
            if (_httpClient.BaseAddress == null)
            {
                _httpClient.BaseAddress = new Uri(baseUrl);
            }
            else if (_httpClient.BaseAddress.AbsoluteUri != baseUrl)
            {
                _httpClient.BaseAddress = new Uri(baseUrl);
            }

            ConfigureDefaultHeaders();
        }

        public async Task<PayOsPaymentLink?> CreatePaymentLinkAsync(Order order, string description, string returnUrl, string cancelUrl, CancellationToken cancellationToken = default)
        {
            var amount = (long)Math.Round(order.TotalBill);
            if (amount <= 0)
            {
                _logger.LogWarning("Attempted to create PayOS payment link for order {OrderId} with invalid amount {Amount}", order.Id, amount);
                return null;
            }

            var request = new PayOsCreatePaymentRequest
            {
                OrderCode = order.Id,
                Amount = amount,
                Description = description,
                ReturnUrl = returnUrl,
                CancelUrl = cancelUrl,
                Items = order.OrderItems?.Select(item => new PayOsItem
                {
                    Name = item.Product?.Name ?? item.Combo?.Name ?? $"Sản phẩm #{item.ProductId ?? item.ComboId}",
                    Quantity = item.Quantity,
                    Price = (long)Math.Round(item.Price)
                }).ToList(),
                BuyerInfo = new PayOsBuyerInfo
                {
                    Name = order.Name,
                    Phone = order.Phone,
                    Email = order.Email
                }
            };

            request.Signature = GenerateSignature(request.OrderCode, request.Amount, request.Description, request.ReturnUrl, request.CancelUrl);

            try
            {
                var response = await _httpClient.PostAsJsonAsync(PaymentRequestEndpoint, request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("PayOS create payment request failed for order {OrderId} with status code {StatusCode}", order.Id, response.StatusCode);
                    return null;
                }

                var payload = await response.Content.ReadFromJsonAsync<PayOsResponse<PayOsPaymentLink>>(cancellationToken: cancellationToken);
                if (payload?.Data == null)
                {
                    _logger.LogError("PayOS create payment request returned no data for order {OrderId}", order.Id);
                }

                return payload?.Data;
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

            if (!string.IsNullOrWhiteSpace(_options.ClientId))
            {
                _httpClient.DefaultRequestHeaders.Add("x-client-id", _options.ClientId);
            }

            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("x-api-key", _options.ApiKey);
            }
        }

        private string GenerateSignature(long orderCode, long amount, string description, string returnUrl, string cancelUrl)
        {
            var data = new SortedDictionary<string, string>
            {
                { "amount", amount.ToString() },
                { "cancelUrl", cancelUrl },
                { "description", description },
                { "orderCode", orderCode.ToString() },
                { "returnUrl", returnUrl }
            };

            var rawData = string.Join("&", data.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.ChecksumKey ?? string.Empty));
            var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
    }
}
