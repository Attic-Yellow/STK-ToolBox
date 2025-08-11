using System.ComponentModel;
using STK_ToolBox.Models;

namespace STK_ToolBox.ViewModels
{
    public class CellViewModel : INotifyPropertyChanged
    {
        public CellIdentifier CellId { get; }
        private bool _isDead;
        public bool IsDead
        {
            get => _isDead;
            set
            {
                if (_isDead != value)
                {
                    _isDead = value;
                    OnPropertyChanged(nameof(IsDead));
                }
            }
        }
        public string DisplayText => $"{CellId.Bank}/{CellId.Bay}/{CellId.Level}";

        public CellViewModel(CellIdentifier id, bool isDead)
        {
            CellId = id;
            _isDead = isDead;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string prop) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
