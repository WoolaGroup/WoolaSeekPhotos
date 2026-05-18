# Woola Photos

Photo manager desktop application with AI-powered analysis, built with .NET MAUI Blazor Hybrid + ASP.NET Core.

## Tech Stack

- **Frontend:** .NET MAUI Blazor Hybrid (Windows/Android/iOS)
- **Backend:** ASP.NET Core Web API (.NET 10)
- **Database:** SQLite (local, no server needed)
- **AI/ML:** ONNX Runtime (YOLOv8, FaceNet, all-MiniLM-L6-v2), Tesseract OCR, ImageSharp
- **UI:** MudBlazor components with custom design system

## Features

- **50+ modules** including gallery, albums, faces, tags, duplicates, timeline, map, slideshow
- **8 AI agents** for automatic photo analysis (metadata, object detection, face recognition, OCR, quality assessment, scene detection, geo-location)
- **Real-time indexing** with SignalR progress
- **Watch folder** for automatic import
- **Edit** photos (rotate, flip, regenerate thumbnails)
- **Backup/export** database + thumbnails + photos

## Quick Start

### Prerequisites

- .NET 10 SDK
- Visual Studio 2022+ (optional, for MAUI development)

### Run the Backend

```bash
cd src\Woola.PhotoManager.Backend.WebApi
dotnet run
```

The API starts at `http://localhost:5150` with Swagger UI at `/swagger`.

### Run the Frontend (Windows)

```bash
cd src\Woola.PhotoManager.Frontend.MAUI
dotnet build -f net10.0-windows10.0.19041.0
dotnet run -f net10.0-windows10.0.19041.0
```

### Configuration

Edit `src\Woola.PhotoManager.Backend.WebApi\appsettings.json`:

```json
{
  "WoolaPhotos": {
    "DatabasePath": "",           // leave empty for default (%APPDATA%/WoolaPhotos/)
    "ThumbnailSize": 512,
    "JpegQuality": 80,
    "MaxUploadSizeMb": 50
  },
  "Jwt": {
    "Key": "your-secret-key-here",  // change in production
    "ExpiryHours": 24
  },
  "Auth": {
    "DefaultUsername": "admin",
    "DefaultPassword": "woola2024"  // change in production
  }
}
```

## Project Structure

```
src/
├── Woola.PhotoManager.Backend.Domain/      # Entities, ValueObjects
├── Woola.PhotoManager.Backend.Application/  # CQRS, Services, Interfaces
├── Woola.PhotoManager.Backend.Infrastructure/ # EF Core, Repositories
├── Woola.PhotoManager.Backend.WebApi/       # API Controllers, Middleware
├── Woola.PhotoManager.Frontend.MAUI/        # MAUI Blazor Hybrid App
├── Woola.PhotoManager.Shared/               # DTOs, Enums, Configuration
├── Woola.PhotoManager.Common/               # Legacy: ONNX, ImageSharp, Tesseract
├── Woola.PhotoManager.Core/                 # Legacy: Agents, Indexer, Services
├── Woola.PhotoManager.Domain/               # Legacy: Old Domain entities
└── Woola.PhotoManager.Infrastructure/       # Legacy: Old Dapper repos
tests/
└── Woola.PhotoManager.Tests/               # Unit tests (xUnit)
```

## API Endpoints

| Category | Endpoints |
|----------|-----------|
| **Auth** | `POST /api/v1/auth/login` |
| **Photos** | `GET /api/v1/photos`, `GET /api/v1/photos/{id}`, `DELETE /api/v1/photos/{id}`, `POST /api/v1/photos/index` |
| **Albums** | `CRUD /api/v1/albums`, `GET/POST /api/v1/albums/{id}/photos` |
| **Tags** | `GET /api/v1/tags`, `GET /api/v1/tags/{name}/photos` |
| **Faces** | `GET /api/v1/faces`, `PUT /api/v1/faces/{id}/name` |
| **Dashboard** | `GET /api/v1/dashboard/stats`, `/top-tags`, `/photos-by-month` |
| **Edit** | `POST /api/v1/edit/{id}/rotate`, `/flip`, `/date`, `/rename` |
| **Search** | `GET /api/v1/search?q=&camera=&isoMin=&dateFrom=` |
| **Batch** | `POST /api/v1/batch/delete`, `/tag`, `/rate`, `/add-to-album` |
| **Trash** | `GET /api/v1/trash`, `POST /api/v1/trash/{id}/restore` |
| **Upload** | `POST /api/v1/upload` (multipart) |
| **Backup** | `GET /api/v1/backup/export` |
| **Export** | `POST /api/v1/export/photos` (ZIP), `POST /api/v1/export-csv/photos` |
| **Stats** | `GET /api/v1/stats/cameras`, `/lenses`, `/yearly`, `/iso-distribution` |
| **Geo** | `GET /api/v1/geo/photos` |
| **Events** | `GET /api/v1/events` |
| **Smart Albums** | `GET /api/v1/smart-albums`, `/smart-albums/{id}/photos` |
| **Watch** | `POST /api/v1/watch/start`, `/watch/stop` |

## License

Internal use — Woola Group
