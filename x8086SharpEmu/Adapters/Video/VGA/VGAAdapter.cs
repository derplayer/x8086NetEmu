//using System.Collections.Generic;
//using System;
//using System.Linq;
//using System.Drawing;
//using System.Diagnostics;
//using System.Xml.Linq;
//using System.Collections;
//using System.Windows.Forms;

//using x8086SharpEmu;

//namespace x8086SharpEmu
//{
//	// http://www.osdever.net/FreeVGA/home.htm
//	// https://pdos.csail.mit.edu/6.828/2007/readings/hardware/vgadoc/VGABIOS.TXT
//	// http://stanislavs.org/helppc/ports.html

//	public abstract class VGAAdapter : CGAAdapter
//	{

//		private readonly Color[] VGABasePalette = new Color[] {
//				Color.FromArgb(0, 0, 0),
//				Color.FromArgb(0, 0, 169),
//				Color.FromArgb(0, 169, 0),
//				Color.FromArgb(0, 169, 169),
//				Color.FromArgb(169, 0, 0),
//				Color.FromArgb(169, 0, 169),
//				Color.FromArgb(169, 169, 0),
//				Color.FromArgb(169, 169, 169),
//				Color.FromArgb(0, 0, 84),
//				Color.FromArgb(0, 0, 255),
//				Color.FromArgb(0, 169, 84),
//				Color.FromArgb(0, 169, 255),
//				Color.FromArgb(169, 0, 84),
//				Color.FromArgb(169, 0, 255),
//				Color.FromArgb(169, 169, 84),
//				Color.FromArgb(169, 169, 255),
//				Color.FromArgb(0, 84, 0),
//				Color.FromArgb(0, 84, 169),
//				Color.FromArgb(0, 255, 0),
//				Color.FromArgb(0, 255, 169),
//				Color.FromArgb(169, 84, 0),
//				Color.FromArgb(169, 84, 169),
//				Color.FromArgb(169, 255, 0),
//				Color.FromArgb(169, 255, 169),
//				Color.FromArgb(0, 84, 84),
//				Color.FromArgb(0, 84, 255),
//				Color.FromArgb(0, 255, 84),
//				Color.FromArgb(0, 255, 255),
//				Color.FromArgb(169, 84, 84),
//				Color.FromArgb(169, 84, 255),
//				Color.FromArgb(169, 255, 84),
//				Color.FromArgb(169, 255, 255),
//				Color.FromArgb(84, 0, 0),
//				Color.FromArgb(84, 0, 169),
//				Color.FromArgb(84, 169, 0),
//				Color.FromArgb(84, 169, 169),
//				Color.FromArgb(255, 0, 0),
//				Color.FromArgb(255, 0, 169),
//				Color.FromArgb(255, 169, 0),
//				Color.FromArgb(255, 169, 169),
//				Color.FromArgb(84, 0, 84),
//				Color.FromArgb(84, 0, 255),
//				Color.FromArgb(84, 169, 84),
//				Color.FromArgb(84, 169, 255),
//				Color.FromArgb(255, 0, 84),
//				Color.FromArgb(255, 0, 255),
//				Color.FromArgb(255, 169, 84),
//				Color.FromArgb(255, 169, 255),
//				Color.FromArgb(84, 84, 0),
//				Color.FromArgb(84, 84, 169),
//				Color.FromArgb(84, 255, 0),
//				Color.FromArgb(84, 255, 169),
//				Color.FromArgb(255, 84, 0),
//				Color.FromArgb(255, 84, 169),
//				Color.FromArgb(255, 255, 0),
//				Color.FromArgb(255, 255, 169),
//				Color.FromArgb(84, 84, 84),
//				Color.FromArgb(84, 84, 255),
//				Color.FromArgb(84, 255, 84),
//				Color.FromArgb(84, 255, 255),
//				Color.FromArgb(255, 84, 84),
//				Color.FromArgb(255, 84, 255),
//				Color.FromArgb(255, 255, 84),
//				Color.FromArgb(255, 255, 255),
//				Color.FromArgb(255, 125, 125),
//				Color.FromArgb(255, 157, 125),
//				Color.FromArgb(255, 190, 125),
//				Color.FromArgb(255, 222, 125),
//				Color.FromArgb(255, 255, 125),
//				Color.FromArgb(222, 255, 125),
//				Color.FromArgb(190, 255, 125),
//				Color.FromArgb(157, 255, 125),
//				Color.FromArgb(125, 255, 125),
//				Color.FromArgb(125, 255, 157),
//				Color.FromArgb(125, 255, 190),
//				Color.FromArgb(125, 255, 222),
//				Color.FromArgb(125, 255, 255),
//				Color.FromArgb(125, 222, 255),
//				Color.FromArgb(125, 190, 255),
//				Color.FromArgb(125, 157, 255),
//				Color.FromArgb(182, 182, 255),
//				Color.FromArgb(198, 182, 255),
//				Color.FromArgb(218, 182, 255),
//				Color.FromArgb(234, 182, 255),
//				Color.FromArgb(255, 182, 255),
//				Color.FromArgb(255, 182, 234),
//				Color.FromArgb(255, 182, 218),
//				Color.FromArgb(255, 182, 198),
//				Color.FromArgb(255, 182, 182),
//				Color.FromArgb(255, 198, 182),
//				Color.FromArgb(255, 218, 182),
//				Color.FromArgb(255, 234, 182),
//				Color.FromArgb(255, 255, 182),
//				Color.FromArgb(234, 255, 182),
//				Color.FromArgb(218, 255, 182),
//				Color.FromArgb(198, 255, 182),
//				Color.FromArgb(182, 255, 182),
//				Color.FromArgb(182, 255, 198),
//				Color.FromArgb(182, 255, 218),
//				Color.FromArgb(182, 255, 234),
//				Color.FromArgb(182, 255, 255),
//				Color.FromArgb(182, 234, 255),
//				Color.FromArgb(182, 218, 255),
//				Color.FromArgb(182, 198, 255),
//				Color.FromArgb(0, 0, 113),
//				Color.FromArgb(28, 0, 113),
//				Color.FromArgb(56, 0, 113),
//				Color.FromArgb(84, 0, 113),
//				Color.FromArgb(113, 0, 113),
//				Color.FromArgb(113, 0, 84),
//				Color.FromArgb(113, 0, 56),
//				Color.FromArgb(113, 0, 28),
//				Color.FromArgb(113, 0, 0),
//				Color.FromArgb(113, 28, 0),
//				Color.FromArgb(113, 56, 0),
//				Color.FromArgb(113, 84, 0),
//				Color.FromArgb(113, 113, 0),
//				Color.FromArgb(84, 113, 0),
//				Color.FromArgb(56, 113, 0),
//				Color.FromArgb(28, 113, 0),
//				Color.FromArgb(0, 113, 0),
//				Color.FromArgb(0, 113, 28),
//				Color.FromArgb(0, 113, 56),
//				Color.FromArgb(0, 113, 84),
//				Color.FromArgb(0, 113, 113),
//				Color.FromArgb(0, 84, 113),
//				Color.FromArgb(0, 56, 113),
//				Color.FromArgb(0, 28, 113),
//				Color.FromArgb(56, 56, 113),
//				Color.FromArgb(68, 56, 113),
//				Color.FromArgb(84, 56, 113),
//				Color.FromArgb(97, 56, 113),
//				Color.FromArgb(113, 56, 113),
//				Color.FromArgb(113, 56, 97),
//				Color.FromArgb(113, 56, 84),
//				Color.FromArgb(113, 56, 68),
//				Color.FromArgb(113, 56, 56),
//				Color.FromArgb(113, 68, 56),
//				Color.FromArgb(113, 84, 56),
//				Color.FromArgb(113, 97, 56),
//				Color.FromArgb(113, 113, 56),
//				Color.FromArgb(97, 113, 56),
//				Color.FromArgb(84, 113, 56),
//				Color.FromArgb(68, 113, 56),
//				Color.FromArgb(56, 113, 56),
//				Color.FromArgb(56, 113, 68),
//				Color.FromArgb(56, 113, 84),
//				Color.FromArgb(56, 113, 97),
//				Color.FromArgb(56, 113, 113),
//				Color.FromArgb(56, 97, 113),
//				Color.FromArgb(56, 84, 113),
//				Color.FromArgb(56, 68, 113),
//				Color.FromArgb(80, 80, 113),
//				Color.FromArgb(89, 80, 113),
//				Color.FromArgb(97, 80, 113),
//				Color.FromArgb(105, 80, 113),
//				Color.FromArgb(113, 80, 113),
//				Color.FromArgb(113, 80, 105),
//				Color.FromArgb(113, 80, 97),
//				Color.FromArgb(113, 80, 89),
//				Color.FromArgb(113, 80, 80),
//				Color.FromArgb(113, 89, 80),
//				Color.FromArgb(113, 97, 80),
//				Color.FromArgb(113, 105, 80),
//				Color.FromArgb(113, 113, 80),
//				Color.FromArgb(105, 113, 80),
//				Color.FromArgb(97, 113, 80),
//				Color.FromArgb(89, 113, 80),
//				Color.FromArgb(80, 113, 80),
//				Color.FromArgb(80, 113, 89),
//				Color.FromArgb(80, 113, 97),
//				Color.FromArgb(80, 113, 105),
//				Color.FromArgb(80, 113, 113),
//				Color.FromArgb(80, 105, 113),
//				Color.FromArgb(80, 97, 113),
//				Color.FromArgb(80, 89, 113),
//				Color.FromArgb(0, 0, 64),
//				Color.FromArgb(16, 0, 64),
//				Color.FromArgb(32, 0, 64),
//				Color.FromArgb(48, 0, 64),
//				Color.FromArgb(64, 0, 64),
//				Color.FromArgb(64, 0, 48),
//				Color.FromArgb(64, 0, 32),
//				Color.FromArgb(64, 0, 16),
//				Color.FromArgb(64, 0, 0),
//				Color.FromArgb(64, 16, 0),
//				Color.FromArgb(64, 32, 0),
//				Color.FromArgb(64, 48, 0),
//				Color.FromArgb(64, 64, 0),
//				Color.FromArgb(48, 64, 0),
//				Color.FromArgb(32, 64, 0),
//				Color.FromArgb(16, 64, 0),
//				Color.FromArgb(0, 64, 0),
//				Color.FromArgb(0, 64, 16),
//				Color.FromArgb(0, 64, 32),
//				Color.FromArgb(0, 64, 48),
//				Color.FromArgb(0, 64, 64),
//				Color.FromArgb(0, 48, 64),
//				Color.FromArgb(0, 32, 64),
//				Color.FromArgb(0, 16, 64),
//				Color.FromArgb(32, 32, 64),
//				Color.FromArgb(40, 32, 64),
//				Color.FromArgb(48, 32, 64),
//				Color.FromArgb(56, 32, 64),
//				Color.FromArgb(64, 32, 64),
//				Color.FromArgb(64, 32, 56),
//				Color.FromArgb(64, 32, 48),
//				Color.FromArgb(64, 32, 40),
//				Color.FromArgb(64, 32, 32),
//				Color.FromArgb(64, 40, 32),
//				Color.FromArgb(64, 48, 32),
//				Color.FromArgb(64, 56, 32),
//				Color.FromArgb(64, 64, 32),
//				Color.FromArgb(56, 64, 32),
//				Color.FromArgb(48, 64, 32),
//				Color.FromArgb(40, 64, 32),
//				Color.FromArgb(32, 64, 32),
//				Color.FromArgb(32, 64, 40),
//				Color.FromArgb(32, 64, 48),
//				Color.FromArgb(32, 64, 56),
//				Color.FromArgb(32, 64, 64),
//				Color.FromArgb(32, 56, 64),
//				Color.FromArgb(32, 48, 64),
//				Color.FromArgb(32, 40, 64),
//				Color.FromArgb(44, 44, 64),
//				Color.FromArgb(48, 44, 64),
//				Color.FromArgb(52, 44, 64),
//				Color.FromArgb(60, 44, 64),
//				Color.FromArgb(64, 44, 64),
//				Color.FromArgb(64, 44, 60),
//				Color.FromArgb(64, 44, 52),
//				Color.FromArgb(64, 44, 48),
//				Color.FromArgb(64, 44, 44),
//				Color.FromArgb(64, 48, 44),
//				Color.FromArgb(64, 52, 44),
//				Color.FromArgb(64, 60, 44),
//				Color.FromArgb(64, 64, 44),
//				Color.FromArgb(60, 64, 44),
//				Color.FromArgb(52, 64, 44),
//				Color.FromArgb(48, 64, 44),
//				Color.FromArgb(44, 64, 44),
//				Color.FromArgb(44, 64, 48),
//				Color.FromArgb(44, 64, 52),
//				Color.FromArgb(44, 64, 60),
//				Color.FromArgb(44, 64, 64),
//				Color.FromArgb(44, 60, 64),
//				Color.FromArgb(44, 52, 64),
//				Color.FromArgb(44, 48, 64),
//				Color.FromArgb(0, 0, 0),
//				Color.FromArgb(0, 0, 0),
//				Color.FromArgb(0, 0, 0),
//				Color.FromArgb(0, 0, 0),
//				Color.FromArgb(0, 0, 0),
//				Color.FromArgb(0, 0, 0),
//				Color.FromArgb(0, 0, 0),
//				Color.FromArgb(0, 0, 0)};

//			protected byte[] VGA_SC = new byte[0xFF];
//			protected byte[] VGA_ATTR = new byte[0xFF];
//			protected byte[] VGA_CRTC = new byte[0xFF];
//			private readonly byte[] VGA_GC = new byte[0xFF];
//			private readonly byte[] VGA_Latch = new byte[4];

//			private bool flip3C0 = false;
//			private byte stateDAC;
//			private int latchReadRGB;
//			private int latchReadPal;
//			private int latchWriteRGB;
//			private int latchWritePal;

//			protected byte[] portRAM = new byte[0xFFF];
//			protected Color[] vgaPalette; 
//			private bool mUseVRAM;

//			protected const uint MEMSIZE = 0x100000U;
//			protected byte[] vRAM = new byte[MEMSIZE];

//			private const uint planeSize = 0x10000;
//			private uint tmpRGB;
//			private byte tmpVal;
//			private uint ramOffset;

//			private const bool useROM = true;

//			private X8086 mCPU;

//			// Video Modes: http://www.columbia.edu/~em36/wpdos/videomodes.txt
//			//              http://webpages.charter.net/danrollins/techhelp/0114.HTM
//			// Ports: http://stanislavs.org/helppc/ports.html

//			public VGAAdapter(X8086 cpu, bool useInternalTimer = true, bool enableWebUI = false) : base(cpu, useInternalTimer, enableWebUI)
//			{
//				
//				vgaPalette = new Color[VGABasePalette.Length];

//				mCPU = cpu;

//				base.vidModeChangeFlag = 0; // Prevent the CGA adapter from changing video modes

//				if (useROM)
//				{
//					mCPU.LoadBIN("roms\\ET4000(1-10-92).BIN", (ushort) (0xC000), (ushort) (0x0));
//				}
//				//If useROM Then mCPU.LoadBIN("..\Other Emulators & Resources\fake86-0.12.9.19-win32\Binaries\videorom.bin", &HC000, &H0)
//				//If useROM Then mCPU.LoadBIN("..\Other Emulators & Resources\PCemV0.7\roms\TRIDENT.BIN", &HC000, &H0)
//				//If useROM Then mCPU.LoadBIN("roms\ET4000(4-7-93).BIN", &HC000, &H0)

//				ValidPortAddress.Clear();
//				for (uint i = 0x3C0; i <= 0x3CF; i++) // EGA/VGA
//				{
//					ValidPortAddress.Add(i);
//				}
//				ValidPortAddress.Add(0x3DA);

//				for (int i = 0; i <= VGABasePalette.Length - 1; i++)
//				{
//					vgaPalette[i] = VGABasePalette[i];
//				}

//				mCPU.TryAttachHook(new X8086.MemHandler((uint address, ushort value, X8086.MemHookMode mode)=>
//				{
//					switch (mMainMode)
//					{
//						case MainModes.Text:
//							if (address >= mStartTextVideoAddress && address < mEndTextVideoAddress)
//							{
//								switch (mode)
//								{
//									case X8086.MemHookMode.Read:
//										value = (ushort)(VideoRAM(address - mStartTextVideoAddress));
//									case X8086.MemHookMode.Write:
//										VideoRAM[address - mStartTextVideoAddress] = value;
//								}
//								return true;
//							}
//							return false;
//						case MainModes.Graphics:
//							if (address >= mStartGraphicsVideoAddress && address < mEndGraphicsVideoAddress)
//							{
//								switch (mode)
//								{
//									case X8086.MemHookMode.Read:
//										value = (ushort)(VideoRAM(address - mStartGraphicsVideoAddress));
//									case X8086.MemHookMode.Write:
//										VideoRAM[address - mStartGraphicsVideoAddress] = value;
//								}
//								return true;
//							}
//							return false;
//					}
//					return false;
//				}));

//				mCPU.TryAttachHook((byte) (0x10), new X8086.IntHandler(()=>
//				{
//					switch (mCPU.Registers.AH)
//					{
//						case 0x0:
//							VideoMode = mCPU.Registers.AL;
//							VGA_SC[4] = 0;
//							return useROM; // When using ROM, prevent the BIOS from handling this function
//						case 0x10:
//							switch (mCPU.Registers.AL)
//							{
//								case 0x10: // Set individual DAC register
//									vgaPalette[mCPU.Registers.BX % 256] = Color.FromArgb(RGBToUInt((uint)(mCPU.Registers.DH & 0x3F) << 2, (uint)(mCPU.Registers.CH & 0x3F) << 2, (uint)(mCPU.Registers.CL & 0x3F) << 2));
//								case 0x12: // Set block of DAC registers
//									int addr = (int)(((uint)(mCPU.Registers.ES)) * 16U + mCPU.Registers.DX);
//									for (int n = mCPU.Registers.BX; n <= (int)(mCPU.Registers.BX + mCPU.Registers.CX) - 1; n++)
//									{
//										vgaPalette[n] = Color.FromArgb(RGBToUInt(mCPU.Memory(addr + 0) << 2,
//										mCPU.Memory(addr + 1) << 2,
//										mCPU.Memory(addr + 2) << 2));
//										addr += 3;
//									}
//							}
//						case 0x1A:
//							mCPU.Registers.AL = 0x1A; // http://stanislavs.org/helppc/int_10-1a.html
//							mCPU.Registers.BL = 0x8;
//							return true;
//					}

//					return false;
//				}));
//			}

//			public bool UseVRAM
//			{
//				get
//				{
//					return mUseVRAM;
//				}
//			}

//			public byte get_VideoRAM(ushort address)
//			{
//				if (!mUseVRAM)
//				{
//					return mCPU.Memory[address + ramOffset];
//				}
//				else if ((VGA_SC[4] & 6) == 0 && mVideoMode != 0xD && mVideoMode != 0x10 && mVideoMode != 0x12)
//				{
//					return mCPU.Memory[address + ramOffset];
//				}
//				else
//				{
//					return Read((uint)(address));
//				}
//			}
//			public void set_VideoRAM(ushort address, byte value)
//			{
//				if (!mUseVRAM)
//				{
//					mCPU.Memory[address + ramOffset] = value;
//				}
//				else if ((VGA_SC[4] & 6) == 0 && mVideoMode != 0xD && mVideoMode != 0x10 && mVideoMode != 0x12)
//				{
//					mCPU.Memory[address + ramOffset] = value;
//				}
//				else
//				{
//					Write((uint)(address), (ushort)(value));
//				}
//			}

//			public override uint VideoMode
//			{
//				get
//				{
//					return mVideoMode;
//				}
//				set
//				{
//					// Mode is in AH
//					if (value >> 8 == ((uint) 0)) // Set video mode
//					{
//						value = value & 0x7F; // http://stanislavs.org/helppc/ports.html
//						mVideoMode = value;
//						X8086.Notify("VGA Video Mode: {CShort(mVideoMode):X2}", X8086.NotificationReasons.Info);

//						if (mVideoMode == ((uint) 0)) // 40x25 Mono Text
//						{
//							mStartTextVideoAddress = 0xB8000;
//							mStartGraphicsVideoAddress = 0xB8000;
//							mTextResolution = new Size(40, 25);
//							mVideoResolution = new Size(360, 400);
//							mCellSize = new Size(9, 16);
//							mMainMode = MainModes.Text;
//							mPixelsPerByte = 4;
//							mUseVRAM = false;
//						} // 40x25 Color Text
//						else if (mVideoMode == ((uint) 1))
//						{
//							mStartTextVideoAddress = 0xB8000;
//							mStartGraphicsVideoAddress = 0xB8000;
//							mTextResolution = new Size(40, 25);
//							mVideoResolution = new Size(360, 400);
//							mCellSize = new Size(9, 16);
//							mMainMode = MainModes.Text;
//							mPixelsPerByte = 4;
//							mUseVRAM = false;
//							portRAM[0x3D8] = (byte)(portRAM[0x3D8] & 0xFE);
//						} // 80x25 Mono Text
//						else if (mVideoMode == ((uint) 2))
//						{
//							mStartTextVideoAddress = 0xB8000;
//							mStartGraphicsVideoAddress = 0xB8000;
//							mTextResolution = new Size(80, 25);
//							mVideoResolution = new Size(640, 400);
//							mCellSize = new Size(9, 16);
//							mMainMode = MainModes.Graphics;
//							mPixelsPerByte = 4;
//							mUseVRAM = false;
//							portRAM[0x3D8] = (byte)(portRAM[0x3D8] & 0xFE);
//						} // 80x25 Color Text
//						else if (mVideoMode == ((uint) 3))
//						{
//							mStartTextVideoAddress = 0xB8000;
//							mStartGraphicsVideoAddress = 0xB8000;
//							mTextResolution = new Size(80, 25);
//							mVideoResolution = new Size(720, 400);
//							mCellSize = new Size(9, 16);
//							mMainMode = MainModes.Text;
//							mPixelsPerByte = 4;
//							mUseVRAM = false;
//							portRAM[0x3D8] = (byte)(portRAM[0x3D8] & 0xFE);
//						} // 320x200 4 Colors
//						else if ((mVideoMode == ((uint) 4)) || (mVideoMode == ((uint) 5)))
//						{
//							mStartTextVideoAddress = 0xB8000;
//							mStartGraphicsVideoAddress = 0xB8000;
//							mTextResolution = new Size(40, 25);
//							mVideoResolution = new Size(320, 200);
//							mCellSize = new Size(8, 8);
//							mMainMode = MainModes.Graphics;
//							mPixelsPerByte = 4;
//							mUseVRAM = false;
//							//portRAM(&H3D9) = If(value And &HF = 4, 48, 0)
//							if (mCPU.Registers.AL == 4)
//							{
//								portRAM[0x3D9] = (byte) 48;
//							}
//							else
//							{
//								portRAM[0x3D9] = (byte) 0;
//							}
//						} // 640x200 2 Colors
//						else if (mVideoMode == ((uint) 6))
//						{
//							mStartTextVideoAddress = 0xB8000;
//							mStartGraphicsVideoAddress = 0xB8000;
//							mTextResolution = new Size(80, 25);
//							mVideoResolution = new Size(640, 200);
//							mCellSize = new Size(8, 8);
//							mMainMode = MainModes.Graphics;
//							mPixelsPerByte = 2;
//							mUseVRAM = false;
//							portRAM[0x3D8] = (byte)(portRAM[0x3D8] & 0xFE);
//						} // 640x200 2 Colors
//						else if (mVideoMode == ((uint) 7))
//						{
//							mStartTextVideoAddress = 0xB0000;
//							mStartGraphicsVideoAddress = 0xB0000;
//							mTextResolution = new Size(80, 25);
//							mVideoResolution = new Size(720, 400);
//							mCellSize = new Size(9, 16);
//							mMainMode = MainModes.Text;
//							mPixelsPerByte = 1;
//							mUseVRAM = false;
//						} // 320x200 16 Colors
//						else if (mVideoMode == ((uint) 9))
//						{
//							mStartTextVideoAddress = 0xB8000;
//							mStartGraphicsVideoAddress = 0xB8000;
//							mTextResolution = new Size(40, 25);
//							mVideoResolution = new Size(320, 200);
//							mCellSize = new Size(8, 8);
//							mMainMode = MainModes.Graphics;
//							mPixelsPerByte = 4;
//							mUseVRAM = false;
//							portRAM[0x3D8] = (byte)(portRAM[0x3D8] & 0xFE);
//						} // 320x200 16 Colors
//						else if (mVideoMode == ((uint) (0xD)))
//						{
//							mStartTextVideoAddress = 0xA0000;
//							mStartGraphicsVideoAddress = 0xA0000;
//							mTextResolution = new Size(40, 25);
//							mVideoResolution = new Size(320, 200);
//							mCellSize = new Size(8, 8);
//							mMainMode = MainModes.Graphics;
//							mPixelsPerByte = 4;
//							mUseVRAM = true;
//							portRAM[0x3D8] = (byte)(portRAM[0x3D8] & 0xFE);
//						} // 640x200 16 Colors
//						else if (mVideoMode == ((uint) (0xE)))
//						{
//							mStartTextVideoAddress = 0xA0000;
//							mStartGraphicsVideoAddress = 0xA0000;
//							mTextResolution = new Size(80, 25);
//							mVideoResolution = new Size(640, 200);
//							mCellSize = new Size(8, 8);
//							mMainMode = MainModes.Graphics;
//							mPixelsPerByte = 4;
//							mUseVRAM = true;
//						} // 640x350 4 Colors
//						else if (mVideoMode == ((uint) (0x10)))
//						{
//							mStartTextVideoAddress = 0xA0000;
//							mStartGraphicsVideoAddress = 0xA0000;
//							mTextResolution = new Size(80, 25);
//							mVideoResolution = new Size(640, 350);
//							mCellSize = new Size(8, 14);
//							mMainMode = MainModes.Graphics;
//							mPixelsPerByte = 4;
//							mUseVRAM = true;
//						}
//						else if (mVideoMode == ((uint) (0x12)))
//						{
//							mStartTextVideoAddress = 0xA0000;
//							mStartGraphicsVideoAddress = 0xA0000;
//							mTextResolution = new Size(80, 30);
//							mVideoResolution = new Size(640, 480);
//							mCellSize = new Size(8, 16);
//							mMainMode = MainModes.Graphics;
//							mPixelsPerByte = 4;
//							mUseVRAM = true;
//							portRAM[0x3D8] = (byte)(portRAM[0x3D8] & 0xFE);
//						}
//						else if (mVideoMode == ((uint) (0x13)))
//						{
//							mStartTextVideoAddress = 0xA0000;
//							mStartGraphicsVideoAddress = 0xA0000;
//							mTextResolution = new Size(40, 25);
//							mVideoResolution = new Size(320, 200);
//							mCellSize = new Size(8, 8);
//							mMainMode = MainModes.Graphics;
//							mPixelsPerByte = 4;
//							mUseVRAM = true;
//							portRAM[0x3D8] = (byte)(portRAM[0x3D8] & 0xFE);
//						} // 90x25 Mono Text
//						else if (mVideoMode == ((uint) 127))
//						{
//							mStartTextVideoAddress = 0xB0000;
//							mStartGraphicsVideoAddress = 0xB0000;
//							mTextResolution = new Size(80, 25);
//							mVideoResolution = new Size(720, 400);
//							mCellSize = new Size(8, 16);
//							mMainMode = MainModes.Text;
//							mPixelsPerByte = 1;
//							portRAM[0x3D8] = (byte)(portRAM[0x3D8] & 0xFE);

//							//Case &H30 ' 800x600 Color Tseng ET3000/4000 chipset
//							//    mStartTextVideoAddress = &HA0000
//							//    mStartGraphicsVideoAddress = &HA0000
//							//    mTextResolution = New Size(100, 37)
//							//    mVideoResolution = New Size(800, 600)
//							//    mCellSize = New Size(8, 8)
//							//    mMainMode = MainModes.Graphics
//							//    mPixelsPerByte = 4
//							//    portRAM(&H3D8) = portRAM(&H3D8) And &HFE
//						}
//						else
//						{
//							//mStartTextVideoAddress = &HB0000
//							//mStartGraphicsVideoAddress = &HB0000
//							//mTextResolution = New Size(132, 25)
//							//mVideoResolution = New Size(640, 200)
//							//mCellSize = New Size(8, 8)
//							//mMainMode = MainModes.Text
//							//mPixelsPerByte = 1
//							//mUseVRAM = False
//						}

//						mCellSize = new Size(8, 16); // Temporary hack until we can stretch the fonts' bitmaps
//						mCPU.Memory[0x449] = mVideoMode;
//						mCPU.Memory[0x44A] = (byte) mTextResolution.Width;
//						mCPU.Memory[0x44B] = (byte) 0;
//						mCPU.Memory[0x484] = (byte) (mTextResolution.Height - 1);
//						//mCPU.Memory(&H463) = &H3D4 ' With and without a BIOS INT 10,8/9/10 fails to work

//						InitVideoMemory(false);
//					}
//				}
//			}

//			private ushort RGBToUInt(ushort r, ushort g, ushort b)
//			{
//				return r | (g << 8) | (b << 16);
//			}

//			public override ushort In(uint port)
//			{
//				if (port == ((uint) (0x3C1)))
//				{
//					return (ushort)(VGA_ATTR[portRAM[0x3C0]]);
//				}
//				else if (port == ((uint) (0x3C5)))
//				{
//					return (ushort)(VGA_SC[portRAM[0x3C4]]);
//				}
//				else if (port == ((uint) (0x3D5)))
//				{
//					return (ushort)(VGA_CRTC[portRAM[0x3D4]]);
//				}
//				else if (port == ((uint) (0x3C7)))
//				{
//					return (ushort)(stateDAC);
//				}
//				else if (port == ((uint) (0x3C8)))
//				{
//					return (ushort)  latchReadPal;
//				}
//				else if (port == ((uint) (0x3C9)))
//				{
//					switch (latchReadRGB)
//					{
//						case 0: // B
//							tmpRGB = (uint)(vgaPalette[latchReadPal].ToArgb() >> 2);
//							break;
//						case 1: // G
//							tmpRGB = (uint)(vgaPalette[latchReadPal].ToArgb() >> 10);
//							break;
//						case 2: // R
//							tmpRGB = (uint)(vgaPalette[latchReadPal].ToArgb() >> 18);
//							latchReadPal++;
//							latchReadRGB = -1;
//							break;
//					}
//					latchReadRGB++;
//					return tmpRGB & 0x3F;
//				}
//				else if (port == ((uint) (0x3DA)))
//				{
//					flip3C0 = true; // https://wiki.osdev.org/VGA_Hardware#Port_0x3C0
//					return base.In(port);
//				}

//				return (ushort)(portRAM[port]);
//			}

//			public override void Out(uint port, ushort value)
//			{
//				if (port == ((uint) (0x3C0))) // https://wiki.osdev.org/VGA_Hardware#Port_0x3C0
//				{
//					if (flip3C0)
//					{
//						portRAM[port] = value;
//					}
//					else
//					{
//						VGA_ATTR[portRAM[port]] = value;
//					}
//					flip3C0 = !flip3C0;
//				} // Sequence controller index
//				else if (port == ((uint) (0x3C4)))
//				{
//					portRAM[port] = value;
//				} // Sequence controller data
//				else if (port == ((uint) (0x3C5)))
//				{
//					VGA_SC[portRAM[0x3C4]] = value;
//				} // Color index register (read operations)
//				else if (port == ((uint) (0x3C7)))
//				{
//					latchReadPal = value;
//					latchReadRGB = 0;
//					stateDAC = (byte) 0;
//				} // Color index register (write operations)
//				else if (port == ((uint) (0x3C8)))
//				{
//					latchWritePal = value;
//					latchWriteRGB = 0;
//					tmpRGB = (uint) 0;
//					stateDAC = (byte) 3;
//				} // RGB data register
//				else if (port == ((uint) (0x3C9)))
//				{
//					value = value & 0x3F;
//					switch (latchWriteRGB)
//					{
//						case 0: // R
//							tmpRGB = (uint)(value << 2);
//							break;
//						case 1: // G
//							tmpRGB = tmpRGB | (value << 10);
//							break;
//						case 2: // B
//							vgaPalette[latchWritePal] = Color.FromArgb((int) (tmpRGB | (value << 18)));
//							//vgaPalette(latchWritePal) = Color.FromArgb((&HFF << 24) Or tmpRGB Or (CUInt(value) << 18))
//							latchWritePal++;
//							break;
//					}
//					latchWriteRGB = (int)((latchWriteRGB + 1) % 3);
//				} // 6845 index register
//				else if (port == ((uint) (0x3D4)))
//				{
//					portRAM[port] = value;
//					base.Out(port, value);
//				} // 6845 data register
//				else if (port == ((uint) (0x3D5)))
//				{
//					VGA_CRTC[portRAM[0x3D4]] = value;
//					base.Out(port, value);

//					//Case &H3CE ' VGA graphics index
//					//    portRAM(port) = value Mod &H8 ' FIXME: This is one fugly hack!
//				}
//				else if (port == ((uint) (0x3CF)))
//				{
//					VGA_GC[portRAM[0x3CE]] = value;

//					//Case &H3B8
//					//    portRAM(port) = value
//					//    MyBase.Out(port, value)
//				}
//				else
//				{
//					portRAM[port] = value;
//				}
//			}

//			public override string Name
//			{
//				get
//				{
//					return "VGA";
//				}
//			}

//			public override string Description
//			{
//				get
//				{
//					return "VGA Video Adapter";
//				}
//			}

//			public override int VersionMajor
//			{
//				get
//				{
//					return 0;
//				}
//			}

//			public override int VersionMinor
//			{
//				get
//				{
//					return 0;
//				}
//			}

//			public override int VersionRevision
//			{
//				get
//				{
//					return 1;
//				}
//			}

//			public override void Reset()
//			{
//				base.Reset();
//				InitVideoMemory(false);
//			}

//			protected override void InitVideoMemory(bool clearScreen)
//			{
//				if (!isInit)
//				{
//					return;
//				}

//				base.InitVideoMemory(clearScreen);

//				mEndGraphicsVideoAddress = mStartGraphicsVideoAddress + 128 * 1024; // 128KB
//				ramOffset = (uint)(mMainMode == MainModes.Text ? mStartTextVideoAddress : mStartGraphicsVideoAddress);

//				AutoSize();
//			}

//			public override void Write(uint address, ushort value)
//			{
//				byte curValue = 0;

//				if ((int) (VGA_GC[5] & 3) == 0)
//				{
//					value = (ushort)(ShiftVGA(value));

//					if ((VGA_SC[2] & 1) != 0)
//					{
//						if ((VGA_GC[1] & 1) != 0)
//						{
//							curValue = ((VGA_GC[0] & 1) != 0) ? 255 : 0;
//						}
//						else
//						{
//							curValue = value;
//						}
//						curValue = LogicVGA(curValue, VGA_Latch[0]);
//						vRAM[address + planeSize * 0] = (byte)(((!VGA_GC[8]) & curValue) || (VGA_SC[8] & VGA_Latch[0]));
//					}

//					if ((VGA_SC[2] & 2) != 0)
//					{
//						if ((VGA_GC[1] & 2) != 0)
//						{
//							curValue = ((VGA_GC[0] & 2) != 0) ? 255 : 0;
//						}
//						else
//						{
//							curValue = value;
//						}
//						curValue = LogicVGA(curValue, VGA_Latch[1]);
//						vRAM[address + planeSize * 1] = (byte)(((!VGA_GC[8]) & curValue) || (VGA_SC[8] & VGA_Latch[1]));
//					}

//					if ((VGA_SC[2] & 4) != 0)
//					{
//						if ((VGA_GC[1] & 4) != 0)
//						{
//							curValue = ((VGA_GC[0] & 4) != 0) ? 255 : 0;
//						}
//						else
//						{
//							curValue = value;
//						}
//						curValue = LogicVGA(curValue, VGA_Latch[2]);
//						vRAM[address + planeSize * 2] = (byte)(((!VGA_GC[8]) & curValue) || (VGA_SC[8] & VGA_Latch[2]));
//					}

//					if ((VGA_SC[2] & 8) != 0)
//					{
//						if ((VGA_GC[1] & 8) != 0)
//						{
//							curValue = ((VGA_GC[0] & 8) != 0) ? 255 : 0;
//						}
//						else
//						{
//							curValue = value;
//						}
//						curValue = LogicVGA(curValue, VGA_Latch[3]);
//						vRAM[address + planeSize * 3] = (byte)(((!VGA_GC[8]) & curValue) || (VGA_SC[8] & VGA_Latch[3]));
//					}
//				}
//				else if ((int) (VGA_GC[5] & 3) == 1)
//				{
//					if ((VGA_SC[2] & 1) != 0)
//					{
//						vRAM[address + planeSize * 0] = VGA_Latch[0];
//					}
//					if ((VGA_SC[2] & 2) != 0)
//					{
//						vRAM[address + planeSize * 1] = VGA_Latch[1];
//					}
//					if ((VGA_SC[2] & 4) != 0)
//					{
//						vRAM[address + planeSize * 2] = VGA_Latch[2];
//					}
//					if ((VGA_SC[2] & 8) != 0)
//					{
//						vRAM[address + planeSize * 3] = VGA_Latch[3];
//					}
//				}
//				else if ((int) (VGA_GC[5] & 3) == 2)
//				{
//					if ((VGA_SC[2] & 1) != 0)
//					{
//						if ((VGA_GC[1] & 1) != 0)
//						{
//							if ((value & 1) != 0)
//							{
//								curValue = (byte) 255;
//							}
//							else
//							{
//								curValue = (byte) 0;
//							}
//						}
//						else
//						{
//							curValue = value;
//						}
//						curValue = LogicVGA(curValue, VGA_Latch[0]);
//						curValue = (byte)(((!VGA_GC[8]) & curValue) || (VGA_SC[8] & VGA_Latch[0]));
//						vRAM[address + planeSize * 0] = curValue;
//					}

//					if ((VGA_SC[2] & 2) != 0)
//					{
//						if ((VGA_GC[1] & 2) != 0)
//						{
//							if ((value & 2) != 0)
//							{
//								curValue = (byte) 255;
//							}
//							else
//							{
//								curValue = (byte) 0;
//							}
//						}
//						else
//						{
//							curValue = value;
//						}
//						curValue = LogicVGA(curValue, VGA_Latch[1]);
//						curValue = (byte)(((!VGA_GC[8]) & curValue) || (VGA_SC[8] & VGA_Latch[1]));
//						vRAM[address + planeSize * 1] = curValue;
//					}

//					if ((VGA_SC[2] & 4) != 0)
//					{
//						if ((VGA_GC[1] & 4) != 0)
//						{
//							if ((value & 4) != 0)
//							{
//								curValue = (byte) 255;
//							}
//							else
//							{
//								curValue = (byte) 0;
//							}
//						}
//						else
//						{
//							curValue = value;
//						}
//						curValue = LogicVGA(curValue, VGA_Latch[2]);
//						curValue = (byte)(((!VGA_GC[8]) & curValue) || (VGA_SC[8] & VGA_Latch[2]));
//						vRAM[address + planeSize * 2] = curValue;
//					}

//					if ((VGA_SC[2] & 8) != 0)
//					{
//						if ((VGA_GC[1] & 8) != 0)
//						{
//							if ((value & 8) != 0)
//							{
//								curValue = (byte) 255;
//							}
//							else
//							{
//								curValue = (byte) 0;
//							}
//						}
//						else
//						{
//							curValue = value;
//						}
//						curValue = LogicVGA(curValue, VGA_Latch[3]);
//						curValue = (byte)(((!VGA_GC[8]) & curValue) || (VGA_SC[8] & VGA_Latch[3]));
//						vRAM[address + planeSize * 3] = curValue;
//					}
//				}
//				else if ((int) (VGA_GC[5] & 3) == 3)
//				{
//					tmpVal = value & VGA_GC[8];
//					value = (ushort)(ShiftVGA(value));

//					if ((VGA_SC[2] & 1) != 0)
//					{
//						if ((VGA_GC[0] & 1) != 0)
//						{
//							if ((value & 1) != 0)
//							{
//								curValue = (byte) 255;
//							}
//							else
//							{
//								curValue = (byte) 0;
//							}
//						}
//						curValue = LogicVGA(curValue, VGA_Latch[0]);
//						curValue = (byte)(((!tmpVal) & curValue) | (tmpVal & VGA_Latch[0]));
//						vRAM[address + planeSize * 0] = curValue;
//					}

//					if ((VGA_SC[2] & 2) != 0)
//					{
//						if ((VGA_GC[0] & 2) != 0)
//						{
//							if ((value & 2) != 0)
//							{
//								curValue = (byte) 255;
//							}
//							else
//							{
//								curValue = (byte) 0;
//							}
//						}
//						curValue = LogicVGA(curValue, VGA_Latch[1]);
//						curValue = (byte)(((!tmpVal) & curValue) | (tmpVal & VGA_Latch[1]));
//						vRAM[address + planeSize * 1] = curValue;
//					}

//					if ((VGA_SC[2] & 4) != 0)
//					{
//						if ((VGA_GC[0] & 4) != 0)
//						{
//							if ((value & 4) != 0)
//							{
//								curValue = (byte) 255;
//							}
//							else
//							{
//								curValue = (byte) 0;
//							}
//						}
//						curValue = LogicVGA(curValue, VGA_Latch[2]);
//						curValue = (byte)(((!tmpVal) & curValue) | (tmpVal & VGA_Latch[2]));
//						vRAM[address + planeSize * 2] = curValue;
//					}

//					if ((VGA_SC[2] & 8) != 0)
//					{
//						if ((VGA_GC[0] & 8) != 0)
//						{
//							if ((value & 8) != 0)
//							{
//								curValue = (byte) 255;
//							}
//							else
//							{
//								curValue = (byte) 0;
//							}
//						}
//						curValue = LogicVGA(curValue, VGA_Latch[3]);
//						curValue = (byte)(((!tmpVal) & curValue) | (tmpVal & VGA_Latch[3]));
//						vRAM[address + planeSize * 3] = curValue;
//					}
//				}
//			}

//			public override ushort Read(uint address)
//			{
//				VGA_Latch[0] = vRAM[address + planeSize * 0];
//				VGA_Latch[1] = vRAM[address + planeSize * 1];
//				VGA_Latch[2] = vRAM[address + planeSize * 2];
//				VGA_Latch[3] = vRAM[address + planeSize * 3];

//				if ((VGA_SC[2] & 1) != 0)
//				{
//					return (ushort)(vRAM[address + planeSize * 0]);
//				}
//				if ((VGA_SC[2] & 2) != 0)
//				{
//					return (ushort)(vRAM[address + planeSize * 1]);
//				}
//				if ((VGA_SC[2] & 4) != 0)
//				{
//					return (ushort)(vRAM[address + planeSize * 2]);
//				}
//				if ((VGA_SC[2] & 8) != 0)
//				{
//					return (ushort)(vRAM[address + planeSize * 3]);
//				}

//				return (ushort)  0;
//			}

//			private byte ShiftVGA(byte value)
//			{
//				for (int i = 0; i <= (VGA_GC[3] & 7) - 1; i++)
//				{
//					value = (byte)((value >> 1) | ((value & 1) << 7));
//				}
//				return value;
//			}

//			private byte LogicVGA(byte curValue, byte latchValue)
//			{
//				// Raster Op
//				if ((int) ((VGA_GC[3] >> 3) != 0 && 3 != 0) == 1)
//				{
//					return curValue & latchValue;
//				}
//				else if ((int) ((VGA_GC[3] >> 3) != 0 && 3 != 0) == 2)
//				{
//					return curValue | latchValue;
//				}
//				else if ((int) ((VGA_GC[3] >> 3) != 0 && 3 != 0) == 3)
//				{
//					return curValue ^ latchValue;
//				}
//				else
//				{
//					return curValue;
//				}
//			}
//		}

//	}
