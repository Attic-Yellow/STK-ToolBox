using ClosedXML.Excel;
using STK_ToolBox.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;

namespace STK_ToolBox.Helpers
{
    /// <summary>
    /// Excel 내보내기(Export) 관련 공통 헬퍼.
    /// 
    /// - TeachingExcelExport : TeachingSheet 형식으로 위치/축값/Bank 기준정보를 내보냄.
    /// - IoByteSheetExport   : IO Byte Sheet 형식으로 X/Y 세그먼트를 각각 시트로 내보냄.
    /// 
    /// ClosedXML을 사용하여 엑셀을 직접 생성한다.
    /// </summary>
    public static class ExcelExporter
    {
        #region ───────── Teaching Excel Export ─────────

        /// <summary>
        /// Teaching Excel 파일 생성.
        /// 
        /// 구조 개요:
        /// - "Teaching" 시트 1개 생성
        /// - (3,1)~(3,17) : 헤더 (Bank/Bay/Level/Base/S/L/ZGet/ZPut/각종 Flag)
        /// - (4,1)부터 : TeachingCell 리스트를 순서대로 출력
        /// - 우측(20열~) : Pitch 정보(Base/Hoist/Profile, Gap) 및 Bank 기준값(Base/Hoist/Turn/Fork 등)
        /// 
        /// 비고:
        /// - Base/S/L/Z 값은 문자열 형태로 저장되도록 1~8열에 Text 포맷 지정
        /// - DeadCell 여부(IsDead)는 UseFlag와 별도로 9열(IsPort) "TRUE/FALSE" 형태로 기록
        /// </summary>
        public static void TeachingExcelExport(
            List<TeachingCell> cells,
            string path,
            int basePitch,
            List<int> hoistPitches,
            int profilePitch,
            int gap,
            ObservableCollection<BankInfo> bankInfos)
        {
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.AddWorksheet("Teaching");

                // ── Pitch 정보 영역 (3행, 20열~) ─────────────────────────────
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

                // ── Bank 기준 정보 헤더 (7~8행, 20열~) ────────────────────────
                ws.Cell(7, 20).Value = "기준열";
                ws.Cell(8, 20).Value = "Bank";
                ws.Cell(8, 21).Value = "Bay";
                ws.Cell(8, 22).Value = "Level";
                ws.Cell(8, 23).Value = "Base";
                ws.Cell(8, 24).Value = "Saxis";
                ws.Cell(8, 25).Value = "Laxis";
                ws.Cell(8, 26).Value = "Zaxis_Get";
                ws.Cell(8, 27).Value = "Zaxis_Put";

                // ── 메인 Teaching 테이블 헤더 (3행, 1~17열) ───────────────────
                ws.Cell(3, 1).Value = "Bank";
                ws.Cell(3, 2).Value = "Bay";
                ws.Cell(3, 3).Value = "Level";
                ws.Cell(3, 4).Value = "Base";
                ws.Cell(3, 5).Value = "Saxis";
                ws.Cell(3, 6).Value = "Laxis";
                ws.Cell(3, 7).Value = "Zaxis_Get";
                ws.Cell(3, 8).Value = "Zaxis_Put";
                ws.Cell(3, 9).Value = "IsPort";
                ws.Cell(3, 10).Value = "PortAccessNo";
                ws.Cell(3, 11).Value = "UseFlag";
                ws.Cell(3, 12).Value = "ShelfSize";
                ws.Cell(3, 13).Value = "ATBase";
                ws.Cell(3, 14).Value = "ATZaxis";
                ws.Cell(3, 15).Value = "ATSaxis";
                ws.Cell(3, 16).Value = "SlopeSensorUseFlag";
                ws.Cell(3, 17).Value = "ForkStretchSensorUseFlag";

                // ── TeachingCell 출력 (4행부터) ──────────────────────────────
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
                    ws.Cell(row, 9).Value = cell.IsDead ? "TRUE" : "FALSE";
                    ws.Cell(row, 10).Value = 0;
                    ws.Cell(row, 11).Value = "TRUE";
                    ws.Cell(row, 12).Value = cell.ShelfSize;
                    ws.Cell(row, 13).Value = 0;
                    ws.Cell(row, 14).Value = 0;
                    ws.Cell(row, 15).Value = 0;
                    ws.Cell(row, 16).Value = "FALSE";
                    ws.Cell(row, 17).Value = "TRUE";

                    // Base~Zaxis_Put 열(1~8)을 텍스트 형식으로 강제
                    // → 불필요한 지수 표기, 자동 형식 변경 방지
                    for (int col = 1; col <= 8; col++)
                    {
                        ws.Cell(row, col).Style.NumberFormat.SetFormat("@");
                    }
                }

                // ── BankInfo 기준열 출력 (9행부터, 20열~) ─────────────────────
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
                    ws.Cell(row, 27).Value = bankInfo.HoistValue + gap;    // Zaxis_Put = Hoist + Gap
                }

                workbook.SaveAs(path);
            }
        }

        #endregion

        #region ───────── 공통 Hex 파싱 헬퍼 ─────────

        /// <summary>
        /// 16진 문자열을 int로 파싱. 실패 시 0 반환.
        /// </summary>
        private static int ParseHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return 0;

            int value;
            if (!int.TryParse(
                    hex.Trim(),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out value))
            {
                return 0;
            }
            return value;
        }

        #endregion

        #region ───────── IO Byte Sheet Export ─────────

        /// <summary>
        /// IO Byte Sheet Export.
        /// 
        /// - IOByteSegment 리스트를 입력받아 X/Y를 각 시트로 분리해서 내보낸다.
        /// - 시트 이름:
        ///   - IOByteTable_X
        ///   - IOByteTable_Y
        /// - 열 구성:
        ///   Channel / StartAddr / ByteSize / Source
        /// - StartAddr 열은 텍스트 형식으로 강제해 3E0, 4E0 같은 값이
        ///   지수 표기나 숫자 형식으로 깨지지 않도록 한다.
        /// </summary>
        public static void IoByteSheetExport(IEnumerable<IOByteSegment> segments, string path)
        {
            if (segments == null) return;

            using (var workbook = new XLWorkbook())
            {
                // ── Device=X / Y 로 분리 ────────────────────────────────────
                var xList = segments
                    .Where(s => string.Equals(s.Device, "X", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var yList = segments
                    .Where(s => string.Equals(s.Device, "Y", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // ───────── X Sheet ─────────
                var wsX = workbook.AddWorksheet("IOByteTable_X");
                wsX.Cell(1, 1).Value = "Channel";
                wsX.Cell(1, 2).Value = "StartAddr";
                wsX.Cell(1, 3).Value = "ByteSize";
                wsX.Cell(1, 4).Value = "Source";

                int row = 2;
                foreach (var seg in xList
                    .OrderBy(s => s.Channel)
                    .ThenBy(s => ParseHex(s.StartAddressHex)))
                {
                    wsX.Cell(row, 1).Value = seg.Channel;

                    var cAddr = wsX.Cell(row, 2);
                    cAddr.Value = seg.StartAddressHex;
                    cAddr.DataType = XLDataType.Text; // 3E0, 5E0 등 텍스트로 유지

                    wsX.Cell(row, 3).Value = seg.ByteSize;
                    wsX.Cell(row, 4).Value = seg.Source;

                    row++;
                }
                wsX.Columns().AdjustToContents();

                // ───────── Y Sheet ─────────
                var wsY = workbook.AddWorksheet("IOByteTable_Y");
                wsY.Cell(1, 1).Value = "Channel";
                wsY.Cell(1, 2).Value = "StartAddr";
                wsY.Cell(1, 3).Value = "ByteSize";
                wsY.Cell(1, 4).Value = "Source";

                row = 2;
                foreach (var seg in yList
                    .OrderBy(s => s.Channel)
                    .ThenBy(s => ParseHex(s.StartAddressHex)))
                {
                    wsY.Cell(row, 1).Value = seg.Channel;

                    var cAddr = wsY.Cell(row, 2);
                    cAddr.Value = seg.StartAddressHex;
                    cAddr.DataType = XLDataType.Text; // 3E0, 4E0, 5E0 텍스트 고정

                    wsY.Cell(row, 3).Value = seg.ByteSize;
                    wsY.Cell(row, 4).Value = seg.Source;

                    row++;
                }
                wsY.Columns().AdjustToContents();

                workbook.SaveAs(path);
            }
        }

        #endregion
    }
}
