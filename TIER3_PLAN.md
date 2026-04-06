# TIER 3 — Escalabilidad + Performance
## 6 mejoras · 7 archivos nuevos · 8 archivos modificados

---

## IMP-T3-001 · VirtualizingWrapPanel
**Problema:** `WrapPanel` no soporta virtualización. Con `VirtualizingStackPanel.IsVirtualizing="True"` en el ItemsControl pero `WrapPanel` como ItemsPanel, WPF instancia TODOS los elementos visuales aunque estén fuera de la vista. 200 fotos = 200 `Border` + `Image` + `StackPanel` + `Button` en memoria.

**Archivos:**
- **NEW** `UI/Controls/VirtualizingWrapPanel.cs` (~170 líneas)
  - Hereda de `VirtualizingPanel`, implementa `IScrollInfo`
  - `ItemWidth = 236.0` (220 card + 8+8 margen), `ItemHeight = 270.0`
  - `MeasureOverride`: calcula columnas = `floor(ancho / ItemWidth)`, primera/última fila visible, genera solo los contenedores visibles con `ItemContainerGenerator`
  - `ArrangeOverride`: posiciona por `col * ItemWidth` y `row * ItemHeight - _verticalOffset`
  - `CleanupChildren`: recicla contenedores fuera del viewport (modo Recycling)
  - `IScrollInfo`: `SetVerticalOffset`, `LineUp/Down`, `MouseWheelUp/Down`, `MakeVisible`
- **MOD** `UI/MainWindow.xaml`
  - Reemplazar `ScrollViewer > StackPanel > ItemsControl[WrapPanel]` por una estructura con `CanContentScroll="True"` + `VirtualizingWrapPanel`
  - Agregar `VirtualizingPanel.VirtualizationMode="Recycling"` al ItemsControl
  - El botón "Cargar más" queda en el mismo `Grid.Row="3"` con `VerticalAlignment="Bottom"`

---

## IMP-T3-002 · RangeObservableCollection (batch UI updates)
**Problema:** `LoadPhotosAsync` hace 50 llamadas `Photos.Add()` → 50 eventos `CollectionChanged` → 50 pasadas de layout. Con `VirtualizingWrapPanel` cada Reset es barato, pero los adds individuales siguen siendo costosos durante la animación de carga.

**Archivos:**
- **NEW** `UI/ViewModels/RangeObservableCollection.cs` (~55 líneas)
  - `AddRange(IEnumerable<T>)`: inserta en el `Items` backing list con notificaciones suprimidas, dispara un único `CollectionChanged(Reset)` al final
  - `ReplaceAll(IEnumerable<T>)`: Clear + AddRange en un único Reset
  - `_suppressNotification` flag en `OnCollectionChanged`
- **MOD** `UI/ViewModels/MainViewModel.cs`
  - Campo `Photos`: `ObservableCollection<PhotoViewModel>` → `RangeObservableCollection<PhotoViewModel>`
  - `LoadPhotosAsync` reset branch: `Photos.Clear()` + `Photos.AddRange(sorted.Select(...))`
  - `LoadPhotosAsync` append branch: `Photos.AddRange(sorted.Select(...))`
  - `FilterByAlbumAsync`: `Photos.ReplaceAll(sorted)`
  - `FilterByTagAsync`: `Photos.ReplaceAll(sorted)`
  - `FastSearchAsync`: `Photos.ReplaceAll(sorted)`
  - `FilterByEventAsync` (si existe): mismo patrón

---

## IMP-T3-003 · Debounced Search + CancellationToken
**Problema:** `OnSearchTextChanged` dispara `FastSearchAsync` (query DB) en cada tecla. Escribir "vacaciones" lanza 9 queries en ~200ms, todas compitiendo por la misma conexión SQLite.

**Archivos:**
- **MOD** `UI/ViewModels/MainViewModel.cs`
  - Nuevos campos: `private CancellationTokenSource? _searchDebounce;` y `private CancellationTokenSource? _searchCts;`
  - `OnSearchTextChanged`: cancela debounce anterior → `Task.Delay(300ms, debounceCts.Token)` → cancela query anterior → lanza nueva query con nuevo `_searchCts`
  - `FastSearchAsync(string query, CancellationToken ct)`: pasa el token a `SearchCandidatesAsync`, captura `OperationCanceledException` sin actualizar UI
  - `Dispose()`: libera `_searchDebounce`, `_searchCts`, `_indexingCts`
- **MOD** `Infrastructure/Repositories/PhotoRepository.cs`
  - `SearchCandidatesAsync(string, int, CancellationToken)`: usa `CommandDefinition(sql, params, cancellationToken: ct)` de Dapper

---

## IMP-T3-004 · Paginación en filtros Album + Tag
**Problema:** `FilterByAlbumAsync` carga TODAS las fotos del álbum sin límite. `FilterByTagAsync` llama a `GetPhotosByTagsAsync` que devuelve hasta 1000. Un álbum con 5.000 fotos instancia 5.000 `PhotoViewModel` de golpe.

**Archivos:**
- **MOD** `UI/ViewModels/MainViewModel.cs`
  - Nuevos campos: `private int _filterOffset; private string _activeFilterType; private int _activeAlbumId; private Album? _activeAlbum;`
  - `FilterByAlbumAsync`: establece `_activeFilterType="album"`, `_filterOffset=0`, llama `LoadAlbumPageAsync(album, reset:true)`
  - Nuevo `LoadAlbumPageAsync(Album, bool reset)`: llama `GetPhotosInAlbumPagedAsync(id, PageSize, _filterOffset)`, `Photos.AddRange()`, actualiza `_filterOffset`, `HasMorePhotos`, `StatusText` con total
  - `FilterByTagAsync`: establece `_activeFilterType="tag"`, `_filterOffset=0`, llama `LoadTagPageAsync(reset:true)`
  - Nuevo `LoadTagPageAsync(bool reset)`: llama `GetPhotosByTagsPagedAsync(_activeTagNames, PageSize, _filterOffset)`
  - `LoadMoreAsync`: switch sobre `_activeFilterType` → `LoadAlbumPageAsync(reset:false)` / `LoadTagPageAsync(reset:false)` / `LoadPhotosAsync(reset:false)`
  - `FilterAllAsync`: resetea `_activeFilterType = string.Empty`
- **MOD** `Infrastructure/Repositories/AlbumRepository.cs`
  - Nuevo método `GetPhotosInAlbumPagedAsync(int albumId, int limit, int offset) → (List<Photo>, int total)`
  - COUNT(*) + SELECT con LIMIT/OFFSET + `ORDER BY COALESCE(DateTaken, CreatedAt) DESC`
- **MOD** `Infrastructure/Repositories/TagRepository.cs`
  - Nuevo método `GetPhotosByTagsPagedAsync(IEnumerable<string> tags, int limit, int offset) → (List<Photo>, int total)`
  - COUNT subquery + SELECT con LIMIT/OFFSET

---

## IMP-T3-005 · Streaming Indexer + Parallel Hashing
**Problema:** `Directory.GetFiles()` bloquea hasta enumerar todos los archivos antes de procesar el primero. En un NAS con 50.000 fotos, el progreso permanece en 0% durante segundos. El hashing SHA256 es secuencial (1 archivo a la vez).

**Archivos:**
- **MOD** `Core/Services/PhotoIndexer.cs`
  - Reemplazar `IndexDirectoryAsync` completo:
    - `Directory.EnumerateFiles()` (streaming, sin esperar al final)
    - `Channel<string>` (capacidad 200, BoundedChannelFullMode.Wait) como pipeline producer→consumer
    - **Producer Task**: itera `EnumerateFiles`, escribe en canal, incrementa `discovered`
    - **Consumer Task**: `ReadAllAsync` + `SemaphoreSlim(8, 8)` para hashing paralelo
    - Cada worker: `ComputeHash` → `PhotoExistsAsync` → `CreatePhotoFromFileAsync` → acumula en `ConcurrentBag<Photo>` → drain cada 100 con `SaveBatchAsync`
    - Progreso emitido desde cada worker: `TotalFound=discovered`, `Processed=processed`, `Percentage=processed/discovered*100`
    - Helper `DrainBatch(ConcurrentBag<Photo>) → List<Photo>` para vaciado thread-safe
    - Cap de tareas pendientes (`pendingTasks.Count >= 32` → `WhenAny`)

---

## IMP-T3-006 · SQLite Tuning + BitmapImage Disposal
**Problema A:** SQLite usa solo `WAL + NORMAL`. Sin `cache_size` ni `mmap_size`, cada query de tags con subconsulta correlacionada genera I/O innecesario.
**Problema B:** `<Image Source="{Binding ThumbnailPath}"/>` crea `BitmapImage` con el file handle abierto indefinidamente. `Photos.Clear()` elimina el `PhotoViewModel` pero WPF puede retener la imagen en su internal bitmap store.

**Archivos:**
- **MOD** `Infrastructure/Database/SqliteConnectionFactory.cs`
  - Extender el `walCmd` con:
    ```sql
    PRAGMA cache_size    = -32000;   -- 32 MB page cache
    PRAGMA temp_store    = MEMORY;   -- sort buffers en RAM
    PRAGMA mmap_size     = 268435456; -- 256 MB memory-mapped I/O
    PRAGMA foreign_keys  = ON;
    ```
- **MOD** `Infrastructure/Repositories/PhotoRepository.cs`
  - `GetPhotosAsync`: cambiar `ORDER BY Id DESC` → `ORDER BY COALESCE(DateTaken, CreatedAt) DESC`
  - Eliminar el `OrderByDescending` en `MainViewModel.LoadPhotosAsync` (ya innecesario)
  - En `InitializeDatabase` o migración: `CREATE INDEX IF NOT EXISTS idx_photos_sort ON Photos(COALESCE(DateTaken, CreatedAt) DESC)`
- **NEW** `UI/Converters/BitmapImageConverter.cs` (~40 líneas)
  - `IValueConverter`: `string path → BitmapImage`
  - `BitmapCacheOption.OnLoad` (cierra el file handle tras cargar)
  - `DecodePixelWidth = 220` (evita decode a resolución completa)
  - `bitmap.Freeze()` (permite GC desde hilo background, desvincula del árbol WPF)
  - Devuelve `null` si el archivo no existe (sin excepción)
- **MOD** `UI/MainWindow.xaml`
  - Agregar en `Window.Resources`: `<conv:BitmapImageConverter x:Key="BitmapImageConv"/>`
  - Cambiar `<Image Source="{Binding ThumbnailPath}"/>` → `<Image Source="{Binding ThumbnailPath, Converter={StaticResource BitmapImageConv}}"/>`

---

## Orden de implementación (sin dependencias rotas)

| Paso | IMP | Archivos | Razón del orden |
|------|-----|----------|-----------------|
| 1 | T3-006 A | SqliteConnectionFactory.cs | Aislado, 0 dependencias |
| 2 | T3-006 B | PhotoRepository.cs + índice | Aislado |
| 3 | T3-002 | RangeObservableCollection.cs | Crea la clase antes de usarla |
| 4 | T3-002 | MainViewModel.cs (Photos + AddRange) | Requiere paso 3 |
| 5 | T3-003 | MainViewModel.cs (debounce) + PhotoRepository.cs | Modifica mismo archivo que paso 4 |
| 6 | T3-004 | AlbumRepository.cs + TagRepository.cs | Nuevos métodos en repos |
| 7 | T3-004 | MainViewModel.cs (pagination) | Requiere paso 6 |
| 8 | T3-005 | PhotoIndexer.cs | Aislado del pipeline UI |
| 9 | T3-006 C | BitmapImageConverter.cs | Nueva clase antes de referenciarla |
| 10 | T3-001 | VirtualizingWrapPanel.cs | Nueva clase antes de referenciarla |
| 11 | T3-001 + T3-006 | MainWindow.xaml | Requiere pasos 9 y 10 |
| 12 | — | `dotnet build` | Verificar 0 errores |

## Impacto esperado

| Métrica | Antes | Después |
|---------|-------|---------|
| Elementos WPF con 200 fotos | 200 vivos | ~15 visibles |
| `CollectionChanged` por carga de 50 fotos | 50 | 1 |
| Queries DB al escribir "vacaciones" (9 teclas) | 9 | 1 |
| Fotos cargadas en filtro álbum/tag | Sin límite | 50 (+ Load More) |
| Progreso indexer al inicio | 0% hasta fin del scan | Inmediato desde 1er archivo |
| File handles abiertos por thumbnails | 1 por foto visible | 0 (cerrado tras load) |
| SQLite page cache | ~2 MB (defecto) | 32 MB |
