# PokeScanner Architecture

## Overview

PokeScanner is a WPF (.NET 10) application designed for live camera capture and Pokemon TCG card identification using AI vision models and OCR.

## Project Structure

- **`PokeScanner/`** - Main application project (WPF, target framework: net10.0-windows10.0.19041)
- **`PokeScanner.Tests/`** - Unit tests (MSTest framework)

### Core Components

1. **MainWindow.xaml.cs** (~1305 lines, single-file app)
   - Handles UI interaction and camera capture
   - Manages LLM vision model integration via LiteLLM proxy
   - Coordinates Tesseract OCR fallback system
   - Performs TCGdex API lookups for card identification

2. **MainWindow.xaml**
   - WPF layout with ROI (Region of Interest) identifier
   - Displays video feed and scanned results
   - Includes control buttons and model selection dropdown

3. **Program.cs**
   - STAThread entry point
   - Application startup logic

## Data Flow Architecture

```
Camera Feed → ROI Extraction → LLM Vision Analysis (Primary) 
                ↓                                      ↓  
            OCR with Otsu (Fallback) → TCGdex API Lookup → Card Details Displayed
                                ↓  
                           Debug Image Saving
```

## Key Workflows

### Main Card Scanning Pipeline
1. **CaptureButton_Click** - Initiates scan cycle
2. **RunOcrAsync** - Manages scan process with cancellation support
3. **IdentifyWithLlmAsync** - Primary AI-based vision model analysis
   - Connects to LiteLLM proxy at `http://localhost:4000/chat/completions`
6. **LookupCardsAsync** - TCGdex API integration for card database lookup
   - Endpoint: `api.tcgdex.net/v2/en/cards`
   - Rate limiting: 3 concurrent requests via SemaphoreSlim

### fallback mechanism
- OCR with Otsu thresholding for text extraction
- Used when LLM vision analysis fails or provides uncertain results

## Camera Processing System

### Backend Selection Strategy
1. Attempts Media Foundation (MSMF) camera API first
2. Falls back to DirectShow if MSMF fails
3. Uses auto-detect as final fallback
4. Hardware transform optimization disabled via environment variable:
   `OPENCV_VIDEOIO_MSMF_ENABLE_HW_TRANSFORMS=0`

### ROI Processing
- 400×560 pixel centered gold rectangle overlay
- Image bounds conversion via `GetImageBounds()` method
- Frame rate controlled for stable processing

## Configuration and Dependencies

### Required Infrastructure
1. **LiteLLM Proxy** (mandatory)
   - Must be running at `http://localhost:4000/chat/completions`
   - Supported vision models:
     - qwen3-vl:30b-a3b-instruct-q4_K_M
     - gemma3:12b  
     - gemma4:12b (default)
   - `.env` configuration reads `LITELLM_SKELETON_KEY`

2. **Tesseract OCR Data** (gitignored)
   - Requires `tessdata/eng.traineddata` file
   - Located in project root: `PokeScanner/tessdata/`
   - Used only as fallback when LLM vision fails

3. **.env File** (copied to output directory)
   - Must be present at repo root during build
   - Key-value pairs used for configuration
   - Trims whitespace and strips "Bearer " prefix from keys
   - German set number pattern `NNN/NNN` extracted via regex with OCR digit correction map

## Testing Architecture

### Test Suite (`PokeScanner.Tests/`)
- **Framework**: MSTest v4.2.3
- **Test Pattern**: UI utility functions

**Current Tests**:
- `UiTestUtilitiesTests.cs` - Tests for text normalization utilities used in card name cleaning
  - `CleanCardName_ShouldNormalizeInput` (DataRow tests)
  - `CleanCardName_ShouldHandleEmptyOrNullInput`

### Test Project Configuration
```xml
<TargetFramework>net10.0-windows10.0.19041</TargetFramework>
<ProjectReference Include="..\PokeScanner\PokeScanner.csproj" />
<PackageReference Include="OpenCvSharp4" Version="4.10.0.20241108" />
```

## Dependencies

- **OpenCV (via OpenCvSharp4)** - Computer vision & camera capture
- **Tesseract OCR** - Optical character recognition fallback
  - leptonica-1.82.0.dll and tesseract50.dll native libraries
- **Newtonsoft.Json** - JSON serialization for API responses
- **Microsoft.NET.Sdk** - .NET SDK for build system

### NuGet Package Versions
- OpenCvSharp4: 4.10.0.20241108
- Microsoft.NET.Test.Sdk: 18.6.0
- MSTest.TestAdapter/MSTest.TestFramework: 4.2.3

## Debugging and Logging System

### Log Files
- **`pokescanner_debug.log`** - Located in binary output directory
  - Appended on each application run
  - Contains scan cycles, errors, and processing details

### Debug Images
- Saved to `{BaseDirectory}/debug/` directory
- PNG format with timestamp prefixes:
  - `name_crop.png`
  - `set_crop.png`
  - Various intermediate processing states (binarized versions)
- Helps diagnose OCR and vision model accuracy issues

## Threading Model

### Legacy Design Challenges
- Blocking `Dispatcher.Invoke` calls in capture loop
- `Task.Delay().Wait()` patterns instead of async/await
- CancellationTokenSource (`_scanCts`) integrated but with blocking semantics

### Synchronization primitives
- `SemaphoreSlim(rateLimit, 3)` for API rate limiting (3 concurrent requests max)