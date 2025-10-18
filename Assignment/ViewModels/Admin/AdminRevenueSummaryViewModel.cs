namespace Assignment.ViewModels.Admin
{
    public class AdminRevenueSummaryViewModel
    {
        public double TotalRevenue { get; set; }
        public double MonthlyRevenue { get; set; }
        public double TodayRevenue { get; set; }
        public int CompletedOrders { get; set; }
        public int PendingOrders { get; set; }

        public double AverageOrderValue => CompletedOrders > 0 ? TotalRevenue / CompletedOrders : 0;
    }
}
