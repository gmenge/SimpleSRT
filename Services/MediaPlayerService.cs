using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LibVLCSharp.Shared;
using SimpleSRT.App.Services.Interfaces;

namespace SimpleSRT.App.Services;

public class MediaPlayerService : IMediaPlayerService
{
    private readonly LibVLC _libVLC;
    public MediaPlayer MediaPlayer { get; }

    private IntPtr _frameBuffer = IntPtr.Zero;
    private int _width = 1920;
    private int _height = 1080;
    private int _stride;

    private IDeckLinkOutput? _deckLinkOutput;
    private bool _isDeckLinkEnabled = false;
    private long _frameCounter = 0;

    public MediaPlayerService(LibVLC libVLC)
    {
        _libVLC = libVLC;
        MediaPlayer = new MediaPlayer(_libVLC);
    }

    public int Volume
    {
        get => MediaPlayer.Volume;
        set => MediaPlayer.Volume = value;
    }

    public bool IsMuted
    {
        get => MediaPlayer.Mute;
        set => MediaPlayer.Mute = value;
    }

    public IEnumerable<(string Id, string Description)> GetAudioOutputs()
    {
        var devices = new List<(string Id, string Description)>();
        foreach (var device in MediaPlayer.AudioOutputDeviceEnum)
        {
            devices.Add((device.DeviceIdentifier, device.Description));
        }
        return devices;
    }

    public void SetAudioOutput(string deviceId)
    {
        MediaPlayer.SetOutputDevice(deviceId);
    }

    public void Play(string url, int networkCachingMs)
    {
        var media = new Media(_libVLC, url, FromType.FromLocation);
        media.AddOption($":network-caching={networkCachingMs}");
        MediaPlayer.Play(media);
    }

    public void Stop()
    {
        MediaPlayer.Stop();
    }

    #region DeckLink Implementation

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
                devices.Add(displayName);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erro ao listar DeckLink: {ex.Message}");
        }
        return devices;
    }

    public void EnableDeckLinkOutput(int deviceIndex = 0, int width = 1920, int height = 1080, double fps = 59.94)
    {
        _width = width;
        _height = height;
        _stride = _width * 2;

        if (_frameBuffer != IntPtr.Zero) Marshal.FreeHGlobal(_frameBuffer);
        _frameBuffer = Marshal.AllocHGlobal(_stride * _height);

        InitDeckLinkHardware(deviceIndex);

        MediaPlayer.SetVideoFormat("UYVY", (uint)_width, (uint)_height, (uint)_stride);
        MediaPlayer.SetVideoCallbacks(LockVideoCallback, UnlockVideoCallback, DisplayVideoCallback);

        _isDeckLinkEnabled = true;
    }

    public void DisableDeckLinkOutput()
    {
        _isDeckLinkEnabled = false;

        if (_deckLinkOutput != null)
        {
            _deckLinkOutput.StopScheduledPlayback(0, out _, 1000);
            _deckLinkOutput.DisableVideoOutput();
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
            throw new InvalidOperationException($"Dispositivo DeckLink no índice {deviceIndex} não encontrado.");

        _deckLinkOutput = (IDeckLinkOutput)selectedDevice;
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
}