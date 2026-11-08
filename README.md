# ⚡ Light Capture Studio

> **Capturador de pantalla, suite de productividad con OCR, traducción en tiempo real, grabación de video con micrófono y diseño Samsung One UI Dark.**

![Light Capture](src/LightCapture.App/Resources/app.ico)

---

## 🌟 Características Principales

1. **Traducción en Vivo (Estilo Google Lens)**: Detecta texto en pantalla y sobrepone la traducción instantánea sobre la imagen o la copia al portapapeles.
2. **Grabación de Pantalla (MP4)**: Graba video acelerado por hardware con audio del sistema y narración por micrófono con reproductor integrado.
3. **Extracción de Texto (OCR)**: Copia al portapapeles cualquier texto no seleccionable de videos de YouTube, PDFs bloqueados o imágenes (<kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>T</kbd>).
4. **Historial Inteligente con Buscador**: Guarda automáticamente cada captura en `Documentos\Light Capture Studio` e indexa las palabras detectadas para encontrarlas semanas después.
5. **Privacidad 100% Local**: No sube capturas a servidores públicos inseguros (a diferencia de prntscr.com).
6. **Censura y Pixelado Destructivo**: Herramienta de mosaico para difuminar contraseñas, caras o datos sensibles.
7. **Fijar en Pantalla (Pin to Screen)**: Deja la captura flotando sobre cualquier ventana o juego (<kbd>Ctrl</kbd> + <kbd>P</kbd>).
8. **Lector de Códigos QR**: Escanea y abre o copia links de códigos QR en pantalla en 1 clic.
9. **Studio Mockup**: Embellece recortes con fondos degradados modernos y sombras 3D para redes sociales.
10. **Compartir Directo**: Enlace rápido a WhatsApp (App de Windows o Web) y Telegram.

---

## ⌨️ Atajos de Teclado

| Atajo | Acción |
|---|---|
| <kbd>F9</kbd> | Iniciar captura de pantalla en cualquier momento |
| <kbd>Enter</kbd> / <kbd>Ctrl</kbd> + <kbd>C</kbd> | Copiar captura al portapapeles y guardar en historial |
| <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>T</kbd> | Copiar texto extraído por OCR |
| <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>C</kbd> | Copiar texto traducido |
| <kbd>Ctrl</kbd> + <kbd>S</kbd> | Guardar archivo en disco |
| <kbd>Ctrl</kbd> + <kbd>P</kbd> | Fijar captura flotante en pantalla |
| <kbd>Ctrl</kbd> + <kbd>Z</kbd> | Deshacer trazo de dibujo |
| <kbd>Escape</kbd> | Cancelar / Salir |

---

## 🛠️ Tecnologías y Requisitos

- **Plataforma:** Windows 10 / 11 (x64)
- **Framework:** .NET 8 (WPF)
- **Bibliotecas:**
  - `Windows.Media.Ocr` (OCR nativo de Windows)
  - `ScreenRecorderLib` (Codificación H.264 acelerada)
  - `ZXing.Net` (Detección y decodificación de QR)

---

## 🚀 Compilación y Publicación

Para generar el ejecutable portable independiente:

```bash
dotnet publish src/LightCapture.App/LightCapture.App.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish-standalone
```
