using Assignment.Models;

namespace Assignment.Services.Payments
{
    public interface IPayOsService
    {
        Task<PayOsPaymentLink?> CreatePaymentLinkAsync(Order order, string description, string returnUrl, string cancelUrl, CancellationToken cancellationToken = default);

        Task<PayOsPaymentDetails?> GetPaymentDetailsAsync(long orderCode, CancellationToken cancellationToken = default);
    }
}
