using System.Collections.Generic;
using System.Linq;

namespace Assignment.Options
{
    public static class PaginationDefaults
    {
        public const int DefaultPageSize = 25;

        public static readonly IReadOnlyList<int> PageSizeOptions = new[] { 25, 50, 75, 100 };

        public static int NormalizePage(int page)
        {
            return page < 1 ? 1 : page;
        }

        public static int NormalizePageSize(int pageSize)
        {
            return PageSizeOptions.Contains(pageSize) ? pageSize : PageSizeOptions[0];
        }
    }
}
