//using System.Collections.Generic;
//using System;
//using System.Linq;
//using System.Drawing;
//using System.Diagnostics;
//using System.Xml.Linq;
//using System.Collections;
//

//using x8086SharpEmu;

//namespace x8086SharpEmu
//{
//	// http://www.csn.ul.ie/~darkstar/assembler/manual/a07.txt
//	// http://www.ousob.com/ng/masm/ng2e21c.php
//	// http://x86.renejeschke.de/html/file_module_x86_id_79.html

//	// Code adapted from DOSBox: https://sourceforge.net/p/dosbox/code-0/HEAD/tree/dosbox/trunk/src/fpu/

//	public class x8087
//	{
//		private struct REGs
//		{
//			public double d;
//			public struct l
//			{
//				public int upper;
//				public uint lower;
//			}
//			public long ll;
//		}

//		private struct P_REGs
//		{
//			public uint m1;
//			public uint m2;
//			public ushort m3;

//			public ushort d1;
//			public uint d2;
//		}

//		public enum TAGe
//		{
//			Valid = 0,
//			Zero = 1,
//			Weird = 2,
//			Empty = 3
//		}

//		public enum ROUNDe
//		{
//			Nearest = 0,
//			Down = 1,
//			Up = 2,
//			Chop = 3
//		}

//		private class FPUc
//		{
//			public REGs[] Regs = new REGs[9];
//			public P_REGs[] P_Regs = new P_REGs[9];
//			public TAGe[] Tags = new TAGe[9];
//			public ushort CW; // Control Word
//			public ushort CW_MaskAll;
//			public ushort SW; // Status Word
//			public uint TOP;
//			public ROUNDe Rounding;
//		}

//		private X8086 cpu;
//		private FPUc fpu = new FPUc();

//		public x8087(X8086 cpu)
//		{
//			this.cpu = cpu;
//		}

//		public void Execute(byte opCode, X8086.AddressingMode am)
//		{
//			int opCode2 = cpu.get_RAM8(cpu.Registers.CS, (ushort) (cpu.Registers.IP + 1), (byte) 0, false);

//			// 10/87 instructions implemented

//			if (opCode == ((byte) (0xD8)))
//			{
//				switch (opCode2)
//				{
//					case 0xD1: // FCOM
//						FCOM();
//						break;
//					case 0xD9: // FCOMP
//						FCOM();
//						if (fpu.Tags[TOP] == TAGe.Empty)
//						{
//							Debugger.Break(); // E_Exit("FPU stack underflow")
//						}
//						fpu.Tags[TOP] = TAGe.Empty;
//						TOP = (uint)((TOP + 1) & 7);
//						break;
//				}
//			}
//			else if (opCode == ((byte) (0xD9)))
//			{
//				switch (opCode2)
//				{
//					case 0xD0: // FNOP
//						break;
//					case 0xE0: // FCHS
//						fpu.Regs[TOP].d = -1.0 * fpu.Regs[TOP].d;
//						break;
//					case 0xE1: // FABS
//						fpu.Regs[TOP].d = Math.Abs(fpu.Regs[TOP].d);
//						break;
//					case 0xF0: // F2XM1
//						fpu.Regs[TOP].d = Math.Pow(2.0, fpu.Regs[TOP].d) - 1;
//						break;
//					case 0x3C: // FNSTCW
//					case 0x3E:
//						set_cpu.RAMn(fpu.CW);
//						break;
//				}
//			}
//			else if (opCode == ((byte) (0xDE)))
//			{
//				switch (opCode2)
//				{
//					case 0xC1: // FADD
//						fpu.Regs[TOP].d += fpu.Regs[8].d;
//						break;
//					case 0xC9: // FMUL
//						fpu.Regs[TOP].d *= fpu.Regs[8].d;
//						break;
//				}
//			}
//			else if (opCode == ((byte) (0xDB)))
//			{
//				switch (opCode2)
//				{
//					case 0xE3: // FINIT
//						SetCW((ushort) (0x37F));
//						fpu.SW = (ushort) 0;
//						TOP = GetTOP();
//						for (int i = 0; i <= fpu.Tags.Length - 2; i++)
//						{
//							fpu.Tags[i] = TAGe.Empty;
//						}
//						fpu.Tags[8] = TAGe.Valid; // Is only used by us
//						cpu.Registers.AX = (ushort) 1;
//						break;
//				}
//			}
//		}

//		private void FCOM()
//		{
//			if (((fpu.Tags[TOP] != (int) TAGe.Valid) && (fpu.Tags[TOP] != (int) TAGe.Zero)) ||
//					((fpu.Tags[8] != (int) TAGe.Valid) && (fpu.Tags[8] != (int) TAGe.Zero)))
//					{
//					SetC3((ushort) 1);
//				SetC2((ushort) 1);
//				SetC0((ushort) 1);
//				return;
//			}
//			if (fpu.Regs[TOP].d == fpu.Regs[8].d)
//			{
//				SetC3((ushort) 1);
//				SetC2((ushort) 0);
//				SetC0((ushort) 0);
//				return;
//			}
//			if (fpu.Regs[TOP].d < fpu.Regs[8].d)
//			{
//				SetC3((ushort) 0);
//				SetC2((ushort) 0);
//				SetC0((ushort) 1);
//				return;
//			}
//			SetC3((ushort) 0);
//			SetC2((ushort) 0);
//			SetC0((ushort) 0);
//		}

//#region Helpers
//		private uint STV(int index)
//		{
//			return (fpu.TOP + index) & 0x7;
//		}

//		private uint TOP
//		{
//			get
//			{
//				return fpu.TOP;
//			}
//			set
//			{
//				fpu.TOP = value;
//			}
//		}

//		public void SetTag(ushort tag)
//		{
//			for (int i = 0; i <= fpu.Tags.Length - 1; i++)
//			{
//				fpu.Tags[i] = (TAGe) ((tag >> (2 * i)) & 3);
//			}
//		}

//		public ushort GetTag()
//		{
//			ushort result = (ushort) 0;
//			for (int i = 0; i <= fpu.Tags.Length - 1; i++)
//			{
//				result = result | ((fpu.Tags[i] & 3) << (2 * i));
//			}
//			return result;
//		}

//		public void SetCW(ushort word)
//		{
//			fpu.CW = word;
//			fpu.CW_MaskAll = word & 0x3F;
//			fpu.Rounding = (ROUNDe) ((word >> 10) & 3);
//		}

//		public uint GetTOP()
//		{
//			return (fpu.SW & 0x3800) >> 11;
//		}

//		public void SetTOP(uint value)
//		{
//			fpu.SW = fpu.SW & (~0x3800);
//			fpu.SW = fpu.SW | (value & 7) << 11;
//		}

//		public void SetC0(ushort C)
//		{
//			fpu.SW = fpu.SW & (~0x100);
//			if (C != 0)
//			{
//				fpu.SW = fpu.SW | 0x100;
//			}
//		}

//		public void SetC1(ushort C)
//		{
//			fpu.SW = fpu.SW & (~0x200);
//			if (C != 0)
//			{
//				fpu.SW = fpu.SW | 0x200;
//			}
//		}

//		public void SetC2(ushort C)
//		{
//			fpu.SW = fpu.SW & (~0x400);
//			if (C != 0)
//			{
//				fpu.SW = fpu.SW | 0x400;
//			}
//		}

//		public void SetC3(ushort C)
//		{
//			fpu.SW = fpu.SW & (~0x4000);
//			if (C != 0)
//			{
//				fpu.SW = fpu.SW | 0x4000;
//			}
//		}

//		private uint GetParam32()
//		{
//			return (cpu.get_RAM16(cpu.Registers.CS, cpu.Registers.IP, (byte) 4, false) << 16) | cpu.get_RAM16(cpu.Registers.CS, cpu.Registers.IP, (byte) 0, false);
//		}
//#endregion
//	}

//}
