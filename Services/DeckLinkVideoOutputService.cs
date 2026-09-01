using System;
using System.Runtime.InteropServices;
using LibVLCSharp.Shared;

namespace SimpleSRT.App.Services;

public class DeckLinkVideoOutputService
{
    private readonly MediaPlayer _mediaPlayer;
    private int _width = 1920;
    private int _height = 1080;

    public DeckLinkVideoOutputService(MediaPlayer mediaPlayer)
    {
        _mediaPlayer = mediaPlayer;
    }

    /// <summary>
    /// Configura os Callbacks de Vídeo para extração de frames descompactados
    /// </summary>
    public void EnableMemoryOutput(int width = 1920, int height = 1080)
    {
        _width = width;
        _height = height;

        // O formato 'UYVY' (YUV 4:2:2 8-bit) é o padrão nativo aceito pelas placas DeckLink
        _mediaPlayer.SetVideoFormat("UYVY", (uint)_width, (uint)_height, (uint)(_width * 2));

        // Registra os 3 callbacks requeridos pelo LibVLC:
        // 1. Lock: Chamado para preparar a memória do próximo frame
        // 2. Unlock: Chamado quando o LibVLC termina de desenhar no buffer
        // 3. Display: Chamado no momento em que o frame deve ser exibido
        _mediaPlayer.SetVideoCallbacks(LockCallback, UnlockCallback, DisplayCallback);
    }

    private IntPtr LockCallback(IntPtr opaque, IntPtr planes)
    {
        // Aloca/retorna o ponteiro do buffer onde o LibVLC deve escrever os pixels
        // Por exemplo: Marshal.WriteIntPtr(planes, ptrBuffer);
        return IntPtr.Zero;
    }

    private void UnlockCallback(IntPtr opaque, IntPtr picture, IntPtr planes)
    {
        // Frame pronto em memória RAM!
    }

    private void DisplayCallback(IntPtr opaque, IntPtr picture)
    {
        // Aqui o frame descompactado é enviado diretamente para a Blackmagic (Passo 3)
    }
}