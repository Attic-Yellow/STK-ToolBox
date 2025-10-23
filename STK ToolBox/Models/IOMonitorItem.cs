// Models/IOMonitorItem.cs
namespace STK_ToolBox.Models
{
    public class IOMonitorItem
    {
        public int Id { get; set; }
        public string IOName { get; set; }
        public string Address { get; set; }   // "X000A" / "Y0010"
        public string Unit { get; set; }
        public string DetailUnit { get; set; }
        public string Description { get; set; }
        public bool CurrentState { get; set; } // 1=true, 0=false
        public bool IsChecked { get; set; }

        // Y(출력)만 토글 허용
        public bool CanToggle => !string.IsNullOrWhiteSpace(Address) &&
                                 char.ToUpperInvariant(Address[0]) == 'Y';

        // B접점(표시만 민트) — 규칙은 필요시 더 좁혀도 됨
        public bool IsBContact
        {
            get
            {
                var s1 = DetailUnit?.ToUpperInvariant() ?? "";
                var s2 = Description?.ToUpperInvariant() ?? "";
                return s1.Contains("B") || s1.Contains("NC") || s2.Contains("B접점") || s2.Contains("NC");
            }
        }
    }
}
