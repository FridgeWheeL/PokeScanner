using System.Windows;
using System.Net.Http;

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

        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        var tcgdexApiService = new TcgdexApiService(httpClient);
        app.Run(new MainWindow(tcgdexApiService));
    }
}
