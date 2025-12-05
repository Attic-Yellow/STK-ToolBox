using System.Collections.Generic;
using System.Linq;
using STK_ToolBox.Models;

namespace STK_ToolBox.Helpers
{
    /// <summary>
    /// Teaching 값 계산 전담 헬퍼.
    /// 
    /// 역할 개요:
    /// - BankInfo(기준열 정보) + TeachingParams(피치/레벨/DeadCell 설정)를 기반으로
    ///   전체 Bank/Bay/Level 격자에 대한 TeachingCell 목록을 생성한다.
    /// 
    /// 계산 규칙(요약):
    /// 1) Base (수평 방향)
    ///    - Bank별 BaseBay / BaseValue 를 기준으로,
    ///      BaseBay 이후 Bay 마다 BasePitch 또는 ProfilePitch 를 누적.
    ///    - ProfileBays 에 포함된 Bay 는 ProfilePitch, 그 외는 BasePitch 사용.
    /// 
    /// 2) Hoist (수직 방향, Zaxis_Get / Zaxis_Put)
    ///    - Bank별 BaseLevel / HoistValue 를 기준으로,
    ///      Level 방향으로 올라가거나 내려가면서 HoistPitches 를 누적/감산.
    ///    - OtherLevels 리스트와 HoistPitches 인덱스를 매칭해서 층별로 다른 피치를 적용.
    ///    - gap 은 마지막에 Zaxis_Put = Hoist + gap 으로 적용.
    /// 
    /// 3) DeadCells
    ///    - DeadCells 에 포함된 (Bank,Bay,Level) 은 TeachingCell 생성에서 제외.
    /// 
    /// 4) ShelfSize
    ///    - OtherLevels / HoistPitches 인덱스 계산 로직을 통해 level 이동 시 결정.
    /// </summary>
    public static class TeachingCalculator
    {
        #region ───────── Public API ─────────

        /// <summary>
        /// 모든 Bank/Bay/Level 조합에 대해 TeachingCell 리스트를 생성.
        /// 
        /// banks    : Bank별 기준 정보 (Base/Hoist/Turn/Fork/기준 Bay/Level 등)
        /// param    : Pitch, ProfileBays, OtherLevels, DeadCells, HoistPitches 등 Teaching 파라미터
        /// maxBay   : 생성할 최대 Bay 번호 (1~maxBay)
        /// maxLevel : 생성할 최대 Level 번호 (1~maxLevel)
        /// gap      : Zaxis_Put 계산 시 추가되는 값 (Zaxis_Put = Hoist + gap)
        /// </summary>
        public static List<TeachingCell> GenerateAll(
            IEnumerable<BankInfo> banks,
            TeachingParams param,
            int maxBay,
            int maxLevel,
            int gap)
        {
            var result = new List<TeachingCell>();

            foreach (var bank in banks)
            {
                for (int bay = 1; bay <= maxBay; bay++)
                {
                    for (int level = 1; level <= maxLevel; level++)
                    {
                        // 1) Dead Cell 체크
                        var cellId = new CellIdentifier(bank.BankNumber, bay, level);
                        if (param.DeadCells.Contains(cellId))
                            continue;

                        // 2) Base 계산 (수평 방향)
                        int baseVal = bank.BaseValue;
                        for (int b = bank.BaseBay + 1; b <= bay; b++)
                        {
                            bool isProfileBay = param.ProfileBays.Contains(b);
                            baseVal += isProfileBay
                                ? param.ProfilePitch
                                : param.BasePitch;
                        }

                        // 3) Hoist / ShelfSize 계산 (수직 방향)
                        int hoistVal = bank.HoistValue;
                        int shelfSize = 0;

                        if (level > bank.BaseLevel)
                        {
                            // 기준 Level 이후 → 위로 증가
                            for (int l = bank.BaseLevel + 1; l <= level; l++)
                            {
                                int idx = 0;

                                // OtherLevels / HoistPitches 인덱스 결정
                                for (int k = 0; k < param.OtherLevels.Count; k++)
                                {
                                    if (l > param.OtherLevels[k])
                                    {
                                        idx = k + 1;
                                        shelfSize = idx;
                                    }
                                    else if (l == param.OtherLevels[k])
                                    {
                                        shelfSize = k + 1;
                                        break;
                                    }
                                    else
                                    {
                                        shelfSize = k;
                                        break;
                                    }
                                }

                                if (param.HoistPitches.Count == 0)
                                    continue;

                                if (idx >= param.HoistPitches.Count)
                                    idx = param.HoistPitches.Count - 1;

                                hoistVal += param.HoistPitches[idx];
                            }
                        }
                        else if (level < bank.BaseLevel)
                        {
                            // 기준 Level 이전 → 아래로 감소
                            for (int l = bank.BaseLevel; l > level; l--)
                            {
                                int idx = 0;

                                // OtherLevels / HoistPitches 인덱스 결정
                                for (int k = 0; k < param.OtherLevels.Count; k++)
                                {
                                    if (l - 1 > param.OtherLevels[k])
                                    {
                                        idx = k + 1;
                                        shelfSize = idx;
                                    }
                                    else if (l == param.OtherLevels[k])
                                    {
                                        shelfSize = k + 1;
                                        break;
                                    }
                                    else
                                    {
                                        shelfSize = k;
                                        break;
                                    }
                                }

                                if (param.HoistPitches.Count == 0)
                                    continue;

                                if (idx >= param.HoistPitches.Count)
                                    idx = param.HoistPitches.Count - 1;

                                hoistVal -= param.HoistPitches[idx];
                            }
                        }
                        // level == BaseLevel 인 경우 hoistVal = bank.HoistValue, shelfSize = 0

                        // 4) TeachingCell 생성 및 추가
                        result.Add(new TeachingCell
                        {
                            Bank = bank.BankNumber,
                            Bay = bay,
                            Level = level,
                            Base = baseVal,
                            Zaxis_Get = hoistVal,
                            Zaxis_Put = hoistVal + gap,
                            Laxis = bank.TurnValue,
                            Saxis = bank.ForkValue,
                            ShelfSize = shelfSize
                        });
                    }
                }
            }

            return result;
        }

        #endregion
    }
}
