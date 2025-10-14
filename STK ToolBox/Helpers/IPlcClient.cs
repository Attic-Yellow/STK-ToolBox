using System;

namespace STK_ToolBox.Helpers
{
    /// PLC(MX Component) 접근용 최소 인터페이스
    public interface IPlcClient : IDisposable
    {
        bool IsOpen { get; }
        bool Open(int logicalStationNumber, out string error);
        void Close();

        // MX Component 글자형 디바이스 사용: "RWr100", "RWw100" 등
        bool ReadWord(string device, out ushort value, out string error);
        bool WriteWord(string device, ushort value, out string error);
        bool ReadWords(string device, int points, ushort[] buffer, out string error);
        bool WriteWords(string device, int points, ushort[] buffer, out string error);
    }
}
