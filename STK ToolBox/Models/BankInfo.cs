using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace STK_ToolBox.Models
{
    public class BankInfo : INotifyPropertyChanged
    {
        public int BankNumber { get; set; }
        public int BaseBay { get; set; }
        public int BaseLevel { get; set; }
        public int BaseValue { get; set; }
        public int HoistValue { get; set; }
        public int TurnValue { get; set; }
        public int ForkValue { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
