using System.Collections.Generic;
using System;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Xml.Linq;
using System.Collections;


using x8086SharpEmu;
using Assets.CCC.x8086Sharp.UnityHelpers;
using System.Windows.Forms;

namespace x8086SharpEmu
{
    public class MouseAdapter : Adapter, IExternalInputHandler
    {

        private class SerialMouse
        {
            public uint[] reg = new uint[8];
            public uint[] buf = new uint[16];
            public uint bufPtr;
        }

        private SerialMouse sm = new SerialMouse();
        private PIC8259.IRQLine irq;

        public Point MidPoint { get; set; }
        public bool IsCaptured { get; set; }

        //private const int M = Strings.Asc("M");
        private const int M = 77; //M as ascii

        public MouseAdapter(X8086 cpu) : base(cpu)
        {
            if (base.CPU.PIC != null)
            {
                irq = base.CPU.PIC.GetIrqLine((byte)4);
            }

            for (uint i = 0x3F8; i <= 0x3F8 + 7; i++)
            {
                ValidPortAddress.Add(i);
            }
        }

        public override ushort In(uint port)
        {
            int tmp = 0;

            if (port == ((uint)(0x3F8))) // Transmit/Receive Buffer
            {
                if (irq != null)
                {
                    tmp = (int)(sm.buf[0]);
                    Array.Copy(sm.buf, 1, sm.buf, 0, 15);
                    sm.bufPtr = (uint)((sm.bufPtr - 1) & 0xF);

                    if (sm.bufPtr < 0)
                    {
                        sm.bufPtr = (uint)0;
                    }
                    if (sm.bufPtr > 0)
                    {
                        irq.Raise(true);
                    }

                    sm.reg[4] = (uint)((~sm.reg[4]) & 1);
                }

                return (ushort)tmp;
            } // Line Status Register - LSR
            else if (port == ((uint)(0x3FD)))
            {
                tmp = (int)(sm.bufPtr > 0 ? 1 : 0);

                return (ushort)tmp;
                //Return &H60 Or tmp
                //Return &H1
            }

            return (ushort)(sm.reg[port & 7]);
        }

        public override void Out(uint port, ushort value)
        {
            int oldReg = (int)(sm.reg[port & 7]);
            sm.reg[port & 7] = (uint)(value);

            if (port == ((uint)(0x3FC))) // Modem Control Register - MCR
            {
                if ((value & 1) != (oldReg & 1)) // Software toggling of this register
                {
                    sm.bufPtr = (uint)0; //                       causes the mouse to reset and fill the buffer
                    BufSerMouseData((byte)M); //                  with a bunch of ASCII 'M' characters.
                    BufSerMouseData((byte)M); //                  this is intended to be a way for
                    BufSerMouseData((byte)M); //                  drivers to verify that there is
                    BufSerMouseData((byte)M); //                  actually a mouse connected to the port.
                    BufSerMouseData((byte)M);
                }
                base.CPU.Flags.OF = (byte)1;
            }
        }

        private void BufSerMouseData(byte value)
        {
            if (irq != null)
            {
                if (sm.bufPtr == 16)
                {
                    return;
                }
                if (sm.bufPtr == 0)
                {
                    irq.Raise(true);
                }

                sm.buf[sm.bufPtr] = (uint)(value);
                sm.bufPtr++;
            }
        }

        public void HandleInput(ExternalInputEvent e)
        {
            MouseEventArgs m = (MouseEventArgs)e.TheEvent;

            Point p = new Point(m.X - MidPoint.X, m.Y - MidPoint.Y);
            //If p.X <> 0 Then If p.X > 0 Then p.X = 1 Else p.X = -1
            //If p.Y <> 0 Then If p.Y > 0 Then p.Y = 1 Else p.Y = -1

            p.X = (int)(Math.Ceiling((double)Math.Abs(p.X) / 5) * Math.Sign(p.X));
            p.Y = (int)(Math.Ceiling((double)Math.Abs(p.Y) / 5) * Math.Sign(p.Y));

            byte highbits = (byte)0;
            if (p.X < 0)
            {
                highbits = (byte)3;
            }
            if (p.Y < 0)
            {
                highbits = (byte)(highbits | 0xC);
            }

            byte btns = (byte)0;
            if (((int)(m.Button) & (int)MouseButtons.Left) == (int)MouseButtons.Left)
            {
                btns = (byte)(btns | 2);
            }
            if (((int)(m.Button) & (int)MouseButtons.Right) == (int)MouseButtons.Right)
            {
                btns = (byte)(btns | 1);
            }

            BufSerMouseData((byte)(0x40 | (btns << 4) | highbits));
            BufSerMouseData((byte)(p.X & 0x3F));
            BufSerMouseData((byte)(p.Y & 0x3F));
        }

        public override void CloseAdapter()
        {

        }

        public override void InitiAdapter()
        {

        }

        public override void Run()
        {

        }

        public override string Name
        {
            get
            {
                return "Mouse";
            }
        }

        public override string Description
        {
            get
            {
                return "Serial Mouse at COM1";
            }
        }

        public override Adapter.AdapterType Type
        {
            get
            {
                return AdapterType.SerialMouseCOM1;
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
                return 1;
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
                return 0;
            }
        }
    }
}
