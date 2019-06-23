using System.Collections.Generic;
using System;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Xml.Linq;
using System.Collections;

using System.Threading;

using x8086SharpEmu;
using System.Windows.Forms;

namespace x8086SharpEmu
{

    // This code is a port from the "Scheduler" class in Retro 0.4

    public class Scheduler : IExternalInputHandler
    {

        private const long NOTASK = long.MaxValue;
        private const long STOPPING = long.MinValue;

        // Number of scheduler time units per simulated second (~1.0 GHz)
        public const long BASECLOCK = (long)(1.19318 * X8086.GHz);

        // Current simulation time in scheduler time units (ns)
        private long mCurrentTime;

        // Scheduled time of next event, or NOTASK or STOPPING
        private long nextTime;

        // Enables slowing the simulation to keep it in sync with wall time
        private bool syncScheduler = true;

        // Determines how often the time synchronization is checked
        private long syncQuantum;

        // Determines speed of the simulation
        private long syncSimTimePerWallMs;

        // Gain on wall time since last synchronization, plus one syncQuantum
        private long syncTimeSaldo;

        // Most recent value of <code>currentTimeMillis</code>
        private long syncWallTimeMillis;

        // Queue containing pending synchronous events
        private PriorityQueue pq;

        // Ordered list of pending asynchronous events (external input events)
        public ArrayList pendingInput;

        // The CPU component controlled by this Scheduler
        private X8086 mCPU;

        private Thread loopThread;

        private bool isCtrlDown;
        private bool isAltDown;
        private int cadCounter;

        // The dispatcher for external input events
        //Private inputHandler As ExternalInputHandler

        // A Task represents a pending discrete event, and is queued for
        // execution at a particular point in simulated time.
        public abstract class Task : Runnable
        {
            public static long NOSCHED = long.MinValue;
            public long LastTime { get; set; }
            public long NextTime { get; set; }
            public long Interval { get; set; }

            //Private mThread As Thread
            private IOPortHandler mOwner;

            public IOPortHandler Owner
            {
                get
                {
                    return mOwner;
                }
            }

            public Task(IOPortHandler owner)
            {
                mOwner = owner;
                LastTime = NOSCHED;
                NextTime = NOSCHED;
                Interval = 0;
            }

            public bool Cancel()
            {
                if (NextTime == NOSCHED)
                {
                    return false;
                }
                NextTime = NOSCHED;
                Interval = 0;
                return true;
            }

            public long LastExecutionTime()
            {
                return LastTime;
            }

            public void Start()
            {
                this.Run();
            }

            public abstract override void Run();
        }

        public Scheduler(X8086 cpu)
        {
            mCPU = cpu;
            pq = new PriorityQueue();
            pendingInput = new ArrayList();

            syncQuantum = (long)BASECLOCK / 20;
            syncSimTimePerWallMs = (long)BASECLOCK / 1000;
        }

        public long CurrentTime
        {
            get
            {
                return mCurrentTime;
            }
        }

        public long CurrentTimeMillis
        {
            get
            {
                return (long)((double)DateTime.Now.Ticks / 10000);
            }
        }

        public void SetSynchronization(bool enabled, long quantum, long simTimePerWallMs)
        {
#if DEBUG
            if (enabled && quantum < 1)
            {
                throw (new ArgumentException("Invalid value for quantum"));
            }
            if (enabled && simTimePerWallMs < 1000)
            {
                throw (new ArgumentException("Invalid value for simTimePerWallMs"));
            }
#endif

            syncScheduler = enabled;
            syncQuantum = quantum;
            syncSimTimePerWallMs = simTimePerWallMs;
            syncTimeSaldo = 0;
            syncWallTimeMillis = CurrentTimeMillis;
        }

        public void RunTaskAt(Task tsk, long t)
        {
            lock (tsk)
            {
#if DEBUG
                if (tsk.NextTime != Task.NOSCHED)
                {
                    throw (new Exception("Task already scheduled"));
                }
#endif
                tsk.NextTime = t;
            }

            pq.Add(tsk, t);
            if (t < nextTime)
            {
                nextTime = t;
            }
        }

        public void RunTaskAfter(Task tsk, long d)
        {
            long t = (long)(mCurrentTime + d);

            lock (tsk)
            {
#if DEBUG
                if (tsk.NextTime != Task.NOSCHED)
                {
                    throw (new Exception("Task already scheduled"));
                }
#endif
                tsk.NextTime = t;
            }

            pq.Add(tsk, t);
            if (t < nextTime)
            {
                nextTime = t;
            }
        }

        public void RunTaskEach(Task tsk, long interval)
        {
            long t = (long)(mCurrentTime + interval);

            lock (tsk)
            {
#if DEBUG
                if (tsk.NextTime != Task.NOSCHED)
                {
                    throw (new Exception("Task already scheduled"));
                }
#endif
                tsk.NextTime = t;
                tsk.Interval = interval;
            }

            pq.Add(tsk, t);
            if (t < nextTime)
            {
                nextTime = t;
            }
        }

        public void Stop()
        {
            Task tsk = default(Task);
            do
            {
                tsk = (Task)(pq.RemoveFirst());
                if (ReferenceEquals(tsk, null))
                {
                    break;
                }
                tsk.Cancel();
            } while (true);

            pq.Clear();
            nextTime = STOPPING;
            // Kick simulation thread
            mCPU.DoReschedule = true;
        }

        public long GetTimeToNextEvent()
        {
            if (nextTime == STOPPING || pendingInput.Count != 0)
            {
                return 0;
            }
            else if (syncScheduler && (nextTime > (long)(mCurrentTime + syncQuantum)))
            {
                return syncQuantum;
            }
            else
            {
                return (long)(nextTime - mCurrentTime);
            }
        }

        public void AdvanceTime(long t)
        {
            mCurrentTime += t;
            if (syncScheduler)
            {
                syncTimeSaldo += t;
                if (syncTimeSaldo > (int)(3 * syncQuantum))
                {
                    // Check the wall clock
                    long wallTime = CurrentTimeMillis;
                    long wallDelta = (long)(wallTime - syncWallTimeMillis);
                    syncWallTimeMillis = wallTime;
                    if (wallDelta < 0)
                    {
                        wallDelta = 0; // Some clown has set the system clock back
                    }
                    syncTimeSaldo -= (long)(wallDelta * syncSimTimePerWallMs);
                    if (syncTimeSaldo < 0)
                    {
                        syncTimeSaldo = 0;
                    }
                    if (syncTimeSaldo > (int)(2 * syncQuantum))
                    {
                        // The simulation has gained more than one time quantum
                        int sleepTime = (int)((syncTimeSaldo - syncQuantum) / syncSimTimePerWallMs);
                        if (syncTimeSaldo > (int)(4 * syncQuantum))
                        {
                            // Force a hard sleep
                            int s = (int)((double)syncQuantum / syncSimTimePerWallMs);
                            Thread.Sleep(s);
                            sleepTime -= s;
                        }

                        lock (this)
                        {
                            // Sleep, but wake up on asynchronous events
                            if (pendingInput.Count == 0)
                            {
                                Wait(sleepTime);
                            }
                        }
                    }
                }
            }
        }

        public void SkipToNextEvent()
        {
            if (nextTime <= mCurrentTime || pendingInput.Count != 0)
            {
                return;
            }

            // Detect end of simulation
            if (nextTime == NOTASK)
            {
                nextTime = STOPPING;
            }

            if (syncScheduler)
            {
                if (nextTime != STOPPING)
                {
                    syncTimeSaldo += (long)(nextTime - mCurrentTime);
                }
                if (syncTimeSaldo > (int)(3 * syncQuantum))
                {
                    // Check the wall clock
                    long wallTime = CurrentTimeMillis;
                    long wallDelta = (long)(wallTime - syncWallTimeMillis);
                    syncWallTimeMillis = wallTime;
                    if (wallDelta < 0)
                    {
                        wallDelta = 0; // some clown has set the system clock back
                    }
                    syncTimeSaldo -= (long)(wallDelta * syncSimTimePerWallMs);
                    if (syncTimeSaldo < 0)
                    {
                        syncTimeSaldo = 0;
                    }
                    if (syncTimeSaldo > (int)(2 * syncQuantum))
                    {
                        // Skipping would give a gain of more than one time quantum
                        int sleepTime = (int)((syncTimeSaldo - syncQuantum) / syncSimTimePerWallMs);
                        Wait(sleepTime);
                        if (pendingInput.Count > 0)
                        {
                            // We woke up from our sleep; find out how long
                            //   we slept and how much simulated time has passed
                            wallTime = CurrentTimeMillis;
                            wallDelta = (long)(wallTime - syncWallTimeMillis);
                            syncWallTimeMillis = wallTime;
                            if (wallDelta < 0)
                            {
                                wallDelta = 0; // Same clown again
                            }
                            syncTimeSaldo -= (long)(wallDelta * syncSimTimePerWallMs);
                            if (syncTimeSaldo > (int)(syncQuantum + nextTime - mCurrentTime))
                            {
                                // No simulated time passed at all
                                syncTimeSaldo -= (long)(nextTime - mCurrentTime);
                            }
                            else if (syncTimeSaldo > syncQuantum)
                            {
                                // Some simulated time passed, but not enough
                                mCurrentTime = (long)(nextTime - (int)(syncTimeSaldo - syncQuantum));
                                syncTimeSaldo = syncQuantum;
                            }
                            else
                            {
                                // Oops, we even overslept
                                mCurrentTime = nextTime;
                            }
                        }
                        else
                        {
                            // Assume we slept the whole interval
                            mCurrentTime = nextTime;
                        }
                        return;
                    }
                }
            }

            // Skip to the next pending event
            mCurrentTime = nextTime;
        }

        public Task NextTask()
        {
            if (nextTime > mCurrentTime || nextTime == STOPPING)
            {
                return null;
            }

            Task tsk = (Task)(pq.RemoveFirst());
            nextTime = pq.MinPriority();
            if (ReferenceEquals(tsk, null))
            {
                return null;
            }

            lock (tsk)
            {
                if ((tsk.NextTime == Task.NOSCHED) || (tsk.NextTime > mCurrentTime))
                {
                    // Canceled or rescheduled
                    tsk = null;
                }
                else
                {
                    // Task is ok to run
                    tsk.LastTime = tsk.NextTime;
                    if (tsk.Interval > 0)
                    {
                        // Schedule next execution
                        long t = (long)(tsk.NextTime + tsk.Interval);
                        tsk.NextTime = t;
                        pq.Add(tsk, t);
                        if (t < nextTime)
                        {
                            nextTime = t;
                        }
                    }
                    else
                    {
                        // Done with this task
                        tsk.NextTime = Task.NOSCHED;
                    }
                }
            }

            return tsk;
        }

        public void Start()
        {
            mCurrentTime = 0;
            nextTime = NOTASK;
            syncWallTimeMillis = CurrentTimeMillis;
            syncTimeSaldo = 0;

            System.Threading.Tasks.Task.Run((Action)Run);
        }

        private void Run()
        {
            ArrayList cleanInputBuf = new ArrayList();
            ArrayList inputBuf = new ArrayList();
            Task tsk = null;
            ExternalInputEvent evt = default(ExternalInputEvent);

            while (true)
            {
                // Detect the end of the simulation run
                if (nextTime == STOPPING)
                {
                    nextTime = pq.MinPriority();
                    break;
                }

                if (pendingInput.Count > 0)
                {
                    // Fetch pending input events
                    inputBuf = pendingInput;
                    pendingInput = cleanInputBuf;
                }
                else if (nextTime <= mCurrentTime)
                {
                    // Fetch the next pending task
                    tsk = NextTask();
                    if (ReferenceEquals(tsk, null))
                    {
                        continue; // This task was canceled, go round again
                    }
                    inputBuf.Clear();
                }
                else
                {
                    tsk = null;
                }

                if (inputBuf.Count > 0)
                {
                    // Process pending input events
                    for (int i = 0; i <= inputBuf.Count - 1; i++)
                    {
                        evt = (ExternalInputEvent)(inputBuf[i]);
                        evt.TimeStamp = mCurrentTime;
                        evt.Handler.HandleInput(evt);
                    }
                    inputBuf.Clear();
                    cleanInputBuf = inputBuf;
                }
                else if (tsk != null)
                {
                    // Run the first pending task
                    tsk.Start();
                }
                else
                {
                    // Run the CPU simulation for a bit (maxRunCycl)
                    try
                    {
                        mCPU.RunEmulation();
                    }
                    catch (Exception ex)
                    {
                        X8086.Notify("Shit happens at {0}:{1}: {2}", X8086.NotificationReasons.Fck, mCPU.Registers.CS.ToString("X4"), mCPU.Registers.IP.ToString("X4"), ex.Message);
                        mCPU.RaiseException($"Scheduler Main Loop Error: {ex.Message}");
                    }

                    if (mCPU.IsHalted)
                    {
                        SkipToNextEvent(); // The CPU is halted, skip immediately to the next event
                    }
                }
            }
        }

        private void Wait(int delay)
        {
            Monitor.Enter(this);
            Monitor.Wait(this, delay);
            Monitor.Exit(this);
        }

        private void Notify()
        {
            Monitor.Enter(this);
            Monitor.PulseAll(this);
            Monitor.Exit(this);
        }

        public void HandleInput(ExternalInputEvent e)
        {
            if (ReferenceEquals(e.Handler, null))
            {
                return;
            }

            if (e.TheEvent is KeyEventArgs)
            {
                var theEvent = (KeyEventArgs)e.TheEvent;

                if (cadCounter > 0)
                {
                    cadCounter--;
                    return;
                }

                if (((int)(theEvent.Modifiers) & (int)Keys.Control) == (int)Keys.Control)
                {
                    isCtrlDown = !(e.Extra);
                }
                else
                {
                    isCtrlDown = false;
                }
                if (((int)(theEvent.Modifiers) & (int)Keys.Alt) == (int)Keys.Alt)
                {
                    isAltDown = !(e.Extra);
                }
                else
                {
                    isAltDown = false;
                }

                if (isCtrlDown && isAltDown && ((int)(theEvent.KeyCode) & (int)Keys.Insert) == (int)Keys.Insert)
                {
                    cadCounter = 3; // Ignore the next three events, which will be the release of CTRL, ALT and DEL
                    e.TheEvent = new KeyEventArgs(Keys.Delete);
                    X8086.Notify("Sending CTRL+ALT+DEL", X8086.NotificationReasons.Info);
                }
            }

            if (pendingInput.Count == 0)
            {
                // Wake up the scheduler in case it is sleeping
                Notify();
                // Kick the CPU simulation to make it yield
                mCPU.DoReschedule = true;
            }

            pendingInput.Add(e);
        }
    }

    public interface IExternalInputHandler
    {
        void HandleInput(ExternalInputEvent e);
    }

    public class ExternalInputEvent : EventArgs
    {

        public IExternalInputHandler Handler { get; set; }
        public EventArgs TheEvent { get; set; }
        public long TimeStamp { get; set; }
        public bool Extra { get; set; }

        public ExternalInputEvent(IExternalInputHandler handler, EventArgs theEvent)
        {
            this.Handler = handler;
            this.TheEvent = theEvent;
        }

        public ExternalInputEvent(IExternalInputHandler handler, EventArgs theEvent, bool? extra)
        {
            this.Handler = handler;
            this.TheEvent = theEvent;
            this.Extra = (bool)extra;
        }

        //Public Shared Operator =(e1 As ExternalInputEvent, e2 As ExternalInputEvent) As Boolean
        //    Dim e1k = CType(e1.TheEvent, KeyEventArgs)
        //    Dim e2k = CType(e2.TheEvent, KeyEventArgs)
        //    Return e1k.KeyCode = e2k.KeyCode AndAlso
        //            e1k.Modifiers = e2k.Modifiers
        //End Operator

        //Public Shared Operator <>(e1 As ExternalInputEvent, e2 As ExternalInputEvent) As Boolean
        //    Return Not (e1 = e2)
        //End Operator
    }

    public abstract class Runnable
    {
        public abstract string Name { get; }
        public abstract void Run();
    }
}
