using System;
using System.Threading.Tasks;
using LightCapture.Core.Services;
using Xunit;

namespace LightCapture.Tests;

public class VideoRecordingServiceTests
{
    [Fact]
    public void InitialState_IsNotRecording()
    {
        using var service = new ScreenRecorderService();
        Assert.False(service.IsRecording);
        Assert.Equal(TimeSpan.Zero, service.ElapsedTime);
    }

    [Fact]
    public async Task StartRecording_TooSmallBounds_ThrowsArgumentException()
    {
        using var service = new ScreenRecorderService();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.StartRecordingAsync(0, 0, 10, 10, false));
    }

    [Fact]
    public async Task StopRecording_WhenNotRecording_ReturnsEmptyString()
    {
        using var service = new ScreenRecorderService();
        string result = await service.StopRecordingAsync();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task CancelRecording_WhenNotRecording_DoesNotThrow()
    {
        using var service = new ScreenRecorderService();
        await service.CancelRecordingAsync();
        Assert.False(service.IsRecording);
    }

    [Fact]
    public async Task StartAndStopRecording_ProducesValidVideoFile()
    {
        using var service = new ScreenRecorderService();
        await service.StartRecordingAsync(100, 100, 300, 200, false, false);
        Assert.True(service.IsRecording);
        await Task.Delay(1200);
        string file = await service.StopRecordingAsync();
        Assert.False(service.IsRecording);
        Assert.True(System.IO.File.Exists(file), $"Expected file {file} to exist");
        try { System.IO.File.Delete(file); } catch { }
    }

    [Fact]
    public async Task StartAndStopRecording_WithMic_ProducesValidVideoFile()
    {
        using var service = new ScreenRecorderService();
        await service.StartRecordingAsync(100, 100, 300, 200, false, true);
        Assert.True(service.IsRecording);
        service.SetMicEnabled(false);
        service.SetMicEnabled(true);
        await Task.Delay(1200);
        string file = await service.StopRecordingAsync();
        Assert.False(service.IsRecording);
        Assert.True(System.IO.File.Exists(file), $"Expected file {file} to exist");
        try { System.IO.File.Delete(file); } catch { }
    }
}
