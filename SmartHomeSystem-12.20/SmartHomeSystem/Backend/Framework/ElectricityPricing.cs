using SmartHomeSystem.Backend.Function;

namespace SmartHomeSystem.Backend.Framework
{
    public static class ElectricityPricing//生成电价信息
    {
        public const double default_price = 0.55;
        private const double OffPeakPriceMultiplier = 0.15;
        private const double peakPriceMultiplier = 0.2;  // 高峰电价乘数,每个峰值参数增加该乘数的电价

        // 低谷电价乘数，每个谷值参数降低该乘数的电价
        //KWH/每元 //默认电价
        private static double current_price = 0;

        static ElectricityPricing()
        {
            Event.HourlyEvent += ElectricityPricing.UpdatePrice;
        }

        public static double GetCurrentPrice()
        {
            return current_price;
        }

        public static void UpdatePrice()//经过计算，电价在85%-160%之间波动
        {
            double multiplier = 1.0;
            //周末波动
            if (Framework.Time.IsWeekend()) multiplier += peakPriceMultiplier;//周末电价上浮
            //季节波动
            if (Framework.Time.GetSeason() == "Summer" && Framework.Time.GetHour() >= 19 && Framework.Time.GetHour() <= 23) multiplier += peakPriceMultiplier;//夏季晚高峰
            if (Framework.Time.GetSeason() == "Winter" && Framework.Time.GetHour() >= 7 && Framework.Time.GetHour() <= 11) multiplier += peakPriceMultiplier;//冬季早高峰
            //时间波动
            if (Framework.Time.IsnightTime()) multiplier -= OffPeakPriceMultiplier;//夜间低谷电价
            else if (Framework.Time.GetHour() >= 13 && Framework.Time.GetHour() <= 16) multiplier -= OffPeakPriceMultiplier;//午间低谷电价
            else if (Framework.Time.GetHour() >= 21 && Framework.Time.GetHour() <= 23) multiplier += peakPriceMultiplier;//晚间照明高峰
            current_price = default_price * multiplier;
            current_price = Math.Round(default_price * multiplier, 2);
            Framework.Topology.PriceHistory.Add((Framework.Time.GetPassHour(), current_price));
        }
    }
}