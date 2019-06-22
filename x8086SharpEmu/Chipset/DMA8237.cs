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
    public class DMAI8237 : IOPortHandler
    {

        // Switches between low/high byte of address and count registers
        private bool msbFlipFlop;

        // Command register (8-bit)
        private int cmdReg;

        // Status register (8-bit)
        private int statusReg;

        // Temporary register (8-bit)
        private int tempReg;

        // Bitmask of active software DMA requests (bits 0-3)
        private int reqReg;

        // Mask register (bits 0-3)
        public int MaskReg;

        // Channel with highest priority
        private int prioChannel;

        // Four DMA channels in the I8237 chip
        private readonly Channel[] channels;

        // CPU
        private X8086 cpu;

        // True if the background task is currently scheduled
        private bool pendingTask;

        // Channel 0 DREQ trigger period for lazy simulation
        private long ch0TriggerPeriod;

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

        // Scheduler time stamp for the first channel 0 DREQ trigger
        // that has not yet been accounted for, or -1 to disable
        private long ch0NextTrigger;

        public class Channel : IDMAChannel
        {

            // Base address register (16-bit)
            public int BaseAddress;

            // Base count register (16-bit)
            public int BaseCount;

            // Current address register (16-bit)
            public int CurrentAddress;

            // Current count register (16-bit)
            public int CurrentCount;

            // Mode register (bits 2-7)
            public int Mode;

            public int Masked;
            public int Direction;
            public int AutoInit;
            public int WriteMode;

            // Page (address bits 16 - 23) for this channel.
            public int Page;

            // Device with which this channel is currently associated.
            public IDMADevice Device;

            // True if DREQ is active for this channel.
            public bool PendingRequest;

            // True if the device signaled external EOP.
            public bool ExternalEop;

            private DMAI8237 PortDevice;

            // Constructs channel.
            public Channel(DMAI8237 dmaDev)
            {
                this.PortDevice = dmaDev;
            }

            public void DMAEOP()
            {
                ExternalEop = true;
            }

            public void DMARequest(bool enable)
            {
                PendingRequest = enable;
                PortDevice.TryHandleRequest();
            }
        }

        public override void Run()
        {
            pendingTask = false;
            TryHandleRequest();
        }

        public DMAI8237(X8086 cpu)
        {
            task = new TaskSC(this);
            this.cpu = cpu;
            channels = new Channel[5];
            for (int i = 0; i <= 4 - 1; i++)
            {
                channels[i] = new Channel(this);
            }
            MaskReg = 0xF; //  mask all channels
            ch0NextTrigger = -1;

            for (uint i = 0x0; i <= 0xF; i++)
            {
                ValidPortAddress.Add(i);
            }

            for (uint i = 0x80; i <= 0x8F; i++)
            {
                ValidPortAddress.Add(i);
            }
        }

        public IDMAChannel GetChannel(int channelNumber)
        {
            return channels[channelNumber];
        }

        // Binds a device to a DMA channel.
        // @param change DMA channel to use (0 ... 3)
        // @param dev    device object to use for callbacks on this channel
        // @return the DmaChannel object
        public IDMAChannel BindChannel(int channelNumber, IDMADevice dev)
        {
            if (channelNumber == 0)
            {
                throw (new ArgumentException("Can not bind DMA channel 0"));
            }
            channels[channelNumber].Device = dev;
            channels[channelNumber].PendingRequest = false;
            channels[channelNumber].ExternalEop = false;
            return channels[channelNumber];
        }

        // Changes the DREQ trigger period for channel 0.
        //@param period trigger period in nanoseconds, or 0 to disable
        public void SetCh0Period(long period)
        {
            UpdateCh0();
            ch0TriggerPeriod = period;
            if (ch0NextTrigger == -1 && period > 0)
            {
                ch0NextTrigger = System.Convert.ToInt64(cpu.Sched.CurrentTime + period);
            }
        }

        // Updates the lazy simulation of the periodic channel 0 DREQ trigger.
        protected void UpdateCh0()
        {
            // Figure out how many channel 0 DREQ triggers have occurred since
            // the last update, and update channel 0 status to account for
            // these triggers.

            long t = cpu.Sched.CurrentTime;
            long ntrigger = 0;
            if (ch0NextTrigger >= 0 && ch0NextTrigger <= t)
            {
                // Rounding errors cause some divergence between DMA channel 0 and
                // timer channel 1, but probably nobody will notice.
                if (ch0TriggerPeriod > 0)
                {
                    long d = System.Convert.ToInt64(t - ch0NextTrigger);
                    ntrigger = System.Convert.ToInt64(1 + System.Convert.ToInt32((double)d / ch0TriggerPeriod));
                    ch0NextTrigger = t + ch0TriggerPeriod - (d % ch0TriggerPeriod);
                }
                else
                {
                    ntrigger = 1;
                    ch0NextTrigger = -1;
                }
            }

            if (ntrigger == 0)
            {
                return;
            }

            // Ignore triggers if DMA controller is disabled
            if ((cmdReg & 0x4) != 0)
            {
                return;
            }

            // Ignore triggers if channel 0 is masked
            if ((MaskReg & 1) == 1)
            {
                return;
            }

            // The only sensible mode for channel 0 in a PC is
            // auto-initialized single read mode, so we simply assume that.

            // Update count, address and status registers to account for
            // the past triggers.
            int addrstep = System.Convert.ToInt32(((cmdReg & 0x2) == 0) ? (((channels[0].Mode & 0x20) == 0) ? 1 : -1) : 0);
            if (ntrigger <= channels[0].CurrentCount)
            {
                // no terminal count
                int n = (int)ntrigger;
                channels[0].CurrentCount -= n;
                channels[0].CurrentAddress = System.Convert.ToInt32((channels[0].CurrentAddress + n * addrstep) & 0xFFFF);
            }
            else
            {
                // terminal count occurred
                int n = System.Convert.ToInt32(System.Convert.ToInt32(System.Convert.ToInt32(ntrigger - channels[0].CurrentCount) - 1) % (channels[0].BaseCount + 1));
                channels[0].CurrentCount = channels[0].BaseCount - n;
                channels[0].CurrentAddress = System.Convert.ToInt32((channels[0].BaseAddress + n * addrstep) & 0xFFFF);
                statusReg = statusReg | 1;
            }
        }

        protected void TryHandleRequest()
        {
            int i = 0;

            // Update request bits in status register
            int rbits = reqReg;
            for (i = 0; i <= 4 - 1; i++)
            {
                if (channels[i].PendingRequest)
                {
                    rbits = rbits | (1 << i);
                }
            }
            statusReg = System.Convert.ToInt32((statusReg & 0xF) | (rbits << 4));

            // Don't start a transfer during dead time after a previous transfer
            if (pendingTask)
            {
                return;
            }

            // Don't start a transfer if the controller is disabled
            if ((cmdReg & 0x4) != 0)
            {
                return;
            }

            // Select a channel with pending request
            rbits = rbits & (~MaskReg);
            rbits = rbits & (~1); // never select channel 0
            if (rbits == 0)
            {
                return;
            }

            i = prioChannel;
            while (((rbits >> i) & 1) == 0)
            {
                i = System.Convert.ToInt32((i + 1) & 3);
            }

            // Just decided to start a transfer on channel i
            Channel chan = channels[i];
            IDMADevice dev = chan.Device;
            int mode = chan.Mode;
            int page = chan.Page;

            // Update dynamic priority
            if ((cmdReg & 10) != 0)
            {
                prioChannel = System.Convert.ToInt32((i + 1) & 3);
            }

            // Block further transactions until this one completes
            pendingTask = true;
            long transferTime = 0;

            if ((mode & 0xC0) == 0xC0)
            {
                //log.warn("cascade mode not implemented (channel " + i + ")")
                Debugger.Break();
            }
            else if ((mode & 0xC) == 0xC)
            {
                //log.warn("invalid mode on channel " + i)
            }
            else
            {
                // Prepare for transfer
                bool blockmode = (mode & 0xC0) == 0x80;
                bool singlemode = (mode & 0xC0) == 0x40;
                int curcount = chan.CurrentCount;
                int maxlen = curcount + 1;
                int curaddr = chan.CurrentAddress;
                int addrstep = System.Convert.ToInt32(((chan.Mode & 0x20) == 0) ? 1 : -1);
                chan.ExternalEop = false;

                // Don't combine too much single transfers in one atomic action
                if (singlemode && maxlen > 25)
                {
                    maxlen = 25;
                }

                // Execute transfer
                switch (mode & 0xC)
                {
                    case 0x0:
                        // DMA verify
                        curcount -= maxlen;
                        curaddr = System.Convert.ToInt32((curaddr + maxlen * addrstep) & 0xFFFF);
                        transferTime += System.Convert.ToInt64(3 * maxlen * Scheduler.BASECLOCK / cpu.Clock);
                        break;
                    case 0x4:
                        // DMA write
                        while ((maxlen > 0) && (!chan.ExternalEop) && (blockmode || chan.PendingRequest))
                        {
                            if (dev != null)
                            {
                                cpu.Memory[(page << 16) | curaddr] = dev.DMAWrite();
                            }
                            maxlen--;
                            curcount--;
                            curaddr = System.Convert.ToInt32((curaddr + addrstep) & 0xFFFF);
                            transferTime += System.Convert.ToInt64(3 * Scheduler.BASECLOCK / cpu.Clock);
                        }
                        break;
                    case 0x8:
                        // DMA read
                        while (maxlen > 0 && !chan.ExternalEop && (blockmode || chan.PendingRequest))
                        {
                            if (dev != null)
                            {
                                dev.DMARead(cpu.Memory[(page << 16) | curaddr]);
                            }
                            maxlen--;
                            curcount--;
                            curaddr = System.Convert.ToInt32((curaddr + addrstep) & 0xFFFF);
                            transferTime += System.Convert.ToInt64(3 * Scheduler.BASECLOCK / cpu.Clock);
                        }
                        break;
                }

                // Update registers
                bool termcount = curcount < 0;
                chan.CurrentCount = System.Convert.ToInt32(termcount ? 0xFFFF : curcount);
                chan.CurrentAddress = curaddr;

                // Handle terminal count or external EOP
                if (termcount || chan.ExternalEop)
                {
                    if ((mode & 0x10) == 0)
                    {
                        // Set mask bit
                        MaskReg = MaskReg | (1 << i);
                    }
                    else
                    {
                        // Auto-initialize
                        chan.CurrentCount = chan.BaseCount;
                        chan.CurrentAddress = chan.BaseAddress;
                    }
                    // Clear software request
                    reqReg = reqReg & (~(1 << i));
                    // Set TC bit in status register
                    statusReg = statusReg | (1 << i);
                }

                // Send EOP to device
                if (termcount && (!chan.ExternalEop) && dev != null)
                {
                    dev.DMAEOP();
                }
            }

            // Schedule a task to run when the simulated DMA transfer completes
            cpu.Sched.RunTaskAfter(task, transferTime);
        }

        public override ushort In(uint port)
        {
            UpdateCh0();
            if ((port & 0xFFF8) == 0)
            {
                // DMA controller: channel status
                Channel chan = channels[(port >> 1) & 3];
                int x = System.Convert.ToInt32(((port & 1) == 0) ? chan.CurrentAddress : chan.CurrentCount);
                bool p = msbFlipFlop;
                msbFlipFlop = !p;
                return (ushort)((p ? ((x >> 8) & 0xFF) : x & 0xFF));
            }
            else if ((port & 0xFFF8) == 0x8)
            {
                // DMA controller: operation registers
                int v = 0;
                if (port == ((uint)8)) // read status register
                {
                    v = statusReg;
                    statusReg = statusReg & 0xF0;
                    return (ushort)v;
                } // read temporary register
                else if (port == ((uint)13))
                {
                    return (ushort)tempReg;
                }
            }

            return (ushort)(0xFF);
        }

        public override void Out(uint port, ushort value)
        {
            UpdateCh0();

            if ((port & 0xFFF8) == 0)
            {
                // DMA controller: channel setup
                Channel chan = channels[(port >> 1) & 3];

                int x = 0;
                int y = 0;

                if ((port & 1) == 0)
                {
                    // base/current address
                    x = chan.BaseAddress;
                    y = chan.CurrentAddress;
                }
                else
                {
                    x = chan.BaseCount;
                    y = chan.CurrentCount;
                }
                bool p = msbFlipFlop;
                msbFlipFlop = !p;
                if (p)
                {
                    x = System.Convert.ToInt32((x & 0xFF) | ((value << 8) & 0xFF00));
                    y = System.Convert.ToInt32((y & 0xFF) | ((value << 8) & 0xFF00));
                }
                else
                {
                    x = System.Convert.ToInt32((x & 0xFF00) | (value & 0xFF));
                    y = System.Convert.ToInt32((y & 0xFF00) | (value & 0xFF));
                }
                if ((port & 1) == 0)
                {
                    chan.BaseAddress = x;
                    chan.CurrentAddress = y;
                }
                else
                {
                    chan.BaseCount = x;
                    chan.CurrentCount = y;
                }
            }
            else if ((port & 0xFFF8) == 0x8)
            {
                // DMA controller: operation registers
                if ((port & 0xF) == ((uint)8)) // write command register
                {
                    cmdReg = value;
                    if ((value & 0x10) == 0)
                    {
                        prioChannel = 0; // enable fixed priority
                    }
                    if ((value & 1) == 1)
                    {
                        cpu.RaiseException("DMA8237: memory-to-memory transfer not implemented");
                    }
                } // set/reset request register
                else if ((port & 0xF) == ((uint)9))
                {
                    if ((value & 4) == 0)
                    {
                        //reqReg = reqReg & (!(1 << (value & 3))); // reset request bit
                        reqReg &= ~(1 << (value & 3));
                    }
                    else
                    {
                        reqReg = reqReg | (1 << (value & 3)); // set request bit
                        if ((value & 7) == 4)
                        {
                            cpu.RaiseException("DMA8237: software request on channel 0 not implemented");
                        }
                    }
                } // set/reset mask register
                else if ((port & 0xF) == ((uint)10))
                {
                    if ((value & 4) == 0)
                    {
                        //MaskReg = MaskReg & (!(1 << (value & 3))); // reset mask bit
                        MaskReg &= ~(1 << (value & 3));
                    }
                    else
                    {
                        MaskReg = MaskReg | (1 << (value & 3)); // set mask bit
                    }
                    channels[value & 3].Masked = System.Convert.ToInt32((value >> 2) & 1);
                } // write mode register
                else if ((port & 0xF) == ((uint)11))
                {
                    channels[value & 3].Mode = value;
                    channels[value & 3].Direction = System.Convert.ToInt32((value >> 5) & 1);
                    channels[value & 3].AutoInit = System.Convert.ToInt32((value >> 4) & 1);
                    channels[value & 3].WriteMode = System.Convert.ToInt32((value >> 2) & 1);
                    if ((value & 3) == 0 && (value & 0xDC) != 0x58)
                    {
                        cpu.RaiseException("DMA8237: unsupported mode on channel 0");
                    }
                } // clear msb flipflop
                else if ((port & 0xF) == ((uint)12))
                {
                    msbFlipFlop = false;
                } // master clear
                else if ((port & 0xF) == ((uint)13))
                {
                    msbFlipFlop = false;
                    cmdReg = 0;
                    statusReg = 0;
                    reqReg = 0;
                    tempReg = 0;
                    MaskReg = 0xF;
                } // clear mask register
                else if ((port & 0xF) == ((uint)14))
                {
                    MaskReg = 0;
                } // write mask register
                else if ((port & 0xF) == ((uint)15))
                {
                    MaskReg = value;
                }
                TryHandleRequest();

            }
            else if ((port & 0xFFF8) == 0x80)
            {
                // DMA page registers
                if (port == ((uint)(0x81)))
                {
                    channels[2].Page = value;
                }
                else if (port == ((uint)(0x82)))
                {
                    channels[3].Page = value;
                }
                else if (port == ((uint)(0x83)))
                {
                    channels[1].Page = value;
                }
                else if (port == ((uint)(0x87)))
                {
                    channels[0].Page = value;
                }
            }
        }

        public override string Description
        {
            get
            {
                return "DMA Controller";
            }
        }

        public override string Name
        {
            get
            {
                return "8237";
            }
        }
    }

    //Public Class DMAI8237
    //    Inherits IOPortHandler

    //    ' Switches between low/high byte of address and count registers
    //    Private msbFlipFlop As Boolean

    //    ' Command register (8-bit)
    //    Private cmdReg As Integer

    //    ' Status register (8-bit)
    //    Private statusReg As Integer

    //    ' Temporary register (8-bit)
    //    Private tempReg As Integer

    //    ' Bitmask of active software DMA requests (bits 0-3)
    //    Private reqReg As Integer

    //    ' Mask register (bits 0-3)
    //    Public MaskReg As Integer

    //    ' Channel with highest priority
    //    Private prioChannel As Integer

    //    ' Four DMA channels in the I8237 chip
    //    Private ReadOnly channels() As Channel

    //    ' CPU
    //    Private cpu As X8086

    //    ' True if the background task is currently scheduled
    //    Private pendingTask As Boolean

    //    ' Channel 0 DREQ trigger period for lazy simulation
    //    Private ch0TriggerPeriod As Long

    //    Private Class TaskSC
    //        Inherits Scheduler.Task

    //        Public Sub New(owner As IOPortHandler)
    //            MyBase.New(owner)
    //        End Sub

    //        Public Overrides Sub Run()
    //            Owner.Run()
    //        End Sub

    //        Public Overrides ReadOnly Property Name As String
    //            Get
    //                Return Owner.Name
    //            End Get
    //        End Property
    //    End Class
    //    Private task As Scheduler.Task = New TaskSC(Me)

    //    ' Scheduler time stamp for the first channel 0 DREQ trigger
    //    ' that has not yet been accounted for, or -1 to disable
    //    Private ch0NextTrigger As Long

    //    Public Class Channel
    //        Implements IDMAChannel

    //        ' Base address register (16-bit)
    //        Public BaseAddress As Integer

    //        ' Base count register (16-bit)
    //        Public BaseCount As Integer

    //        ' Current address register (16-bit)
    //        Public CurrentAddress As Integer

    //        ' Current count register (16-bit)
    //        Public CurrentCount As Integer

    //        ' Mode register (bits 2-7)
    //        Public Mode As Integer

    //        ' Page (address bits 16 - 23) for this channel.
    //        Public Page As Integer

    //        ' Device with which this channel is currently associated.
    //        Public Device As IDMADevice

    //        ' True if DREQ is active for this channel.
    //        Public PendingRequest As Boolean

    //        ' True if the device signaled external EOP.
    //        Public ExternalEop As Boolean

    //        Private PortDevice As DMAI8237

    //        ' Constructs channel.
    //        Public Sub New(dmaDev As DMAI8237)
    //            Me.PortDevice = dmaDev
    //        End Sub

    //        Public Sub DMAEOP() Implements IDMAChannel.DMAEOP
    //            ExternalEop = True
    //        End Sub

    //        Public Sub DMARequest(enable As Boolean) Implements IDMAChannel.DMARequest
    //            PendingRequest = enable
    //            PortDevice.TryHandleRequest()
    //        End Sub
    //    End Class

    //    Public Overrides Sub Run()
    //        pendingTask = False
    //        TryHandleRequest()
    //    End Sub

    //    Public Sub New(cpu As X8086)
    //        Me.cpu = cpu
    //        ReDim channels(4)
    //        For i As Integer = 0 To 4 - 1
    //            channels(i) = New Channel(Me)
    //        Next
    //        MaskReg = &HF '  mask all channels
    //        ch0NextTrigger = -1

    //        For i As UInt32 = &H0 To &HF
    //            ValidPortAddress.Add(i)
    //        Next

    //        For i As UInt32 = &H80 To &H8F
    //            ValidPortAddress.Add(i)
    //        Next
    //    End Sub

    //    Public Function GetChannel(channelNumber As Integer) As IDMAChannel
    //        Return channels(channelNumber)
    //    End Function

    //    ' Binds a device to a DMA channel.
    //    ' @param change DMA channel to use (0 ... 3)
    //    ' @param dev    device object to use for callbacks on this channel
    //    ' @return the DmaChannel object
    //    Public Function BindChannel(channelNumber As Integer, dev As IDMADevice) As IDMAChannel
    //        If channelNumber = 0 Then Throw New ArgumentException("Can not bind DMA channel 0")
    //        channels(channelNumber).Device = dev
    //        channels(channelNumber).PendingRequest = False
    //        channels(channelNumber).ExternalEop = False
    //        Return channels(channelNumber)
    //    End Function

    //    ' Changes the DREQ trigger period for channel 0.
    //    '@param period trigger period in nanoseconds, or 0 to disable
    //    Public Sub SetCh0Period(period As Long)
    //        UpdateCh0()
    //        ch0TriggerPeriod = period
    //        If ch0NextTrigger = -1 AndAlso period > 0 Then ch0NextTrigger = cpu.Sched.CurrentTime + period
    //    End Sub

    //    ' Updates the lazy simulation of the periodic channel 0 DREQ trigger.
    //    Protected Sub UpdateCh0()
    //        ' Figure out how many channel 0 DREQ triggers have occurred since
    //        ' the last update, and update channel 0 status to account for
    //        ' these triggers.

    //        Dim t As Long = cpu.Sched.CurrentTime
    //        Dim ntrigger As Long = 0
    //        If ch0NextTrigger >= 0 AndAlso ch0NextTrigger <= t Then
    //            ' Rounding errors cause some divergence between DMA channel 0 and
    //            ' timer channel 1, but probably nobody will notice.
    //            If ch0TriggerPeriod > 0 Then
    //                Dim d As Long = t - ch0NextTrigger
    //                ntrigger = 1 + d / ch0TriggerPeriod
    //                ch0NextTrigger = t + ch0TriggerPeriod - (d Mod ch0TriggerPeriod)
    //            Else
    //                ntrigger = 1
    //                ch0NextTrigger = -1
    //            End If
    //        End If

    //        If ntrigger = 0 Then Exit Sub

    //        ' Ignore triggers if DMA controller is disabled
    //        If (cmdReg And &H4) <> 0 Then Exit Sub

    //        ' Ignore triggers if channel 0 is masked
    //        If (MaskReg And 1) = 1 Then Exit Sub

    //        ' The only sensible mode for channel 0 in a PC is
    //        ' auto-initialized single read mode, so we simply assume that.

    //        ' Update count, address and status registers to account for
    //        ' the past triggers.
    //        Dim addrstep As Integer = If((cmdReg And &H2) = 0, If((channels(0).Mode And &H20) = 0, 1, -1), 0)
    //        If ntrigger <= channels(0).CurrentCount Then
    //            ' no terminal count
    //            Dim n As Integer = CInt(ntrigger)
    //            channels(0).CurrentCount -= n
    //            channels(0).CurrentAddress = (channels(0).CurrentAddress + n * addrstep) And &HFFFF
    //        Else
    //            ' terminal count occurred
    //            Dim n As Integer = CInt((ntrigger - channels(0).CurrentCount - 1) Mod (channels(0).BaseCount + 1))
    //            channels(0).CurrentCount = channels(0).BaseCount - n
    //            channels(0).CurrentAddress = (channels(0).BaseAddress + n * addrstep) And &HFFFF
    //            statusReg = statusReg Or 1
    //        End If
    //    End Sub

    //    Protected Sub TryHandleRequest()
    //        Dim i As Integer

    //        ' Update request bits in status register
    //        Dim rbits As Integer = reqReg
    //        For i = 0 To 4 - 1
    //            If channels(i).PendingRequest Then rbits = rbits Or (1 << i)
    //        Next
    //        statusReg = (statusReg And &HF) Or (rbits << 4)

    //        ' Don't start a transfer during dead time after a previous transfer
    //        If pendingTask Then Exit Sub

    //        ' Don't start a transfer if the controller is disabled
    //        If (cmdReg And &H4) <> 0 Then Exit Sub

    //        ' Select a channel with pending request
    //        rbits = rbits And (Not MaskReg)
    //        rbits = rbits And (Not 1) ' never select channel 0
    //        If rbits = 0 Then Exit Sub

    //        i = prioChannel
    //        While ((rbits >> i) And 1) = 0
    //            i = (i + 1) And 3
    //        End While

    //        ' Just decided to start a transfer on channel i
    //        Dim chan As Channel = channels(i)
    //        Dim dev As IDMADevice = chan.Device
    //        Dim mode As Integer = chan.Mode
    //        Dim page As Integer = chan.Page

    //        ' Update dynamic priority
    //        If (cmdReg And 10) <> 0 Then prioChannel = (i + 1) And 3

    //        ' Block further transactions until this one completes
    //        pendingTask = True
    //        Dim transferTime As Long = 0

    //        If (mode And &HC0) = &HC0 Then
    //            'log.warn("cascade mode not implemented (channel " + i + ")")
    //            Stop
    //        ElseIf (mode And &HC) = &HC Then
    //            'log.warn("invalid mode on channel " + i)
    //        Else
    //            ' Prepare for transfer
    //            Dim blockmode As Boolean = (mode And &HC0) = &H80
    //            Dim singlemode As Boolean = (mode And &HC0) = &H40
    //            Dim curcount As Integer = chan.CurrentCount
    //            Dim maxlen As Integer = curcount + 1
    //            Dim curaddr As Integer = chan.CurrentAddress
    //            Dim addrstep As Integer = If((chan.Mode And &H20) = 0, 1, -1)
    //            chan.ExternalEop = False

    //            ' Don't combine too much single transfers in one atomic action
    //            If singlemode AndAlso maxlen > 25 Then maxlen = 25

    //            ' Execute transfer
    //            Select Case mode And &HC
    //                Case &H0
    //                    ' DMA verify
    //                    curcount -= maxlen
    //                    curaddr = (curaddr + maxlen * addrstep) And &HFFFF
    //                    transferTime += 3 * maxlen * Scheduler.BASECLOCK / cpu.Clock
    //                Case &H4
    //                    ' DMA write
    //                    While (maxlen > 0) AndAlso (Not chan.ExternalEop) AndAlso (blockmode OrElse chan.PendingRequest)
    //                        If dev IsNot Nothing Then cpu.Memory((page << 16) Or curaddr) = dev.DMAWrite()
    //                        maxlen -= 1
    //                        curcount -= 1
    //                        curaddr = (curaddr + addrstep) And &HFFFF
    //                        transferTime += 3 * Scheduler.BASECLOCK / cpu.Clock
    //                    End While
    //                Case &H8
    //                    ' DMA read
    //                    While maxlen > 0 AndAlso Not chan.ExternalEop AndAlso (blockmode OrElse chan.PendingRequest)
    //                        If dev IsNot Nothing Then dev.DMARead(cpu.Memory((page << 16) Or curaddr))
    //                        maxlen -= 1
    //                        curcount -= 1
    //                        curaddr = (curaddr + addrstep) And &HFFFF
    //                        transferTime += 3 * Scheduler.BASECLOCK / cpu.Clock
    //                    End While
    //            End Select

    //            ' Update registers
    //            Dim termcount As Boolean = curcount < 0
    //            chan.CurrentCount = If(termcount, &HFFFF, curcount)
    //            chan.CurrentAddress = curaddr

    //            ' Handle terminal count or external EOP
    //            If termcount OrElse chan.ExternalEop Then
    //                If (mode And &H10) = 0 Then
    //                    ' Set mask bit
    //                    MaskReg = MaskReg Or (1 << i)
    //                Else
    //                    ' Auto-initialize
    //                    chan.CurrentCount = chan.BaseCount
    //                    chan.CurrentAddress = chan.BaseAddress
    //                End If
    //                ' Clear software request
    //                reqReg = reqReg And (Not 1 << i)
    //                ' Set TC bit in status register
    //                statusReg = statusReg Or (1 << i)
    //            End If

    //            ' Send EOP to device
    //            If termcount AndAlso (Not chan.ExternalEop) AndAlso dev IsNot Nothing Then dev.DMAEOP()
    //        End If

    //        ' Schedule a task to run when the simulated DMA transfer completes
    //        cpu.Sched.RunTaskAfter(task, transferTime)
    //    End Sub

    //    Public Overrides Function [In](port As UInt32) As UInt16
    //        UpdateCh0()
    //        If (port And &HFFF8) = 0 Then
    //            ' DMA controller: channel status
    //            Dim chan As Channel = channels((port >> 1) And 3)
    //            Dim x As Integer = If((port And 1) = 0, chan.CurrentAddress, chan.CurrentCount)
    //            Dim p As Boolean = msbFlipFlop
    //            msbFlipFlop = Not p
    //            Return If(p, (x >> 8) And &HFF, x And &HFF)
    //        ElseIf (port And &HFFF8) = &H8 Then
    //            ' DMA controller: operation registers
    //            Dim v As Integer
    //            Select Case port
    //                Case 8 ' read status register
    //                    v = statusReg
    //                    statusReg = statusReg And &HF0
    //                    Return v
    //                Case 13 ' read temporary register
    //                    Return tempReg
    //            End Select
    //        End If

    //        Return &HFF
    //    End Function

    //    Public Overrides Sub Out(port As UInt32, value As UInt16)
    //        UpdateCh0()

    //        If (port And &HFFF8) = 0 Then
    //            ' DMA controller: channel setup
    //            Dim chan As Channel = channels((port >> 1) And 3)

    //            Dim x As Integer
    //            Dim y As Integer

    //            If (port And 1) = 0 Then
    //                ' base/current address
    //                x = chan.BaseAddress
    //                y = chan.CurrentAddress
    //            Else
    //                x = chan.BaseCount
    //                y = chan.CurrentCount
    //            End If
    //            Dim p As Boolean = msbFlipFlop
    //            msbFlipFlop = Not p
    //            If p Then
    //                x = (x And &HFF) Or ((value << 8) And &HFF00)
    //                y = (y And &HFF) Or ((value << 8) And &HFF00)
    //            Else
    //                x = (x And &HFF00) Or (value And &HFF)
    //                y = (y And &HFF00) Or (value And &HFF)
    //            End If
    //            If (port And 1) = 0 Then
    //                chan.BaseAddress = x
    //                chan.CurrentAddress = y
    //            Else
    //                chan.BaseCount = x
    //                chan.CurrentCount = y
    //            End If
    //        ElseIf (port And &HFFF8) = &H8 Then
    //            ' DMA controller: operation registers
    //            Select Case port And &HF
    //                Case 8 ' write command register
    //                    cmdReg = value
    //                    If (value And &H10) = 0 Then prioChannel = 0 ' enable fixed priority
    //                    If (value And 1) = 1 Then cpu.RaiseException("DMA8237: memory-to-memory transfer not implemented")

    //                Case 9 ' set/reset request register
    //                    If (value And 4) = 0 Then
    //                        reqReg = reqReg And (Not 1 << (value And 3)) ' reset request bit
    //                    Else
    //                        reqReg = reqReg Or (1 << (value And 3))  ' set request bit
    //                        If (value And 7) = 4 Then cpu.RaiseException("DMA8237: software request on channel 0 not implemented")
    //                    End If

    //                Case 10 ' set/reset mask register
    //                    If (value And 4) = 0 Then
    //                        MaskReg = MaskReg And (Not 1 << (value And 3)) ' reset mask bit
    //                    Else
    //                        MaskReg = MaskReg Or (1 << (value And 3))  ' set mask bit
    //                    End If

    //                Case 11 ' write mode register
    //                    channels(value And 3).Mode = value
    //                    If (value And 3) = 0 AndAlso (value And &HDC) <> &H58 Then cpu.RaiseException("DMA8237: unsupported mode on channel 0")

    //                Case 12 ' clear msb flipflop
    //                    msbFlipFlop = False

    //                Case 13 ' master clear
    //                    msbFlipFlop = False
    //                    cmdReg = 0
    //                    statusReg = 0
    //                    reqReg = 0
    //                    tempReg = 0
    //                    MaskReg = &HF

    //                Case 14 ' clear mask register
    //                    MaskReg = 0

    //                Case 15 ' write mask register
    //                    MaskReg = value
    //            End Select
    //            TryHandleRequest()

    //        ElseIf (port And &HFFF8) = &H80 Then
    //            ' DMA page registers
    //            Select Case port
    //                Case &H81 : channels(2).Page = value
    //                Case &H82 : channels(3).Page = value
    //                Case &H83 : channels(1).Page = value
    //                Case &H87 : channels(0).Page = value
    //            End Select
    //        End If
    //    End Sub

    //    Public Overrides ReadOnly Property Description As String
    //        Get
    //            Return "DMA Controller"
    //        End Get
    //    End Property

    //    Public Overrides ReadOnly Property Name As String
    //        Get
    //            Return "8237"
    //        End Get
    //    End Property
    //End Class

}
