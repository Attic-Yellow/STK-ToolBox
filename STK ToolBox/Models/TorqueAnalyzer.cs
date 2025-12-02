using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace STK_ToolBox.Models
{
    public class TorqueResult
    {
        public double BaseMin, BaseMax, BaseAvg;
        public double HoistMin, HoistMax, HoistAvg;
        public double ForkMin, ForkMax, ForkAvg;
        public double TurnMin, TurnMax, TurnAvg;
        public string ExcelPath = string.Empty;
    }

    public static class TorqueAnalyzer
    {
        // 로그에서 "Base/Hoist/Fork/Turn Max Torque: n" 형태를 읽어서 축별 Max 샘플 수집
        private static readonly Regex LineRegex = new Regex(
            @"Base Max Torque:\s*(-?\d+),\s*Hoist Max Torque:\s*(-?\d+),\s*Fork Max Torque:\s*(-?\d+),\s*Turn Max Torque:\s*(-?\d+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static TorqueResult AnalyzeTorqueWithReturn(string logPath)
        {
            if (!File.Exists(logPath))
                throw new FileNotFoundException("로그 파일을 찾을 수 없습니다.", logPath);

            var baseList = new List<int>();
            var hoistList = new List<int>();
            var forkList = new List<int>();
            var turnList = new List<int>();

            foreach (string line in File.ReadLines(logPath))
            {
                Match m = LineRegex.Match(line);
                if (!m.Success) continue;

                baseList.Add(int.Parse(m.Groups[1].Value));
                hoistList.Add(int.Parse(m.Groups[2].Value));
                forkList.Add(int.Parse(m.Groups[3].Value));
                turnList.Add(int.Parse(m.Groups[4].Value));
            }

            if (baseList.Count == 0)
                throw new Exception("Torque 데이터가 없습니다. 로그 포맷을 확인하세요.");

            double baseMin, baseMax, baseAvg;
            double hoistMin, hoistMax, hoistAvg;
            double forkMin, forkMax, forkAvg;
            double turnMin, turnMax, turnAvg;

            CalcStat(baseList, out baseMin, out baseMax, out baseAvg);
            CalcStat(hoistList, out hoistMin, out hoistMax, out hoistAvg);
            CalcStat(forkList, out forkMin, out forkMax, out forkAvg);
            CalcStat(turnList, out turnMin, out turnMax, out turnAvg);

            // 결과는 항상 D:\TorqueResults 에 저장
            string outDir = @"D:\TorqueResults";
            Directory.CreateDirectory(outDir);

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
            string outPath = Path.Combine(outDir, string.Format("Torque Sheet_{0}.xlsx", stamp));

            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.AddWorksheet("Torque Data");

                ws.Cell(1, 1).Value =
                    string.Format("분석일시: {0:yyyy-MM-dd HH:mm:ss}", DateTime.Now);

                // 헤더
                ws.Cell(3, 2).Value = "Base";
                ws.Cell(3, 3).Value = "Hoist";
                ws.Cell(3, 4).Value = "Fork";
                ws.Cell(3, 5).Value = "Turn";

                ws.Cell(4, 1).Value = "Min";
                ws.Cell(5, 1).Value = "Max";
                ws.Cell(6, 1).Value = "Average";

                ws.Cell(4, 2).Value = baseMin; ws.Cell(5, 2).Value = baseMax; ws.Cell(6, 2).Value = baseAvg;
                ws.Cell(4, 3).Value = hoistMin; ws.Cell(5, 3).Value = hoistMax; ws.Cell(6, 3).Value = hoistAvg;
                ws.Cell(4, 4).Value = forkMin; ws.Cell(5, 4).Value = forkMax; ws.Cell(6, 4).Value = forkAvg;
                ws.Cell(4, 5).Value = turnMin; ws.Cell(5, 5).Value = turnMax; ws.Cell(6, 5).Value = turnAvg;

                // 평균 값 소수 1자리
                ws.Cell(6, 2).Style.NumberFormat.SetFormat("0.0");
                ws.Cell(6, 3).Style.NumberFormat.SetFormat("0.0");
                ws.Cell(6, 4).Style.NumberFormat.SetFormat("0.0");
                ws.Cell(6, 5).Style.NumberFormat.SetFormat("0.0");

                ws.Columns().AdjustToContents();

                workbook.SaveAs(outPath);
            }

            var result = new TorqueResult
            {
                BaseMin = baseMin,
                BaseMax = baseMax,
                BaseAvg = baseAvg,
                HoistMin = hoistMin,
                HoistMax = hoistMax,
                HoistAvg = hoistAvg,
                ForkMin = forkMin,
                ForkMax = forkMax,
                ForkAvg = forkAvg,
                TurnMin = turnMin,
                TurnMax = turnMax,
                TurnAvg = turnAvg,
                ExcelPath = outPath
            };

            return result;
        }

        private static void CalcStat(List<int> xs, out double min, out double max, out double avg)
        {
            if (xs == null || xs.Count == 0)
                throw new ArgumentException("데이터가 비어 있습니다.", "xs");

            min = xs.Min();
            max = xs.Max();
            avg = xs.Average();
        }
    }
}
