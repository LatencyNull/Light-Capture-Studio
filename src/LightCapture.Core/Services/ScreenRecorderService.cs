using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LightCapture.Core.Services;

public class ScreenRecorderService : IVideoRecordingService, IDisposable
{
    private Process? _workerProcess;
    private TaskCompletionSource<string>? _stopTcs;
    private string? _currentFilePath;
    private readonly Stopwatch _stopwatch = new();
    private bool _isCancelled;

    public bool IsRecording { get; private set; }
    public TimeSpan ElapsedTime => _stopwatch.Elapsed;

    public event Action<TimeSpan>? DurationTick;
    public event Action<string>? RecordingComplete;
    public event Action<string>? RecordingError;

    public Task StartRecordingAsync(int x, int y, int width, int height, bool captureAudio, bool captureMic = true, bool isGif = false, int fps = 60, CancellationToken ct = default)
    {
        if (IsRecording)
        {
            throw new InvalidOperationException("A recording is already in progress.");
        }

        // Ensure even dimensions (required by H.264 encoders)
        if (width % 2 != 0) width++;
        if (height % 2 != 0) height++;

        if (width < 32 || height < 32)
        {
            throw new ArgumentException("The selected area is too small to record video.");
        }

        string capturesFolder = AppPaths.GetCapturesDirectory();

        string ext = isGif ? "gif" : "mp4";
        _currentFilePath = Path.Combine(capturesFolder, $"Clip_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}");
        _isCancelled = false;
        _stopTcs = new TaskCompletionSource<string>();

        // Localizar el ejecutable del grabador
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string appDataBin = AppPaths.GetRecorderBinDirectory();

        string workerExe = Path.Combine(baseDir, "MasterCapture.Recorder.exe");
        if (!File.Exists(workerExe))
        {
            workerExe = Path.Combine(appDataBin, "MasterCapture.Recorder.exe");
        }

        if (!File.Exists(workerExe))
        {
            string[] possiblePaths = new[]
            {
                Path.Combine(baseDir, "..", "..", "..", "LightCapture.Recorder", "bin", "x64", "Debug", "net8.0-windows10.0.19041.0", "MasterCapture.Recorder.exe"),
                Path.Combine(baseDir, "..", "..", "..", "LightCapture.Recorder", "bin", "x64", "Release", "net8.0-windows10.0.19041.0", "MasterCapture.Recorder.exe"),
                Path.Combine(baseDir, "..", "..", "..", "..", "src", "LightCapture.Recorder", "bin", "x64", "Debug", "net8.0-windows10.0.19041.0", "MasterCapture.Recorder.exe"),
                Path.Combine(baseDir, "..", "..", "..", "..", "src", "LightCapture.Recorder", "bin", "x64", "Release", "net8.0-windows10.0.19041.0", "MasterCapture.Recorder.exe")
            };

            string? found = possiblePaths.FirstOrDefault(File.Exists);
            if (found != null)
            {
                workerExe = Path.GetFullPath(found);
            }
            else
            {
                throw new FileNotFoundException($"Recorder module not found: {workerExe}");
            }
        }

        var readyTcs = new TaskCompletionSource<bool>();

        var psi = new ProcessStartInfo
        {
            FileName = workerExe,
            Arguments = $"{x} {y} {width} {height} {captureAudio.ToString().ToLower()} {captureMic.ToString().ToLower()} \"{_currentFilePath}\" {fps}",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        _workerProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };

        _workerProcess.OutputDataReceived += (s, e) =>
        {
            if (string.IsNullOrEmpty(e.Data)) return;

            string line = e.Data.Trim();
            if (line.StartsWith("READY"))
            {
                readyTcs.TrySetResult(true);
            }
            else if (line.StartsWith("TICK:"))
            {
                if (long.TryParse(line.Substring(5), out long ms))
                {
                    DurationTick?.Invoke(TimeSpan.FromMilliseconds(ms));
                }
            }
            else if (line.StartsWith("COMPLETE:"))
            {
                string path = line.Substring(9);
                IsRecording = false;
                _stopwatch.Stop();
                RecordingComplete?.Invoke(path);
                _stopTcs?.TrySetResult(path);
            }
            else if (line.StartsWith("ERROR:"))
            {
                string err = line.Substring(6);
                IsRecording = false;
                _stopwatch.Stop();
                RecordingError?.Invoke(err);
                _stopTcs?.TrySetException(new Exception(err));
                readyTcs.TrySetException(new Exception(err));
            }
        };

        _workerProcess.Exited += (s, e) =>
        {
            IsRecording = false;
            _stopwatch.Stop();
            if (!_isCancelled && !(_stopTcs?.Task.IsCompleted ?? true))
            {
                if (File.Exists(_currentFilePath) && new FileInfo(_currentFilePath).Length > 0)
                {
                    RecordingComplete?.Invoke(_currentFilePath);
                    _stopTcs?.TrySetResult(_currentFilePath);
                }
                else
                {
                    _stopTcs?.TrySetException(new Exception("Recording process terminated unexpectedly."));
                }
            }
        };

        _workerProcess.Start();
        _workerProcess.BeginOutputReadLine();

        IsRecording = true;
        _stopwatch.Restart();

        return readyTcs.Task;
    }

    public async Task<string> StopRecordingAsync()
    {
        if (!IsRecording || _workerProcess == null)
        {
            return _currentFilePath ?? string.Empty;
        }

        try
        {
            _workerProcess.StandardInput.WriteLine("STOP");
            _workerProcess.StandardInput.Flush();
        }
        catch { }

        if (_stopTcs != null)
        {
            var completedTask = await Task.WhenAny(_stopTcs.Task, Task.Delay(10000));
            if (completedTask == _stopTcs.Task)
            {
                return await _stopTcs.Task;
            }
        }

        try
        {
            if (!_workerProcess.HasExited) _workerProcess.Kill();
        }
        catch { }

        return _currentFilePath ?? string.Empty;
    }

    public Task CancelRecordingAsync()
    {
        if (!IsRecording || _workerProcess == null)
        {
            return Task.CompletedTask;
        }

        _isCancelled = true;
        try
        {
            _workerProcess.StandardInput.WriteLine("CANCEL");
            _workerProcess.StandardInput.Flush();
        }
        catch { }

        try
        {
            if (!_workerProcess.WaitForExit(3000))
            {
                _workerProcess.Kill();
            }
        }
        catch { }

        try
        {
            if (File.Exists(_currentFilePath))
            {
                File.Delete(_currentFilePath);
            }
        }
        catch { }

        IsRecording = false;
        _stopwatch.Stop();
        return Task.CompletedTask;
    }

    public void SetAudioEnabled(bool enabled)
    {
        if (IsRecording && _workerProcess != null && !_workerProcess.HasExited)
        {
            try
            {
                _workerProcess.StandardInput.WriteLine($"SET_AUDIO:{enabled.ToString().ToLower()}");
                _workerProcess.StandardInput.Flush();
            }
            catch { }
        }
    }

    public void SetMicEnabled(bool enabled)
    {
        if (IsRecording && _workerProcess != null && !_workerProcess.HasExited)
        {
            try
            {
                _workerProcess.StandardInput.WriteLine($"SET_MIC:{enabled.ToString().ToLower()}");
                _workerProcess.StandardInput.Flush();
            }
            catch { }
        }
    }

    public void Dispose()
    {
        try
        {
            if (_workerProcess != null && !_workerProcess.HasExited)
            {
                _workerProcess.Kill();
                _workerProcess.Dispose();
            }
        }
        catch { }
    }
}
