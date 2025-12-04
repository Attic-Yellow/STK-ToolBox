using Microsoft.Win32;
using STK_ToolBox.Helpers;
using STK_ToolBox.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
// WinForms 전체를 쓰지 말고, Alias 로만 사용
using WinForms = System.Windows.Forms;

namespace STK_ToolBox.ViewModels
{
    public class TorqueAnalyzerViewModel : BaseViewModel
    {
        private string _selectedLogPath;
        private string _previewText;
        private string _statusText;
        private string _outputDirectory;

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

        // 저장 폴더 경로
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

        public ICommand SelectLogCommand { get; private set; }
        public ICommand AnalyzeCommand { get; private set; }
        public ICommand MergeLogsCommand { get; private set; }
        public ICommand BrowseOutputDirCommand { get; private set; }

        public TorqueAnalyzerViewModel()
        {
            _selectedLogPath = string.Empty;
            _previewText = "로그를 선택한 뒤 [분석 및 미리보기]를 눌러주세요.";
            _statusText = "대기 중";

            // 기본 저장 폴더: 내 문서\TorqueResults
            string defaultRoot = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            OutputDirectory = Path.Combine(defaultRoot, "TorqueResults");

            SelectLogCommand = new RelayCommand(SelectLog);
            AnalyzeCommand = new RelayCommand(Analyze, CanAnalyze);
            MergeLogsCommand = new RelayCommand(MergeLogs);
            BrowseOutputDirCommand = new RelayCommand(BrowseOutputDir);
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
                    Title = "Torque 로그 파일 선택",
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

                    CommandManager.InvalidateRequerySuggested();
                }
            }
            catch (Exception ex)
            {
                // MessageBox 는 WPF 의 System.Windows.MessageBox
                MessageBox.Show(
                    string.Format("파일 선택 오류: {0}", ex.Message),
                    "Torque Analyzer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                StatusText = "파일 선택 오류";
            }
        }

        // 저장 폴더 선택
        private void BrowseOutputDir()
        {
            try
            {
                // WinForms.FolderBrowserDialog 사용 (Alias)
                using (var dlg = new WinForms.FolderBrowserDialog())
                {
                    dlg.Description = "Torque 분석 결과 및 병합 로그 저장 폴더를 선택하세요.";

                    if (!string.IsNullOrEmpty(OutputDirectory) && Directory.Exists(OutputDirectory))
                        dlg.SelectedPath = OutputDirectory;
                    else
                        dlg.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                    WinForms.DialogResult result = dlg.ShowDialog();
                    if (result == WinForms.DialogResult.OK && !string.IsNullOrEmpty(dlg.SelectedPath))
                    {
                        OutputDirectory = dlg.SelectedPath;
                        StatusText = string.Format("저장 폴더 설정: {0}", OutputDirectory);
                    }
                }
            }
            catch (Exception ex)
            {
                // MessageBox WPF 버전
                MessageBox.Show(
                    string.Format("폴더 선택 오류: {0}", ex.Message),
                    "Torque Analyzer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                StatusText = "폴더 선택 오류";
            }
        }

        private void MergeLogs()
        {
            try
            {
                var dlg = new OpenFileDialog
                {
                    Title = "여러 Torque 로그 파일 선택 (시간순 병합)",
                    Filter = "텍스트 로그 (*.txt)|*.txt|모든 파일 (*.*)|*.*",
                    InitialDirectory =
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    Multiselect = true
                };

                bool? result = dlg.ShowDialog();
                if (result != true || dlg.FileNames == null || dlg.FileNames.Length == 0)
                    return;

                if (dlg.FileNames.Length == 1)
                {
                    _selectedLogPath = dlg.FileNames[0];
                    PreviewText = string.Format("선택된 로그 파일:\r\n{0}", _selectedLogPath);
                    StatusText = "로그 파일 선택 완료";
                    CommandManager.InvalidateRequerySuggested();
                    return;
                }

                // OutputDirectory 사용
                string mergedPath = TorqueAnalyzer.MergeLogsWithSorting(dlg.FileNames, OutputDirectory);

                _selectedLogPath = mergedPath;

                PreviewText =
                    "[병합 결과]\r\n\r\n" +
                    string.Format("선택된 로그 파일 수: {0}개\r\n", dlg.FileNames.Length) +
                    string.Format("병합된 로그 경로:\r\n{0}", mergedPath);

                StatusText = "로그 병합 완료";

                CommandManager.InvalidateRequerySuggested();

                MessageBoxResult mb = MessageBox.Show(
                    "병합된 로그 파일로 Torque 분석을 진행할까요?",
                    "Torque Analyzer",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (mb == MessageBoxResult.Yes)
                {
                    Analyze();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format("로그 병합 오류: {0}", ex.Message),
                    "Torque Analyzer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                StatusText = "로그 병합 오류";
            }
        }

        private void Analyze()
        {
            if (string.IsNullOrEmpty(_selectedLogPath))
            {
                MessageBox.Show(
                    "먼저 로그 파일을 선택해주세요.",
                    "Torque Analyzer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                TorqueResult result = TorqueAnalyzer.AnalyzeTorqueWithReturn(_selectedLogPath, OutputDirectory);

                PreviewText =
                    "[Torque 분석 결과 요약]\r\n\r\n" +
                    string.Format("Base  → Min: {0}  Max: {1}  Avg: {2:F1}\r\n",
                        result.BaseMin, result.BaseMax, result.BaseAvg) +
                    string.Format("Hoist → Min: {0}  Max: {1}  Avg: {2:F1}\r\n",
                        result.HoistMin, result.HoistMax, result.HoistAvg) +
                    string.Format("Fork  → Min: {0}  Max: {1}  Avg: {2:F1}\r\n",
                        result.ForkMin, result.ForkMax, result.ForkAvg) +
                    string.Format("Turn  → Min: {0}  Max: {1}  Avg: {2:F1}\r\n\r\n",
                        result.TurnMin, result.TurnMax, result.TurnAvg) +
                    string.Format("Excel 저장 위치:\r\n{0}", result.ExcelPath);

                StatusText = "분석 완료";

                MessageBoxResult mb = MessageBox.Show(
                    "Excel 파일을 여시겠습니까?",
                    "Torque Analyzer",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (mb == MessageBoxResult.Yes && File.Exists(result.ExcelPath))
                {
                    Process.Start(new ProcessStartInfo(result.ExcelPath)
                    {
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format("분석 오류: {0}", ex.Message),
                    "Torque Analyzer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                StatusText = "오류";
            }
        }
    }
}
