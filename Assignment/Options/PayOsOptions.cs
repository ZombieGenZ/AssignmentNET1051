namespace Assignment.Options
{
    public class PayOsOptions
    {
        public string ClientId { get; set; } = string.Empty;

        public string ApiKey { get; set; } = string.Empty;

        public string ChecksumKey { get; set; } = string.Empty;

        public string BaseUrl { get; set; } = "https://api.payos.vn/v2/";
    }
}
