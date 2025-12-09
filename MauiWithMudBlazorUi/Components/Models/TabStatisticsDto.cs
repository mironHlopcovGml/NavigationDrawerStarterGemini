using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MauiAppWithMudBlazor.Components.Models
{
    public class TabStatisticsDto
    {
        // 1. Основные метрики
        public double AverageCheck { get; set; }
        public double MaxSum { get; set; }
        public double MinSum { get; set; }

        // 2. График по дням (последние 7-30 дней или весь период)
        public double[] DailyChartData { get; set; } = Array.Empty<double>();
        public string[] DailyChartLabels { get; set; } = Array.Empty<string>();

        // 3. Топ категорий и проценты
        public List<CategoryStat> TopCategories { get; set; } = new();

        // 4. Аномалии (Топ 3 самых крупных операции)
        public List<FinanceItem> Anomalies { get; set; } = new();
    }
}
