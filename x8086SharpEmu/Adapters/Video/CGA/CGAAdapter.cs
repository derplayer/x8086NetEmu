using System.Collections.Generic;
using System;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Xml.Linq;
using System.Collections;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;

using x8086SharpEmu;

namespace x8086SharpEmu
{

    public abstract class CGAAdapter : VideoAdapter
    {

        public const double VERTSYNC = 60.0;
        public const double HORIZSYNC = VERTSYNC * 262.5;

        protected const ulong ht = (ulong)(Scheduler.BASECLOCK / HORIZSYNC);
        protected const ulong vt = (ulong)((Scheduler.BASECLOCK / HORIZSYNC) * (HORIZSYNC / VERTSYNC));

        protected List<VideoChar> charsCache = new List<VideoChar>();
        protected Dictionary<int, Size> charSizeCache = new Dictionary<int, Size>();
        protected readonly VideoChar[] memCache = new VideoChar[0xFFFFF + 1];

        public enum VideoModes
        {
            Mode0_Text_BW_40x25 = 0x4,
            Mode1_Text_Color_40x25 = 0x0,
            Mode2_Text_BW_80x25 = 0x5,
            Mode3_Text_Color_80x25 = 0x1,

            Mode4_Graphic_Color_320x200 = 0x2,
            Mode5_Graphic_BW_320x200 = 0x6,
            Mode6_Graphic_Color_640x200 = 0x16,
            Mode6_Graphic_Color_640x200_Alt = 0x12,

            Mode7_Text_BW_80x25 = 0x7F,

            Undefined = 0xFF
        }

        private Color[] CGABasePalette;

        // http://www.htl-steyr.ac.at/~morg/pcinfo/hardware/interrupts/inte6l9s.htm

        protected Color[] CGAPalette = new Color[16];

        protected internal enum CGAModeControlRegisters
        {
            blink_enabled = 5,
            high_resolution_graphics = 4,
            video_enabled = 3,
            black_and_white = 2,
            graphics_mode = 1,
            high_resolution = 0
        }

        protected internal enum CGAColorControlRegisters
        {
            bright_background_or_blinking_text = 7,
            red_background = 6,
            green_background = 5,
            blue_background = 4,
            bright_foreground = 3,
            red_foreground = 2,
            green_foreground = 1,
            blue_foreground = 0
        }

        protected internal enum CGAStatusRegisters
        {
            vertical_retrace = 3,
            light_pen_switch_status = 2,
            light_pen_trigger_set = 1,
            display_enable = 0
        }

        protected internal enum CGAPaletteRegisters
        {
            active_color_set_is_red_green_brown = 5,
            intense_colors_in_graphics_or_background_colors_text = 4,
            intense_border_in_40x25_or_intense_background_in_320x200_or_intense_foreground_in_640x200 = 3,
            red_border_in_40x25_or_red_background_in_320x200_or_red_foreground_in_640x200 = 2,
            green_border_in_40x25_or_green_background_in_320x200_or_green_foreground_in_640x200 = 2,
            blue_border_in_40x25_or_blue_background_in_320x200_or_blue_foreground_in_640x200 = 0
        }

        private readonly byte[] CtrlMask = new byte[] {
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0x7F,
            0x1F,
            0x7F,
            0x7F,
            0xF3,
            0x1F,
            0x7F,
            0x1F,
            0x3F,
            0xFF,
            0x3F,
            0xFF,
            0xFF,
            0xFF,
            0x0,
            0x0,
            0x0,
            0x0,
            0x0,
            0x0,
            0x0,
            0x0,
            0x0,
            0x0,
            0x0,
            0x0,
            0x0,
            0x0
        };

        protected internal byte CRT6845IndexRegister = (byte)0;
        protected internal byte[] CRT6845DataRegister = new byte[256];

        protected internal bool[] CGAModeControlRegister = new bool[8];
        protected internal bool[] CGAColorControlRegister = new bool[8];
        protected internal bool[] CGAStatusRegister = new bool[8];
        protected internal bool[] CGAPaletteRegister = new bool[8];

        protected bool isInit;

        protected int mCursorCol = 0;
        protected int mCursorRow = 0;
        protected bool mCursorVisible;
        protected int mCursorStart = 0;
        protected int mCursorEnd = 1;

        protected bool mVideoEnabled = true;
        protected uint mVideoMode = (uint)VideoModes.Undefined;
        protected int mBlinkRate = 16; // 8 frames on, 8 frames off (http://www.oldskool.org/guides/oldonnew/resources/cgatech.txt)
        protected bool mBlinkCharOn;
        protected int mPixelsPerByte;

        private double mZoom = 1.0;

        protected DirectBitmap videoBMP = new DirectBitmap(1, 1);
        private readonly AutoResetEvent waiter = new AutoResetEvent(false);
        protected bool cancelAllThreads;
        private readonly bool useInternalTimer;

        //Public Event VideoRefreshed(sender As Object)

        protected char[] chars = new char[256];

        protected int vidModeChangeFlag;

        protected abstract override void AutoSize();
        protected abstract void Render();

        //protected WebUI wui;

        public CGAAdapter(X8086 cpu, bool useInternalTimer = true, bool enableWebUI = false) : base(cpu)
        {
            CGABasePalette = new Color[] {
                Color.FromArgb(0x0, 0x0, 0x0),
                Color.FromArgb(0x0, 0x0, 0xAA),
                Color.FromArgb(0x0, 0xAA, 0x0),
                Color.FromArgb(0x0, 0xAA, 0xAA),
                Color.FromArgb(0xAA, 0x0, 0x0),
                Color.FromArgb(0xAA, 0x0, 0xAA),
                Color.FromArgb(0xAA, 0x55, 0x0),
                Color.FromArgb(0xAA, 0xAA, 0xAA),
                Color.FromArgb(0x55, 0x55, 0x55),
                Color.FromArgb(0x55, 0x55, 0xFF),
                Color.FromArgb(0x55, 0xFF, 0x55),
                Color.FromArgb(0x55, 0xFF, 0xFF),
                Color.FromArgb(0xFF, 0x55, 0x55),
                Color.FromArgb(0xFF, 0x55, 0xFF),
                Color.FromArgb(0xFF, 0xFF, 0x55),
                Color.FromArgb(0xFF, 0xFF, 0xFF)};
            vidModeChangeFlag = System.Convert.ToInt32(+0b1000);

            this.useInternalTimer = useInternalTimer;

            //if (enableWebUI)
            //{
            //	wui = new WebUI(cpu, videoBMP, chars);
            //}

            for (uint i = 0x3D0; i <= 0x3DF; i++) // CGA
            {
                ValidPortAddress.Add(i);
            }
            ValidPortAddress.Add(0x3B8);

            for (int i = 0; i <= 255; i++)
            {
                if (i >= 32 && i < 255)
                {
                    chars[i] = Convert.ToChar(i);
                }
                else
                {
                    chars[i] = ' ';
                }
            }

            Reset();

            //VideoMode = VideoModes.Mode7_Text_BW_80x25
        }

        public void HandleKeyDown(object sender, KeyEventArgs e)
        {
            if (keyMap.GetScanCode(e.KeyValue) != 0)
            {
                base.OnKeyDown(this, e);
                if (e.Handled)
                {
                    return;
                }
                //Debug.WriteLine($"KEY DOWN: {e.KeyCode} | {e.Modifiers} | {e.KeyValue}")
                if (base.CPU.Keyboard != null)
                {
                    base.CPU.Sched.HandleInput(new ExternalInputEvent(base.CPU.Keyboard, e, false));
                }
            }
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        public void HandleKeyUp(object sender, KeyEventArgs e)
        {
            if (keyMap.GetScanCode(e.KeyValue) != 0)
            {
                base.OnKeyUp(this, e);
                if (e.Handled)
                {
                    return;
                }
                //Debug.WriteLine($"KEY UP: {e.KeyCode} | {e.Modifiers} | {e.KeyValue}")
                if (base.CPU.Keyboard != null)
                {
                    base.CPU.Sched.HandleInput(new ExternalInputEvent(base.CPU.Keyboard, e, true));
                }
            }
            e.Handled = true;
        }

        public void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (base.CPU.Mouse != null)
            {
                base.CPU.Sched.HandleInput(new ExternalInputEvent(base.CPU.Mouse, e, true));
            }
        }

        public void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (base.CPU.Mouse != null)
            {
                base.CPU.Sched.HandleInput(new ExternalInputEvent(base.CPU.Mouse, e, null));
            }
        }

        public void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (base.CPU.Mouse != null)
            {
                base.CPU.Sched.HandleInput(new ExternalInputEvent(base.CPU.Mouse, e, false));
            }
        }

        public int BlinkRate
        {
            get
            {
                return mBlinkRate;
            }
        }

        public bool BlinkCharOn
        {
            get
            {
                return mBlinkCharOn;
            }
        }

        public int CursorStart
        {
            get
            {
                return mCursorStart;
            }
        }

        public int CursorEnd
        {
            get
            {
                return mCursorEnd;
            }
        }

        public bool VideoEnabled
        {
            get
            {
                return mVideoEnabled;
            }
        }

        public int PixelsPerByte
        {
            get
            {
                return mPixelsPerByte;
            }
        }

        public override string Name
        {
            get
            {
                return "CGA";
            }
        }

        public override string Description
        {
            get
            {
                return "CGA (6845) Emulator";
            }
        }

        public override string Vendor
        {
            get
            {
                return "xFX JumpStart";
            }
        }

        public override int VersionMajor
        {
            get
            {
                return 0;
            }
        }

        public override int VersionMinor
        {
            get
            {
                return 4;
            }
        }

        public override int VersionRevision
        {
            get
            {
                return 7;
            }
        }

        public override Adapter.AdapterType Type
        {
            get
            {
                return AdapterType.Video;
            }
        }

        public int CursorCol
        {
            get
            {
                return mCursorCol;
            }
        }

        public int CursorRow
        {
            get
            {
                return mCursorRow;
            }
        }

        public override void InitiAdapter()
        {
            isInit = base.CPU != null;
            if (isInit && useInternalTimer)
            {
                Task.Run((Action)MainLoop);
            }
        }

        private void MainLoop()
        {
            int multiplier = System.Convert.ToInt32(base.CPU.VideoAdapter is CGAConsole ? 2 : 2);
            do
            {
                waiter.WaitOne((int)(multiplier * 1000 / VERTSYNC));
                Render();

                //RaiseEvent VideoRefreshed(Me)
            } while (!cancelAllThreads);
        }

        public override void Reset()
        {
            X8086.WordToBitsArray((ushort)(0x29), CGAModeControlRegister);
            X8086.WordToBitsArray((ushort)(0x0), CGAColorControlRegister);
            CRT6845DataRegister[0] = (byte)(0x71);
            CRT6845DataRegister[1] = (byte)(0x50);
            CRT6845DataRegister[2] = (byte)(0x5A);
            CRT6845DataRegister[3] = (byte)(0xA);
            CRT6845DataRegister[4] = (byte)(0x1F);
            CRT6845DataRegister[5] = (byte)(0x6);
            CRT6845DataRegister[6] = (byte)(0x19);
            CRT6845DataRegister[7] = (byte)(0x1C);
            CRT6845DataRegister[8] = (byte)(0x2);
            CRT6845DataRegister[9] = (byte)(0x7);
            CRT6845DataRegister[10] = (byte)(0x6);
            CRT6845DataRegister[11] = (byte)(0x71);
            for (int i = 12; i <= 32 - 1; i++)
            {
                CRT6845DataRegister[i] = (byte)0;
            }

            //HandleCGAModeControlRegisterUpdated()
            InitVideoMemory(false);
        }

        public bool CursorVisible
        {
            get
            {
                return mCursorVisible;
            }
        }

        public Point CursorLocation
        {
            get
            {
                return new Point(mCursorCol, mCursorRow);
            }
        }

        public override uint VideoMode
        {
            get
            {
                return mVideoMode;
            }
            set
            {
                mVideoMode = (uint)(value & (~0x80));

                mStartTextVideoAddress = 0xB8000;
                mStartGraphicsVideoAddress = 0xB8000;

                X8086.Notify($"CGA Video Mode: {(short)mVideoMode:X2}", X8086.NotificationReasons.Info);

                if (value == ((uint)VideoModes.Mode0_Text_BW_40x25))
                {
                    mTextResolution = new Size(40, 25);
                    mVideoResolution = new Size(0, 0);
                    mMainMode = MainModes.Text;
                }
                else if (value == ((uint)VideoModes.Mode1_Text_Color_40x25))
                {
                    mTextResolution = new Size(40, 25);
                    mVideoResolution = new Size(0, 0);
                    mMainMode = MainModes.Text;
                }
                else if (value == ((uint)VideoModes.Mode2_Text_BW_80x25))
                {
                    mTextResolution = new Size(80, 25);
                    mVideoResolution = new Size(0, 0);
                    mMainMode = MainModes.Text;
                }
                else if (value == ((uint)VideoModes.Mode3_Text_Color_80x25))
                {
                    mTextResolution = new Size(80, 25);
                    mVideoResolution = new Size(0, 0);
                    mMainMode = MainModes.Text;
                }
                else if (value == ((uint)VideoModes.Mode4_Graphic_Color_320x200))
                {
                    mTextResolution = new Size(40, 25);
                    mVideoResolution = new Size(320, 200);
                    mMainMode = MainModes.Graphics;
                }
                else if (value == ((uint)VideoModes.Mode5_Graphic_BW_320x200))
                {
                    mTextResolution = new Size(40, 25);
                    mVideoResolution = new Size(320, 200);
                    mMainMode = MainModes.Graphics;
                }
                else if (value == ((uint)VideoModes.Mode6_Graphic_Color_640x200))
                {
                    mTextResolution = new Size(80, 25);
                    mVideoResolution = new Size(640, 200);
                    mMainMode = MainModes.Graphics;
                }
                else if (value == ((uint)VideoModes.Mode6_Graphic_Color_640x200_Alt))
                {
                    mTextResolution = new Size(80, 25);
                    mVideoResolution = new Size(640, 200);
                    mMainMode = MainModes.Graphics;
                }
                else if (value == ((uint)VideoModes.Mode7_Text_BW_80x25))
                {
                    mStartTextVideoAddress = 0xB0000;
                    mStartGraphicsVideoAddress = 0xB0000;
                    mTextResolution = new Size(80, 25);
                    mVideoResolution = new Size(720, 400);
                    mMainMode = MainModes.Text;
                }
                else
                {
                    base.CPU.RaiseException("CGA: Unknown Video Mode " + value.ToString("X2"));
                    mVideoMode = (uint)VideoModes.Undefined;
                }

                InitVideoMemory(false);
            }
        }

        protected virtual void InitVideoMemory(bool clearScreen)
        {
            if (!isInit)
            {
                return;
            }

            mEndTextVideoAddress = mStartTextVideoAddress + 0x4000;
            mEndGraphicsVideoAddress = mStartGraphicsVideoAddress + 0x4000;

            if (VideoMode == ((uint)VideoModes.Mode6_Graphic_Color_640x200))
            {
                mPixelsPerByte = 8;
            }
            else if (VideoMode == ((uint)VideoModes.Mode6_Graphic_Color_640x200_Alt))
            {
                mPixelsPerByte = 1;
            }
            else
            {
                mPixelsPerByte = 4;
            }

            OnPaletteRegisterChanged();

            AutoSize();
        }

        public override ushort In(uint port)
        {
            if ((((port == ((uint)(0x3D0))) || (port == ((uint)(0x3D2)))) || (port == ((uint)(0x3D4)))) || (port == ((uint)(0x3D6)))) // CRT (6845) index register
            {
                return System.Convert.ToUInt16(CRT6845IndexRegister);
            } // CRT (6845) data register
            else if (((((port == ((uint)(0x3D1))) || (port == ((uint)(0x3D3)))) || (port == ((uint)(0x3D5)))) || (port == ((uint)(0x3D7)))) || (port == ((uint)(0x3B5))))
            {
                return System.Convert.ToUInt16(CRT6845DataRegister[CRT6845IndexRegister]);
            } // CGA mode control register  (except PCjr)
            else if (port == ((uint)(0x3D8)))
            {
                return X8086.BitsArrayToWord(CGAModeControlRegister);
            } // CGA palette register
            else if (port == ((uint)(0x3D9)))
            {
                return X8086.BitsArrayToWord(CGAPaletteRegister);
            } // CGA status register
            else if ((port == ((uint)(0x3DA))) || (port == ((uint)(0x3BA))))
            {
                UpdateStatusRegister();
                return X8086.BitsArrayToWord(CGAStatusRegister);
            } // CRT/CPU page register  (PCjr only)
            else if (port == ((uint)(0x3DF)))
            {
#if DEBUG
                //stop
#endif
            }
            else
            {
                base.CPU.RaiseException("CGA: Unknown In Port: " + port.ToString("X4"));
            }

            return (ushort)(0xFF);
        }

        public override void Out(uint port, ushort value)
        {
            if (port == ((uint)(0x3B8)))
            {
                if ((value & 2) == 2 && mVideoMode != (int)VideoModes.Mode7_Text_BW_80x25)
                {
                    VideoMode = (uint)VideoModes.Mode7_Text_BW_80x25;

                }
            } // CRT (6845) index register
            else if (((((((port == ((uint)(0x3D0))) || (port == ((uint)(0x3D2)))) || (port == ((uint)(0x3D4)))) || (port == ((uint)(0x3D6)))) || (port == +0x3B0)) || (port == ((uint)(0x3B2)))) || (port == ((uint)(0x3B4))))
            {
                CRT6845IndexRegister = (byte)(value & 31);
            } // CRT (6845) data register
            else if (((((((port == ((uint)(0x3D1))) || (port == ((uint)(0x3D3)))) || (port == ((uint)(0x3D5)))) || (port == ((uint)(0x3D7)))) || (port == +0x3B1)) || (port == ((uint)(0x3B3)))) || (port == ((uint)(0x3B5))))
            {
                CRT6845DataRegister[CRT6845IndexRegister] = (byte)(value & CtrlMask[CRT6845IndexRegister]);
                OnDataRegisterChanged();
            } // CGA mode control register  (except PCjr)
            else if ((port == ((uint)(0x3D8))) || (port == ((uint)(0x3B8))))
            {
                X8086.WordToBitsArray(value, CGAModeControlRegister);
                OnModeControlRegisterChanged();
            } // CGA palette register
            else if ((port == ((uint)(0x3D9))) || (port == ((uint)(0x3B9))))
            {
                X8086.WordToBitsArray(value, CGAPaletteRegister);
                OnPaletteRegisterChanged();
            } // CGA status register	EGA/VGA: input status 1 register / EGA/VGA feature control register
            else if ((port == ((uint)(0x3DA))) || (port == ((uint)(0x3BA))))
            {
                X8086.WordToBitsArray(value, CGAStatusRegister);
            } // The trigger is cleared by writing any value to port 03DBh (undocumented)
            else if (port == ((uint)(0x3DB)))
            {
                CGAStatusRegister[(int)CGAStatusRegisters.light_pen_trigger_set] = false;
            } // CRT/CPU page register  (PCjr only)
            else if (port == ((uint)(0x3DF)))
            {
                //Stop
            }
            else
            {
                base.CPU.RaiseException("CGA: Unknown Out Port: " + port.ToString("X4"));
            }
        }

        protected virtual void OnDataRegisterChanged()
        {
            mCursorVisible = (CRT6845DataRegister[0xA] & 0x60) != 0x20;

            if (mCursorVisible)
            {
                int startOffset = ((CRT6845DataRegister[0xC] & 0x3F) << 8) | (CRT6845DataRegister[0xD] & 0xFF);
                int p = ((CRT6845DataRegister[0xE] & 0x3F) << 8) | (CRT6845DataRegister[0xF] & 0xFF);
                //int startOffset = System.Convert.ToInt32(((CRT6845DataRegister[0xC] & 0x3F) << 8) || (CRT6845DataRegister[0xD] & 0xFF));
                //int p = System.Convert.ToInt32(((CRT6845DataRegister[0xE] & 0x3F) << 8) || (CRT6845DataRegister[0xF] & 0xFF));
                p = System.Convert.ToInt32((p - startOffset) & 0x1FFF);

                if (p < 0)
                {
                    mCursorCol = 0;
                    mCursorRow = 50;
                }
                else
                {
                    mCursorCol = p % mTextResolution.Width;
                    mCursorRow = p / mTextResolution.Width;
                }
            }

            mCursorStart = System.Convert.ToInt32(CRT6845DataRegister[0xA] & +0b11111);
            mCursorEnd = System.Convert.ToInt32(CRT6845DataRegister[0xB] & +0b11111);

            mBlinkCharOn = CGAModeControlRegister[(int)CGAModeControlRegisters.blink_enabled];
        }

        protected virtual void OnModeControlRegisterChanged()
        {
            // http://www.seasip.info/VintagePC/cga.html
            uint v = System.Convert.ToUInt32(X8086.BitsArrayToWord(CGAModeControlRegister));
            VideoModes newMode = (VideoModes)(v & 0x17); // 10111

            if ((v & vidModeChangeFlag) != 0 && (int)newMode != mVideoMode)
            {
                VideoMode = (uint)newMode;
            }

            mVideoEnabled = CGAModeControlRegister[(int)CGAModeControlRegisters.video_enabled] || VideoMode == (int)VideoModes.Mode7_Text_BW_80x25;
        }

        protected virtual void OnPaletteRegisterChanged()
        {
            if (MainMode == MainModes.Text)
            {
                CGAPalette = (Color[])(CGABasePalette.Clone());
            }
            else
            {
                Color[] colors = null;
                uint cgaModeReg = System.Convert.ToUInt32(X8086.BitsArrayToWord(CGAModeControlRegister));
                uint cgaColorReg = System.Convert.ToUInt32(X8086.BitsArrayToWord(CGAPaletteRegister));

                if (VideoMode == ((uint)VideoModes.Mode4_Graphic_Color_320x200))
                {
                    int intense = System.Convert.ToInt32((cgaColorReg & 0x10) >> 1);
                    int pal1 = System.Convert.ToInt32((cgaColorReg >> 5) & (~(cgaModeReg >> 2)) & 1);
                    int pal2 = System.Convert.ToInt32(((~cgaColorReg) >> 5) & (~(cgaModeReg >> 2)) & 1);

                    colors = new Color[] {
                                CGABasePalette[cgaColorReg & 0xF],
                                CGABasePalette[3 ^ pal2 | intense],
                                CGABasePalette[4 ^ pal1 | intense],
                                CGABasePalette[7 ^ pal2 | intense]
                            };
                }
                else if (VideoMode == ((uint)VideoModes.Mode6_Graphic_Color_640x200))
                {
                    colors = new Color[] {
                                CGABasePalette[0],
                                CGABasePalette[cgaColorReg & 0xF]
                            };
                }

                if (colors != null)
                {
                    for (int i = 0; i <= colors.Length - 1; i++)
                    {
                        CGAPalette[i] = colors[i];
                    }
                }
            }
        }

        private void UpdateStatusRegister()
        {
            // Determine current retrace state
            long t = base.CPU.Sched.CurrentTime;

            //bool vRetrace = decimal.Compare(decimal.Remainder(new decimal(t), new decimal(19848334L)), new decimal(1984833L)) <= 0;
            //bool hRetrace = decimal.Compare(decimal.Remainder(new decimal(t), new decimal(75757L)), new decimal(7575L)) <= 0;
            bool vRetrace = (t % (long)vt) <= ((long)vt / 10);
            bool hRetrace = (t % (long)ht) <= ((long)ht / 10);

            CGAStatusRegister[(int)CGAStatusRegisters.display_enable] = hRetrace;
            CGAStatusRegister[(int)CGAStatusRegisters.vertical_retrace] = vRetrace;
        }

        public override double Zoom
        {
            get
            {
                return mZoom;
            }
            set
            {
                mZoom = value;
                AutoSize();
            }
        }

        public override void CloseAdapter()
        {
            isInit = false;
            cancelAllThreads = true;
            //wui?.Close();

            Application.DoEvents();
        }
    }
}
