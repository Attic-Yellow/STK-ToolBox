using System;
using System.Threading;
using System.Threading.Tasks;

namespace STK_ToolBox.Helpers
{
    /// PLC(MX Component)를 통해 CC-Link 마스터의 RWr/RWw를 조작
    public sealed class InverterViaPlcController
    {
        private readonly IPlcClient _plc;

        // 디바이스 문자열(PLC의 CC-Link 마스터 버퍼메모리)
        public string RunCmdDevice { get; set; } = "RWr100"; // 운전명령 Word
        public string FreqCmdDevice { get; set; } = "RWr101"; // 속도명령 Word
        public string StatDevice { get; set; } = "RWw100"; // 상태 Word
        public string FreqMonDevice { get; set; } = "RWw101"; // 현재주파수 Word
        public int HzUnitDivisor { get; set; } = 100;         // 0.01Hz 단위

        public InverterViaPlcController(IPlcClient plc) { _plc = plc; }

        public Task SetRunAsync(bool forward, CancellationToken ct)
            => Task.Run(() =>
            {
                ushort cmd = (ushort)(forward ? 0x0001 : 0x0002); // 예시: FWD=1, REV=2
                if (!_plc.WriteWord(RunCmdDevice, cmd, out var err))
                    throw new InvalidOperationException(err);
            }, ct);

        public Task StopAsync(CancellationToken ct)
            => Task.Run(() =>
            {
                if (!_plc.WriteWord(RunCmdDevice, 0x0000, out var err))
                    throw new InvalidOperationException(err);
            }, ct);

        public Task ResetAsync(CancellationToken ct)
            => Task.Run(() =>
            {
                if (!_plc.WriteWord(RunCmdDevice, 0x0010, out var e1))
                    throw new InvalidOperationException(e1);
                Thread.Sleep(100);
                if (!_plc.WriteWord(RunCmdDevice, 0x0000, out var e2))
                    throw new InvalidOperationException(e2);
            }, ct);

        public Task SetFrequencyAsync(double hz, CancellationToken ct)
            => Task.Run(() =>
            {
                int raw = (int)Math.Round(hz * HzUnitDivisor, MidpointRounding.AwayFromZero);
                if (raw < 0) raw = 0;
                if (!_plc.WriteWord(FreqCmdDevice, (ushort)raw, out var err))
                    throw new InvalidOperationException(err);
            }, ct);

        public Task<(int statusWord, double freqHz)> ReadStatusAsync(CancellationToken ct)
            => Task.Run<(int, double)>(() =>
            {
                if (!_plc.ReadWord(StatDevice, out ushort st, out var e1))
                    throw new InvalidOperationException(e1);
                if (!_plc.ReadWord(FreqMonDevice, out ushort fr, out var e2))
                    throw new InvalidOperationException(e2);
                return (st, fr / (double)HzUnitDivisor);
            }, ct);
    }
}
