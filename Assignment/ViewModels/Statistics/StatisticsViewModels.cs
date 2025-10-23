using System;
using System.ComponentModel.DataAnnotations;
using Assignment.Enums;
using System.Linq;

namespace Assignment.ViewModels.Statistics
{
    public class StatisticsFilterInputModel
    {
        public StatisticsPeriodType PeriodType { get; set; } = StatisticsPeriodType.Day;

        [DataType(DataType.DateTime)]
        public DateTime? PrimaryStart { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime? PrimaryEnd { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime? CompareStart { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime? CompareEnd { get; set; }

        [DataType(DataType.Date)]
        public DateTime? PrimaryDay { get; set; }

        [DataType(DataType.Time)]
        public TimeSpan? PrimaryStartTime { get; set; }

        [DataType(DataType.Time)]
        public TimeSpan? PrimaryEndTime { get; set; }

        [DataType(DataType.Date)]
        public DateTime? CompareDay { get; set; }

        [DataType(DataType.Time)]
        public TimeSpan? CompareStartTime { get; set; }

        [DataType(DataType.Time)]
        public TimeSpan? CompareEndTime { get; set; }

        public int? PrimaryStartQuarter { get; set; }
        public int? PrimaryEndQuarter { get; set; }
        public int? CompareStartQuarter { get; set; }
        public int? CompareEndQuarter { get; set; }

        public int? PrimaryStartYear { get; set; }
        public int? PrimaryEndYear { get; set; }
        public int? CompareStartYear { get; set; }
        public int? CompareEndYear { get; set; }
    }

    public class StatisticsDataPointViewModel
    {
        public string Label { get; set; } = string.Empty;
        public double TotalBill { get; set; }
        public long TotalQuantity { get; set; }
    }

    public class StatisticsSeriesViewModel
    {
        public string Name { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public List<StatisticsDataPointViewModel> DataPoints { get; set; } = new();

        public double TotalBill => DataPoints.Sum(point => point.TotalBill);
        public long TotalQuantity => DataPoints.Sum(point => point.TotalQuantity);
    }

    public class ProductRevenueDistributionViewModel
    {
        public string Name { get; set; } = string.Empty;
        public double TotalBill { get; set; }
        public double Percentage { get; set; }
    }

    public class StatisticsViewModel
    {
        public StatisticsFilterInputModel Filter { get; set; } = new();
        public StatisticsSeriesViewModel PrimarySeries { get; set; } = new();
        public StatisticsSeriesViewModel? CompareSeries { get; set; }
        public List<ProductRevenueDistributionViewModel> ProductDistribution { get; set; } = new();
    }
}
