namespace Assignment.Services.PayOs
{
    public class PayOsOptions
    {
        public const string SectionName = "PayOs";

        public string ClientId { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string ChecksumKey { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://api-merchant.payos.vn/";
    }
}
