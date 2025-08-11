using ClosedXML.Excel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using STK_ToolBox.Models;

namespace STK_ToolBox.Helpers
{
    public static class ExcelExporter
    {
        public static void Export(List<TeachingCell> cells, string path, int basePitch, List<int> hoistPitches, int profilePitch, int gap, ObservableCollection<BankInfo> bankInfos)
        {
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.AddWorksheet("Teaching");

                // Pitch info
                ws.Cell(3, 20).Value = "Base Pitch";
                ws.Cell(3, 21).Value = "Profile Pitch";
                for (int i = 0; i < hoistPitches.Count; i++)
                {
                    ws.Cell(3, 22 + i).Value = $"Hoist Pitch {i + 1}";
                }

                ws.Cell(4, 20).Value = basePitch;
                ws.Cell(4, 21).Value = profilePitch;
                for (int i = 0; i < hoistPitches.Count; i++)
                {
                    ws.Cell(4, 22 + i).Value = hoistPitches[i];
                }


                // Base info
                ws.Cell(7, 20).Value = "기준열";
                ws.Cell(8, 20).Value = "Bank";
                ws.Cell(8, 21).Value = "Bay";
                ws.Cell(8, 22).Value = "Level";
                ws.Cell(8, 23).Value = "Base";
                ws.Cell(8, 24).Value = "Saxis";
                ws.Cell(8, 25).Value = "Laxis";
                ws.Cell(8, 26).Value = "Zaxis_Get";
                ws.Cell(8, 27).Value = "Zaxis_Put";

                // Header
                ws.Cell(3, 1).Value = "Bank";
                ws.Cell(3, 2).Value = "Bay";
                ws.Cell(3, 3).Value = "Level";
                ws.Cell(3, 4).Value = "Base";
                ws.Cell(3, 5).Value = "Saxis";
                ws.Cell(3, 6).Value = "Laxis";
                ws.Cell(3, 7).Value = "Zaxis_Get";
                ws.Cell(3, 8).Value = "Zaxis_Put";
                ws.Cell(3, 9).Value = "IsPort";

                for (int i = 0; i < cells.Count; i++)
                {
                    var cell = cells[i];
                    int row = i + 4;
                    ws.Cell(row, 1).Value = cell.Bank;
                    ws.Cell(row, 2).Value = cell.Bay;
                    ws.Cell(row, 3).Value = cell.Level;
                    ws.Cell(row, 4).Value = cell.Base;
                    ws.Cell(row, 5).Value = cell.Saxis;
                    ws.Cell(row, 6).Value = cell.Laxis;
                    ws.Cell(row, 7).Value = cell.Zaxis_Get;
                    ws.Cell(row, 8).Value = cell.Zaxis_Put;
                    ws.Cell(row, 9).Value = cell.IsDead ? "True" : "False";

                    for (int col = 1; col <= 8; col++)
                    {
                        ws.Cell(row, col).Style.NumberFormat.SetFormat("@");
                    }
                }

                for (int i = 0; i < bankInfos.Count; i++)
                {
                    var bankInfo = bankInfos[i];
                    int row = i + 9;
                    ws.Cell(row, 20).Value = bankInfo.BankNumber;
                    ws.Cell(row, 21).Value = bankInfo.BaseBay;
                    ws.Cell(row, 22).Value = bankInfo.BaseLevel;
                    ws.Cell(row, 23).Value = bankInfo.BaseValue;
                    ws.Cell(row, 24).Value = bankInfo.TurnValue;
                    ws.Cell(row, 25).Value = bankInfo.ForkValue;
                    ws.Cell(row, 26).Value = bankInfo.HoistValue;
                    ws.Cell(row, 27).Value = bankInfo.HoistValue + gap;
                }

                workbook.SaveAs(path);
            }
        }
    }
}
