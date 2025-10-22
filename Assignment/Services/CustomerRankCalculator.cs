using Assignment.Enums;

namespace Assignment.Services
{
    public static class CustomerRankCalculator
    {
        private const long BronzeThreshold = 1_000_000;
        private const long SilverThreshold = 3_000_000;
        private const long GoldThreshold = 6_000_000;
        private const long PlatinumThreshold = 10_000_000;
        private const long DiamondThreshold = 20_000_000;
        private const long EmeraldThreshold = 50_000_000;

        public static CustomerRank CalculateRank(long exp)
        {
            if (exp >= EmeraldThreshold)
            {
                return CustomerRank.Emerald;
            }

            if (exp >= DiamondThreshold)
            {
                return CustomerRank.Diamond;
            }

            if (exp >= PlatinumThreshold)
            {
                return CustomerRank.Platinum;
            }

            if (exp >= GoldThreshold)
            {
                return CustomerRank.Gold;
            }

            if (exp >= SilverThreshold)
            {
                return CustomerRank.Silver;
            }

            if (exp >= BronzeThreshold)
            {
                return CustomerRank.Bronze;
            }

            return CustomerRank.Potential;
        }

        public static (CustomerRank? NextRank, long? RequiredExp) GetNextRankInfo(long exp)
        {
            if (exp < BronzeThreshold)
            {
                return (CustomerRank.Bronze, BronzeThreshold);
            }

            if (exp < SilverThreshold)
            {
                return (CustomerRank.Silver, SilverThreshold);
            }

            if (exp < GoldThreshold)
            {
                return (CustomerRank.Gold, GoldThreshold);
            }

            if (exp < PlatinumThreshold)
            {
                return (CustomerRank.Platinum, PlatinumThreshold);
            }

            if (exp < DiamondThreshold)
            {
                return (CustomerRank.Diamond, DiamondThreshold);
            }

            if (exp < EmeraldThreshold)
            {
                return (CustomerRank.Emerald, EmeraldThreshold);
            }

            return (null, null);
        }
    }
}
