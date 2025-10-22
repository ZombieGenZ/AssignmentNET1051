using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using Assignment.Enums;

namespace Assignment.Extensions
{
    public static class CustomerRankExtensions
    {
        public static string GetDisplayName(this CustomerRank rank)
        {
            var memberInfo = rank.GetType().GetMember(rank.ToString());
            var displayAttribute = memberInfo.FirstOrDefault()?.GetCustomAttribute<DisplayAttribute>();
            return displayAttribute?.GetName() ?? rank.ToString();
        }

        public static bool IsAtLeast(this CustomerRank rank, CustomerRank other)
            => rank >= other;

        public static bool TryParse(int? value, out CustomerRank rank)
        {
            if (value.HasValue && Enum.IsDefined(typeof(CustomerRank), value.Value))
            {
                rank = (CustomerRank)value.Value;
                return true;
            }

            rank = default;
            return false;
        }
    }
}
