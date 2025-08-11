using System.Windows;
using System.Windows.Controls;
using STK_ToolBox.ViewModels;

namespace STK_ToolBox.View
{
    public partial class TimeSyncControl : UserControl
    {
        private TimeSyncViewModel _vm;

        public TimeSyncControl()
        {
            InitializeComponent();
            _vm = new TimeSyncViewModel();
            this.DataContext = _vm;
        }

        private void SyncWithPassword_Click(object sender, RoutedEventArgs e)
        {
            _vm.AdminPassword = PwdBox.Password; // 전역 기본 비밀번호
            _vm.SyncTimeSelected();
        }

        private void SetCredential_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var pc = btn?.Tag as PcInfo;
            if (pc == null) return;

            var dlg = new CredentialDialog(pc.Ip, pc.CredentialUserDisplay);
            dlg.Owner = Window.GetWindow(this);
            if (dlg.ShowDialog() == true)
            {
                // 저장
                _vm.SetCredentialForIp(pc.Ip, dlg.UserName, dlg.Password);
                // UI 반영
                _vm.RefreshCredentialIndicators(pc.Ip);
            }
        }
    }
}
