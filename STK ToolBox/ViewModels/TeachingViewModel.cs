using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using STK_ToolBox.Helpers;
using STK_ToolBox.Models;
using MessageBox = System.Windows.MessageBox;
using System.IO;
using Newtonsoft.Json;

namespace STK_ToolBox.ViewModels
{
    #region === Preset DTO / Map Model ===
    // ===== 프리셋 저장용 DTO =====
    public class TeachingPreset
    {
        // 단일 값
        public int BasePitch { get; set; }
        public int ProfilePitch { get; set; }
        public int Gap { get; set; }
        public int TotalBank { get; set; }
        public int TotalBay { get; set; }
        public int TotalLevel { get; set; }

        // 문자열 입력 원문(파서가 다시 읽도록)
        public string HoistPitchInput { get; set; }
        public string ProfileBayInput { get; set; }
        public string OtherLevelInput { get; set; }

        // 기준열(BankInfos 그리드) 값들
        public List<BankInfo> BankInfos { get; set; }
        // 선택된 데드셀 저장
        public List<CellIdentifier> DeadCells { get; set; }
    }

    public class BankCellsMap
    {
        public int BankNumber { get; set; }
        public ObservableCollection<CellViewModel> Cells { get; set; }
        public ObservableCollection<int> BayLabels { get; set; }
        public ObservableCollection<int> LevelLabels { get; set; }
    }
    #endregion

    public class TeachingViewModel : INotifyPropertyChanged
    {
        #region === Fields ===
        private int _totalBank = 1;
        private int _totalBay = 1;
        private int _totalLevel = 1;

        private int _basePitch, _hoistPitch, _profilePitch, _gap;

        private string _hoistPitchInput;
        private string _otherLevelInput;
        private string _profileBayInput;

        private string _lastPresetPath;
        private string _savePath = string.Empty;
        #endregion

        #region === Public Properties (Sizes / Pitches / Inputs) ===
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

        public int TotalBay
        {
            get => _totalBay;
            set { if (_totalBay != value) { _totalBay = value; OnPropertyChanged(); } }
        }

        public int TotalLevel
        {
            get => _totalLevel;
            set { if (_totalLevel != value) { _totalLevel = value; OnPropertyChanged(); } }
        }

        public int BasePitch { get => _basePitch; set { _basePitch = value; OnPropertyChanged(); } }
        public int HoistPitch { get => _hoistPitch; set { _hoistPitch = value; OnPropertyChanged(); } }
        public int ProfilePitch { get => _profilePitch; set { _profilePitch = value; OnPropertyChanged(); } }
        public int Gap { get => _gap; set { _gap = value; OnPropertyChanged(); } }

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

        public string LastPresetPath
        {
            get => _lastPresetPath;
            set { _lastPresetPath = value; OnPropertyChanged(); }
        }

        public string SavePath
        {
            get => _savePath;
            set { _savePath = value; OnPropertyChanged(); }
        }
        #endregion

        #region === Parsed Collections (from Inputs) ===
        public List<int> HoistPitches { get; set; } = new List<int>();
        public ObservableCollection<int> ProfileBays { get; set; } = new ObservableCollection<int>();
        public ObservableCollection<int> OtherLevels { get; set; } = new ObservableCollection<int>();

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
        #endregion

        #region === Data Collections (Main Data) ===
        public ObservableCollection<BankInfo> BankInfos { get; set; } = new ObservableCollection<BankInfo>();
        public ObservableCollection<CellIdentifier> DeadCells { get; set; } = new ObservableCollection<CellIdentifier>();
        public ObservableCollection<BankCellsMap> BankCellMaps { get; set; } = new ObservableCollection<BankCellsMap>();
        #endregion

        #region === Ctor ===
        public TeachingViewModel()
        {
            DeadCells.CollectionChanged += (s, e) => UpdateAllCellStates();
            UpdateBankInfos();
        }
        #endregion

        #region === Helpers: BankInfos/Cells Sync ===
        private void UpdateBankInfos()
        {
            while (BankInfos.Count < TotalBank)
                BankInfos.Add(new BankInfo { BankNumber = BankInfos.Count + 1 });
            while (BankInfos.Count > TotalBank)
                BankInfos.RemoveAt(BankInfos.Count - 1);
        }

        private void UpdateAllCellStates()
        {
            foreach (var map in BankCellMaps)
                foreach (var cell in map.Cells)
                    cell.IsDead = DeadCells.Contains(cell.CellId);
        }

        private void OverwriteBankInfosFromPreset(List<BankInfo> src)
        {
            if (src == null) return;

            // 편집 상태를 끊기 위해 컬렉션을 통째로 교체(=Clear 후 재채움)
            BankInfos.Clear();

            for (int i = 0; i < TotalBank; i++)
            {
                var s = (i < src.Count) ? src[i] : new BankInfo { BankNumber = i + 1 };

                BankInfos.Add(new BankInfo
                {
                    BankNumber = (s.BankNumber == 0) ? (i + 1) : s.BankNumber,
                    BaseBay = s.BaseBay,
                    BaseLevel = s.BaseLevel,
                    BaseValue = s.BaseValue,
                    HoistValue = s.HoistValue,
                    TurnValue = s.TurnValue,
                    ForkValue = s.ForkValue
                });
            }

            // (안전) 바인딩 강제 갱신
            OnPropertyChanged(nameof(BankInfos));
        }
        #endregion

        #region === Commands: Map / DeadCell ===
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
        #endregion

        #region === Commands: Save Excel ===
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
        #endregion

        #region === Commands: Preset Save/Load (JSON) ===
        public ICommand SavePresetCommand => new RelayCommand(SavePreset);
        public ICommand LoadPresetCommand => new RelayCommand(LoadPreset);

        private void SavePreset()
        {
            try
            {
                var sfd = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "설정 저장",
                    Filter = "Teaching Preset (*.json)|*.json",
                    FileName = "TeachingPreset_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json",
                    AddExtension = true,
                    OverwritePrompt = true
                };

                if (sfd.ShowDialog() == true)
                {
                    var preset = new TeachingPreset
                    {
                        BasePitch = this.BasePitch,
                        ProfilePitch = this.ProfilePitch,
                        Gap = this.Gap,
                        TotalBank = this.TotalBank,
                        TotalBay = this.TotalBay,
                        TotalLevel = this.TotalLevel,

                        HoistPitchInput = this.HoistPitchInput,
                        ProfileBayInput = this.ProfileBayInput,
                        OtherLevelInput = this.OtherLevelInput,

                        BankInfos = this.BankInfos.Select(b => new BankInfo
                        {
                            BankNumber = b.BankNumber,
                            BaseBay = b.BaseBay,
                            BaseLevel = b.BaseLevel,
                            BaseValue = b.BaseValue,
                            HoistValue = b.HoistValue,
                            TurnValue = b.TurnValue,
                            ForkValue = b.ForkValue
                        }).ToList(),

                        DeadCells = this.DeadCells.ToList()
                    };

                    string json = JsonConvert.SerializeObject(preset, Formatting.Indented);
                    File.WriteAllText(sfd.FileName, json);
                    LastPresetPath = sfd.FileName;

                    MessageBox.Show("설정이 저장되었습니다.\n" + sfd.FileName, "저장 완료",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("설정 저장 중 오류가 발생했습니다.\n" + ex.Message, "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadPreset()
        {
            try
            {
                var ofd = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "설정 불러오기",
                    Filter = "Teaching Preset (*.json)|*.json",
                    Multiselect = false
                };

                if (ofd.ShowDialog() == true)
                {
                    string json = File.ReadAllText(ofd.FileName);
                    var preset = JsonConvert.DeserializeObject<TeachingPreset>(json);

                    if (preset == null)
                    {
                        MessageBox.Show("파일 형식이 올바르지 않습니다.", "불러오기 실패",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // 1) 사이즈/피치 값 적용
                    this.BasePitch = preset.BasePitch;
                    this.ProfilePitch = preset.ProfilePitch;
                    this.Gap = preset.Gap;

                    // 2) 총 개수 먼저 반영 (BankInfos 개수 재조정)
                    this.TotalBank = preset.TotalBank;
                    this.TotalBay = preset.TotalBay;
                    this.TotalLevel = preset.TotalLevel;

                    // 3) 문자열 입력 원문을 대입 → 내부 파서가 HoistPitches/ProfileBays/OtherLevels 갱신
                    this.HoistPitchInput = preset.HoistPitchInput ?? string.Empty;
                    this.ProfileBayInput = preset.ProfileBayInput ?? string.Empty;
                    this.OtherLevelInput = preset.OtherLevelInput ?? string.Empty;

                    // 4) 기준열 값들 반영
                    OverwriteBankInfosFromPreset(preset.BankInfos);

                    // 5) 데드셀 선택값 반영
                    DeadCells.Clear();
                    if (preset.DeadCells != null)
                    {
                        foreach (var dc in preset.DeadCells)
                        {
                            // (방어 로직) 범위 체크 후 추가
                            // CellIdentifier에 Bank/Bay/Level 속성이 있다고 가정
                            // 만약 속성명이 다르면 여기만 맞춰주면 됨.
                            var bank = dc.Bank;
                            var bay = dc.Bay;
                            var level = dc.Level;

                            if (bank >= 1 && bank <= TotalBank &&
                                bay >= 1 && bay <= TotalBay &&
                                level >= 1 && level <= TotalLevel)
                            {
                                // 새 인스턴스로 보강(값형/참조형 상관없이 안전)
                                var id = new CellIdentifier(bank, bay, level);
                                if (!DeadCells.Contains(id))
                                    DeadCells.Add(id);
                            }
                        }

                        // 맵이 생성되어 있다면 버튼 상태 즉시 반영
                        UpdateAllCellStates();
                    }

                    LastPresetPath = ofd.FileName;

                    MessageBox.Show("설정을 불러왔습니다.\n필요하다면 '맵 생성'을 다시 눌러 맵을 갱신하세요.",
                        "불러오기 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("설정 불러오기 중 오류가 발생했습니다.\n" + ex.Message, "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region === INotifyPropertyChanged ===
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        #endregion
    }
}
