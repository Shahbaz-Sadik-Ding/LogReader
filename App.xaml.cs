using System.IO;
using System.Linq;
using System.Windows;
using LogReader.Views;

namespace LogReader;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var window = new MainWindow();
        window.Show();

        // If files were passed on the command line (e.g. the `watch` helper:
        //   LogReader.exe "C:\Logs\app1.log" "C:\Logs\app2.log"), open those.
        // Otherwise reopen the tabs from the last session.
        var files = e.Args.Where(File.Exists).ToArray();
        if (files.Length > 0)
            window.OpenFiles(files);
        else
            window.RestoreSession();
    }
}
