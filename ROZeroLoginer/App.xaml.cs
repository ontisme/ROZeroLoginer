using System.Windows;

namespace ROZeroLoginer
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // 確保只有一個實例運行
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            var processes = System.Diagnostics.Process.GetProcessesByName(currentProcess.ProcessName);
            
            if (processes.Length > 1)
            {
                MessageBox.Show("應用程式已在運行中！", "ROZeroLoginer", MessageBoxButton.OK, MessageBoxImage.Information);
                Current.Shutdown();
                return;
            }
        }
    }
}