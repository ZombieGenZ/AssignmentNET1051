using System.Collections.Generic;

namespace Assignment.ViewModels.Vouchers
{
    public class UserVoucherListViewModel
    {
        public IReadOnlyCollection<VoucherSummaryViewModel> PrivateVouchers { get; set; } = new List<VoucherSummaryViewModel>();
        public IReadOnlyCollection<VoucherSummaryViewModel> SavedVouchers { get; set; } = new List<VoucherSummaryViewModel>();
    }
}
