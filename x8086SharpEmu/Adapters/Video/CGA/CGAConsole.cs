using System.Collections.Generic;
using System;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Xml.Linq;
using System.Collections;

using System.Threading;

using x8086SharpEmu;
using Assets.CCC.x8086Sharp.UnityHelpers;
using System.Windows.Forms;

namespace x8086SharpEmu
{

    public class CGAConsole : CGAAdapter
    {

        private int blinkCounter;
        private byte[] buffer;

        private ConsoleModifiers lastModifiers;

        private Image2Ascii i2a;
        private bool isRendering;
        private Size ratio = new Size(3, 4);
        private int frameRate = 27;

        public CGAConsole(X8086 cpu) : base(cpu)
        {
            InitiAdapter();
            AutoSize();

            Console.TreatControlCAsInput = true;
            Console.OutputEncoding = new System.Text.UTF8Encoding();
            Console.Clear();

            i2a = new Image2Ascii()
            {
                Charset = Image2Ascii.Charsets.Standard,
                ColorMode = Image2Ascii.ColorModes.Color,
                GrayScaleMode = Image2Ascii.GrayscaleModes.Average,
                ScanMode = Image2Ascii.ScanModes.Fast
            };

            System.Threading.Tasks.Task.Run(() =>
            {
                do
                {
                    Thread.Sleep(1000 / frameRate);

                    try
                    {
                        if (MainMode == MainModes.Graphics)
                        {
                            i2a.ProcessImage(false);

                            for (int y = 0; y <= Console.WindowHeight - 1; y++)
                            {
                                for (int x = 0; x <= Console.WindowWidth - 1; x++)
                                {
                                    //ConsoleCrayon.WriteFast(i2a.Canvas(x)[y].Character,
                                    //Image2Ascii.ToConsoleColor(i2a.Canvas(x)[y].Color),
                                    //ConsoleColor.Black,
                                    ConsoleCrayon.WriteFast(Char.ToString(i2a.Canvas[x][y].Character), Image2Ascii.ToConsoleColor(i2a.Canvas[x][y].Color), ConsoleColor.Black,
                                    x, y);
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                } while (!base.cancelAllThreads);
            });
        }

        private bool HasModifier(ConsoleModifiers v, ConsoleModifiers t)
        {
            return (v & t) == t;
        }

        protected override void AutoSize()
        {
            int length = TextResolution.Width * TextResolution.Height * 2;
            if (ReferenceEquals(buffer, null) || buffer?.Length != length)
            {
                buffer = new byte[length];
                ResizeRenderControl();
            }
        }

        protected override void ResizeRenderControl()
        {
#if Win32
			if (MainMode == MainModes.Text)
			{
				Console.SetWindowSize(TextResolution.Width, TextResolution.Height);
			}
			else if (MainMode == MainModes.Graphics)
			{
				ratio = new Size((int)(Math.Ceiling((double) GraphicsResolution.Width / Console.LargestWindowWidth)), (int)(Math.Ceiling((double) GraphicsResolution.Height / Console.LargestWindowHeight)));
				Console.SetWindowSize((int)((double) GraphicsResolution.Width / ratio.Width), (int)((double) GraphicsResolution.Height / ratio.Height));
				ResetI2A();
				Console.SetWindowSize(i2a.CanvasSize.Width, i2a.CanvasSize.Height);
			}
			Console.SetBufferSize(Console.WindowWidth, Console.WindowHeight);
			Array.Clear(buffer, 0, buffer.Length);
#endif
        }

        protected override void InitVideoMemory(bool clearScreen)
        {
            base.InitVideoMemory(clearScreen);

            Console.Title = "x8086SharpEmuConsole - " + VideoMode.ToString();

            if (MainMode == MainModes.Graphics)
            {
                ResetI2A();
            }

            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                case PlatformID.Win32S:
                case PlatformID.Win32Windows:
                case PlatformID.WinCE:
                    break;
                default:
                    if (mVideoMode != 0xFF && (Console.WindowWidth != mTextResolution.Width || Console.WindowHeight != mTextResolution.Height))
                    {
                        ConsoleCrayon.ResetColor();
                        Console.Clear();
                        ConsoleCrayon.WriteFast("Unsupported Console Window Size", ConsoleColor.White, ConsoleColor.Red, 0, 0);
                        ConsoleCrayon.ResetColor();
                        Console.WriteLine();
                        Console.WriteLine("The window console cannot be resized on this platform, which will case the text to be rendered incorrectly");
                        Console.WriteLine();
                        Console.WriteLine("Expected Resolution for Video Mode {mVideoMode:X2}: {mTextResolution.Width}x{mTextResolution.Height}");
                        Console.WriteLine("Current console window resolution:     {Console.WindowWidth}x{Console.WindowHeight}");
                        Console.WriteLine();
                        Console.WriteLine("Manually resize the window to the appropriate resolution or press any key to continue");
                        do
                        {
                            ConsoleCrayon.WriteFast("New resolution: {Console.WindowWidth}x{Console.WindowHeight}", ConsoleColor.White, ConsoleColor.DarkRed, 0, 10);
                            Thread.Sleep(200);
                            if (Console.WindowWidth == mTextResolution.Width && Console.WindowHeight == mTextResolution.Height)
                            {
                                break;
                            }
                        } while (!Console.KeyAvailable);
                        ConsoleCrayon.ResetColor();
                        Console.Clear();
                    }
                    break;
            }
        }

        private void ResetI2A()
        {
            if (i2a != null)
            {
                if (i2a.Bitmap != null)
                {
                    //i2a.Bitmap.Dispose();
                }
                i2a.Bitmap = (Bitmap)new DirectBitmap(GraphicsResolution.Width, GraphicsResolution.Height);
                i2a.CanvasSize = new Size(Console.WindowWidth, Console.WindowHeight);
                Console.CursorVisible = false;
            }
        }

        private void HandleKeyPress()
        {
            if (Console.KeyAvailable)
            {
                ConsoleKeyInfo keyInfo = Console.ReadKey(true);
                KeyEventArgs keyEvent = new KeyEventArgs((Keys)keyInfo.Key);

                HandleModifier(keyInfo.Modifiers, ConsoleModifiers.Shift, Keys.ShiftKey);
                HandleModifier(keyInfo.Modifiers, ConsoleModifiers.Control, Keys.ControlKey);
                HandleModifier(keyInfo.Modifiers, ConsoleModifiers.Alt, (System.Windows.Forms.Keys)(Keys.Alt | Keys.Menu));
                lastModifiers = keyInfo.Modifiers;

                base.HandleKeyDown(this, keyEvent);
                Thread.Sleep(100);
                base.HandleKeyUp(this, keyEvent);
            }
        }

        private void HandleModifier(ConsoleModifiers v, ConsoleModifiers t, Keys k)
        {
            if (HasModifier(v, t) && !HasModifier(lastModifiers, t))
            {
                base.HandleKeyDown(this, new KeyEventArgs(k));
                Thread.Sleep(100);
            }
            else if (!HasModifier(v, t) && HasModifier(lastModifiers, t))
            {
                base.HandleKeyUp(this, new KeyEventArgs(k));
                Thread.Sleep(100);
            }
        }

        protected override void Render()
        {
            if (isRendering || ReferenceEquals(CPU, null))
            {
                return;
            }
            isRendering = true;

            if (base.VideoEnabled)
            {
                try
                {
                    if (MainMode == MainModes.Text)
                    {
                        RenderText();
                    }
                    else if (MainMode == MainModes.Graphics)
                    {
                        RenderGraphics();
                    }
                }
                catch
                {
                }
            }

            HandleKeyPress();

            isRendering = false;
        }

        private void RenderGraphics() // This is cool, I guess, but completely useless...
        {
            byte b = 0;
            uint address = 0;
            int xDiv = (int)(PixelsPerByte == 4 ? 2 : 3);

            for (int y = 0; y <= GraphicsResolution.Height - 1; y++)
            {
                for (int x = 0; x <= GraphicsResolution.Width - 1; x++)
                {
                    address = (uint)(mStartGraphicsVideoAddress + ((y >> 1) * 80) + ((y & 1) * 0x2000) + (x >> xDiv));
                    b = CPU.Memory[address];

                    if (PixelsPerByte == 4)
                    {
                        switch (x & 3)
                        {
                            case 3:
                                b = (byte)(b & 3);
                                break;
                            case 2:
                                b = (byte)((b >> 2) & 3);
                                break;
                            case 1:
                                b = (byte)((b >> 4) & 3);
                                break;
                            case 0:
                                b = (byte)((b >> 6) & 3);
                                break;
                        }
                    }
                    else
                    {
                        b = (byte)((b >> (7 - (x & 7))) & 1);
                    }

                    i2a.DirectBitmap.set_Pixel(x, y, CGAPalette[b]);
                }
            }
        }

        private void RenderText()
        {
            byte b0 = 0;
            byte b1 = 0;

            int col = 0;
            int row = 0;
            int bufIdx = 0;

            bool cv = false;

            string text = "";
            byte b1c = CPU.Memory[mStartTextVideoAddress + 1];
            int c = 0;
            int r = 0;

            Console.CursorVisible = false;

            // The "-4" is to prevent the code from printing the last character and avoid scrolling.
            // Unfortunately, this causes the last char to not be printed
            for (int address = mStartTextVideoAddress; address <= mEndTextVideoAddress + buffer.Length - 4; address += 2)
            {
                b0 = CPU.Memory[address];
                b1 = CPU.Memory[address + 1];

                if ((blinkCounter < BlinkRate) && BlinkCharOn && (b1 & 0x80) != 0)
                {
                    b0 = (byte)0;
                }

                if (b1c != b1)
                {
                    ConsoleCrayon.WriteFast(text, (ConsoleColor)b1c.LowNib(), (ConsoleColor)b1c.HighNib(), c, r);
                    text = "";
                    b1c = b1;
                    c = col;
                    r = row;
                }
                text += chars[b0].ToString();

                if (CursorVisible && row == CursorRow && col == CursorCol)
                {
                    cv = blinkCounter < BlinkRate;

                    if (blinkCounter >= 2 * BlinkRate)
                    {
                        blinkCounter = 0;
                    }
                    else
                    {
                        blinkCounter++;
                    }
                }

                col++;
                if (col == TextResolution.Width)
                {
                    col = 0;
                    row++;
                    if (row == TextResolution.Height)
                    {
                        break;
                    }
                }

                bufIdx += 2;
            }

            if (!string.IsNullOrEmpty(text))
            {
                ConsoleCrayon.WriteFast(text, (ConsoleColor)b1c.LowNib(), (ConsoleColor)b1c.HighNib(), c, r);
            }

            if (cv)
            {
                Console.SetCursorPosition(CursorCol, CursorRow);
                Console.CursorVisible = true;
            }
        }

        public override void Run()
        {
        }

        public override string Description
        {
            get
            {
                return "CGA Console Adapter";
            }
        }

        public override string Name
        {
            get
            {
                return "CGA Console";
            }
        }
    }

}
