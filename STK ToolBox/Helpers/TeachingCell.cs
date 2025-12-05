using STK_ToolBox.Models;

namespace STK_ToolBox.Helpers
{
    /// <summary>
    /// TeachingSheet 에서 하나의 셀(Bank/Bay/Level)을 표현하는 데이터 모델.
    ///
    /// 포함 정보:
    /// - Bank / Bay / Level : 셀의 위치
    /// - Base               : 수평축(BaseAxis) 위치 값
    /// - Zaxis_Get          : Hoist 위치(픽업 중심)
    /// - Zaxis_Put          : Hoist 위치(배치 중심 = Get + gap)
    /// - Laxis              : Turn/Rotation 축 값
    /// - Saxis              : Fork/Stabbing 축 값
    /// - ShelfSize          : 셀 높이 계층(레벨 그룹)
    /// - IsDead             : DeadCell 여부 (생성 제외 여부 판단용)
    ///
    /// TeachingCalculator 에 의해 계산되며,
    /// ExcelExporter.TeachingExcelExport 에 의해 최종 Teaching Sheet 로 출력됨.
    /// </summary>
    public class TeachingCell
    {
        #region ───────── 기본 위치 정보 ─────────

        /// <summary>Bank 번호</summary>
        public int Bank { get; set; }

        /// <summary>Bay 번호</summary>
        public int Bay { get; set; }

        /// <summary>Level 번호</summary>
        public int Level { get; set; }

        #endregion

        #region ───────── Teaching 좌표 값 ─────────

        /// <summary>
        /// Base 축 거리 (수평 이동값)
        /// </summary>
        public int Base { get; set; }

        /// <summary>
        /// Hoist 축 위치 (픽업 위치)
        /// </summary>
        public int Zaxis_Get { get; set; }

        /// <summary>
        /// Hoist 축 위치 (배치 위치 = Zaxis_Get + gap)
        /// </summary>
        public int Zaxis_Put { get; set; }

        /// <summary>
        /// Turn 축(회전 축) 위치 값
        /// </summary>
        public int Laxis { get; set; }

        /// <summary>
        /// Fork 축(삽입 축) 위치 값
        /// </summary>
        public int Saxis { get; set; }

        #endregion

        #region ───────── Dead 셀 & 기타 정보 ─────────

        /// <summary>
        /// DeadCell 여부 → true일 경우 TeachingCalculator에서 제외 처리.
        /// </summary>
        public bool IsDead { get; set; }

        /// <summary>
        /// 층(Level) 그룹 분류. Excel 출력 시 ShelfSize 컬럼에 기록됨.
        /// OtherLevels / HoistPitches 인덱싱 기반.
        /// </summary>
        public int ShelfSize { get; set; }

        #endregion
    }
}
