# Ollama Modeller Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Desktop WPF application for managing local Ollama models

**Architecture:** Single-window MVVM application with HttpClient communicating to Ollama REST API

**Tech Stack:** WPF + C# (.NET 8), HttpClient, System.Text.Json

---

## Project Structure

```
OllamaModeller/
├── OllamaModeller.csproj       # .NET 8 WPF project
├── App.xaml(.cs)               # Application entry, dark theme
├── MainWindow.xaml(.cs)        # Single panel UI
├── Models/
│   ├── OllamaModel.cs         # Installed model data
│   └── ModelStatistics.cs      # Running model stats
├── ViewModels/
│   ├── ViewModelBase.cs       # INotifyPropertyChanged base
│   ├── RelayCommand.cs        # ICommand implementation
│   └── MainViewModel.cs       # Main business logic
└── Services/
    └── OllamaApiService.cs    # HTTP client for Ollama API
```

---

## Task 1: Project Setup

**Files:**
- Create: `OllamaModeller.csproj`
- Create: `App.xaml`
- Create: `App.xaml.cs`

- [ ] **Step 1: Create .csproj file**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <UseWPF>true</UseWPF>
    <ApplicationIcon></ApplicationIcon>
    <Title>Ollama Modeller</Title>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Create App.xaml with dark theme resources**

```xml
<Application x:Class="OllamaModeller.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Application.Resources>
    <ResourceDictionary>
      <!-- Dark Theme Colors -->
      <SolidColorBrush x:Key="WindowBackground" Color="#1e1e1e"/>
      <SolidColorBrush x:Key="PanelBackground" Color="#252526"/>
      <SolidColorBrush x:Key="BorderBrush" Color="#3e3e42"/>
      <SolidColorBrush x:Key="TextPrimary" Color="#d4d4d4"/>
      <SolidColorBrush x:Key="TextSecondary" Color="#9e9e9e"/>
      <SolidColorBrush x:Key="AccentBlue" Color="#0e639c"/>
      <SolidColorBrush x:Key="AccentGreen" Color="#4CAF50"/>
      <SolidColorBrush x:Key="AccentRed" Color="#f44336"/>
      <SolidColorBrush x:Key="StatusBarBackground" Color="#007acc"/>
    </ResourceDictionary>
  </Application.Resources>
</Application>
```

- [ ] **Step 3: Create App.xaml.cs**

```csharp
using System.Windows;
namespace OllamaModeller;

public partial class App : Application { }
```

- [ ] **Step 4: Commit**

```bash
git add OllamaModeller.csproj App.xaml App.xaml.cs
git commit -m "feat: project setup with dark theme"
```

---

## Task 2: Models

**Files:**
- Create: `Models/OllamaModel.cs`
- Create: `Models/ModelStatistics.cs`

- [ ] **Step 1: Create Models directory and OllamaModel.cs**

```csharp
namespace OllamaModeller.Models;

public class OllamaModel
{
    public string Name { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public DateTime Modified { get; set; }
}
```

- [ ] **Step 2: Create ModelStatistics.cs**

```csharp
namespace OllamaModeller.Models;

public class ModelStatistics
{
    public string Name { get; set; } = string.Empty;
    public double CpuPercent { get; set; }
    public long MemoryBytes { get; set; }
    public DateTime StartedAt { get; set; }
}
```

- [ ] **Step 3: Commit**

```bash
git add Models/OllamaModel.cs Models/ModelStatistics.cs
git commit -m "feat: add data models"
```

---

## Task 3: ViewModel Infrastructure

**Files:**
- Create: `ViewModels/ViewModelBase.cs`
- Create: `ViewModels/RelayCommand.cs`

- [ ] **Step 1: Create ViewModelBase.cs**

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OllamaModeller.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
```

- [ ] **Step 2: Create RelayCommand.cs**

```csharp
using System.Windows.Input;

namespace OllamaModeller.ViewModels;

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute != null ? _ => canExecute() : null)
    {
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);
}
```

- [ ] **Step 3: Commit**

```bash
git add ViewModels/ViewModelBase.cs ViewModels/RelayCommand.cs
git commit -m "feat: add ViewModel infrastructure"
```

---

## Task 4: OllamaApiService

**Files:**
- Create: `Services/OllamaApiService.cs`

- [ ] **Step 1: Create OllamaApiService.cs**

```csharp
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using OllamaModeller.Models;

namespace OllamaModeller.Services;

public class OllamaApiService : IDisposable
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "http://localhost:11434";

    public OllamaApiService()
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(10) };
    }

    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/");
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<List<OllamaModel>> GetModelsAsync()
    {
        var response = await _httpClient.GetAsync("/api/tags");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var models = new List<OllamaModel>();
        foreach (var item in doc.RootElement.GetProperty("models").EnumerateArray())
        {
            models.Add(new OllamaModel
            {
                Name = item.GetProperty("name").GetString() ?? "",
                Size = FormatSize(item.GetProperty("size").GetInt64()),
                Modified = DateTime.TryParse(item.TryGetProperty("modified_at", out var mod) ? mod.GetString() : null, out var dt) ? dt : DateTime.MinValue
            });
        }
        return models;
    }

    public async Task<List<ModelStatistics>> GetRunningModelsAsync()
    {
        var response = await _httpClient.GetAsync("/api/ps");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var models = new List<ModelStatistics>();
        foreach (var item in doc.RootElement.GetProperty("models").EnumerateArray())
        {
            models.Add(new ModelStatistics
            {
                Name = item.GetProperty("name").GetString() ?? "",
                CpuPercent = item.GetProperty("cpu_percent").GetDouble(),
                MemoryBytes = item.GetProperty("memory").GetInt64(),
                StartedAt = DateTime.TryParse(item.TryGetProperty("started_at", out var start) ? start.GetString() : null, out var dt) ? dt : DateTime.MinValue
            });
        }
        return models;
    }

    public async Task PullModelAsync(string modelName, IProgress<double> progress, CancellationToken ct)
    {
        var content = new StringContent(JsonSerializer.Serialize(new { name = modelName }), System.Text.Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/api/pull", content, ct);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line != null && JsonDocument.TryParse(line, out var doc) && doc.RootElement.TryGetProperty("progress", out var prog))
            {
                progress.Report(prog.GetDouble());
            }
        }
    }

    public async Task StopModelAsync(string modelName)
    {
        var content = new StringContent(JsonSerializer.Serialize(new { name = modelName }), System.Text.Encoding.UTF8, "application/json");
        await _httpClient.PostAsync("/api/generate", content);
    }

    private static string FormatSize(long bytes)
    {
        double gb = bytes / 1_073_741_824.0;
        if (gb >= 1) return $"{gb:F1} GB";
        double mb = bytes / 1_048_576.0;
        return $"{mb:F0} MB";
    }

    public void Dispose() => _httpClient.Dispose();
}
```

- [ ] **Step 2: Commit**

```bash
git add Services/OllamaApiService.cs
git commit -m "feat: add Ollama API service"
```

---

## Task 5: MainViewModel

**Files:**
- Create: `ViewModels/MainViewModel.cs`

- [ ] **Step 1: Create MainViewModel.cs**

```csharp
using System.Collections.ObjectModel;
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
```

- [ ] **Step 2: Commit**

```bash
git add ViewModels/MainViewModel.cs
git commit -m "feat: add MainViewModel with all commands"
```

---

## Task 6: MainWindow UI

**Files:**
- Create: `MainWindow.xaml`
- Create: `MainWindow.xaml.cs`

- [ ] **Step 1: Create MainWindow.xaml**

```xml
<Window x:Class="OllamaModeller.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="clr-namespace:OllamaModeller.ViewModels"
        Title="Ollama Modeller" Height="600" Width="700"
        Background="{StaticResource WindowBackground}"
        WindowStartupLocation="CenterScreen">
  <Window.DataContext>
    <vm:MainViewModel/>
  </Window.DataContext>

  <Grid>
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="*"/>
      <RowDefinition Height="Auto"/>
    </Grid.RowDefinitions>

    <!-- Header -->
    <Border Grid.Row="0" Background="#2d2d30" Padding="16,12">
      <Grid>
        <TextBlock Text="Ollama Modeller" FontSize="18" FontWeight="SemiBold" Foreground="White" VerticalAlignment="Center"/>
        <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" VerticalAlignment="Center">
          <Ellipse Width="10" Height="10" Margin="0,0,8,0">
            <Ellipse.Style>
              <Style TargetType="Ellipse">
                <Setter Property="Fill" Value="#f44336"/>
                <Style.Triggers>
                  <DataTrigger Binding="{Binding IsConnected}" Value="True">
                    <Setter Property="Fill" Value="#4CAF50"/>
                  </DataTrigger>
                </Style.Triggers>
              </Style>
            </Ellipse.Style>
          </Ellipse>
          <TextBlock VerticalAlignment="Center">
            <TextBlock.Style>
              <Style TargetType="TextBlock">
                <Setter Property="Text" Value="Disconnected"/>
                <Setter Property="Foreground" Value="#9e9e9e"/>
                <Style.Triggers>
                  <DataTrigger Binding="{Binding IsConnected}" Value="True">
                    <Setter Property="Text" Value="Connected"/>
                    <Setter Property="Foreground" Value="#9e9e9e"/>
                  </DataTrigger>
                </Style.Triggers>
              </Style>
            </TextBlock.Style>
          </TextBlock>
        </StackPanel>
      </Grid>
    </Border>

    <!-- Installed Models -->
    <Border Grid.Row="1" Padding="16" BorderBrush="{StaticResource BorderBrush}" BorderThickness="0,0,0,1">
      <Grid>
        <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="Auto"/></Grid.RowDefinitions>
        <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
        <TextBlock Text="Installed Models" FontSize="14" Foreground="{StaticResource TextPrimary}" Margin="0,0,0,12"/>
        <Button Grid.Column="1" Content="↻ Refresh" Command="{Binding RefreshCommand}"
                Background="{StaticResource AccentBlue}" Foreground="White" Padding="12,6" BorderThickness="0"/>
        <DataGrid Grid.Row="1" Grid.ColumnSpan="2" ItemsSource="{Binding Models}" AutoGenerateColumns="False"
                  Background="{StaticResource PanelBackground}" RowBackground="{StaticResource PanelBackground}"
                  AlternatingRowBackground="#1e1e1e" BorderThickness="1" BorderBrush="{StaticResource BorderBrush}"
                  HeadersVisibility="Column" GridLinesVisibility="None" IsReadOnly="True" CanUserAddRows="False" CanUserDeleteRows="False">
          <DataGrid.Columns>
            <DataGridTextColumn Header="NAME" Binding="{Binding Name}" Width="*" Foreground="{StaticResource TextPrimary}"/>
            <DataGridTextColumn Header="SIZE" Binding="{Binding Size}" Width="100" Foreground="{StaticResource TextSecondary}"/>
            <DataGridTemplateColumn Header="ACTIONS" Width="150">
              <DataGridTemplateColumn.CellTemplate>
                <DataTemplate>
                  <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                    <Button Content="▶ Run" Background="{StaticResource AccentGreen}" Foreground="White" Padding="8,4" Margin="0,0,4,0" BorderThickness="0"/>
                    <Button Content="✕ Delete" Background="{StaticResource AccentRed}" Foreground="White" Padding="8,4" BorderThickness="0"/>
                  </StackPanel>
                </DataTemplate>
              </DataGridTemplateColumn.CellTemplate>
            </DataGridTemplateColumn>
          </DataGrid.Columns>
        </DataGrid>
      </Grid>
    </Border>

    <!-- Download Section -->
    <Border Grid.Row="2" Padding="16" BorderBrush="{StaticResource BorderBrush}" BorderThickness="0,0,0,1">
      <Grid>
        <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="Auto"/><RowDefinition Height="Auto"/></Grid.RowDefinitions>
        <TextBlock Text="Download Model" FontSize="14" Foreground="{StaticResource TextPrimary}" Margin="0,0,0,12"/>
        <Grid Grid.Row="1">
          <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
          <TextBox Text="{Binding PullModelName, UpdateSourceTrigger=PropertyChanged}" 
                   Padding="8" FontSize="13" Background="#1e1e1e" Foreground="{StaticResource TextPrimary}" BorderBrush="{StaticResource BorderBrush}"/>
          <Button Grid.Column="1" Content="⬇ Pull" Command="{Binding PullCommand}"
                  Background="{StaticResource AccentBlue}" Foreground="White" Padding="16,8" Margin="8,0,0,0" BorderThickness="0"/>
        </Grid>
        <Grid Grid.Row="2" Margin="0,12,0,0" Visibility="{Binding IsPulling, Converter={StaticResource BoolToVis}}">
          <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
          <ProgressBar Value="{Binding DownloadProgress}" Maximum="100" Height="6" Background="#3e3e42" Foreground="{StaticResource AccentBlue}"/>
          <TextBlock Grid.Column="1" Text="{Binding DownloadProgress, StringFormat={}{0:F0}%}" Foreground="{StaticResource TextSecondary}" Margin="8,0"/>
          <Button Grid.Column="2" Content="✕ Cancel" Command="{Binding CancelPullCommand}"
                  Background="{StaticResource AccentRed}" Foreground="White" Padding="8,4" BorderThickness="0"/>
        </Grid>
      </Grid>
    </Border>

    <!-- Running Models -->
    <Border Grid.Row="3" Padding="16" BorderBrush="{StaticResource BorderBrush}" BorderThickness="0,0,0,1">
      <Grid>
        <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="Auto"/></Grid.RowDefinitions>
        <TextBlock Text="Running Models" FontSize="14" Foreground="{StaticResource TextPrimary}" Margin="0,0,0,12"/>
        <DataGrid Grid.Row="1" ItemsSource="{Binding RunningModels}" AutoGenerateColumns="False"
                  Background="{StaticResource PanelBackground}" RowBackground="{StaticResource PanelBackground}"
                  AlternatingRowBackground="#1e1e1e" BorderThickness="1" BorderBrush="{StaticResource BorderBrush}"
                  HeadersVisibility="Column" GridLinesVisibility="None" IsReadOnly="True" CanUserAddRows="False">
          <DataGrid.Columns>
            <DataGridTextColumn Header="MODEL" Binding="{Binding Name}" Width="*" Foreground="#4ec9b0"/>
            <DataGridTextColumn Header="CPU" Binding="{Binding CpuPercent, StringFormat={}{0:F0}%}" Width="100" Foreground="{StaticResource TextSecondary}"/>
            <DataGridTemplateColumn Header="ACTIONS" Width="100">
              <DataGridTemplateColumn.CellTemplate>
                <DataTemplate>
                  <Button Content="■ Stop" Command="{Binding DataContext.StopCommand, RelativeSource={RelativeSource AncestorType=Window}}"
                          CommandParameter="{Binding Name}" Background="{StaticResource AccentRed}" Foreground="White" Padding="8,4" BorderThickness="0" HorizontalAlignment="Right"/>
                </DataTemplate>
              </DataGridTemplateColumn.CellTemplate>
            </DataGridTemplateColumn>
          </DataGrid.Columns>
        </DataGrid>
      </Grid>
    </Border>

    <!-- Status Bar -->
    <Border Grid.Row="5" Background="{StaticResource StatusBarBackground}" Padding="16,10">
      <TextBlock Text="{Binding StatusMessage}" Foreground="White" FontSize="13"/>
    </Border>
  </Grid>
</Window>
```

- [ ] **Step 2: Create MainWindow.xaml.cs**

```csharp
using System.Windows;

namespace OllamaModeller;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();
}
```

- [ ] **Step 3: Add BoolToVisibilityConverter to App.xaml**

Update App.xaml Resources:
```xml
<Application x:Class="OllamaModeller.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="clr-namespace:OllamaModeller"
             StartupUri="MainWindow.xaml">
  <Application.Resources>
    <ResourceDictionary>
      <!-- Add converter -->
      <BooleanToVisibilityConverter x:Key="BoolToVis"/>
      <!-- ... existing colors ... -->
    </ResourceDictionary>
  </Application.Resources>
</Application>
```

- [ ] **Step 4: Commit**

```bash
git add MainWindow.xaml MainWindow.xaml.cs App.xaml
git commit -m "feat: add MainWindow UI with all sections"
```

---

## Task 7: Build and Test

- [ ] **Step 1: Verify project builds**

Run: `dotnet build`
Expected: Build succeeded with 0 warnings

- [ ] **Step 2: Run application**

Run: `dotnet run`
Expected: Application launches with dark themed window

---

## Verification Checklist

- [ ] Ollama Modeller window opens
- [ ] Connection status shows (green/red based on Ollama availability)
- [ ] Models list displays installed models
- [ ] Download section shows input field and Pull button
- [ ] Progress bar appears when downloading
- [ ] Running models section shows active models
- [ ] Status bar displays messages
- [ ] Refresh button reloads data
- [ ] Stop button stops running model
