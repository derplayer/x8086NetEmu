using System.Collections.Generic;
using System;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Xml.Linq;
using System.Collections;


using x8086SharpEmu;

namespace x8086SharpEmu
{
    public class RTC : IOPortHandler
    {

        private readonly InterruptRequest irq;

        private delegate int ReadFunction();
        private delegate void WriteFunction(int v);

        private int index;

        private int cmosA = 0x26;
        private int cmosB = 0x2;
        private int cmosC = 0;
        private int cmosD = 0;
        private int[] cmosData = new int[128];

        private long periodicInt;
        private long nextInt;
        private long lastUpdate;
        private long ticks;

        private const int baseFrequency = (int)(32.768 * X8086.KHz);

        private class TaskSC : Scheduler.Task
        {

            public TaskSC(IOPortHandler owner) : base(owner)
            {
            }

            public override void Run()
            {
                Owner.Run();
            }

            public override string Name
            {
                get
                {
                    return Owner.Name;
                }
            }
        }
        private Scheduler.Task task;// = new TaskSC(this);

        public RTC(X8086 cpu, InterruptRequest irq)
        {
            task = new TaskSC(this);
            this.irq = irq;

            for (int i = 0x70; i <= 0x71; i++)
            {
                ValidPortAddress.Add((uint)i);
            }

            for (int i = 0x240; i <= 0x24F; i++)
            {
                ValidPortAddress.Add((uint)i);
            }

            // FIXME: Although this works, when pausing the emulation causes the internal timers to get out of sync:
            // The contents at 46C no longer reflect what's returned by INT 1A, 02
            // So the x8086.Resume method should perform a re-sync setting the new tick values into 46C.
            // It also appears that the x8086.Resume method should also advance the time...

            cpu.TryAttachHook((byte)(0x8), new X8086.IntHandler(() =>
           {
               uint ticks = (uint)((DateTime.Now - new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0)).Ticks / 10000000 * 18.206);
                //cpu.RAM16[0x40, 0x6E] = (ticks >> 16) & 0xFFFF;
                //cpu.RAM16[0x40, 0x6C] = ticks & 0xFFFF;
                //cpu.RAM8[0x40, 0x70] = 0;
                cpu.set_RAM16(0x40, 0x6E, 0x0, false, (ushort)((long)(ticks >> 16) & 0xFFFF));
               cpu.set_RAM16(0x40, 0x6C, 0x0, false, (ushort)((long)(ticks) & 0xFFFF));
               cpu.set_RAM8(0x40, 0x70, 0x0, false, 0x00);

               cpu.TryDetachHook(0x8);
               return false;
           }));

            cpu.TryAttachHook((byte)(0x1A), new X8086.IntHandler(() =>
           {
               switch (cpu.Registers.AH)
               {
                   case 0x2: // Read real time clock time
                        cpu.Registers.CH = (byte)ToBCD((ushort)DateTime.Now.Hour);
                       cpu.Registers.CL = (byte)ToBCD((ushort)DateTime.Now.Minute);
                       cpu.Registers.DH = (byte)ToBCD((ushort)DateTime.Now.Second);
                       cpu.Registers.DL = 0;
                       cpu.Flags.CF = 0;
                       return true;

                   case 0x4: // Read real time clock date
                        cpu.Registers.CH = (byte)ToBCD((ushort)(DateTime.Now.Year / 100));
                       cpu.Registers.CL = (byte)ToBCD((ushort)DateTime.Now.Year);
                       cpu.Registers.DH = (byte)ToBCD((ushort)DateTime.Now.Month);
                       cpu.Registers.DL = (byte)ToBCD((ushort)DateTime.Now.Day);
                       cpu.Flags.CF = 0;
                       return true;

                   default:
                       return false;

               }
           }));
        }

        private ushort EncodeTime(ushort t)
        {
            if ((cmosB & 0x4) != 0)
            {
                return t;
            }
            else
            {
                return ToBCD(t);
            }
        }

        private ushort ToBCD(ushort v)
        {
            //If v >= 100 Then v = v Mod 100
            //Return (v Mod 10) + 16 * (v / 10)

            int i = 0;
            int r = 0;
            int d = 0;

            while (v != 0)
            {
                d = v % 10;
                r = r | (d << (4 * i));
                i++;
                v = (ushort)((v - d) / 10);
            }
            return (ushort)r;
        }

        private ushort FromBCD(ushort v)
        {
            if ((v & 0xF) > 0x9)
            {
                v += (ushort)(0x6);
            }
            if ((v & 0xF0) > 0x90)
            {
                v += (ushort)(0x60);
            }
            return (ushort)((v & 0xF) + 10 * ((ushort)((uint)v >> 4) & 0xF0));
        }

        public override ushort In(uint port)
        {
            switch (index)
            {
                case 0x0:
                    return EncodeTime((ushort)(DateTime.Now.ToUniversalTime().Second));
                case 0x2:
                    return EncodeTime((ushort)(DateTime.Now.ToUniversalTime().Minute));
                case 0x4:
                    return EncodeTime((ushort)(DateTime.Now.ToUniversalTime().Hour));
                case 0x7:
                    return EncodeTime((ushort)(DateTime.Now.ToUniversalTime().Day));
                case 0x8:
                    return EncodeTime((ushort)(DateTime.Now.ToUniversalTime().Month + 1));
                case 0x9:
                    return EncodeTime((ushort)(DateTime.Now.ToUniversalTime().Year % 100));

                case 0xA:
                    return (ushort)cmosA;
                case 0xB:
                    return (ushort)cmosB;
                case 0xC:
                    return (ushort)(cmosC & (~0xF0));
                case 0xD:
                    return (ushort)cmosD;

                case 0x32:
                    return EncodeTime((ushort)(DateTime.Now.ToUniversalTime().Year / 100));
            }

            return (ushort)(cmosData[index]);
        }

        public override void Out(uint port, ushort value)
        {
            if ((port & 1) == 0)
            {
                index = value & 0x7F;
            }
            else
            {
                switch (index)
                {
                    case 0xA:
                        cmosA = value & 0x7F;
                        periodicInt = (long)(1000 / (32768 >> (cmosA & 0xF) - 1));
                        break;
                    case 0xB:
                        cmosB = value;
                        break;
                    case 0xC:
                        cmosC = value;
                        break;
                    case 0xD:
                        cmosD = value;
                        break;
                    default:
                        cmosData[index] = value;
                        break;
                }
            }
            cmosData[index] = value;
        }

        public override string Name
        {
            get
            {
                return "RTC";
            }
        }

        public override string Description
        {
            get
            {
                return "Real Time Clock";
            }
        }

        public override void Run()
        {
            Debugger.Break();
        }
    }

}
