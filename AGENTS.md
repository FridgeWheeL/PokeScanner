# PokeScanner — Agent Guide

## What

WPF (.NET 10, `net10.0-windows10.0.19041.0`) Pokemon TCG card scanner. Captures live camera feed → vision LLM (primary) → Tesseract OCR (fallback) → TCGdex API lookup → open in Collectr/TCGplayer.

## Build & run

```powershell
cd PokeScanner
dotnet build
dotnet run
```

Single `.csproj`, single project, no tests.

## Prerequisites (easy to miss)

1. **LiteLLM proxy** must be running at `http://localhost:4000/chat/completions` with a vision model (llava:13b, gemma3:12b, qwen3-vl:30b). Loading `.env` reads `LITELLM_SKELETON_KEY` (not `LITELLM_MASTER_KEY`).
2. **tessdata/** directory with `eng.traineddata` (gitignored, download manually). Tesseract is fallback only.
3. **`.env`** at repo root is copied to output dir by csproj.

## Key files

- `PokeScanner/MainWindow.xaml.cs` — ~1305 lines, single-file app. Everything is here.
- `PokeScanner/MainWindow.xaml` — WPF layout (ROI = 400×560 centered gold rectangle).
- `PokeScanner/Program.cs` — `STAThread` entry point.

## Architecture

- `CaptureButton_Click` → `RunOcrAsync` → `IdentifyWithLlmAsync` (LLM vision) → Tesseract `OcrWithOtsu` fallback → `LookupCardsAsync` (TCGdex API, semaphore=3, no key needed).
- LLM model selectable: `qwen3-vl:30b-a3b-instruct-q4_K_M`, `gemma3:12b`, `gemma4:12b` (default).
- Camera backends tried in order: MSMF → DShow → auto-detect.
- `OPENCV_VIDEOIO_MSMF_ENABLE_HW_TRANSFORMS=0` set before camera open.
- ROI identifier is a `Rectangle` overlay — coordinates converted via `GetImageBounds()`.

## Debug output

- `pokescanner_debug.log` in binary output dir (appended every run).
- Per-scan debug images saved to `{BaseDirectory}/debug/` (pngs with timestamps: name crop, set crop, binarized versions).

## Notable quirks

- No CancellationTokenSource was used originally — current code has `_scanCts` wired up.
- Capture loop uses blocking `Dispatcher.Invoke` and `Task.Delay().Wait()` (legacy design, not async).
- TCGdex API (`api.tcgdex.net/v2/en/cards`) needs no auth.
- German set number pattern `NNN/NNN` extracted via regex and OCR digit correction map.
- .env keys are trimmed and `Bearer ` prefix stripped.
