using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LibVLCSharp.Shared;
using SimpleSRT.App.Services.Interfaces;

namespace SimpleSRT.App.Services;

public class VLCPlayerService : IMediaPlayerService
{
    private readonly LibVLC _libVlc;
    private readonly MediaPlayer _mediaPlayer;
    private int _videoWidth;
    private int _videoHeight;

    public event Action<byte[], int, int>? OnFrameDecoded;

    public VLCPlayerService()
    {
        // Qualificação explícita do namespace para evitar conflito com SimpleSRT.App.Core
        LibVLCSharp.Shared.Core.Initialize();

        var options = new[]
        {
            "--no-osd",
            "--no-snapshot-preview",
            "--quiet"
        };

        _libVlc = new LibVLC(options);
        _mediaPlayer = new MediaPlayer(_libVlc);

        ConfigureVideoCallbacks();
    }

    public int Volume
    {
        get => _mediaPlayer.Volume;
        set => _mediaPlayer.Volume = Math.Clamp(value, 0, 100);
    }

    public bool IsMuted
    {
        get => _mediaPlayer.Mute;
        set => _mediaPlayer.Mute = value;
    }

    public IEnumerable<(string Id, string Description)> GetAudioOutputs()
    {
        var devices = new List<(string Id, string Description)>();
        var outputDevices = _mediaPlayer.AudioOutputDeviceEnum;

        foreach (var device in outputDevices)
        {
            if (!string.IsNullOrEmpty(device.DeviceIdentifier))
            {
                devices.Add((device.DeviceIdentifier, device.Description ?? device.DeviceIdentifier));
            }
        }

        return devices;
    }

    public void SetAudioOutput(string deviceId)
    {
        if (!string.IsNullOrEmpty(deviceId))
        {
            _mediaPlayer.SetOutputDevice(deviceId);
        }
    }

    public void Play(string url, int networkCachingMs)
    {
        Stop();

        var mediaOptions = new[]
        {
            $":network-caching={networkCachingMs}",
            ":clock-jitter=0",
            ":clock-synchro=0"
        };

        using var media = new Media(_libVlc, url, FromType.FromLocation, mediaOptions);
        _mediaPlayer.Play(media);
    }

    public void Stop()
    {
        if (_mediaPlayer.IsPlaying)
        {
            _mediaPlayer.Stop();
        }
    }

    private void ConfigureVideoCallbacks()
    {
        _mediaPlayer.SetVideoFormatCallbacks(VideoFormatCallback, null);
        _mediaPlayer.SetVideoCallbacks(LockCallback, null, DisplayCallback);
    }

    private uint VideoFormatCallback(ref IntPtr opaque, IntPtr chroma, ref uint width, ref uint height, ref uint pitches, ref uint lines)
    {
        _videoWidth = (int)width;
        _videoHeight = (int)height;

        byte[] chromaBytes = System.Text.Encoding.ASCII.GetBytes("RV32");
        Marshal.Copy(chromaBytes, 0, chroma, 4);

        pitches = (uint)(_videoWidth * 4);
        lines = (uint)_videoHeight;

        return 1;
    }

    private IntPtr LockCallback(IntPtr opaque, IntPtr planes)
    {
        int bufferSize = _videoWidth * _videoHeight * 4;
        IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
        Marshal.WriteIntPtr(planes, buffer);
        return buffer;
    }

    private void DisplayCallback(IntPtr opaque, IntPtr picture)
    {
        if (picture == IntPtr.Zero || _videoWidth <= 0 || _videoHeight <= 0)
            return;

        int bufferSize = _videoWidth * _videoHeight * 4;
        byte[] frameData = new byte[bufferSize];

        Marshal.Copy(picture, frameData, 0, bufferSize);
        Marshal.FreeHGlobal(picture);

        OnFrameDecoded?.Invoke(frameData, _videoWidth, _videoHeight);
    }

    public void Dispose()
    {
        Stop();
        _mediaPlayer.Dispose();
        _libVlc.Dispose();
        GC.SuppressFinalize(this);
    }
}