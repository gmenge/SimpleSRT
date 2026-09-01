using System;
using System.Collections.Generic;

namespace SimpleSRT.App.Services.Interfaces;

public interface IMediaPlayerService : IDisposable
{
    event Action<byte[], int, int>? OnFrameDecoded;

    void Play(string url, int networkCachingMs);
    void Stop();

    int Volume { get; set; }
    bool IsMuted { get; set; }

    IEnumerable<(string Id, string Description)> GetAudioOutputs();
    void SetAudioOutput(string deviceId);
}