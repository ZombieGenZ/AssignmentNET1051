using System.ComponentModel.DataAnnotations;

namespace Assignment.Enums
{
    public enum CustomerRank
    {
        [Display(Name = "Khách hàng tiềm năng")]
        Potential = 0,
        [Display(Name = "Khách hàng cấp đồng")]
        Bronze = 1,
        [Display(Name = "Khách hàng cấp bạc")]
        Silver = 2,
        [Display(Name = "Khách hàng cấp vàng")]
        Gold = 3,
        [Display(Name = "Khách hàng bạch kim")]
        Platinum = 4,
        [Display(Name = "Khách hàng kim cương")]
        Diamond = 5,
        [Display(Name = "Khách hàng lục bảo")]
        Emerald = 6
    }
}
