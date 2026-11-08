# Especificación de Diseño: Light Capture Translator (Fase 2: Killer Features)

**Fecha:** 2026-11-06  
**Estado:** Aprobado  
**Ubicación:** `docs/superpowers/specs/2026-11-06-light-capture-translator-design.md`

---

## 1. Intención y Propósito

El proyecto busca crear el reemplazo definitivo y superior a **Lightshot**. Combina la interfaz familiar y ultrarrápida de Lightshot con capacidades modernas de inteligencia óptica y productividad que Lightshot nunca implementó:
1. **Traducción Óptica In-Place (Google Lens)** con endpoint de Google Translate (`client=gtx`).
2. **Herramienta de Censura / Pixelado (Blur & Pixelate)** para proteger contraseñas, tokens y datos sensibles sin tachones toscos.
3. **Copiar Texto Original (OCR Directo)** para extraer texto bloqueado en imágenes, videos o errores de Windows.
4. **Pasos Numerados Automáticos (Burbujas ①, ②, ③)** para crear tutoriales y reportar bugs con un solo clic por paso.
5. **Escáner Automático de Códigos QR** con detección en tiempo real y botón de copia/apertura de enlaces.
6. **Historial Local de Capturas con Búsqueda de Texto OCR** para recuperar capturas recientes buscando palabras clave.

---

## 2. Especificación de las Nuevas Funciones

### Función 1: Herramienta de Pixelado / Censura (Pixelate / Blur)
* **Ubicación:** Barra vertical derecha, justo después del Marcador fluorescente.
* **Comportamiento:** Al seleccionar la herramienta y arrastrar un rectángulo sobre cualquier parte de la selección, toma los píxeles de esa región y los agrupa en bloques mosaico (pixelado de 10x10 px) o desenfoque.
* **Undo:** Compatible con la pila de deshacer (`Ctrl + Z`).

### Función 2: Copiar Texto Original (OCR Directo)
* **Ubicación:** Barra horizontal inferior (icono de documento/portapapeles junto a Traducir).
* **Atajo:** `Ctrl + Shift + T` o botón en barra.
* **Comportamiento:** Ejecuta `Windows.Media.Ocr`, une todas las líneas de texto detectadas y las copia directamente al portapapeles con una notificación toast: *"Texto copiado al portapapeles"*.

### Función 3: Pasos Numerados Automáticos (Burbujas ①, ②, ③)
* **Ubicación:** Barra vertical derecha (icono `①`).
* **Comportamiento:** Cada clic del ratón dentro de la selección estampa un círculo sólido con el color activo y un número secuencial blanco (`1`, `2`, `3`...).
* **Undo:** Al presionar `Ctrl + Z` se elimina el último número y el contador retrocede automáticamente.

### Función 4: Escáner Automático de Códigos QR
* **Comportamiento:** Al recortar un área, un servicio en segundo plano analiza el bitmap con `ZXing.Net`. Si detecta un código QR o código de barras, despliega una píldora flotante elegante en la parte superior: `🔗 [URL / Texto del QR] [Copiar] [Abrir]`.

### Función 5: Historial Local con Búsqueda por Texto
* **Almacenamiento:** `%LOCALAPPDATA%\LightCaptureTranslator\History\` guarda las últimas 50 capturas (`.png`) junto a un archivo `history.json` que asocia cada captura con la fecha y el texto indexado por OCR.
* **Acceso:** Opción en el menú de la bandeja del sistema: *"🕒 Historial de capturas"*.
* **Interfaz:** Ventana moderna tipo galería (`HistoryWindow.xaml`) con barra de búsqueda reactiva para encontrar capturas por texto contenido en pantalla.
