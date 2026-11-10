using System;
using System.IO;
using System.Windows.Media.Imaging;
using Clipboard = System.Windows.Clipboard;
using DataFormats = System.Windows.DataFormats;
using DataObject = System.Windows.DataObject;

namespace LightCapture.Core.Services;

/// <summary>
/// Helper para copiar imágenes al portapapeles de Windows de forma compatible
/// con navegadores modernos (Google Chrome, Edge, Firefox), aplicaciones web
/// (Gemini, ChatGPT, WhatsApp Web) y suites de oficina.
/// Resuelve el problema donde WPF Clipboard.SetImage pierde datos al cerrar la ventana
/// y carece de flujos PNG y CF_DIB limpios.
/// </summary>
public static class ClipboardHelper
{
    public static void CopyImageToClipboard(BitmapSource? bitmapSource)
    {
        if (bitmapSource == null) return;

        try
        {
            var dataObject = new DataObject();

            // 1. Formato Bitmap estándar de WPF / Windows
            dataObject.SetData(DataFormats.Bitmap, bitmapSource, true);

            // 2. Flujo PNG: requerido por navegadores web (Chrome, Edge) y apps como Discord/Telegram
            using var pngMs = new MemoryStream();
            var pngEncoder = new PngBitmapEncoder();
            pngEncoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            pngEncoder.Save(pngMs);
            var pngBytes = pngMs.ToArray();
            var pngStream = new MemoryStream(pngBytes);
            dataObject.SetData("PNG", pngStream, false);

            // 3. Flujo DIB (Device Independent Bitmap) sin el encabezado BITMAPFILEHEADER de 14 bytes:
            // Google Chrome y WebView2 leen CF_DIB directamente desde el portapapeles.
            using var bmpMs = new MemoryStream();
            var bmpEncoder = new BmpBitmapEncoder();
            bmpEncoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            bmpEncoder.Save(bmpMs);
            byte[] bmpBytes = bmpMs.ToArray();

            if (bmpBytes.Length > 14)
            {
                var dibStream = new MemoryStream(bmpBytes, 14, bmpBytes.Length - 14);
                dataObject.SetData(DataFormats.Dib, dibStream, false);
            }

            // CRÍTICO: copy: true asegura que los datos permanezcan en el portapapeles del sistema
            // incluso después de que la ventana de superposición o la aplicación se cierre.
            Clipboard.SetDataObject(dataObject, copy: true);
        }
        catch
        {
            // Fallback de seguridad al método nativo de WPF
            try
            {
                Clipboard.SetImage(bitmapSource);
            }
            catch { }
        }
    }

    public static void CopyText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        try
        {
            var dataObject = new DataObject();
            dataObject.SetData(DataFormats.UnicodeText, text, true);
            dataObject.SetData(DataFormats.Text, text, true);
            Clipboard.SetDataObject(dataObject, copy: true);
        }
        catch
        {
            try
            {
                Clipboard.SetText(text);
            }
            catch { }
        }
    }
}
