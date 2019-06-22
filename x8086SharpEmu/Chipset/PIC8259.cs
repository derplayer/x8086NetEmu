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
    public class PIC8259 : IOPortHandler
    {

        private enum States
        {
            Ready = 0,
            ICW1 = 1,
            ICW2 = 2,
            ICW3 = 3,
            ICW4 = 4
        }

        private States state;
        private bool expectICW3;
        private bool expectICW4;

        private PIC8259[] slave = new PIC8259[8];
        private PIC8259 master;
        private byte masterIrq;

        private bool levelTriggered;
        private bool autoEOI;
        private bool autoRotate;
        private byte baseVector;
        private bool specialMask;
        private bool specialNest;
        private bool pollMode;
        private bool readISR;
        private byte lowPrio;
        private byte slaveInput;
        private byte cascadeId;
        private byte rIMR;
        private byte rIRR;
        private byte rISR;

        public class IRQLine : InterruptRequest
        {

            private PIC8259 mPic;
            private byte mIrq;

            public IRQLine(PIC8259 pic, byte irq)
            {
                mPic = pic;
                mIrq = irq;
            }

            public override void Raise(bool enable)
            {
                mPic.RaiseIrq(mIrq, enable);
            }
        }

        public PIC8259(X8086 cpu, PIC8259 master = null)
        {
            if (ReferenceEquals(master, null))
            {
                for (int i = 0x20; i <= 0x2F; i++)
                {
                    ValidPortAddress.Add((uint)i);
                }

                //cascadeId = 0
                //slave(cascadeId) = New PIC8259(cpu, Me)
                //slave(cascadeId).SetMaster(Me, 2)
            }
            else
            {
                for (int i = 0x30; i <= 0x3F; i++)
                {
                    ValidPortAddress.Add((uint)i);
                }
            }

            state = States.ICW1;
        }

        public override byte GetPendingInterrupt()
        {
            if ((int)state != (int)States.Ready)
            {
                return (byte)(0xFF);
            }

            // Determine set of pending interrupt requests
            //byte reqMask = rIRR & (!rIMR);
            byte reqMask = (byte)(rIRR & (byte)(~rIMR));
            reqMask = (byte)((!specialNest) ? (reqMask & (byte)(~rISR)) : (reqMask & ((byte)(~rISR) | slaveInput)));
            //if (specialNest)
            //{
            //	reqMask = reqMask & ((!rISR) | slaveInput);
            //}
            //else
            //{
            //	reqMask = reqMask & (!rISR);
            //}

            // Select non-masked request with highest priority
            if (reqMask == 0)
            {
                return (byte)(0xFF);
            }

            int irq = System.Convert.ToInt32((lowPrio + 1) & 7);
            while ((reqMask & (1 << irq)) == 0)
            {
                if (!specialMask && ((rISR & (1 << irq)) != 0))
                {
                    return (byte)(0xFF); // ISR bit blocks all lower-priority requests
                }
                irq = System.Convert.ToInt32((irq + 1) & 7);
            }

            byte irqBit = (byte)(1 << irq);

            // Update controller state
            if (!autoEOI)
            {
                rISR = (byte)(rISR | irqBit);
            }
            if (!levelTriggered)
            {
                //rIRR = rIRR & (!irqBit);
                rIRR &= (byte)(~irqBit);
            }
            if (autoEOI && autoRotate)
            {
                lowPrio = (byte)irq;
            }
            if (master != null)
            {
                UpdateSlaveOutput();
            }

            // Return vector number or pass down to slave controller
            if ((slaveInput & irqBit) != 0 && slave[irq] != null)
            {
                return slave[irq].GetPendingInterrupt();
            }
            else
            {
                return (byte)(baseVector + irq);
            }
        }

        public IRQLine GetIrqLine(byte i)
        {
            return new IRQLine(this, i);
        }

        public void RaiseIrq(byte irq, bool enable)
        {
            if (enable)
            {
                rIRR = (byte)(rIRR | (1 << irq));
            }
            else
            {
                rIRR = (byte)(rIRR & (~(1 << irq)));
            }
            if (master != null)
            {
                UpdateSlaveOutput();
            }
        }

        public override ushort In(uint port)
        {
            if ((port & 1) == 0)
            {
                // A0 = 0
                if (pollMode)
                {
                    byte pi = GetPendingInterrupt();
                    return System.Convert.ToUInt16(pi == 0xFF ? 0 : 0x80 | pi);
                }
                return (readISR ? rISR : rIRR);
            }
            else
            {
                // A0 = 1
                return System.Convert.ToUInt16(rIMR);
            }
        }

        public override void Out(uint port, ushort value)
        {
            if ((port & 1) == 0)
            {
                // A0 = 0
                if ((value & 0x10) != 0)
                {
                    DoICW1((byte)value);
                }
                else if ((value & 0x8) == 0)
                {
                    DoOCW2((byte)value);
                }
                else
                {
                    DoOCW3((byte)value);
                }
            }
            else
            {
                // A0 = 1
                switch (state)
                {
                    case States.ICW2:
                        DoICW2((byte)value);
                        break;
                    case States.ICW3:
                        DoICW3((byte)value);
                        break;
                    case States.ICW4:
                        DoICW4((byte)value);
                        break;
                    default:
                        DoOCW1((byte)value);
                        break;
                }
            }
        }

        private void UpdateSlaveOutput()
        {
            //int reqmask = rIRR & (!rIMR);
            int reqmask = rIRR & (byte)(~rIMR);
            if (!specialMask)
            {
                //reqmask = reqmask & (!rISR);
                reqmask &= (byte)(~rISR);
            }
            if (master != null)
            {
                master.RaiseIrq(masterIrq, reqmask != 0);
            }
        }

        public void SetMaster(PIC8259 pic, byte irq)
        {
            if (master != null)
            {
                master.slave[cascadeId] = null;
            }
            master = pic;
            masterIrq = irq;
            if (master != null)
            {
                master.slave[cascadeId] = this;
            }
        }

        private void DoICW1(byte v)
        {
            state = States.ICW2;
            rIMR = (byte)0;
            rISR = (byte)0;
            specialMask = false;
            specialNest = false;
            autoEOI = false;
            autoRotate = false;
            pollMode = false;
            readISR = false;
            lowPrio = (byte)7;
            slaveInput = (byte)0;
            if (master != null)
            {
                master.slave[cascadeId] = null;
            }
            cascadeId = (byte)7;
            if (master != null)
            {
                master.slave[cascadeId] = this;
            }
            levelTriggered = (v & 0x8) != 0;
            expectICW3 = (v & 0x2) == 0;
            expectICW4 = (v & 0x1) != 0;
            if (master != null)
            {
                UpdateSlaveOutput();
            }
        }

        private void DoICW2(byte v)
        {
            baseVector = (byte)(v & 0xF8);
            state = expectICW3 ? (expectICW4 ? States.ICW4 : States.Ready) : States.ICW3;
        }

        private void DoICW3(byte v)
        {
            slaveInput = v;
            if (master != null)
            {
                master.slave[cascadeId] = null;
            }
            cascadeId = (byte)(v & 0x7);
            if (master != null)
            {
                master.slave[cascadeId] = this;
            }
            state = expectICW4 ? States.ICW4 : States.Ready;
        }

        private void DoICW4(byte v)
        {
            specialNest = (v & 0x10) != 0;
            autoEOI = (v & 0x2) != 0;
            state = States.Ready;
        }

        private void DoOCW1(byte v)
        {
            rIMR = v;
            if (master != null)
            {
                UpdateSlaveOutput();
            }
        }

        private void DoOCW2(byte v)
        {
            byte irq = (byte)(v & 0x7);
            bool rotate = (v & 0x80) != 0;
            bool specific = (v & 0x40) != 0;
            bool eoi = (v & 0x20) != 0;

            // Resolve non-specific EOI
            if (!specific)
            {
                //byte m = System.Convert.ToByte(specialMask ? (rISR & (!rIMR)) : rISR);
                byte m = (byte)(specialMask ? (rISR & (byte)(~rIMR)) : rISR);
                if (m != 0)
                {
                    byte i = lowPrio;
                    do
                    {
                        i = System.Convert.ToByte((i + 1) & 7);
                        if ((m & (1 << i)) != 0)
                        {
                            irq = i;
                            break;
                        }
                    } while (i != lowPrio);
                }
            }

            if (eoi)
            {
                rISR = (byte)(rISR & (~(1 << irq)));
                if (master != null)
                {
                    UpdateSlaveOutput();
                }
            }

            if (!eoi && !specific)
            {
                autoRotate = rotate;
            }
            else if (rotate)
            {
                lowPrio = irq;
            }
        }

        private void DoOCW3(byte v)
        {
            if ((v & 0x40) != 0)
            {
                specialMask = (v & 0x20) != 0;
                if (master != null)
                {
                    UpdateSlaveOutput();
                }
            }

            pollMode = (v & 0x4) != 0;
            if ((v & 0x2) != 0)
            {
                readISR = (v & 0x1) != 0;
            }
        }

        public override string Name
        {
            get
            {
                return "8259";
            }
        }

        public override string Description
        {
            get
            {
                return "8259 Programmable Interrupt Controller";
            }
        }

        public override void Run()
        {
        }
    }

}
