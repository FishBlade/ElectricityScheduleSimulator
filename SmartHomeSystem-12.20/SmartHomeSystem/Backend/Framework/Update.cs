using SmartHomeSystem.Backend.Function;
using System.Diagnostics;
using System.Text;
using System.Windows.Threading;

namespace SmartHomeSystem.Backend.Framework
{
    public static class LoopManager//循环管理器
    {
        public const int HighCostFPS = 10;
        public const int lowCostFPS = 60;

        private static readonly TimeSpan HighCostInterval =
            TimeSpan.FromTicks(TimeSpan.TicksPerSecond / HighCostFPS);

        private static readonly TimeSpan LowCostInterval =
                    TimeSpan.FromTicks(TimeSpan.TicksPerSecond / lowCostFPS);

        private static DispatcherTimer? HighCostTimer = null;
        private static DispatcherTimer? LowCostTimer = null;
        private static Stopwatch stopwatch = new Stopwatch();

        public static void StartLoop()
        {
            LowCostTimer = new DispatcherTimer();
            LowCostTimer.Interval = LowCostInterval;
            LowCostTimer.Tick += OnLowCostTimerElapsed;
            HighCostTimer = new DispatcherTimer();
            HighCostTimer.Interval = HighCostInterval;
            HighCostTimer.Tick += OnHighLowCostTimerElapsed;

            stopwatch.Start();
            LowCostTimer.Start();
            HighCostTimer.Start();
        }

        public static void StopLoop()
        {
            LowCostTimer?.Stop();
            HighCostTimer?.Stop();
        }

        private static void OnHighLowCostTimerElapsed(object sender, EventArgs e)
        {
            Event.HighCostUpdate(); // 前端渲染更新
        }

        private static void OnLowCostTimerElapsed(object sender, EventArgs e)
        {
            Event.LowCostUpdate();
        }
    }

    public static class UpdateManager//更新管理器
    {
        static UpdateManager()
        {
            Event.HourlyEvent += LogManager.HourlyLog;
            Event.DailyEvent += LogManager.DailyLog;
        }
    }

    public class LogManager//日志管理器
    {
        private const int maxLogNumber = 2000;
        private static Queue<string> logStorage = new Queue<string>();

        public static void BrokenLog()//
        {
            Log("设备", "损坏了");
        }

        public static void DailyLog()
        {
            Log("系统", "新的一天");
        }

        public static void HourlyLog()

        {
            Log("系统", "状态更新");
        }

        public static void Log(string label, string message)//一般日志
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Time.GetStringTime());
            sb.Append(' ');
            sb.Append(label);
            sb.Append(message);
            logStorage.Enqueue(sb.ToString());
            if (logStorage.Count > maxLogNumber)
                logStorage.Dequeue();
        }

        public static void NewDeviceLog()//
        {
            Log("拓补", "新增了设备");
        }

        public static void OverRideLog()//
        {
            Log("拓补", "检测到超载，挂起了");
        }

        public static void RecoverLog()//
        {
            Log("拓补", "尝试恢复了");
        }

        public static void RemoveDeviceLog()//
        {
            Log("拓补", "移除了设备");
        }
    }
}