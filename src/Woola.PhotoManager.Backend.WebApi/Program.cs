using System.Text;
using System.Text.Json.Serialization;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Woola.PhotoManager.Backend.Application.Albums.Commands;
using Woola.PhotoManager.Backend.Application.Albums.Queries;
using Woola.PhotoManager.Backend.Application.Common.Interfaces;
using Woola.PhotoManager.Backend.Application.Photos.Commands;
using Woola.PhotoManager.Backend.Application.Photos.Queries;
using Woola.PhotoManager.Backend.Infrastructure;
using Woola.PhotoManager.Backend.Infrastructure.Data;
using Woola.PhotoManager.Backend.WebApi.Hubs;
using Woola.PhotoManager.Backend.WebApi.Middleware;
using Woola.PhotoManager.Backend.WebApi.Services;
using Woola.PhotoManager.Shared.Configuration;

// Legacy Core references
using Woola.PhotoManager.Infrastructure.Database;
using Woola.PhotoManager.Infrastructure.Repositories;
using Woola.PhotoManager.Common.Services;
using CoreServices = Woola.PhotoManager.Core.Services;
using CoreAgents = Woola.PhotoManager.Core.Agents;

var builder = WebApplication.CreateBuilder(args);

// Config
var woolaConfig = builder.Configuration.GetSection(WoolaOptions.SectionName).Get<WoolaOptions>() ?? new();
var jwtConfig = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new();
var authConfig = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new();

builder.Services.Configure<WoolaOptions>(builder.Configuration.GetSection(WoolaOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));

var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WoolaPhotos");
var dbPath = string.IsNullOrEmpty(woolaConfig.DatabasePath) ? Path.Combine(appData, "photos.db") : woolaConfig.DatabasePath;
var thumbDir = string.IsNullOrEmpty(woolaConfig.ThumbnailDirectory) ? Path.Combine(appData, "thumbnails") : woolaConfig.ThumbnailDirectory;
var uploadDir = string.IsNullOrEmpty(woolaConfig.UploadDirectory) ? Path.Combine(appData, "uploads") : woolaConfig.UploadDirectory;
var logDir = Path.Combine(appData, "logs");

Directory.CreateDirectory(appData);
Directory.CreateDirectory(thumbDir);
Directory.CreateDirectory(uploadDir);
Directory.CreateDirectory(logDir);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(logDir, "api-.log"), rollingInterval: RollingInterval.Day));

builder.Services.AddInfrastructure(dbPath);
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetPhotosHandler).Assembly));
builder.Services.AddSignalR();

// Legacy Core Services
builder.Services.AddSingleton(new SqliteConnectionFactory(dbPath));
builder.Services.AddSingleton<PhotoRepository>();
builder.Services.AddSingleton<TagRepository>();
builder.Services.AddSingleton<FaceRepository>();
builder.Services.AddSingleton<IMetadataService, MetadataService>();
builder.Services.AddSingleton<IThumbnailService, ThumbnailService>();
builder.Services.AddSingleton<CoreServices.IAutoTaggingService, CoreServices.AutoTaggingService>();
builder.Services.AddSingleton<IObjectDetectionService, ObjectDetectionService>();
builder.Services.AddSingleton<IFaceService, FaceService>();
builder.Services.AddSingleton<IOcrService, OcrService>();
builder.Services.AddSingleton<IQualityAssessmentService, QualityAssessmentService>();

builder.Services.AddSingleton<CoreAgents.IAgentOrchestrator>(sp =>
{
    var o = new CoreAgents.AgentOrchestrator(
        sp.GetRequiredService<TagRepository>(),
        sp.GetRequiredService<ILogger<CoreAgents.AgentOrchestrator>>());
    var fs = sp.GetRequiredService<IFaceService>();
    var fr = sp.GetRequiredService<FaceRepository>();
    var tr = sp.GetRequiredService<TagRepository>();
    o.RegisterAgent(new CoreAgents.Agents.MetadataAgent(sp.GetRequiredService<IMetadataService>()));
    o.RegisterAgent(new CoreAgents.Agents.AutoTaggingAgent(sp.GetRequiredService<CoreServices.IAutoTaggingService>()));
    o.RegisterAgent(new CoreAgents.Agents.VisionAgent(sp.GetRequiredService<IObjectDetectionService>()));
    o.RegisterAgent(new CoreAgents.Agents.FaceAgent(fs, fr, tr));
    o.RegisterAgent(new CoreAgents.Agents.OcrAgent(sp.GetRequiredService<IOcrService>()));
    o.RegisterAgent(new CoreAgents.Agents.SceneAgent(tr));
    o.RegisterAgent(new CoreAgents.Agents.QualityAgent(sp.GetRequiredService<IQualityAssessmentService>()));
    o.RegisterAgent(new CoreAgents.Agents.GeoLocationAgent());
    return o;
});

builder.Services.AddScoped<IIndexingService, IndexingService>();
builder.Services.AddScoped<IAnalysisService, AnalysisService>();
builder.Services.AddSingleton<IAgentOrchestrator, AgentOrchestratorService>();
builder.Services.AddSingleton<CoreServices.IEventDetectionService, CoreServices.EventDetectionService>();
builder.Services.AddSingleton<WatchFolderService>();
builder.Services.AddSingleton<BackupScheduleService>();
builder.Services.AddHostedService<TrashCleanupService>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();

builder.Services.AddControllers().AddJsonOptions(opts =>
{
    opts.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    opts.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var jwtKey = Encoding.UTF8.GetBytes(jwtConfig.Key);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidIssuer = jwtConfig.Issuer,
            ValidAudience = jwtConfig.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(jwtKey),
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins("http://localhost:5000", "http://localhost:5150")
          .AllowAnyMethod().AllowAnyHeader()));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<WoolaDbContext>(tags: new[] { "db" });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WoolaDbContext>();
    await DbInitializer.InitializeAsync(db);
    try { await db.Database.MigrateAsync(); }
    catch (Exception ex)
    {
        var log = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        log.LogWarning(ex, "Migrations skipped");
    }
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseSwagger();
app.UseSwaggerUI();
app.UseSerilogRequestLogging();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<IndexingHub>("/hubs/indexing");
app.MapHealthChecks("/api/v1/health/ready", new()
{
    Predicate = _ => true,
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString() }),
            timestamp = DateTime.UtcNow
        });
        await ctx.Response.WriteAsync(json);
    }
});

// In production, redirect HTTP to HTTPS
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(thumbDir),
    RequestPath = "/thumbnails",
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Cache-Control", $"public, max-age={woolaConfig.ThumbnailCacheMinutes * 60}");
    }
});

app.Urls.Add("http://localhost:5150");
Console.WriteLine($"Woola Photos API running on http://localhost:5150");
Console.WriteLine($"Swagger: http://localhost:5150/swagger");
Console.WriteLine($"Database: {dbPath}");
Console.WriteLine($"Thumbnails: {thumbDir}");

await app.RunAsync();
