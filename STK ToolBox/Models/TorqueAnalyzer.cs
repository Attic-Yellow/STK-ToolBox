using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        private static readonly Regex LineRegex = new Regex(
            @"Base Max Torque:\s*(-?\d+),\s*Hoist Max Torque:\s*(-?\d+),\s*Fork Max Torque:\s*(-?\d+),\s*Turn Max Torque:\s*(-?\d+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex TimestampRegex = new Regex(
            @"(\d{4}[-/]\d{2}[-/]\d{2}[ T]\d{2}:\d{2}:\d{2})",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // 기존 시그니처는 기본 경로로 래핑
        public static TorqueResult AnalyzeTorqueWithReturn(string logPath)
        {
            return AnalyzeTorqueWithReturn(logPath, null);
        }

        // 출력 폴더 지정 가능
        public static TorqueResult AnalyzeTorqueWithReturn(string logPath, string outputDirectory)
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

            // 출력 디렉터리 결정
            string rootDir;
            if (string.IsNullOrEmpty(outputDirectory))
            {
                string defaultRoot = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                rootDir = Path.Combine(defaultRoot, "TorqueResults");
            }
            else
            {
                rootDir = outputDirectory;
            }

            Directory.CreateDirectory(rootDir);

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
            string outPath = Path.Combine(rootDir, string.Format("Torque Sheet_{0}.xlsx", stamp));

            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.AddWorksheet("Torque Data");

                ws.Cell(1, 1).Value =
                    string.Format("분석일시: {0:yyyy-MM-dd HH:mm:ss}", DateTime.Now);

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

                ws.Cell(6, 2).Style.NumberFormat.SetFormat("0.0");
                ws.Cell(6, 3).Style.NumberFormat.SetFormat("0.0");
                ws.Cell(6, 4).Style.NumberFormat.SetFormat("0.0");
                ws.Cell(6, 5).Style.NumberFormat.SetFormat("0.0");

                int headerRow = 8;

                ws.Cell(headerRow, 1).Value = "Index";
                ws.Cell(headerRow, 2).Value = "Base Torque";
                ws.Cell(headerRow, 3).Value = "Hoist Torque";
                ws.Cell(headerRow, 4).Value = "Fork Torque";
                ws.Cell(headerRow, 5).Value = "Turn Torque";

                for (int i = 0; i < baseList.Count; i++)
                {
                    int r = headerRow + 1 + i;

                    ws.Cell(r, 1).Value = i + 1;
                    ws.Cell(r, 2).Value = baseList[i];
                    ws.Cell(r, 3).Value = hoistList[i];
                    ws.Cell(r, 4).Value = forkList[i];
                    ws.Cell(r, 5).Value = turnList[i];
                }

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

        //  기존 시그니처: 기본 경로 사용
        public static string MergeLogsWithSorting(IEnumerable<string> logPaths)
        {
            return MergeLogsWithSorting(logPaths, null);
        }

        //  출력 폴더 기반 병합
        // 기존 MergeLogsWithSorting(IEnumerable<string> logPaths, string outputDirectory)를 이걸로 교체
        public static string MergeLogsWithSorting(IEnumerable<string> logPaths, string outputDirectory)
        {
            if (logPaths == null)
                throw new ArgumentNullException("logPaths");

            var pathList = new List<string>(logPaths);
            if (pathList.Count == 0)
                throw new ArgumentException("로그 파일이 선택되지 않았습니다.", "logPaths");

            // 출력 루트 폴더 결정 
            string rootDir;
            if (string.IsNullOrEmpty(outputDirectory))
            {
                string defaultRoot = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                rootDir = Path.Combine(defaultRoot, "TorqueResults");
            }
            else
            {
                rootDir = outputDirectory;
            }

            // 병합 로그는 하위 폴더 MergedLogs 사용
            string mergedDir = Path.Combine(rootDir, "MergedLogs");
            Directory.CreateDirectory(mergedDir);

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            // 파일 이름을 CYCLE LOG 로 변경 (덮어쓰기 방지를 위해 타임스탬프 붙임)
            string mergedPath = Path.Combine(mergedDir,
                string.Format("CYCLE LOG_{0}.txt", stamp));

            var allLines = new List<LogLine>();

            for (int fileIndex = 0; fileIndex < pathList.Count; fileIndex++)
            {
                string path = pathList[fileIndex];
                if (!File.Exists(path))
                    continue;

                int lineIndex = 0;

                //  인코딩: Windows 기본(보통 CP949, ANSI)으로 읽기
                using (var reader = new StreamReader(path, Encoding.Default))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        DateTime ts;
                        if (!TryParseTimestamp(line, out ts))
                        {
                            // 타임스탬프 없으면 제일 뒤로
                            ts = DateTime.MaxValue;
                        }

                        allLines.Add(new LogLine
                        {
                            Timestamp = ts,
                            Line = line,
                            FileIndex = fileIndex,
                            LineIndex = lineIndex
                        });

                        lineIndex++;
                    }
                }
            }

            var ordered = allLines
                .OrderBy(x => x.Timestamp)
                .ThenBy(x => x.FileIndex)
                .ThenBy(x => x.LineIndex)
                .ToList();

            //  쓰기도 같은 인코딩으로
            using (var writer = new StreamWriter(mergedPath, false, Encoding.Default))
            {
                foreach (var item in ordered)
                {
                    writer.WriteLine(item.Line);
                }
            }

            return mergedPath;
        }


        private class LogLine
        {
            public DateTime Timestamp;
            public string Line;
            public int FileIndex;
            public int LineIndex;
        }

        private static bool TryParseTimestamp(string line, out DateTime timestamp)
        {
            Match m = TimestampRegex.Match(line);
            if (m.Success)
            {
                if (DateTime.TryParse(m.Groups[1].Value, out timestamp))
                {
                    return true;
                }
            }

            timestamp = DateTime.MinValue;
            return false;
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
