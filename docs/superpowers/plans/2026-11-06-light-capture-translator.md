# Light Capture Translator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a native Windows desktop screen capture utility identical to Lightshot in UI and tools, enhanced with in-place optical character recognition (OCR) and translation (Google Lens style).

**Architecture:** .NET 8 WPF application with native Win32 screen capture and global hotkeys, `Windows.Media.Ocr` for text and bounding box detection, async Google Translate API (`client=gtx`), and an interactive multi-layer drawing canvas with undo support.

**Tech Stack:** C#, .NET 8 (WPF / Windows Desktop), `Windows.Media.Ocr`, Win32 User32 APIs, HttpClient, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-06-light-capture-translator-design.md`

---

## Global Constraints

- **Plataforma:** Windows 10/11 x64 (.NET 8 SDK).
- **Fidelidad de interfaz:** 8 herramientas de dibujo verticales (derecha) y 8 acciones horizontales (inferior derecha).
- **Posición del botón Traducir:** Exactamente a la derecha de «Imprimir» y a la izquierda de «Copiar».
- **Endpoint de traducción:** `https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl=es&dt=t&q=` (sin claves API, 100% gratuito).
- **Interacción de dibujo:** Las cajas de traducción no deben interceptar el ratón (`IsHitTestVisible = false`), permitiendo dibujar sobre ellas.
- **Portapapeles:** `Ctrl+C` copia la imagen procesada con anotaciones; `Ctrl+Shift+C` copia únicamente el texto traducido.

---

## Review Focus

1. **Selección con dimensiones invertidas o nulas (arrastrar de abajo a arriba o de derecha a izquierda):** Normalizar siempre `X, Y, Ancho, Alto` a valores positivos y descartar selecciones con área < 10x10 px.
2. **Entrada sin texto detectado en la captura:** Si el OCR no encuentra palabras, mostrar notificación toast discreta sin crashear ni congelar la UI.
3. **Fallo de red o timeout en Google Translate:** Si no hay conexión a internet, notificar el error en la barra de captura y mantener la selección con la imagen original intacta.
4. **Soporte multimonitor con DPI desigual:** El overlay debe cubrir todo el `SystemParameters.VirtualScreenWidth/Height` respetando las coordenadas de cada monitor sin distorsión.
5. **Reintento o re-dibujo tras traducir:** Al alternar entre texto original y traducción, o al pulsar Deshacer (`Ctrl+Z`), los trazos de pluma y flechas deben preservarse en su capa correcta.

---

## File Structure

```
src/
├── LightCapture.Core/
│   ├── Models/
│   │   ├── OcrTextBlock.cs            (Texto, BoundingBox X,Y,W,H, Confianza)
│   │   ├── TranslationResult.cs       (TextoOriginal, TextoTraducido, IdiomaDetectado)
│   │   └── DrawingElement.cs          (Pintura vectorial: Pen, Line, Arrow, Rect, Marker, Text)
│   ├── Services/
│   │   ├── ITranslationService.cs
│   │   ├── GoogleTranslationService.cs (Implementación client=gtx de AutoHotkey)
│   │   ├── IOcrService.cs
│   │   ├── WindowsOcrService.cs       (Windows.Media.Ocr wrapper)
│   │   ├── ICaptureService.cs
│   │   └── ScreenCaptureService.cs    (Win32/GDI+ Fullscreen & VirtualScreen grabber)
├── LightCapture.App/
│   ├── App.xaml / App.xaml.cs        (Ciclo de vida, System Tray, Single Instance)
│   ├── Controls/
│   │   ├── LightshotToolbar.xaml      (Barras vertical y horizontal con iconos nativos)
│   │   └── DrawingCanvas.cs           (Canvas interactivo con renderizado vectorial e In-Place Lens chips)
│   ├── Views/
│   │   └── OverlayWindow.xaml         (Ventana fullscreen transparente con marcha de hormigas)
│   └── Services/
│       ├── HotkeyManager.cs           (RegisterHotKey para Impr Pant / Ctrl+T / Esc)
│       └── NotificationService.cs     (Toasts flotantes y bandeja del sistema)
tests/
└── LightCapture.Tests/
    ├── TranslationServiceTests.cs     (Pruebas unitarias de parsing y peticiones HTTP)
    ├── OcrBoundingBoxTests.cs         (Pruebas de normalización de cajas y mapeo de coordenadas)
    └── DrawingHistoryTests.cs         (Pruebas de pila Undo/Redo y dibujo vectorial)
```

---

## Plan Tasks

### Task 1: Scaffolding de la Solución y Servicio de Traducción (Google Translate GTX)

**Files:**
- Create: `LightCaptureTranslator.sln`
- Create: `src/LightCapture.Core/LightCapture.Core.csproj`
- Create: `src/LightCapture.Core/Models/TranslationResult.cs`
- Create: `src/LightCapture.Core/Services/ITranslationService.cs`
- Create: `src/LightCapture.Core/Services/GoogleTranslationService.cs`
- Create: `tests/LightCapture.Tests/LightCapture.Tests.csproj`
- Create: `tests/LightCapture.Tests/TranslationServiceTests.cs`

**Interfaces:**
- Consumes: Ninguna.
- Produces: `ITranslationService.TranslateAsync(string text, string targetLang = "es", string sourceLang = "auto") -> Task<TranslationResult>`

- [ ] **Step 1: Crear la solución .NET 8 y los proyectos Core y Tests**
  - Run: `dotnet new sln -n LightCaptureTranslator`
  - Run: `dotnet new classlib -n LightCapture.Core -f net8.0-windows`
  - Run: `dotnet new xunit -n LightCapture.Tests -f net8.0-windows`
  - Run: `dotnet sln add src/LightCapture.Core/LightCapture.Core.csproj tests/LightCapture.Tests/LightCapture.Tests.csproj`
  - Run: `dotnet add tests/LightCapture.Tests reference src/LightCapture.Core/LightCapture.Core.csproj`

- [ ] **Step 2: Escribir el test que falla para el parsing y traducción HTTP**
  Escribir pruebas en `TranslationServiceTests.cs` comprobando:
  1. Extracción de fragmentos de JSON devueltos por `translate_a/single?client=gtx`.
  2. Manejo de saltos de línea y comillas escapadas idéntico al script `.ahk`.
  3. Comportamiento ante cadenas vacías o sin texto.

- [ ] **Step 3: Ejecutar el test para verificar que falla**
  - Run: `dotnet test tests/LightCapture.Tests --filter TranslationServiceTests`
  - Expected: FAIL (clases no definidas).

- [ ] **Step 4: Implementar `GoogleTranslationService`**
  Implementar cliente `HttpClient` que encodifica con `Uri.EscapeDataString` y consulta `https://translate.googleapis.com/translate_a/single?client=gtx&sl={sourceLang}&tl={targetLang}&dt=t&q={encoded}`, parseando el árbol JSON resultante en `TranslationResult`.

- [ ] **Step 5: Ejecutar el test para verificar que pasa**
  - Run: `dotnet test tests/LightCapture.Tests --filter TranslationServiceTests`
  - Expected: PASS.

- [ ] **Step 6: Commit**
  - `git add . && git commit -m "feat(core): setup solution and implement GoogleTranslationService"`

---

### Task 2: Servicio OCR y Extracción de Coordenadas Bounding Box

**Files:**
- Create: `src/LightCapture.Core/Models/OcrTextBlock.cs`
- Create: `src/LightCapture.Core/Services/IOcrService.cs`
- Create: `src/LightCapture.Core/Services/WindowsOcrService.cs`
- Create: `tests/LightCapture.Tests/OcrBoundingBoxTests.cs`

**Interfaces:**
- Consumes: `OcrTextBlock` con `(Text, X, Y, Width, Height)`.
- Produces: `IOcrService.RecognizeTextAsync(byte[] imageBytes) -> Task<IReadOnlyList<OcrTextBlock>>`

- [ ] **Step 1: Escribir el test unitario para agrupación y normalización de cajas OCR**
  En `OcrBoundingBoxTests.cs`, verificar que las palabras adyacentes en la misma línea se puedan agrupar en bloques coherentes para traducción fluida de oraciones, manteniendo los límites exteriores de la caja.

- [ ] **Step 2: Ejecutar el test para verificar que falla**
  - Run: `dotnet test tests/LightCapture.Tests --filter OcrBoundingBoxTests`
  - Expected: FAIL.

- [ ] **Step 3: Implementar `WindowsOcrService`**
  - Envolver `Windows.Media.Ocr.OcrEngine` (soporte nativo de Windows).
  - Convertir el flujo de bytes de imagen a `SoftwareBitmap`.
  - Extraer las líneas reconocidas con su `Rect` bounding box exacto en coordenadas de píxeles.

- [ ] **Step 4: Ejecutar el test para verificar que pasa**
  - Run: `dotnet test tests/LightCapture.Tests --filter OcrBoundingBoxTests`
  - Expected: PASS.

- [ ] **Step 5: Commit**
  - `git add . && git commit -m "feat(ocr): add WindowsOcrService and OcrTextBlock models"`

---

### Task 3: Motor de Captura de Pantalla Multimonitor y Proyecto WPF

**Files:**
- Create: `src/LightCapture.App/LightCapture.App.csproj` (WPF .NET 8)
- Create: `src/LightCapture.Core/Services/ScreenCaptureService.cs`
- Create: `src/LightCapture.Core/Services/ICaptureService.cs`

**Interfaces:**
- Consumes: Win32 GDI / VirtualScreen bounds.
- Produces: `ICaptureService.CaptureVirtualScreen() -> BitmapSource`

- [ ] **Step 1: Crear proyecto WPF `LightCapture.App` e integrar en la solución**
  - Run: `dotnet new wpf -n LightCapture.App -f net8.0-windows`
  - Run: `dotnet sln add src/LightCapture.App/LightCapture.App.csproj`
  - Run: `dotnet add src/LightCapture.App reference src/LightCapture.Core/LightCapture.Core.csproj`

- [ ] **Step 2: Implementar `ScreenCaptureService`**
  Implementar captura de pantalla de bajo retardo usando GDI+ `BitBlt` o `CopyFromScreen` cubriendo todo el área virtual multimonitor (`SystemParameters.VirtualScreenLeft`, `Top`, `Width`, `Height`).

- [ ] **Step 3: Verificar compilación del proyecto WPF**
  - Run: `dotnet build src/LightCapture.App/LightCapture.App.csproj`
  - Expected: BUILD SUCCESS.

- [ ] **Step 4: Commit**
  - `git add . && git commit -m "feat(capture): implement ScreenCaptureService and WPF app scaffolding"`

---

### Task 4: Ventana de Overlay, Recorte Interactivo y Marching Ants

**Files:**
- Create: `src/LightCapture.App/Views/OverlayWindow.xaml`
- Create: `src/LightCapture.App/Views/OverlayWindow.xaml.cs`
- Create: `src/LightCapture.App/Controls/SelectionAdorner.cs`

**Interfaces:**
- Consumes: `BitmapSource` de la pantalla congelada.
- Produces: Rectángulo de selección normalizado `Rect SelectionBounds`, evento `SelectionCompleted`.

- [ ] **Step 1: Crear `OverlayWindow` fullscreen translúcido**
  - Ventana sin bordes (`WindowStyle="None"`, `AllowsTransparency="True"`, `Topmost="True"`).
  - Posicionada para cubrir el rectángulo virtual completo de todos los monitores.
  - Al abrirse, renderiza el congelado de pantalla con filtro oscuro del 50%.

- [ ] **Step 2: Implementar el rectángulo de selección interactivo**
  - Eventos de ratón (`MouseDown`, `MouseMove`, `MouseUp`) para definir el área de captura.
  - Borde punteado con animación marching ants (estilo Lightshot).
  - Indicador flotante superior izquierdo con el tamaño en píxeles: `Ancho x Alto`.
  - 8 puntos de control (tiradores) para redimensionar en cualquier momento.

- [ ] **Step 3: Verificar visualmente la ventana de selección**
  - Probar la interacción de redimensionado y arrastre del recuadro.

- [ ] **Step 4: Commit**
  - `git add . && git commit -m "feat(ui): implement OverlayWindow with interactive selection and dimensions tooltip"`

---

### Task 5: Capa de Herramientas de Dibujo de Lightshot (8 Herramientas)

**Files:**
- Create: `src/LightCapture.Core/Models/DrawingElement.cs`
- Create: `src/LightCapture.App/Controls/DrawingCanvas.cs`
- Create: `tests/LightCapture.Tests/DrawingHistoryTests.cs`

**Interfaces:**
- Consumes: Herramientas: `Pen, Line, Arrow, Rect, Marker, Text, Color`.
- Produces: `DrawingCanvas.AddElement(...)`, `DrawingCanvas.Undo()`, `DrawingCanvas.GetFlattenedImage()`.

- [ ] **Step 1: Escribir tests para la pila de Deshacer (Undo)**
  En `DrawingHistoryTests.cs`, verificar que al insertar trazos, flechas o textos se puedan deshacer uno a uno en orden inverso conservando el estado previo.

- [ ] **Step 2: Ejecutar test para verificar que falla**
  - Run: `dotnet test tests/LightCapture.Tests --filter DrawingHistoryTests`
  - Expected: FAIL.

- [ ] **Step 3: Implementar `DrawingCanvas`**
  - Implementar trazo libre de pluma (`Pen`).
  - Implementar línea recta (`Line`).
  - Implementar flecha señalizadora con cabeza triangular geométrica (`Arrow`).
  - Implementar rectángulo (`Rect`).
  - Implementar marcador fluorescente translúcido (`Marker`, opacidad 0.35).
  - Implementar cuadro de texto editable en pantalla (`Text`).
  - Selector de color con paleta Lightshot (rojo por defecto).
  - Soporte para `Ctrl+Z` (Deshacer).

- [ ] **Step 4: Ejecutar test para verificar que pasa**
  - Run: `dotnet test tests/LightCapture.Tests --filter DrawingHistoryTests`
  - Expected: PASS.

- [ ] **Step 5: Commit**
  - `git add . && git commit -m "feat(drawing): implement Lightshot 8 drawing tools and undo history"`

---

### Task 6: Barras de Herramientas Lightshot y Motor de Reemplazo Google Lens In-Place

**Files:**
- Create: `src/LightCapture.App/Controls/LightshotToolbar.xaml`
- Create: `src/LightCapture.App/Controls/LightshotToolbar.xaml.cs`
- Modify: `src/LightCapture.App/Views/OverlayWindow.xaml`
- Create: `src/LightCapture.App/Controls/LensOverlayElement.cs`

**Interfaces:**
- Consumes: `IOcrService`, `ITranslationService`, selección activa.
- Produces: Barra vertical con 8 botones; barra horizontal con 8 botones (`[Nube] [Compartir] [Buscar] [Imprimir] [Traducir] [Copiar] [Guardar] [Cerrar]`).

- [ ] **Step 1: Crear `LightshotToolbar.xaml`**
  - Maquetar las dos barras con el aspecto nativo exacto de Lightshot (fondo `#f1f2f4`, iconos de 24x24 px).
  - **Barra vertical:** Pluma, Línea, Flecha, Rectángulo, Marcador, Texto, Color, Deshacer.
  - **Barra horizontal:** Nube, Compartir, Buscar en Google, Imprimir, **Traducir (Google Lens)**, Copiar, Guardar, Cerrar.
  - Colocar el botón **Traducir** inmediatamente a la derecha de Imprimir.

- [ ] **Step 2: Implementar el motor de sustitución Google Lens In-Place**
  - Al hacer clic en **Traducir** (o presionar `Ctrl+T`):
    1. Mostrar indicador visual de escaneo.
    2. Recortar el bitmap de la selección y pasarlo a `WindowsOcrService`.
    3. Para cada línea detectada, consultar `GoogleTranslationService`.
    4. Crear parches de fondo (`LensOverlayElement`) que cubren las coordenadas exactas del texto original.
    5. Renderizar el texto en español con tamaño de fuente ajustado proporcionalmente al área.
    6. Configurar `IsHitTestVisible = false` para que el usuario pueda seguir dibujando encima sin obstáculos.
    7. Añadir botón toggle para alternar al instante entre vista original y traducida.

- [ ] **Step 3: Acciones de Guardar y Copiar**
  - Copiar imagen (`Ctrl+C`): Genera el bitmap fusionado (recorte + traducción + trazos dibujados) y lo deposita en `Clipboard.SetImage`.
  - Copiar texto plano (`Ctrl+Shift+C` o clic derecho en Traducir): Junta todos los bloques de texto traducidos y los deposita en `Clipboard.SetText`.
  - Guardar imagen (`Ctrl+S`): Abre el cuadro de diálogo estándar de guardado `.png` / `.jpg`.
  - Buscar en Google: Abre el navegador con la búsqueda visual.

- [ ] **Step 4: Commit**
  - `git add . && git commit -m "feat(ui): implement Lightshot toolbars and in-place Google Lens translation layer"`

---

### Task 7: Integración con System Tray, Atajos Globales y Manejo de Errores

**Files:**
- Create: `src/LightCapture.App/Services/HotkeyManager.cs`
- Modify: `src/LightCapture.App/App.xaml.cs`
- Create: `src/LightCapture.App/Resources/tray_icon.ico`

**Interfaces:**
- Consumes: Win32 `RegisterHotKey`, `VK_SNAPSHOT` (Impr Pant).
- Produces: Activación global de la captura en segundo plano.

- [ ] **Step 1: Implementar `HotkeyManager` con Win32 API**
  - Registrar la tecla `Impr Pant` (`VK_SNAPSHOT`) para disparar el congelado de pantalla desde cualquier aplicación activa.
  - Interceptar `Esc` dentro del overlay para cancelar la selección de forma inmediata.

- [ ] **Step 2: Implementar System Tray Icon**
  - Icono en la barra de tareas con menú contextual: «Capturar pantalla», «Opciones» y «Salir».
  - Prevenir múltiples instancias simultáneas mediante `Mutex`.

- [ ] **Step 3: Notificaciones y manejo de fallos**
  - Toasts discretos ante fallos de conexión o capturas sin texto detectado.

- [ ] **Step 4: Commit**
  - `git add . && git commit -m "feat(system): add system tray icon, global hotkey hook and lifecycle management"`

---

### Task 8: Pruebas de Integración y Verificación Funcional Completa

**Files:**
- Tests en `tests/LightCapture.Tests/`
- Compilación final y verificación de build en Release.

- [ ] **Step 1: Ejecutar la suite completa de pruebas unitarias**
  - Run: `dotnet test`
  - Expected: Todos los tests pasan al 100%.

- [ ] **Step 2: Compilar el ejecutable final en modo Release**
  - Run: `dotnet publish src/LightCapture.App/LightCapture.App.csproj -c Release -r win-x64 --self-contained false`
  - Expected: Generación exitosa de `LightCapture.App.exe`.

- [ ] **Step 3: Prueba funcional de captura y traducción real**
  - Comprobar respuesta del OCR de Windows y traducción con el endpoint libre.
  - Validar atajos `Ctrl+C`, `Ctrl+Shift+C`, `Ctrl+T` y `Esc`.

- [ ] **Step 4: Commit final**
  - `git add . && git commit -m "chore(release): verify all tests and build release package"`
