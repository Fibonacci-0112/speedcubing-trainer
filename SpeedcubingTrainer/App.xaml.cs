using System.Diagnostics.CodeAnalysis;
using SpeedcubingTrainer.Services;
using SpeedcubingTrainer.ViewModels;
using Uno.Resizetizer;

namespace SpeedcubingTrainer;

public partial class App : Application
{
    private static IServiceProvider? _services;

    /// <summary>Application-wide services, available once the host is built in OnLaunched.</summary>
    public static IServiceProvider Services => _services ?? throw new InvalidOperationException("The application host has not been built yet.");

    /// <summary>
    /// Initializes the singleton application object. This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();
    }

    protected Window? MainWindow { get; private set; }

    /// <summary>The main window, for platform APIs that must be initialised with a window handle.</summary>
    public static Window? MainWindowInstance { get; private set; }
    protected IHost? Host { get; private set; }

    [SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Uno.Extensions APIs are used in a way that is safe for trimming in this template context.")]
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var builder = this.CreateBuilder(args)
            .Configure(host => host
#if DEBUG
                // Switch to Development environment when running in DEBUG
                .UseEnvironment(Environments.Development)
#endif
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<AppDataService>();
                    services.AddSingleton<RepositoryProvider>();
                    services.AddSingleton<SettingsService>();
                    services.AddSingleton<ScrambleService>();
                    services.AddSingleton<TimerViewModel>();
                    services.AddSingleton<SessionsViewModel>();
                    services.AddSingleton<SettingsViewModel>();
                    services.AddSingleton<AlgorithmsViewModel>();
                    services.AddSingleton<TrainerViewModel>();
                })
            );
        MainWindow = builder.Window;
        MainWindowInstance = MainWindow;

        #if DEBUG
        MainWindow.UseStudio();
#endif
                MainWindow.SetWindowIcon();

        Host = builder.Build();
        _services = Host.Services;

        // Do not repeat app initialization when the Window already has content,
        // just ensure that the window is active
        if (MainWindow.Content is not Frame rootFrame)
        {
            // Create a Frame to act as the navigation context and navigate to the first page
            rootFrame = new Frame();

            // Place the frame in the current Window
            MainWindow.Content = rootFrame;
        }

        if (rootFrame.Content == null)
        {
            // When the navigation stack isn't restored navigate to the first page,
            // configuring the new page by passing required information as a navigation
            // parameter
            rootFrame.Navigate(typeof(Shell), args.Arguments);
        }
        // Ensure the current window is active
        MainWindow.Activate();
    }
}
