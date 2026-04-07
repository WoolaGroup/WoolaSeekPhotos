namespace Woola.PhotoManager.Infrastructure.Repositories;

/// <summary>
/// B4: Contrato CRUD mínimo para todos los repositorios.
/// Permite mocking en unit tests sin SQLite real.
/// </summary>
public interface IRepository<T, TKey>
{
    Task<T?>  GetByIdAsync(TKey id, CancellationToken ct = default);
    Task<TKey> InsertAsync(T entity, CancellationToken ct = default);
    Task       UpdateAsync(T entity, CancellationToken ct = default);
    Task<bool> ExistsAsync(TKey id, CancellationToken ct = default);
}
