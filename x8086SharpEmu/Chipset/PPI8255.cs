using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Xml.Linq;
using System.Collections;

using System.Threading;

using x8086SharpEmu;

namespace x8086SharpEmu
{
    public class PPI8255 : IOPortHandler
    {

        private Scheduler sched;
        private InterruptRequest irq;
        private PIT8254 timer;

        private uint ppiB;
        private string keyBuf;
        private ushort lastKeyCode = (ushort)0;
        private bool keyShiftPending;

        private KeyMap keyMap;
        private bool[] keyUpStates = new bool[16];

        private X8086 cpu;

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

        // Set configuration switch data to be reported by PPI.
        // bit 0: diskette drive present
        // bit 1: math coprocessor present
        // bits 3-2: memory size:
        //   00=256k, 01=512k, 10=576k, 11=640k
        // bits 5-4: initial video mode:
        //   00=EGA/VGA, 01=CGA 40x25, 10=CGA 80x25 color, 11=MDA 80x25
        // bits 7-6: one less than number of diskette drives (1 - 4 drives)
        public byte SwitchData { get; set; }

        public PPI8255(X8086 cpu, InterruptRequest irq)
        {
            task = new TaskSC(this);

            for (int i = 0x60; i <= 0x6F; i++)
            {
                ValidPortAddress.Add((uint)i);
            }

            //PPISystemControl = x8086.WordToBitsArray(&HA5, PPISystemControl.Length)
            //PPI = x8086.WordToBitsArray(&HA, PPISystemControl.Length)
            //PPICommandModeRegister = &H99

            this.cpu = cpu;
            this.sched = cpu.Sched;
            this.irq = irq;
            if (cpu.PIT != null)
            {
                timer = cpu.PIT;
                timer.SetCh2Gate((ppiB & 1) != 0);
            }

            keyBuf = "";
            keyShiftPending = false;
            keyMap = new KeyMap();
        }

        public override string Description
        {
            get
            {
                return "Programmable Peripheral Interface 8255";
            }
        }

        public override string Name
        {
            get
            {
                return "8255";
            }
        }

        public override ushort In(uint port)
        {
            if ((port & 3) == ((uint)0)) // port &h60 (PPI port A)
            {
                // Return keyboard data if bit 7 in port B is cleared.
                return (ushort)(((ppiB & 0x80) == 0) ? (GetKeyData()) : 0);
            } // port &h61 (PPI port B)
            else if ((port & 3) == ((uint)1))
            {
                // Return last value written to the port.
                return (ushort)(ppiB);
            } // port &h62 (PPI port C)
            else if ((port & 3) == ((uint)2))
            {
                return (ushort)(GetStatusByte());
            }
            else
            {
                // Reading from port &h63 is not supported
                return (ushort)(0xFF);
            }
        }

        public override void Out(uint port, ushort value)
        {
            if ((port & 3) == ((uint)1))
            {
                // Write to port 0x61 (system control port)
                // bit 0: gate signal for timer channel 2
                // bit 1: speaker control: 0=disconnect, 1=connect to timer 2
                // bit 3: read low(0) or high(1) nibble of S2 switches
                // bit 4: NMI RAM parity check disable
                // bit 5: NMI I/O check disable
                // bit 6: enable(1) or disable(0) keyboard clock ?
                // bit 7: pulse 1 to reset keyboard and IRQ1
                uint oldv = ppiB;
                ppiB = (uint)(value);
                if ((timer != null) && ((oldv ^ value) & 1) != 0)
                {
                    timer.SetCh2Gate((ppiB & 1) != 0);
#if Win32
					if (timer.Speaker != null)
					{
						timer.Speaker.Enabled = (value & 1) == 1;
					}
#endif
                }
            }
        }

        public override void Run()
        {
            keyShiftPending = false;
            TrimBuffer();
            if (keyBuf.Length > 0 && irq != null)
            {
                irq.Raise(true);
            }
        }

        private void TrimBuffer()
        {
            lock (keyBuf)
            {
                keyBuf = keyBuf.Substring(1);
                Array.Copy(keyUpStates, 1, keyUpStates, 0, keyUpStates.Length - 1);
            }
        }

        // Store a scancode byte in the buffer
        public void PutKeyData(int v, bool isKeyUp)
        {
            if (keyBuf.Length == 16)
            {
                TrimBuffer();
            }

            lock (keyBuf)
            {
                keyBuf = keyBuf + Convert.ToChar(v);
                keyUpStates[keyBuf.Length - 1] = isKeyUp;

                if (keyBuf.Length == 1 && irq != null)
                {
                    irq.Raise(true);
                }
            }
        }

        public bool Reset()
        {
            bool r = false;

            lock (keyBuf)
            {
                if (keyBuf.Length == 0)
                {
                    r = false;
                }
                else
                {
                    keyBuf = "";
                    lastKeyCode = (ushort)(0);
                    keyShiftPending = false;

                    for (int i = 0; i <= keyUpStates.Length - 1; i++)
                    {
                        keyUpStates[i] = false;
                    }

                    r = true;
                }
            }

            return r;
        }

        // Get a scancode byte from the buffer
        public ushort GetKeyData()
        {
            // release interrupt
            if (irq != null)
            {
                irq.Raise(false);
            }
            // if the buffer is empty, we just return the most recent byte

            lock (keyBuf)
            {
                if (keyBuf.Length > 0)
                {
                    // read byte from buffer
                    lastKeyCode = (ushort)(keyMap.GetScanCode(keyBuf[0]) & 0xFF);
                    if (keyUpStates[0])
                    {
                        lastKeyCode = (ushort)(lastKeyCode | 0x80);
                    }

                    // wait .5 msec before going to the next byte
                    if (!keyShiftPending)
                    {
                        keyShiftPending = true;
                        sched.RunTaskAfter(task, 500000);
                    }
                }
            }

            // return scancode byte
            return lastKeyCode;
        }

        // Get status byte for Port C read.
        // bits 3-0: low/high nibble of S2 byte depending on bit 3 of port B
        // bit 4: inverted speaker signal
        // bit 5: timer 2 output status
        // bit 6: I/O channel parity error occurred (we always set it to 0)
        // bit 7: RAM parity error occurred (we always set it to 0)
        private byte GetStatusByte()
        {
            //bool timerout = ReferenceEquals(timer, null) ? 0 : (timer.GetOutput(2));
            bool timerout = Convert.ToBoolean((timer == null) ? ((object)0) : ((object)timer.GetOutput(2)));

            bool speakerout = timerout && ((ppiB & 2) != 0);
            int vh = (int)((speakerout ? 0 : 0x10) | (timerout ? 0x20 : 0));
            int vl = (int)(((ppiB & 0x8) == 0) ? SwitchData : SwitchData >> 4);
            return (byte)((vh & 0xF0) | (vl & 0xF));
        }
    }

}
