using System;
using System.Collections.Generic;
using LibVLCSharp.Shared;

namespace SimpleSRT.App.Services.Interfaces;

public interface IMediaPlayerService
{
    MediaPlayer MediaPlayer { get; }
    int Volume { get; set; }
    bool IsMuted { get; set; }

    IEnumerable<(string Id, string Description)> GetAudioOutputs();
    void SetAudioOutput(string deviceId);
    void Play(string url, int networkCachingMs);
    void Stop(); // Corrigido: adicionado 'void'

    IEnumerable<string> GetDeckLinkDevices();
    void EnableDeckLinkOutput(int deviceIndex = 0, int width = 1920, int height = 1080, double fps = 59.94);
    void DisableDeckLinkOutput();
}