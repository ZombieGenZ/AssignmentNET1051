using Assignment.ViewModels;
using System.Collections.Generic;

namespace Assignment.Models
{
    public class HomeViewModel
    {
        public IEnumerable<Product> Products { get; set; } = new List<Product>();

        public IEnumerable<Combo> Combos { get; set; } = new List<Combo>();

        public IEnumerable<Category> Categories { get; set; } = new List<Category>();

        public HomeFilterViewModel Filter { get; set; } = new();
    }
}
