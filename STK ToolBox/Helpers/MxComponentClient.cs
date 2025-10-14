using System;
using System.Reflection;

namespace STK_ToolBox.Helpers
{
    /// MX Component ActUtlType를 Interop 없이 late-binding으로 호출
    public sealed class MxComponentClient : IPlcClient
    {
        private object _ax;              // ActUtlType COM 객체
        private Type _t;                 // 타입 캐시
        public bool IsOpen { get; private set; }
        public int LogicalStationNumber { get; private set; }

        public bool Open(int logicalStationNumber, out string error)
        {
            error = null;
            try
            {
                // COM ProgID 로드 (MX Component 설치 필요)
                _t = Type.GetTypeFromProgID("ActUtlType.ActUtlType");
                if (_t == null) { error = "MX Component 미설치(ActUtlType CLSID 없음)"; return false; }

                _ax = Activator.CreateInstance(_t);
                _t.InvokeMember("ActLogicalStationNumber", BindingFlags.SetProperty, null, _ax, new object[] { logicalStationNumber });

                var rc = (int)_t.InvokeMember("Open", BindingFlags.InvokeMethod, null, _ax, null);
                if (rc == 0) { LogicalStationNumber = logicalStationNumber; IsOpen = true; return true; }

                error = GetError(rc);
                return false;
            }
            catch (Exception ex) { error = "MXC Open 예외: " + ex.Message; return false; }
        }

        public void Close()
        {
            try { if (_ax != null && IsOpen) _t.InvokeMember("Close", BindingFlags.InvokeMethod, null, _ax, null); }
            catch { }
            IsOpen = false; _ax = null; _t = null;
        }

        public bool ReadWord(string device, out ushort value, out string error)
        {
            value = 0; error = null;
            try
            {
                object[] args = { device, 1, null }; // 1 point, out object
                var rc = (int)_t.InvokeMember("ReadDeviceBlock2", BindingFlags.InvokeMethod, null, _ax, args);
                if (rc == 0)
                {
                    var arr = (Array)args[2];
                    value = Convert.ToUInt16(arr.GetValue(0));
                    return true;
                }
                error = GetError(rc); return false;
            }
            catch (Exception ex) { error = "READ FAIL: " + ex.Message; return false; }
        }

        public bool WriteWord(string device, ushort value, out string error)
        {
            error = null;
            try
            {
                Array data = Array.CreateInstance(typeof(int), 1);
                data.SetValue((int)value, 0);
                var rc = (int)_t.InvokeMember("WriteDeviceBlock2", BindingFlags.InvokeMethod, null, _ax, new object[] { device, 1, data });
                if (rc == 0) return true;
                error = GetError(rc); return false;
            }
            catch (Exception ex) { error = "WRITE FAIL: " + ex.Message; return false; }
        }

        public bool ReadWords(string device, int points, ushort[] buffer, out string error)
        {
            error = null;
            try
            {
                object[] args = { device, points, null };
                var rc = (int)_t.InvokeMember("ReadDeviceBlock2", BindingFlags.InvokeMethod, null, _ax, args);
                if (rc != 0) { error = GetError(rc); return false; }

                var arr = (Array)args[2];
                for (int i = 0; i < points && i < buffer.Length; i++)
                    buffer[i] = Convert.ToUInt16(arr.GetValue(i));
                return true;
            }
            catch (Exception ex) { error = "READ FAIL: " + ex.Message; return false; }
        }

        public bool WriteWords(string device, int points, ushort[] buffer, out string error)
        {
            error = null;
            try
            {
                Array data = Array.CreateInstance(typeof(int), points);
                for (int i = 0; i < points; i++) data.SetValue((int)buffer[i], i);
                var rc = (int)_t.InvokeMember("WriteDeviceBlock2", BindingFlags.InvokeMethod, null, _ax, new object[] { device, points, data });
                if (rc == 0) return true;
                error = GetError(rc); return false;
            }
            catch (Exception ex) { error = "WRITE FAIL: " + ex.Message; return false; }
        }

        public void Dispose() => Close();

        // MX Component 에러코드 → 간단 문자열
        private static string GetError(int rc)
        {
            // 0x5B8 ~ 0x5BF 등 상세 맵핑 필요하면 확장 가능
            return rc == 0 ? "OK" :
                   rc == -1 ? "TIMEOUT" :
                   $"MXC RC={rc}";
        }
    }
}
