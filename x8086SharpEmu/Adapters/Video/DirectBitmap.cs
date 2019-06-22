using System.Collections.Generic;
using System;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Xml.Linq;
using System.Collections;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using x8086SharpEmu;

namespace x8086SharpEmu
{

    // Credit: SaxxonPike (http://stackoverflow.com/users/3117338/saxxonpike)
    // http://stackoverflow.com/questions/24701703/c-sharp-faster-alternatives-to-setpixel-And-getpixel-for-bitmaps-for-windows-f

    public class DirectBitmap : IDisposable
    {

        public Bitmap Bitmap { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[] Bits { get; set; }
        public Size Size { get; set; }

        private GCHandle bitsHandle;
        private int w4;
        private int bufferSize;

        private static ImageConverter imgConverter = new ImageConverter();
        private static Type imgFormat = typeof(byte[]);

        public DirectBitmap(int w, int h)
        {
            this.Width = w;
            this.Height = h;
            this.Size = new Size(w, h);

            w4 = w * 4;
            bufferSize = w4 * h - 1;
            Bits = new byte[bufferSize + 1];

            bitsHandle = GCHandle.Alloc(Bits, GCHandleType.Pinned);
            this.Bitmap = new Bitmap(w, h, w4, PixelFormat.Format32bppPArgb, bitsHandle.AddrOfPinnedObject());
        }

        public DirectBitmap(Bitmap bmp) : this(bmp.Width, bmp.Height)
        {

            BitmapData sourceData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, bmp.PixelFormat);
            IntPtr sourcePointer = sourceData.Scan0;
            int sourceStride = sourceData.Stride;
            int srcBytesPerPixel = sourceStride / bmp.Width;
            int srcOffset = 0;

            int a = 0;
            double pa = 0;

            for (int y = 0; y <= bmp.Height - 1; y++)
            {
                for (int x = 0; x <= bmp.Width - 1; x++)
                {
                    srcOffset = x * srcBytesPerPixel + y * sourceStride;

                    a = System.Convert.ToInt32(srcBytesPerPixel == 4 ? (Marshal.ReadByte(sourcePointer, srcOffset + 3)) : 255);
                    pa = (double)a / 255;
                    set_Pixel(x, y, Color.FromArgb(a, System.Convert.ToInt32(Marshal.ReadByte(sourcePointer, srcOffset + 2) * pa), System.Convert.ToInt32(Marshal.ReadByte(sourcePointer, srcOffset + 1) * pa), System.Convert.ToInt32(Marshal.ReadByte(sourcePointer, srcOffset + 0) * pa)));
                }
            }

            bmp.UnlockBits(sourceData);
        }

        public static implicit operator DirectBitmap(Bitmap bmp)
        {
            if (ReferenceEquals(bmp, null))
            {
                return null;
            }

            DirectBitmap dbmp = new DirectBitmap(bmp.Width, bmp.Height);
            using (Graphics g = Graphics.FromImage(dbmp.Bitmap))
            {
                g.DrawImageUnscaled(bmp, Point.Empty);
            }


            return dbmp;
        }

        public static explicit operator Bitmap(DirectBitmap dbmp)
        {
            if (ReferenceEquals(dbmp, null))
            {
                return null;
            }
            return dbmp.Bitmap;
        }

        public static explicit operator byte[] (DirectBitmap dbmp)
        {
            return (byte[])imgConverter.ConvertTo(dbmp.Bitmap, imgFormat);
        }

        public Color get_Pixel(Point p)
        {
            return get_Pixel(p.X, p.Y);
        }
        public void set_Pixel(Point p, Color value)
        {
            set_Pixel(p.X, p.Y, value);
        }

        public Color get_Pixel(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
            {
                return default(Color);
            }
            int offset = y * w4 + x * 4;
            return Color.FromArgb(Bits[offset + 3],
                Bits[offset + 2],
                Bits[offset + 1],
                Bits[offset + 0]);
        }
        public void set_Pixel(int x, int y, Color value)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
            {
                return;
            }
            int offset = y * w4 + x * 4;
            Bits[offset + 3] = value.A;
            Bits[offset + 2] = value.R;
            Bits[offset + 1] = value.G;
            Bits[offset + 0] = value.B;
        }

        public Color get_PixelA(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
            {
                return default(Color);
            }
            int offset = y * w4 + x * 4;
            return Color.FromArgb(Bits[offset + 3],
                Bits[offset + 2],
                Bits[offset + 1],
                Bits[offset + 0]);
        }
        public void set_PixelA(int x, int y, Color value)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
            {
                return;
            }
            int offset = y * w4 + x * 4;

            int A = Bits[offset + 3];
            int R = Bits[offset + 2];
            int G = Bits[offset + 1];
            int B = Bits[offset + 0];

            double A1p = value.A / 256.0;
            double A2p = A / 256.0;
            double OA = GetAlpha(A1p, A2p);

            //Bits[offset + 3] = (byte) (Math.Floor(OA * 255));
            Bits[offset + 3] = (byte)(Math.Floor(OA * 255));
            Bits[offset + 2] = (byte)(ApplyAlpha(value.R, R, A1p, A2p, OA));
            Bits[offset + 1] = (byte)(ApplyAlpha(value.G, G, A1p, A2p, OA));
            Bits[offset + 0] = (byte)(ApplyAlpha(value.B, B, A1p, A2p, OA));
        }

        private int ApplyAlpha(int c1, int c2, double a1p, double a2p, double a)
        {
            if (a == 0)
            {
                return 0;
            }
            return System.Convert.ToInt32(Math.Floor((c1 * a1p + c2 * a2p * (1 - a1p)) * a));
        }

        private double GetAlpha(double a1p, double a2p)
        {
            return a1p + a2p * (1 - a1p);
        }

        #region IDisposable Support
        private bool disposedValue; // To detect redundant calls

        // IDisposable
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    Bitmap.Dispose();
                    bitsHandle.Free();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.
                // TODO: set large fields to null.
            }
            disposedValue = true;
        }

        // TODO: override Finalize() only if Dispose(disposing As Boolean) above has code to free unmanaged resources.
        //Protected Overrides Sub Finalize()
        //    ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
        //    Dispose(False)
        //    MyBase.Finalize()
        //End Sub

        // This code added by Visual Basic to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
            Dispose(true);
            // TODO: uncomment the following line if Finalize() is overridden above.
            // GC.SuppressFinalize(Me)
        }
        #endregion
    }

    static class DirectBitmapExtensions
    {
        private const double ToRad = Math.PI / 180.0;
        private const double ToDeg = 180.0 / Math.PI;

        public static void Clear(this DirectBitmap dbmp, Color c)
        {
            object[] b = new object[] { c.B, c.G, c.R, c.A };
            int bufferSize = dbmp.Height * dbmp.Width * 4 - 1;
            for (int i = 0; i <= bufferSize; i += 4)
            {
                Array.Copy((System.Array)b, 0, dbmp.Bits, i, 4);
            }
        }

        public static void DrawLine(this DirectBitmap dbmp, Color c, int x1, int y1, int x2, int y2)
        {
            int dx = x2 - x1;
            int dy = y2 - y1;
            int l = System.Convert.ToInt32(Math.Sqrt(Math.Pow(dx, 2) + Math.Pow(dy, 2)));
            double a = Math.Atan2(dy, dx);
            for (int r = 0; r <= l; r++)
            {
                dbmp.set_Pixel(System.Convert.ToInt32(x1 + r * Math.Cos(System.Convert.ToDouble(-a))), System.Convert.ToInt32(y1 + r * Math.Sin(a)), c);
            }
        }

        public static void DrawLine(this DirectBitmap dbmp, Color c, Point p1, Point p2)
        {
            dbmp.DrawLine(c, p1.X, p1.Y, p2.X, p2.Y);
        }

        public static void DrawPolygon(this DirectBitmap dbmp, Color c, Point[] p)
        {
            int j = 0;
            int l = p.Length;
            for (int i = 0; i <= l - 1; i++)
            {
                j = System.Convert.ToInt32((i + 1) % l);
                dbmp.DrawLine(c, p[i], p[j]);
            }
        }

        public static void DrawPolygon(this DirectBitmap dbmp, Color c, PointF[] p)
        {
            int l = p.Length - 1;
            Point[] pi = new Point[l + 1];
            for (int i = 0; i <= l; i++)
            {
                pi[i] = new Point(System.Convert.ToInt32(p[i].X), System.Convert.ToInt32(p[i].Y));
            }
            dbmp.DrawPolygon(c, pi);
        }

        public static void FillRectangle(this DirectBitmap dbmp, Color c, Rectangle r)
        {
            FillRectangle(dbmp, c, r.X, r.Y, r.Width, r.Height);
        }

        public static void FillRectangle(this DirectBitmap dbmp, Color c, int x, int y, int w, int h)
        {
            for (int y1 = y; y1 <= y + h - 1; y1++)
            {
                for (int x1 = x; x1 <= x + w - 1; x1++)
                {
                    dbmp.set_Pixel(x1, y1, c);
                }
            }
        }
    }

}
