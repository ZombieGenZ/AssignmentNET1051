using System.Text.Json.Serialization;

namespace Assignment.Services.Payments
{
    public class PayOsCreatePaymentRequest
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

        [JsonPropertyName("items")]
        public List<PayOsItem>? Items { get; set; }

        [JsonPropertyName("buyerInfo")]
        public PayOsBuyerInfo? BuyerInfo { get; set; }

        [JsonPropertyName("signature")]
        public string Signature { get; set; } = string.Empty;
    }

    public class PayOsBuyerInfo
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("phone")]
        public string? Phone { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }
    }

    public class PayOsItem
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("quantity")]
        public long Quantity { get; set; }

        [JsonPropertyName("price")]
        public long Price { get; set; }
    }

    public class PayOsResponse<T>
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("desc")]
        public string? Description { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }

    public class PayOsPaymentLink
    {
        [JsonPropertyName("orderCode")]
        public long OrderCode { get; set; }

        [JsonPropertyName("checkoutUrl")]
        public string? CheckoutUrl { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }

    public class PayOsPaymentDetails
    {
        [JsonPropertyName("orderCode")]
        public long OrderCode { get; set; }

        [JsonPropertyName("amount")]
        public long Amount { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("checkoutUrl")]
        public string? CheckoutUrl { get; set; }
    }
}
