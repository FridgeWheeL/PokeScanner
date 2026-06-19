# TICKET-3: Fix Missing Data on Startup (Camera List, Models, Config Blank)

## Overview

Extract the shared initialization logic from the no-arg constructor into a private method so that the DI constructor used by `Program.cs` performs the same startup steps (load settings, load env, wire `Loaded`/`Closed` events) as the no-arg constructor.

## Areas Affected

Only **one file** is modified: `PokeScanner/MainWindow.xaml.cs`. No other source files, test files, or configuration files are touched.

- `MainWindow.xaml.cs` — Extract lines 55–59 from `MainWindow()` into a private helper and call it from both constructors.
- `PokeScanner/Program.cs` — No change (already correctly invokes the DI constructor).
- `PokeScanner.Tests/` — No change needed (no test instantiates `MainWindow` directly).

## New Items

None.

## Modified Items

### `PokeScanner/MainWindow.xaml.cs`

| What | Change |
|------|--------|
| **New private method `InitializeMainWindow()`** | Extract lines 55–59 (log init + `LoadSettings()` + `Loaded`/`Closed` event wiring) from the no-arg constructor into a `private void InitializeMainWindow()` method. |
| **No-arg constructor `MainWindow()`** | Replace lines 55–59 with a call to `InitializeMainWindow()`. |
| **DI constructor `MainWindow(ITcgdexApiService)`** | Insert a call to `InitializeMainWindow()` after `InitializeComponent()`. |

**What this accomplishes:**

1. `LoadSettings()` (line 94) runs → `_appSettings` is populated from `settings.json`. Defaults are set for `LlmBaseUrl`, `AvailableModels`, and `DefaultModel`. If no API key is in settings, `LoadEnv()` is called to load from `.env`.
2. `Loaded += OnLoaded` → when the window loads, `OnLoaded` fires, calling `PopulateCameraList()` and `PopulateModelSelectorFromSettings()`.
3. `Closed += OnClosed` → when the window closes, `SaveSettingsAsync()` is called.

## Implementation Order

1. **Add `InitializeMainWindow()` method** in `MainWindow.xaml.cs` — extract the common four statements into the new private method.
2. **Update `MainWindow()`** — replace the four extracted lines with a call to `InitializeMainWindow()`.
3. **Update `MainWindow(ITcgdexApiService)`** — add `InitializeMainWindow()` call after `InitializeComponent()`.
4. **Build and verify** — `dotnet build` succeeds.
5. **Run tests** — `dotnet test` passes (all existing tests continue to pass).

No dependencies between steps — the order strictly eliminates duplication first, then applies the fix.

## Edge Cases & Risks

| Risk | Mitigation |
|------|------------|
| `LoadEnv()` is called from inside `LoadSettings()` — ensures env key is loaded even if settings.json lacks it. This is already handled at line 126–127. | No change needed; flow is preserved because `LoadSettings()` calls `LoadEnv()` if key is empty. |
| `LogPath` (static readonly, line 18) is initialized at class load time before constructors run. | Safe — used only in instance method `InitializeMainWindow()`. |
| `LoadSettings()` uses `_appSettings` field (initialized at line 50 with `new()`). | Safe — field is set before any event handlers run. |
| No-arg constructor is still used by XAML designer or elsewhere. | Behavior is identical — same operations in same order, just extracted. |
| Log file initialization (lines 55–56) runs in the DI constructor too. | Desirable — consistent logging regardless of which constructor is used. |

## Test Strategy

No test changes are needed:

- **`ApiTests.cs`** — tests `ITcgdexApiService` via mocked interface; never creates `MainWindow`. Unaffected.
- **`LlmTests.cs`** — tests JSON parsing/extraction logic; never creates `MainWindow`. Unaffected.
- **`UiTestUtilitiesTests.cs`** — tests static utility methods; never creates `MainWindow`. Unaffected.

To verify the fix, run `dotnet test` — all existing tests should pass.

**(Optional manual QA)** After the fix, launching the app should show:

1. Camera dropdown populated (WinRT or registry fallback)
2. Model selector populated from settings.json or default models list
3. Settings panel (when toggled) showing saved values
4. API key loaded from `.env` if not in settings.json
5. Settings saved on window close
