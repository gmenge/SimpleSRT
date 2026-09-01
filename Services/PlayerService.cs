using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LibVLCSharp.Shared;
using SimpleSRT.App.Services.Interfaces;

namespace SimpleSRT.App.Services;

public class PlayerService : IMediaPlayerService, IDisposable
{
    private readonly LibVLC _libVlc;
    private bool _isDisposed;

    public MediaPlayer MediaPlayer { get; }

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

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

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