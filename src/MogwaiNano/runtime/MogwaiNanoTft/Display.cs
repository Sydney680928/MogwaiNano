using System;

namespace MogwaiNanoTft
{
    /// <summary>
    /// RGB565 canvas in RAM, independent of the display chip (which sits behind an IDisplayDriver).
    /// All drawing goes to the buffer; Flush() sends only the modified rectangle to the screen.
    /// </summary>
    public class Display : IDisposable
    {
        /// <summary>Background value meaning "no background" for text.</summary>
        public const int Transparent = -1;

        private readonly IDisplayDriver _driver;

        private byte[] _fb;
        private byte[] _tx;
        private readonly byte[] _glyph = new byte[8];
        private byte[] _rowTmp;     // temporary row, only allocated by horizontal scrolling

        private int _width;
        private int _height;
        private int _rotation;

        private bool _dirty;
        private int _dx0, _dy0, _dx1, _dy1;

        /// <summary>When true, text drawn with a size of 2 or more is anti-aliased (smoothed edges).</summary>
        public bool Smooth;

        /// <summary>When true, every drawing call immediately sends its area to the screen.</summary>
        public bool AutoFlush;

        public int Width { get { return _width; } }
        public int Height { get { return _height; } }
        public int Rotation { get { return _rotation; } }
        public int NativeWidth { get { return _driver.NativeWidth; } }
        public int NativeHeight { get { return _driver.NativeHeight; } }

        public Display(IDisplayDriver driver)
        {
            _driver = driver;
            _width = driver.NativeWidth;
            _height = driver.NativeHeight;
        }

        // ------------------------------------------------------------------
        // Initialization
        // ------------------------------------------------------------------

        public void Initialize()
        {
            try
            {
                _fb = new byte[_driver.NativeWidth * _driver.NativeHeight * 2];
                _tx = new byte[_driver.MaxTransferSize];
            }
            catch (OutOfMemoryException)
            {
                throw new InvalidOperationException("not enough memory for the screen buffer");
            }

            _driver.Initialize();
            SetRotation(0);
            Flush();                        // black screen before the backlight comes on
            _driver.SetBacklight(true);
        }

        public void Dispose()
        {
            _driver.Dispose();
        }

        public void SetBacklight(bool on)
        {
            _driver.SetBacklight(on);
        }

        /// <summary>0 = portrait, 1 = landscape, 2 = inverted portrait, 3 = inverted landscape. Clears the buffer.</summary>
        public void SetRotation(int rotation)
        {
            rotation = ((rotation % 4) + 4) % 4;
            _driver.SetRotation(rotation);
            _rotation = rotation;

            if ((rotation & 1) == 0)
            {
                _width = _driver.NativeWidth;
                _height = _driver.NativeHeight;
            }
            else
            {
                _width = _driver.NativeHeight;
                _height = _driver.NativeWidth;
            }

            FillRectRaw(0, 0, _width, _height, 0);
            _dx0 = 0; _dy0 = 0; _dx1 = _width - 1; _dy1 = _height - 1;
            _dirty = true;
        }

        public static ushort Color565(int r, int g, int b)
        {
            return (ushort)(((r & 0xF8) << 8) | ((g & 0xFC) << 3) | ((b & 0xF8) >> 3));
        }

        // ------------------------------------------------------------------
        // Sending the buffer to the screen
        // ------------------------------------------------------------------

        /// <summary>Sends the area modified since the last Flush to the screen.</summary>
        public void Flush()
        {
            if (!_dirty) return;

            int x0 = _dx0, y0 = _dy0, x1 = _dx1, y1 = _dy1;
            _dirty = false;

            _driver.SetWindow(x0, y0, x1, y1);

            int chunk = _tx.Length;
            int rowBytes = (x1 - x0 + 1) * 2;
            int pos = 0;

            for (int y = y0; y <= y1; y++)
            {
                int src = (y * _width + x0) * 2;
                int remaining = rowBytes;
                while (remaining > 0)
                {
                    int n = chunk - pos;
                    if (n > remaining) n = remaining;
                    Array.Copy(_fb, src, _tx, pos, n);
                    pos += n;
                    src += n;
                    remaining -= n;
                    if (pos == chunk)
                    {
                        _driver.WritePixels(_tx, chunk);
                        pos = 0;
                    }
                }
            }

            if (pos > 0)
            {
                _driver.WritePixels(_tx, pos);
            }
        }

        /// <summary>Sends the whole buffer to the screen.</summary>
        public void FlushAll()
        {
            _dx0 = 0; _dy0 = 0; _dx1 = _width - 1; _dy1 = _height - 1;
            _dirty = true;
            Flush();
        }

        // ------------------------------------------------------------------
        // Dirty area tracking
        // ------------------------------------------------------------------

        private void Mark(int x0, int y0, int x1, int y1)
        {
            if (x0 < 0) x0 = 0;
            if (y0 < 0) y0 = 0;
            if (x1 >= _width) x1 = _width - 1;
            if (y1 >= _height) y1 = _height - 1;
            if (x0 > x1 || y0 > y1) return;

            if (!_dirty)
            {
                _dx0 = x0; _dy0 = y0; _dx1 = x1; _dy1 = y1;
                _dirty = true;
            }
            else
            {
                if (x0 < _dx0) _dx0 = x0;
                if (y0 < _dy0) _dy0 = y0;
                if (x1 > _dx1) _dx1 = x1;
                if (y1 > _dy1) _dy1 = y1;
            }
        }

        private void Done(int x0, int y0, int x1, int y1)
        {
            Mark(x0, y0, x1, y1);
            if (AutoFlush) Flush();
        }

        // ------------------------------------------------------------------
        // Internal primitives (no dirty marking, no flush)
        // ------------------------------------------------------------------

        private void FillPattern(int start, int length, byte hi, byte lo)
        {
            int init = length < 64 ? length : 64;
            for (int i = 0; i < init; i += 2)
            {
                _fb[start + i] = hi;
                _fb[start + i + 1] = lo;
            }
            int filled = init;
            while (filled < length)
            {
                int n = filled;
                if (n > length - filled) n = length - filled;
                Array.Copy(_fb, start, _fb, start + filled, n);
                filled += n;
            }
        }

        private void FillRectRaw(int x, int y, int w, int h, ushort color)
        {
            if (w <= 0 || h <= 0) return;
            int x1 = x + w - 1;
            int y1 = y + h - 1;
            if (x < 0) x = 0;
            if (y < 0) y = 0;
            if (x1 >= _width) x1 = _width - 1;
            if (y1 >= _height) y1 = _height - 1;
            if (x > x1 || y > y1) return;

            int cw = x1 - x + 1;
            int ch = y1 - y + 1;
            byte hi = (byte)(color >> 8);
            byte lo = (byte)(color & 0xFF);

            if (cw == _width)
            {
                FillPattern(y * _width * 2, cw * ch * 2, hi, lo);
            }
            else
            {
                int start = (y * _width + x) * 2;
                int rowBytes = cw * 2;
                FillPattern(start, rowBytes, hi, lo);
                for (int r = 1; r < ch; r++)
                {
                    Array.Copy(_fb, start, _fb, start + r * _width * 2, rowBytes);
                }
            }
        }

        private void PutPixel(int x, int y, ushort color)
        {
            if (x < 0 || y < 0 || x >= _width || y >= _height) return;
            int i = (y * _width + x) * 2;
            _fb[i] = (byte)(color >> 8);
            _fb[i + 1] = (byte)(color & 0xFF);
        }

        private void LineRaw(int x0, int y0, int x1, int y1, ushort color)
        {
            int adx = x1 > x0 ? x1 - x0 : x0 - x1;
            int ady = y1 > y0 ? y1 - y0 : y0 - y1;

            if (ady == 0)
            {
                FillRectRaw(x0 < x1 ? x0 : x1, y0, adx + 1, 1, color);
                return;
            }
            if (adx == 0)
            {
                FillRectRaw(x0, y0 < y1 ? y0 : y1, 1, ady + 1, color);
                return;
            }

            bool steep = ady > adx;
            int t;
            if (steep)
            {
                t = x0; x0 = y0; y0 = t;
                t = x1; x1 = y1; y1 = t;
            }
            if (x0 > x1)
            {
                t = x0; x0 = x1; x1 = t;
                t = y0; y0 = y1; y1 = t;
            }

            int dx = x1 - x0;
            int dy = y1 > y0 ? y1 - y0 : y0 - y1;
            int err = dx / 2;
            int ystep = y0 < y1 ? 1 : -1;
            int yy = y0;

            for (int xx = x0; xx <= x1; xx++)
            {
                if (steep) PutPixel(yy, xx, color);
                else PutPixel(xx, yy, color);

                err -= dy;
                if (err < 0)
                {
                    yy += ystep;
                    err += dx;
                }
            }
        }

        private void CircleHelperRaw(int x0, int y0, int r, int corners, ushort color)
        {
            int f = 1 - r;
            int ddx = 1;
            int ddy = -2 * r;
            int x = 0;
            int y = r;

            while (x < y)
            {
                if (f >= 0)
                {
                    y--;
                    ddy += 2;
                    f += ddy;
                }
                x++;
                ddx += 2;
                f += ddx;

                if ((corners & 4) != 0)
                {
                    PutPixel(x0 + x, y0 + y, color);
                    PutPixel(x0 + y, y0 + x, color);
                }
                if ((corners & 2) != 0)
                {
                    PutPixel(x0 + x, y0 - y, color);
                    PutPixel(x0 + y, y0 - x, color);
                }
                if ((corners & 8) != 0)
                {
                    PutPixel(x0 - y, y0 + x, color);
                    PutPixel(x0 - x, y0 + y, color);
                }
                if ((corners & 1) != 0)
                {
                    PutPixel(x0 - y, y0 - x, color);
                    PutPixel(x0 - x, y0 - y, color);
                }
            }
        }

        private void FillCircleHelperRaw(int x0, int y0, int r, int corners, int delta, ushort color)
        {
            int f = 1 - r;
            int ddx = 1;
            int ddy = -2 * r;
            int x = 0;
            int y = r;
            int px = x;
            int py = y;

            delta++;

            while (x < y)
            {
                if (f >= 0)
                {
                    y--;
                    ddy += 2;
                    f += ddy;
                }
                x++;
                ddx += 2;
                f += ddx;

                if (x < (y + 1))
                {
                    if ((corners & 1) != 0) FillRectRaw(x0 + x, y0 - y, 1, 2 * y + delta, color);
                    if ((corners & 2) != 0) FillRectRaw(x0 - x, y0 - y, 1, 2 * y + delta, color);
                }
                if (y != py)
                {
                    if ((corners & 1) != 0) FillRectRaw(x0 + py, y0 - px, 1, 2 * px + delta, color);
                    if ((corners & 2) != 0) FillRectRaw(x0 - py, y0 - px, 1, 2 * px + delta, color);
                    py = y;
                }
                px = x;
            }
        }

        // ------------------------------------------------------------------
        // Drawing API
        // ------------------------------------------------------------------

        public void Clear(ushort color)
        {
            FillRectRaw(0, 0, _width, _height, color);
            Done(0, 0, _width - 1, _height - 1);
        }

        public void DrawPixel(int x, int y, ushort color)
        {
            PutPixel(x, y, color);
            Done(x, y, x, y);
        }

        public ushort GetPixel(int x, int y)
        {
            if (x < 0 || y < 0 || x >= _width || y >= _height) return 0;
            int i = (y * _width + x) * 2;
            return (ushort)((_fb[i] << 8) | _fb[i + 1]);
        }

        public void DrawHLine(int x, int y, int w, ushort color)
        {
            FillRectRaw(x, y, w, 1, color);
            Done(x, y, x + w - 1, y);
        }

        public void DrawVLine(int x, int y, int h, ushort color)
        {
            FillRectRaw(x, y, 1, h, color);
            Done(x, y, x, y + h - 1);
        }

        public void DrawLine(int x0, int y0, int x1, int y1, ushort color)
        {
            LineRaw(x0, y0, x1, y1, color);
            Done(x0 < x1 ? x0 : x1, y0 < y1 ? y0 : y1, x0 > x1 ? x0 : x1, y0 > y1 ? y0 : y1);
        }

        public void FillRect(int x, int y, int w, int h, ushort color)
        {
            FillRectRaw(x, y, w, h, color);
            Done(x, y, x + w - 1, y + h - 1);
        }

        public void DrawRect(int x, int y, int w, int h, ushort color)
        {
            if (w <= 0 || h <= 0) return;
            FillRectRaw(x, y, w, 1, color);
            FillRectRaw(x, y + h - 1, w, 1, color);
            FillRectRaw(x, y, 1, h, color);
            FillRectRaw(x + w - 1, y, 1, h, color);
            Done(x, y, x + w - 1, y + h - 1);
        }

        public void DrawRoundRect(int x, int y, int w, int h, int r, ushort color)
        {
            if (w <= 0 || h <= 0) return;
            if (r > w / 2) r = w / 2;
            if (r > h / 2) r = h / 2;

            FillRectRaw(x + r, y, w - 2 * r, 1, color);
            FillRectRaw(x + r, y + h - 1, w - 2 * r, 1, color);
            FillRectRaw(x, y + r, 1, h - 2 * r, color);
            FillRectRaw(x + w - 1, y + r, 1, h - 2 * r, color);

            CircleHelperRaw(x + r, y + r, r, 1, color);
            CircleHelperRaw(x + w - r - 1, y + r, r, 2, color);
            CircleHelperRaw(x + w - r - 1, y + h - r - 1, r, 4, color);
            CircleHelperRaw(x + r, y + h - r - 1, r, 8, color);

            Done(x, y, x + w - 1, y + h - 1);
        }

        public void FillRoundRect(int x, int y, int w, int h, int r, ushort color)
        {
            if (w <= 0 || h <= 0) return;
            if (r > w / 2) r = w / 2;
            if (r > h / 2) r = h / 2;

            FillRectRaw(x + r, y, w - 2 * r, h, color);
            FillCircleHelperRaw(x + w - r - 1, y + r, r, 1, h - 2 * r - 1, color);
            FillCircleHelperRaw(x + r, y + r, r, 2, h - 2 * r - 1, color);

            Done(x, y, x + w - 1, y + h - 1);
        }

        public void DrawCircle(int x0, int y0, int r, ushort color)
        {
            if (r < 0) return;
            PutPixel(x0, y0 + r, color);
            PutPixel(x0, y0 - r, color);
            PutPixel(x0 + r, y0, color);
            PutPixel(x0 - r, y0, color);
            CircleHelperRaw(x0, y0, r, 15, color);
            Done(x0 - r, y0 - r, x0 + r, y0 + r);
        }

        public void FillCircle(int x0, int y0, int r, ushort color)
        {
            if (r < 0) return;
            FillRectRaw(x0, y0 - r, 1, 2 * r + 1, color);
            FillCircleHelperRaw(x0, y0, r, 3, 0, color);
            Done(x0 - r, y0 - r, x0 + r, y0 + r);
        }

        public void DrawTriangle(int x0, int y0, int x1, int y1, int x2, int y2, ushort color)
        {
            LineRaw(x0, y0, x1, y1, color);
            LineRaw(x1, y1, x2, y2, color);
            LineRaw(x2, y2, x0, y0, color);

            int minX = x0 < x1 ? x0 : x1; if (x2 < minX) minX = x2;
            int minY = y0 < y1 ? y0 : y1; if (y2 < minY) minY = y2;
            int maxX = x0 > x1 ? x0 : x1; if (x2 > maxX) maxX = x2;
            int maxY = y0 > y1 ? y0 : y1; if (y2 > maxY) maxY = y2;
            Done(minX, minY, maxX, maxY);
        }

        /// <summary>Copies an RGB565 image (2 bytes per pixel, high byte first).</summary>
        public void DrawBitmap(int x, int y, int w, int h, byte[] rgb565)
        {
            if (rgb565 == null || w <= 0 || h <= 0 || rgb565.Length < w * h * 2)
            {
                throw new ArgumentException("bitmap data is too small for the given size");
            }

            for (int r = 0; r < h; r++)
            {
                int dy = y + r;
                if (dy < 0 || dy >= _height) continue;

                int sx = 0;
                int dx = x;
                int cw = w;
                if (dx < 0)
                {
                    sx = -dx;
                    cw += dx;
                    dx = 0;
                }
                if (dx + cw > _width) cw = _width - dx;
                if (cw <= 0) continue;

                Array.Copy(rgb565, (r * w + sx) * 2, _fb, (dy * _width + dx) * 2, cw * 2);
            }
            Done(x, y, x + w - 1, y + h - 1);
        }

        // ------------------------------------------------------------------
        // Scrolling
        // ------------------------------------------------------------------

        /// <summary>
        /// Scrolls the content of a rectangle inside the canvas. dx and dy are the displacement of the content
        /// (dy = -8: the content moves up by 8 pixels, like a text console). What scrolls out of the rectangle is lost,
        /// the band that is freed is filled with color. The whole rectangle is marked as modified.
        /// </summary>
        public void Scroll(int x, int y, int w, int h, int dx, int dy, ushort color)
        {
            if (w <= 0 || h <= 0) return;

            int x1 = x + w - 1;
            int y1 = y + h - 1;
            if (x < 0) x = 0;
            if (y < 0) y = 0;
            if (x1 >= _width) x1 = _width - 1;
            if (y1 >= _height) y1 = _height - 1;
            if (x > x1 || y > y1) return;

            if (dx == 0 && dy == 0) return;

            int cw = x1 - x + 1;
            int ch = y1 - y + 1;
            int adx = dx < 0 ? -dx : dx;
            int ady = dy < 0 ? -dy : dy;

            if (adx >= cw || ady >= ch)
            {
                // everything scrolls out
                FillRectRaw(x, y, cw, ch, color);
                Done(x, y, x1, y1);
                return;
            }

            int copyW = cw - adx;
            int copyH = ch - ady;
            int rowBytes = copyW * 2;
            int srcX = dx < 0 ? x + adx : x;
            int dstX = dx > 0 ? x + adx : x;
            int srcY = dy < 0 ? y + ady : y;
            int dstY = dy > 0 ? y + ady : y;
            int stride = _width * 2;

            // Rows are copied one by one so that no single copy overlaps itself,
            // except when the content only moves sideways (dy == 0): a temporary row is used then.
            if (dy == 0 && _rowTmp == null)
            {
                int max = _driver.NativeWidth > _driver.NativeHeight ? _driver.NativeWidth : _driver.NativeHeight;
                _rowTmp = new byte[max * 2];
            }

            if (dy <= 0)
            {
                // content moves up (or stays on the same rows): the destination is before the source, go downwards
                for (int r = 0; r < copyH; r++)
                {
                    CopyScrollRow((srcY + r) * stride + srcX * 2, (dstY + r) * stride + dstX * 2, rowBytes, dy == 0);
                }
            }
            else
            {
                // content moves down: the destination is after the source, go upwards
                for (int r = copyH - 1; r >= 0; r--)
                {
                    CopyScrollRow((srcY + r) * stride + srcX * 2, (dstY + r) * stride + dstX * 2, rowBytes, false);
                }
            }

            // Band(s) freed by the move
            if (dy < 0) FillRectRaw(x, y1 - ady + 1, cw, ady, color);
            else if (dy > 0) FillRectRaw(x, y, cw, ady, color);

            if (dx < 0) FillRectRaw(x1 - adx + 1, y, adx, ch, color);
            else if (dx > 0) FillRectRaw(x, y, adx, ch, color);

            Done(x, y, x1, y1);
        }

        private void CopyScrollRow(int src, int dst, int length, bool sameRow)
        {
            if (sameRow)
            {
                Array.Copy(_fb, src, _rowTmp, 0, length);
                Array.Copy(_rowTmp, 0, _fb, dst, length);
            }
            else
            {
                Array.Copy(_fb, src, _fb, dst, length);
            }
        }

        // ------------------------------------------------------------------
        // Text (8x8 font, scale factor 1, 2, 3...)
        // ------------------------------------------------------------------

        // Glyph pixel (0 or 1), 0 outside the 8x8 cell.
        private int GlyphBit(int col, int row)
        {
            if (col < 0 || col > 7 || row < 0 || row > 7) return 0;
            return (_glyph[row] >> col) & 1;
        }

        // Blends fg over the pixel already in the canvas. alpha: 0 (nothing) to 256 (opaque).
        private void BlendPixel(int x, int y, ushort fg, int alpha)
        {
            if (alpha <= 0) return;
            if (x < 0 || y < 0 || x >= _width || y >= _height) return;

            int i = (y * _width + x) * 2;

            if (alpha >= 256)
            {
                _fb[i] = (byte)(fg >> 8);
                _fb[i + 1] = (byte)(fg & 0xFF);
                return;
            }

            int d = (_fb[i] << 8) | _fb[i + 1];
            int inv = 256 - alpha;

            int r = ((((fg >> 11) & 0x1F) * alpha) + (((d >> 11) & 0x1F) * inv)) >> 8;
            int g = ((((fg >> 5) & 0x3F) * alpha) + (((d >> 5) & 0x3F) * inv)) >> 8;
            int b = (((fg & 0x1F) * alpha) + ((d & 0x1F) * inv)) >> 8;

            int v = (r << 11) | (g << 5) | b;
            _fb[i] = (byte)(v >> 8);
            _fb[i + 1] = (byte)(v & 0xFF);
        }

        // Anti-aliased glyph. Straight edges and corners stay sharp: only the stair steps of diagonal edges
        // are smoothed. For each pixel, a corner (quadrant) is cut or filled along its diagonal when
        // the two neighbours on that side are equal to each other and different from the pixel, while
        // the two neighbours on the other side are equal to the pixel (the usual "Scale2x" rule).
        private void DrawCharSmooth(int x, int y, ushort fg, int scale)
        {
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    int p = GlyphBit(col, row);
                    int l = GlyphBit(col - 1, row);
                    int r = GlyphBit(col + 1, row);
                    int u = GlyphBit(col, row - 1);
                    int d = GlyphBit(col, row + 1);

                    bool ul = l == u && l != p && r == p && d == p;
                    bool ur = r == u && r != p && l == p && d == p;
                    bool dl = l == d && l != p && r == p && u == p;
                    bool dr = r == d && r != p && l == p && u == p;

                    if (p == 0 && !ul && !ur && !dl && !dr) continue;

                    int bx = x + col * scale;
                    int by = y + row * scale;

                    if (!ul && !ur && !dl && !dr)
                    {
                        FillRectRaw(bx, by, scale, scale, fg);
                        continue;
                    }

                    for (int oy = 0; oy < scale; oy++)
                    {
                        int uy = 2 * oy + 1 - scale;     // distance to the center of the block, in 1/(2*scale)
                        bool top = uy < 0;
                        int ay = top ? -uy : uy;

                        for (int ox = 0; ox < scale; ox++)
                        {
                            int ux = 2 * ox + 1 - scale;
                            bool left = ux < 0;
                            int ax = left ? -ux : ux;

                            bool flag = top ? (left ? ul : ur) : (left ? dl : dr);
                            int alpha = p != 0 ? 256 : 0;

                            if (flag)
                            {
                                // coverage of the corner triangle beyond the line qx + qy = 0.5 (ramp about one pixel wide)
                                int a = 128 + (ax + ay - scale) * 90;
                                if (a < 0) a = 0;
                                if (a > 256) a = 256;

                                alpha = p != 0 ? 256 - a : a;
                            }

                            BlendPixel(bx + ox, by + oy, fg, alpha);
                        }
                    }
                }
            }
        }

        private void DrawCharRaw(int x, int y, char c, ushort fg, int bg, int scale)
        {
            Font8x8.GetGlyph(c, _glyph);

            if (bg >= 0)
            {
                FillRectRaw(x, y, 8 * scale, 8 * scale, (ushort)bg);
            }

            if (Smooth && scale >= 2)
            {
                DrawCharSmooth(x, y, fg, scale);
                return;
            }

            for (int row = 0; row < 8; row++)
            {
                byte bits = _glyph[row];
                if (bits == 0) continue;

                for (int col = 0; col < 8; col++)
                {
                    if ((bits & (1 << col)) != 0)
                    {
                        if (scale == 1) PutPixel(x + col, y + row, fg);
                        else FillRectRaw(x + col * scale, y + row * scale, scale, scale, fg);
                    }
                }
            }
        }

        /// <summary>Draws a text. bg = Transparent (-1) or an RGB565 color. '\n' starts a new line.</summary>
        public void DrawString(int x, int y, string text, ushort fg, int bg, int scale)
        {
            if (text == null) return;
            if (scale < 1) scale = 1;

            int cx = x;
            int cy = y;
            int maxX = x;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\n')
                {
                    cx = x;
                    cy += 8 * scale;
                    continue;
                }
                if (c == '\r') continue;

                DrawCharRaw(cx, cy, c, fg, bg, scale);
                cx += 8 * scale;
                if (cx > maxX) maxX = cx;
            }

            Done(x, y, maxX - 1, cy + 8 * scale - 1);
        }

        /// <summary>Width in pixels of the longest line of the text.</summary>
        public int MeasureString(string text, int scale)
        {
            if (text == null) return 0;
            if (scale < 1) scale = 1;

            int best = 0;
            int cur = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\n')
                {
                    cur = 0;
                }
                else if (c != '\r')
                {
                    cur++;
                    if (cur > best) best = cur;
                }
            }
            return best * 8 * scale;
        }
    }
}