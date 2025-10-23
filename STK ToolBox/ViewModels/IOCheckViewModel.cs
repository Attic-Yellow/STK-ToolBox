// ViewModels/IOCheckViewModel.cs (네 코드 교체/추가 부분만)
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using STK_ToolBox.Helpers;
using STK_ToolBox.Models;

namespace STK_ToolBox.ViewModels
{
    public class IOCheckViewModel : BaseViewModel
    {
        public ObservableCollection<IOMonitorItem> IOList { get; set; }
        public ICommand RefreshCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand ToggleOutputCommand { get; } // ★ 추가

        private readonly string DbPath = @"D:\LBS_DB\LBSControl.db3";
        private readonly int _stationNo = 0; // 현장 Station 번호

        public IOCheckViewModel()
        {
            IOList = new ObservableCollection<IOMonitorItem>();
            RefreshCommand = new RelayCommand(() => LoadIOStatus());
            SaveCommand = new RelayCommand(() => SaveStates());
            ToggleOutputCommand = new RelayCommand<IOMonitorItem>(ToggleOutput, (it) => it?.CanToggle == true);

            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(LoadIOStatus));
        }

        private void ToggleOutput(IOMonitorItem item)
        {
            if (item == null || !item.CanToggle) return;

            try
            {
                // DLL OPEN
                var opened = MdFunc32Wrapper.Open(_stationNo) == 0;

                var ok = false;
                if (opened)
                {
                    ok = MdFunc32Wrapper.TryWriteBit(_stationNo, item.Address, !item.CurrentState);
                    // 성공 시 즉시 재독
                    if (ok && MdFunc32Wrapper.TryReadBit(_stationNo, item.Address, out var on))
                        item.CurrentState = on;
                }

                try { MdFunc32Wrapper.Close(_stationNo); } catch { }

                if (!ok)
                    MessageBox.Show($"출력 토글 실패: {item.Address}", "I/O 출력", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"출력 오류: {ex.Message}", "I/O 출력", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadIOStatus()
        {
            IOList.Clear();

            try
            {
                if (!File.Exists(DbPath))
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        MessageBox.Show($"SQLite DB 파일을 찾을 수 없습니다:\n{DbPath}",
                            "DB 연결 오류", MessageBoxButton.OK, MessageBoxImage.Error)));
                    return;
                }

                using (var conn = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                {
                    conn.Open();

                    using (var cmd = new SQLiteCommand("SELECT * FROM IOMonitoring;", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        // DLL 오픈(한 번만)
                        var opened = MdFunc32Wrapper.Open(_stationNo) == 0;

                        while (reader.Read())
                        {
                            int Find(string a, params string[] b)
                            {
                                foreach (var name in new[] { a }.Concat(b))
                                {
                                    try { var i = reader.GetOrdinal(name); if (i >= 0) return i; } catch { }
                                }
                                return -1;
                            }
                            string Read(int ord) => (ord < 0 || reader.IsDBNull(ord)) ? "" : (reader.GetValue(ord)?.ToString() ?? "");

                            int idIdx = Find("ID", "Id", "io_id", "rowid");
                            int nameIdx = Find("IOName", "Name", "IoName");
                            int addrIdx = Find("Address", "IOAddress", "Addr");
                            int unitIdx = Find("Unit", "unit");
                            int detIdx = Find("DetailUnit", "detailunit", "detailUnit");
                            int dscIdx = Find("Description", "Desc", "Remark", "Remarks");

                            var address = Read(addrIdx);

                            bool on = false;
                            if (opened && !string.IsNullOrWhiteSpace(address))
                            {
                                try { MdFunc32Wrapper.TryReadBit(_stationNo, address, out on); }
                                catch { on = false; }
                            }

                            var item = new IOMonitorItem
                            {
                                Id = (idIdx >= 0 && !reader.IsDBNull(idIdx)) ? Convert.ToInt32(reader.GetValue(idIdx)) : 0,
                                IOName = Read(nameIdx),
                                Address = address,
                                Unit = Read(unitIdx),
                                DetailUnit = Read(detIdx),
                                Description = Read(dscIdx),
                                CurrentState = on
                            };

                            IOList.Add(item);
                        }

                        try { MdFunc32Wrapper.Close(_stationNo); } catch { }
                    }
                }

                // 저장된 체크 상태 복원
                var saved = JsonStorageHelper.Load<List<IOMonitorItem>>();
                foreach (var s in saved)
                {
                    var t = IOList.FirstOrDefault(x => x.Id == s.Id && x.Id != 0);
                    if (t != null) t.IsChecked = s.IsChecked;
                }
            }
            catch (SQLiteException ex)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    MessageBox.Show($"SQLite 오류: {ex.Message}", "SQLite 오류", MessageBoxButton.OK, MessageBoxImage.Error)));
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    MessageBox.Show($"예기치 못한 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error)));
            }
        }

        private void SaveStates()
        {
            try
            {
                JsonStorageHelper.Save(IOList.ToList());
                MessageBox.Show("현재 I/O 체크 상태가 저장되었습니다.", "저장 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"저장 중 오류: {ex.Message}", "저장 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
