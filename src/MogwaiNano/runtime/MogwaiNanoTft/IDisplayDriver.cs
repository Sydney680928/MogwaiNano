using System;

namespace MogwaiNanoTft
{
    /// <summary>
    /// Minimal contract of a color display chip.
    /// Pixels are sent as RGB565, high byte first.
    /// </summary>
    public interface IDisplayDriver : IDisposable
    {
        int NativeWidth { get; }          // width in rotation 0
        int NativeHeight { get; }         // height in rotation 0
        int MaxTransferSize { get; }      // max size of one transfer, in bytes

        void Initialize();
        void SetRotation(int rotation);   // 0..3
        void SetWindow(int x0, int y0, int x1, int y1);   // logical coordinates
        void WritePixels(byte[] buffer, int count);       // sends the first count bytes
        void SetBacklight(bool on);
    }
}