using System.Speech.Synthesis;

namespace LightCapture.Core.Services;

public interface ISpeechService
{
    void Speak(string text);
    void Stop();
}

public class SpeechService : ISpeechService, IDisposable
{
    private readonly SpeechSynthesizer _synthesizer;

    public SpeechService()
    {
        _synthesizer = new SpeechSynthesizer();
        SelectSpanishVoice();
    }

    private void SelectSpanishVoice()
    {
        try
        {
            var voices = _synthesizer.GetInstalledVoices();
            foreach (var voice in voices)
            {
                if (voice.Enabled && voice.VoiceInfo.Culture.TwoLetterISOLanguageName.Equals("es", StringComparison.OrdinalIgnoreCase))
                {
                    _synthesizer.SelectVoice(voice.VoiceInfo.Name);
                    return;
                }
            }
        }
        catch { }
    }

    public void Speak(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        Stop();
        try
        {
            _synthesizer.SpeakAsync(text);
        }
        catch { }
    }

    public void Stop()
    {
        try
        {
            _synthesizer.SpeakAsyncCancelAll();
        }
        catch { }
    }

    public void Dispose()
    {
        _synthesizer.Dispose();
    }
}
