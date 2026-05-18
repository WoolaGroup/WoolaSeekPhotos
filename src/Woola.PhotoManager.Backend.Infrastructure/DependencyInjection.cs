using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Woola.PhotoManager.Backend.Domain.Repositories;
using Woola.PhotoManager.Backend.Infrastructure.Data;
using Woola.PhotoManager.Backend.Infrastructure.Repositories;

namespace Woola.PhotoManager.Backend.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, string dbPath)
    {
        services.AddDbContext<WoolaDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        services.AddScoped<IPhotoRepository, PhotoRepository>();
        services.AddScoped<IAlbumRepository, AlbumRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IFaceRepository, FaceRepository>();

        return services;
    }
}
