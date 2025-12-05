using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace STK_ToolBox.Models
{
    #region ──────────────── 데이터 모델 클래스 ────────────────

    /// <summary>
    /// 한 번의 UNLOAD ~ PICK UP 사이클에 대한 정보.
    /// - UNLOAD START 시각 ~ PICK UP TACT 라인이 찍힌 시각까지를 하나의 사이클로 본다.
    /// - UnloadTact, PickupTact 는 각각 로그의 "UNLOAD TACT", "PICK UP TACT" 라인에서 추출.
    /// - TotalSeconds = UnloadTact + PickupTact
    /// - RawLines 에는 해당 사이클에 포함된 원본 로그 라인들을 그대로 보관한다.
    /// </summary>
    public class TactCycle
    {
        public DateTime StartTime;     // 사이클 시작 시각 (UNLOAD START 타임스탬프)
        public DateTime EndTime;       // 사이클 종료 시각 (PICK UP TACT 타임스탬프)

        public double UnloadTact;      // UNLOAD TACT (sec)
        public double PickupTact;      // PICK UP TACT (sec)
        public double TotalSeconds;    // UnloadTact + PickupTact

        public List<string> RawLines = new List<string>();  // 이 사이클에 해당하는 로그 원본 라인들
    }

    /// <summary>
    /// TACT 분석 결과.
    /// - Cycles : 분석된 개별 사이클 목록 (필터링/정렬된 상태)
    /// - UsedCycleCount : 통계 계산에 실제 사용된 사이클 개수
    /// - MinTotal / MaxTotal / AvgTotal : TotalSeconds(UNLOAD+PICKUP)에 대한 최소/최대/평균
    /// - UsedLines : 통계에 사용된 사이클들의 로그 라인을 시간 순으로 모아둔 것 (파일 출력용)
    /// </summary>
    public class TactStatsResult
    {
        public List<TactCycle> Cycles = new List<TactCycle>();

        public int UsedCycleCount;
        public double MinTotal;
        public double MaxTotal;
        public double AvgTotal;

        // 파일 출력용 : 선택된 사이클들의 로그 라인들을 시간 순으로 저장
        public List<string> UsedLines = new List<string>();
    }

    #endregion

    #region ──────────────── TACT 분석기 ────────────────

    /// <summary>
    /// TACT 로그 분석 전담 클래스.
    /// 
    /// 개략 알고리즘:
    /// 1) 로그 파일 전체 라인을 읽는다.
    /// 2) "UNLOAD START" ~ "PICK UP TACT" 구간을 하나의 사이클로 묶어 TactCycle 리스트를 만든다.
    /// 3) 모든 사이클을 시작 시각 기준으로 정렬한다.
    /// 4) 뒤에서부터 최신 사이클부터 요청된 개수(cycleCount)만큼 잘라서 사용한다.
    /// 5) 선택된 사이클에 대해 TotalSeconds(UNLOAD+PICKUP) 기준으로 Min, Max, Avg 를 계산한다.
    /// 6) 선택된 사이클의 RawLines 를 UsedLines 리스트에 모아서 파일로 내보낼 수 있게 한다.
    /// </summary>
    public static class TactAnalyzer
    {
        #region ───────── Public API : AnalyzeTact ─────────

        /// <summary>
        /// TACT 로그 분석 메인 함수.
        /// 
        /// - 로그 파일 경로(logPath)를 받아서 UNLOAD ~ PICK UP 사이클을 모두 파싱한다.
        /// - 최신 사이클부터 cycleCount 개를 선택해서:
        ///   * 각 사이클의 TotalSeconds(UNLOAD TACT + PICK UP TACT)를 통계 대상으로 사용한다.
        ///   * Min / Max / Avg 를 계산한다.
        ///   * 통계에 사용된 사이클의 RawLines 를 UsedLines 에 모은다.
        /// 
        /// 현재 bank/bay/level 파라미터는 위치 필터용으로만 예약되어 있고, 실제 필터링은 하지 않는다.
        /// </summary>
        /// <param name="logPath">TACT 로그 파일 전체 경로</param>
        /// <param name="bank1">미사용 (향후 위치 필터용)</param>
        /// <param name="bay1">미사용 (향후 위치 필터용)</param>
        /// <param name="level1">미사용 (향후 위치 필터용)</param>
        /// <param name="bank2">미사용 (향후 위치 필터용)</param>
        /// <param name="bay2">미사용 (향후 위치 필터용)</param>
        /// <param name="level2">미사용 (향후 위치 필터용)</param>
        /// <param name="cycleCount">
        /// 통계에 사용할 최신 사이클 개수. 
        /// (실제 사이클 수가 이 값보다 작으면 가능한 만큼만 사용한다.)
        /// </param>
        /// <returns>TactStatsResult : 사이클 목록 + 통계 정보 + 로그 라인 모음</returns>
        public static TactStatsResult AnalyzeTact(
            string logPath,
            int bank1, int bay1, int level1,   // 현재는 위치 필터는 사용하지 않음
            int bank2, int bay2, int level2,
            int cycleCount)
        {
            // 1) 파일 체크 및 전체 라인 읽기
            if (string.IsNullOrEmpty(logPath) || !File.Exists(logPath))
                throw new FileNotFoundException("TACT 로그 파일을 찾을 수 없습니다.", logPath);

            var allLines = File.ReadAllLines(logPath);

            // 2) UNLOAD START ~ PICK UP TACT 사이클 파싱
            var allCycles = ParseCycles(allLines);

            if (allCycles.Count == 0)
                throw new Exception("로그에서 유효한 TACT 사이클을 찾을 수 없습니다.");

            // 3) 시작 시각 기준으로 정렬 후, 최신 사이클부터 cycleCount개를 사용
            allCycles.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));   // 오래된 순
            allCycles.Reverse();                                            // 최신이 앞에 오도록 반전

            var usedCycles = new List<TactCycle>();
            for (int i = 0; i < allCycles.Count && i < cycleCount; i++)
                usedCycles.Add(allCycles[i]);

            // 4) 화면/파일 출력 시 보기 좋게 다시 시간 오름차순 정렬
            usedCycles.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));

            // 5) 통계 계산 및 UsedLines 구성
            var result = new TactStatsResult();
            result.Cycles.AddRange(usedCycles);
            result.UsedCycleCount = usedCycles.Count;

            double min = double.MaxValue;
            double max = double.MinValue;
            double sum = 0.0;
            int n = usedCycles.Count;

            foreach (var c in usedCycles)
            {
                if (c.TotalSeconds < min) min = c.TotalSeconds;
                if (c.TotalSeconds > max) max = c.TotalSeconds;
                sum += c.TotalSeconds;

                // 이 사이클에 포함된 로그 라인들을 그대로 복사
                result.UsedLines.AddRange(c.RawLines);
                result.UsedLines.Add(""); // 사이클 간 구분용 빈 줄
            }

            result.MinTotal = min;
            result.MaxTotal = max;
            result.AvgTotal = (n > 0) ? (sum / n) : 0.0;

            return result;
        }

        #endregion

        #region ───────── Private Parsing Logic ─────────

        /// <summary>
        /// 로그 라인 배열에서 UNLOAD START ~ PICK UP TACT 구간을 찾아
        /// 개별 TactCycle 리스트로 변환한다.
        /// 
        /// 파싱 규칙:
        /// - "TactLog|UNLOAD START" 라인이 나오면 새로운 사이클 시작.
        ///   * 이 시점의 타임스탬프를 StartTime 으로 사용.
        /// - 사이클 진행 중에는 모든 라인을 RawLines 에 추가.
        /// - "TactLog|UNLOAD TACT" 라인에서 콜론 뒤의 실수값을 파싱하여 UnloadTact 에 기록.
        /// - "TactLog|PICK UP TACT" 라인에서 콜론 뒤의 실수값을 파싱하여 PickupTact 에 기록.
        ///   * 이 시점의 타임스탬프를 EndTime 으로 사용.
        ///   * TotalSeconds = UnloadTact + PickupTact 계산 후 사이클 확정.
        /// - 그 외 라인은 그대로 RawLines 에만 추가한다.
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
                    continue; // 빈 줄은 건너뜀

                // 1) 타임스탬프 파싱
                //    형식 예: "2025-11-27 16:43:01,907|INFO|TactLog|..."
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
                    ts = DateTime.MinValue;  // 형식이 다를 경우, 시간 정보는 사용하지 않음
                }

                // 2) UNLOAD START : 새 사이클 시작
                if (trimmed.Contains("TactLog|UNLOAD START"))
                {
                    current = new TactCycle();
                    current.StartTime = ts;
                    current.RawLines.Add(trimmed);
                    inCycle = true;
                    continue;
                }

                // 아직 사이클이 시작되지 않았으면 무시
                if (!inCycle || current == null)
                    continue;

                // 3) 사이클 진행 중인 라인들
                current.RawLines.Add(trimmed);

                // 3-1) UNLOAD TACT 파싱
                if (trimmed.Contains("TactLog|UNLOAD TACT"))
                {
                    double val;
                    if (TryParseTailDouble(trimmed, out val))
                        current.UnloadTact = val;
                }

                // 3-2) PICK UP TACT 파싱 + 사이클 종료
                if (trimmed.Contains("TactLog|PICK UP TACT"))
                {
                    double val;
                    if (TryParseTailDouble(trimmed, out val))
                        current.PickupTact = val;

                    current.EndTime = ts;
                    current.TotalSeconds = current.UnloadTact + current.PickupTact;

                    list.Add(current);

                    // 상태 초기화
                    current = null;
                    inCycle = false;
                    continue;
                }
            }

            return list;
        }

        #endregion

        #region ───────── Helper Methods ─────────

        /// <summary>
        /// ".... : 8.41" 처럼 콜론(:) 뒤에 실수값이 오는 라인에서
        /// 그 실수값을 파싱한다.
        /// 
        /// - 마지막 콜론 위치를 기준으로 그 뒤의 문자열을 잘라낸다.
        /// - "sec" 같은 단위 문자가 붙어 있을 가능성이 있으므로
        ///   's', 'e', 'c', ' ' 를 뒤에서 제거한다.
        /// - InvariantCulture 를 사용하여 소수점 파싱.
        /// </summary>
        private static bool TryParseTailDouble(string line, out double value)
        {
            value = 0.0;

            int idx = line.LastIndexOf(':');
            if (idx < 0 || idx == line.Length - 1)
                return false;

            string part = line.Substring(idx + 1).Trim();

            // "8.41 sec" 같은 형태를 허용하기 위해 뒤쪽의 단위 문자를 제거
            part = part.TrimEnd('s', 'e', 'c', ' ');

            return double.TryParse(
                part,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        }

        #endregion
    }

    #endregion
}
