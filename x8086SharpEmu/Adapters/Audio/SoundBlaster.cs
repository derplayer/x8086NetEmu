//using System.Collections.Generic;
//using System;
//using System.Linq;
//using System.Drawing;
//using System.Diagnostics;
//using System.Xml.Linq;
//using System.Collections;
//using System.Windows.Forms;

//using NAudio.Wave;
//using x8086SharpEmu;

//namespace x8086SharpEmu
//{
//#if Win32

//	public class SoundBlaster : Adapter, IDMADevice // Based on fake86's implementation
//	{

//		private WaveOut waveOut;
//		private SpeakerAdpater.CustomBufferProvider audioProvider;

//		public struct BlasterData
//		{
//			public byte[] Mem;
//			public ushort MemPtr;
//			public ushort SampleRate;
//			public byte DspMaj;
//			public byte DspMin;
//			public bool SpeakerEnabled;
//			public byte LastResetVal;
//			public byte LastCmdVal;
//			public byte LastTestVal;
//			public byte WaitForArg;
//			public bool Paused8;
//			public bool Paused16;
//			public byte Sample;
//			public InterruptRequest Irq;
//			public byte Dma;
//			public bool UsingDma;
//			public byte MaskDma;
//			public bool UseAutoInit;
//			public uint BlockSize;
//			public uint BlockStep;
//			public ulong SampleTicks;

//			public struct Mixer
//			{
//				public byte Index;
//				public byte[] Reg;
//			}
//			public Mixer MixerData;
//		}
//		private BlasterData blaster;

//		private byte[] mixer = new byte[256];
//		private byte mixerIndex;

//		private DMAI8237.Channel dma;

//		private AdlibAdapter adLib;

//		public SoundBlaster(X8086 cpu, AdlibAdapter adlib, ushort port = 0x220, byte irq = 5, byte dmaChannel = 1) : base(cpu)
//		{
//			this.adLib = adlib;

//			blaster.Mem = new byte[1024];
//			blaster.MixerData.Reg = new byte[256];

//			blaster.Irq = base.CPU.PIC?.GetIrqLine(irq);
//			blaster.Dma = dmaChannel;

//			for (uint i = port; i <= port + 0xE; i++)
//			{
//				ValidPortAddress.Add(i);
//			}
//		}

//		private void SetSampleTicks()
//		{
//			if (blaster.SampleRate == 0)
//			{
//				blaster.SampleTicks = 0;
//			}
//			else
//			{
//				blaster.SampleTicks = base.CPU.Clock / blaster.SampleRate;
//			}
//		}

//		private void CmdBlaster(byte value)
//		{
//			byte recognized = (byte) 1;
//			if (blaster.WaitForArg != 0)
//			{
//				if (blaster.LastCmdVal == ((byte) (0x10))) // direct 8-bit sample output
//				{
//					blaster.Sample = value;
//				} // 8-bit single block DMA output
//				else if (((blaster.LastCmdVal == ((byte) (0x14))) || (blaster.LastCmdVal == ((byte) (0x24)))) || (blaster.LastCmdVal == ((byte) (0x91))))
//				{
//					if (blaster.WaitForArg == 2)
//					{
//						blaster.BlockSize = System.Convert.ToUInt32((blaster.BlockSize & 0xFF00) | value);
//						blaster.WaitForArg = (byte) 3;
//						return;
//					}
//					else
//					{
//						blaster.BlockSize = System.Convert.ToUInt32((blaster.BlockSize & 0xFF) | (value << 8));

//						blaster.UsingDma = true;
//						blaster.BlockStep = (uint) 0;
//						blaster.UseAutoInit = false;
//						blaster.Paused8 = false;
//						blaster.SpeakerEnabled = true;
//					}
//				} // set time constant
//				else if (blaster.LastCmdVal == ((byte) (0x40)))
//				{
//					blaster.SampleRate = System.Convert.ToUInt16(base.CPU.Clock / (256 - value));
//					SetSampleTicks();
//				} // set DSP block transfer size
//				else if (blaster.LastCmdVal == ((byte) (0x48)))
//				{
//					if (blaster.WaitForArg == 2)
//					{
//						blaster.BlockSize = System.Convert.ToUInt32((blaster.BlockSize & 0xFF00) | value);
//						blaster.WaitForArg = (byte) 3;
//						return;
//					}
//					else
//					{
//						blaster.BlockSize = System.Convert.ToUInt32((blaster.BlockSize & 0xFF) | (value << 8));
//						blaster.BlockStep = (uint) 0;
//					}
//				} // DSP identification for Sound Blaster 2.0 and newer (invert each bit and put in read buffer)
//				else if (blaster.LastCmdVal == ((byte) (0xE0)))
//				{
//					BufNewData(!value);
//				} // DSP write test, put data value into read buffer
//				else if (blaster.LastCmdVal == ((byte) (0xE4)))
//				{
//					BufNewData(value);
//					blaster.LastTestVal = value;
//				}
//				else
//				{
//					recognized = (byte) 0;
//				}
//				if (recognized)
//				{
//					return;
//				}
//			}

//			if ((((value == ((byte) (0x10))) || (value == ((byte) (0x40)))) || (value == ((byte) (0xE0)))) || (value == ((byte) (0xE4))))
//			{
//				blaster.WaitForArg = (byte) 1;
//			} // 8-bit single block DMA output
//			else if ((((value == ((byte) (0x14))) || (value == ((byte) (0x24)))) || (value == ((byte) (0x48)))) || (value == ((byte) (0x91))))
//			{
//				blaster.WaitForArg = (byte) 2;
//			} // 8-bit auto-init DMA output
//			else if ((value == ((byte) (0x1C))) || (value == ((byte) (0x2C))))
//			{
//				blaster.UsingDma = true;
//				blaster.BlockStep = (uint) 0;
//				blaster.UseAutoInit = true;
//				blaster.Paused8 = false;
//				blaster.SpeakerEnabled = true;
//			} // pause 8-bit DMA I/O
//			else if (value == ((byte) (0xD0)))
//			{
//				blaster.Paused8 = true;
//			} // speaker output on
//			else if (value == ((byte) (0xD1)))
//			{
//				blaster.SpeakerEnabled = true;
//			} // speaker output off
//			else if (value == ((byte) (0xD3)))
//			{
//				blaster.SpeakerEnabled = true;
//			} // continue 8-bit DMA I/O
//			else if (value == ((byte) (0xD4)))
//			{
//				blaster.Paused8 = false;
//			} // get speaker status
//			else if (value == ((byte) (0xD8)))
//			{
//				if (blaster.SpeakerEnabled)
//				{
//					BufNewData((byte) (0xFF));
//				}
//				else
//				{
//					BufNewData((byte) (0x0));
//				}
//			} // exit 8-bit auto-init DMA I/O mode
//			else if (value == ((byte) (0xDA)))
//			{
//				blaster.UsingDma = false;
//			} // get DSP version info
//			else if (value == ((byte) (0xE1)))
//			{
//				blaster.MemPtr = (ushort) 0;
//				BufNewData(blaster.DspMaj);
//				BufNewData(blaster.DspMin);
//			} // DSP read test
//			else if (value == ((byte) (0xE8)))
//			{
//				blaster.MemPtr = (ushort) 0;
//				BufNewData(blaster.LastTestVal);
//			} // force 8-bit IRQ
//			else if (value == ((byte) (0xF2)))
//			{
//				blaster.Irq.Raise(true);
//			} // undocumented command, clears in-buffer And inserts a null byte
//			else if (value == ((byte) (0xF8)))
//			{
//				blaster.MemPtr = (ushort) 0;
//				BufNewData((byte) 0);
//			}
//		}

//		public override void InitiAdapter()
//		{
//			blaster.DspMaj = (byte) 2; // emulate a Sound Blaster 2.0
//			blaster.DspMin = (byte) 0;
//			MixerReset();

//			dma = base.CPU.DMA.GetChannel(blaster.Dma);
//			base.CPU.DMA.BindChannel(blaster.Dma, this);

//			waveOut = new WaveOut() {
//					NumberOfBuffers = 4,
//					DesiredLatency = 200
//				};
//			audioProvider = new SpeakerAdpater.CustomBufferProvider(FillAudioBuffer, SpeakerAdpater.SampleRate, 8, 1);
//			waveOut.Init(audioProvider);
//			waveOut.Play();
//		}

//		public override void CloseAdapter()
//		{
//			waveOut.Stop();
//			waveOut.Dispose();
//		}

//		private ushort GetBlasterSample()
//		{
//			TickBlaster();
//			if (!blaster.SpeakerEnabled)
//			{
//				return (ushort)  0;
//			}
//			return System.Convert.ToUInt16(blaster.Sample); //- 128
//		}

//		private void MixerReset()
//		{
//			byte v = System.Convert.ToByte((4 << 5) | (4 << 1));

//			Array.Clear(blaster.MixerData.Reg, 0, blaster.MixerData.Reg.Length);

//			blaster.MixerData.Reg[0x4] = v;
//			blaster.MixerData.Reg[0x22] = v;
//			blaster.MixerData.Reg[0x26] = v;
//		}

//		private void FillAudioBuffer(byte[] buffer)
//		{
//			for (int i = 0; i <= buffer.Length - 1; i++)
//			{
//				buffer[i] = GetBlasterSample();
//			}
//		}

//		private void BufNewData(byte value)
//		{
//			if (blaster.MemPtr < blaster.Mem.Length)
//			{
//				blaster.Mem[blaster.MemPtr] = value;
//				blaster.MemPtr++;
//			}
//		}

//		public override void Out(uint port, ushort value)
//		{
//			if ((port & 0xF == ((uint) (0x0))) || (port & 0xF == ((uint) (0x8))))
//			{
//				adLib.Out((uint) (0x388), value);
//			}
//			else if ((port & 0xF == ((uint) (0x1))) || (port & 0xF == ((uint) (0x9))))
//			{
//				adLib.Out((uint) (0x389), value);
//			}
//			else if (port & 0xF == ((uint) (0x4)))
//			{
//				mixerIndex = value;
//			}
//			else if (port & 0xF == ((uint) (0x5)))
//			{
//				mixer[mixerIndex] = value;
//			} // reset port
//			else if (port & 0xF == ((uint) (0x6)))
//			{
//				if ((value == 0x0) && (blaster.LastResetVal == 0x1))
//				{
//					blaster.SpeakerEnabled = false;
//					blaster.Sample = (byte) 128;
//					blaster.WaitForArg = (byte) 0;
//					blaster.MemPtr = (ushort) 0;
//					blaster.UsingDma = false;
//					blaster.BlockSize = (uint) 65535;
//					blaster.BlockStep = (uint) 0;
//					BufNewData((byte) (0xAA));
//					for (int i = 0; i <= mixer.Length - 1; i++)
//					{
//						mixer[i] = (byte) (0xEE);
//					}
//				}
//				blaster.LastResetVal = value;
//			} // write command/data
//			else if (port & 0xF == ((uint) (0xC)))
//			{
//				CmdBlaster(value);
//				if (blaster.WaitForArg != 3)
//				{
//					blaster.LastCmdVal = value;
//				}
//			}
//		}

//		public override ushort In(uint port)
//		{
//			if ((port & 0xF == ((uint) (0x0))) || (port & 0xF == ((uint) (0x8))))
//			{
//				return adLib.In((uint) (0x388));
//			}
//			else if ((port & 0xF == ((uint) (0x1))) || (port & 0xF == ((uint) (0x9))))
//			{
//				return adLib.In((uint) (0x389));
//			}
//			else if (port & 0xF == ((uint) (0x5)))
//			{
//				return System.Convert.ToUInt16(mixer[mixerIndex]);
//			} // read data
//			else if (port & 0xF == ((uint) (0xA)))
//			{
//				if (blaster.MemPtr == 0)
//				{
//					return (ushort)  0;
//				}
//				else
//				{
//					byte r = blaster.Mem[0];
//					Array.Copy(blaster.Mem, 0, blaster.Mem, 1, blaster.Mem.Length - 1);
//					blaster.MemPtr--;
//					return System.Convert.ToUInt16(r);
//				}
//			} // read-buffer status
//			else if (port & 0xF == ((uint) (0xE)))
//			{
//				if (blaster.MemPtr > 0)
//				{
//					return (ushort) (0x80);
//				}
//				else
//				{
//					return (ushort) (0x0);
//				}
//			}
//			else
//			{
//				return (ushort) (0x0);
//			}
//		}

//		private void TickBlaster()
//		{
//			if (!blaster.UsingDma)
//			{
//				return;
//			}
//			dma.DMARequest(true);

//			blaster.BlockStep++;
//			if (blaster.BlockStep > blaster.BlockSize)
//			{
//				blaster.Irq.Raise(true);
//				if (blaster.UseAutoInit)
//				{
//					blaster.BlockStep = (uint) 0;
//				}
//				else
//				{
//					blaster.UsingDma = false;
//				}
//			}
//		}

//		public void DMARead(byte v)
//		{
//			if (dma.Masked != 0)
//			{
//				blaster.Sample = (byte) 128;
//			}
//			if (dma.AutoInit != 0 && dma.CurrentCount > dma.BaseCount)
//			{
//				dma.CurrentCount = 0;
//			}
//			if (dma.CurrentCount > dma.BaseCount)
//			{
//				blaster.Sample = (byte) 128;
//			}

//			if (dma.Direction == 0)
//			{
//				blaster.Sample = base.CPU.Memory[(dma.Page << 16) + dma.CurrentAddress + dma.CurrentCount];
//			}
//			else
//			{
//				blaster.Sample = base.CPU.Memory[(dma.Page << 16) + dma.CurrentAddress - dma.CurrentCount];
//			}
//			dma.CurrentCount++;
//		}

//		public byte DMAWrite()
//		{
//			return blaster.Mem[blaster.MemPtr];
//		}

//		public void DMAEOP()
//		{
//			dma.DMARequest(false);
//		}

//		public override AdapterType Type
//		{
//			get
//			{
//				return AdapterType.AudioDevice;
//			}
//		}

//		public override void Run()
//		{
//			X8086.Notify("{Name} Running", X8086.NotificationReasons.Info);
//		}

//		public override string Vendor
//		{
//			get
//			{
//				return "Creative Technology Pte Ltd";
//			}
//		}

//		public override int VersionMajor
//		{
//			get
//			{
//				return 0;
//			}
//		}

//		public override int VersionMinor
//		{
//			get
//			{
//				return 0;
//			}
//		}

//		public override int VersionRevision
//		{
//			get
//			{
//				return 1;
//			}
//		}

//		public override string Description
//		{
//			get
//			{
//				return "Sound Blaster Pro 2.0";
//			}
//		}

//		public override string Name
//		{
//			get
//			{
//				return "Sound Blaster Pro 2.0";
//			}
//		}
//	}
//#endif
//}
