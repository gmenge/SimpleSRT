using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LibVLCSharp.Shared;
using SimpleSRT.App.Services.Interfaces;

namespace SimpleSRT.App.Services;

public class PlayerService : IMediaPlayerService, IDisposable
{
    private readonly LibVLC _libVlc;
    private bool _isDisposed;

    public MediaPlayer MediaPlayer { get; }

    // Gerenciamento de memória para saída física via DeckLink
    private IntPtr _frameBuffer = IntPtr.Zero;
    private int _width = 1920;
    private int _height = 1080;
    private int _stride;

    // Interfaces nativas da Blackmagic
    private IDeckLinkOutput? _deckLinkOutput;
    private bool _isDeckLinkEnabled = false;
    private long _frameCounter = 0;

    public PlayerService()
    {
        LibVLCSharp.Shared.Core.Initialize();

        var options = new[]
        {
            "--no-osd",
            "--no-snapshot-preview",
            "--quiet",
            "--no-stats",
            "--drop-late-frames",
            "--skip-frames"
        };

        _libVlc = new LibVLC(options);
        MediaPlayer = new MediaPlayer(_libVlc);
    }

    public int Volume
    {
        get => MediaPlayer.Volume;
        set => MediaPlayer.Volume = Math.Clamp(value, 0, 100);
    }

    public bool IsMuted
    {
        get => MediaPlayer.Mute;
        set => MediaPlayer.Mute = value;
    }

    public IEnumerable<(string Id, string Description)> GetAudioOutputs()
    {
        var devices = new List<(string Id, string Description)>();
        try
        {
            foreach (var device in MediaPlayer.AudioOutputDeviceEnum)
            {
                if (!string.IsNullOrEmpty(device.DeviceIdentifier))
                {
                    devices.Add((device.DeviceIdentifier, device.Description ?? device.DeviceIdentifier));
                }
            }
        }
        catch { }

        return devices;
    }

    public void SetAudioOutput(string deviceId)
    {
        if (!string.IsNullOrEmpty(deviceId))
        {
            try
            {
                MediaPlayer.SetOutputDevice(deviceId);
            }
            catch { }
        }
    }

    public void Play(string url, int networkCachingMs)
    {
        if (_isDisposed) return;

        Task.Run(() =>
        {
            try
            {
                if (MediaPlayer.IsPlaying)
                {
                    MediaPlayer.Stop();
                }

                var mediaOptions = new[]
                {
                    $":network-caching={networkCachingMs}",
                    ":clock-jitter=0",
                    ":clock-synchro=0"
                };

                using var media = new Media(_libVlc, url, FromType.FromLocation, mediaOptions);
                MediaPlayer.Play(media);
            }
            catch { }
        });
    }

    public void Stop()
    {
        if (_isDisposed) return;

        Task.Run(() =>
        {
            try
            {
                if (MediaPlayer.IsPlaying || MediaPlayer.State == VLCState.Opening || MediaPlayer.State == VLCState.Buffering)
                {
                    MediaPlayer.Stop();
                }
            }
            catch { }
        });
    }

    #region Implementação da Blackmagic DeckLink

    public IEnumerable<string> GetDeckLinkDevices()
    {
        var devices = new List<string>();
        try
        {
            var discovery = new CDeckLinkDiscovery();
            var iterator = (IDeckLinkIterator)discovery;

            while (iterator.Next(out IDeckLink deckLink) == 0)
            {
                deckLink.GetDisplayName(out string displayName);
                if (!string.IsNullOrEmpty(displayName))
                {
                    devices.Add(displayName);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erro ao listar placas DeckLink: {ex.Message}");
        }
        return devices;
    }

    public void EnableDeckLinkOutput(int deviceIndex = 0, int width = 1920, int height = 1080, double fps = 59.94)
    {
        if (_isDisposed) return;

        _width = width;
        _height = height;
        _stride = _width * 2; // Formato UYVY usa 2 bytes por pixel

        // Aloca espaço em memória RAM unmanaged para ponteiro de transferência
        if (_frameBuffer != IntPtr.Zero) Marshal.FreeHGlobal(_frameBuffer);
        _frameBuffer = Marshal.AllocHGlobal(_stride * _height);

        InitDeckLinkHardware(deviceIndex);

        // Define os callbacks de renderização em memória no LibVLC
        MediaPlayer.SetVideoFormat("UYVY", (uint)_width, (uint)_height, (uint)_stride);
        MediaPlayer.SetVideoCallbacks(LockVideoCallback, UnlockVideoCallback, DisplayVideoCallback);

        _isDeckLinkEnabled = true;
    }

    public void DisableDeckLinkOutput()
    {
        _isDeckLinkEnabled = false;

        if (_deckLinkOutput != null)
        {
            try
            {
                _deckLinkOutput.StopScheduledPlayback(0, out _, 1000);
                _deckLinkOutput.DisableVideoOutput();
            }
            catch { }
            _deckLinkOutput = null;
        }

        if (_frameBuffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_frameBuffer);
            _frameBuffer = IntPtr.Zero;
        }
    }

    private void InitDeckLinkHardware(int deviceIndex)
    {
        var discovery = new CDeckLinkDiscovery();
        var iterator = (IDeckLinkIterator)discovery;

        IDeckLink? selectedDevice = null;
        int current = 0;

        while (iterator.Next(out IDeckLink deckLink) == 0)
        {
            if (current == deviceIndex)
            {
                selectedDevice = deckLink;
                break;
            }
            current++;
        }

        if (selectedDevice == null)
            throw new InvalidOperationException($"Placa DeckLink no índice {deviceIndex} não foi encontrada.");

        _deckLinkOutput = (IDeckLinkOutput)selectedDevice;

        // Configura padrão de transmissão HD1080p59.94 em UYVY
        _deckLinkOutput.EnableVideoOutput(BMDDisplayMode.bmdModeHD1080p5994, BMDVideoOutputFlags.bmdVideoOutputFlagDefault);
        _deckLinkOutput.StartScheduledPlayback(0, 1000, 1.0);
    }

    private IntPtr LockVideoCallback(IntPtr opaque, IntPtr planes)
    {
        Marshal.WriteIntPtr(planes, _frameBuffer);
        return IntPtr.Zero;
    }

    private void UnlockVideoCallback(IntPtr opaque, IntPtr picture, IntPtr planes)
    {
    }

    private void DisplayVideoCallback(IntPtr opaque, IntPtr picture)
    {
        if (!_isDeckLinkEnabled || _deckLinkOutput == null || _frameBuffer == IntPtr.Zero) return;

        _deckLinkOutput.CreateVideoFrame(_width, _height, _stride, BMDPixelFormat.bmdFormat8BitYUV, 0, out IDeckLinkVideoFrame videoFrame);

        videoFrame.GetBytes(out IntPtr deckLinkBuffer);

        unsafe
        {
            Buffer.MemoryCopy(_frameBuffer.ToPointer(), deckLinkBuffer.ToPointer(), _stride * _height, _stride * _height);
        }

        long frameDuration = 1000;
        long timeScale = 60000;
        _deckLinkOutput.ScheduleVideoFrame(videoFrame, _frameCounter * frameDuration, frameDuration, timeScale);
        _frameCounter++;
    }

    #endregion

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        DisableDeckLinkOutput();

        Task.Run(() =>
        {
            try
            {
                if (MediaPlayer.IsPlaying) MediaPlayer.Stop();
                MediaPlayer.Dispose();
                _libVlc.Dispose();
            }
            catch { }
        });

        GC.SuppressFinalize(this);
    }
}