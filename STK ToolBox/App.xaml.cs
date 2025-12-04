using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;


namespace STK_ToolBox
{
    /// <summary>
    /// App.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class App : Application
    {
        private static Mutex _appMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            bool createdNew;

            // 다른 프로그램과 절대 겹치지 않을 고유 이름으로!
            _appMutex = new Mutex(true, "STK_ToolBox_SingleInstance_Mutex", out createdNew);

            if (!createdNew)
            {
                // 이미 실행 중
                MessageBox.Show(
                    "STK ToolBox가 이미 실행 중입니다.",
                    "중복 실행 방지",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // 두 번째로 켠 프로세스는 바로 종료
                Shutdown();
                return;
            }

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_appMutex != null)
            {
                _appMutex.ReleaseMutex();
                _appMutex.Dispose();
                _appMutex = null;
            }

            base.OnExit(e);
        }
    }
}
