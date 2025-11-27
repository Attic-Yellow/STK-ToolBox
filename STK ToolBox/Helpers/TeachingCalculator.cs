using System.Collections.Generic;
using System.Linq;
using STK_ToolBox.Models;

namespace STK_ToolBox.Helpers
{
    public static class TeachingCalculator
    {
        public static List<TeachingCell> GenerateAll(IEnumerable<BankInfo> banks, TeachingParams param, int maxBay, int maxLevel, int gap)
        {
            var result = new List<TeachingCell>();

            foreach (var bank in banks)
            {
                for (int bay = 1; bay <= maxBay; bay++)
                {
                    for (int level = 1; level <= maxLevel; level++)
                    {
                        var cellId = new CellIdentifier(bank.BankNumber, bay, level);
                        if (param.DeadCells.Contains(cellId))
                        {
                            continue;
                        }

                        int baseVal = bank.BaseValue;
                        for (int b = bank.BaseBay + 1; b <= bay; b++)
                        {
                            baseVal += param.ProfileBays.Contains(b) ? param.ProfilePitch : param.BasePitch;
                        }

                        int hoistVal = bank.HoistValue;
                        int shelfSize = 0;

                        if (level > bank.BaseLevel)
                        {
                            // 기준 이후 → 증가
                            for (int l = bank.BaseLevel + 1; l <= level; l++)
                            {
                                int idx = 0;
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
                            // 기준 이전 → 감소
                            for (int l = bank.BaseLevel; l > level; l--)
                            {
                                int idx = 0;
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
    }
}