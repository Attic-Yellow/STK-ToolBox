using System;
using System.Windows;
using System.Windows.Controls;

namespace STK_ToolBox.Helpers
{
    /// <summary>
    /// 간단한 메모 입력용 팝업 창을 띄워
    /// 사용자에게 문자열을 입력받는 헬퍼 클래스.
    /// 
    /// - IOCheck 화면에서 선택한 IO에 대한 메모 작성/수정 시 사용.
    /// - TextBox에 여러 줄 입력 가능(Enter 허용).
    /// - [저장] → 입력된 문자열 반환
    /// - [취소] 또는 창 닫기 → null 반환
    /// </summary>
    internal static class NotePrompt
    {
        #region ───────── Public API ─────────

        /// <summary>
        /// 메모 입력 다이얼로그를 표시하고 결과 문자열을 반환한다.
        /// </summary>
        /// <param name="title">창 제목</param>
        /// <param name="message">상단 안내 문구</param>
        /// <param name="defaultText">기본 메모 내용 (기존 메모 등)</param>
        /// <returns>
        /// [저장] 클릭 시 입력된 문자열,
        /// [취소]/닫기 시 null 반환
        /// </returns>
        public static string Show(string title, string message, string defaultText)
        {
            // ── Window 생성 ───────────────────────────────────────────────
            var win = new Window
            {
                Title = title,
                Width = 520,
                Height = 260,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.CanResizeWithGrip,
                ShowInTaskbar = false,
                Topmost = false,
                Content = null
            };

            // ── 레이아웃(Grid) 구성 ──────────────────────────────────────
            var grid = new Grid { Margin = new Thickness(14) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                    // 안내 텍스트
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // TextBox
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                    // 버튼 영역

            // 상단 안내 문구
            var lbl = new TextBlock
            {
                Text = message,
                Margin = new Thickness(0, 0, 0, 8),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(lbl, 0);

            // 메모 입력 TextBox
            var tb = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MinHeight = 120,
                Text = defaultText
            };
            Grid.SetRow(tb, 1);

            // 버튼 패널 (저장/취소)
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 8, 0, 0)
            };

            var ok = new Button
            {
                Content = "저장",
                MinWidth = 80,
                Margin = new Thickness(0, 0, 8, 0),
                IsDefault = true
            };

            var cancel = new Button
            {
                Content = "취소",
                MinWidth = 80,
                IsCancel = true
            };

            btnPanel.Children.Add(ok);
            btnPanel.Children.Add(cancel);
            Grid.SetRow(btnPanel, 2);

            // Grid 조립
            grid.Children.Add(lbl);
            grid.Children.Add(tb);
            grid.Children.Add(btnPanel);
            win.Content = grid;

            // ── 버튼 이벤트 & 결과 처리 ─────────────────────────────────
            string result = null;

            ok.Click += (s, e) =>
            {
                result = tb.Text;
                win.DialogResult = true;   // 다이얼로그 닫기
            };
            // cancel 버튼은 IsCancel=true 로 자동 닫힘 (DialogResult=false)

            // ── 메인 윈도우 기준 중앙 정렬 ───────────────────────────────
            if (Application.Current?.MainWindow != null &&
                Application.Current.MainWindow.IsVisible)
            {
                win.Owner = Application.Current.MainWindow;
            }

            // 모달 다이얼로그로 표시
            win.ShowDialog();

            // 취소 또는 예외 상황일 경우 result는 null
            return result;
        }

        #endregion
    }
}
