<div align="center">

# ⚡ Light Capture Studio

**La suite definitiva de captura de pantalla, traducción en vivo, OCR y grabación de video para Windows.**  
*Desarrollado en .NET 8, con interfaz moderna Samsung One UI Dark y privacidad 100% local.*

[![Licencia: MIT](https://img.shields.io/badge/Licencia-MIT-blue.svg)](LICENSE)
[![Plataforma](https://img.shields.io/badge/Plataforma-Windows%2010%20%2F%2011%20(x64)-0078D6.svg)](https://microsoft.com)
[![.NET](https://img.shields.io/badge/.NET-8.0%20WPF-512BD4.svg)](https://dotnet.microsoft.com/)

[Características](#-características-principales) • [Comparativa](#-por-qué-light-capture-studio-vs-lightshot-clásico) • [Atajos de Teclado](#-atajos-de-teclado) • [Versión Portable](#-versión-portable) • [Privacidad](#-arquitectura-con-privacidad-primero)

</div>

---

## 🚀 Descripción General

**Light Capture Studio** es una suite de captura, traducción y grabación de pantalla para Windows, diseñada como una evolución moderna de herramientas clásicas como Lightshot. Su propósito es consolidar en un único ejecutable ligero y portable las tareas cotidianas que antes requerían múltiples programas dispersos: capturar áreas con precisión, traducir textos de inmediato sin salir de la pantalla, grabar clips con audio y extraer texto de imágenes mediante OCR.

A diferencia del software de captura convencional, este proyecto sigue una arquitectura **100% local**: no sube imágenes a servidores públicos ni depende de servicios web de terceros para el almacenamiento. Todo el procesamiento de imágenes, OCR, codificación de video e indexación de historial se ejecuta dentro de tu propio equipo.

---

## 🥊 ¿Por qué Light Capture Studio? (vs. Lightshot Clásico)

| Característica | ⚡ Light Capture Studio | 🐢 Lightshot Clásico |
|:---|:---:|:---:|
| **Traducción en Vivo en Pantalla** | ✅ Capa estilo Google Lens integrada | ❌ No disponible |
| **Grabación de Video y GIF** | ✅ MP4 (audio + micrófono) y GIFs animados ligeros | ❌ Solo capturas estáticas |
| **Anotaciones en Video y Línea de Tiempo** | ✅ Flechas, texto, cajas, pluma y censura con pistas de duración | ❌ Imposible |
| **Previsualización de Video en Tiempo Real** | ✅ Vista previa fluida a ~40 fps, búsqueda y botón Stop | ❌ No disponible |
| **Extracción de Texto OCR** | ✅ Copiado instantáneo al portapapeles (<kbd>Ctrl+Shift+T</kbd>) | ❌ Imposible (solo píxeles) |
| **Historial Local con Búsqueda** | ✅ Busca capturas anteriores por el texto que contienen | ❌ Inexistente (se pierde al cerrar) |
| **Privacidad y Seguridad** | ✅ 100% almacenamiento local en `Documentos` | ⚠️ Subidas públicas inseguras |
| **Censura Inteligente / Privacidad** | ✅ Pincel de pixelado mosaico destructivo e irreversible | ❌ Garabatos toscos con pluma |
| **Fijar en Pantalla (Picture-in-Picture)** | ✅ Ventana flotante siempre visible (<kbd>Ctrl+P</kbd>) | ❌ Requiere visor externo |
| **Reconocimiento de Códigos QR** | ✅ Detección automática y apertura con un clic | ❌ Requiere escanear con el móvil |
| **Compartir Directo e Instantáneo** | ✅ WhatsApp App (Windows) y Web, Telegram | ❌ Enlaces sociales rotos o anticuados |
| **Diseño y Estética** | ✅ Moderno Samsung One UI Dark / Fluent | ❌ Paneles grises de la era Win7 / XP |

---

## 🌟 Características Principales

### 1. 🌐 Traducción en Vivo en Pantalla (Modo Lens)
Selecciona cualquier área de la pantalla —en un videojuego, artículo en otro idioma o documento protegido— y presiona **Traducir**. Los bloques de texto se reemplazan directamente en pantalla conservando el tamaño de fuente y la posición original, o se copian al portapapeles con formato listo para pegar.

### 2. 🎥 Grabación de Video MP4 y GIF con Anotaciones y Línea de Tiempo
¿Necesitas reportar un bug, demostrar una partida o crear un tutorial interactivo? Graba cualquier región del escritorio a 30/60 FPS en H.264 MP4 con sonido del sistema y voz por micrófono, o exporta GIFs animados ligeros (de hasta 15 segundos) ideales para chats y foros:

- ↗️ **Herramientas de Anotación en Video**: Coloca flechas indicadoras (`↗️`), textos flotantes personalizados (`🔤`), rectángulos (`🔲`), trazos libres (`✏️`) y bloques sólidos de censura Knox (`🔒`) directamente sobre el video.
- ⏱️ **Línea de Tiempo de Objetos**: Se genera automáticamente una pista visual por cada elemento añadido. Arrastra los bloques horizontalmente para fijar en qué segundo aparecen y ajusta sus extremos para definir su duración exacta.
- ⚡ **Previsualización y Fregado en Tiempo Real**: Adelanta, atrasa o arrastra el cabezal de reproducción con vista previa en vivo a ~40 FPS incluso mientras está en pausa. Recorta el inicio y el final con precisión o presiona el botón **Stop** (`⏹`) para reiniciar la reproducción al instante.
- 🎨 **Estampado por Hardware**: Todas las anotaciones y pistas temporales se procesan y se integran al video final MP4 o GIF mediante aceleración por hardware (FFmpeg / Windows Media).

### 3. 🔍 Extracción de Texto OCR Nativo de Windows
Copia texto desde cualquier lugar: videos de YouTube, subtítulos, páginas web protegidas o documentos escaneados. Presiona <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>T</kbd> y el texto detectado quedará listo en tu portapapeles.

### 4. 🕒 Historial Local Inteligente con Búsqueda
Cada vez que copias (<kbd>Ctrl</kbd> + <kbd>C</kbd>, <kbd>Enter</kbd> o desde la barra), tu captura se guarda automáticamente en `Documentos\Light Capture Studio` y su contenido de texto se indexa. Abre la galería del historial (<kbd>F9</kbd> → icono de reloj) para buscar por palabras clave, copiar miniaturas o abrir la carpeta con un solo clic.

### 5. 🛡️ Censura Destructiva por Pixelado
Oculta contraseñas, números de tarjeta, rostros o datos confidenciales con total tranquilidad. El pincel de censura aplica un mosaico irreversible directamente sobre los píxeles de la imagen.

### 6. 📌 Fijar en Pantalla (Picture-in-Picture)
Mantén una captura flotando siempre encima de otras ventanas mientras programas, diseñas o investigas. Puedes redimensionarla, moverla por la pantalla o cerrarla cuando ya no la necesites.

### 7. 📱 Compartir Directo en WhatsApp y Telegram
Envía capturas y videos directamente a **WhatsApp Desktop (App de Windows)**, **WhatsApp Web**, Telegram o realiza búsquedas visuales inversas con Google Lens.

---

## ⌨️ Atajos de Teclado

| Atajo | Descripción |
|---|---|
| <kbd>F9</kbd> o <kbd>ImprPant</kbd> | Activar el selector de captura en cualquier lugar |
| <kbd>Enter</kbd> o <kbd>Ctrl</kbd> + <kbd>C</kbd> | Copiar captura al portapapeles y guardarla en el historial |
| <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>T</kbd> | Extraer y copiar texto OCR al portapapeles |
| <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>C</kbd> | Copiar texto traducido al portapapeles |
| <kbd>Ctrl</kbd> + <kbd>S</kbd> | Abrir diálogo de guardar (carpeta predeterminada `Documentos\Light Capture Studio`) |
| <kbd>Ctrl</kbd> + <kbd>P</kbd> | Fijar captura como ventana flotante siempre visible (Pin) |
| <kbd>Ctrl</kbd> + <kbd>Z</kbd> | Deshacer último trazo o anotación |
| <kbd>Esc</kbd> | Cancelar captura / Cerrar panel |

---

## 💾 Versión Portable

**Light Capture Studio es 100% portable y autónomo.**
- **Sin instalación:** No requiere instalador, asistentes ni permisos de administrador.
- **Autocontenido:** Desarrollado con todos los componentes de .NET 8 integrados en un único archivo ejecutable.
- **Limpio y ligero:** No deja residuos en el registro de Windows y funciona directamente desde cualquier carpeta o memoria USB.

¡Descarga el ejecutable portable compilado directamente desde la sección de [**Releases**](https://github.com/LatencyNull/Light-Capture-Studio/releases)!

---

## 🔒 Arquitectura con Privacidad Primero

A diferencia de Lightshot clásico y herramientas antiguas que suben las capturas a enlaces web públicos (donde cualquiera puede adivinar URLs consecutivas y ver documentos privados), **Light Capture Studio opera con un modelo 100% local**:

- Las capturas se guardan exclusivamente en tu carpeta personal `Documentos\Light Capture Studio`.
- El reconocimiento óptico de caracteres (OCR) se ejecuta localmente mediante las APIs nativas de Windows (`Windows.Media.Ocr`).
- Sin telemetría oculta en segundo plano, sin cuentas obligatorias y sin rastreadores externos.

---

## 🛠️ Requisitos del Sistema y Tecnologías

- **Sistema Operativo:** Windows 10 (Compilación 19041+) o Windows 11 (64 bits)
- **Entorno de Ejecución:** .NET 8.0 Desktop Runtime (o usar el ejecutable portable autocontenido)
- **Bibliotecas Principales:**
  - `Windows.Media.Ocr` (Motor OCR nativo acelerado por hardware de Windows)
  - `ScreenRecorderLib` (Codificación de video H.264 por hardware con DirectX y Media Foundation)
  - `ZXing.Net` (Lector de alta velocidad para códigos QR y códigos de barras)

---

## 📦 Compilación desde el Código Fuente

Clona el repositorio y compila el ejecutable portable independiente utilizando el SDK de .NET 8:

```powershell
# Clonar el repositorio
git clone https://github.com/LatencyNull/Light-Capture-Studio.git
cd Light-Capture-Studio

# Publicar el ejecutable portable autocontenido (x64)
dotnet publish src/LightCapture.App/LightCapture.App.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish-standalone
```

El archivo resultante en `publish-standalone\LightCapture.App.exe` es totalmente portable y listo para ejecutarse sin instalación.

---

## 📄 Licencia

Este proyecto está bajo la [Licencia MIT](LICENSE) — libre para uso personal, educativo y comercial.

---

<div align="center">
  <sub>Desarrollado con dedicación para los flujos de trabajo modernos en Windows. Si este proyecto te resulta útil, ¡considera dejar una ⭐!</sub>
</div>
