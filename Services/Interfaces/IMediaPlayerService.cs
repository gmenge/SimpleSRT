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
    void Stop();
}