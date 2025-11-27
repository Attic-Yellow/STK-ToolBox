using System;

namespace STK_ToolBox.Models
{
    /// <summary>
    /// IO Byte 단위 구간 정보
    /// ex) Channel=81, Device=X, StartAddressHex="1A0", ByteSize=2
    /// </summary>
    public class IOByteSegment
    {
        public int Channel { get; set; }              // 81
        public string Device { get; set; }            // "X" 또는 "Y"
        public string StartAddressHex { get; set; }   // "0", "1A0" 등 (X/Y 없는 순수 HEX)
        public int ByteSize { get; set; }             // 1, 2, 4 ...
        public string Source { get; set; }            // "IOMonitoring" / "Inverter" 등
    }
}
