using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FFmpeg.AutoGen;
using SimpleSRT.App.Services.Interfaces;

namespace SimpleSRT.App.Services
{
    public unsafe class VLCPlayerService : IMediaPlayerService
    {
        public event Action<byte[], int, int>? OnFrameDecoded;
        public bool IsPlaying { get; private set; }

        private CancellationTokenSource? _cts;

        public void Initialize()
{
    // 1. Define onde o C# deve procurar as DLLs nativas
    ffmpeg.RootPath = AppDomain.CurrentDomain.BaseDirectory;

    // 2. Garante que os ponteiros de funções C nativas sejam carregados na memória
    DynamicallyLoadedBindings.Initialize();

    // 3. Inicializa os protocolos de rede (necessário para abrir URLs SRT/UDP/HTTP)
    ffmpeg.avformat_network_init();
}

        public void Play(string streamUrl, int networkCachingMs)
        {
            Stop();
            IsPlaying = true;
            _cts = new CancellationTokenSource();

            Task.Run(() => DecodeLoop(streamUrl, _cts.Token));
        }

        private void DecodeLoop(string streamUrl, CancellationToken token)
        {
            AVFormatContext* pFormatContext = ffmpeg.avformat_alloc_context();

            if (ffmpeg.avformat_open_input(&pFormatContext, streamUrl, null, null) < 0)
            {
                IsPlaying = false;
                return;
            }

            if (ffmpeg.avformat_find_stream_info(pFormatContext, null) < 0) return;

            int videoStream = -1;
            for (int i = 0; i < pFormatContext->nb_streams; i++)
            {
                if (pFormatContext->streams[i]->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
                {
                    videoStream = i;
                    break;
                }
            }

            if (videoStream == -1) return;

            AVCodecParameters* pCodecParams = pFormatContext->streams[videoStream]->codecpar;
            AVCodec* pCodec = ffmpeg.avcodec_find_decoder(pCodecParams->codec_id);
            AVCodecContext* pCodecContext = ffmpeg.avcodec_alloc_context3(pCodec);
            ffmpeg.avcodec_parameters_to_context(pCodecContext, pCodecParams);
            ffmpeg.avcodec_open2(pCodecContext, pCodec, null);

            AVPacket* pPacket = ffmpeg.av_packet_alloc();
            AVFrame* pFrame = ffmpeg.av_frame_alloc();
            AVFrame* pFrameBgra = ffmpeg.av_frame_alloc();

            SwsContext* swsContext = null;
            byte[]? managedBuffer = null;

            while (!token.IsCancellationRequested && ffmpeg.av_read_frame(pFormatContext, pPacket) >= 0)
            {
                if (pPacket->stream_index == videoStream)
                {
                    if (ffmpeg.avcodec_send_packet(pCodecContext, pPacket) == 0)
                    {
                        while (ffmpeg.avcodec_receive_frame(pCodecContext, pFrame) == 0)
                        {
                            int width = pCodecContext->width;
                            int height = pCodecContext->height;

                            if (swsContext == null)
                            {
                                // Fix 1: Constante bilinear inteira para SWS
                                swsContext = ffmpeg.sws_getContext(
                                    width, height, pCodecContext->pix_fmt,
                                    width, height, AVPixelFormat.AV_PIX_FMT_BGRA,
                                    2, null, null, null); // 2 = SWS_BILINEAR

                                int bufferSize = ffmpeg.av_image_get_buffer_size(AVPixelFormat.AV_PIX_FMT_BGRA, width, height, 1);
                                managedBuffer = new byte[bufferSize];
                            }

                            fixed (byte* pBuffer = managedBuffer)
                            {
                                // Fix 2: Mapeamento direto de ponteiros de buffer sem conversão inválida de array
                                ffmpeg.av_image_fill_arrays(
                                    ref *(byte_ptrArray4*)&pFrameBgra->data,
                                    ref *(int_array4*)&pFrameBgra->linesize,
                                    pBuffer,
                                    AVPixelFormat.AV_PIX_FMT_BGRA,
                                    width,
                                    height,
                                    1);

                                // Realiza a conversão YUV420p -> BGRA
                                ffmpeg.sws_scale(
                                    swsContext,
                                    pFrame->data,
                                    pFrame->linesize,
                                    0,
                                    height,
                                    pFrameBgra->data,
                                    pFrameBgra->linesize);

                                OnFrameDecoded?.Invoke(managedBuffer!, width, height);
                            }
                        }
                    }
                }
                ffmpeg.av_packet_unref(pPacket);
            }

            // Fix 3: Liberação de ponteiros nativos sem uso de instruções 'fixed' desnecessárias
            if (swsContext != null) ffmpeg.sws_freeContext(swsContext);
            ffmpeg.av_frame_free(&pFrameBgra);
            ffmpeg.av_frame_free(&pFrame);
            ffmpeg.av_packet_free(&pPacket);

            AVCodecContext* ctx = pCodecContext;
            ffmpeg.avcodec_free_context(&ctx);

            AVFormatContext* fmtCtx = pFormatContext;
            ffmpeg.avformat_close_input(&fmtCtx);

            IsPlaying = false;
        }

        public void Stop()
        {
            _cts?.Cancel();
            IsPlaying = false;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}