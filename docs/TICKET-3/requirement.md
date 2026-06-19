# TICKET-3: Fix Missing Data on Startup (Camera List, Models, Config Blank)

## Requirements
When debugging the application via `Program.cs`, the camera list and model selector are empty, and the settings panel shows blank fields. This is a startup initialization bug.

## Root Cause
`MainWindow` has two constructors:
1. `MainWindow()` (no-arg) — calls `LoadSettings()`, `LoadEnv()`, wires `Loaded` and `Closed` events
2. `MainWindow(ITcgdexApiService)` (DI constructor) — only sets `_tcgdexApiService` and calls `InitializeComponent()`

`Program.cs` uses `new MainWindow(tcgdexApiService)`, which invokes the DI constructor. This constructor skips:
- `LoadSettings()` → settings.json never loaded, models list empty
- `Loaded += OnLoaded` → `PopulateCameraList()` and `PopulateModelSelectorFromSettings()` never called
- `Closed += OnClosed` → settings never saved on close

## Acceptance Criteria
- [ ] Camera list populates on startup
- [ ] Model selector is populated from default models or settings.json
- [ ] Settings panel shows previously saved values (or defaults)
- [ ] LLM API key is loaded from `.env` if not in settings.json
- [ ] Settings are saved on window close
- [ ] Application builds and runs correctly
