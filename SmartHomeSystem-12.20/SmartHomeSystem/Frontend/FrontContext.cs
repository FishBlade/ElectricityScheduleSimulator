using SmartHomeSystem.Backend.Framework;
using SmartHomeSystem.Connection;
using System.Windows;
using static SmartHomeSystem.Backend.Framework.Topology;

namespace SmartHomeSystem.Frontend
{
    public static class FrontContext
    {
        public static Dictionary<NodeType, (string Name, string Color)> dic
            = new Dictionary<NodeType, (string, string)>{
                { NodeType.BaseConsumer, ("普通用电器", "#4ECDC4") },
                { NodeType.BaseProducer, ("普通发电器", "#45B7D1") },
                { NodeType.BaseBattery, ("普通电池", "#FFBE0B") },
                { NodeType.Line, ("线路节点", "#96CEB4") },
                { NodeType.Root, ("电网根节点", "#FF6B6B") },
                { NodeType.Unknown, ("未知节点", "#D3D3D3") }
            };

        public static BaseNode selectNode = null;  // 直接使用后端节点
        public static MainWindow CurrentWindow { get; set; }

        public static void ShowErrorMessage(string message)
        {
            MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public static void Refresh()
        {
            MainViewModel.Instance.RefreshEntireTree();
        }
    }
}