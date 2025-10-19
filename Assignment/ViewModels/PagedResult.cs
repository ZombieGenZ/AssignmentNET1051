using System;
using System.Collections.Generic;
using System.Linq;

namespace Assignment.ViewModels
{
    public class PagedResult<T>
    {
        private int _pageSize;
        private int _currentPage;

        public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

        public int CurrentPage
        {
            get => _currentPage <= 0 ? 1 : _currentPage;
            set => _currentPage = value <= 0 ? 1 : value;
        }

        public int PageSize
        {
            get => _pageSize <= 0 ? 1 : _pageSize;
            set => _pageSize = value <= 0 ? 1 : value;
        }

        public int TotalItems { get; set; }

        public IReadOnlyList<int> PageSizeOptions { get; set; } = Array.Empty<int>();

        public int TotalPages => PageSize <= 0
            ? 0
            : (int)Math.Ceiling(TotalItems / (double)PageSize);

        public int StartItem => TotalItems == 0 ? 0 : (CurrentPage - 1) * PageSize + 1;

        public int EndItem => TotalItems == 0
            ? 0
            : Math.Min(CurrentPage * PageSize, TotalItems);

        public PagedResult<T> EnsureValidPage()
        {
            if (TotalPages > 0 && CurrentPage > TotalPages)
            {
                CurrentPage = TotalPages;
            }

            if (TotalPages == 0)
            {
                CurrentPage = 1;
            }

            return this;
        }

        public void SetItems(IEnumerable<T> items)
        {
            Items = items?.ToList() ?? Array.Empty<T>();
        }
    }
}

