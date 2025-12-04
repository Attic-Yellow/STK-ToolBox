using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace STK_ToolBox.Models
{
    public class TactCycle
    {
        public DateTime StartTime;
        public DateTime EndTime;
        public double UnloadTact;   // UNLOAD TACT
        public double PickupTact;   // PICK UP TACT
        public double TotalSeconds; // Unload + Pickup
        public List<string> RawLines = new List<string>();
    }

    public class TactStatsResult
    {
        public List<TactCycle> Cycles = new List<TactCycle>();

        public int UsedCycleCount;
        public double MinTotal;
        public double MaxTotal;
        public double AvgTotal;

        // 파일 출력용
        public List<string> UsedLines = new List<string>();  // 선택된 사이클의 로그 라인들 (시간 순)
    }

    public static class TactAnalyzer
    {
        /// <summary>
        /// TACT 로그 분석:
        /// - UNLOAD START ~ PICK UP END 를 한 사이클로 보고
        /// - 각 사이클의 UNLOAD TACT + PICK UP TACT 를 Total Tact 로 사용
        /// - 최신 사이클부터 cycleCount 개를 써서 Min / Max / Avg 계산
        /// </summary>
        public static TactStatsResult AnalyzeTact(
            string logPath,
            int bank1, int bay1, int level1,   // 현재는 위치 필터는 사용하지 않음
            int bank2, int bay2, int level2,
            int cycleCount)
        {
            if (string.IsNullOrEmpty(logPath) || !File.Exists(logPath))
                throw new FileNotFoundException("TACT 로그 파일을 찾을 수 없습니다.", logPath);

            var allLines = File.ReadAllLines(logPath);
            var allCycles = ParseCycles(allLines);

            if (allCycles.Count == 0)
                throw new Exception("로그에서 유효한 TACT 사이클을 찾을 수 없습니다.");

            // 최신 사이클부터 cycleCount 개 사용
            allCycles.Sort((a, b) => a.StartTime.CompareTo(b.StartTime)); // 시간순 정렬
            allCycles.Reverse();                                          // 최신이 앞에 오도록

            var usedCycles = new List<TactCycle>();
            for (int i = 0; i < allCycles.Count && i < cycleCount; i++)
                usedCycles.Add(allCycles[i]);

            usedCycles.Sort((a, b) => a.StartTime.CompareTo(b.StartTime)); // 다시 시간 오름차순으로

            var result = new TactStatsResult();
            result.Cycles.AddRange(usedCycles);
            result.UsedCycleCount = usedCycles.Count;

            // 통계 계산
            double min = double.MaxValue;
            double max = double.MinValue;
            double sum = 0.0;
            int n = usedCycles.Count;

            foreach (var c in usedCycles)
            {
                if (c.TotalSeconds < min) min = c.TotalSeconds;
                if (c.TotalSeconds > max) max = c.TotalSeconds;
                sum += c.TotalSeconds;

                // 로그 라인 수집
                result.UsedLines.AddRange(c.RawLines);
                result.UsedLines.Add(""); // 사이클 사이에 빈 줄 하나
            }

            result.MinTotal = min;
            result.MaxTotal = max;
            result.AvgTotal = (n > 0) ? (sum / n) : 0.0;

            return result;
        }

        /// <summary>
        /// UNLOAD START ~ PICK UP END 를 한 사이클로 파싱
        /// </summary>
        private static List<TactCycle> ParseCycles(string[] lines)
        {
            var list = new List<TactCycle>();

            TactCycle current = null;
            bool inCycle = false;

            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0)
                    continue;

                // 타임스탬프 파싱 (맨 앞 "2025-11-27 16:43:01,907|" 형태)
                DateTime ts;
                int firstPipe = trimmed.IndexOf('|');
                if (firstPipe > 0)
                {
                    string tsPart = trimmed.Substring(0, firstPipe);
                    DateTime.TryParseExact(
                        tsPart,
                        "yyyy-MM-dd HH:mm:ss,fff",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out ts);
                }
                else
                {
                    ts = DateTime.MinValue;
                }

                if (trimmed.Contains("TactLog|UNLOAD START"))
                {
                    // 새 사이클 시작
                    current = new TactCycle();
                    current.StartTime = ts;
                    current.RawLines.Add(trimmed);
                    inCycle = true;
                    continue;
                }

                if (!inCycle || current == null)
                    continue;

                // 사이클 안에 있는 라인들
                current.RawLines.Add(trimmed);

                // UNLOAD TACT
                if (trimmed.Contains("TactLog|UNLOAD TACT"))
                {
                    double val;
                    if (TryParseTailDouble(trimmed, out val))
                        current.UnloadTact = val;
                }

                // PICK UP TACT & 사이클 종료
                if (trimmed.Contains("TactLog|PICK UP TACT"))
                {
                    double val;
                    if (TryParseTailDouble(trimmed, out val))
                        current.PickupTact = val;

                    current.EndTime = ts;
                    current.TotalSeconds = current.UnloadTact + current.PickupTact;

                    list.Add(current);
                    current = null;
                    inCycle = false;
                    continue;
                }
            }

            return list;
        }

        /// <summary>
        /// " ... : 8.41" 같은 라인에서 콜론 뒤의 실수값 파싱
        /// </summary>
        private static bool TryParseTailDouble(string line, out double value)
        {
            value = 0.0;
            int idx = line.LastIndexOf(':');
            if (idx < 0 || idx == line.Length - 1)
                return false;

            string part = line.Substring(idx + 1).Trim();
            part = part.TrimEnd('s', 'e', 'c', ' '); // "sec" 같은게 붙어있어도 대충 제거

            return double.TryParse(
                part,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        }
    }
}
