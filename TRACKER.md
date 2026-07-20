# Moviebase — Development Tracker

## Project

- **Location:** `D:\dev\net\Moviebase`
- **Type:** Unpackaged WinUI 3 (.NET 10, CommunityToolkit.Mvvm)
- **Analyzer:** Terka (`D:\dev\net\Terka`) — GuessIt-style filename parser
- **Run:** `dotnet run --project Moviebase.csproj`

---

## Done

### Core App (v0.1)

- [x] Unpackaged WinUI 3 project scaffold (net10.0, WindowsPackageType=None, Mica backdrop)
- [x] Custom title bar with folder path display
- [x] CommunityToolkit.Mvvm ViewModel with async RelayCommands
- [x] AppSettings JSON persistence (API key, patterns, extensions)

### Scanning & Analysis

- [x] Folder scanner — detects movie files in sub-folders
- [x] Series folder grouping — nested folder structure (e.g. `Series Name/Movie (Year)/file.mkv`)
- [x] Terka integration — extracts title, year, resolution, source, codec, release group, edition from filenames
- [x] Fallback: folder name parsing when filename title extraction fails

### TMDB Integration

- [x] Search movies by title + year
- [x] Fetch full metadata (genre, plot, IMDB ID, poster, alternative names)
- [x] Poster thumbnail URLs (w92 for list, w342 for detail)
- [x] Batch poster download to `poster.jpg` in each movie folder

### Organize

- [x] Batch rename files by pattern (`{Title} ({Year})`)
- [x] Batch rename folders by pattern
- [x] Swap "The" prefix to end option

### UI

- [x] ListView with DataTemplate — proper WinUI selection/hover styling
- [x] Poster thumbnails (40×56px) in list items
- [x] Collapsible Expander groups for series
- [x] Detail panel — poster, title, year, genre, IMDB, plot, quality badge, filename
- [x] Toolbar grouped by workflow: Scan → Look Up → Get Posters → Rename All
- [x] Settings overlay panel (API key, extensions, patterns, swap toggle)
- [x] Status bar with movie count + progress
- [x] Empty states (no folder loaded, no movie selected)

### Test Data

- [x] `SampleMovies/` — 12 standalone movies + 2 series (LOTR 3, John Wick 4)
- [x] Various naming patterns: `Title (Year)`, `Title Year`, `Title.Year.Quality`, scene names

---

## Planned

### Low Effort

| # | Feature | Notes |
|---|---------|-------|
| 1 | Search/filter bar | TextBox above list, filter by title as you type |
| 2 | Drag-and-drop folder | Drop folder onto window to scan |
| 3 | Export to CSV/JSON | Export library metadata |
| 4 | Subtitle detection | Show .srt/.ass presence per movie |
| 5 | Duplicate detection | Flag same title in multiple folders |
| 6 | Folder thumbnail | Write desktop.ini + folder.jpg for Explorer thumbnails |
| 7 | Multi-language title | Show alternative titles from TMDB |
| 8 | Progress persistence | Save scan results to .moviebase.json, reload instantly |

### Medium Effort

| # | Feature | Notes |
|---|---------|-------|
| 9 | Manual TMDB search | Right-click → search by different name, pick from results |
| 10 | NFO file generation | Kodi/Jellyfin/Plex compatible .nfo metadata files |
| 11 | Watch status tracking | Mark watched/unwatched, persist to JSON |
| 12 | Batch move to series | Select movies → create series folder → move them in |
| 13 | TMDB collection detection | Auto-detect franchise grouping from TMDB collections API |

---

## Architecture

```
Moviebase/
├── App.xaml / .cs              — Entry point, exception handler
├── MainWindow.xaml / .cs       — Single-page UI (list + detail + settings)
├── Models/
│   ├── MovieEntry.cs           — Domain model (title, year, quality, TMDB data)
│   ├── MovieEntryDisplay.cs    — ListView binding DTO
│   └── AppSettings.cs          — JSON settings persistence
├── ViewModels/
│   └── MainViewModel.cs        — ObservableObject, RelayCommands
├── Services/
│   ├── TmdbService.cs          — TMDB API client (search, details, posters)
│   ├── MovieScanner.cs         — Folder scanner + Terka analysis
│   └── MovieRenamer.cs         — Batch file/folder rename
├── Assets/
├── Package.appxmanifest
└── Moviebase.csproj            — References Terka project

Terka/ (D:\dev\net\Terka)
├── src/Terka/
│   ├── GuessIt.cs              — Main entry point
│   ├── Tokenizer.cs            — Filename tokenizer
│   ├── GuessResult.cs          — Result model
│   ├── MediaType.cs            — Movie/Episode enum
│   └── Matchers/               — 14 matchers (episode, codec, source, etc.)
└── tests/Terka.Tests/
```

---

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.WindowsAppSDK | 2.2.0 | WinUI 3 runtime |
| Microsoft.Windows.SDK.BuildTools | 10.0.28000.2270 | Build tooling |
| Microsoft.Windows.SDK.BuildTools.WinApp | 0.4.0 | dotnet run support |
| CommunityToolkit.Mvvm | 8.4.2 | MVVM (ObservableObject, RelayCommand) |
| Terka (ProjectReference) | 1.0.0 | Filename analysis |

---

## Notes

- `winapp run` with `--debug-output` crashes on Window creation (known AppX layout issue). Use `dotnet run` or `winapp run --detach`.
- ReactiveUI was attempted but abandoned (v20 missing Uwp assembly, v23 TypeInitializationException on .NET 10). CommunityToolkit.Mvvm works cleanly.
- Terka targets netstandard2.0 for broad compatibility.
