using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using ScreenRecorderLib;

namespace LightCapture.RecorderWorker
{
    class Program
    {
        private static Recorder _recorder;
        private static string _outputPath;
        private static bool _isCancelled = false;
        private static readonly ManualResetEvent _completeEvent = new ManualResetEvent(false);
        private static readonly Stopwatch _stopwatch = new Stopwatch();
        private static System.Threading.Timer _tickTimer;
        private static LoopbackAudioSource _loopbackSource;
        private static CaptureAudioSource _micSource;

        static void Main(string[] args)
        {
            if (args.Length < 6)
            {
                Console.WriteLine("ERROR:Argumentos insuficientes. Uso: <x> <y> <w> <h> <captureAudio:true/false> [captureMic:true/false] <outputPath>");
                return;
            }

            int x = int.Parse(args[0]);
            int y = int.Parse(args[1]);
            int width = int.Parse(args[2]);
            int height = int.Parse(args[3]);
            bool captureAudio = bool.Parse(args[4]);
            bool captureMic = false;

            if (args.Length >= 7)
            {
                captureMic = bool.Parse(args[5]);
                _outputPath = args[6];
            }
            else
            {
                _outputPath = args[5];
            }

            // Requisito de H.264: dimensiones pares
            if (width % 2 != 0) width++;
            if (height % 2 != 0) height++;

            try
            {
                // Determinar la pantalla donde se encuentra el área de captura
                var centerPoint = new Point(x + width / 2, y + height / 2);
                var screen = Screen.FromPoint(centerPoint);
                string deviceName = screen.DeviceName;

                // Coordenadas locales relativas a la pantalla seleccionada
                int localX = Math.Max(0, x - screen.Bounds.X);
                int localY = Math.Max(0, y - screen.Bounds.Y);

                var sources = new List<RecordingSourceBase>
                {
                    new DisplayRecordingSource
                    {
                        DeviceName = deviceName,
                        SourceRect = new ScreenRect(localX, localY, width, height)
                    }
                };

                // Configurar fuentes de audio
                var audioSources = new List<AudioSourceBase>();

                if (captureAudio)
                {
                    try
                    {
                        var loopbackDevs = Recorder.GetSystemAudioLoopbackDevices();
                        if (loopbackDevs != null && loopbackDevs.Count > 0)
                        {
                            string loopDevName = loopbackDevs[0].DeviceName;
                            foreach (var d in loopbackDevs)
                            {
                                if (d.IsDefaultDevice)
                                {
                                    loopDevName = d.DeviceName;
                                    break;
                                }
                            }
                            _loopbackSource = new LoopbackAudioSource
                            {
                                DeviceName = loopDevName,
                                IsAudioCaptureEnabled = true
                            };
                            audioSources.Add(_loopbackSource);
                        }
                    }
                    catch { }
                }

                if (captureMic)
                {
                    try
                    {
                        var captureDevs = Recorder.GetSystemAudioCaptureDevices();
                        if (captureDevs != null && captureDevs.Count > 0)
                        {
                            string micDevName = captureDevs[0].DeviceName;
                            foreach (var d in captureDevs)
                            {
                                if (d.IsDefaultDevice)
                                {
                                    micDevName = d.DeviceName;
                                    break;
                                }
                            }
                            _micSource = new CaptureAudioSource
                            {
                                DeviceName = micDevName,
                                IsAudioCaptureEnabled = true
                            };
                            audioSources.Add(_micSource);
                        }
                    }
                    catch { }
                }

                var options = new RecorderOptions
                {
                    SourceOptions = new SourceOptions
                    {
                        RecordingSources = sources
                    },
                    OutputOptions = new OutputOptions
                    {
                        RecorderMode = RecorderMode.Video
                    },
                    VideoEncoderOptions = new VideoEncoderOptions
                    {
                        Framerate = 30,
                        Quality = 80,
                        IsFixedFramerate = true
                    },
                    AudioOptions = new AudioOptions
                    {
                        IsAudioEnabled = audioSources.Count > 0,
                        AudioSources = audioSources
                    }
                };

                _recorder = Recorder.CreateRecorder(options);
                _recorder.OnRecordingComplete += (s, e) =>
                {
                    if (_tickTimer != null) _tickTimer.Dispose();
                    _stopwatch.Stop();
                    if (_isCancelled)
                    {
                        try { if (File.Exists(_outputPath)) File.Delete(_outputPath); } catch { }
                        Console.WriteLine("CANCELLED");
                    }
                    else
                    {
                        Console.WriteLine("COMPLETE:" + e.FilePath);
                    }
                    _completeEvent.Set();
                };

                _recorder.OnRecordingFailed += (s, e) =>
                {
                    if (_tickTimer != null) _tickTimer.Dispose();
                    _stopwatch.Stop();
                    Console.WriteLine("ERROR:" + e.Error);
                    _completeEvent.Set();
                };

                // Asegurar que el directorio de salida existe
                string dir = Path.GetDirectoryName(_outputPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                _recorder.Record(_outputPath);
                _stopwatch.Start();

                // Notificar que la grabación comenzó
                Console.WriteLine("READY");

                // Timer de progreso (cada 500 ms)
                _tickTimer = new System.Threading.Timer(state =>
                {
                    if (_stopwatch.IsRunning)
                    {
                        Console.WriteLine("TICK:" + _stopwatch.ElapsedMilliseconds);
                    }
                }, null, 500, 500);

                // Hilo de escucha de comandos Stdin
                var inputThread = new Thread(() =>
                {
                    while (true)
                    {
                        string line = Console.ReadLine();
                        if (line == null) break;
                        line = line.Trim();
                        if (line.Equals("STOP", StringComparison.OrdinalIgnoreCase))
                        {
                            if (_recorder != null) _recorder.Stop();
                            break;
                        }
                        else if (line.Equals("CANCEL", StringComparison.OrdinalIgnoreCase))
                        {
                            _isCancelled = true;
                            if (_recorder != null) _recorder.Stop();
                            break;
                        }
                        else if (line.StartsWith("SET_AUDIO:", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                bool enabled = bool.Parse(line.Substring(10));
                                if (_recorder != null && _loopbackSource != null)
                                {
                                    _loopbackSource.IsAudioCaptureEnabled = enabled;
                                    _recorder.GetDynamicOptionsBuilder()
                                        .SetUpdatedAudioSource(_loopbackSource)
                                        .Apply();
                                }
                            }
                            catch { }
                        }
                        else if (line.StartsWith("SET_MIC:", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                bool enabled = bool.Parse(line.Substring(8));
                                if (_recorder != null && _micSource != null)
                                {
                                    _micSource.IsAudioCaptureEnabled = enabled;
                                    _recorder.GetDynamicOptionsBuilder()
                                        .SetUpdatedAudioSource(_micSource)
                                        .Apply();
                                }
                            }
                            catch { }
                        }
                    }
                })
                {
                    IsBackground = true
                };
                inputThread.Start();

                // Esperar a que la grabación finalice
                _completeEvent.WaitOne();
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR:" + ex.Message);
            }
            finally
            {
                if (_recorder != null) _recorder.Dispose();
            }
        }
    }
}
