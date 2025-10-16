using Assignment.Models;
using Microsoft.AspNetCore.Http;

namespace Assignment.Services.PayOs
{
    public interface IPayOsService
    {
        Task<string> CreatePaymentUrlAsync(Order order, string successUrl, string cancelUrl, CancellationToken cancellationToken = default);
        bool ValidateRedirectSignature(IQueryCollection query);
    }
}
