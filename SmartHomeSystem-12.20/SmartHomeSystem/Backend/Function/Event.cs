namespace SmartHomeSystem.Backend.Function
{
    public static class Event//事件模型--用来维护状态更新
    {
        public static event Action DailyEvent = delegate { };

        public static event Action HighCostEvent = delegate { };

        public static event Action HourlyEvent = delegate { };

        public static event Action LowCostEvent = delegate { };

        public static event Action SensorEvent = delegate { };

        // 创建事件
        public static void DailyUpdate()//每日更新
        {
            Event.DailyEvent?.Invoke();
        }

        public static void HighCostUpdate()//低频更新
        {
            Event.HighCostEvent?.Invoke();
        }

        public static void HourlyUpdate()//每小时更新
        {
            Event.HourlyEvent?.Invoke();
        }

        public static void LowCostUpdate()//高频更新
        {
            Event.LowCostEvent?.Invoke();
        }

        public static void PublishSensorData(string dataLabel, string Data)
        {
            Event.SensorEvent?.Invoke();
        }
    }
}