using STK_ToolBox.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace STK_ToolBox.View
{
    public partial class InverterControl : UserControl
    {
        public InverterControl()
        {
            InitializeComponent();
            Unloaded += (s, e) => (DataContext as InverterDriveCCLinkViewModel)?.Dispose();
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
