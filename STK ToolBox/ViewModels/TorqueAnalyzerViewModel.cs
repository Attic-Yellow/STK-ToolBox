using Microsoft.Win32;
using STK_ToolBox.Helpers;
using STK_ToolBox.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace STK_ToolBox.ViewModels
{
    public class TorqueAnalyzerViewModel : BaseViewModel
    {
        private string _selectedLogPath;
        private string _previewText;
        private string _statusText;

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

        public TorqueAnalyzerViewModel()
        {
            _selectedLogPath = string.Empty;
            _previewText = "로그를 선택한 뒤 [분석 및 미리보기]를 눌러주세요.";
            _statusText = "대기 중";

            // RelayCommand(Action execute, Func<bool> canExecute = null) 가정
            SelectLogCommand = new RelayCommand(SelectLog);
            AnalyzeCommand = new RelayCommand(Analyze, CanAnalyze);
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

                    // CanExecute 다시 평가
                    CommandManager.InvalidateRequerySuggested();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format("파일 선택 오류: {0}", ex.Message),
                    "Torque Analyzer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                StatusText = "파일 선택 오류";
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
                TorqueResult result = TorqueAnalyzer.AnalyzeTorqueWithReturn(_selectedLogPath);

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
