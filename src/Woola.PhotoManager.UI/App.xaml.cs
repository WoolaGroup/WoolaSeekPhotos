using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Woola.PhotoManager.Common.Services;
using Woola.PhotoManager.Core.Agents;
using Woola.PhotoManager.Core.Agents.Agents;
using Woola.PhotoManager.Core.Services;
using Woola.PhotoManager.Infrastructure.Database;
using Woola.PhotoManager.Infrastructure.Repositories;
using Woola.PhotoManager.UI.Services;
using Woola.PhotoManager.UI.ViewModels;

namespace Woola.PhotoManager.UI;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Woola");

        Directory.CreateDirectory(appData);
        Directory.CreateDirectory(Path.Combine(appData, "Thumbnails"));
        Directory.CreateDirectory(Path.Combine(appData, "Logs"));

        var dbPath = Path.Combine(appData, "woola.db");

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                // Infrastructure
                services.AddSingleton(_ => new SqliteConnectionFactory(dbPath));
                services.AddSingleton<PhotoRepository>();
                services.AddSingleton<TagRepository>();
                services.AddSingleton<FaceRepository>();

                // Common Services (Singleton: modelos ONNX son costosos de cargar)
                services.AddSingleton<IThumbnailService, ThumbnailService>();
                services.AddSingleton<IMetadataService, MetadataService>();
                services.AddSingleton<IObjectDetectionService, ObjectDetectionService>();
                services.AddSingleton<IOcrService, OcrService>();
                services.AddSingleton<IFaceService, FaceService>();
                services.AddSingleton<TextEmbeddingService>();

                // Core Services
                services.AddSingleton<IAutoTaggingService, AutoTaggingService>();
                services.AddSingleton<ISemanticSearchService, SemanticSearchService>();
                services.AddSingleton<IAgentOrchestrator, AgentOrchestrator>();
                services.AddSingleton<IPhotoIndexer, PhotoIndexer>();

                // UI
                services.AddSingleton<IFolderPickerService, WinFormsFolderPickerService>();
                services.AddSingleton<MainViewModel>();
                services.AddTransient<MainWindow>();
            })
            .Build();

        // Post-build: registrar agentes con instancias ya construidas por el contenedor
        var sp = _host.Services;
        var orchestrator = sp.GetRequiredService<IAgentOrchestrator>();
        orchestrator.RegisterAgent(new MetadataAgent(sp.GetRequiredService<IMetadataService>()));
        orchestrator.RegisterAgent(new AutoTaggingAgent(sp.GetRequiredService<TagRepository>()));
        orchestrator.RegisterAgent(new VisionAgent(sp.GetRequiredService<IObjectDetectionService>()));
        orchestrator.RegisterAgent(new OcrAgent(sp.GetRequiredService<IOcrService>()));
        orchestrator.RegisterAgent(new FaceAgent(
            sp.GetRequiredService<IFaceService>(),
            sp.GetRequiredService<FaceRepository>(),
            sp.GetRequiredService<TagRepository>()));

        await _host.StartAsync();
        sp.GetRequiredService<MainWindow>().Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        base.OnExit(e);
    }
}
