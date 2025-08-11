using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace STK_ToolBox.Models
{
    public class TeachingParams
    {
        public int BasePitch { get; set; }
        public int HoistPitch { get; set; }
        public int ProfilePitch { get; set; }
        public int Gap { get; set; } // Hoist + Gap => Zaxis_Put 계산용

        public ObservableCollection<int> ProfileBays { get; set; }
        public ObservableCollection<int> OtherLevels { get; set; }

        public ObservableCollection<CellIdentifier> DeadCells { get; set; }

        public List<int> HoistPitches { get; set; }

        public TeachingParams()
        {
            ProfileBays = new ObservableCollection<int>();
            OtherLevels = new ObservableCollection<int>();
            DeadCells = new ObservableCollection<CellIdentifier>();
            HoistPitches = new List<int>();
        }
    }
}
