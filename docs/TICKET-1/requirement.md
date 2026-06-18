# TICKET-1: Remove OCR Functionality

## Requirements
- Remove all Tesseract OCR code from the production codebase
- Remove the Tesseract NuGet package dependency
- Remove the `tessdata/` directory handling (native language data)
- Remove all OCR-related test code
- Update documentation to remove OCR references
- Ensure the application still compiles and functions (LLM-based card identification remains)

## Acceptance Criteria
- [ ] `Tesseract` NuGet package removed from `PokeScanner.csproj`
- [ ] `tessdata` build action removed from `PokeScanner.csproj`
- [ ] All `using Tesseract;` imports removed
- [ ] `TesseractEngine` field and all OCR methods removed from `MainWindow.xaml.cs`:
  - `RunOcrAsync`
  - `GetOcrEngine`
  - `PreprocessForOcr`
  - `OcrWithOtsu`
  - `ParseOcrFields`
  - `ExtractSetNumber`
  - `GetDigitMap`
- [ ] All references to OCR methods removed from scan pipeline in `MainWindow.xaml.cs`
- [ ] `OcrTests.cs` deleted
- [ ] OCR references removed from `ARCHITECTURE.md`
- [ ] OCR references removed from `WORKSPACE.md`
- [ ] `tessdata/` entry removed from `.gitignore`
- [ ] Project builds successfully
- [ ] Existing tests pass

## Notes
- The LLM-based card identification pipeline should remain intact
- This is a pure removal task — no replacement code needed
- See comprehensive OCR code map from codebase exploration
