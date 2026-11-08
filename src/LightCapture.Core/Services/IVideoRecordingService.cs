using System;
using System.Threading;
using System.Threading.Tasks;

namespace LightCapture.Core.Services;

public interface IVideoRecordingService
{
    bool IsRecording { get; }
    TimeSpan ElapsedTime { get; }

    event Action<TimeSpan>? DurationTick;
    event Action<string>? RecordingComplete;
    event Action<string>? RecordingError;

    Task StartRecordingAsync(int x, int y, int width, int height, bool captureAudio, bool captureMic = true, bool isGif = false, CancellationToken ct = default);
    Task<string> StopRecordingAsync();
    Task CancelRecordingAsync();

    void SetAudioEnabled(bool enabled);
    void SetMicEnabled(bool enabled);
}
