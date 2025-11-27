using STK_ToolBox.Models;
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
    /// IOByteSheetControl.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class IOByteSheetControl : UserControl
    {
        public IOByteSheetControl()
        {
            InitializeComponent();
            this.DataContext = new IOByteSheetViewModel();
        }

        private void SegmentsXView_Filter(object sender, FilterEventArgs e)
        {
            var seg = e.Item as IOByteSegment;
            if (seg == null)
            {
                e.Accepted = false;
                return;
            }

            e.Accepted = string.Equals(seg.Device, "X",
                StringComparison.OrdinalIgnoreCase);
        }

        private void SegmentsYView_Filter(object sender, FilterEventArgs e)
        {
            var seg = e.Item as IOByteSegment;
            if (seg == null)
            {
                e.Accepted = false;
                return;
            }

            e.Accepted = string.Equals(seg.Device, "Y",
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
