using LiveCharts;
using LiveCharts.Wpf;
using SmartHomeSystem.Backend.Framework;
using SmartHomeSystem.Backend.Function;
using System.Linq; // 引入 LINQ

namespace SmartHomeSystem.Frontend.ViewModels
{
    public class ChartData : IDisposable
    {
        private const int VisibleDataPoints = 24;
        private int _lastDataCount = 0;

        public ChartData()
        {
            // 在构造函数初始化
            SeriesCollection = new SeriesCollection
            {
                CreateLineSeries("电价历史"),
                CreateLineSeries("用电历史"),
                CreateLineSeries("电费历史")
            };

            Event.HighCostEvent += UpdateChartData;
        }

        public event Action OnSeriesCollectionChanged;

        public SeriesCollection SeriesCollection { get; private set; }
        
        // 表达式主体定义简化只读属性
        public Func<double, string> YFormatter => value => value.ToString("F2");

        public void Dispose() => Event.HighCostEvent -= UpdateChartData;

        private void UpdateChartData()
        {
            // 获取三个数据源，统一处理
            var dataSources = new[] 
            { 
                Topology.PriceHistory, 
                Topology.ConsumptionHistory, 
                Topology.MoneyHistory 
            };

            // 安全检查：取最小长度，防止不同步导致越界
            int currentBackendCount = dataSources.Min(x => x.Count);

            // 如果后端数据重置（例如重新加载），则重置前端计数
            if (currentBackendCount < _lastDataCount) _lastDataCount = 0;

            // 如果没有新数据，直接返回
            if (currentBackendCount == _lastDataCount) return;

            // 遍历所有曲线进行统一更新
            for (int i = 0; i < SeriesCollection.Count; i++)
            {
                var chartValues = SeriesCollection[i].Values;
                var sourceHistory = dataSources[i];

                // 1. 增量添加新数据 (保留两位小数)
                for (int j = _lastDataCount; j < currentBackendCount; j++)
                {
                    // Item2 是值，根据原有逻辑
                    chartValues.Add(Math.Round(sourceHistory[j].Item2, 2));
                }

                // 2. 移除旧数据，保持窗口大小
                while (chartValues.Count > VisibleDataPoints)
                {
                    chartValues.RemoveAt(0);
                }
            }

            _lastDataCount = currentBackendCount;
            OnSeriesCollectionChanged?.Invoke();
        }

        // 辅助方法：统一创建曲线样式
        private LineSeries CreateLineSeries(string title) => new LineSeries
        {
            Title = title,
            Values = new ChartValues<double>(),
            PointGeometry = null // 无数据点图形，提升性能
        };
    }
}