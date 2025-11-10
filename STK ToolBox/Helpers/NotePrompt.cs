using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace STK_ToolBox.Helpers
{
    internal static class NotePrompt
    {
        public static string Show(string title, string message, string defaultText)
        {
            // Window
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

            // 레이아웃
            var grid = new Grid { Margin = new Thickness(14) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var lbl = new TextBlock { Text = message, Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap };
            Grid.SetRow(lbl, 0);

            var tb = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MinHeight = 120,
                Text = defaultText
            };
            Grid.SetRow(tb, 1);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
            var ok = new Button { Content = "저장", MinWidth = 80, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            var cancel = new Button { Content = "취소", MinWidth = 80, IsCancel = true };
            btnPanel.Children.Add(ok);
            btnPanel.Children.Add(cancel);
            Grid.SetRow(btnPanel, 2);

            grid.Children.Add(lbl);
            grid.Children.Add(tb);
            grid.Children.Add(btnPanel);
            win.Content = grid;

            string result = null;
            ok.Click += (s, e) => { result = tb.Text; win.DialogResult = true; };
            // cancel 버튼은 IsCancel=true 로 닫힘

            // 소유자(메인윈도)가 있으면 가운데 정렬
            if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsVisible)
                win.Owner = Application.Current.MainWindow;

            win.ShowDialog();
            return result; // 취소면 null
        }
    }
}
