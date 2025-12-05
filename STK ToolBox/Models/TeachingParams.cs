using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace STK_ToolBox.Models
{
    /// <summary>
    /// Teaching 계산에 필요한 모든 파라미터 묶음.
    /// 
    /// 구성 요소:
    /// - Pitch 관련 값(Base, Hoist, Profile)
    /// - Gap (ZAxis_Put 계산식에서 Hoist + Gap 사용)
    /// - Profile 전용 Bay 리스트
    /// - Profile 외 Other Level 리스트
    /// - Dead Cell(사용 불가 위치) 목록
    /// - 복수 HoistPitch 값 목록
    /// 
    /// TeachingSheetGenerator 및 Teaching 계산 로직 전체에서 공통 파라미터 컨테이너 역할을 한다.
    /// </summary>
    public class TeachingParams
    {
        #region ───────── Pitch Values ─────────

        /// <summary>
        /// Base Pitch 이동 간격.
        /// </summary>
        public int BasePitch { get; set; }

        /// <summary>
        /// Hoist Pitch 이동 간격.
        /// (HoistPitch 리스트가 있을 경우 단일값은 Legacy 또는 기본 pitch 용도로 사용)
        /// </summary>
        public int HoistPitch { get; set; }

        /// <summary>
        /// Profile Pitch (프로파일 구간 Bay 간격).
        /// </summary>
        public int ProfilePitch { get; set; }

        /// <summary>
        /// ZAxis_Put 계산 시 (HoistPitch + Gap) 형태로 사용되는 보정값.
        /// </summary>
        public int Gap { get; set; }

        #endregion

        #region ───────── Bay / Level Lists ─────────

        /// <summary>
        /// Profile 구간에 포함되는 Bay 목록.
        /// TeachingSheet에서 Profile 전용 계산 시 이용된다.
        /// </summary>
        public ObservableCollection<int> ProfileBays { get; set; }

        /// <summary>
        /// Profile 외의 Level 목록 (예: 1층, 2층 등).
        /// TeachingSheet에서 레벨 분리 계산할 때 사용.
        /// </summary>
        public ObservableCollection<int> OtherLevels { get; set; }

        #endregion

        #region ───────── Dead Cells ─────────

        /// <summary>
        /// Teaching 계산에서 제외되는 Dead Cell 위치 목록.
        /// (Bank/Bay/Level 조합)
        /// </summary>
        public ObservableCollection<CellIdentifier> DeadCells { get; set; }

        #endregion

        #region ───────── Hoist Pitch List ─────────

        /// <summary>
        /// Bank/Bay/Level 조건에 따라 서로 다른 Hoist Pitch를 사용할 수 있음.
        /// 복수 Pitch 지원용 리스트.
        /// </summary>
        public List<int> HoistPitches { get; set; }

        #endregion

        #region ───────── Constructor ─────────

        public TeachingParams()
        {
            ProfileBays = new ObservableCollection<int>();
            OtherLevels = new ObservableCollection<int>();
            DeadCells = new ObservableCollection<CellIdentifier>();
            HoistPitches = new List<int>();
        }

        #endregion
    }
}
