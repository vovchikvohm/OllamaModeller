using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using OllamaModeller.Models;
using OllamaModeller.Services;

namespace OllamaModeller.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly OllamaApiService _apiService;
    private readonly string _logFile;
    private CancellationTokenSource? _pullCts;
    private readonly DispatcherTimer _timer;

    public MainViewModel()
    {
        _apiService = new OllamaApiService();
        var exeDir = AppDomain.CurrentDomain.BaseDirectory;
        _logFile = Path.Combine(exeDir, "ollama_debug.log");

        Models = new ObservableCollection<OllamaModel>();
        RunningModels = new ObservableCollection<ModelStatistics>();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => UpdateRemainingTimes();
        _timer.Start();

        RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
        PullCommand = new RelayCommand(async _ => await PullModelAsync(), _ => !string.IsNullOrWhiteSpace(PullModelName) && !IsPulling);
        CancelPullCommand = new RelayCommand(_ => CancelPull(), _ => IsPulling);
        RunCommand = new RelayCommand(async o => await RunModelAsync(o as string));
        StopCommand = new RelayCommand(async o => await StopModelAsync(o as string));
        DeleteCommand = new RelayCommand(async o => await DeleteModelAsync(o as string));

        Log("App started");
        _ = InitializeAsync();
    }

    private void Log(string message)
    {
        try { File.AppendAllText(_logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n"); }
        catch { }
    }

    private async Task InitializeAsync()
    {
        IsConnected = await _apiService.CheckHealthAsync();
        if (IsConnected) await RefreshAsync();
        else StatusMessage = "⚠ Cannot connect to Ollama. Make sure it's running.";
    }

    public ObservableCollection<OllamaModel> Models { get; }
    public ObservableCollection<ModelStatistics> RunningModels { get; }

    private bool _isConnected;
    public bool IsConnected { get => _isConnected; set => SetProperty(ref _isConnected, value); }

    private string _statusMessage = "Ready";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    private string _pullModelName = "";
    public string PullModelName { get => _pullModelName; set => SetProperty(ref _pullModelName, value); }

    private double _downloadProgress;
    public double DownloadProgress { get => _downloadProgress; set => SetProperty(ref _downloadProgress, value); }

    private bool _isPulling;
    public bool IsPulling { get => _isPulling; set => SetProperty(ref _isPulling, value); }

    public ICommand RefreshCommand { get; }
    public ICommand PullCommand { get; }
    public ICommand CancelPullCommand { get; }
    public ICommand RunCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand DeleteCommand { get; }

    private async Task RefreshAsync()
    {
        Log("Refresh clicked");
        try
        {
            Models.Clear();
            foreach (var m in await _apiService.GetModelsAsync()) Models.Add(m);
            RunningModels.Clear();
            foreach (var m in await _apiService.GetRunningModelsAsync()) RunningModels.Add(m);
            StatusMessage = IsConnected ? "✓ Connection OK" : "Ready";
            Log("Refresh completed");
        }
        catch (Exception ex) { StatusMessage = $"⚠ {ex.Message}"; Log($"Refresh error: {ex.Message}"); }
    }

    private async Task PullModelAsync()
    {
        Log($"Pull clicked: {PullModelName}");
        if (string.IsNullOrWhiteSpace(PullModelName)) return;
        IsPulling = true;
        DownloadProgress = 0;
        _pullCts = new CancellationTokenSource();
        try
        {
            await _apiService.PullModelAsync(PullModelName, new Progress<double>(p => DownloadProgress = p * 100), _pullCts.Token);
            StatusMessage = $"✓ Downloaded {PullModelName}";
            Log($"Pull completed: {PullModelName}");
            await RefreshAsync();
        }
        catch (OperationCanceledException) { StatusMessage = "Download cancelled"; Log("Pull cancelled"); }
        catch (Exception ex) { StatusMessage = $"⚠ {ex.Message}"; Log($"Pull error: {ex.Message}"); }
        finally { IsPulling = false; DownloadProgress = 0; _pullCts?.Dispose(); _pullCts = null; }
    }

    private void CancelPull() => _pullCts?.Cancel();

    private async Task RunModelAsync(string? modelName)
    {
        Log($"Run clicked: {modelName}");
        if (string.IsNullOrEmpty(modelName)) return;
        try
        {
            StatusMessage = $"▶ Running {modelName}...";
            await _apiService.RunModelAsync(modelName, "Hello");
            StatusMessage = $"✓ {modelName} is now running";
            Log($"Run completed: {modelName}");
            await RefreshAsync();
        }
        catch (Exception ex) { StatusMessage = $"⚠ {ex.Message}"; Log($"Run error: {ex.Message}"); }
    }

    private async Task StopModelAsync(string? modelName)
    {
        Log($"Stop clicked: {modelName}");
        if (string.IsNullOrEmpty(modelName)) return;
        try
        {
            await _apiService.StopModelAsync(modelName);
            StatusMessage = $"✓ Stopped {modelName}";
            Log($"Stop completed: {modelName}");
            await RefreshAsync();
        }
        catch (Exception ex) { StatusMessage = $"⚠ {ex.Message}"; Log($"Stop error: {ex.Message}"); }
    }

    private async Task DeleteModelAsync(string? modelName)
    {
        Log($"Delete clicked: {modelName}");
        if (string.IsNullOrEmpty(modelName)) return;
        try
        {
            await _apiService.DeleteModelAsync(modelName);
            StatusMessage = $"✓ Deleted {modelName}";
            Log($"Delete completed: {modelName}");
            await RefreshAsync();
        }
        catch (Exception ex) { StatusMessage = $"⚠ {ex.Message}"; Log($"Delete error: {ex.Message}"); }
    }

    private void UpdateRemainingTimes()
    {
        foreach (var model in RunningModels)
        {
            model.RemainingTime = OllamaApiService.GetRemainingTime(model.Until);
        }
    }
}
