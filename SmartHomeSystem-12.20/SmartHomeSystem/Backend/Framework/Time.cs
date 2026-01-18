using SmartHomeSystem.Backend.Function;

namespace SmartHomeSystem.Backend.Framework

{
    public static class Time//时间模型--用来实现时间流逝
    {
        public static DateTime virtualTime;
        public static DateTime startTime;
        private static DateTime LastRealTime;
        private static DateTime LastUpdateTime;

        static Time()
        {
            Event.LowCostEvent += UpdateTime;
            startTime = DateTime.Now;
            LastRealTime = DateTime.Now;
            LastUpdateTime = startTime;
            virtualTime = startTime;
        }

        #region 暂停逻辑

        private static bool isPaused = true;

        public static void Pause()
        {
            isPaused = true;
            LastRealTime = DateTime.Now;
        }

        public static void UnPause()
        {
            isPaused = false;
            LastRealTime = DateTime.Now;
        }

        #endregion 暂停逻辑

        #region 时间缩放

        // 允许的时间加比例
        private static Dictionary<string, double> allowedScales = new Dictionary<string, double>
        {
            {"实时",1},
            {"低速",60},
            {"中速",3600},
            {"快速",18000},
            {"非法",0}
        };

        private static string timeScaleLabel = "实时";

        public static void SetTimeScale(string scale)//设置时间缩放倍率
        {
            if (!CheckTimeScale()) timeScaleLabel = "非法";
            else timeScaleLabel = scale;
        }

        private static bool CheckTimeScale()//检查时间缩放倍率是否合法
        {
            return allowedScales.ContainsKey(timeScaleLabel);
        }

        #region 时间更新

        public static void UpdateTime()//更新
        {
            if (!isPaused && CheckTimeScale())
            {
                DateTime now = DateTime.Now;
                TimeSpan realElapsed = now - LastRealTime;
                double scale = allowedScales[timeScaleLabel];
                virtualTime += TimeSpan.FromSeconds(realElapsed.TotalSeconds * scale);
                LastRealTime = now;
                UpdateCheck();
            }
        }

        #endregion 时间更新

        #endregion 时间缩放

        #region 辅助函数

        public static int GetHour()
        {
            return virtualTime.Hour;
        }

        public static int GetPassHour()
        {
            TimeSpan timePassed = virtualTime - startTime;
            return (int)timePassed.TotalHours;  // 取整小时数
        }

        public static string GetSeason()
        {
            int month = virtualTime.Month;
            if (month >= 3 && month <= 5)
                return "Spring";
            else if (month >= 6 && month <= 8)
                return "Summer";
            else if (month >= 9 && month <= 11)
                return "Autumn";
            else
                return "Winter";
        }

        public static string GetStringTime()
        {
            return virtualTime.ToString("yyyy-MM-dd HH:mm:ss");
        }

        public static DateTime GetTime()
        {
            return virtualTime;
        }

        public static bool IsnightTime()
        {
            int hour = virtualTime.Hour;
            return (hour >= 24 || hour < 6);
        }

        public static bool IsWeekend()
        {
            DayOfWeek day = virtualTime.DayOfWeek;
            return (day == DayOfWeek.Saturday || day == DayOfWeek.Sunday);
        }

        public static void UpdateCheck()
        {
            DateTime now = virtualTime;
            DateTime last = LastUpdateTime;

            // 使用更精确的时间段比较
            if (now.Year != last.Year || now.Month != last.Month || now.Day != last.Day || now.Hour != last.Hour)
            {
                TimeSpan timePassed = now - last;

                // 按小时触发
                int hoursPassed = (int)timePassed.TotalHours;
                for (int i = 0; i < hoursPassed; i++)
                {
                    Event.HourlyUpdate();
                }

                // 按天触发
                if (now.Date != last.Date)
                {
                    Event.DailyUpdate();
                }

                LastUpdateTime = now;
            }
        }

        #endregion 辅助函数
    }
}