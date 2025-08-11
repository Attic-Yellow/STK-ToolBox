﻿using MahApps.Metro.Controls;
using System.Windows;
using System.Windows.Controls;
using STK_ToolBox.View;

namespace STK_ToolBox
{
    public partial class MainWindow : MetroWindow
    {
        private TeachingPageView _teachingPageView;
        private ParameterPageView _parameterView;
        private DevicePageView _devicePageView;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void MenuTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MenuTabControl.SelectedItem == TeachingTab)
            {
                ShowTeachingTab();
            }
            else if (MenuTabControl.SelectedItem == ParameterTab)
            {
                ShowParameterTab();
            }
            else if (MenuTabControl.SelectedItem == DeviceTab)
            {
                ShowDeviceTab();
            }
        }

        private void ShowTeachingTab()
        {
            if (_teachingPageView == null)
                _teachingPageView = new TeachingPageView();

            TeachingContent.Content = _teachingPageView;
            TeachingContent.Visibility = Visibility.Visible;

            ParameterContent.Content = null;
            ParameterContent.Visibility = Visibility.Collapsed;
            DeviceContent.Content = null;
            DeviceContent.Visibility = Visibility.Collapsed;
        }

        private void ShowParameterTab()
        {
            if (_parameterView == null)
                _parameterView = new ParameterPageView();

            ParameterContent.Content = _parameterView;
            ParameterContent.Visibility = Visibility.Visible;
            TeachingContent.Content = null;
            TeachingContent.Visibility = Visibility.Collapsed;
            DeviceContent.Content = null;
            DeviceContent.Visibility = Visibility.Collapsed;
        }

        private void ShowDeviceTab()
        {
            if (_devicePageView == null)
                _devicePageView = new DevicePageView();

            DeviceContent.Content = _devicePageView;
            DeviceContent.Visibility = Visibility.Visible;
            ParameterContent.Content = null;
            ParameterContent.Visibility = Visibility.Collapsed;
            TeachingContent.Content = null;
            TeachingContent.Visibility = Visibility.Collapsed;
        }

        private void TabItem_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var tab = sender as TabItem;
            if (tab == TeachingTab)
            {
                MenuTabControl.SelectedItem = TeachingTab;
                ShowTeachingTab();
            }
            else if (tab == ParameterTab)
            {
                MenuTabControl.SelectedItem = ParameterTab;
                ShowParameterTab();
            }
            else if (tab == DeviceTab)
            {
                MenuTabControl.SelectedItem = DeviceTab;
                ShowDeviceTab();
            }
        }
    }
}
