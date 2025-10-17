using System.ComponentModel.DataAnnotations;
using Assignment.Enums;
using System.Linq;

namespace Assignment.ViewModels.Statistics
{
    public class StatisticsFilterInputModel
    {
        public StatisticsPeriodType PeriodType { get; set; } = StatisticsPeriodType.Month;

        [DataType(DataType.Date)]
        public DateTime? PrimaryStart { get; set; }

        [DataType(DataType.Date)]
        public DateTime? PrimaryEnd { get; set; }

        [DataType(DataType.Date)]
        public DateTime? CompareStart { get; set; }

        [DataType(DataType.Date)]
        public DateTime? CompareEnd { get; set; }
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

    public class StatisticsViewModel
    {
        public StatisticsFilterInputModel Filter { get; set; } = new();
        public StatisticsSeriesViewModel PrimarySeries { get; set; } = new();
        public StatisticsSeriesViewModel? CompareSeries { get; set; }
    }
}
