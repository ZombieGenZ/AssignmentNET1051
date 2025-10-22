using System;
using System.Collections.Generic;

namespace Assignment.ViewModels.Vouchers
{
    public class PaginatedVoucherCollectionViewModel
    {
        public IReadOnlyCollection<VoucherSummaryViewModel> Items { get; set; } = new List<VoucherSummaryViewModel>();
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 6;
        public int TotalItems { get; set; } = 0;

        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalItems / (double)PageSize) : 0;
        public int StartItem => TotalItems == 0 ? 0 : (CurrentPage - 1) * PageSize + 1;
        public int EndItem => TotalItems == 0 ? 0 : Math.Min(TotalItems, CurrentPage * PageSize);
        public bool HasPrevious => CurrentPage > 1;
        public bool HasNext => CurrentPage < TotalPages;
    }

    public class UserVoucherListViewModel
    {
        public PaginatedVoucherCollectionViewModel PrivateVouchers { get; set; } = new();
        public PaginatedVoucherCollectionViewModel SavedVouchers { get; set; } = new();
    }
}
