using System.Windows;
using System.Diagnostics;
using VictusLounge.Data;

namespace VictusLounge;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            DatabaseInitializer.Initialize();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Database initialization failed: {ex}");
            // The UI can still open if the local SQL Server is not running.
            // Database-backed screens will fall back to the current in-memory values.
        }

        base.OnStartup(e);
    }
}
