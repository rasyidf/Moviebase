# Moviebase

All-in-one movie library manager. Scan your folders, fetch metadata from TMDB, rename files, download posters — all from a single window.

![WinUI 3](https://img.shields.io/badge/WinUI_3-blue) ![.NET 10](https://img.shields.io/badge/.NET_10-purple) ![License](https://img.shields.io/badge/license-MIT-green)

## Features

- **Smart scanning** — detects movie titles, year, resolution, codec, and source from filenames using [Terka](https://github.com/rasyidf/Trka)
- **Series grouping** — automatically groups movies in series folders with collapsible sections
- **TMDB lookup** — fetches title, plot, genre, IMDB ID, and poster art
- **Poster thumbnails** — shows poster art in the movie list and detail panel
- **Batch rename** — rename files and folders by pattern (`{Title} ({Year})`)
- **Poster download** — saves poster.jpg to each movie folder
- **Settings** — configurable API key, file extensions, rename patterns

## Screenshot

| Movie List | Detail Panel |
|---|---|
| Poster thumbnails, quality badges, series groups | TMDB metadata, poster, technical info |

## Getting Started

### Prerequisites

- Windows 10 1903+ with Developer Mode enabled
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A [TMDB API key](https://www.themoviedb.org/settings/api) (free)

### Clone

```bash
git clone --recurse-submodules https://github.com/rasyidf/Moviebase.git
cd Moviebase
```

### Build & Run

```bash
dotnet run
```

Or use the included script:

```powershell
.\BuildAndRun.ps1
```

### Configuration

On first run, click **Settings** (overflow menu `...`) and enter your TMDB API key.

## Project Structure

```
Moviebase/
├── App.xaml / .cs              — Entry point
├── MainWindow.xaml / .cs       — Single-page UI
├── Models/                     — MovieEntry, AppSettings
├── ViewModels/                 — MainViewModel (CommunityToolkit.Mvvm)
├── Services/
│   ├── TmdbService.cs          — TMDB API client
│   ├── MovieScanner.cs         — Folder scanner + Terka analysis
│   └── MovieRenamer.cs         — Batch rename
├── Terka/                      — Git submodule (filename analyzer)
├── SampleMovies/               — Test data
└── TRACKER.md                  — Development roadmap
```

## Dependencies

| Package | Purpose |
|---------|---------|
| Microsoft.WindowsAppSDK 2.2 | WinUI 3 runtime |
| CommunityToolkit.Mvvm 8.4 | MVVM framework |
| [Terka](https://github.com/rasyidf/Trka) | Filename analysis (submodule) |

## Roadmap

See [TRACKER.md](TRACKER.md) for the full development plan including:

- Search/filter bar
- Drag-and-drop folder scanning
- NFO file generation (Kodi/Jellyfin/Plex)
- Export to CSV/JSON
- Manual TMDB search
- TMDB collection auto-detection
- Watch status tracking
- Subtitle detection
- Duplicate detection

## License

MIT
