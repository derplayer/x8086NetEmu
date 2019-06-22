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
    public class PIT8254 : IOPortHandler
    {

        public class Counter
        {
            // Mode (0..5)
            private int countMode;

            // Count format (1=lsb, 2=msb, 3=lsb+msb)
            private int rwMode;

            // True when counting in BCD instead of binary
            private bool bcdMode;

            // Contents of count register
            private int countRegister;

            // True if next write to count register will set the MSB
            private bool countRegisterMsb;

            // Contents of output latch
            private int outputLatch;

            // True if the output value is latched
            private bool outputLatched;

            // True if the next read from the output latch will get the MSB
            private bool outputLatchMsb;

            // Status latch register
            private int statusLatch;

            // True if the status is latched
            private bool statusLatched;

            // Signal on gate input pin
            private bool mGgate;

            // True if triggered after the last clock was processed
            private bool trigger;

            // Internal counter state (lazy)
            private long timeStamp;
            private int counterValue;
            private bool outputValue;
            private bool nullCount;
            private bool active;

            private PIT8254 owner;

            // Constructs and resets counter
            public Counter(PIT8254 owner)
            {
                this.owner = owner;

                // assume no gate signal
                mGgate = false;
                // set undefined mode
                countMode = -1;
                outputValue = false;
            }

            // Reprograms counter mode
            public void SetMode(int countMode, int countFormat, bool bcdMode)
            {
                // set mode
                this.countMode = countMode;
                this.rwMode = countFormat;
                this.bcdMode = bcdMode;
                // reset registers
                countRegister = 0;
                countRegisterMsb = false;
                outputLatched = false;
                outputLatchMsb = false;
                statusLatched = false;
                // reset internal state
                timeStamp = owner.currentTime;
                counterValue = 0;
                outputValue = countMode == 0 ? false : true;
                nullCount = true;
                trigger = false;
                active = false;
            }

            public void LatchOutput()
            {
                if (countMode >= 0 && !outputLatched)
                {
                    Update();
                    // copy counter value to output latch
                    outputLatch = counterValue;
                    outputLatched = true;
                    outputLatchMsb = false;
                }
            }

            public void LatchStatus()
            {
                if (countMode >= 0 && !statusLatched)
                {
                    Update();
                    // fill status latch register:
                    // bit7   = output
                    // bit6   = null count
                    // bit4-5 = rwMode
                    // bit1-3 = countMode
                    // bit0   = bcdMode
                    //statusLatch = System.Convert.ToInt32((outputValue ? 0x80 : 0x0) || (nullCount ? 0x40 : 0x0) |
                    //	(rwMode << 4) |
                    //	(countMode << 1) || (bcdMode ? 0x1 : 0x0));
                    statusLatch = ((outputValue ? 0x80 : 0) | (nullCount ? 0x40 : 0) | (rwMode << 4) | (countMode << 1) | (bcdMode ? 0x1 : 0x0));
                    statusLatched = true;
                }
            }

            public dynamic GetByte()
            {
                if (countMode < 0)
                {
                    return 0xFF; // undefined state
                }

                if (statusLatched)
                {
                    // read status latch register
                    statusLatched = false;
                    return statusLatch;
                }

                if (!outputLatched)
                {
                    // output latch directly follows counter
                    Update();
                    outputLatch = counterValue;
                }

                // read output latch register
                switch (rwMode)
                {
                    case 1: // LSB only
                        outputLatched = false;
                        return outputLatch & 0xFF;
                    case 2: // MSB only
                        outputLatched = false;
                        return outputLatch >> 8;
                    case 3: // LSB followed by MSB
                        if (outputLatchMsb)
                        {
                            outputLatched = false;
                            outputLatchMsb = false;
                            return outputLatch >> 8;
                        }
                        else
                        {
                            outputLatchMsb = true;
                            return outputLatch & 0xFF;
                        }
                        break;
                    default: // cannot happen
                        throw (new Exception("PIT8254: Invalid GetByte"));
                }
            }

            public void PutByte(int v)
            {
                if (countMode < 0)
                {
                    return; // undefined state
                }

                // write to count register
                switch (rwMode)
                {
                    case 1: // LSB only
                        countRegister = v & 0xFF;
                        ChangeCount();
                        break;
                    case 2: // MSB only
                        countRegister = System.Convert.ToInt32((v << 8) & 0xFF00);
                        ChangeCount();
                        break;
                    case 3: // LSB followed by MSB
                        if (countRegisterMsb)
                        {
                            countRegister = System.Convert.ToInt32((countRegister & 0xFF) | ((v << 8) & 0xFF00));
                            countRegisterMsb = false;
                            ChangeCount();
                        }
                        else
                        {
                            countRegister = System.Convert.ToInt32((countRegister & 0xFF00) | (v & 0xFF));
                            countRegisterMsb = true;
                        }
                        break;
                }
            }

            public bool Gate
            {
                get
                {
                    return mGgate;
                }
                set
                {
                    if (countMode >= 0)
                    {
                        Update();
                    }
                    // trigger on rising edge of the gate signal
                    if (value && (!mGgate))
                    {
                        trigger = true;
                    }
                    mGgate = value;
                    // mode 2 and mode 3: when gate goes low, output
                    // is set high immediately
                    if ((!mGgate) && ((countMode == 2) || (countMode == 3)))
                    {
                        outputValue = true;
                    }
                }
            }

            // Returns current output state
            public bool GetOutput()
            {
                if (countMode >= 0)
                {
                    Update();
                }
                return outputValue;
            }

            // Returns the time when the output state will change,
            // or returns 0 if the output will not change spontaneously.
            public long NextOutputChangeTime()
            {
                if (countMode < 0)
                {
                    return 0;
                }
                int clocks = 0;
                Update();
                switch (countMode)
                {
                    case 0:
                        // output goes high on terminal count
                        if (active && mGgate && (!outputValue))
                        {
                            clocks = System.Convert.ToInt32(FromCounter(counterValue) + (nullCount ? 1 : 0));
                        }
                        break;
                    case 1:
                        // output goes high on terminal count
                        if (!outputValue)
                        {
                            clocks = System.Convert.ToInt32(FromCounter(counterValue) + (trigger ? 1 : 0));
                        }
                        // output goes low on next clock after trigger
                        if (outputValue && trigger)
                        {
                            clocks = 1;
                        }
                        break;
                    case 2:
                        // output goes high on reaching one
                        if (active && mGgate && outputValue)
                        {
                            clocks = System.Convert.ToInt32(FromCounter(counterValue) + (trigger ? 0 : -1));
                        }
                        // strobe ends on next clock
                        if (!outputValue)
                        {
                            clocks = 1;
                        }
                        break;
                    case 3:
                        // trigger pulls output high
                        if ((!outputValue) && trigger)
                        {
                            clocks = 1;
                        }
                        // output goes low on reaching zero
                        if (active && mGgate && outputValue)
                        {
                            clocks = System.Convert.ToInt32((double)FromCounter(counterValue) / 2 + (trigger ? 1 : 0) + (countRegister & 1));
                        }
                        // output goes high on reaching zero
                        if (active && mGgate && (!outputValue) && (!trigger))
                        {
                            clocks = System.Convert.ToInt32((double)FromCounter(counterValue) / 2);
                        }
                        break;
                    case 4:
                        // strobe starts on terminal count
                        if (active && mGgate && outputValue)
                        {
                            clocks = System.Convert.ToInt32(FromCounter(counterValue) + (nullCount ? 1 : 0));
                        }
                        // strobe ends on next clock
                        if (!outputValue)
                        {
                            clocks = 1;
                        }
                        break;
                    case 5:
                        // strobe starts on terminal count
                        if (active && outputValue)
                        {
                            clocks = FromCounter(counterValue);
                        }
                        // strobe ends on next clock
                        if (!outputValue)
                        {
                            clocks = 1;
                        }
                        break;
                }

                if (clocks == 0)
                {
                    return 0;
                }
                else
                {
                    return owner.ClocksToTime(System.Convert.ToInt64(owner.TimeToClocks(owner.currentTime) + clocks));
                }
            }

            // Returns the full period for mode 3 (square wave),
            // or returns 0 in other modes.
            public long GetSquareWavePeriod()
            {
                if ((countMode != 3) || (!active) || (!mGgate))
                {
                    return 0;
                }
                Update();
                return owner.ClocksToTime(FromCounter(countRegister));
            }

            // Returns the full period, or 0 if not enabled.
            public long GetPeriod()
            {
                if ((!active) || (!mGgate))
                {
                    return 0;
                }
                Update();
                return owner.ClocksToTime(FromCounter(countRegister));
            }

            // Converts an internal counter value to a number,
            // wrapping the zero value to the maximum value.
            private int FromCounter(int v)
            {
                if (v == 0)
                {
                    return (bcdMode ? 10000 : 0x10000);
                }
                else if (bcdMode)
                {
                    return ((v >> 12) & 0xF) * 1000 +
                        ((v >> 8) & 0xF) * 100 +
                        ((v >> 4) & 0xF) * 10 +
                        (v & 0xF);
                }
                else
                {
                    return v;
                }
            }

            // Converts a number to an internal counter value,
            // using zero to represent the maximum counter value.
            private int ToCounter(int v)
            {
                if (bcdMode)
                {
                    v = v % 10000;
                    return ((v / 1000) % 10) << 12 |
                        ((v / 100) % 10) << 8 |
                        ((v / 10) % 10) << 4 |
                        (v % 10);
                }
                else
                {
                    return v % 0x10000;
                }
            }

            // Substracts c from the counter and
            // returns true if the zero value was reached.
            private bool CountDown(long c)
            {
                bool zero = false;
                if (bcdMode)
                {
                    int v = System.Convert.ToInt32(((counterValue >> 12) & 0xF) * 1000 +
                        ((counterValue >> 8) & 0xF) * 100 +
                        ((counterValue >> 4) & 0xF) * 10 +
                        (counterValue & 0xF));
                    zero = c >= 10000 || (v != 0 && c >= v);
                    v += System.Convert.ToInt32(10000 - (c % 10000));
                    counterValue = System.Convert.ToInt32(((v / 1000) % 10) << 12 |
                        ((v / 100) % 10) << 8 |
                        ((v / 10) % 10) << 4 |
                        (v % 10));
                }
                else
                {
                    zero = c > 0xFFFF || (counterValue != 0 && c >= counterValue);
                    counterValue = System.Convert.ToInt32((counterValue - c) & 0xFFFF);
                }

                return zero;
            }

            // Recomputes the internal state of the counter at the
            // current time from the last computed state.
            private void Update()
            {
                // compute elapsed clock pulses since last update
                long clocks = System.Convert.ToInt64(owner.TimeToClocks(owner.currentTime) - owner.TimeToClocks(timeStamp));

                // call mode-dependent update function
                switch (countMode)
                {
                    case 0:
                        UpdMode0(clocks);
                        break;
                    case 1:
                        UpdMode1(clocks);
                        break;
                    case 2:
                        UpdMode2(clocks);
                        break;
                    case 3:
                        UpdMode3(clocks);
                        break;
                    case 4:
                        UpdMode4(clocks);
                        break;
                    case 5:
                        UpdMode5(clocks);
                        break;
                }
                // put timestamp on new state
                trigger = false;
                timeStamp = owner.currentTime;
            }

            // MODE 0 - INTERRUPT ON TERMINAL COUNT
            private void UpdMode0(long clocks)
            {
                // init:      output low, stop counter
                // set count: output low, start counter
                // on zero:   output high, counter wraps
                if (active && nullCount)
                {
                    // load counter on next clock after writing
                    counterValue = countRegister;
                    nullCount = false;
                    clocks--;
                }
                if (clocks < 0)
                {
                    return;
                }
                if (active && mGgate)
                {
                    // count down, zero sets output high
                    if (CountDown(clocks))
                    {
                        outputValue = true;
                    }
                }
            }

            // MODE 1 - HARD-TRIGGERED ONE-SHOT
            private void UpdMode1(long clocks)
            {
                // init:      output high, counter running
                // set count: nop
                // trigger:   load counter, output low
                // on zero:   output high, counter wraps
                if (trigger)
                {
                    // load counter on next clock after trigger
                    counterValue = countRegister;
                    nullCount = false;
                    outputValue = false;
                    clocks--;
                }
                // count down, zero sets output high
                if (clocks < 0)
                {
                    return;
                }
                if (CountDown(clocks))
                {
                    outputValue = true;
                }
            }

            // MODE 2 - RATE GENERATOR
            private void UpdMode2(long clocks)
            {
                // init:      output high, stop counter
                // initial c: load and start counter
                // trigger:   reload counter
                // on one:    output strobes low
                // on zero:   reload counter
                if (trigger)
                {
                    // load counter on trigger
                    counterValue = countRegister;
                    nullCount = false;
                    clocks--;
                }
                if (clocks < 0)
                {
                    return;
                }
                if (active && mGgate)
                {
                    // count down
                    int v = FromCounter(counterValue);
                    if (clocks < v)
                    {
                        v -= (int)clocks;
                    }
                    else
                    {
                        // zero reached, reload counter
                        clocks -= v;
                        v = FromCounter(countRegister);
                        v -= (int)(clocks % v);
                        nullCount = false;
                    }
                    counterValue = ToCounter(v);
                }
                // output strobes low on decrement to 1
                outputValue = !mGgate || counterValue != 1;
            }

            // MODE 3 - SQUARE WAVE
            private void UpdMode3(long clocks)
            {
                //  init:      output high, stop counter
                //  initial c: load and start counter
                //  trigger:   reload counter
                //  on one:    switch phase, reload counter
                if (trigger)
                {
                    //  load counter on trigger
                    counterValue = countRegister & (~2);
                    nullCount = false;
                    outputValue = true;
                    clocks--;
                }
                if (clocks < 0)
                {
                    return;
                }
                if (active && mGgate)
                {
                    //  count down
                    int v = FromCounter(counterValue);
                    if ((counterValue == 0) && outputValue && ((countRegister & 1) != 0))
                    {
                        v = 0;
                    }
                    if (System.Convert.ToInt32(2 * clocks) < v)
                    {
                        v -= System.Convert.ToInt32(2 * clocks);
                    }
                    else
                    {
                        //  zero reached, reload counter
                        clocks -= (long)((double)v / 2);
                        v = FromCounter(countRegister);
                        int c = (int)(clocks % v);
                        v = v & (~2);
                        nullCount = false;
                        if (!outputValue)
                        {
                            //  zero reached in low phase
                            //  switch to high phase
                            outputValue = true;
                            //  continue counting
                            if (2 * c < v)
                            {
                                v -= 2 * c;
                                counterValue = ToCounter(v);
                                return;
                            }
                            c -= System.Convert.ToInt32((double)v / 2);
                        }
                        //  zero reached in high phase
                        if ((countRegister & 1) != 0)
                        {
                            //  wait one more clock
                            if (clocks == 0)
                            {
                                counterValue = 0;
                                return;
                            }
                            clocks--;
                        }
                        //  switch to low phase
                        outputValue = false;
                        //  continue counting
                        if (2 * c >= v)
                        {
                            //  zero reached again
                            c -= System.Convert.ToInt32((double)v / 2);
                            //  switch to high phase
                            outputValue = true;
                        }
                        //  continue counting
                        v -= 2 * c;
                    }
                    counterValue = ToCounter(v);
                }
            }

            // MODE 4 - SOFT-TRIGGERED STROBE
            private void UpdMode4(long clocks)
            {
                //  init:      output high, counter running
                //  set count: load counter
                //  on zero:   output strobes low, counter wraps
                if (active && nullCount)
                {
                    //  load counter on first clock
                    counterValue = countRegister;
                    nullCount = false;
                    clocks--;
                }
                if (clocks < 0)
                {
                    return;
                }
                if (mGgate)
                {
                    //  count down
                    CountDown(clocks);
                    //  output strobes low on zero
                    outputValue = !active || counterValue != 0;
                }
                else
                {
                    //  end previous strobe
                    outputValue = true;
                }
            }

            // MODE 5 - HARD-TRIGGERED STROBE
            private void UpdMode5(long clocks)
            {
                //  init:      output high, counter running
                //  set count: nop
                //  trigger:   reload counter
                //  on zero:   output strobes low, counter wraps
                outputValue = true;
                if (trigger)
                {
                    //  load counter on trigger
                    counterValue = countRegister;
                    nullCount = false;
                    active = true;
                    clocks--;
                }
                if (clocks < 0)
                {
                    return;
                }
                //  count down
                CountDown(clocks);
                //  output strobes low on zero
                outputValue = !active || counterValue != 0;
            }

            // Called when a new count is written to the Count Register
            private void ChangeCount()
            {
                Update();
                if (countMode == 0)
                {
                    // mode 0 is restarted by writing a count
                    outputValue = false;
                }
                else
                {
                    // modes 2 and 3 are soft-triggered by
                    // writing the initial count
                    if (!active)
                    {
                        trigger = true;
                    }
                }
                nullCount = true;
                // mode 5 is only activated by a trigger
                if (countMode != 5)
                {
                    active = true;
                }
            }
        }

        // Global counter clock rate (1.19318 MHz)
        private long countRate;

        // Three counters in the I8254 chip
        private readonly Counter[] mChannels = new Counter[3];

        // Interrupt request line for channel 0
        private InterruptRequest irq;

        // Speaker Adapter connected to channel 2
        private SpeakerAdpater mSpeaker;

        // Current time mirrored from Scheduler
        private long currentTime;

        private double speakerBaseFrequency;

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
        private Scheduler.Task task;

        public PIT8254(X8086 cpu, InterruptRequest irq)
        {
            task = new TaskSC(this);
            this.cpu = cpu;
            this.irq = irq;
            this.currentTime = cpu.Sched.CurrentTime;

            // construct 3 timer channels
            mChannels[0] = new Counter(this);
            mChannels[1] = new Counter(this);
            mChannels[2] = new Counter(this);

            // gate input for channels 0 and 1 is always high
            mChannels[0].Gate = true;
            mChannels[1].Gate = true;

            for (int i = 0x40; i <= 0x43; i++)
            {
                ValidPortAddress.Add((uint)i);
            }

            UpdateClock();
        }

        public void UpdateClock()
        {
            countRate = (long)((double)Scheduler.BASECLOCK / X8086.KHz * cpu.SimulationMultiplier);
            speakerBaseFrequency = ((double)Scheduler.BASECLOCK / X8086.KHz) * 1000.0 / cpu.SimulationMultiplier;
        }

        public bool GetOutput(int c)
        {
            return mChannels[c].GetOutput();
        }

        public void SetCh2Gate(bool v)
        {
            currentTime = cpu.Sched.CurrentTime;
            mChannels[2].Gate = v;
            UpdateCh2(0);
        }

        public override ushort In(uint port)
        {
            currentTime = cpu.Sched.CurrentTime;
            int c = (int)(port & 3);
            if (c == 3)
            {
                // invalid read
                return (ushort)(0xFF);
            }
            else
            {
                // read from counter
                return mChannels[c].GetByte();
            }
        }

        public override void Out(uint port, ushort value)
        {
            currentTime = cpu.Sched.CurrentTime;
            int c = (int)(port & 3);
            if (c == 3)
            {
                //  write Control Word
                int s = 0;
                c = System.Convert.ToInt32((value >> 6) & 3);
                if (c == 3)
                {
                    //  Read Back command
                    for (int i = 0; i <= 3 - 1; i++)
                    {
                        s = 2 << i;
                        if ((value & (0x10 | s)) == s)
                        {
                            mChannels[i].LatchStatus();
                        }
                        if ((value & (0x20 | s)) == s)
                        {
                            mChannels[i].LatchOutput();
                        }
                    }
                }
                else
                {
                    //  Channel Control Word
                    if ((value & 0x30) == 0)
                    {
                        //  Counter Latch command
                        mChannels[c].LatchOutput();
                    }
                    else
                    {
                        //  reprogram counter mode
                        int countm = System.Convert.ToInt32((value >> 1) & 7);
                        if (countm > 5)
                        {
                            countm = countm & 3;
                        }
                        int rwm = System.Convert.ToInt32((value >> 4) & 3);
                        bool bcdm = (value & 1) != 0;
                        mChannels[c].SetMode(countm, rwm, bcdm);
                        switch (c)
                        {
                            case 0:
                                UpdateCh0();
                                break;
                            case 1:
                                UpdateCh1();
                                break;
                            case 2:
                                UpdateCh2(value);
                                break;
                        }
                    }
                }
            }
            else
            {
                //  write to counter
                mChannels[c].PutByte(value);
                switch (c)
                {
                    case 0:
                        UpdateCh0();
                        break;
                    case 1:
                        UpdateCh1();
                        break;
                    case 2:
                        UpdateCh2(value);
                        break;
                }
            }
        }

        public Counter Channel(int index)
        {
            return mChannels[index];
        }

        private void UpdateCh0()
        {
            // State of channel 0 may have changed
            // Run the IRQ task immediately to take this into account
            task.Cancel();
            task.Start();
        }

        private void UpdateCh1()
        {
            // Notify the DMA controller of the new frequency
            if (cpu.DMA != null)
            {
                cpu.DMA.SetCh0Period(System.Convert.ToInt64(mChannels[1].GetPeriod()));
            }
        }

        private void UpdateCh2(int v)
        {
            //If cpu.PPI IsNot Nothing Then
            //    If cpu.Model = X8086.Models.IBMPC_5150 Then
            //        If v <> 0 Then
            //            cpu.PPI.PortC(0) = cpu.PPI.PortC(0) Or &H20
            //            cpu.PPI.PortC(1) = cpu.PPI.PortC(1) Or &H20
            //        Else
            //            cpu.PPI.PortC(0) = cpu.PPI.PortC(0) And (Not &H20)
            //            cpu.PPI.PortC(1) = cpu.PPI.PortC(1) And (Not &H20)
            //        End If
            //    End If
            //End If

#if Win32
			if (mSpeaker != null)
			{
				long period = System.Convert.ToInt64(mChannels[2].GetSquareWavePeriod());
				if (period == 0)
				{
					mSpeaker.Frequency = 0;
				}
				else
				{
					mSpeaker.Frequency = speakerBaseFrequency / period;
				}
			}
#endif
        }

        public long TimeToClocks(long t)
        {
            return (t / Scheduler.BASECLOCK) * countRate +
                ((t % Scheduler.BASECLOCK) * countRate) / Scheduler.BASECLOCK;
        }

        public long ClocksToTime(long c)
        {
            return (c / countRate) * Scheduler.BASECLOCK +
                ((c % countRate) * Scheduler.BASECLOCK + countRate - 1) / countRate;
        }

        public SpeakerAdpater Speaker
        {
            get
            {
                return mSpeaker;
            }
            set
            {
                mSpeaker = value;
            }
        }

        public override string Description
        {
            get
            {
                return "Programmable Interval Timer";
            }
        }

        public override string Name
        {
            get
            {
                return "8254";
            }
        }

        private bool lastChan0 = false;
        // Scheduled task to drive IRQ 0 based on counter 0 output signal
        public override void Run()
        {
            currentTime = cpu.Sched.CurrentTime;
            // Set IRQ 0 signal equal to counter 0 output
            bool s = System.Convert.ToBoolean(mChannels[0].GetOutput());
            if (s != lastChan0)
            {
                irq.Raise(s);
                lastChan0 = s;
            }

            // reschedule task for next output change
            long t = System.Convert.ToInt64(mChannels[0].NextOutputChangeTime());
            if (t > 0)
            {
                cpu.Sched.RunTaskAt(task, t);
            }

            //cpu.RTC?.Run()
        }
    }

}
