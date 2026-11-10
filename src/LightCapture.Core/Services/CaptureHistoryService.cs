using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;

namespace LightCapture.Core.Services;

public record HistoryEntry(
    string Id,
    DateTime Timestamp,
    string ImageFileName,
    string OcrText
);

public interface ICaptureHistoryService
{
    bool IsHistoryEnabled { get; set; }
    string HistoryDirectory { get; }
    void SaveCapture(BitmapSource image, string ocrText);
    IReadOnlyList<HistoryEntry> GetHistory();
    IReadOnlyList<HistoryEntry> Search(string query);
    string GetImagePath(HistoryEntry entry);
    void DeleteEntry(string id);
    void ClearAllHistory();
}

public class CaptureHistoryService : ICaptureHistoryService
{
    private readonly string _historyDir;
    private readonly string _metadataFile;
    private readonly List<HistoryEntry> _entries = new();
    private readonly object _lock = new();

    public bool IsHistoryEnabled { get; set; } = true;
    public string HistoryDirectory => _historyDir;

    public CaptureHistoryService(string? customHistoryDir = null)
    {
        if (!string.IsNullOrWhiteSpace(customHistoryDir))
        {
            _historyDir = customHistoryDir;
        }
        else
        {
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            _historyDir = Path.Combine(docs, "Light Capture");
        }

        Directory.CreateDirectory(_historyDir);
        _metadataFile = Path.Combine(_historyDir, "history.json");

        // Migración transparente de capturas de rutas anteriores si existieran
        try
        {
            var oldDirs = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Light Capture Studio"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LightCaptureTranslator", "History"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LightCaptureStudio", "History")
            };

            foreach (var oldDir in oldDirs)
            {
                if (Directory.Exists(oldDir) && !oldDir.Equals(_historyDir, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var file in Directory.GetFiles(oldDir))
                    {
                        string dest = Path.Combine(_historyDir, Path.GetFileName(file));
                        if (!File.Exists(dest))
                        {
                            File.Copy(file, dest, false);
                        }
                    }
                }
            }
        }
        catch { }

        LoadMetadata();
    }

    private void LoadMetadata()
    {
        lock (_lock)
        {
            if (File.Exists(_metadataFile))
            {
                try
                {
                    string json = File.ReadAllText(_metadataFile);
                    var list = JsonSerializer.Deserialize<List<HistoryEntry>>(json);
                    if (list != null)
                    {
                        _entries.Clear();
                        _entries.AddRange(list);
                    }
                }
                catch { }
            }

            ReconcileExistingImages();
        }
    }

    private void ReconcileExistingImages()
    {
        try
        {
            if (!Directory.Exists(_historyDir)) return;

            var existingFiles = Directory.GetFiles(_historyDir, "*.*")
                .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var indexedFileNames = new HashSet<string>(_entries.Select(e => e.ImageFileName), StringComparer.OrdinalIgnoreCase);
            bool updated = false;

            foreach (var file in existingFiles.OrderByDescending(File.GetLastWriteTime))
            {
                string fileName = Path.GetFileName(file);
                if (!indexedFileNames.Contains(fileName))
                {
                    string id = Guid.NewGuid().ToString("N");
                    DateTime dt = File.GetLastWriteTime(file);
                    _entries.Add(new HistoryEntry(id, dt, fileName, "Captura en disco"));
                    indexedFileNames.Add(fileName);
                    updated = true;
                }
            }

            if (updated)
            {
                _entries.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
                SaveMetadata();
            }
        }
        catch { }
    }

    private void SaveMetadata()
    {
        lock (_lock)
        {
            try
            {
                string json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_metadataFile, json);
            }
            catch { }
        }
    }

    public void SaveCapture(BitmapSource image, string ocrText)
    {
        if (!IsHistoryEnabled || image == null) return;

        try
        {
            Directory.CreateDirectory(_historyDir);

            if (image.CanFreeze && !image.IsFrozen)
            {
                image.Freeze();
            }

            string id = Guid.NewGuid().ToString("N");
            string fileName = $"LightCapture_{DateTime.Now:yyyyMMdd_HHmmss}_{id[..6]}.png";
            string filePath = Path.Combine(_historyDir, fileName);

            // Guardar PNG a disco
            using (var stream = File.Create(filePath))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                encoder.Save(stream);
            }

            lock (_lock)
            {
                _entries.Insert(0, new HistoryEntry(id, DateTime.Now, fileName, ocrText ?? string.Empty));

                // Mantener hasta 100 capturas en historial
                while (_entries.Count > 100)
                {
                    var oldest = _entries[^1];
                    string oldPath = Path.Combine(_historyDir, oldest.ImageFileName);
                    if (File.Exists(oldPath))
                    {
                        try { File.Delete(oldPath); } catch { }
                    }
                    _entries.RemoveAt(_entries.Count - 1);
                }

                SaveMetadata();
            }
        }
        catch { }
    }

    public IReadOnlyList<HistoryEntry> GetHistory()
    {
        lock (_lock)
        {
            ReconcileExistingImages();
            return _entries.ToList();
        }
    }

    public IReadOnlyList<HistoryEntry> Search(string query)
    {
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return _entries.ToList();
            }

            return _entries
                .Where(e => e.OcrText.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    public string GetImagePath(HistoryEntry entry)
    {
        return Path.Combine(_historyDir, entry.ImageFileName);
    }

    public void DeleteEntry(string id)
    {
        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(e => e.Id == id);
            if (entry != null)
            {
                string path = Path.Combine(_historyDir, entry.ImageFileName);
                if (File.Exists(path))
                {
                    try { File.Delete(path); } catch { }
                }
                _entries.Remove(entry);
                SaveMetadata();
            }
        }
    }

    public void ClearAllHistory()
    {
        lock (_lock)
        {
            foreach (var entry in _entries)
            {
                string path = Path.Combine(_historyDir, entry.ImageFileName);
                if (File.Exists(path))
                {
                    try { File.Delete(path); } catch { }
                }
            }
            _entries.Clear();
            SaveMetadata();
        }
    }
}
