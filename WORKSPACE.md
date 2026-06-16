# PokeScanner — Workspace Log

## 2026-06-14 — Initial workspace log created

- Converted unit tests from xUnit to MSTest (UiTestUtilitiesTests.cs)
- Removed unused PokeScanner.Core project
- Consolidated test files into PokeScanner.Tests project
- Fixed test project TFM mismatch (net10.0 → net10.0-windows10.0.19041)
- Updated MSTest packages to 4.2.3
- Removed unused files: plan.txt, Dockerfile, .dockerignore, appsettings.json, appsettings.Development.json, requirement.txt
- Added PokeScanner.Tests to solution file (.sln)
- Created this workspace tracking file (WORKSPACE.md)
- Updated AGENTS.md to include workspace tracking convention

## 2026-06-14 — Settings/Configuration feature scope analysis

### Goal
Replace hardcoded values (LLM URL, API key source, model list) with user-configurable settings in a GUI settings panel. Persisted between runs via JSON file.

### Current state — hardcoded values to externalize

| Config key                | Current value                                      | Location (file:line)                     |
| ------------------------- | -------------------------------------------------- | ---------------------------------------- |
| `llm_url`                 | `"http://localhost:4000/chat/completions"`         | `IdentifyWithLlmAsync`:1135              |
| `llm_key`                 | `.env` → `LITELLM_MASTER_KEY` or `LITELLM_SKELETON_KEY` | `LoadEnv`:59-89                   |
| `available_models`        | 3 hardcoded strings (`qwen3-vl:30b...`, `gemma3:12b`, `gemma4:12b`) | `AvailableModels`:42-47         |
| `default_model`           | `"gemma3:12b"`                                     | `_selectedModel`:41                      |

### Implementation plan (todo)

1. **Create `LlmSettings` record** — POCO for serializing/deserializing config
   - Fields: `llmBaseUrl`, `llmApiKey`, `availableModels` (string[]), `defaultModel`
   - Storage: `<BaseDirectory>/settings.json`
   - Default: current hardcoded values

2. **Add settings persistence** — load/save methods in MainWindow.xaml.cs
   - `LoadSettings()` on startup (merge defaults with user config)
   - `SaveSettingsAsync()` after any change
   - If no file exists → create from defaults

3. **Add settings UI panel in MainWindow.xaml**
   - New section as grid row or toggleable panel near the ToolBar
   - Text boxes: LLM URL, API Key (Password field), Model list (newline-separated)
   - Default model dropdown / text box
   - Save button + "Apply" that re-inits internal state

4. **Wire settings into existing flow**
   - Replace `LoadEnv()` call with new settings-based key retrieval (`llmBaseUrl`/`llmApiKey`)
   - Settings panel populates `IdentifyWithLlmAsync` URL and model list
   - If API key filled in → send as Bearer header; if blank → omit Authorization entirely

5. **Keep `.env` fallback** for backward compatibility
   - If `settings.json` does not exist AND no settings have been saved, read from `.env` (existing behavior)

### Completed

- [x] Scope analysis — identified all 4 hardcoded values
- [x] Created `SettingsContainer` class — POCO for JSON serialization of settings
- [x] Added `LoadSettings()` — reads `settings.json` from base directory, falls back to defaults + `.env`
- [x] Added `SaveSettingsAsync()` — serializes current UI values to `settings.json`
- [x] Added `PopulateModelSelectorFromSettings()` — populates model dropdown from settings
- [x] Added Settings panel UI (toggleable) in MainWindow.xaml — gear button in toolbar toggles expandable panel
- [x] Settings panel fields: LLM URL (textbox), API Key (password), model list (multiline text), default model (combobox), Save & Apply button
- [x] Settings applied immediately to LLM flow (URL + auth header conditionally)
- [x] `.env` fallback preserved — API key from settings takes priority, else loads from `.env`
- [x] `settings.json` saved on window close and on "Save & Apply"

### 2026-06-14 — Save flow fix (models not persisting)

**Bug**: Adding new models in the settings panel textbox didn't save. Root cause: `SettingsSave_Click` called `SaveSettingsAsync()` which read from `ModelSelector.Items` (the OLD dropdown, not yet updated with new models) and overwrote `_appSettings.AvailableModels` before `PopulateModelSelectorFromSettings()` ran.

**Fix**: 
- `SaveSettingsAsync()` now serializes `_appSettings` directly — pure persistence, no UI reads.
- `SettingsSave_Click` reads ALL UI controls (URL, API key, models textbox, default model), updates `_appSettings`, calls `PopulateModelSelectorFromSettings()` *before* `SaveSettingsAsync()`.
- `OnClosed` just saves `_appSettings` as-is — no stale UI reads.

(End of file - total 43 lines)
