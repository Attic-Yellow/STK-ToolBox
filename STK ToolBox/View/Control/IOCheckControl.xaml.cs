using STK_ToolBox.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace STK_ToolBox.View
{
    /// <summary>
    /// IOCheckControl.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class IOCheckControl : UserControl
    {
        public IOCheckControl()
        {
            InitializeComponent();
            this.DataContext = new IOCheckViewModel();

            this.Loaded += (s, e) =>
            {
                if (DataContext is IOCheckViewModel vm)
                    vm.OnViewLoaded();
            };
        }
    }
}
