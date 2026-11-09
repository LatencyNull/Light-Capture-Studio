# Video Annotations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

## Goal
Enable users to add lightweight, GPU-accelerated annotations (arrows, text, pen drawings, rectangles, Knox solid redaction) directly onto the video in `VideoPreviewWindow`, preview them in real time during playback, and cleanly burn them into the exported MP4 or GIF clip without degrading performance or bloating the application.

## Architecture & Design Decisions
1. **Zero-Lag UI Layer**:
   - Inside `VideoPreviewWindow.xaml`, `VideoViewport` tightly bounds `MediaElement` based on natural video aspect ratio.
   - `DrawingCanvas` is overlaid directly onto `VideoViewport`.
   - When no drawing tool is selected (pointer mode), clicking toggles play/pause as before.
   - When an annotation tool (Arrow, Text, Pen, Rect, Redact) is active, clicking/dragging draws annotations on the video and automatically pauses playback if playing.
2. **One UI Adaptive Floating Annotation Toolbar**:
   - A floating pill at the top-center of the video container with tools:
     - 👆 Cursor / Play Mode
     - ↗️ Flecha (Arrow)
     - 🔤 Texto (Text)
     - ✏️ Pluma (Freehand Pen)
     - 🔲 Rectángulo (Rectangle)
     - 🔒 Censura Knox (Solid block redaction)
     - Separator
     - 🎨 Color selector (Red, Blue, Green, Yellow, Purple, White)
     - ↩️ Deshacer (Undo, Ctrl+Z)
     - 🗑️ Limpiar todo (Clear)
3. **Hardware-Accelerated Zero-Bloat Export**:
   - If annotations exist, `DrawingCanvas` renders the overlay into a full-resolution transparent PNG matching the video's natural dimensions (`NaturalVideoWidth` x `NaturalVideoHeight`).
   - `VideoTrimmerService.cs`:
     - **MP4**: If FFmpeg is available on system, burns overlay with `-filter_complex "[0:v][1:v]overlay=0:0" -preset veryfast -c:a copy`; fallback to native Windows 10/11 WinRT `MediaComposition.OverlayLayers`.
     - **GIF**: Uses native C# GDI+ frame-by-frame renderer (`gfx.DrawImage(overlayImage)`).
   - If no annotations exist, uses the instant stream copy / standard trim with zero overhead.

---

## File Structure & Proposed Changes

- **Modified Files**:
  - `src/LightCapture.App/Controls/DrawingCanvas.cs`:
    - Add `HasElements` boolean property.
    - Add `ExportOverlayToPng(string path, int pixelWidth, int pixelHeight)` to render all current elements at high resolution to a transparent PNG.
  - `src/LightCapture.App/Services/VideoTrimmerService.cs`:
    - Add optional `overlayPngPath` parameter to `TrimAsync`, `TrimMp4Async`, and `TrimGifAsync`.
    - Implement overlay burning for MP4 (FFmpeg fast pass + WinRT `MediaComposition` fallback) and GIF (GDI+ overlay composite per frame).
  - `src/LightCapture.App/Views/VideoPreviewWindow.xaml`:
    - Refactor `MediaVideoPlayer` container to use `VideoContainer` and centered `VideoViewport`.
    - Add `DrawingCanvas` (`VideoDrawingCanvas`) and floating `TextBox` (`VideoFloatingInput`).
    - Add floating `AnnotationToolbar` with tool toggle buttons, color palette, undo, and clear.
  - `src/LightCapture.App/Views/VideoPreviewWindow.xaml.cs`:
    - Implement `UpdateVideoViewportLayout()` on `MediaOpened` and `VideoContainer_SizeChanged`.
    - Wire annotation tool selection, drawing events, and floating text commit.
    - Connect `GetEffectiveVideoPathAsync()` to export with overlay if `VideoDrawingCanvas.HasElements`.
  - `tests/LightCapture.Tests/VideoTrimmerServiceTests.cs`:
    - Add unit tests verifying overlay parameter handling and export contract.

---

## Tasks

- [ ] **Task 1: Add Overlay Export Capability to DrawingCanvas**
  - [ ] Add `HasElements` getter (`_elements.Count > 0`).
  - [ ] Add `ExportOverlayToPng(string filePath, int targetPixelWidth, int targetPixelHeight)`.
  - [ ] Verify build and tests via `ctx_execute`.

- [ ] **Task 2: Support Overlay Burning in VideoTrimmerService**
  - [ ] Update `VideoTrimmerService.TrimAsync` to accept optional `string? overlayPngPath = null`.
  - [ ] Implement MP4 overlay burning via FFmpeg (with `MediaComposition` fallback).
  - [ ] Implement GIF overlay burning in `TrimGifAsync`.
  - [ ] Add unit test in `VideoTrimmerServiceTests.cs`.
  - [ ] Verify build and tests via `ctx_execute`.

- [ ] **Task 3: Implement Annotation UI in VideoPreviewWindow.xaml**
  - [ ] Wrap `MediaVideoPlayer` in centered `VideoViewport` within `VideoContainer`.
  - [ ] Add `DrawingCanvas` (`VideoDrawingCanvas`) over `MediaVideoPlayer`.
  - [ ] Add `TextBox` (`VideoFloatingInput`) for text annotations.
  - [ ] Add floating `AnnotationToolbar` with One UI styling and icons (Pointer, Arrow, Text, Pen, Rect, Redact, Palette, Undo, Clear).

- [ ] **Task 4: Implement VideoPreviewWindow Annotation Controller Code-Behind**
  - [ ] Implement `UpdateVideoViewportLayout()` to match natural video aspect ratio without letterbox deadzones.
  - [ ] Handle tool switching: when in Pointer mode, clicking toggles play/pause; when drawing tool is active, mouse draws on canvas and pauses video.
  - [ ] Implement floating text input (`VideoFloatingInput`) for clicking to type text annotations.
  - [ ] Implement palette cycling and Undo (Ctrl+Z) / Clear actions.
  - [ ] Wire `GetEffectiveVideoPathAsync()` to export overlay PNG and pass to `VideoTrimmerService`.

- [ ] **Task 5: Verification, Building, and Deployment**
  - [ ] Run `dotnet test` via `ctx_execute` ensuring all unit tests pass.
  - [ ] Run `dotnet publish` to standalone single-file binary.
  - [ ] Safely replace `LightCapture.exe` and relaunch process.
  - [ ] Commit all changes to git and report status to user.
