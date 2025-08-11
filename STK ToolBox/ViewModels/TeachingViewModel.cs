using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using STK_ToolBox.Helpers;
using STK_ToolBox.Models;
using MessageBox = System.Windows.MessageBox;

namespace STK_ToolBox.ViewModels
{
    public class BankCellsMap
    {
        public int BankNumber { get; set; }
        public ObservableCollection<CellViewModel> Cells { get; set; }
        public ObservableCollection<int> BayLabels { get; set; }
        public ObservableCollection<int> LevelLabels { get; set; }

    }

    public class TeachingViewModel : INotifyPropertyChanged
    {
        private int _totalBank = 1;
        public int TotalBank
        {
            get => _totalBank;
            set
            {
                if (_totalBank != value)
                {
                    _totalBank = value;
                    OnPropertyChanged();
                    UpdateBankInfos();
                }
            }
        }
        private int _totalBay = 1;
        public int TotalBay
        {
            get => _totalBay;
            set { if (_totalBay != value) { _totalBay = value; OnPropertyChanged(); } }
        }
        private int _totalLevel = 1;
        public int TotalLevel
        {
            get => _totalLevel;
            set { if (_totalLevel != value) { _totalLevel = value; OnPropertyChanged(); } }
        }

        private int _basePitch, _hoistPitch, _profilePitch, _gap;
        public int BasePitch { get => _basePitch; set { _basePitch = value; OnPropertyChanged(); } }
        public int HoistPitch { get => _hoistPitch; set { _hoistPitch = value; OnPropertyChanged(); } }
        public int ProfilePitch { get => _profilePitch; set { _profilePitch = value; OnPropertyChanged(); } }
        public int Gap { get => _gap; set { _gap = value; OnPropertyChanged(); } }


        private string _hoistPitchInput;
        public string HoistPitchInput
        {
            get => _hoistPitchInput;
            set
            {
                if (_hoistPitchInput != value)
                {
                    _hoistPitchInput = value;
                    OnPropertyChanged();
                    ParseHoistPitchInput();
                }
            }
        }

        private string _otherLevelInput;
        public string OtherLevelInput
        {
            get => _otherLevelInput;
            set
            {
                if (_otherLevelInput != value)
                {
                    _otherLevelInput = value;
                    OnPropertyChanged();
                    ParseOtherLevels();
                }
            }
        }

        private string _profileBayInput;
        public string ProfileBayInput
        {
            get => _profileBayInput;
            set
            {
                if (_profileBayInput != value)
                {
                    _profileBayInput = value;
                    OnPropertyChanged();
                    ParseProfileBays();
                }
            }
        }

        public List<int> HoistPitches { get; set; } = new List<int>();

        private void ParseHoistPitchInput()
        {
            HoistPitches.Clear();
            if (!string.IsNullOrWhiteSpace(HoistPitchInput))
            {
                foreach (var part in HoistPitchInput.Split(','))
                {
                    if (int.TryParse(part.Trim(), out int pitch))
                        HoistPitches.Add(pitch);
                }
            }
        }

        public ObservableCollection<int> ProfileBays { get; set; } = new ObservableCollection<int>();
        private void ParseProfileBays()
        {
            ProfileBays.Clear();
            if (!string.IsNullOrWhiteSpace(ProfileBayInput))
            {
                foreach (var part in ProfileBayInput.Split(','))
                {
                    if (int.TryParse(part.Trim(), out int bay) && !ProfileBays.Contains(bay))
                        ProfileBays.Add(bay);
                }
            }
        }

        public ObservableCollection<int> OtherLevels { get; set; } = new ObservableCollection<int>();
        private void ParseOtherLevels()
        {
            OtherLevels.Clear();
            if (!string.IsNullOrWhiteSpace(OtherLevelInput))
            {
                foreach (var part in OtherLevelInput.Split(','))
                {
                    if (int.TryParse(part.Trim(), out int level) && !OtherLevels.Contains(level))
                        OtherLevels.Add(level);
                }
            }
        }

        public ObservableCollection<BankInfo> BankInfos { get; set; } = new ObservableCollection<BankInfo>();
        public ObservableCollection<CellIdentifier> DeadCells { get; set; } = new ObservableCollection<CellIdentifier>();
        public ObservableCollection<BankCellsMap> BankCellMaps { get; set; } = new ObservableCollection<BankCellsMap>();

        public TeachingViewModel()
        {
            DeadCells.CollectionChanged += (s, e) => UpdateAllCellStates();
            UpdateBankInfos();
        }

        private void UpdateBankInfos()
        {
            while (BankInfos.Count < TotalBank)
                BankInfos.Add(new BankInfo { BankNumber = BankInfos.Count + 1 });
            while (BankInfos.Count > TotalBank)
                BankInfos.RemoveAt(BankInfos.Count - 1);
        }

        public ICommand GenerateMapCommand => new RelayCommand(() =>
        {
            BankCellMaps.Clear();
            var bayLabels = new ObservableCollection<int>(Enumerable.Range(1, TotalBay).Reverse());
            var levelLabels = new ObservableCollection<int>(Enumerable.Range(1, TotalLevel).Reverse());

            foreach (var bank in BankInfos)
            {
                var cells = new ObservableCollection<CellViewModel>();
                for (int level = TotalLevel; level >= 1; level--)
                    for (int bay = TotalBay; bay >= 1; bay--)
                    {
                        var id = new CellIdentifier(bank.BankNumber, bay, level);
                        cells.Add(new CellViewModel(id, DeadCells.Contains(id)));
                    }
                BankCellMaps.Add(new BankCellsMap
                {
                    BankNumber = bank.BankNumber,
                    Cells = cells,
                    BayLabels = bayLabels,
                    LevelLabels = levelLabels
                });
            }
        });

        public ICommand ToggleDeadCellCommand => new RelayCommand<CellIdentifier>(id =>
        {
            if (DeadCells.Contains(id))
                DeadCells.Remove(id);
            else
                DeadCells.Add(id);

            UpdateAllCellStates();
        });

        private void UpdateAllCellStates()
        {
            foreach (var map in BankCellMaps)
                foreach (var cell in map.Cells)
                    cell.IsDead = DeadCells.Contains(cell.CellId);
        }

        private string _savePath = string.Empty;
        public string SavePath
        {
            get => _savePath;
            set { _savePath = value; OnPropertyChanged(); }
        }

        public ICommand BrowsePathCommand => new RelayCommand(() =>
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "폴더 선택|*.none", // 확장자 무효화해서 폴더만 선택하는 꼼수
                CheckFileExists = false,
                FileName = "폴더 선택"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                // 사용자가 선택한 파일의 폴더 경로를 가져옴
                SavePath = System.IO.Path.GetDirectoryName(openFileDialog.FileName);
            }
        });

        public ICommand GenerateAndSaveCommand => new RelayCommand(() =>
        {
            if (HoistPitches.Count == 0)
            {
                MessageBox.Show("Hoist Pitch 값을 입력하세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var parameters = new TeachingParams
            {
                BasePitch = this.BasePitch,
                ProfilePitch = this.ProfilePitch,
                ProfileBays = new ObservableCollection<int>(ProfileBays),
                OtherLevels = new ObservableCollection<int>(OtherLevels),
                DeadCells = new ObservableCollection<CellIdentifier>(DeadCells),
                Gap = this.Gap,
                HoistPitches = new List<int>(this.HoistPitches)
            };

            var cells = TeachingCalculator.GenerateAll(BankInfos, parameters, TotalBay, TotalLevel, Gap);
            string filename = "TeachingSheet_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx";
            string path = System.IO.Path.Combine(SavePath, filename);

            ExcelExporter.Export(cells, path, BasePitch, HoistPitches, ProfilePitch, Gap, BankInfos);
            System.Windows.MessageBox.Show("엑셀 저장 완료\n" + path);
        });

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
