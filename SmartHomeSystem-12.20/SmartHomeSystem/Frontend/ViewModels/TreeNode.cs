using SmartHomeSystem.Backend.Framework;
using SmartHomeSystem.Connection;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using static SmartHomeSystem.Backend.Framework.Topology;

namespace SmartHomeSystem.Frontend.ViewModels
{
    public class TreeNode : INotifyPropertyChanged
    {
        // 实例字段
        private bool _isExpanded;
        private bool _isSelected;
        private string _name;
        private string _nodeTypeDisplay;
        private string _powerInfo;
        private string _statusDisplay;

        // 缓存后端节点的引用，用于快速比对
        public BaseNode BackendNode { get; }

        public TreeNode(BaseNode backendNode)
        {
            BackendNode = backendNode;
            Children = new ObservableCollection<TreeNode>();

            // 初始化显示数据
            UpdateDisplayProperties();

            // 初始加载子节点
            if (BackendNode is LineNode lineNode)
            {
                SyncChildrenStructure(lineNode);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        #region 绑定属性 (保持与 XAML 兼容)

        public ObservableCollection<TreeNode> Children { get; }

        public string Name
        {
            get => _name;
            private set => SetProperty(ref _name, value, nameof(Name));
        }

        public string NodeTypeDisplay
        {
            get => _nodeTypeDisplay;
            private set => SetProperty(ref _nodeTypeDisplay, value, nameof(NodeTypeDisplay));
        }

        public string PowerInfo
        {
            get => _powerInfo;
            private set => SetProperty(ref _powerInfo, value, nameof(PowerInfo));
        }

        public string StatusDisplay
        {
            get => _statusDisplay;
            private set => SetProperty(ref _statusDisplay, value, nameof(StatusDisplay));
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (SetProperty(ref _isExpanded, value, nameof(IsExpanded)))
                {
                    // 展开时，确保数据是最新的
                    if (value) RefreshRecursive();
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value, nameof(IsSelected)) && value)
                {
                    // 配合 XAML.cs 的逻辑，更新全局选中状态
                    Frontend.FrontContext.selectNode = BackendNode;
                    MainViewModel.Instance?.UpdateEditorValues();
                }
            }
        }

        #endregion 绑定属性

        #region 核心高性能同步逻辑

        /// <summary>
        /// 递归刷新：只更新文本，仅在必要时调整结构
        /// </summary>
        public void RefreshRecursive()
        {
            // 1. 更新当前节点的显示文本 (极快)
            UpdateDisplayProperties();

            // 2. 如果是线路节点，同步子节点结构 (Diff算法)
            if (BackendNode is LineNode lineNode)
            {
                // 如果节点未展开且没有子节点，可以跳过深层更新以提升性能（可选）
                // 这里为了保证数据实时性，选择总是同步，但因为是Diff操作，开销很小
                SyncChildrenStructure(lineNode);

                // 3. 递归更新子节点
                foreach (var child in Children)
                {
                    child.RefreshRecursive();
                }
            }
        }

        /// <summary>
        /// 仅更新文字属性，不涉及集合操作
        /// </summary>
        private void UpdateDisplayProperties()
        {
            // 使用局部变量减少属性访问开销
            string newName = BackendNode switch
            {
                LineNode { type: NodeType.Root } ln => $"根节点 (功率: {ln.PowerConsumption:F1}KW)",
                LineNode ln => $"线路 ({ln.name})", // 简化显示，避免频繁变动的数字触发重绘
                DeviceNode { currentDevice: { } d } => $"设备 ({d.name})",
                _ => "未知节点"
            };

            if (_name != newName) Name = newName;

            string newType = BackendNode.type.ToString();
            if (_nodeTypeDisplay != newType) NodeTypeDisplay = newType;

            string newStatus = BackendNode switch
            {
                LineNode { isOverLoad: true } => "过载",
                { nodeSwitch: true } => "运行中",
                _ => "已关闭"
            };
            if (_statusDisplay != newStatus) StatusDisplay = newStatus;

            string newPower = BackendNode switch
            {
                LineNode ln => $"消耗:{ln.PowerConsumption:F1}KW",
                DeviceNode { currentDevice: BaseConsumer c } => $"用电:{c.realpowerConsumption:F1}KW",
                DeviceNode { currentDevice: BaseProducer p } => $"发电:{p.realpowerProduction:F1}KW",
                _ => ""
            };
            if (_powerInfo != newPower) PowerInfo = newPower;
        }

        /// <summary>
        /// 结构同步 (Diff 算法)：保持现有对象，仅增删变化的节点
        /// </summary>
        private void SyncChildrenStructure(LineNode lineNode)
        {
            // 1. 获取后端当前的所有子节点 (设备 + 子线路)
            var backendChildren = new List<BaseNode>();
            backendChildren.AddRange(lineNode.devices);
            backendChildren.AddRange(lineNode.childrenNode);

            // 2. 移除：前端有，但后端没有的节点
            // 倒序遍历以便安全删除
            for (int i = Children.Count - 1; i >= 0; i--)
            {
                var childVM = Children[i];
                if (!backendChildren.Contains(childVM.BackendNode))
                {
                    Children.RemoveAt(i);
                }
            }

            // 3. 添加：后端有，但前端没有的节点
            // 这种情况通常发生在末尾添加，或者插入
            foreach (var backendChild in backendChildren)
            {
                // 简单的线性查找 (因为子节点数量通常 < 100，比维护字典更轻量)
                bool exists = false;
                foreach (var childVM in Children)
                {
                    if (childVM.BackendNode == backendChild)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    var newVM = new TreeNode(backendChild);
                    Children.Add(newVM);
                }
            }
        }

        #endregion 核心高性能同步逻辑

        #region 静态管理入口

        /// <summary>
        /// 全局刷新入口 (由 MainViewModel 调用)
        /// </summary>
        public static void SyncTreeWithBackend(ObservableCollection<TreeNode> treeNodes, LineNode rootNode)
        {
            if (rootNode == null) return;

            // 根节点处理：如果树为空或根节点变了，才重建根
            if (treeNodes.Count == 0 || treeNodes[0].BackendNode != rootNode)
            {
                treeNodes.Clear();
                var rootVM = new TreeNode(rootNode)
                {
                    IsExpanded = true,
                    IsSelected = true
                };
                treeNodes.Add(rootVM);
            }
            else
            {
                // 根节点存在，开始增量更新
                treeNodes[0].RefreshRecursive();
            }
        }

        /// <summary>
        /// 仅刷新数据 (兼容旧接口)
        /// </summary>
        public static void RefreshTreeDataOnly(ObservableCollection<TreeNode> nodes)
        {
            foreach (var node in nodes)
            {
                node.RefreshRecursive();
            }
        }

        public static void RefreshSelectedNodeAndAncestors(ObservableCollection<TreeNode> nodes, TreeNode selected)
        {
            // 在增量更新模式下，全量刷新也非常快，直接调用通用接口即可
            RefreshTreeDataOnly(nodes);
        }

        #endregion 静态管理入口

        #region 辅助方法

        private bool SetProperty<T>(ref T field, T value, string propertyName)
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                return true;
            }
            return false;
        }

        #endregion 辅助方法
    }
}