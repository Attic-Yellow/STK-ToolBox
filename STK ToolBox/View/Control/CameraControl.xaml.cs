using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using STK_ToolBox.ViewModels;

namespace STK_ToolBox.View
{
    public partial class CameraControl : UserControl
    {
        public CameraControl()
        {
            InitializeComponent();
            this.DataContext = new CameraViewModel();
        }

        private void PwdBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            var vm = this.DataContext as CameraViewModel;
            if (vm != null) vm.Password = ((PasswordBox)sender).Password;
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            // 필요 시 향후 스트리밍 중지용 훅
        }
    }

}