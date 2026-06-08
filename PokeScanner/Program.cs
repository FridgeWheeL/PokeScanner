using System.Windows;

namespace PokeScanner;

public class Program
{
    [STAThread]
    public static void Main()
    {
        Application app = new();
        app.DispatcherUnhandledException += (s, e) =>
        {
            MessageBox.Show(e.Exception.ToString(), "Fatal Error");
            e.Handled = true;
        };
        app.Run(new MainWindow());
    }
}
