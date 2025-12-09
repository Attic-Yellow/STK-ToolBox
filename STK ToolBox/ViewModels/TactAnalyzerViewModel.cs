using Microsoft.Win32;
using STK_ToolBox.Helpers;
using STK_ToolBox.Models;
using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using WinForms = System.Windows.Forms;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace STK_ToolBox.ViewModels
{
    /// <summary>
    /// TACT Analyzer 화면 전용 ViewModel.
    /// 
    /// 주요 역할:
    /// 1) 사용자가 TACT 로그 파일과 출력 폴더를 선택할 수 있도록 UI와 연결.
    /// 2) 위치/거리/속도/가속도/사이클 개수 등의 입력값을 바인딩.
    /// 3) TactAnalyzer(모델)를 호출하여 통계 결과를 생성.
    /// 4) 화면에 미리보기 텍스트를 보여주고, 결과 텍스트/로그를 파일로 저장.
    /// 5) 사용 방법을 안내하는 도움말 메시지 제공.
    /// </summary>
    public class TactAnalyzerViewModel : BaseViewModel
    {
        #region ──────────────── Private Fields ────────────────

        private string _selectedLogPath;   // 현재 선택된 TACT 로그 파일 전체 경로
        private string _previewText;       // 화면에 보여줄 요약/결과 텍스트
        private string _statusText;        // 하단 상태 표시용 텍스트

        private string _outputDirectory;   // 결과 파일(요약/로그)을 저장할 폴더 경로

        #endregion

        #region ──────────────── Public Properties (Bindings) ────────────────

        /// <summary>
        /// 결과 파일을 저장할 폴더 경로.
        /// - 사용자가 [저장 폴더 선택]에서 지정하거나
        ///   지정하지 않은 경우, 기본적으로 로그 파일이 있는 폴더를 사용.
        /// </summary>
        public string OutputDirectory
        {
            get { return _outputDirectory; }
            set
            {
                if (_outputDirectory == value) return;
                _outputDirectory = value;
                OnPropertyChanged();
            }
        }

        // ── 위치 1 ──────────────────────────────────────────────
        public string Bank1 { get; set; }
        public string Bay1 { get; set; }
        public string Level1 { get; set; }

        // ── 위치 2 ──────────────────────────────────────────────
        public string Bank2 { get; set; }
        public string Bay2 { get; set; }
        public string Level2 { get; set; }

        // ── 거리 / 속도 / 가속도 (사용자 메모용 파라미터) ───────
        public string BaseDistance { get; set; }
        public string HoistDistance { get; set; }
        public string UpSpeed { get; set; }
        public string DownSpeed { get; set; }
        public string UpAcc { get; set; }
        public string DownAcc { get; set; }

        /// <summary>
        /// 통계를 낼 사이클 개수 (텍스트로 입력받고 int로 파싱).
        /// - "가장 최근"부터 몇 개의 사이클을 사용할지 지정.
        /// </summary>
        public string CycleCountText { get; set; }

        /// <summary>
        /// 화면에 표시될 결과 요약/상세 텍스트.
        /// - 분석 조건, 파라미터, 사이클별 Total Tact, 통계 정보, 파일 경로 안내 등.
        /// </summary>
        public string PreviewText
        {
            get { return _previewText; }
            set
            {
                if (_previewText == value) return;
                _previewText = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 상태 표시줄에 보여줄 간단한 상태 메시지.
        /// - "대기 중", "로그 파일 선택 완료", "분석 완료", "오류" 등.
        /// </summary>
        public string StatusText
        {
            get { return _statusText; }
            set
            {
                if (_statusText == value) return;
                _statusText = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region ──────────────── Commands ────────────────

        /// <summary>
        /// TACT 로그 파일 선택 커맨드.
        /// </summary>
        public ICommand SelectLogCommand { get; private set; }

        /// <summary>
        /// 실제 분석 실행 커맨드.
        /// </summary>
        public ICommand AnalyzeCommand { get; private set; }

        /// <summary>
        /// 간단한 사용 방법 도움말을 표시하는 커맨드.
        /// </summary>
        public ICommand HelpCommand { get; private set; }

        /// <summary>
        /// 결과 파일 저장 폴더 선택 커맨드.
        /// </summary>
        public ICommand SelectOutputDirCommand { get; private set; }

        #endregion

        #region ──────────────── Constructor ────────────────

        public TactAnalyzerViewModel()
        {
            _selectedLogPath = string.Empty;
            _previewText = "TACT 로그를 선택한 뒤 조건을 입력하고 [분석] 버튼을 눌러주세요.";
            _statusText = "대기 중";

            // 기본값: 최근 10개 사이클 사용
            CycleCountText = "10";
            OutputDirectory = string.Empty;

            // 커맨드 바인딩
            SelectLogCommand = new RelayCommand(SelectLog);
            AnalyzeCommand = new RelayCommand(Analyze, CanAnalyze);
            HelpCommand = new RelayCommand(ShowHelp);
            SelectOutputDirCommand = new RelayCommand(SelectOutputDir);
        }

        #endregion

        #region ──────────────── Command CanExecute ────────────────

        /// <summary>
        /// [분석] 버튼 활성화 조건:
        /// - 로그 파일이 선택되어 있고, 실제 파일이 존재해야 한다.
        /// </summary>
        private bool CanAnalyze()
        {
            return !string.IsNullOrEmpty(_selectedLogPath) &&
                   File.Exists(_selectedLogPath);
        }

        #endregion

        #region ──────────────── Command Handlers ────────────────

        /// <summary>
        /// [TACT 로그 선택] 버튼 클릭 시 실행.
        /// 
        /// 1) OpenFileDialog 를 띄워 텍스트 로그를 선택하게 함.
        /// 2) 선택된 파일 경로를 _selectedLogPath 에 저장.
        /// 3) PreviewText / StatusText 를 갱신.
        /// 4) OutputDirectory 가 비어 있으면, 기본으로 로그 파일 폴더를 채워넣음.
        /// </summary>
        private void SelectLog()
        {
            try
            {
                var dlg = new OpenFileDialog
                {
                    Title = "TACT 로그 파일 선택",
                    Filter = "모든 파일 (*.*)|*.*|텍스트 로그 (*.txt)|*.txt",
                    InitialDirectory =
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                };

                bool? result = dlg.ShowDialog();
                if (result == true)
                {
                    _selectedLogPath = dlg.FileName;

                    PreviewText = string.Format("선택된 로그 파일:\r\n{0}", _selectedLogPath);
                    StatusText = "로그 파일 선택 완료";

                    // 폴더 선택이 안 되어 있으면, 기본값으로 로그 폴더를 사용
                    if (string.IsNullOrEmpty(OutputDirectory))
                    {
                        var dir = Path.GetDirectoryName(_selectedLogPath);
                        if (!string.IsNullOrEmpty(dir))
                        {
                            OutputDirectory = dir;
                        }
                    }

                    // AnalyzeCommand 의 CanExecute 재평가
                    CommandManager.InvalidateRequerySuggested();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format("파일 선택 오류: {0}", ex.Message),
                    "Tact Analyzer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                StatusText = "파일 선택 오류";
            }
        }

        /// <summary>
        /// [저장 폴더 선택] 버튼 클릭 시 실행.
        /// 
        /// 1) WinForms.FolderBrowserDialog 로 폴더 선택 UI를 띄운다.
        /// 2) 선택된 경로를 OutputDirectory 에 저장.
        /// 3) 상태 메시지를 갱신한다.
        /// </summary>
        private void SelectOutputDir()
        {
            try
            {
                var dlg = new CommonOpenFileDialog()
                {
                    Title = "TACT 요약 파일과 Tact Log를 저장할 폴더를 선택하세요.",
                    IsFolderPicker = true,
                    AllowNonFileSystemItems = false,
                    EnsurePathExists = true,
                    EnsureValidNames = true
                };

                // 초기 디렉터리 설정
                if (!string.IsNullOrEmpty(OutputDirectory) && Directory.Exists(OutputDirectory))
                {
                    dlg.InitialDirectory = OutputDirectory;
                }
                else if (!string.IsNullOrEmpty(_selectedLogPath))
                {
                    var dir = Path.GetDirectoryName(_selectedLogPath);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                        dlg.InitialDirectory = dir;
                }

                if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    OutputDirectory = dlg.FileName;
                    StatusText = $"저장 폴더 선택: {OutputDirectory}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"폴더 선택 오류: {ex.Message}",
                    "Tact Analyzer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// [분석] 버튼 클릭 시 실행되는 메인 로직.
        /// 
        /// 처리 흐름:
        /// 1) 입력값 검증 (로그 파일, 사이클 개수).
        /// 2) 위치/거리/속도/ACC 입력값 파싱(숫자 부분은 int.TryParse).
        /// 3) TactAnalyzer.AnalyzeTact 호출 → TactStatsResult 확보.
        /// 4) 화면에 보여줄 PreviewText 구성.
        /// 5) 결과 파일(TACT Summary.txt / Tact Log.txt) 저장 폴더 결정 및 파일 생성.
        /// 6) 생성된 파일 경로를 PreviewText 맨 아래에 안내.
        /// </summary>
        private void Analyze()
        {
            #region 1) 입력값 검증

            if (string.IsNullOrEmpty(_selectedLogPath) || !File.Exists(_selectedLogPath))
            {
                MessageBox.Show(
                    "먼저 TACT 로그 파일을 선택해주세요.",
                    "Tact Analyzer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            int cycleCount;
            if (!int.TryParse(CycleCountText, out cycleCount) || cycleCount <= 0)
            {
                MessageBox.Show(
                    "통계를 낼 사이클 개수를 올바르게 입력해주세요.",
                    "Tact Analyzer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            #endregion

            try
            {
                #region 2) 위치 값 파싱 (숫자로써는 현재 미사용, 0 기본값)

                int dummy;
                int bank1 = int.TryParse(Bank1, out dummy) ? dummy : 0;
                int bay1 = int.TryParse(Bay1, out dummy) ? dummy : 0;
                int level1 = int.TryParse(Level1, out dummy) ? dummy : 0;
                int bank2 = int.TryParse(Bank2, out dummy) ? dummy : 0;
                int bay2 = int.TryParse(Bay2, out dummy) ? dummy : 0;
                int level2 = int.TryParse(Level2, out dummy) ? dummy : 0;

                #endregion

                #region 3) TactAnalyzer 호출 → 통계 결과 계산

                // AnalyzeTact 내부에서:
                // - UNLOAD START ~ PICK UP TACT 구간을 각각 TactCycle 로 묶고
                // - 최신 사이클부터 cycleCount개를 사용해서
                //   Min/Max/Avg TotalSeconds(UNLOAD+PICKUP)를 계산한다.
                TactStatsResult result = TactAnalyzer.AnalyzeTact(
                    _selectedLogPath,
                    bank1, bay1, level1,
                    bank2, bay2, level2,
                    cycleCount);

                #endregion

                #region 4) 화면 출력용 PreviewText 구성

                var sb = new StringBuilder();

                sb.AppendLine("[TACT 분석 조건]");
                sb.AppendLine(string.Format("위치1 : BANK {0}, BAY {1}, LEVEL {2}",
                    Bank1, Bay1, Level1));
                sb.AppendLine(string.Format("위치2 : BANK {0}, BAY {1}, LEVEL {2}",
                    Bank2, Bay2, Level2));
                sb.AppendLine(string.Format("분석 사이클 수(최신부터) : {0}", result.UsedCycleCount));
                sb.AppendLine();
                sb.AppendLine("[입력 파라미터]");
                sb.AppendLine(string.Format("Base 거리 : {0}", BaseDistance));
                sb.AppendLine(string.Format("Hoist 거리 : {0}", HoistDistance));
                sb.AppendLine(string.Format("Up 속도  : {0}", UpSpeed));
                sb.AppendLine(string.Format("Down 속도: {0}", DownSpeed));
                sb.AppendLine(string.Format("Up Acc   : {0}", UpAcc));
                sb.AppendLine(string.Format("Down Acc : {0}", DownAcc));
                sb.AppendLine();

                sb.AppendLine("[사이클별 Total TACT (초)]");
                int idx = 1;
                foreach (var c in result.Cycles)
                {
                    sb.AppendLine(
                        string.Format("{0}. {1:yyyy-MM-dd HH:mm:ss} ~ {2:HH:mm:ss}  => {3:F3} sec",
                        idx,
                        c.StartTime,
                        c.EndTime,
                        c.TotalSeconds));
                    idx++;
                }

                sb.AppendLine();
                sb.AppendLine("[통계]");
                sb.AppendLine(string.Format("Min Total Tact : {0:F3} sec", result.MinTotal));
                sb.AppendLine(string.Format("Max Total Tact : {0:F3} sec", result.MaxTotal));
                sb.AppendLine(string.Format("Avg Total Tact : {0:F3} sec", result.AvgTotal));

                PreviewText = sb.ToString();
                StatusText = "분석 완료";

                #endregion

                #region 5) 결과 파일 저장 위치 결정

                string dir = OutputDirectory;
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                {
                    dir = Path.GetDirectoryName(_selectedLogPath);
                    if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                    {
                        dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    }
                }

                string summaryPath = Path.Combine(dir, "TACT Summary.txt");
                string tactLogPath = Path.Combine(dir, "Tact Log.txt");

                #endregion

                #region 6) Summary 파일 생성 (요약 + 파라미터 + 평균 TACT)

                using (var sw = new StreamWriter(summaryPath, false, Encoding.UTF8))
                {
                    // 첫 줄: 위치1 / base, hoist 거리
                    sw.WriteLine(string.Format("{0}/{1}/{2}\tbase : {3}\thoist : {4}",
                        Bank1, Bay1, Level1,
                        BaseDistance, HoistDistance));

                    // 둘째 줄: 위치2
                    sw.WriteLine(string.Format("{0}/{1}/{2}",
                        Bank2, Bay2, Level2));
                    sw.WriteLine();
                    sw.WriteLine("============================================================================");
                    sw.WriteLine();

                    // 통계에 사용된 Raw 로그 라인들 그대로 기록
                    foreach (string line in result.UsedLines)
                        sw.WriteLine(line);

                    sw.WriteLine();
                    sw.WriteLine("============================================================================");
                    sw.WriteLine();

                    // 속도/가속도 정보
                    sw.WriteLine(string.Format("UP : {0}", UpSpeed));
                    sw.WriteLine(string.Format("DOWN : {0}", DownSpeed));

                    string accText;
                    if (!string.IsNullOrEmpty(UpAcc) && !string.IsNullOrEmpty(DownAcc) && UpAcc != DownAcc)
                        accText = string.Format("{0} / {1}", UpAcc, DownAcc);
                    else if (!string.IsNullOrEmpty(UpAcc))
                        accText = UpAcc;
                    else if (!string.IsNullOrEmpty(DownAcc))
                        accText = DownAcc;
                    else
                        accText = string.Empty;

                    sw.WriteLine(string.Format("UP/DOWN ACC : {0}", accText));
                    sw.WriteLine();
                    sw.WriteLine(string.Format("TOTAL TACT TIME : {0:F2}", result.AvgTotal));
                }

                #endregion

                #region 7) 사용된 로그만 모은 파일 생성

                using (var sw = new StreamWriter(tactLogPath, false, Encoding.UTF8))
                {
                    foreach (string line in result.UsedLines)
                        sw.WriteLine(line);
                }

                #endregion

                #region 8) PreviewText 하단에 파일 경로 안내 추가

                PreviewText += Environment.NewLine + Environment.NewLine +
                    string.Format("요약 파일: {0}", summaryPath) + Environment.NewLine +
                    string.Format("Tact Log 파일: {0}", tactLogPath);

                #endregion
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format("분석 오류: {0}", ex.Message),
                    "Tact Analyzer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                StatusText = "분석 오류";
            }
        }

        /// <summary>
        /// [도움말] 버튼 클릭 시 실행.
        /// 
        /// - TACT Analyzer 기본 사용 순서를 안내하는 메시지 박스를 띄운다.
        /// </summary>
        private void ShowHelp()
        {
            string msg =
                "■ TACT Analyzer 사용 방법\r\n\r\n" +
                "1) [TACT 로그 선택] 버튼으로 TactLog 파일(.txt)을 선택합니다.\r\n" +
                "2) 위치 1/2, 거리, 속도, ACC는 메모용으로 입력합니다.\r\n" +
                "3) 사이클 개수에는 [가장 최근]부터 몇 개의 사이클을 통계에 사용할지 입력합니다.\r\n" +
                "4) [저장 폴더 선택] 버튼으로 결과 파일을 저장할 폴더를 지정할 수 있습니다.\r\n" +
                "5) [분석] 버튼을 누르면 UNLOAD START ~ PICK UP TACT 묶음을 1사이클로 보고,\r\n" +
                "   Total TACT(UNLOAD+PICK UP)의 Min/Max/Avg를 계산하고,\r\n" +
                "   'TACT Summary.txt'와 'Tact Log.txt'를 생성합니다.";

            MessageBox.Show(
                msg,
                "Tact Analyzer 도움말",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        #endregion
    }
}
