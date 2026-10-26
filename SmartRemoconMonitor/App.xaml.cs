using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using SmartRemoconMonitor.Configuration;
using SmartRemoconMonitor.Services.Export;
using SmartRemoconMonitor.Services.Mqtt;
using SmartRemoconMonitor.Services.Navigation;
using SmartRemoconMonitor.Services.Storage;
using SmartRemoconMonitor.Services.Ui;
using SmartRemoconMonitor.ViewModels;

namespace SmartRemoconMonitor;

/// <summary>
/// アプリケーションの起動、終了。サービスの登録。
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// 終了時にホストの停止を待つ時間の上限
    /// </summary>
    private static readonly TimeSpan HostStopTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 終了処理全体（停止と破棄）を待つ時間の上限
    /// </summary>
    private static readonly TimeSpan ShutdownWaitLimit = TimeSpan.FromSeconds(10);

    /// <summary>
    /// ロガー。ホストを組み立てる前と破棄した後にも使うため、NLogから直接取得する
    /// </summary>
    private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 二重起動を防ぐためのMutex
    /// </summary>
    private static Mutex? _instanceLock;

    /// <summary>
    /// サービス管理ホスト
    /// </summary>
    private IHost? _host;

    /// <summary>
    /// 二重起動を確認し、ホストを起動してメインウィンドウを表示する。
    /// </summary>
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instanceLock = new Mutex(initiallyOwned: true, $"Local\\{AppPaths.ProductName}", out var isOnlyInstance);
        if (!isOnlyInstance)
        {
            ShowMessage($"{AppPaths.ProductName}はすでに起動しています。", MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            _host = BuildHost(e.Args);
            await _host.StartAsync().ConfigureAwait(true);

            var shell = _host.Services.GetRequiredService<Views.MainWindow>();
            MainWindow = shell;
            shell.Show();

            await _host.Services.GetRequiredService<MainWindowViewModel>()
                .InitializeAsync()
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application failed to start.");
            ShowMessage($"起動に失敗しました。\n\n{ex.Message}", MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    /// <summary>
    /// ホストの停止と破棄をスレッドプールで行い、上限時間まで待つ。最後にMutexとログを片付ける。
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            var host = _host;
            _host = null;

            if (host is not null)
            {
                var shutdown = Task.Run(() => StopHostAsync(host));
                if (!shutdown.Wait(ShutdownWaitLimit))
                {
                    Log.Warn("Shutdown did not finish within {Limit}.", ShutdownWaitLimit);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during shutdown.");
        }
        finally
        {
            _instanceLock?.Dispose();
            NLog.LogManager.Shutdown();
            base.OnExit(e);
        }
    }

    /// <summary>
    /// ホストを停止して破棄する。
    /// </summary>
    private static async Task StopHostAsync(IHost host)
    {
        await host.StopAsync(HostStopTimeout).ConfigureAwait(false);

        if (host is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            host.Dispose();
        }
    }

    /// <summary>
    /// 設定の読み込みと検証、ログとサービス登録、ホストの組み立て。
    /// </summary>
    private static IHost BuildHost(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
            DisableDefaults = true,
        });

        builder.Logging.ClearProviders();
        builder.Logging.AddNLog();

        AddValidatedOptions<MqttOptions>(builder, MqttOptions.SectionName);
        AddValidatedOptions<AcOptions>(builder, AcOptions.SectionName);
        AddValidatedOptions<StorageOptions>(builder, StorageOptions.SectionName);

        builder.Services.AddSingleton<IFileDialogService, FileDialogService>();
        builder.Services.AddSingleton<IUiDispatcher>(_ => new UiDispatcher(Current.Dispatcher));

        AddHostedSingleton<IMqttService, MqttService>(builder.Services);
        AddHostedSingleton<ISensorDataStore, SensorDataStore>(builder.Services);

        builder.Services.AddHostedService<SensorDataRecorder>();

        builder.Services.AddSingleton<ISensorDataExportService, SensorDataExportService>();

        builder.Services.AddSingleton<NavigationHost>();
        builder.Services.AddSingleton<MainWindowViewModel>();
        builder.Services.AddSingleton<Views.MainWindow>();

        return builder.Build();
    }

    /// <summary>
    /// 設定のセクションを型に結び付け、起動時に検証するよう登録する。
    /// </summary>
    private static void AddValidatedOptions<TOptions>(HostApplicationBuilder builder, string sectionName)
        where TOptions : class
    {
        builder.Services
            .AddOptions<TOptions>()
            .Bind(builder.Configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }

    /// <summary>
    /// 1つのインスタンスを、具体的なクラス、インターフェース、バックグラウンドで動くサービスの3つとして登録する。
    /// </summary>
    private static void AddHostedSingleton<TService, TImplementation>(IServiceCollection services)
        where TService : class
        where TImplementation : class, TService, IHostedService
    {
        services.AddSingleton<TImplementation>();
        services.AddSingleton<TService>(sp => sp.GetRequiredService<TImplementation>());
        services.AddHostedService(sp => sp.GetRequiredService<TImplementation>());
    }

    /// <summary>
    /// アプリ名を表題にして、OKボタンだけのメッセージボックスを出す。
    /// </summary>
    private static void ShowMessage(string text, MessageBoxImage icon)
        => MessageBox.Show(text, AppPaths.ProductName, MessageBoxButton.OK, icon);

    /// <summary>
    /// UIスレッドで捕まらなかった例外をログに出し、メッセージボックスを出してアプリを続行する。
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled exception on the UI thread.");
        ShowMessage($"予期しないエラーが発生しました。\n\n{e.Exception.Message}", MessageBoxImage.Error);
        e.Handled = true;
    }

    /// <summary>
    /// UIスレッド以外で捕まらなかった例外をログに出す。
    /// </summary>
    private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        => Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception. Terminating={Terminating}", e.IsTerminating);

    /// <summary>
    /// 待たれなかったTaskの例外をログに出し、処理済みにする。
    /// </summary>
    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unobserved task exception.");
        e.SetObserved();
    }
}
