using System.Collections.Generic;
using System;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Xml.Linq;
using System.Collections;
using System.Windows.Forms;

using x8086SharpEmu;

namespace x8086SharpEmu
{
    public class VideoChar
    {
        public int CGAChar { get; set; }
        public Color ForeColor { get; set; }
        public Color BackColor { get; set; }

        private DirectBitmap mBitmap;
        private int w4s;

        public static byte[] FontBitmaps;

        public VideoChar(int c, Color fb, Color bb)
        {
            CGAChar = c;
            ForeColor = fb;
            BackColor = bb;
        }

        public void Paint(DirectBitmap dbmp, Point p, SizeF scale)
        {
            int w4d = dbmp.Width * 4;
            p.X *= 4;
            for (int y = 0; y <= mBitmap.Height - 1; y++)
            {
                Array.Copy(mBitmap.Bits, y * w4s, dbmp.Bits, (y + p.Y) * w4d + p.X, w4s);
            }
        }

        public void Render(int w, int h)
        {
            if (ReferenceEquals(mBitmap, null))
            {
                w = 8;
                h = 16;
                mBitmap = new DirectBitmap(w, h);
                w4s = w * 4;

                for (int y = 0; y <= h - 1; y++)
                {
                    for (int x = 0; x <= w - 1; x++)
                    {
                        if (FontBitmaps[CGAChar * w * h + y * w + x] == 1)
                        {
                            mBitmap.set_Pixel(x, y, ForeColor);
                        }
                        else
                        {
                            mBitmap.set_Pixel(x, y, BackColor);
                        }
                    }
                }
            }
        }

        public static bool operator ==(VideoChar c1, VideoChar c2)
        {
            return c1.CGAChar == c2.CGAChar &&
                c1.ForeColor == c2.ForeColor &&
                c1.BackColor == c2.BackColor;
        }

        public static bool operator !=(VideoChar c1, VideoChar c2)
        {
            return !(c1 == c2);
        }

        public override bool Equals(object obj)
        {
            return this == ((VideoChar)obj);
        }

        public override string ToString()
        {
            return string.Format("{0:000} [{1:000}:{2:000}:{3:000}] [{4:000}:{5:000}:{6:000}]",
                CGAChar,
                ForeColor.R,
                ForeColor.G,
                ForeColor.B,
                BackColor.R,
                BackColor.G,
                BackColor.B);
        }

        // http://goughlui.com/2016/05/01/project-examining-vga-bios-from-old-graphic-cards/
        public static void BuildFontBitmapsFromROM(int fontWidth, int fontHeight, int romFontHeight, int romOffset, byte[] rom)
        {
            int fw = fontWidth;
            int fh = fontHeight;
            int dataW = 1;
            int dataH = romFontHeight;

            int romSize = rom.Length;
            int offset = romOffset;
            VideoChar.FontBitmaps = new byte[fw * fh * 512];

            int tempCount = 0;
            int @base = 0;
            int row = 0;
            int mask = 0x80;

            int x = 0;
            int y = 0;

            for (int i = 0; i <= 512 - 1; i++)
            {
                while (@base < fh)
                {
                    while (tempCount < dataW)
                    {
                        while (mask != 0)
                        {
                            if ((rom[(@base + (tempCount * dataH) + (row * dataW * dataH) + offset) % romSize] & mask) != 0)
                            {
                                VideoChar.FontBitmaps[i * fw * fh + y * fw + x] = (byte)1;
                            }
                            else
                            {
                                VideoChar.FontBitmaps[i * fw * fh + y * fw + x] = (byte)0;
                            }
                            x++;
                            mask = mask >> 1;
                        }
                        tempCount++;
                        mask = 0x80;
                    }
                    tempCount = 0;
                    @base++;
                    x = 0;
                    y++;
                }
                @base = 0;
                row++;
                x = 0;
                y = 0;
            }
        }
    }
}
