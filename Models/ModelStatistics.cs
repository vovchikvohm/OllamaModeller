using System;

namespace OllamaModeller.Models;

public class ModelStatistics
{
    public string Name { get; set; } = string.Empty;
    public double CpuPercent { get; set; }
    public long MemoryBytes { get; set; }
    public DateTime StartedAt { get; set; }
}
