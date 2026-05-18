# Woola Photos — ROADMAP

> **Objetivo:** Migrar de WPF a MAUI Blazor Hybrid con backend API local, preservando el pipeline de IA y agregando nuevos módulos.
> **Layout:** Basado en el diseño moderno del proyecto POS Web (sidebar + topbar + dark mode).
> **Arquitectura:** Backend localhost + Frontend MAUI consumiendo API.
> **Agentes:** 100% locales, sin exponer fotos a internet.

---

# FASE 0 — FUNDACIÓN (Sprint 0)

## Sprint 0.1 — Backend Clean Architecture

Crear la API backend en `localhost:5150` con capas Domain → Application → Infrastructure → WebApi.

### Backend

| Tarea | Archivos | Descripción |
|-------|----------|-------------|
| B0.1.1 | Domain/ | Migrar entidades existentes (Photo, Album, Tag, Face, PhotoTag) a BaseEntity con audit fields |
| B0.1.2 | Domain/ | ValueObjects: PhotoFile, GeoLocation, ExifData |
| B0.1.3 | Domain/ | Repository contracts: IPhotoRepository, IAlbumRepository, ITagRepository, IFaceRepository |
| B0.1.4 | Infrastructure/ | EF Core DbContext + Fluent API configuration |
| B0.1.5 | Infrastructure/ | PhotoRepository, AlbumRepository, TagRepository, FaceRepository (EF Core) |
| B0.1.6 | Infrastructure/ | SQLite connection factory con PRAGMAs optimizados (WAL, 32MB cache, 256MB mmap) |
| B0.1.7 | Application/ | CQRS: GetPhotosQuery, GetPhotoDetailQuery, SearchPhotosQuery |
| B0.1.8 | Application/ | CQRS: IndexPhotosCommand, CreateAlbumCommand, AddPhotosToAlbumCommand |
| B0.1.9 | Application/ | Mapping: Entity → DTO (AutoMapper o manual) |
| B0.1.10 | Application/ | ApplicationServices: IIndexingService, IAgentAnalysisService, ISearchService |
| B0.1.11 | WebApi/ | Program.cs con JWT, Swagger, Serilog, CORS localhost, SignalR |
| B0.1.12 | WebApi/ | PhotosController: GET list, GET by id, GET search, POST index |
| B0.1.13 | WebApi/ | AlbumsController: CRUD + fotos por álbum |
| B0.1.14 | WebApi/ | TagsController, FacesController, DashboardController, AuthController |
| B0.1.15 | WebApi/ | IndexingHub (SignalR): progreso de indexación en tiempo real |
| B0.1.16 | WebApi/ | Static files middleware para servir thumbnails locales |

### Base de Datos

| Tarea | Descripción |
|-------|-------------|
| DB0.1 | Migrar schema SQLite actual a EF Core migrations |
| DB0.2 | Índices: idx_photos_hash (unique), idx_photos_path (unique), idx_photos_sort |
| DB0.3 | Soft Delete en todas las entidades con query filter global |
| DB0.4 | Auditoría: CreatedAt, UpdatedAt automáticos via EF Core interceptors |
| DB0.5 | Seed data para desarrollo |

### Dependencias
- Código existente: Domain entities, repositorios Dapper (como referencia lógica)
- Common (ONNX, ImageSharp, Tesseract, OpenCvSharp) — sin cambios

### Riesgos
| Riesgo | Mitigación |
|--------|------------|
| EF Core performance vs Dapper actual | Mantener Dapper para consultas complejas/search; EF Core para CRUD normal |
| Migración de datos existente | Crear migration inicial que recrea schema; datos se conservan en SQLite |

---

## Sprint 0.2 — Frontend MAUI + Layout Moderno

Crear la app MAUI Blazor Hybrid con el layout inspirado en el proyecto POS Web.

### Frontend

| Tarea | Archivos | Descripción |
|-------|----------|-------------|
| F0.2.1 | Woola.PhotoManager.App/ | Proyecto MAUI Blazor Hybrid (net8.0-android;net8.0-ios;net8.0-windows) |
| F0.2.2 | App/Components/Layout/MainLayout.razor | Sidebar (240px) + Topbar (60px) + Content Area |
| F0.2.3 | App/Components/Layout/NavMenu.razor | Navegación: Dashboard, Photos, Albums, People, Tags, Duplicates, Settings |
| F0.2.4 | App/Components/Layout/EmptyLayout.razor | Layout limpio para pantalla de inicio |
| F0.2.5 | App/wwwroot/app.css | Sistema de diseño: colores, tipografía, cards, botones, dark mode |
| F0.2.6 | App/MauiProgram.cs | DI: servicios, MudBlazor, HttpClient apuntando a localhost:5150 |
| F0.2.7 | App/Components/Routes.razor | Router con layouts |
| F0.2.8 | App/Services/PhotoApiClient.cs | HttpClient centralizado con JWT + refresh automático |
| F0.2.9 | App/Services/*.cs | Service wrappers: IPhotoService, IAlbumService, ITagService, IFaceService, IIndexingService, IDashboardService |
| F0.2.10 | App/ViewModels/BaseViewModel.cs | Loading/error/success, ExecuteAsync (copiado del POS) |
| F0.2.11 | App/ViewModels/*.cs | ViewModels por página |
| F0.2.12 | App/Components/Pages/Welcome.razor | Pantalla de inicio con botón "Comenzar" |
| F0.2.13 | App/Components/Pages/NotFound.razor | Página 404 |

### Layout — Estructura Visual

```
┌──────────────┬──────────────────────────────────────────┐
│              │  Topbar                                   │
│   Sidebar    │  ☰ Photos   🔍 Buscar...   🌙  👤 Admin  │
│   (240px)    ├──────────────────────────────────────────┤
│              │                                          │
│  🖼️ Photos   │  Content Area                           │
│  📁 Albums   │  (scroll, padded 24px)                   │
│  👤 People   │                                          │
│  🏷️ Tags     │                                          │
│  🔍 Dupes    │                                          │
│  ⚙️ Settings │                                          │
│              │                                          │
│  ────────    │                                          │
│  🌙 DarkMode │                                          │
└──────────────┴──────────────────────────────────────────┘
```

### Dependencias
- Backend Sprint 0.1 (API en localhost:5150)
- POS Web layout como referencia visual

---

# FASE 1 — NÚCLEO (Sprint 1)

## Sprint 1.1 — Galería de Fotos

Reemplazar MainWindow.xaml + MainViewModel.cs (640 líneas) por página Blazor moderna.

### Frontend

| Tarea | Mejora vs WPF | Descripción |
|-------|---------------|-------------|
| F1.1.1 | ✅ Virtualización nativa | `PhotosPage.razor` con `<Virtualize>` + MudGrid. WPF usaba VirtualizingWrapPanel custom; Blazor trae virtualización incorporada |
| F1.1.2 | ✅ Debounce search (ya existe) | Search con MudTextField Immediate=true + DebounceInterval=300 |
| F1.1.3 | ✅ Paginación (ya existe en TIER3) | Load more con offset/limit. Ya implementado en MainViewModel.cs con `_filterOffset` |
| F1.1.4 | ✅ Vista Grid/Lista | Toggle entre MudGrid y lista vertical |
| F1.1.5 | ✅ Ordenación | SortMode: DateDesc, DateAsc, NameAsc, SizeDesc, CameraModel (ya existe en T4-001) |
| F1.1.6 | **NUEVO** | PhotoDetailDrawer (MudDrawer lateral) con metadatos EXIF, tags, faces, mapa |
| F1.1.7 | **NUEVO** | Navegación por teclado ← → (ya existe en T4-003) |
| F1.1.8 | **NUEVO** | Filtros como MudChip removibles (ya existen FilterChip en WPF) |

### Backend

| Tarea | Descripción |
|-------|-------------|
| B1.1.1 | GET /api/v1/photos con paginación, sort, filtros |
| B1.1.2 | GET /api/v1/photos/{id} con metadata + tags + faces |
| B1.1.3 | GET /api/v1/photos/search con búsqueda híbrida |
| B1.1.4 | Migrar HybridSearchService (ya existe en Core) al Application layer |

### Comportamiento visual de la galería

```
┌──────────────────────────────────────────────────────────┐
│ [🔍 Buscar...                        ] [Grid] [Lista]   │
│ [📁 Vacaciones x] [🏷️ Playa x]                         │
├──────────────────────────────────────────────────────────┤
│ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐           │
│ │foto1 │ │foto2 │ │foto3 │ │foto4 │ │foto5 │           │
│ │  🖼️  │ │  🖼️  │ │  🖼️  │ │  🖼️  │ │  🖼️  │           │
│ │playa │ │monte │ │playa │ │casa  │ │comida│           │
│ └──────┘ └──────┘ └──────┘ └──────┘ └──────┘           │
│ [══════════════════ Cargar más ═════════════════════]    │
└──────────────────────────────────────────────────────────┘
```

---

## Sprint 1.2 — Indexación y Agentes

Migrar PhotoIndexer + AgentOrchestrator + 10 agentes IA al backend.

### Backend

| Tarea | Archivo WPF Existente | Acción |
|-------|----------------------|--------|
| B1.2.1 | Core/Services/PhotoIndexer.cs | Envolver en IndexingHostedService (BackgroundService) |
| B1.2.2 | Core/Agents/AgentOrchestrator.cs | Envolver en AnalysisBackgroundService |
| B1.2.3 | Core/Agents/MetadataAgent.cs | **Sin cambios** |
| B1.2.4 | Core/Agents/AutoTaggingAgent.cs | **Sin cambios** |
| B1.2.5 | Core/Agents/VisionAgent.cs | **Sin cambios** (YOLOv8 ONNX) |
| B1.2.6 | Core/Agents/FaceAgent.cs | **Sin cambios** (FaceNet ONNX) |
| B1.2.7 | Core/Agents/OcrAgent.cs | **Sin cambios** (Tesseract) |
| B1.2.8 | Core/Agents/ClaudeVisionAgent.cs | **Sin cambios** (API opcional) |
| B1.2.9 | Core/Agents/SceneAgent.cs | **Sin cambios** |
| B1.2.10 | Core/Agents/QualityAgent.cs | **Sin cambios** |
| B1.2.11 | Core/Agents/GeoLocationAgent.cs | **Sin cambios** |
| B1.2.12 | SignalR Hub | Progreso de indexación en tiempo real |
| B1.2.13 | API: POST /api/v1/photos/index | Iniciar indexación |
| B1.2.14 | API: POST /api/v1/photos/{id}/analyze | Analizar foto individual |

### Frontend

| Tarea | Descripción |
|-------|-------------|
| F1.2.1 | IndexingPage.razor con progreso en vivo via SignalR |
| F1.2.2 | MudProgressBar + nombre de archivo actual + contador |
| F1.2.3 | Botón Start/Stop indexación |
| F1.2.4 | Historial de últimas indexaciones |

### Mejoras TIER3 ya incluidas aquí

| Mejora | Estado WPF | En MAUI + API |
|--------|------------|---------------|
| IMP-T3-005: Streaming Indexer + Parallel Hashing | Pendiente en PhotoIndexer.cs | Se implementa directamente en el BackgroundService |
| IMP-T3-005: Channel<string> + SemaphoreSlim(8) | Pendiente | Se incluye desde el inicio |

---

# FASE 2 — MÓDULOS EXISTENTES MEJORADOS (Sprint 2)

## Sprint 2.1 — Álbumes

Reemplazar AlbumWindow.xaml (293 líneas, code-behind pesado).

### Frontend

| Tarea | Mejora vs WPF | Descripción |
|-------|---------------|-------------|
| F2.1.1 | ✅ MudBlazor Cards | AlbumsPage.razor con lista visual de álbumes |
| F2.1.2 | ✅ MudDialog | Crear/editar álbum en dialog |
| F2.1.3 | ✅ Drag & drop | Agregar fotos desde galería (futuro) |
| F2.1.4 | ✅ Export ZIP | Reutilizar ExportService existente vía API |
| F2.1.5 | **NUEVO** | Álbumes inteligentes: "Últimos 30 días", "Sin álbum", "Favoritos" |
| F2.1.6 | **NUEVO** | Portada de álbum con collage de 4 fotos |

### Backend

| Tarea | Descripción |
|-------|-------------|
| B2.1.1 | CRUD álbumes |
| B2.1.2 | Agregar/quitar fotos de álbum |
| B2.1.3 | GET /api/v1/albums/{id}/photos paginado |
| B2.1.4 | Export álbum como ZIP |

---

## Sprint 2.2 — Tags

### Frontend

| Tarea | Mejora vs WPF | Descripción |
|-------|---------------|-------------|
| F2.2.1 | ✅ MudAutocomplete | Buscar y agregar tags |
| F2.2.2 | ✅ MudChipSet | Tags como chips removibles en filtros |
| F2.2.3 | ✅ Sidebar de tags | Lista con conteo de uso |
| F2.2.4 | **NUEVO** | Tag cloud visual |
| F2.2.5 | **NUEVO** | Tags automáticos vs manuales (con badge de origen) |

### Backend

| Tarea | Descripción |
|-------|-------------|
| B2.2.1 | CRUD tags |
| B2.2.2 | GET /api/v1/tags con conteo de fotos |
| B2.2.3 | GET /api/v1/tags/{id}/photos paginado |
| B2.2.4 | Tags por fuente: Metadata, Vision, OCR, Manual |

---

## Sprint 2.3 — Personas / Faces

Reemplazar FaceManagementWindow.xaml (404 líneas, código más pesado del proyecto).

### Frontend

| Tarea | Mejora vs WPF | Descripción |
|-------|---------------|-------------|
| F2.3.1 | ✅ MudGrid de caras | Grid de personas con thumbnail grupal |
| F2.3.2 | ✅ MudTextField inline | Renombrar persona inline (sin dialog) |
| F2.3.3 | ✅ Fotos por persona | Página de fotos de una persona |
| F2.3.4 | **NUEVO** | Faces no agrupadas (unknown) sección separada |
| F2.3.5 | **NUEVO** | Confirmación individual de cara |

### Backend

| Tarea | Descripción |
|-------|-------------|
| B2.3.1 | GET /api/v1/faces (agrupadas por persona) |
| B2.3.2 | PUT /api/v1/faces/{id}/name |
| B2.3.3 | POST /api/v1/faces/cluster (ejecutar clustering) |
| B2.3.4 | GET /api/v1/faces/person/{personId}/photos |

---

## Sprint 2.4 — Dashboard

Reemplazar DashboardWindow.xaml.

### Frontend

| Tarea | Mejora vs WPF | Descripción |
|-------|---------------|-------------|
| F2.4.1 | ✅ MudCard KPI | Total fotos, álbumes, personas, espacio usado |
| F2.4.2 | ✅ Gráfico de barras | Fotos por mes (HTML + CSS) |
| F2.4.3 | ✅ Top tags | Lista de tags más usados |
| F2.4.4 | **NUEVO** | Distribución por cámara/ISO/apertura |
| F2.4.5 | **NUEVO** | Mapa de calor de ubicaciones GPS |
| F2.4.6 | **NUEVO** | Actividad de indexación (fotos/día) |

### Backend

| Tarea | Descripción |
|-------|-------------|
| B2.4.1 | GET /api/v1/dashboard/stats |
| B2.4.2 | GET /api/v1/dashboard/photos-by-month |
| B2.4.3 | GET /api/v1/dashboard/top-tags |
| B2.4.4 | GET /api/v1/dashboard/camera-distribution |

---

## Sprint 2.5 — Settings

Reemplazar SettingsWindow.xaml.

### Frontend

| Tarea | Mejora vs WPF | Descripción |
|-------|---------------|-------------|
| F2.5.1 | ✅ MudSwitch | Habilitar/deshabilitar agentes individuales (10 switches) |
| F2.5.2 | ✅ MudTextField | API Key de Claude (opaque) |
| F2.5.3 | ✅ MudSlider | Umbral de clustering facial |
| F2.5.4 | ✅ Folder picker | Ruta de importación y thumbnails |
| F2.5.5 | **NUEVO** | Resetear base de datos |
| F2.5.6 | **NUEVO** | Exportar/Importar configuración |
| F2.5.7 | **NUEVO** | About: versión, licencias ONNX/models |

---

# FASE 3 — NUEVOS MÓDULOS (Sprint 3)

## Sprint 3.1 — Duplicados

Reemplazar DuplicatesWindow.xaml.

| Tarea | Descripción |
|-------|-------------|
| F3.1.1 | Grupos de duplicados con thumbnail + % similitud |
| F3.1.2 | Acciones: Delete duplicate, Keep both, Merge metadata |
| F3.1.3 | Vista: "Todos los duplicados" y "Por album" |
| B3.1.1 | POST /api/v1/photos/detect-duplicates (pHash + Union-Find) |
| B3.1.2 | GET /api/v1/photos/duplicates |

---

## Sprint 3.2 — Fotos Similares

Reemplazar SimilarPhotosWindow.xaml.

| Tarea | Descripción |
|-------|-------------|
| F3.2.1 | Desde detalle de foto: botón "Fotos similares" |
| F3.2.2 | Grid de similares con % de similitud |
| B3.2.1 | GET /api/v1/photos/{id}/similar (cosine similarity) |

---

## Sprint 3.3 — Timeline / Eventos

Mejora de EventDetectionService existente.

| Tarea | Descripción |
|-------|-------------|
| F3.3.1 | Vista de línea de tiempo (año/mes/día) |
| F3.3.2 | Agrupación por eventos detectados (gap > 2 días) |
| F3.3.3 | **NUEVO** | Editar eventos: unir, dividir, renombrar |
| B3.3.1 | GET /api/v1/events |
| B3.3.2 | PUT /api/v1/events/{id} |

---

## Sprint 3.4 — Mapa Geográfico

**Módulo completamente nuevo.**

| Tarea | Descripción |
|-------|-------------|
| F3.4.1 | Mapa con Leaflet/Mapbox via JSInterop |
| F3.4.2 | Marcadores de fotos con GPS |
| F3.4.3 | Cluster de marcadores por zoom |
| F3.4.4 | Click → foto en galería |
| F3.4.5 | Fotos sin GPS sección separada |
| B3.4.1 | GET /api/v1/photos/geo (fotos con GPS) |
| B3.4.2 | GET /api/v1/photos/geo/bounds (fotos en bounding box) |

---

# FASE 4 — FUTURO (Sprint 4+)

## Módulos Propuestos

| Módulo | Prioridad | Descripción | Técnicamente |
|--------|-----------|-------------|--------------|
| **Álbumes Compartidos** | Alta | Generar link local para compartir álbumes en LAN | Servir HTML estático desde backend + zeroconf |
| **Carga Móvil** | Alta | Subir fotos desde Android/iOS al backend local | MAUI FilePicker + HTTP upload a localhost |
| **Slideshow** | Media | Pantalla completa con transiciones | MudBlazor fullscreen + timer |
| **Edición Básica** | Media | Recortar, rotar, ajustar brillo/contraste | ImageSharp en backend + MudColorPicker |
| **Comparación Lado a Lado** | Media | Comparar dos fotos similares con slider | JSInterop + canvas |
| **Backup / Export** | Media | Exportar base de datos + thumbnails a ZIP | Backend service |
| **Caras: Sugerir Nombres** | Baja | Integración con contactos del dispositivo | MAUI Contacts API (solo móvil) |
| **Línea de Tiempo 3D** | Baja | Vista tipo "fotografía" con profundidad | Experimento, no crítico |
| **Scanner de Fotos Físicas** | Baja | Usar cámara para digitalizar fotos impresas | MAUI Camera + backend processing |
| **Multi-idioma** | Baja | Español/Inglés | Archivos .resx + MudBlazor localization |
| **Plugins de Agentes** | Baja | Permitir agentes personalizados vía scripting | Carga dinámica de assemblies |

---

# MEJORAS TIER3 YA PLANIFICADAS (Incorporadas en la migración)

| ID | Mejora | Estado WPF | En MAUI + API |
|----|--------|------------|---------------|
| IMP-T3-001 | VirtualizingWrapPanel | Pendiente, 170 líneas custom | **Resuelto nativamente:** Blazor `<Virtualize>` lo reemplaza sin código custom |
| IMP-T3-002 | RangeObservableCollection | Pendiente, 55 líneas custom | **Resuelto nativamente:** Blazor `StateHasChanged()` + `ObservableCollection` es suficiente |
| IMP-T3-003 | Debounced Search + CancellationToken | **Ya implementado** en MainViewModel.cs | Se adapta al ViewModel Blazor (cambiar Dispatcher.Invoke → InvokeAsync) |
| IMP-T3-004 | Paginación en filtros Album + Tag | **Ya implementado** en MainViewModel.cs | Se adapta al ViewModel Blazor |
| IMP-T3-005 | Streaming Indexer + Parallel Hashing | Pendiente en PhotoIndexer.cs | Se implementa directamente en el IndexingHostedService |
| IMP-T3-006A | SQLite Tuning (cache_size, mmap) | Pendiente en SqliteConnectionFactory.cs | Se migra a EF Core configuration |
| IMP-T3-006B | ORDER BY COALESCE(DateTaken, CreatedAt) | Pendiente en PhotoRepository.cs | Se configura en EF Core query |
| IMP-T3-006C | BitmapImageConverter | Pendiente, 40 líneas custom | **Resuelto nativamente:** MAUI `<img src>` maneja descarga lazy + disposal |

---

# MEJORAS ADICIONALES DETECTADAS EN ANÁLISIS

| ID | Problema | Solución | Sprint |
|----|----------|----------|--------|
| IMP-A1 | Static Service Locator `App.Services.GetRequiredService()` | DI real via constructor injection en ViewModels y Pages | S0.2 |
| IMP-A2 | Code-behind pesado (MainWindow 238 lines, AlbumWindow 293, FaceManagement 404) | Lógica movida a ViewModels; Pages solo tienen binding | S1-S2 |
| IMP-A3 | Sub-windows creadas manualmente sin DI | Navegación Blazor nativa con DI | S0.2 |
| IMP-A4 | `Console.WriteLine` en agentes en vez de ILogger | Migrar a `ILogger<T>` inyectado | S1.2 |
| IMP-A5 | Serilog no configurado a pesar de tener el paquete | Configurar Serilog en Program.cs del backend | S0.1 |
| IMP-A6 | API Key de Anthropic en settings.json plano | Usar `SecureStorage` en MAUI + cifrado en backend | S2.5 |
| IMP-A7 | Sin tests | Proyecto de tests desde S0 | S0 |
| IMP-A8 | Sin migraciones de DB | EF Core migrations con versionado | S0.1 |
| IMP-A9 | Magic strings (agent names, filter types) | Enums y constantes tipadas en Shared project | S0.2 |
| IMP-A10 | Duplicate embedding serialization (FaceAgent + FaceManagement) | Unificar en servicio compartido | S2.3 |

---

# NUEVOS MÓDULOS SUGERIDOS (No existentes en WPF)

| Módulo | Descripción | Sprint | Esfuerzo |
|--------|-------------|--------|----------|
| **Mapa Geográfico** | Visualización de fotos con GPS en mapa interactivo | S3.4 | Medio |
| **Línea de Tiempo** | Vista cronológica por año/mes/día con eventos | S3.3 | Medio |
| **Estadísticas Avanzadas** | Distribución por cámara, ISO, apertura, lente | S2.4 | Bajo |
| **Fotos sin Geolocalización** | Sección para fotos sin GPS, con opción de asignar manual | S3.4 | Bajo |
| **Collage de Portada** | Portada de álbum con collage automático de 4 fotos | S2.1 | Bajo |
| **Modo Comparación** | Comparar dos fotos lado a lado con slider | S4 | Medio |
| **Álbumes Inteligentes** | "Sin álbum", "Recién añadidas", "Favoritos" | S2.1 | Bajo |
| **Exportación Selectiva** | Exportar selección de fotos a ZIP con metadatos JSON | S4 | Bajo |
| **Modo Oscuro Automático** | Cambio automático según hora del día | S0.2 | Bajo |

---

# RESUMEN DE SPRINTS

| Fase | Sprint | Duración Est. | Frontend | Backend | DB | QA |
|------|--------|---------------|----------|---------|----|----|
| **F0** | S0.1 Backend | 2 semanas | — | 16 tareas | 5 tareas | — |
| **F0** | S0.2 Frontend | 2 semanas | 13 tareas | — | — | — |
| **F1** | S1.1 Galería | 1 semana | 8 tareas | 4 tareas | — | Smoke test |
| **F1** | S1.2 Indexación | 2 semanas | 4 tareas | 14 tareas | — | Tests pipeline IA |
| **F2** | S2.1 Álbumes | 1 semana | 6 tareas | 4 tareas | — | CRUD + export |
| **F2** | S2.2 Tags | 1 semana | 5 tareas | 4 tareas | — | Filtros + búsqueda |
| **F2** | S2.3 Faces | 1 semana | 5 tareas | 4 tareas | — | Clustering + naming |
| **F2** | S2.4 Dashboard | 1 semana | 6 tareas | 4 tareas | — | Stats precisión |
| **F2** | S2.5 Settings | 0.5 semana | 7 tareas | — | — | Persistencia |
| **F3** | S3.1 Duplicados | 1 semana | 3 tareas | 2 tareas | — | Tests pHash |
| **F3** | S3.2 Similares | 0.5 semana | 2 tareas | 1 tarea | — | Tests cosine sim |
| **F3** | S3.3 Timeline | 1 semana | 3 tareas | 2 tareas | — | Event detection |
| **F3** | S3.4 Mapa Geo | 1.5 semanas | 5 tareas | 2 tareas | — | Coordenadas |
| **F4** | S4+ Futuro | Variable | Módulos varios | — | — | — |

**Total estimado:** ~13-15 semanas para Fase 0-3 completa.

---

# PRINCIPIOS ARQUITECTÓNICOS

1. **Backend localhost** — Todo corre en `localhost:5150`. Sin exposición a internet.
2. **Agentes locales** — ONNX, Tesseract, ImageSharp en el backend. Sin cambios en los 10 agentes existentes.
3. **Comunicación vía API** — Frontend nunca accede a DB directamente. Todo via REST + SignalR.
4. **Clean Architecture** — Domain → Application → Infrastructure → WebApi. Capas bien definidas.
5. **Layout moderno** — Inspirado en POS Web. Sidebar + Topbar + Dark Mode + MudBlazor.
6. **85% código reutilizado** — Common, Core agents, lógica de negocio intacta.
7. **WPF legacy coexiste** — La app WPF sigue funcionando durante la migración.

---

> **Documento generado por:** Orquestador Técnico Woola Group
> **Fecha:** Mayo 2026
> **Próxima acción:** Elegir Sprint inicial y comenzar implementación.
