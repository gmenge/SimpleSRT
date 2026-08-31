using System;

namespace SimpleSRT.App.Services.Interfaces
{
    public interface IMediaPlayerService : IDisposable
    {
        // Evento disparado a cada novo frame YUV/RGB decodificado da rede
        event Action<byte[], int, int>? OnFrameDecoded;

        bool IsPlaying { get; }
        void Initialize();
        void Play(string streamUrl, int networkCachingMs);
        void Stop();
    }
}