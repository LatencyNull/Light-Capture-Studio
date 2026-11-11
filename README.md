<div align="center">

# ⚡ Master Capture

**The ultimate screen capture, live translation, OCR, and video recording suite for Windows.**  
*Built with .NET 8, featuring a modern Samsung One UI Dark interface and 100% local privacy.*

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011%20(x64)-0078D6.svg)](https://microsoft.com)
[![.NET](https://img.shields.io/badge/.NET-8.0%20WPF-512BD4.svg)](https://dotnet.microsoft.com/)
[![CI Build &amp; Release](https://github.com/LatencyNull/Master-Capture/actions/workflows/build-release.yml/badge.svg)](https://github.com/LatencyNull/Master-Capture/actions/workflows/build-release.yml)

[Features](#-key-features) • [Comparison](#-master-capture-vs-lightshot) • [Keyboard Shortcuts](#-keyboard-shortcuts) • [Portable Version](#-portable-version) • [Privacy](#-privacy-first-architecture)

</div>

---

## 🚀 Overview

**Master Capture** is a modern screen capture, translation, and screen recording suite for Windows, built as a modern evolution of classic tools like Lightshot. Its goal is to consolidate everyday tasks that previously required multiple disjointed utilities into a single, lightweight, portable executable: capture regions with pixel precision, translate text immediately without leaving your screen, record video clips with audio, and extract text from images via OCR.

Unlike conventional capture software, this project follows a **100% local** architecture: it does not upload your images to public web servers or rely on third-party cloud hosting. All image processing, OCR, video encoding, and history indexing run directly on your own computer.

---

## 🥊 Master Capture vs Lightshot

| Feature | ⚡ Master Capture | 🐢 Classic Lightshot |
|:---|:---:|:---:|
| **Live On-Screen Translation** | ✅ Google Lens-style overlay integrated | ❌ Not available |
| **Video & GIF Recording** | ✅ MP4 (system audio + mic) and lightweight animated GIFs | ❌ Static captures only |
| **Video Annotations & Timeline** | ✅ Arrows, text, boxes, pen, Knox redaction with duration tracks | ❌ Impossible |
| **Real-Time Video Preview** | ✅ Smooth scrubbing at ~40 fps, seek bar, and Stop button | ❌ Not available |
| **OCR Text Extraction** | ✅ Instant copy to clipboard (<kbd>Ctrl+Shift+T</kbd>) | ❌ Impossible (pixels only) |
| **Local History with Search** | ✅ Search past captures by the text detected in them | ❌ None (lost upon closing) |
| **Privacy & Security** | ✅ 100% local storage in `Documents\Master Capture` | ⚠️ Insecure public uploads |
| **Intelligent Censorship** | ✅ Destructive irreversible mosaic pixelation brush | ❌ Crude pen scribbles |
| **Pin to Screen (Picture-in-Picture)**| ✅ Always-on-top floating window (<kbd>Ctrl+P</kbd>) | ❌ Requires external viewer |
| **QR Code Recognition** | ✅ Automatic detection and one-click opening | ❌ Requires mobile phone scan |
| **Direct Instant Sharing** | ✅ Windows WhatsApp App, Web, Telegram, Discord, etc. | ❌ Broken or outdated links |
| **Design & Aesthetics** | ✅ Modern Samsung One UI Dark / Fluent | ❌ Win7/XP-era gray boxes |

---

## 🌟 Key Features

### 1. 🌐 Live On-Screen Translation (Lens Mode)
Select any area on screen — in a game, foreign-language document, or protected website — and press **Translate**. Text blocks are overlaid directly in place preserving original position and size, or copied to your clipboard ready to paste.

### 2. 🎥 MP4 & GIF Video Recording with Annotations and Timeline
Need to report a bug, demo a match, or build an interactive tutorial? Record any desktop region at 30/60 FPS in H.264 MP4 with system audio and microphone voice, or export lightweight animated GIFs (up to 15s) ideal for chats and forums:

- ↗️ **On-Video Annotation Tools**: Place indicator arrows (`↗️`), custom floating text (`🔤`), rectangles (`🔲`), freehand drawings (`✏️`), and solid Knox redaction boxes (`🔒`) directly over the video.
- ⏱️ **Object Timeline**: A visual track is automatically generated for every added element. Drag blocks horizontally to set when they appear, and adjust edges to customize exact duration.
- ⚡ **Real-Time Preview & Scrubbing**: Fast-forward, rewind, or scrub with live preview at ~40 FPS even when paused. Trim start and end points with precision or press the **Stop** (`⏹`) button to reset playback instantly.
- 🎨 **Hardware Stamping**: All annotations and temporal tracks are rendered into the final MP4 or GIF using hardware-accelerated encoding (FFmpeg / Windows Media).

### 3. 🔍 Native Windows OCR Text Extraction
Copy text from anywhere: YouTube videos, locked web pages, images, or scanned documents. Press <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>T</kbd> and detected text is copied instantly to your clipboard.

### 4. 🕒 Smart Local History with Search
Every time you copy (<kbd>Ctrl</kbd> + <kbd>C</kbd>, <kbd>Enter</kbd>, or from toolbar), your capture is saved in `Documents\Master Capture` and its text content is indexed. Open the history gallery (<kbd>F9</kbd> → clock icon) to search by keyword, batch-select/delete items, copy thumbnails, or open the folder in one click.

### 5. 🛡️ Destructive Pixelation Censorship
Hide passwords, credit card numbers, faces, or confidential data with complete peace of mind. The redaction brush applies an irreversible mosaic directly onto image pixels.

### 6. 📌 Pin to Screen (Picture-in-Picture)
Keep a capture floating above all other windows while coding, designing, or researching. Resize with mouse wheel, drag anywhere, or press Esc to dismiss.

### 7. 📱 Direct Sharing to WhatsApp, Telegram & More
Send captures and clips directly to **WhatsApp Desktop (Windows App)**, **WhatsApp Web**, Telegram, Facebook, TikTok, Instagram, X, or perform reverse visual search with Google Lens and Gemini.

---

## ⌨️ Keyboard Shortcuts

| Shortcut | Description |
|---|---|
| <kbd>F9</kbd> or <kbd>Print Screen</kbd> | Activate screen capture selector anywhere |
| <kbd>Enter</kbd> or <kbd>Ctrl</kbd> + <kbd>C</kbd> | Copy capture to clipboard and save to history |
| <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>T</kbd> | Extract and copy OCR text to clipboard |
| <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>C</kbd> | Copy translated text to clipboard |
| <kbd>Ctrl</kbd> + <kbd>S</kbd> | Open save dialog (default folder `Documents\Master Capture`) |
| <kbd>Ctrl</kbd> + <kbd>P</kbd> | Pin capture as always-on-top floating window |
| <kbd>Ctrl</kbd> + <kbd>Z</kbd> | Undo last drawing stroke or annotation |
| <kbd>Esc</kbd> | Cancel capture / Close overlay |

---

## 💾 Portable Version

**Master Capture is 100% portable and self-contained.**
- **No installation:** No installer, wizard, or administrator rights required.
- **Self-contained:** Built with all .NET 8 runtime dependencies bundled into a single executable (`MasterCapture.exe`).
- **Clean & lightweight:** Leaves no clutter in the Windows registry; runs straight from any folder or USB drive.

Download the precompiled portable binary directly from the [**Releases**](https://github.com/LatencyNull/Master-Capture/releases) section!

---

## 🔒 Privacy-First Architecture

Unlike classic Lightshot and older utilities that upload screenshots to public web links (where anyone could guess sequential URLs and view sensitive data), **Master Capture is 100% local**:

- Captures are stored exclusively in your personal `Documents\Master Capture` folder.
- Optical Character Recognition (OCR) runs locally using native Windows APIs (`Windows.Media.Ocr`).
- No hidden background telemetry, no required accounts, and no third-party trackers.

---

## 🛠️ System Requirements & Tech Stack

- **Operating System:** Windows 10 (Build 19041+) or Windows 11 (64-bit)
- **Runtime:** .NET 8.0 Desktop Runtime (or run the self-contained portable executable)
- **Core Libraries:**
  - `Windows.Media.Ocr` (Hardware-accelerated native Windows OCR engine)
  - `ScreenRecorderLib` (DirectX & Media Foundation H.264 hardware video encoder)
  - `ZXing.Net` (High-speed QR code and barcode scanner)

---

## 📦 Building from Source

### Option 1: Automated Build Script (Recommended)
You can build the full standalone portable release with one command:

```powershell
# Build self-contained standalone executable and package ZIP
.\build.ps1

# Clean build and immediately launch the app
.\build.ps1 -Clean -Run
```

### Option 2: Standard .NET CLI
```powershell
# Clone the repository
git clone https://github.com/LatencyNull/Master-Capture.git
cd Master-Capture

# Restore and build the complete solution
dotnet build LightCaptureStudio.sln

# Run automated tests
dotnet test LightCaptureStudio.sln

# Publish self-contained portable executable (x64)
dotnet publish src/LightCapture.App/LightCapture.App.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish/standalone
```

The resulting file in `publish/standalone/MasterCapture.exe` (and `release/MasterCapture-v1.1.0-Standalone-win-x64.exe`) is completely portable and ready to run.

---

## 🔒 Optional Cloud Upload Configuration

By default, Master Capture operates **100% locally**. If you choose to use the *"Upload to Cloud & Copy Link"* feature, you can optionally supply your own free FreeImage.host API key:
- Via environment variable: `MASTERCAPTURE_FREEIMAGE_KEY="your_api_key"`
- Or via configuration file at `%AppData%\MasterCapture\cloud_settings.json`:
  ```json
  {
    "freeimage_key": "your_api_key"
  }
  ```

---

## 📄 License

This project is licensed under the [MIT License](LICENSE) — free for personal, educational, and commercial use.

---

<div align="center">
  <sub>Crafted with passion for modern Windows workflows. If you find this project useful, consider dropping a ⭐!</sub>
</div>
