using System;
using System.Runtime.InteropServices;

namespace SimpleSRT.App.Services;

public enum BMDDisplayMode : uint
{
    bmdModeHD1080p5994 = 0x48703539, // 'Hp59'
    bmdModeHD1080p50   = 0x48703530, // 'Hp50'
    bmdModeHD1080p6000 = 0x48703630  // 'Hp60'
}

public enum BMDPixelFormat : uint
{
    bmdFormat8BitYUV = 0x32767579 // '2vuy' (UYVY)
}

public enum BMDVideoOutputFlags : uint
{
    bmdVideoOutputFlagDefault = 0
}

[ComImport, Guid("C418F2D0-8A01-423A-9A59-828B02C65B0A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IDeckLink
{
    void GetModelName([MarshalAs(UnmanagedType.BStr)] out string modelName);
    void GetDisplayName([MarshalAs(UnmanagedType.BStr)] out string displayName);
}

[ComImport, Guid("2A57462B-A4A3-420C-31E6-92A79C897485"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IDeckLinkIterator
{
    [PreserveSig]
    int Next(out IDeckLink deckLinkInstance);
}

[ComImport, Guid("CC716A00-F867-40AE-9111-92A151B93D2B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IDeckLinkOutput
{
    void EnableVideoOutput(BMDDisplayMode displayMode, BMDVideoOutputFlags flags);
    void DisableVideoOutput();
    void StartScheduledPlayback(long playbackStartTime, long timeScale, double playbackSpeed);
    void StopScheduledPlayback(long stopTimeToQueue, out long actualStopTime, long timeScale);
    void CreateVideoFrame(int width, int height, int rowBytes, BMDPixelFormat pixelFormat, uint flags, out IDeckLinkVideoFrame outFrame);
    void ScheduleVideoFrame(IDeckLinkVideoFrame videoFrame, long scheduledDeliveryTime, long videoFrameDuration, long timeScale);
}

[ComImport, Guid("3F713268-FA29-4B14-8341-2E48F2487840"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IDeckLinkVideoFrame
{
    int GetWidth();
    int GetHeight();
    int GetRowBytes();
    BMDPixelFormat GetPixelFormat();
    uint GetFlags();
    void GetBytes(out IntPtr buffer);
}

[ComImport, Guid("D864517A-EDD5-466D-867D-985115551078")]
public class CDeckLinkDiscovery
{
}