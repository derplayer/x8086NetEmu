using System.Collections.Generic;
using System;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Xml.Linq;
using System.Collections;


//using System.Threading;
//using NAudio.Wave;
//using x8086SharpEmu;

namespace x8086SharpEmu
{
#if Win32
	
	public class AdlibAdapter : Adapter // Based on fake86's implementation
	{
		
		private WaveOut waveOut;
		private SpeakerAdpater.CustomBufferProvider audioProvider;
		private readonly byte[] mAudioBuffer;
		
		private readonly byte[][] waveForm = new byte[][] {new byte[] {1, 8, 13, 20, 25, 32, 36, 42, 46, 50, 54, 57, 60, 61, 62, 64, 63, 65, 61, 61, 58, 55, 51, 49, 44, 38, 34, 28, 23, 16, 11, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0}, new byte[] {1, 8, 13, 21, 25, 31, 36, 43, 45, 50, 54, 57, 59, 62, 63, 63, 63, 64, 63, 59, 59, 55, 52, 48, 44, 38, 34, 28, 23, 16, 10, 4, 2, 7, 14, 20, 26, 31, 36, 42, 45, 51, 54, 56, 60, 62, 62, 63, 65, 63, 62, 60, 58, 55, 52, 48, 44, 38, 34, 28, 23, 17, 10, 3}, new byte[] {1, 8, 13, 20, 26, 31, 36, 42, 46, 51, 53, 57, 60, 62, 61, 66, 16, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 7, 13, 21, 25, 32, 36, 41, 47, 50, 54, 56, 60, 62, 61, 67, 15, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0}, new byte[] {1, 8, 13, 20, 26, 31, 37, 41, 47, 49, 54, 58, 58, 62, 63, 63, 64, 63, 62, 61, 58, 55, 52, 47, 45, 38, 34, 27, 23, 17, 10, 4, -2, -8, -15, -21, -26, -34, -36, -42, -48, -51, -54, -59, -60, -62, -64, -65, -65, -63, -64, -61, -59, -56, -53, -48, -46, -39, -36, -28, -24, -17, -11, -6}};
		
		private readonly byte[][] oplWave = new byte[][] {new byte[] {
				0, 1, 3, 4, 6, 7, 9, 11, 12, 14, 15, 17, 18, 20, 22, 23, 24, 26, 27, 29, 30, 31, 33, 34, 36, 37, 38, 40, 40, 42, 43, 44, 46, 46, 48, 49, 50, 51, 51, 53,
				53, 54, 55, 56, 57, 57, 58, 59, 59, 60, 61, 61, 62, 62, 63, 63, 63, 64, 64, 64, 116, 116, 116, 116, 116, 116, 116, 116, 116, 64, 64, 64, 63, 63, 63, 62, 62, 61, 61, 60,
				59, 59, 58, 57, 57, 56, 55, 54, 53, 53, 51, 51, 50, 49, 48, 46, 46, 44, 43, 42, 40, 40, 38, 37, 36, 34, 33, 31, 30, 29, 27, 26, 24, 23, 22, 20, 18, 17, 15, 14,
				12, 11, 9, 7, 6, 4, 3, 1, 0, -1, -3, -4, -6, -7, -9, -11, -12, -14, -15, -17, -18, -20, -22, -23, -24, -26, -27, -29, -30, -31, -33, -34, -36, -37, -38, -40, -40, -42, -43, -44,
				-46, -46, -48, -49, -50, -51, -51, -53, -53, -54, -55, -56, -57, -57, -58, -59, -59, -60, -61, -61, -62, -62, -63, -63, -63, -64, -64, -64, -116, -116, -116, -116, -116, -116, -116, -116, -116, -64, -64, -64,
				-63, -63, -63, -62, -62, -61, -61, -60, -59, -59, -58, -57, -57, -56, -55, -54, -53, -53, -51, -51, -50, -49, -48, -46, -46, -44, -43, -42, -40, -40, -38, -37, -36, -34, -33, -31, -30, -29, -27, -26,
				-24, -23, -22, -20, -18, -17, -15, -14, -12, -11, -9, -7, -6, -4, -3, -1
			}, new byte[] {
				0, 1, 3, 4, 6, 7, 9, 11, 12, 14, 15, 17, 18, 20, 22, 23, 24, 26, 27, 29, 30, 31, 33, 34, 36, 37, 38, 40, 40, 42, 43, 44, 46, 46, 48, 49, 50, 51, 51, 53,
				53, 54, 55, 56, 57, 57, 58, 59, 59, 60, 61, 61, 62, 62, 63, 63, 63, 64, 64, 64, 116, 116, 116, 116, 116, 116, 116, 116, 116, 64, 64, 64, 63, 63, 63, 62, 62, 61, 61, 60,
				59, 59, 58, 57, 57, 56, 55, 54, 53, 53, 51, 51, 50, 49, 48, 46, 46, 44, 43, 42, 40, 40, 38, 37, 36, 34, 33, 31, 30, 29, 27, 26, 24, 23, 22, 20, 18, 17, 15, 14,
				12, 11, 9, 7, 6, 4, 3, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
				0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
				0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
				0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
			}, new byte[] {
				0, 1, 3, 4, 6, 7, 9, 11, 12, 14, 15, 17, 18, 20, 22, 23, 24, 26, 27, 29, 30, 31, 33, 34, 36, 37, 38, 40, 40, 42, 43, 44, 46, 46, 48, 49, 50, 51, 51, 53,
				53, 54, 55, 56, 57, 57, 58, 59, 59, 60, 61, 61, 62, 62, 63, 63, 63, 64, 64, 64, 116, 116, 116, 116, 116, 116, 116, 116, 116, 64, 64, 64, 63, 63, 63, 62, 62, 61, 61, 60,
				59, 59, 58, 57, 57, 56, 55, 54, 53, 53, 51, 51, 50, 49, 48, 46, 46, 44, 43, 42, 40, 40, 38, 37, 36, 34, 33, 31, 30, 29, 27, 26, 24, 23, 22, 20, 18, 17, 15, 14,
				12, 11, 9, 7, 6, 4, 3, 1, 0, 1, 3, 4, 6, 7, 9, 11, 12, 14, 15, 17, 18, 20, 22, 23, 24, 26, 27, 29, 30, 31, 33, 34, 36, 37, 38, 40, 40, 42, 43, 44,
				46, 46, 48, 49, 50, 51, 51, 53, 53, 54, 55, 56, 57, 57, 58, 59, 59, 60, 61, 61, 62, 62, 63, 63, 63, 64, 64, 64, 116, 116, 116, 116, 116, 116, 116, 116, 116, 64, 64, 64,
				63, 63, 63, 62, 62, 61, 61, 60, 59, 59, 58, 57, 57, 56, 55, 54, 53, 53, 51, 51, 50, 49, 48, 46, 46, 44, 43, 42, 40, 40, 38, 37, 36, 34, 33, 31, 30, 29, 27, 26,
				24, 23, 22, 20, 18, 17, 15, 14, 12, 11, 9, 7, 6, 4, 3, 1
			}, new byte[] {
				0, 1, 3, 4, 6, 7, 9, 11, 12, 14, 15, 17, 18, 20, 22, 23, 24, 26, 27, 29, 30, 31, 33, 34, 36, 37, 38, 40, 40, 42, 43, 44, 46, 46, 48, 49, 50, 51, 51, 53,
				53, 54, 55, 56, 57, 57, 58, 59, 59, 60, 61, 61, 62, 62, 63, 63, 63, 64, 64, 64, 116, 116, 116, 116, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
				0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
				0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 3, 4, 6, 7, 9, 11, 12, 14, 15, 17, 18, 20, 22, 23, 24, 26, 27, 29, 30, 31, 33, 34, 36, 37, 38, 40, 40, 42, 43, 44,
				46, 46, 48, 49, 50, 51, 51, 53, 53, 54, 55, 56, 57, 57, 58, 59, 59, 60, 61, 61, 62, 62, 63, 63, 63, 64, 64, 64, 116, 116, 116, 116, 0, 0, 0, 0, 0, 0, 0, 0,
				0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
				0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
			}};
		
		private struct OplStruct
		{
			public byte wave;
		}
		private OplStruct[][] Opl = new OplStruct[9][];
		
		private struct ChanStruct
		{
			public ushort Frequency;
			public double ConvFreq;
			public bool KeyOn;
			public ushort Octave;
			public byte WaveformSelect;
		}
		private readonly ChanStruct[] channel = new ChanStruct[9];
		
		private readonly double[] attackTable = new double[] {1.0003, 1.00025, 1.0002, 1.00015, 1.0001, 1.00009, 1.00008, 1.00007, 1.00006, 1.00005, 1.00004, 1.00003, 1.00002, 1.00001, 1.000005};
		private readonly double[] decayTable = new double[] {0.99999, 0.999985, 0.99998, 0.999975, 0.99997, 0.999965, 0.99996, 0.999955, 0.99995, 0.999945, 0.99994, 0.999935, 0.99994, 0.999925, 0.99992, 0.99991};
		private readonly byte[] oplTable = new byte[] {(byte) 0, (byte) 0, (byte) 0, (byte) 1, (byte) 1, (byte) 1, (byte) 255, (byte) 255, (byte) 0, (byte) 0, (byte) 0, (byte) 1, (byte) 1, (byte) 1, (byte) 255, (byte) 255, (byte) 0, (byte) 0, (byte) 0, (byte) 1, (byte) 1, (byte) 1};
		
		private readonly double[] envelope = new double[9];
		private readonly double[] decay = new double[9];
		private readonly double[] attack = new double[9];
		private readonly bool[] attack2 = new bool[9];
		
		private readonly ushort[] regMem = new ushort[0xFF];
		private ushort address = (ushort) 0;
		private bool precussion = false;
		private byte status = (byte) 0;
		private readonly double[] oplSstep = new double[9];
		
		public AdlibAdapter(X8086 cpu) : base(cpu)
		{
			
			for (int i = 0; i <= Opl.Length - 1; i++)
			{
				Opl[i] = new OplStruct[2];
			}
			
			ValidPortAddress.Add(0x388);
			ValidPortAddress.Add(0x389);
			
			Array.Resize(ref attackTable, 16);
			Array.Resize(ref decayTable, 16);
			Array.Resize(ref oplTable, 16);
		}
		
		public double Volume
		{
			get
			{
				return waveOut.Volume;
			}
			set
			{
				waveOut.Volume = (float) value;
			}
		}
		
		public byte[] AudioBuffer
		{
			get
			{
				return mAudioBuffer;
			}
		}
		
		public override void CloseAdapter()
		{
			waveOut.Stop();
			waveOut.Dispose();
		}
		
		public override void InitiAdapter()
		{
			waveOut = new WaveOut() {
					NumberOfBuffers = 32,
					DesiredLatency = 200
				};
			
			audioProvider = new SpeakerAdpater.CustomBufferProvider(FillAudioBuffer, SpeakerAdpater.SampleRate, 8, 1);
			waveOut.Init(audioProvider);
			waveOut.Play();
			
			System.Threading.Tasks.Task.Run(()=>
			{
				long maxTicks = (long)(100000 * Scheduler.BASECLOCK / SpeakerAdpater.SampleRate);
				long curTick = 0;
				long lastTick = 0;
				do
				{
					curTick = (long)(base.CPU.Sched.CurrentTime);
					
					if (curTick >= (lastTick + maxTicks))
					{
						lastTick = curTick - (curTick - (lastTick + maxTicks));
						
						for (byte currentChannel = 0; currentChannel <= 9 - 1; currentChannel++)
						{
							if (Frequency(currentChannel) != 0)
							{
								if (attack2(currentChannel))
								{
									envelope[currentChannel] *= decay(currentChannel);
								}
								else
								{
									envelope[currentChannel] *= attack(currentChannel);
									if (envelope(currentChannel) >= 1.0)
									{
										attack2[currentChannel] = true;
									}
								}
							}
						}
					}
					
					Thread.Sleep(100);
				} while (waveOut.PlaybackState == PlaybackState.Playing);
			});
		}
		
		private void FillAudioBuffer(byte[] buffer)
		{
			for (int i = 0; i <= buffer.Length - 1; i++)
			{
				buffer[i] = (byte) (GenerateSample() + 128);
			}
		}
		
		public override ushort In(uint port)
		{
			status = (regMem[4] == 0) ? 0 : 0x80;
			status += (byte)((regMem[4] & 1) * 0x40 + (regMem[4] & 2) * 0x10);
			return (ushort)(status);
		}
		
		public override void Out(uint port, ushort value)
		{
			if (port == 0x388)
			{
				address = value;
				return;
			}
			
			port = (uint)(address);
			regMem[port] = value;
			
			if (port == ((uint) 4)) // Timer Control
			{
				if ((value & 0x80) != 0)
				{
					status = (byte) 0;
					regMem[4] = (ushort) 0;
				}
			}
			else if (port == ((uint) (0xBD)))
			{
				precussion = (value & 0x10) != 0;
			}
			
			if (port >= 0x60 && port <= 0x75) // Attack / Decay
			{
				port = (uint)((port & 15) % 9);
				attack[port] = attackTable[15 - (value >> 4)] * 1.006;
				decay[port] = decayTable[value & 15];
			}
			else if (port >= 0xA0 && port <= 0xB8) // Octave / Frequency / Key On
			{
				port = (uint)((port & 15) % 9);
				if (!channel[port].KeyOn && ((regMem[0xB0 + port] >> 5) & 1) == 1)
				{
					attack2[port] = false;
					envelope[port] = 0.0025;
				}
				
				channel[port].Frequency = (ushort)(regMem[0xA0 + port] | ((regMem[0xB0 + port] & 3) << 8));
				channel[port].ConvFreq = System.Convert.ToDouble(channel[port].Frequency * 0.7626459);
				channel[port].KeyOn = ((regMem[0xB0 + port] >> 5) & 1) == 1;
				channel[port].Octave = (ushort)((regMem[0xB0 + port] >> 2) & 7);
			}
			else if (port >= 0xE0 & port <= 0xF5) // Waveform select
			{
				channel[(port & 15) % 9].WaveformSelect = value & 3;
			}
		}
		
		private ushort Frequency(byte chanNum)
		{
			ushort tmpFreq = default(ushort);
			
			if (!channel[chanNum].KeyOn)
			{
				return (ushort)  0;
			}
			tmpFreq = (ushort)(channel[chanNum].ConvFreq);
			
			switch (channel[chanNum].Octave)
			{
				case (ushort) 0:
					tmpFreq >>= (ushort) 4;
					break;
				case (ushort) 1:
					tmpFreq >>= (ushort) 3;
					break;
				case (ushort) 2:
					tmpFreq >>= (ushort) 2;
					break;
				case (ushort) 3:
					tmpFreq >>= (ushort) 1;
					break;
				case (ushort) 5:
					tmpFreq <<= (ushort) 1;
					break;
				case (ushort) 6:
					tmpFreq <<= (ushort) 2;
					break;
				case (ushort) 7:
					tmpFreq <<= (ushort) 3;
					break;
			}
			
			return tmpFreq;
		}
		
		private int Sample(byte chanNum)
		{
			if (precussion && chanNum >= 6 && chanNum <= 8)
			{
				return 0;
			}
			
			ushort fullStep = (ushort) (SpeakerAdpater.SampleRate / Frequency(chanNum));
			byte idx = (byte)((oplSstep[chanNum] / (fullStep / 256.0)) % 255);
			uint tmpSample = (uint)(oplWave[channel[chanNum].WaveformSelect][idx]);
			double tmpStep = envelope[chanNum];
			if (tmpStep > 1.0)
			{
				tmpStep = 1.0;
			}
			tmpSample = tmpSample * tmpStep * 12.0;
			
			oplSstep[chanNum]++;
			if (oplSstep[chanNum] > fullStep)
			{
				oplSstep[chanNum] = 0;
			}
			return (int) tmpSample;
		}
		
		private short GenerateSample()
		{
			int accumulator = 0;
			for (byte chanNum = 0; chanNum <= 9 - 1; chanNum++)
			{
				if (Frequency(chanNum) != 0)
				{
					accumulator += Sample(chanNum);
				}
			}
			return (short)  accumulator;
		}
		
		public override string Name
		{
			get
			{
				return "Adlib OPL2"; // FM OPerator Type-L
			}
		}
		
		public override string Description
		{
			get
			{
				return "Yamaha YM3526";
			}
		}
		
		public override void Run()
		{
			X8086.Notify("{Name} Running", X8086.NotificationReasons.Info);
		}
		
		public override Adapter.AdapterType Type
		{
			get
			{
				return AdapterType.AudioDevice;
			}
		}
		
		public override string Vendor
		{
			get
			{
				return "Ad Lib, Inc.";
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
				return 0;
			}
		}
		
		public override int VersionRevision
		{
			get
			{
				return 23;
			}
		}
	}
#endif
}