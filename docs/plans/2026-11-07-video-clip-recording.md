# Video Clip Recording Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

## Overview
Add a native screen video recording system to capture short clips (5-60 seconds in MP4/GIF format) of any selected area on screen, optimized for instant sharing to WhatsApp and Discord. Supports both system audio recording (WASAPI loopback) and silent GIF-style clips, with universal hardware acceleration (NVIDIA/AMD/Intel) and automatic CPU fallback for PCs without a dedicated GPU.

---

## File Structure

```
src/
├── LightCapture.Core/
│   ├── Services/
│   │   ├── IVideoRecordingService.cs           # Core interface for video recording
│   │   └── ScreenRecorderService.cs            # Implementation using Windows Media Foundation via ScreenRecorderLib
├── LightCapture.App/
│   ├── Assets/Icons/
│   │   └── video.png                           # 160x160 One UI emoji video camera icon
│   ├── Controls/
│   │   ├── LightshotToolbar.xaml               # Add 🎥 BtnRecordVideo to horizontal bar
│   │   └── LightshotToolbar.xaml.cs            # Event wiring for video recording
│   ├── Views/
│   │   ├── RecordingControlWidget.xaml         # Floating pill [ 🔴 REC 00:05 ] [ 🔊 ] [ ⏹️ ] [ ❌ ]
│   │   ├── RecordingControlWidget.xaml.cs      # Widget logic (timer, audio toggle, stop, cancel)
│   │   ├── RecordingBorderOverlay.xaml         # Transparent click-through border highlighting recording area
│   │   ├── RecordingBorderOverlay.xaml.cs      # Frame position & pulsating animation
│   │   ├── VideoPreviewWindow.xaml             # One UI Video preview player & share hub
│   │   ├── VideoPreviewWindow.xaml.cs          # Looping playback, Discord/WhatsApp share, Copy, Save
│   │   ├── OverlayWindow.xaml.cs               # Connect toolbar click to start recording flow
tests/
└── LightCapture.Tests/
    └── VideoRecordingServiceTests.cs           # Unit tests for recording service & state machine
```

---

## Task 1: Video Recording Core Service (`IVideoRecordingService` & `ScreenRecorderService`)

**Files:**
- Create: `src/LightCapture.Core/Services/IVideoRecordingService.cs`
- Create: `src/LightCapture.Core/Services/ScreenRecorderService.cs`
- Create: `tests/LightCapture.Tests/VideoRecordingServiceTests.cs`

- [ ] **Step 1.1:** Define `IVideoRecordingService` interface with methods: `StartRecordingAsync`, `StopRecordingAsync`, `CancelRecordingAsync`, `IsRecording`, `DurationTick`, `RecordingComplete`, `RecordingError`.
- [ ] **Step 1.2:** Write unit tests in `VideoRecordingServiceTests.cs` verifying initial state, duration updates, validation when bounds are zero, and cancellation.
- [ ] **Step 1.3:** Implement `ScreenRecorderService` wrapping `ScreenRecorderLib.Recorder`:
  - Calculate normalized crop coordinates for virtual screen bounds.
  - Configure `RecorderOptions`: H.264 MP4 output, 30 FPS, high quality bitrate.
  - Configure `AudioOptions`: Loopback capture of system audio if `captureAudio == true`.
  - Wire timer for duration ticks.
- [ ] **Step 1.4:** Run `dotnet test tests/LightCapture.Tests -c Release -p:Platform=x64` to verify tests pass.

---

## Task 2: Video Camera Icon & Toolbar Button

**Files:**
- Create: `src/LightCapture.App/Assets/Icons/video.png`
- Modify: `src/LightCapture.App/Controls/LightshotToolbar.xaml`
- Modify: `src/LightCapture.App/Controls/LightshotToolbar.xaml.cs`

- [ ] **Step 2.1:** Download Apple/One UI 3D emoji video camera icon (U+1F4F9 `1f4f9.png`) into `src/LightCapture.App/Assets/Icons/video.png`.
- [ ] **Step 2.2:** Add `BtnRecordVideo` to `HorizontalBar` in `LightshotToolbar.xaml`:
  - Position right after Google Lens and Gemini.
  - Tooltip: `Grabar clip de video [🎥]&#x0a;Clic izquierdo: Clip con audio del sistema&#x0a;Clic derecho: Clip silencioso (tipo GIF)`.
- [ ] **Step 2.3:** Add events `RecordVideoRequested` (with `bool withAudio`) in `LightshotToolbar.xaml.cs`.
- [ ] **Step 2.4:** Build and verify `dotnet build src/LightCapture.App/LightCapture.App.csproj -c Release -p:Platform=x64`.

---

## Task 3: Floating Recording Controller & Visual Frame Overlay

**Files:**
- Create: `src/LightCapture.App/Views/RecordingBorderOverlay.xaml` & `.xaml.cs`
- Create: `src/LightCapture.App/Views/RecordingControlWidget.xaml` & `.xaml.cs`

- [ ] **Step 3.1:** Implement `RecordingBorderOverlay.xaml`:
  - `WindowStyle="None"`, `AllowsTransparency="True"`, `Background="Transparent"`, `Topmost="True"`, `WS_EX_TRANSPARENT` (click-through).
  - Glowing animated red/crimson border tracing the exact recording rectangle.
- [ ] **Step 3.2:** Implement `RecordingControlWidget.xaml`:
  - Samsung One UI dark floating capsule.
  - Shows:
    - 🔴 Pulsing recording dot.
    - Duration timer (`00:00`).
    - 🔊/🔇 Audio toggle button.
    - ⏹️ Stop recording button (`BtnStop`).
    - ❌ Cancel recording button (`BtnCancel`).
  - Allows dragging widget anywhere on screen.
- [ ] **Step 3.3:** Test rendering widget and overlay.

---

## Task 4: Video Preview & Share Dialog (`VideoPreviewWindow`)

**Files:**
- Create: `src/LightCapture.App/Views/VideoPreviewWindow.xaml`
- Create: `src/LightCapture.App/Views/VideoPreviewWindow.xaml.cs`

- [ ] **Step 4.1:** Build XAML for `VideoPreviewWindow`:
  - Header: Samsung One UI title *"Previsualización del Clip"*, badge with duration and size (e.g. `00:08 • 1.4 MB`).
  - Center: `MediaElement` with looping playback, Play/Pause toggle on click.
  - Bottom action bar:
    - 💬 **WhatsApp**: Opens WhatsApp Web / App with instructions or file copied.
    - 🎮 **Discord**: Copies video file to clipboard in FileDrop format so user can immediately press `Ctrl+V` in any Discord channel.
    - 📋 **Copiar Archivo**: Copies file to Windows Clipboard.
    - 💾 **Guardar**: Opens `SaveFileDialog` for `.mp4` (or animated `.gif`).
    - 🗑️ **Descartar**: Deletes temporary video file and closes.
- [ ] **Step 4.2:** Code-behind logic in `VideoPreviewWindow.xaml.cs`:
  - Safe file playback with loop timer.
  - Clipboard file drop integration (`DataObject(DataFormats.FileDrop, new string[] { filePath })`).
  - Clean disposal of temp files on discard.

---

## Task 5: Integration in `OverlayWindow` & Release Verification

**Files:**
- Modify: `src/LightCapture.App/Views/OverlayWindow.xaml.cs`
- Modify: `src/LightCapture.App/App.xaml.cs`

- [ ] **Step 5.1:** In `OverlayWindow.xaml.cs`:
  - Handle `MainToolbar.RecordVideoRequested`:
    - Hide the capture overlay window so desktop is completely visible and interactable.
    - Display `RecordingBorderOverlay` around `_selectionBounds`.
    - Display `RecordingControlWidget` just above or below the selection.
    - Launch `ScreenRecorderService.StartRecordingAsync`.
- [ ] **Step 5.2:** When recording stops:
  - Close widget and border overlay.
  - Open `VideoPreviewWindow` with the generated `.mp4` path.
  - Close `OverlayWindow`.
- [ ] **Step 5.3:** Run all unit tests: `dotnet test tests/LightCapture.Tests -c Release -p:Platform=x64`.
- [ ] **Step 5.4:** Publish standalone portable executable and verify size remains at ~75 MB.
- [ ] **Step 5.5:** Update deliverables and verify end-to-end functionality.
