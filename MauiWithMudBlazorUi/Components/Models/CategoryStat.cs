using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MauiAppWithMudBlazor.Components.Models
{
    public class CategoryStat
    {
        public string Name { get; set; } = string.Empty;
        public double Amount { get; set; }
        public double Percentage { get; set; }
    }
}
