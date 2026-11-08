<div align="center">

# ⚡ Light Capture Studio

**The ultimate screen capture, live overlay translation, OCR & screen recording suite for Windows.**  
*Engineered with .NET 8, modern Samsung One UI Dark aesthetics, and uncompromising local privacy.*

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011%20(x64)-0078D6.svg)](https://microsoft.com)
[![.NET](https://img.shields.io/badge/.NET-8.0%20WPF-512BD4.svg)](https://dotnet.microsoft.com/)

[Features](#-key-features) • [Comparison](#-why-light-capture-studio-vs-legacy-tools) • [Keyboard Shortcuts](#-keyboard-shortcuts) • [Installation & Build](#-build-from-source) • [Privacy](#-privacy-first-architecture)

</div>

---

## 🚀 Overview

**Light Capture Studio** reimagines what a modern desktop screenshot tool should be in 2026. While traditional tools like legacy Lightshot remain frozen in 2010—offering only basic static snips and insecure public image uploads—**Light Capture Studio** delivers a powerhouse productivity suite right to your desktop:

- 🌐 **In-Place Live Translation**: Translate games, comics, foreign PDFs, or unselectable web content directly on your screen with a Google Lens-style overlay.
- 🎥 **H.264 Screen Recording**: Record crisp MP4 video clips with system loopback audio and microphone voiceover in two clicks.
- 🔍 **Native Windows OCR**: Extract locked text from YouTube videos, scanned documents, and images directly to your clipboard (<kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>T</kbd>).
- 🕒 **Searchable Local History**: Automatically saves and indexes every capture to your `Documents\Light Capture Studio` folder. Search weeks later by words detected in the image!
- 🔒 **100% Local Privacy**: Zero public cloud uploads. What happens on your screen stays on your computer.

---

## 🥊 Why Light Capture Studio? (vs. Legacy Lightshot)

| Feature | ⚡ Light Capture Studio | 🐢 Legacy Lightshot |
|:---|:---:|:---:|
| **Live In-Place Translation** | ✅ Google Lens-style overlay directly on screen | ❌ None |
| **MP4 Video & Mic Recording** | ✅ Hardware-accelerated with audio + voiceover | ❌ Static screenshots only |
| **OCR Text Extraction** | ✅ Instant clipboard extraction (<kbd>Ctrl+Shift+T</kbd>) | ❌ Impossible (pixels only) |
| **Local Searchable History** | ✅ Search past captures by text contained inside | ❌ None (lost once closed) |
| **Privacy & Security** | ✅ 100% Local offline storage (`Documents`) | ⚠️ Insecure public uploads |
| **Smart Censorship / Redaction**| ✅ Irreversible mosaic pixelation tool | ❌ Rough pen doodles |
| **Pin to Screen (Picture-in-Picture)** | ✅ Floating always-on-top window (<kbd>Ctrl+P</kbd>) | ❌ Requires 3rd-party viewer |
| **QR Code Recognition** | ✅ Instant link detection and one-click launch | ❌ Must scan with phone |
| **Direct Instant Sharing** | ✅ Windows App & Web WhatsApp, Telegram | ❌ Broken / legacy social links |
| **UI Aesthetics** | ✅ Modern Samsung One UI Dark / Fluent | ❌ Win7 / XP era grey panels |

---

## 🌟 Key Features

### 1. 🌐 Live Overlay Translation (Lens Mode)
Select any portion of your screen—in a video game, foreign article, or locked document—and click **Translate**. Text blocks are replaced seamlessly with translated text in-place, preserving font size and position, or copied instantly as formatted plain text.

### 2. 🎥 Screen & Audio Video Recording
Need to explain a bug, demonstrate a gameplay clip, or record a quick walkthrough? Switch to video mode to capture any region in smooth 30/60 FPS H.264 MP4 with system sound and microphone narration. An integrated mini-player lets you review, copy, or share right away.

### 3. 🔍 Native Windows OCR Text Extraction
Extract text from anywhere—even protected web pages, YouTube subtitles, or rasterized images. Press <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>T</kbd> and the extracted text is immediately ready to paste.

### 4. 🕒 Smart Local History with Search
Every time you copy (<kbd>Ctrl</kbd> + <kbd>C</kbd>, <kbd>Enter</kbd>, or toolbar), your capture is saved to `Documents\Light Capture Studio` and its recognized text is indexed. Open the history gallery (<kbd>F9</kbd> → Clock icon) to search by keyword, copy thumbnails, or open the folder with a single click.

### 5. 🛡️ Destructive Pixelation Censorship
Easily mask passwords, API keys, faces, or credit card details. The censorship brush applies an irreversible mosaic pixelation directly to the underlying bitmap.

### 6. 📌 Pin to Screen (Picture-in-Picture)
Keep a snip floating above other windows while coding, designing, or researching. Resize it, move it anywhere, or dismiss it when done.

### 7. 📱 Direct WhatsApp & Telegram Sharing
Share directly to **WhatsApp Desktop (Windows App)** or **WhatsApp Web**, as well as Telegram and visual Google Lens search.

---

## ⌨️ Keyboard Shortcuts

| Shortcut | Description |
|---|---|
| <kbd>F9</kbd> or <kbd>PrintScreen</kbd> | Activate screen capture overlay anywhere |
| <kbd>Enter</kbd> or <kbd>Ctrl</kbd> + <kbd>C</kbd> | Copy capture to clipboard & save to history |
| <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>T</kbd> | Extract and copy raw OCR text to clipboard |
| <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>C</kbd> | Copy translated text to clipboard |
| <kbd>Ctrl</kbd> + <kbd>S</kbd> | Open save dialog (defaults to `Documents\Light Capture Studio`) |
| <kbd>Ctrl</kbd> + <kbd>P</kbd> | Pin capture as floating always-on-top window |
| <kbd>Ctrl</kbd> + <kbd>Z</kbd> | Undo last annotation stroke |
| <kbd>Esc</kbd> | Cancel capture / Close overlay |

---

## 🔒 Privacy-First Architecture

Unlike legacy screenshot tools that upload images to publicly accessible URLs (where anyone can iterate sequential URLs to uncover private documents), **Light Capture Studio is strictly local-first**:

- Captures are saved exclusively in your personal `Documents\Light Capture Studio` directory.
- OCR text recognition runs locally via Microsoft Windows native APIs (`Windows.Media.Ocr`).
- No background telemetry, no forced account creation, and no third-party tracking.

---

## 🛠️ System Requirements & Tech Stack

- **Operating System:** Windows 10 (Build 19041+) or Windows 11 (64-bit)
- **Runtime:** .NET 8.0 Desktop Runtime (or use the self-contained portable executable)
- **Key Libraries:**
  - `Windows.Media.Ocr` (Hardware-accelerated native Windows OCR engine)
  - `ScreenRecorderLib` (DirectX & Media Foundation H.264 video encoding)
  - `ZXing.Net` (High-performance barcode & QR code decoding)

---

## 📦 Build from Source

Clone the repository and build the standalone portable executable using the .NET 8 SDK:

```powershell
# Clone the repository
git clone https://github.com/LatencyNull/Light-Capture-Studio.git
cd Light-Capture-Studio

# Publish self-contained single-file binary (x64)
dotnet publish src/LightCapture.App/LightCapture.App.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish-standalone
```

The resulting `publish-standalone\LightCapture.App.exe` is completely portable—no installer needed.

---

## 📄 License

This project is licensed under the [MIT License](LICENSE) — free for personal, educational, and commercial use.

---

<div align="center">
  <sub>Crafted with passion for modern Windows workflows. If you find this project useful, consider giving it a ⭐!</sub>
</div>
