using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OllamaModeller.Models;

public class ModelStatistics : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private string _remainingTime = "";
    public string RemainingTime
    {
        get => _remainingTime;
        set { _remainingTime = value; OnPropertyChanged(); }
    }

    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Size { get; set; } = string.Empty;
    public double CpuPercent { get; set; }
    public double GpuPercent { get; set; }
    public string GpuInfo { get; set; } = string.Empty;
    public long MemoryBytes { get; set; }
    public string Memory { get; set; } = string.Empty;
    public long Context { get; set; }
    public string ContextFormatted { get; set; } = string.Empty;
    public long GpuMemoryBytes { get; set; }
    public string GpuMemory { get; set; } = string.Empty;
    public long Duration { get; set; }
    public string DurationFormatted { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? Until { get; set; }
}
