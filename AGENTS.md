# Ollama Modeller

Desktop application for managing local Ollama models (download, run, stop, monitor status).

## Project Status

**Framework**: WPF + C# (.NET 8)  
**Pattern**: MVVM  
**API**: HttpClient → Ollama REST API

## Ollama Integration

- Communicates with Ollama via REST API (default: `http://localhost:11434`)
- Key endpoints: `/api/tags`, `/api/pull`, `/api/generate`, `/api/chat`, `/api/ps`
- Health check: `GET /` should return `{"status":"ok"}`

## Commands

```bash
dotnet build          # Build the project
dotnet run           # Run the application
dotnet build --release  # Release build
```

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
└── Services/
    └── OllamaApiService.cs
```
