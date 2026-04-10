using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using OllamaModeller.Models;
using OllamaModeller.Services;

namespace OllamaModeller.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly OllamaApiService _apiService;
    private CancellationTokenSource? _pullCts;

    public MainViewModel()
    {
        _apiService = new OllamaApiService();
        Models = new ObservableCollection<OllamaModel>();
        RunningModels = new ObservableCollection<ModelStatistics>();

        RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
        PullCommand = new RelayCommand(async _ => await PullModelAsync(), _ => !string.IsNullOrWhiteSpace(PullModelName) && !IsPulling);
        CancelPullCommand = new RelayCommand(_ => CancelPull(), _ => IsPulling);
        StopCommand = new RelayCommand(async o => await StopModelAsync(o as string));

        _ = InitializeAsync();
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
    public ICommand StopCommand { get; }

    private async Task RefreshAsync()
    {
        try
        {
            Models.Clear();
            foreach (var m in await _apiService.GetModelsAsync()) Models.Add(m);
            RunningModels.Clear();
            foreach (var m in await _apiService.GetRunningModelsAsync()) RunningModels.Add(m);
            StatusMessage = IsConnected ? "✓ Connection OK" : "Ready";
        }
        catch (Exception ex) { StatusMessage = $"⚠ {ex.Message}"; }
    }

    private async Task PullModelAsync()
    {
        if (string.IsNullOrWhiteSpace(PullModelName)) return;
        IsPulling = true;
        DownloadProgress = 0;
        _pullCts = new CancellationTokenSource();
        try
        {
            await _apiService.PullModelAsync(PullModelName, new Progress<double>(p => DownloadProgress = p * 100), _pullCts.Token);
            StatusMessage = $"✓ Downloaded {PullModelName}";
            await RefreshAsync();
        }
        catch (OperationCanceledException) { StatusMessage = "Download cancelled"; }
        catch (Exception ex) { StatusMessage = $"⚠ {ex.Message}"; }
        finally { IsPulling = false; DownloadProgress = 0; _pullCts?.Dispose(); _pullCts = null; }
    }

    private void CancelPull() => _pullCts?.Cancel();

    private async Task StopModelAsync(string? modelName)
    {
        if (string.IsNullOrEmpty(modelName)) return;
        try
        {
            await _apiService.StopModelAsync(modelName);
            StatusMessage = $"✓ Stopped {modelName}";
            await RefreshAsync();
        }
        catch (Exception ex) { StatusMessage = $"⚠ {ex.Message}"; }
    }
}
