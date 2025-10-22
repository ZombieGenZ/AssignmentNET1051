using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Assignment.Data;
using Assignment.Enums;
using Assignment.Models;
using Assignment.ViewModels.Statistics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using System.IO;

namespace Assignment.Controllers
{
    [Authorize(Policy = "ViewStatisticsPolicy")]
    public class StatisticsController : Controller
    {
        private static readonly HashSet<OrderStatus> RevenueStatuses = new(new[]
        {
            OrderStatus.Paid,
            OrderStatus.Completed
        });

        private readonly ApplicationDbContext _context;

        public StatisticsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] StatisticsFilterInputModel? filter)
        {
            filter ??= new StatisticsFilterInputModel();
            NormalizeFilter(filter);

            var viewModel = new StatisticsViewModel
            {
                Filter = filter
            };

            var primarySeries = await BuildSeriesAsync("Khoảng thời gian chính", filter.PeriodType,
                filter.PrimaryStart!.Value, filter.PrimaryEnd!.Value);
            viewModel.PrimarySeries = primarySeries;

            viewModel.ProductDistribution = await BuildProductDistributionAsync(filter.PeriodType,
                filter.PrimaryStart.Value, filter.PrimaryEnd.Value);

            if (filter.CompareStart.HasValue && filter.CompareEnd.HasValue)
            {
                var compareSeries = await BuildSeriesAsync("So sánh", filter.PeriodType,
                    filter.CompareStart.Value, filter.CompareEnd.Value);
                viewModel.CompareSeries = compareSeries;
            }

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Export([FromQuery] StatisticsFilterInputModel filter)
        {
            NormalizeFilter(filter);

            var primarySeries = await BuildSeriesAsync("Khoảng thời gian chính", filter.PeriodType,
                filter.PrimaryStart!.Value, filter.PrimaryEnd!.Value);
            var compareSeries = filter.CompareStart.HasValue && filter.CompareEnd.HasValue
                ? await BuildSeriesAsync("So sánh", filter.PeriodType,
                    filter.CompareStart.Value, filter.CompareEnd.Value)
                : null;

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Doanh thu");

            var titleRange = worksheet.Range(1, 1, 1, 4);
            titleRange.Merge();
            titleRange.Value = "Báo cáo thống kê doanh thu";
            titleRange.Style.Font.SetBold().Font.SetFontSize(16);
            titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var timestampRange = worksheet.Range(2, 1, 2, 4);
            timestampRange.Merge();
            timestampRange.Value = $"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}";
            timestampRange.Style.Font.SetItalic();
            timestampRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var currentRow = 4;
            AppendSeries(worksheet, ref currentRow, primarySeries);

            if (compareSeries != null)
            {
                AppendSeries(worksheet, ref currentRow, compareSeries);
            }

            worksheet.Columns(1, 4).AdjustToContents();
            worksheet.Column(2).Style.NumberFormat.Format = "#,##0";
            worksheet.Column(3).Style.NumberFormat.Format = "#,##0";
            worksheet.Column(4).Style.NumberFormat.Format = "0.00%";

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var fileName = $"bao-cao-thong-ke-{DateTime.Now:yyyyMMddHHmmss}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        private static void AppendSeries(IXLWorksheet worksheet, ref int currentRow, StatisticsSeriesViewModel series)
        {
            var headerRange = worksheet.Range(currentRow, 1, currentRow, 4);
            headerRange.Merge();
            headerRange.Value = $"{series.Name}: {series.Start:dd/MM/yyyy} - {series.End:dd/MM/yyyy}";
            headerRange.Style.Font.SetBold();
            headerRange.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#e0f2fe"));
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            currentRow++;

            worksheet.Cell(currentRow, 1).Value = "Nhãn";
            worksheet.Cell(currentRow, 2).Value = "Số lượng";
            worksheet.Cell(currentRow, 3).Value = "Doanh thu (VND)";
            worksheet.Cell(currentRow, 4).Value = "Tỷ trọng (%)";
            var columnsHeader = worksheet.Range(currentRow, 1, currentRow, 4);
            columnsHeader.Style.Font.SetBold();
            columnsHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            columnsHeader.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#eff6ff"));
            columnsHeader.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            currentRow++;

            foreach (var point in series.DataPoints)
            {
                worksheet.Cell(currentRow, 1).Value = point.Label;
                worksheet.Cell(currentRow, 2).Value = point.TotalQuantity;
                worksheet.Cell(currentRow, 3).Value = point.TotalBill;
                worksheet.Cell(currentRow, 4).Value = series.TotalBill > 0
                    ? point.TotalBill / series.TotalBill
                    : 0;
                currentRow++;
            }

            worksheet.Cell(currentRow, 1).Value = "Tổng cộng";
            worksheet.Cell(currentRow, 2).Value = series.TotalQuantity;
            worksheet.Cell(currentRow, 3).Value = series.TotalBill;
            worksheet.Cell(currentRow, 4).Value = series.TotalBill > 0 ? 1 : 0;

            var totalRange = worksheet.Range(currentRow, 1, currentRow, 4);
            totalRange.Style.Font.SetBold();
            totalRange.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#dbeafe"));
            totalRange.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            totalRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            currentRow += 2;
        }

        private async Task<List<ProductRevenueDistributionViewModel>> BuildProductDistributionAsync(StatisticsPeriodType periodType,
            DateTime startDate, DateTime endDate)
        {
            var (startInclusive, endInclusive) = GetActualRange(periodType, startDate, endDate);
            var endExclusive = endInclusive.AddTicks(1);

            var items = await _context.OrderItems
                .AsNoTracking()
                .Where(item => !item.IsDeleted && item.Order != null && !item.Order.IsDeleted &&
                               RevenueStatuses.Contains(item.Order.Status) &&
                               item.Order.CreatedAt >= startInclusive && item.Order.CreatedAt < endExclusive)
                .Select(item => new
                {
                    item.ProductId,
                    ProductName = item.Product != null ? item.Product.Name : null,
                    item.ComboId,
                    ComboName = item.Combo != null ? item.Combo.Name : null,
                    Revenue = item.Price * item.Quantity
                })
                .ToListAsync();

            var grouped = items
                .GroupBy(item => item.ProductId.HasValue
                    ? item.ProductName ?? $"Sản phẩm #{item.ProductId}"
                    : item.ComboId.HasValue
                        ? item.ComboName ?? $"Combo #{item.ComboId}"
                        : "Khác")
                .Select(group => new
                {
                    Name = group.Key,
                    Revenue = group.Sum(x => x.Revenue)
                })
                .Where(result => result.Revenue > 0)
                .OrderByDescending(result => result.Revenue)
                .ToList();

            if (grouped.Count > 10)
            {
                var leading = grouped.Take(9).ToList();
                var othersRevenue = grouped.Skip(9).Sum(item => item.Revenue);
                if (othersRevenue > 0)
                {
                    leading.Add(new { Name = "Khác", Revenue = othersRevenue });
                }

                grouped = leading;
            }

            var totalRevenue = grouped.Sum(item => item.Revenue);

            return grouped
                .Select(item => new ProductRevenueDistributionViewModel
                {
                    Name = item.Name,
                    TotalBill = Math.Round(item.Revenue, 2),
                    Percentage = totalRevenue > 0
                        ? Math.Round(item.Revenue / totalRevenue * 100, 2)
                        : 0
                })
                .ToList();
        }

        private async Task<StatisticsSeriesViewModel> BuildSeriesAsync(string name, StatisticsPeriodType periodType,
            DateTime startDate, DateTime endDate)
        {
            var (startInclusive, endInclusive) = GetActualRange(periodType, startDate, endDate);
            var endExclusive = endInclusive.AddTicks(1);

            var query = _context.Orders
                .AsNoTracking()
                .Where(order => !order.IsDeleted && RevenueStatuses.Contains(order.Status) &&
                                order.CreatedAt >= startInclusive && order.CreatedAt < endExclusive);

            var dataPoints = periodType switch
            {
                StatisticsPeriodType.Hour => await BuildHourlyDataAsync(query, startInclusive, endInclusive),
                StatisticsPeriodType.Day => await BuildDailyDataAsync(query, startInclusive, endInclusive),
                StatisticsPeriodType.Month => await BuildMonthlyDataAsync(query, startInclusive, endInclusive),
                StatisticsPeriodType.Quarter => await BuildQuarterlyDataAsync(query, startInclusive, endInclusive),
                StatisticsPeriodType.Year => await BuildYearlyDataAsync(query, startInclusive, endInclusive),
                _ => throw new ArgumentOutOfRangeException(nameof(periodType), periodType, null)
            };

            return new StatisticsSeriesViewModel
            {
                Name = name,
                Start = startInclusive,
                End = endInclusive,
                DataPoints = dataPoints
            };
        }

        private static (DateTime startInclusive, DateTime endInclusive) GetActualRange(StatisticsPeriodType periodType,
            DateTime startDate, DateTime endDate)
        {
            return periodType switch
            {
                StatisticsPeriodType.Hour =>
                    (startDate.Date, startDate.Date.AddDays(1).AddTicks(-1)),
                StatisticsPeriodType.Day =>
                    (startDate.Date, endDate.Date.AddDays(1).AddTicks(-1)),
                StatisticsPeriodType.Month =>
                    (new DateTime(startDate.Year, startDate.Month, 1),
                        new DateTime(endDate.Year, endDate.Month, 1).AddMonths(1).AddTicks(-1)),
                StatisticsPeriodType.Quarter =>
                    (GetQuarterStart(startDate), GetQuarterStart(endDate).AddMonths(3).AddTicks(-1)),
                StatisticsPeriodType.Year =>
                    (new DateTime(startDate.Year, 1, 1), new DateTime(endDate.Year, 1, 1).AddYears(1).AddTicks(-1)),
                _ => throw new ArgumentOutOfRangeException(nameof(periodType), periodType, null)
            };
        }

        private async Task<List<StatisticsDataPointViewModel>> BuildHourlyDataAsync(IQueryable<Order> query,
            DateTime startInclusive, DateTime endInclusive)
        {
            var aggregated = await query
                .GroupBy(order => new DateTime(order.CreatedAt.Year, order.CreatedAt.Month, order.CreatedAt.Day,
                    order.CreatedAt.Hour, 0, 0))
                .Select(group => new
                {
                    group.Key,
                    TotalBill = group.Sum(order => order.TotalBill),
                    TotalQuantity = group.Sum(order => order.TotalQuantity)
                })
                .ToListAsync();

            var lookup = aggregated.ToDictionary(item => item.Key);
            var results = new List<StatisticsDataPointViewModel>();
            var current = new DateTime(startInclusive.Year, startInclusive.Month, startInclusive.Day, startInclusive.Hour, 0, 0);
            var endHour = new DateTime(endInclusive.Year, endInclusive.Month, endInclusive.Day, endInclusive.Hour, 0, 0);

            while (current <= endHour)
            {
                lookup.TryGetValue(current, out var data);
                results.Add(new StatisticsDataPointViewModel
                {
                    Label = current.ToString("HH:00"),
                    TotalBill = data?.TotalBill ?? 0,
                    TotalQuantity = data?.TotalQuantity ?? 0
                });

                current = current.AddHours(1);
            }

            return results;
        }

        private async Task<List<StatisticsDataPointViewModel>> BuildDailyDataAsync(IQueryable<Order> query,
            DateTime startInclusive, DateTime endInclusive)
        {
            var aggregated = await query
                .GroupBy(order => order.CreatedAt.Date)
                .Select(group => new
                {
                    group.Key,
                    TotalBill = group.Sum(order => order.TotalBill),
                    TotalQuantity = group.Sum(order => order.TotalQuantity)
                })
                .ToListAsync();

            var lookup = aggregated.ToDictionary(item => item.Key);
            var results = new List<StatisticsDataPointViewModel>();
            var current = startInclusive.Date;
            var endDay = endInclusive.Date;

            while (current <= endDay)
            {
                lookup.TryGetValue(current, out var data);
                results.Add(new StatisticsDataPointViewModel
                {
                    Label = current.ToString("dd/MM/yyyy"),
                    TotalBill = data?.TotalBill ?? 0,
                    TotalQuantity = data?.TotalQuantity ?? 0
                });

                current = current.AddDays(1);
            }

            return results;
        }

        private async Task<List<StatisticsDataPointViewModel>> BuildMonthlyDataAsync(IQueryable<Order> query,
            DateTime startInclusive, DateTime endInclusive)
        {
            var aggregated = await query
                .GroupBy(order => new { order.CreatedAt.Year, order.CreatedAt.Month })
                .Select(group => new
                {
                    group.Key.Year,
                    group.Key.Month,
                    TotalBill = group.Sum(order => order.TotalBill),
                    TotalQuantity = group.Sum(order => order.TotalQuantity)
                })
                .ToListAsync();

            var lookup = aggregated.ToDictionary(item => (item.Year, item.Month));
            var results = new List<StatisticsDataPointViewModel>();
            var current = new DateTime(startInclusive.Year, startInclusive.Month, 1);
            var endMonth = new DateTime(endInclusive.Year, endInclusive.Month, 1);

            while (current <= endMonth)
            {
                lookup.TryGetValue((current.Year, current.Month), out var data);
                results.Add(new StatisticsDataPointViewModel
                {
                    Label = current.ToString("MM/yyyy"),
                    TotalBill = data?.TotalBill ?? 0,
                    TotalQuantity = data?.TotalQuantity ?? 0
                });

                current = current.AddMonths(1);
            }

            return results;
        }

        private async Task<List<StatisticsDataPointViewModel>> BuildQuarterlyDataAsync(IQueryable<Order> query,
            DateTime startInclusive, DateTime endInclusive)
        {
            var aggregated = await query
                .GroupBy(order => new
                {
                    order.CreatedAt.Year,
                    Quarter = (order.CreatedAt.Month - 1) / 3 + 1
                })
                .Select(group => new
                {
                    group.Key.Year,
                    group.Key.Quarter,
                    TotalBill = group.Sum(order => order.TotalBill),
                    TotalQuantity = group.Sum(order => order.TotalQuantity)
                })
                .ToListAsync();

            var lookup = aggregated.ToDictionary(item => (item.Year, item.Quarter));
            var results = new List<StatisticsDataPointViewModel>();
            var current = GetQuarterStart(startInclusive);
            var endQuarter = GetQuarterStart(endInclusive);

            while (current <= endQuarter)
            {
                var quarter = GetQuarter(current);
                lookup.TryGetValue((current.Year, quarter), out var data);
                results.Add(new StatisticsDataPointViewModel
                {
                    Label = $"Q{quarter} {current:yyyy}",
                    TotalBill = data?.TotalBill ?? 0,
                    TotalQuantity = data?.TotalQuantity ?? 0
                });

                current = current.AddMonths(3);
            }

            return results;
        }

        private async Task<List<StatisticsDataPointViewModel>> BuildYearlyDataAsync(IQueryable<Order> query,
            DateTime startInclusive, DateTime endInclusive)
        {
            var aggregated = await query
                .GroupBy(order => order.CreatedAt.Year)
                .Select(group => new
                {
                    Year = group.Key,
                    TotalBill = group.Sum(order => order.TotalBill),
                    TotalQuantity = group.Sum(order => order.TotalQuantity)
                })
                .ToListAsync();

            var lookup = aggregated.ToDictionary(item => item.Year);
            var results = new List<StatisticsDataPointViewModel>();

            for (var year = startInclusive.Year; year <= endInclusive.Year; year++)
            {
                lookup.TryGetValue(year, out var data);
                results.Add(new StatisticsDataPointViewModel
                {
                    Label = year.ToString(),
                    TotalBill = data?.TotalBill ?? 0,
                    TotalQuantity = data?.TotalQuantity ?? 0
                });
            }

            return results;
        }

        private static DateTime GetQuarterStart(DateTime date)
        {
            var quarter = (date.Month - 1) / 3;
            var month = quarter * 3 + 1;
            return new DateTime(date.Year, month, 1);
        }

        private static int GetQuarter(DateTime date) => (date.Month - 1) / 3 + 1;

        private static void NormalizeFilter(StatisticsFilterInputModel filter)
        {
            var today = DateTime.Today;

            filter.PrimaryStart = filter.PrimaryStart?.Date;
            filter.PrimaryEnd = filter.PrimaryEnd?.Date;
            filter.CompareStart = filter.CompareStart?.Date;
            filter.CompareEnd = filter.CompareEnd?.Date;

            switch (filter.PeriodType)
            {
                case StatisticsPeriodType.Hour:
                    var baseDay = (filter.PrimaryStart ?? today).Date;
                    filter.PrimaryStart = baseDay;
                    filter.PrimaryEnd = baseDay;

                    if (filter.CompareStart.HasValue)
                    {
                        var compareDay = filter.CompareStart.Value.Date;
                        filter.CompareStart = compareDay;
                        filter.CompareEnd = filter.CompareEnd?.Date ?? compareDay;
                    }
                    else
                    {
                        filter.CompareStart = null;
                        filter.CompareEnd = null;
                    }

                    break;

                case StatisticsPeriodType.Day:
                    var defaultStart = new DateTime(today.Year, today.Month, 1);
                    var defaultEnd = defaultStart.AddMonths(1).AddDays(-1);
                    var startDay = (filter.PrimaryStart ?? defaultStart).Date;
                    DateTime endDay;

                    if (filter.PrimaryEnd.HasValue)
                    {
                        endDay = filter.PrimaryEnd.Value.Date;
                    }
                    else if (filter.PrimaryStart.HasValue)
                    {
                        endDay = startDay;
                    }
                    else
                    {
                        endDay = defaultEnd.Date;
                    }

                    if (endDay < startDay)
                    {
                        (startDay, endDay) = (endDay, startDay);
                    }

                    filter.PrimaryStart = startDay;
                    filter.PrimaryEnd = endDay;
                    NormalizeComparisonRange(filter);
                    break;

                case StatisticsPeriodType.Month:
                    var monthStart = filter.PrimaryStart ?? new DateTime(today.Year, today.Month, 1);
                    monthStart = new DateTime(monthStart.Year, monthStart.Month, 1);
                    var monthEndInput = filter.PrimaryEnd ?? monthStart;
                    var monthEnd = new DateTime(monthEndInput.Year, monthEndInput.Month, 1);
                    if (monthEnd < monthStart)
                    {
                        (monthStart, monthEnd) = (monthEnd, monthStart);
                    }

                    filter.PrimaryStart = monthStart;
                    filter.PrimaryEnd = monthEnd;
                    NormalizeComparisonRange(filter, alignToMonth: true);
                    break;

                case StatisticsPeriodType.Quarter:
                    var quarterStart = GetQuarterStart(filter.PrimaryStart ?? today);
                    var quarterEndInput = filter.PrimaryEnd ?? quarterStart;
                    var quarterEnd = GetQuarterStart(quarterEndInput);
                    if (quarterEnd < quarterStart)
                    {
                        (quarterStart, quarterEnd) = (quarterEnd, quarterStart);
                    }

                    filter.PrimaryStart = quarterStart;
                    filter.PrimaryEnd = quarterEnd;
                    NormalizeComparisonRange(filter, alignToQuarter: true);
                    break;

                case StatisticsPeriodType.Year:
                    var yearStart = new DateTime((filter.PrimaryStart ?? today).Year, 1, 1);
                    var yearEndInput = filter.PrimaryEnd ?? yearStart;
                    var yearEnd = new DateTime(yearEndInput.Year, 1, 1);
                    if (yearEnd < yearStart)
                    {
                        (yearStart, yearEnd) = (yearEnd, yearStart);
                    }

                    filter.PrimaryStart = yearStart;
                    filter.PrimaryEnd = yearEnd;
                    NormalizeComparisonRange(filter, alignToYear: true);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static void NormalizeComparisonRange(StatisticsFilterInputModel filter, bool alignToMonth = false,
            bool alignToQuarter = false, bool alignToYear = false)
        {
            if (!filter.CompareStart.HasValue || !filter.CompareEnd.HasValue)
            {
                filter.CompareStart = null;
                filter.CompareEnd = null;
                return;
            }

            var compareStart = filter.CompareStart.Value;
            var compareEnd = filter.CompareEnd.Value;

            if (alignToMonth)
            {
                compareStart = new DateTime(compareStart.Year, compareStart.Month, 1);
                compareEnd = new DateTime(compareEnd.Year, compareEnd.Month, 1);
            }
            else if (alignToQuarter)
            {
                compareStart = GetQuarterStart(compareStart);
                compareEnd = GetQuarterStart(compareEnd);
            }
            else if (alignToYear)
            {
                compareStart = new DateTime(compareStart.Year, 1, 1);
                compareEnd = new DateTime(compareEnd.Year, 1, 1);
            }
            else
            {
                compareStart = compareStart.Date;
                compareEnd = compareEnd.Date;
            }

            if (compareEnd < compareStart)
            {
                (compareStart, compareEnd) = (compareEnd, compareStart);
            }

            filter.CompareStart = compareStart;
            filter.CompareEnd = compareEnd;
        }
    }
}
