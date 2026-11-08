# Light Capture Translator - Killer Features (Phase 2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement 5 killer differentiator features: Pixelate/Censorship tool, Numbered Step bubbles, Direct OCR text copy, Automatic QR code scanner, and Local Searchable Capture History.

**Tech Stack:** C# .NET 8 WPF, ZXing.Net, Windows.Media.Ocr, GDI+/WPF DrawingContext.

**Spec:** `docs/superpowers/specs/2026-11-06-light-capture-translator-design.md`

---

## Tasks

### Task 1: Pixelado / Censura y Pasos Numerados (Modelos y DrawingCanvas)
- Añadir `Pixelate` y `NumberedStep` a `DrawingToolType`.
- Implementar `PixelateDrawingElement` con algoritmo de mosaico de píxeles sobre el recorte de pantalla.
- Implementar `NumberedStepDrawingElement` que renderiza una burbuja circular con número centrado.
- Actualizar `DrawingCanvas` para soportar arrastre de pixelado y clics de pasos con contador autoincremental y retroceso en Undo.

### Task 2: Escáner de Códigos QR (ZXing.Net)
- Crear `IQrScannerService` e implementar `QrScannerService` usando `ZXing.BarcodeReaderGeneric`.
- Escribir pruebas unitarias de decodificación QR en `tests/LightCapture.Tests/QrScannerTests.cs`.
- Integrar banner flotante de QR detectado en `OverlayWindow` con botones de Copiar y Abrir en navegador.

### Task 3: Botón "Copiar Texto OCR" y Actualización de Barras de Herramientas
- Añadir botón de Pixelado e icono `①` en la barra vertical de `LightshotToolbar.xaml`.
- Añadir botón "Copiar Texto (OCR)" en la barra horizontal junto al botón de Traducir.
- Implementar atajo `Ctrl + Shift + T` para extracción directa a portapapeles.

### Task 4: Servicio de Historial Local y Ventana de Búsqueda
- Crear `CaptureHistoryService` que almacena imágenes en `%LOCALAPPDATA%\LightCaptureTranslator\History\` e indexa el texto OCR en `history.json`.
- Crear `HistoryWindow.xaml` con vista de tarjetas en cuadrícula y barra de búsqueda en tiempo real.
- Integrar acceso a "🕒 Historial de capturas" en el menú contextual de la bandeja del sistema (`App.xaml.cs`).

### Task 5: Pruebas Unitarias, Verificación y Publicación Single-File
- Ejecutar suite completa de tests con `dotnet test`.
- Publicar el ejecutable final standalone con icono incrustado en `publish/LightCapture.App.exe` y `LightCapture.exe`.
