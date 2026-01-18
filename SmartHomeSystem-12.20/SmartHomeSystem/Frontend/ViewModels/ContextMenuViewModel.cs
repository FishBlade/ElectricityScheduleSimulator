using SmartHomeSystem.Backend.Framework;
using SmartHomeSystem.Connection;
using System.ComponentModel;
using System.Windows.Input;
using static SmartHomeSystem.Backend.Framework.Topology;

namespace SmartHomeSystem.Frontend.ViewModels
{
    public class ContextMenuViewModel : INotifyPropertyChanged
    {
        private bool _isMenuVisible;
        private BaseNode _selectedNode;

        public ContextMenuViewModel()
        {
            // 命令留空，因为 xaml.cs 的 Click 事件负责执行逻辑
            DeleteNodeCommand = new RelayCommand(() => { }, () => CanDeleteNode);
            AddLineCommand = new RelayCommand(() => { }, () => CanAddLine);
            AddDeviceCommand = new RelayCommand(() => { }, () => CanAddDevice);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        // --- 供 XAML 绑定的命令 ---
        public ICommand AddDeviceCommand { get; }
        public ICommand AddLineCommand { get; }
        public ICommand DeleteNodeCommand { get; }

        // --- 状态判断 (控制按钮显示/隐藏) ---
        public bool CanAddDevice => SelectedNode is LineNode { type: not NodeType.Root };
        public bool CanAddLine => SelectedNode is LineNode { type: not NodeType.Root };

        public bool CanDeleteNode => SelectedNode switch
        {
            DeviceNode => true,
            LineNode { type: not NodeType.Root } => true,
            _ => false
        };

        // --- 视图属性 ---
        public bool IsMenuVisible
        {
            get => _isMenuVisible;
            set
            {
                if (_isMenuVisible != value)
                {
                    _isMenuVisible = value;
                    OnPropertyChanged(nameof(IsMenuVisible));
                    if (!value) SelectedNode = null;
                }
            }
        }

        public BaseNode SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (_selectedNode != value)
                {
                    _selectedNode = value;
                    OnPropertyChanged(nameof(SelectedNode));
                    RefreshCommandStates();
                }
            }
        }

        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private void RefreshCommandStates()
        {
            OnPropertyChanged(nameof(CanAddDevice));
            OnPropertyChanged(nameof(CanAddLine));
            OnPropertyChanged(nameof(CanDeleteNode));

            (DeleteNodeCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AddLineCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AddDeviceCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }
}