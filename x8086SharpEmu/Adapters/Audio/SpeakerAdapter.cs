using System.Collections.Generic;
using System;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Xml.Linq;
using System.Collections;
using System.Windows.Forms;

using NAudio.Wave;
using x8086SharpEmu;

namespace x8086SharpEmu
{
#if Win32
	
	public partial class SpeakerAdpater : Adapter
	{
		
		public class CustomBufferProvider : IWaveProvider
		{
			
			public delegate void FillBuffer(byte[] buffer);
			
			public WaveFormat WaveFormat {get; set;}
			private FillBuffer fb;
			
			public CustomBufferProvider(FillBuffer bufferFiller, int sampleRate, int bitDepth, int channels)
			{
				WaveFormat = new WaveFormat(sampleRate, bitDepth, channels);
				fb = bufferFiller;
			}
			
			public int Read(byte[] buffer, int offset, int count)
			{
				fb.Invoke(buffer);
				return count;
			}
		}
		
		private enum WaveForms
		{
			Squared,
			Sinusoidal
		}
		
		private readonly WaveForms waveForm = WaveForms.Squared;
		
		private const double ToRad = Math.PI / 180;
		
		private WaveOut waveOut;
		private CustomBufferProvider audioProvider;
		
		public const int SampleRate = 44100;
		
		private bool mEnabled;
		
		private double mFrequency;
		private int waveLength;
		private int halfWaveLength;
		private int currentStep;
		
		public double Volume {get; set;}
		public byte[] AudioBuffer {get; set;}
		
		public SpeakerAdpater(X8086 cpu) : base(cpu)
		{
			if (base.CPU.PIT != null)
			{
				base.CPU.PIT.Speaker = this;
			}
			Volume = 0.08;
		}
		
		public double Frequency
		{
			get
			{
				return mFrequency;
			}
			set
			{
				if (mFrequency != value)
				{
					mFrequency = value;
					UpdateWaveformParameters();
				}
			}
		}
		
		public bool Enabled
		{
			get
			{
				return mEnabled;
			}
			set
			{
				mEnabled = value;
				UpdateWaveformParameters();
			}
		}
		
		private void UpdateWaveformParameters()
		{
			if (mFrequency > 0)
			{
				waveLength = SampleRate / mFrequency;
			}
			else
			{
				waveLength = 0;
			}
			
			halfWaveLength = System.Convert.ToInt32((double) waveLength / 2);
		}
		
		private void FillAudioBuffer(byte[] buffer)
		{
			double v = 0;
			
			for (int i = 0; i <= buffer.Length - 1; i++)
			{
				switch (waveForm)
				{
					case WaveForms.Squared:
						if (mEnabled)
						{
							if (currentStep <= halfWaveLength)
							{
								v = -128;
							}
							else
							{
								v = 127;
							}
						}
						else
						{
							v = 0;
						}
						break;
					case WaveForms.Sinusoidal:
						if (mEnabled && waveLength > 0)
						{
							v = (double) (Math.Floor((decimal) (Math.Sin((double) currentStep / waveLength * (mFrequency / 2) * ToRad) * 128)));
						}
						else
						{
							v = 0;
						}
						break;
				}
				
				v *= Volume;
				if (v <= 0)
				{
					v = 128 + v;
				}
				else
				{
					v += 127;
				}
				buffer[i] = (byte) v;
				
				currentStep++;
				if (currentStep >= waveLength)
				{
					currentStep = 0;
				}
			}
			
			//mAudioBuffer = buffer
		}
		
		public override void CloseAdapter()
		{
			if (waveOut.PlaybackState == PlaybackState.Playing)
			{
				waveOut.Stop();
			}
			waveOut.Dispose();
		}
		
		public override string Description
		{
			get
			{
				return "PC Speaker";
			}
		}
		
		public override void InitiAdapter()
		{
			waveOut = new WaveOut() {
					NumberOfBuffers = 32,
					DesiredLatency = 200
				};
			audioProvider = new CustomBufferProvider(FillAudioBuffer, SampleRate, 8, 1);
			waveOut.Init(audioProvider);
			waveOut.Volume = 1;
			waveOut.Play();
		}
		
//#DisableWarningBC42353;
		public override ushort In(uint port)
		{
		}
		
		public override void Out(uint port, ushort value)
		{
			
		}
		
		public override string Name
		{
			get
			{
				return "Speaker";
			}
		}
		
		public override void Run()
		{
			X8086.Notify("Speaker Running", X8086.NotificationReasons.Info);
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
				return 0;
			}
		}
		
		public override int VersionRevision
		{
			get
			{
				return 1;
			}
		}
	}
#else
    public partial class SpeakerAdpater : Adapter
    {

        public SpeakerAdpater(X8086 cpu)
        {

        }

        public override void CloseAdapter()
        {

        }

        public override string Description
        {
            get
            {
                return "Null PC Speaker";
            }
        }

        public override ushort In(uint port)
        {
            return (ushort)0;
        }

        public override void Out(uint port, ushort value)
        {

        }

        public override void InitiAdapter()
        {

        }

        public override string Name
        {
            get
            {
                return "Null Speaker";
            }
        }

        public override void Run()
        {

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
                return 0;
            }
        }

        public override int VersionRevision
        {
            get
            {
                return 1;
            }
        }
    }
#endif
}
