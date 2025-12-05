using System;

namespace STK_ToolBox.Models
{
    /// <summary>
    /// TeachingSheet / IO / 위치 계산 등에서
    /// Bank–Bay–Level 조합을 하나의 셀(위치) 식별자로 표현하는 모델.
    /// 
    /// - ValueObject 개념으로, 값 자체의 동일성을 비교하기 위해 IEquatable 구현.
    /// - Hash 기반 컬렉션(Dictionary/HashSet)에서 사용될 수 있도록 GetHashCode 오버라이드.
    /// </summary>
    public class CellIdentifier : IEquatable<CellIdentifier>
    {
        #region ───────── Properties ─────────

        /// <summary>Bank 번호</summary>
        public int Bank { get; set; }

        /// <summary>Bay 번호</summary>
        public int Bay { get; set; }

        /// <summary>Level 번호</summary>
        public int Level { get; set; }

        #endregion

        #region ───────── Constructor ─────────

        public CellIdentifier(int bank, int bay, int level)
        {
            Bank = bank;
            Bay = bay;
            Level = level;
        }

        #endregion

        #region ───────── Equality (값 기반 동일성) ─────────

        /// <summary>
        /// object.Equals 오버라이드 → CellIdentifier 비교는 값(Bank/Bay/Level) 기준.
        /// </summary>
        public override bool Equals(object obj) => Equals(obj as CellIdentifier);

        /// <summary>
        /// IEquatable 구현 → 형 안전한 비교 제공.
        /// </summary>
        public bool Equals(CellIdentifier other)
        {
            return other != null &&
                   Bank == other.Bank &&
                   Bay == other.Bay &&
                   Level == other.Level;
        }

        /// <summary>
        /// HashSet/Dictionary 사용을 위해 HashCode 오버라이드.
        /// XOR 방식으로 Bank/Bay/Level 조합.
        /// </summary>
        public override int GetHashCode()
        {
            // 간단한 XOR 조합, 값 기반 동일성에 적합
            return Bank.GetHashCode() ^
                   Bay.GetHashCode() ^
                   Level.GetHashCode();
        }

        #endregion

        #region ───────── Debug Output ─────────

        /// <summary>
        /// 로그/디버깅용 문자열 표현.
        /// </summary>
        public override string ToString() => $"({Bank}, {Bay}, {Level})";

        #endregion
    }
}
