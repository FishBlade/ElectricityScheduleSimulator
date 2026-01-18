using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace SmartHomeSystem.Frontend
{
    public class CriticalToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isCritical)
            {
                if (parameter?.ToString() == "Background")
                {
                    return isCritical ?
                        new SolidColorBrush(Color.FromArgb(30, 231, 76, 60)) : // 浅红色背景
                        new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)); // 白色背景
                }
                else // Text
                {
                    return isCritical ?
                        new SolidColorBrush(Color.FromRgb(192, 57, 43)) : // 深红色文字
                        new SolidColorBrush(Color.FromRgb(51, 51, 51));   // 黑色文字
                }
            }
            return new SolidColorBrush(Colors.Black);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CycleToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int cycle)
            {
                // 根据恢复周期设置不同的浅橙色背景
                return cycle switch
                {
                    0 => new SolidColorBrush(Color.FromRgb(255, 255, 255)),     // 纯白 - 当前周期
                    1 => new SolidColorBrush(Color.FromRgb(255, 250, 240)),     // 浅米色
                    2 => new SolidColorBrush(Color.FromRgb(255, 245, 230)),     // 浅杏色
                    3 => new SolidColorBrush(Color.FromRgb(255, 240, 220)),     // 浅橙色
                    4 => new SolidColorBrush(Color.FromRgb(255, 235, 210)),     // 浅橙黄色
                    5 => new SolidColorBrush(Color.FromRgb(255, 230, 200)),     // 浅橙红色
                    _ => new SolidColorBrush(Color.FromRgb(255, 255, 255))      // 默认白色
                };
            }
            return new SolidColorBrush(Colors.White);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CycleToHeaderColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int cycleIndex)
            {
                return cycleIndex switch
                {
                    0 => new SolidColorBrush(Color.FromRgb(74, 101, 114)),   // 深蓝灰
                    1 => new SolidColorBrush(Color.FromRgb(86, 98, 112)),     // 中蓝灰
                    2 => new SolidColorBrush(Color.FromRgb(98, 95, 110)),     // 紫灰
                    3 => new SolidColorBrush(Color.FromRgb(110, 92, 108)),   // 红紫灰
                    _ => new SolidColorBrush(Color.FromRgb(122, 89, 106))   // 默认
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CycleToTextColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int cycle)
            {
                // 周期数字颜色随背景色加深而变深
                return cycle switch
                {
                    0 => new SolidColorBrush(Color.FromRgb(153, 153, 153)),     // 浅灰色
                    1 => new SolidColorBrush(Color.FromRgb(170, 119, 51)),       // 浅棕色
                    2 => new SolidColorBrush(Color.FromRgb(187, 102, 34)),       // 中棕色
                    3 => new SolidColorBrush(Color.FromRgb(204, 85, 17)),       // 深棕色
                    4 => new SolidColorBrush(Color.FromRgb(221, 68, 0)),         // 橙红色
                    5 => new SolidColorBrush(Color.FromRgb(238, 51, 0)),         // 红色
                    _ => new SolidColorBrush(Color.FromRgb(153, 153, 153))      // 默认灰色
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class OverloadLevelToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double percentage)
            {
                if (percentage < 10) return new SolidColorBrush(Color.FromRgb(39, 174, 96));   // 绿色
                if (percentage < 20) return new SolidColorBrush(Color.FromRgb(241, 196, 15));  // 黄色
                if (percentage < 30) return new SolidColorBrush(Color.FromRgb(230, 126, 34)); // 橙色
                return new SolidColorBrush(Color.FromRgb(231, 76, 60)); // 红色
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class PriorityToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int priority)
            {
                return priority switch
                {
                    0 => new SolidColorBrush(Color.FromRgb(231, 76, 60)), // Critical - 红色
                    1 => new SolidColorBrush(Color.FromRgb(230, 126, 34)),  // Important - 橙色
                    2 => new SolidColorBrush(Color.FromRgb(241, 196, 15)), // Normal - 黄色
                    3 => new SolidColorBrush(Color.FromRgb(52, 152, 219)),  // Low - 蓝色
                    _ => new SolidColorBrush(Color.FromRgb(149, 165, 166)) // 默认灰色
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class RecoveryChanceToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string chance)
            {
                return chance switch
                {
                    "高" => new SolidColorBrush(Color.FromRgb(39, 174, 96)),   // 绿色
                    "中" => new SolidColorBrush(Color.FromRgb(241, 196, 15)),  // 黄色
                    "低" => new SolidColorBrush(Color.FromRgb(230, 126, 34)),  // 橙色
                    "极低" => new SolidColorBrush(Color.FromRgb(231, 76, 60)), // 红色
                    _ => new SolidColorBrush(Color.FromRgb(149, 165, 166))    // 灰色
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public static BoolToVisibilityConverter Instance { get; } = new BoolToVisibilityConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool boolValue && boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility visibility && visibility == Visibility.Visible;
        }
    }
}