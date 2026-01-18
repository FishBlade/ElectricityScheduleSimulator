using SmartHomeSystem.Connection;
using SmartHomeSystem.Frontend.Forms;
using SmartHomeSystem.Frontend.Function;
using SmartHomeSystem.Frontend.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static SmartHomeSystem.Backend.Framework.Topology;

namespace SmartHomeSystem.Frontend
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            SubscribeToEvents();
            FrontContext.CurrentWindow = this;
            Connection.MainViewModel visual = new MainViewModel();
            this.DataContext = visual;
            Backend.Framework.LoopManager.StartLoop();
            if (this.DataContext is MainViewModel viewModel)
                viewModel.UpdateQueuesDisplay();
            CreateTestTopology();
        }

        public void ChartDataMouseDown(object sender, MouseButtonEventArgs e)
        {
            Function.Button.ChartDataMouseDown(sender, e);
        }

        public void EmptyButtonClick(object sender, RoutedEventArgs e)
        {
            Function.Button.EmptyButtonClick(sender, e);
        }

        public void ExitButtonClick(object sender, RoutedEventArgs e)
        {
            Function.Button.ExitButtonClick(sender, e);
        }

        public void LoadButtonClick(object sender, RoutedEventArgs e)
        {
            Function.Button.LoadButtonClick(sender, e);
        }

        public void MainWindow_Closed(object sender, EventArgs e)
        {
            UnsubscribeEvents();
            Backend.Framework.LoopManager.StopLoop();
        }

        public void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            Function.Button.RadioButton_Checked(sender, e);
        }

        public void SaveButtonClick(object sender, RoutedEventArgs e)
        {
            Function.Button.SaveButtonClick(sender, e);
        }

        public void UnsubscribeEvents()
        {
            this.Closed -= MainWindow_Closed;

            ExitButton.Click -= Function.Button.ExitButtonClick;
            ChartData.MouseDown -= Function.Button.ChartDataMouseDown;
            pause.Checked -= Function.Button.RadioButton_Checked;
            speed1.Checked -= Function.Button.RadioButton_Checked;
            speed2.Checked -= Function.Button.RadioButton_Checked;
            speed3.Checked -= Function.Button.RadioButton_Checked;
            speed4.Checked -= Function.Button.RadioButton_Checked;
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            do
            {
                if (current is T ancestor)
                {
                    return ancestor;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            while (current != null);
            return null;
        }

        // 在 MainWindow.xaml.cs 中简化事件处理
        private void TreeViewItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var treeViewItem = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);
            if (treeViewItem?.DataContext is TreeNode treeNode)
            {
                // 通过 MainViewModel 处理右键点击
                MainViewModel.Instance?.OnTreeNodeRightClicked(treeNode);

                // 设置选中状态
                treeViewItem.IsSelected = true;
                e.Handled = true;
            }
        }

        private void CreateTestTopology()
        {
            // 创建复杂测试拓扑
            ComplexTopologyTest.CreateComplexTestTopology();

            // 刷新树视图显示
            ComplexTopologyTest.RefreshTreeView();
        }

        private void SubscribeToEvents()
        {
            // 按钮事件
            ExitButton.Click += Function.Button.ExitButtonClick;
            ChartData.MouseDown += Function.Button.ChartDataMouseDown;

            // 速度控制按钮事件
            pause.Checked += Function.Button.RadioButton_Checked;
            speed1.Checked += Function.Button.RadioButton_Checked;
            speed2.Checked += Function.Button.RadioButton_Checked;
            speed3.Checked += Function.Button.RadioButton_Checked;
            speed4.Checked += Function.Button.RadioButton_Checked;
        }

        private void TopologyTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeNode selectedNode)

                selectedNode.IsSelected = true;
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            Backend.Framework.Time.Pause();
            this.pause.IsChecked = true;
            FrontContext.selectNode.Delete();
            MainViewModel._instance.RefreshEntireTree();
        }

        private void AddLine_Click(object sender, RoutedEventArgs e)
        {
            Pause();
            CreateLine createLineWindow = new CreateLine();
            createLineWindow.Owner = this;
            createLineWindow.ShowDialog();
        }

        private void AddDevice_Click(object sender, RoutedEventArgs e)
        {
            Pause();
            CreateDevice createDeviceWindow = new CreateDevice();
            createDeviceWindow.Owner = this;
            createDeviceWindow.ShowDialog();
        }

        private void Pause()
        {
            Backend.Framework.Time.Pause();
            this.pause.IsChecked = true;
        }
    }
}