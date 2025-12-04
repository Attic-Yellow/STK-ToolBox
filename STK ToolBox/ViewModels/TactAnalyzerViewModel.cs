using Microsoft.Win32;
using STK_ToolBox.Helpers;
using STK_ToolBox.Models;
using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using WinForms = System.Windows.Forms;

namespace STK_ToolBox.ViewModels
{
    public class TactAnalyzerViewModel : BaseViewModel
    {
        private string _selectedLogPath;
        private string _previewText;
        private string _statusText;

        //  결과 저장 폴더
        private string _outputDirectory;
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

        // 위치 1
        public string Bank1 { get; set; }
        public string Bay1 { get; set; }
        public string Level1 { get; set; }

        // 위치 2
        public string Bank2 { get; set; }
        public string Bay2 { get; set; }
        public string Level2 { get; set; }

        // 거리 / 속도 / 가속도
        public string BaseDistance { get; set; }
        public string HoistDistance { get; set; }
        public string UpSpeed { get; set; }
        public string DownSpeed { get; set; }
        public string UpAcc { get; set; }
        public string DownAcc { get; set; }

        // 통계를 낼 사이클 개수
        public string CycleCountText { get; set; }

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

        public ICommand SelectLogCommand { get; private set; }
        public ICommand AnalyzeCommand { get; private set; }
        public ICommand HelpCommand { get; private set; }

        // 저장 폴더 선택 커맨드
        public ICommand SelectOutputDirCommand { get; private set; }

        public TactAnalyzerViewModel()
        {
            _selectedLogPath = string.Empty;
            _previewText = "TACT 로그를 선택한 뒤 조건을 입력하고 [분석] 버튼을 눌러주세요.";
            _statusText = "대기 중";

            CycleCountText = "10";   // 기본값
            OutputDirectory = string.Empty;

            SelectLogCommand = new RelayCommand(SelectLog);
            AnalyzeCommand = new RelayCommand(Analyze, CanAnalyze);
            HelpCommand = new RelayCommand(ShowHelp);
            SelectOutputDirCommand = new RelayCommand(SelectOutputDir);   
        }

        private bool CanAnalyze()
        {
            return !string.IsNullOrEmpty(_selectedLogPath) &&
                   File.Exists(_selectedLogPath);
        }

        private void SelectLog()
        {
            try
            {
                var dlg = new OpenFileDialog
                {
                    Title = "TACT 로그 파일 선택",
                    Filter = "텍스트 로그 (*.txt)|*.txt|모든 파일 (*.*)|*.*",
                    InitialDirectory =
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                };

                bool? result = dlg.ShowDialog();
                if (result == true)
                {
                    _selectedLogPath = dlg.FileName;

                    PreviewText = string.Format("선택된 로그 파일:\r\n{0}", _selectedLogPath);
                    StatusText = "로그 파일 선택 완료";

                    // 폴더 선택 안 했으면, 기본값으로 로그 폴더 사용
                    if (string.IsNullOrEmpty(OutputDirectory))
                    {
                        var dir = Path.GetDirectoryName(_selectedLogPath);
                        if (!string.IsNullOrEmpty(dir))
                        {
                            OutputDirectory = dir;
                        }
                    }

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

        // 저장 폴더 선택
        private void SelectOutputDir()
        {
            try
            {
                using (var dlg = new WinForms.FolderBrowserDialog())
                {
                    dlg.Description = "TACT 요약 파일과 Tact Log를 저장할 폴더를 선택하세요.";
                    dlg.ShowNewFolderButton = true;

                    // 기본 경로
                    if (!string.IsNullOrEmpty(OutputDirectory) && Directory.Exists(OutputDirectory))
                        dlg.SelectedPath = OutputDirectory;
                    else if (!string.IsNullOrEmpty(_selectedLogPath))
                    {
                        var dir = Path.GetDirectoryName(_selectedLogPath);
                        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                            dlg.SelectedPath = dir;
                    }

                    if (dlg.ShowDialog() == WinForms.DialogResult.OK)
                    {
                        OutputDirectory = dlg.SelectedPath;
                        StatusText = string.Format("저장 폴더 선택: {0}", OutputDirectory);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format("폴더 선택 오류: {0}", ex.Message),
                    "Tact Analyzer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void Analyze()
        {
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

            try
            {
                int dummy;
                int bank1 = int.TryParse(Bank1, out dummy) ? dummy : 0;
                int bay1 = int.TryParse(Bay1, out dummy) ? dummy : 0;
                int level1 = int.TryParse(Level1, out dummy) ? dummy : 0;
                int bank2 = int.TryParse(Bank2, out dummy) ? dummy : 0;
                int bay2 = int.TryParse(Bay2, out dummy) ? dummy : 0;
                int level2 = int.TryParse(Level2, out dummy) ? dummy : 0;

                // 여기서 AnalyzeTact는 그대로 사용
                TactStatsResult result = TactAnalyzer.AnalyzeTact(
                    _selectedLogPath,
                    bank1, bay1, level1,
                    bank2, bay2, level2,
                    cycleCount);

                //  Preview 텍스트 구성 
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

                //  파일 저장 위치 결정 
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

                //  Summary 파일 생성
                using (var sw = new StreamWriter(summaryPath, false, Encoding.UTF8))
                {
                    sw.WriteLine(string.Format("{0}/{1}/{2}\tbase : {3}\thoist : {4}",
                        Bank1, Bay1, Level1,
                        BaseDistance, HoistDistance));
                    sw.WriteLine(string.Format("{0}/{1}/{2}",
                        Bank2, Bay2, Level2));
                    sw.WriteLine();
                    sw.WriteLine("============================================================================");
                    sw.WriteLine();

                    foreach (string line in result.UsedLines)
                        sw.WriteLine(line);

                    sw.WriteLine();
                    sw.WriteLine("============================================================================");
                    sw.WriteLine();
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

                //  사용된 로그만 모은 파일 
                using (var sw = new StreamWriter(tactLogPath, false, Encoding.UTF8))
                {
                    foreach (string line in result.UsedLines)
                        sw.WriteLine(line);
                }

                // 경로 안내 추가
                PreviewText += Environment.NewLine + Environment.NewLine +
                    string.Format("요약 파일: {0}", summaryPath) + Environment.NewLine +
                    string.Format("Tact Log 파일: {0}", tactLogPath);
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
    }
}
