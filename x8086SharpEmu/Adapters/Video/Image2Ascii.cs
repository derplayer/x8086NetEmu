using System.Collections.Generic;
using System;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Xml.Linq;
using System.Collections;


using x8086SharpEmu;
using Assets.CCC.x8086Sharp.UnityHelpers;

namespace x8086SharpEmu
{
    public class Image2Ascii
    {
        public enum ColorModes
        {
            GrayScale,
            FullGrayScale,
            Color,
            DitheredGrayScale,
            DitheredColor
        }

        public enum ScanModes
        {
            Fast,
            Accurate
        }

        public enum Charsets
        {
            Standard = 0,
            Advanced = 1
        }

        public enum GrayscaleModes
        {
            Average,
            Accuarte
        }

        public struct ASCIIChar
        {
            public char Character { get; set; }
            public Color Color { get; set; }

            public ASCIIChar(char character, Color color)
            {
                this.Character = character;
                this.Color = color;
            }
        }

        private DirectBitmap mBitmap;
        private Bitmap mSurface;

        private Size mCanvasSize;
        private ASCIIChar[][] mCanvas;
        private ColorModes mColorMode;
        private ScanModes mScanMode;
        private Charsets mCharset;
        private GrayscaleModes mGrayScaleMode;
        private Color mBackColor;

        private int mDitherColors = 8;

        private Font mFont;

        private Size lastCanvasSize = new Size(-1, -1);
        private Graphics surfaceGraphics;
        private string[] charsetsChars = new string[] { " ·:+x#W@", " ░░▒▒▓▓█" };
        private string activeChars;

        private Point mChatOffset;
        private Size mCharSize;

        private static Dictionary<Color, ConsoleColor> c2ccCache = new Dictionary<Color, ConsoleColor>();

        public delegate void ImageProcessedEventHandler(object sender, System.EventArgs e);
        private ImageProcessedEventHandler ImageProcessedEvent;

        public event ImageProcessedEventHandler ImageProcessed
        {
            add
            {
                ImageProcessedEvent = (ImageProcessedEventHandler)System.Delegate.Combine(ImageProcessedEvent, value);
            }
            remove
            {
                ImageProcessedEvent = (ImageProcessedEventHandler)System.Delegate.Remove(ImageProcessedEvent, value);
            }
        }


        public Image2Ascii()
        {
            activeChars = new string[] { " ·:+x#W@", " ░░▒▒▓▓█" }[0];

            mCanvasSize = new Size(80, 25);
            mColorMode = ColorModes.GrayScale;
            mScanMode = ScanModes.Fast;
            mCharset = Charsets.Standard;
            mGrayScaleMode = GrayscaleModes.Average;
            mBackColor = Color.Black;
            mFont = new Font("Consolas", 12); //, GraphicsUnit.Pixel
            SetCharSize();
        }

        public Size CanvasSize
        {
            get
            {
                return mCanvasSize;
            }
            set
            {
                if (mCanvasSize != value)
                {
                    mCanvasSize = value;
                    ProcessImage();
                }
            }
        }

        public DirectBitmap DirectBitmap
        {
            get
            {
                return mBitmap;
            }
        }

        public Bitmap Bitmap
        {
            get
            {
                return mBitmap.Bitmap;
            }
            set
            {
                mBitmap = value;
                ProcessImage();
            }
        }

        public Bitmap Surface
        {
            get
            {
                return mSurface;
            }
        }

        public GrayscaleModes GrayScaleMode
        {
            get
            {
                return mGrayScaleMode;
            }
            set
            {
                mGrayScaleMode = value;
                ProcessImage();
            }
        }

        public Charsets Charset
        {
            get
            {
                return mCharset;
            }
            set
            {
                mCharset = value;
                activeChars = charsetsChars[(int)mCharset];
                ProcessImage();
            }
        }

        public ColorModes ColorMode
        {
            get
            {
                return mColorMode;
            }
            set
            {
                mColorMode = value;
                ProcessImage();
            }
        }

        public ScanModes ScanMode
        {
            get
            {
                return mScanMode;
            }
            set
            {
                mScanMode = value;
                ProcessImage();
            }
        }

        public Size CharSize
        {
            get
            {
                return mCharSize;
            }
        }

        public Color BackColor
        {
            get
            {
                return mBackColor;
            }
            set
            {
                mBackColor = value;
                ProcessImage();
            }
        }

        public Font Font
        {
            get
            {
                return mFont;
            }
            set
            {
                mFont = value;
                SetCharSize();
                ProcessImage();
            }
        }

        public int DitherColors
        {
            get
            {
                return mDitherColors;
            }
            set
            {
                if (value >= 2)
                {
                    mDitherColors = value;
                    ProcessImage();
                }
                else
                {
                    // Throw New ArgumentOutOfRangeException($"{NameOf(DitherColors)} must be 2 or larger")
                }
            }
        }

        public ASCIIChar[][] Canvas
        {
            get
            {
                return mCanvas;
            }
        }

        delegate bool delegate_IsBlack(Color c);

        private void SetCharSize()
        {
            delegate_IsBlack IsBlack = (Color c) =>
            {
                return c.R == 0 && c.G == 0 && c.B == 0;
            };

            using (DirectBitmap bmp = new DirectBitmap(100, 100))
            {
                //using (Graphics g = Graphics.FromImage(bmp.Bitmap))
                //{
                //    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                //    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None;
                //    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

                //    g.Clear(Color.Black);
                //    g.DrawString("█", mFont, Brushes.White, 0, 0);
                //}


                Point lt = new Point();
                Point rb = new Point();

                for (int y = 0; y <= bmp.Height - 1; y++)
                {
                    for (int x = 0; x <= bmp.Width - 1; x++)
                    {
                        if (!IsBlack(bmp.get_Pixel(x, y)))
                        {
                            lt = new Point(x, y);
                            y = bmp.Height;
                            break;
                        }
                    }
                }

                for (int y = bmp.Height - 1; y >= 0; y--)
                {
                    for (int x = bmp.Width - 1; x >= 0; x--)
                    {
                        if (!IsBlack(bmp.get_Pixel(x, y)))
                        {
                            rb = new Point(x, y);
                            y = 0;
                            break;
                        }
                    }
                }

                mCharSize = new Size(rb.X - lt.X, rb.Y - lt.Y);
                if (mCharSize.Width > 1)
                {
                    mCharSize.Width--;
                }
            }

        }

        delegate void delegate_ApplyQuantaError(int qx, int qy, int qr, int qg, int qb, double w);

        public void ProcessImage(bool surfaceGraphics = true)
        {
            if (ReferenceEquals(mBitmap, null))
            {
                return;
            }

            int sx = 0;
            int sy = 0;

            if (lastCanvasSize != mCanvasSize)
            {
                lastCanvasSize = mCanvasSize;

                if (mSurface != null)
                {
                    //mSurface.Dispose();
                }
                mSurface = (Bitmap)new DirectBitmap(mCanvasSize.Width * CharSize.Width, mCanvasSize.Height * CharSize.Height);

                mCanvas = new ASCIIChar[mCanvasSize.Width][];
                for (var x = 0; x <= mCanvasSize.Width - 1; x++)
                {
                    mCanvas[(int)x] = new ASCIIChar[mCanvasSize.Height];
                    for (var y = 0; y <= mCanvasSize.Height - 1; y++)
                    {
                        mCanvas[(int)x][y] = new ASCIIChar(' ', this.BackColor);
                    }
                }
            }

            if (surfaceGraphics)
            {
                //this.surfaceGraphics = Graphics.FromImage(mSurface);
                this.surfaceGraphics.Clear(this.BackColor);
            }

            Size scanStep = new Size((int)(Math.Ceiling((double)mBitmap.Width / mCanvasSize.Width)), (int)(Math.Ceiling((double)mBitmap.Height / mCanvasSize.Height)));
            //scanStep.Width += mCanvasSize.Width Mod scanStep.Width
            //scanStep.Height += mCanvasSize.Height Mod scanStep.Height
            var scanStepSize = scanStep.Width * scanStep.Height;

            // Source color
            int r = 0;
            int g = 0;
            int b = 0;

            // Dithered Color
            int dr = 0;
            int dg = 0;
            int db = 0;
            int dColorFactor = mDitherColors - 1;
            double dFactor = (double)255 / dColorFactor;
            double[] quantaError = new double[3];
            delegate_ApplyQuantaError ApplyQuantaError = (int qx, int qy, int qr, int qg, int qb, double w) =>
            {
                if (qx < 0 || qx >= mCanvasSize.Width ||
                    qy < 0 || qy >= mCanvasSize.Height)
                {
                    return;
                }
                qr += (int)(quantaError[0] * w);
                qg += (int)(quantaError[1] * w);
                qb += (int)(quantaError[2] * w);
                mCanvas[qx][qy] = new ASCIIChar(ColorToASCII(qr, qg, qb), Color.FromArgb(qr, qg, qb));
            };

            // For gray scale modes
            int gray = 0;

            int offset = 0;

            for (int y = 0; y <= mBitmap.Height - scanStep.Height - 1; y += scanStep.Height)
            {
                for (int x = 0; x <= mBitmap.Width - scanStep.Width - 1; x += scanStep.Width)
                {
                    if (mScanMode == ScanModes.Fast)
                    {
                        offset = (x + y * mBitmap.Width) * 4;
                        r = mBitmap.Bits[offset + 2];
                        g = mBitmap.Bits[offset + 1];
                        b = mBitmap.Bits[offset + 0];
                    }
                    else
                    {
                        r = 0;
                        g = 0;
                        b = 0;

                        for (var y1 = y; y1 <= y + scanStep.Height - 1; y1++)
                        {
                            for (var x1 = x; x1 <= x + scanStep.Width - 1; x1++)
                            {
                                offset = (int)((x1 + y1 * mBitmap.Width) * 4);

                                r += mBitmap.Bits[offset + 2];
                                g += mBitmap.Bits[offset + 1];
                                b += mBitmap.Bits[offset + 0];
                            }
                        }

                        r /= scanStepSize;
                        g /= scanStepSize;
                        b /= scanStepSize;
                    }

                    sx = (int)((double)x / scanStep.Width);
                    sy = (int)((double)y / scanStep.Height);

                    switch (mColorMode)
                    {
                        case ColorModes.GrayScale:
                            mCanvas[sx][sy] = new ASCIIChar(ColorToASCII(r, g, b), Color.White);
                            break;
                        case ColorModes.FullGrayScale:
                            gray = (int)(ToGrayScale(r, g, b));
                            mCanvas[sx][sy] = new ASCIIChar(ColorToASCII(r, g, b), Color.FromArgb(gray, gray, gray));
                            break;
                        case ColorModes.Color:
                            mCanvas[sx][sy] = new ASCIIChar(ColorToASCII(r, g, b), Color.FromArgb(r, g, b));
                            break;
                        case ColorModes.DitheredGrayScale:
                        case ColorModes.DitheredColor:
                            // https://en.wikipedia.org/wiki/Floyd%E2%80%93Steinberg_dithering
                            if (mColorMode == ColorModes.DitheredGrayScale)
                            {
                                r = (int)(ToGrayScale(r, g, b));
                                g = r;
                                b = r;
                            }
                            dr = (int)(Math.Round((double)dColorFactor * r / 255) * dFactor);
                            dg = (int)(Math.Round((double)dColorFactor * g / 255) * dFactor);
                            db = (int)(Math.Round((double)dColorFactor * b / 255) * dFactor);

                            mCanvas[sx][sy] = new ASCIIChar(ColorToASCII(dr, dg, db), Color.FromArgb(dr, dg, db));

                            quantaError = new double[] {Math.Max(0, r - dr),
                                Math.Max(0, g - dg),
                                Math.Max(0, b - db)};

                            ApplyQuantaError(sx + 1, sy, dr, dg, db, (double)7 / 16);
                            ApplyQuantaError(sx - 1, sy + 1, dr, dg, db, (double)3 / 16);
                            ApplyQuantaError(sx, sy + 1, dr, dg, db, (double)5 / 16);
                            ApplyQuantaError(sx + 1, sy + 1, dr, dg, db, (double)1 / 16);
                            break;
                    }

                    if (surfaceGraphics)
                    {
                        using (SolidBrush sb = new SolidBrush(mCanvas[sx][sy].Color))
                        {
                            this.surfaceGraphics.DrawString(System.Convert.ToString(mCanvas[sx][sy].Character), this.Font, sb, sx * CharSize.Width, sy * CharSize.Height);
                        }

                    }
                }
            }
            if (surfaceGraphics)
            {
                this.surfaceGraphics.Dispose();
            }

            if (ImageProcessedEvent != null)
                ImageProcessedEvent(this, new System.EventArgs());
        }

        private char ColorToASCII(Color color)
        {
            return ColorToASCII(color.R, color.G, color.B);
        }

        private char ColorToASCII(int r, int g, int b)
        {
            return activeChars[(int)Math.Floor(ToGrayScale(r, g, b) / ((double)256 / activeChars.Length))];
        }

        private double ToGrayScale(int r, int g, int b)
        {
            switch (mGrayScaleMode)
            {
                case GrayscaleModes.Accuarte:
                    return r * 0.2126 + g * 0.7152 + b * 0.0722;
                case GrayscaleModes.Average:
                    return (double)r / 3 + (double)g / 3 + (double)b / 3;
                default:
                    return 0;
            }
        }

        public static ConsoleColor ToConsoleColor(Color c)
        {
            double d = 0;
            double minD = double.MaxValue;
            ConsoleColor bestResult = default(ConsoleColor);
            int[] ccRgb = null;

            if (c2ccCache.ContainsKey(c))
            {
                return c2ccCache[c];
            }

            foreach (ConsoleColor cc in Enum.GetValues(typeof(ConsoleColor)))
            {
                switch (cc)
                {
                    case ConsoleColor.Black:
                        ccRgb = HexColorToArray("000000");
                        break;
                    case ConsoleColor.DarkBlue:
                        ccRgb = HexColorToArray("000080");
                        break;
                    case ConsoleColor.DarkGreen:
                        ccRgb = HexColorToArray("008000");
                        break;
                    case ConsoleColor.DarkCyan:
                        ccRgb = HexColorToArray("008080");
                        break;
                    case ConsoleColor.DarkRed:
                        ccRgb = HexColorToArray("800000");
                        break;
                    case ConsoleColor.DarkMagenta:
                        ccRgb = HexColorToArray("800080");
                        break;
                    case ConsoleColor.DarkYellow:
                        ccRgb = HexColorToArray("808000");
                        break;
                    case ConsoleColor.Gray:
                        ccRgb = HexColorToArray("C0C0C0");
                        break;
                    case ConsoleColor.DarkGray:
                        ccRgb = HexColorToArray("808080");
                        break;
                    case ConsoleColor.Blue:
                        ccRgb = HexColorToArray("0000FF");
                        break;
                    case ConsoleColor.Green:
                        ccRgb = HexColorToArray("00FF00");
                        break;
                    case ConsoleColor.Cyan:
                        ccRgb = HexColorToArray("00FFFF");
                        break;
                    case ConsoleColor.Red:
                        ccRgb = HexColorToArray("FF0000");
                        break;
                    case ConsoleColor.Magenta:
                        ccRgb = HexColorToArray("FF00FF");
                        break;
                    case ConsoleColor.Yellow:
                        ccRgb = HexColorToArray("FFFF00");
                        break;
                    case ConsoleColor.White:
                        ccRgb = HexColorToArray("FFFFFF");
                        break;
                }

                d = Math.Sqrt(Math.Pow((c.R - ccRgb[0]), 2) + Math.Pow((c.G - ccRgb[1]), 2) + Math.Pow((c.B - ccRgb[2]), 2));
                if (d < minD)
                {
                    minD = d;
                    bestResult = cc;
                }
            }

            c2ccCache.Add(c, bestResult);
            return bestResult;
        }

        // EGA Palette
        // http://stackoverflow.com/questions/1988833/converting-color-to-consolecolor
        public static ConsoleColor ToConsoleColorEGA(Color c)
        {
            int index = (int)(c.R > 128 | c.G > 128 | c.B > 128 ? 8 : 0); // Bright bit
            index = (int)(index | (c.R > 64 ? 4 : 0)); // Red bit
            index = (int)(index | (c.G > 64 ? 2 : 0)); // Green bit
            index = (int)(index | (c.B > 64 ? 1 : 0)); // Blue bit
            return ((ConsoleColor)index);
        }

        public static int[] HexColorToArray(string hexColor)
        {
            return new[] {int.Parse(hexColor.Substring(0, 2), System.Globalization.NumberStyles.HexNumber),
                int.Parse(hexColor.Substring(2, 2), System.Globalization.NumberStyles.HexNumber),
                int.Parse(hexColor.Substring(4, 2), System.Globalization.NumberStyles.HexNumber)};
        }
    }

}
