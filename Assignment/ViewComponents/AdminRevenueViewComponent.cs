using System;
using System.Linq;
using Assignment.Data;
using Assignment.Enums;
using Assignment.ViewModels.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Assignment.ViewComponents
{
    public class AdminRevenueViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public AdminRevenueViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var now = DateTime.Now;
            var startOfToday = now.Date;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);

            var revenueStatuses = new[] { OrderStatus.Paid, OrderStatus.Completed };
            var pendingStatuses = new[] { OrderStatus.Pending, OrderStatus.Processing };

            var revenueQuery = _context.Orders
                .Where(o => !o.IsDeleted && revenueStatuses.Contains(o.Status));

            var totalRevenue = await revenueQuery.SumAsync(o => (double?)o.TotalBill) ?? 0;
            var monthlyRevenue = await revenueQuery
                .Where(o => o.CreatedAt >= startOfMonth)
                .SumAsync(o => (double?)o.TotalBill) ?? 0;
            var todayRevenue = await revenueQuery
                .Where(o => o.CreatedAt >= startOfToday)
                .SumAsync(o => (double?)o.TotalBill) ?? 0;

            var completedOrders = await revenueQuery.CountAsync();
            var pendingOrders = await _context.Orders
                .Where(o => !o.IsDeleted && pendingStatuses.Contains(o.Status))
                .CountAsync();

            var viewModel = new AdminRevenueSummaryViewModel
            {
                TotalRevenue = totalRevenue,
                MonthlyRevenue = monthlyRevenue,
                TodayRevenue = todayRevenue,
                CompletedOrders = completedOrders,
                PendingOrders = pendingOrders
            };

            return View(viewModel);
        }
    }
}
