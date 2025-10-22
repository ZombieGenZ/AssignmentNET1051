using Assignment.Enums;

namespace Assignment.ViewModels.Rewards
{
    public class CustomerRewardItemViewModel
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public long PointCost { get; set; }
        public CustomerRank? MinimumRank { get; set; }
        public long Quantity { get; set; }
        public long Redeemed { get; set; }
        public int ValidityValue { get; set; }
        public RewardValidityUnit ValidityUnit { get; set; }
        public bool IsPublish { get; set; }
        public bool IsAvailable { get; set; }
    }

    public class CustomerRewardIndexViewModel
    {
        public Assignment.ViewModels.PagedResult<CustomerRewardItemViewModel> Rewards { get; set; } = new();
        public long CurrentPoint { get; set; }
        public long TotalPoint { get; set; }
        public long Exp { get; set; }
        public CustomerRank Rank { get; set; }
        public CustomerRank? NextRank { get; set; }
        public long? NextRankExp { get; set; }
    }
}
