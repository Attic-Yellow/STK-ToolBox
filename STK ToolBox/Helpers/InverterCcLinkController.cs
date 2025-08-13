using System;
using System.Threading;
using System.Threading.Tasks;

namespace STK_ToolBox.Helpers
{
    public sealed class InverterCcLinkController
    {
        private readonly ICCLinkClient _cli;
        public short StationNo { get; set; } = 1;

        public short RunCmdAddr { get; set; } = 100; // RWr
        public short FreqCmdAddr { get; set; } = 101; // RWr
        public short StatAddr { get; set; } = 100; // RWw
        public short FreqMonAddr { get; set; } = 101; // RWw

        public int HzUnitDivisor { get; set; } = 100; // 0.01Hz

        public InverterCcLinkController(ICCLinkClient cli) { _cli = cli; }

        public Task SetRunAsync(bool forward, CancellationToken ct)
            => Task.Run(() =>
            {
                ushort cmd = (ushort)(forward ? 0x0001 : 0x0002); // 설비 맵에 맞게 조정
                var rc = _cli.WriteWord(StationNo, RunCmdAddr, cmd, out var err);
                if (rc != 0) throw new InvalidOperationException(err);
            }, ct);

        public Task StopAsync(CancellationToken ct)
            => Task.Run(() =>
            {
                var rc = _cli.WriteWord(StationNo, RunCmdAddr, 0x0000, out var err);
                if (rc != 0) throw new InvalidOperationException(err);
            }, ct);

        public Task ResetAsync(CancellationToken ct)
            => Task.Run(() =>
            {
                var rc1 = _cli.WriteWord(StationNo, RunCmdAddr, 0x0010, out var e1); // 펄스 예시
                if (rc1 != 0) throw new InvalidOperationException(e1);
                Thread.Sleep(100);
                var rc2 = _cli.WriteWord(StationNo, RunCmdAddr, 0x0000, out var e2);
                if (rc2 != 0) throw new InvalidOperationException(e2);
            }, ct);

        public Task SetFrequencyAsync(double hz, CancellationToken ct)
            => Task.Run(() =>
            {
                int raw = (int)Math.Round(hz * HzUnitDivisor, MidpointRounding.AwayFromZero);
                if (raw < 0) raw = 0;
                var rc = _cli.WriteWord(StationNo, FreqCmdAddr, (ushort)raw, out var err);
                if (rc != 0) throw new InvalidOperationException(err);
            }, ct);

        public Task<(int statusWord, double freqHz)> ReadStatusAsync(CancellationToken ct)
            => Task.Run<(int, double)>(() =>
            {
                var rc1 = _cli.ReadWord(StationNo, StatAddr, out ushort st, out var e1);
                if (rc1 != 0) throw new InvalidOperationException(e1);
                var rc2 = _cli.ReadWord(StationNo, FreqMonAddr, out ushort fr, out var e2);
                if (rc2 != 0) throw new InvalidOperationException(e2);
                return (st, fr / (double)HzUnitDivisor);
            }, ct);
    }
}
