# Ollama Modeller - Design Specification

**Date**: 2026-04-10  
**Status**: Approved

## Overview

Desktop application for managing local Ollama models (download, run, stop, monitor status).

## Technology Stack

- **Framework**: WPF + C# (.NET 8)
- **Pattern**: MVVM (Model-View-ViewModel)
- **API Integration**: HttpClient → Ollama REST API (http://localhost:11434)

## Project Structure

```
OllamaModeller/
├── OllamaModeller.csproj
├── App.xaml(.cs)
├── MainWindow.xaml(.cs)
├── Models/
│   ├── OllamaModel.cs
│   └── ModelStatistics.cs
├── ViewModels/
│   ├── ViewModelBase.cs
│   ├── RelayCommand.cs
│   └── MainViewModel.cs
├── Services/
│   └── OllamaApiService.cs
└── Converters/
    └── BoolToVisibilityConverter.cs
```

## UI Design

### Layout: Single Panel

All operations on one screen with three main sections:

```
┌─────────────────────────────────────────────────────┐
│  Ollama Modeller              [Status Indicator]    │
├─────────────────────────────────────────────────────┤
│  Installed Models                        [Refresh]  │
│  ┌─────────────────────────────────────────────────┤
│  │ NAME        SIZE        ACTIONS                 │
│  │ llama3.2    2.3 GB      [Run] [Delete]          │
│  │ codellama   3.8 GB      [Run] [Delete]          │
│  └─────────────────────────────────────────────────┘
├─────────────────────────────────────────────────────┤
│  Download Model                                     │
│  [____________] [Pull]                              │
│  [█████████░░░░░░░] 45% [Cancel]                    │
├─────────────────────────────────────────────────────┤
│  Running Models                                    │
│  ┌─────────────────────────────────────────────────┤
│  │ MODEL       CPU         ACTIONS                 │
│  │ llama3.2    45%         [Stop]                  │
│  └─────────────────────────────────────────────────┘
├─────────────────────────────────────────────────────┤
│  ✓ Connection OK                                    │
└─────────────────────────────────────────────────────┘
```

### Theme

Dark theme (VS Code style) - system toggle optional.

## Functionality

### 1. View Installed Models
- Command: GET `/api/tags`
- Display: name, size, modified date
- Refresh button to reload list

### 2. Download Model
- Command: POST `/api/pull` with streaming response
- Input: model name text field
- Progress bar with percentage
- Cancel button to abort download

### 3. Run Model
- Command: POST `/api/generate` (keep alive)
- Starts model for interaction
- Updates running models list

### 4. Stop Model
- Command: POST to cancel running model
- Updates running models list

### 5. View Statistics
- Command: GET `/api/ps`
- Display: model name, CPU usage, memory
- Real-time updates (optional polling)

### 6. Health Check
- Command: GET `/`
- Status indicator in header (green/red)
- Auto-check on startup and periodic

## Data Models

### OllamaModel
```csharp
public class OllamaModel
{
    public string Name { get; set; }
    public string Size { get; set; }
    public DateTime Modified { get; set; }
}
```

### ModelStatistics
```csharp
public class ModelStatistics
{
    public string Name { get; set; }
    public double CpuPercent { get; set; }
    public long MemoryBytes { get; set; }
    public DateTime StartedAt { get; set; }
}
```

## ViewModel Commands

- `RefreshCommand` - Reload models list
- `PullCommand` - Start model download
- `CancelPullCommand` - Cancel current download
- `RunCommand` - Start a model
- `StopCommand` - Stop a running model
- `DeleteCommand` - Delete a model (via CLI)

## Error Handling

- Ollama unavailable → Status bar warning (yellow)
- API errors → Status bar with error message
- Network timeout → Retry with exponential backoff
- Download cancelled → Clear progress, reset state

## Acceptance Criteria

1. ✓ App starts and checks Ollama connectivity
2. ✓ Lists all installed models from `/api/tags`
3. ✓ Pull command downloads model with progress bar
4. ✓ Run/Stop commands work via API
5. ✓ Running models shown from `/api/ps`
6. ✓ All operations show status in message bar
7. ✓ Dark theme applied
