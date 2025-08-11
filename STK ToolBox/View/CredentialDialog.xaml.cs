using System.Windows;

namespace STK_ToolBox.View
{
    public partial class CredentialDialog : Window
    {
        public string TargetIp { get; private set; }
        public string UserName { get; private set; }
        public string Password { get; private set; }

        public CredentialDialog(string ip, string currentUser)
        {
            InitializeComponent();
            TargetIp = ip;
            IpText.Text = ip;
            if (!string.IsNullOrWhiteSpace(currentUser) && currentUser != "미설정")
                TbUser.Text = currentUser;

            Loaded += (s, e) => {
                if (string.IsNullOrEmpty(TbUser.Text)) TbUser.Focus();
                else TbPassword.Focus();
            };
        }


        private void Save_Click(object sender, RoutedEventArgs e)
        {
            UserName = TbUser.Text ?? "";
            Password = TbPassword.Password ?? "";
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }


    }
}
