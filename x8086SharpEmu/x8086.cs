using System.Collections.Generic;
using System;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Xml.Linq;
using System.Collections;
using System.Threading;

using x8086SharpEmu;
using UnityEngine;

// Map Of Instructions: http://www.mlsite.net/8086/ and http://www.sandpile.org/x86/opc_1.htm
// http://en.wikibooks.org/wiki/X86_Assembly/Machine_Language_Conversion
// http://www.xs4all.nl/~ganswijk/chipdir/iset/8086bin.txt
// The Intel 8086 / 8088/ 80186 / 80286 / 80386 / 80486 Instruction Set: http://zsmith.co/intel.html
// http://www.felixcloutier.com/x86/
// https://c9x.me/x86/

namespace x8086SharpEmu
{

    public partial class X8086
    {
        public enum Models
        {
            IBMPC_5150,
            IBMPC_5160
        }

        private Models mModel = Models.IBMPC_5160;

        private GPRegisters mRegisters = new GPRegisters();
        private GPFlags mFlags = new GPFlags();

        public enum MemHookMode
        {
            Read,
            Write
        }
        //public delegate bool  MemHandler(uint address, ref ushort value, MemHookMode mode);
        public delegate bool MemHandler(uint address, ushort value, MemHookMode mode);
        private readonly List<MemHandler> memHooks = new List<MemHandler>();
        public delegate bool IntHandler();
        private readonly Dictionary<byte, IntHandler> intHooks = new Dictionary<byte, IntHandler>();

        private byte opCode;
        private byte opCodeSize;

        public uint tmpUVal;
        private int tmpVal;

        private AddressingMode addrMode;
        private bool mIsExecuting = false;

        private Thread mipsThread;
        private AutoResetEvent mipsWaiter;
        private uint instrucionsCounter;
        private bool newPrefix = false;
        private int newPrefixLast = 0;

        public enum REPLoopModes
        {
            None,
            REPE,
            REPENE
        }
        private REPLoopModes mRepeLoopMode;

        private ushort forceNewIPAddress;
        private ushort IPAddrOffet
        {
            get
            {
                useIPAddrOffset = false;
                return forceNewIPAddress;
            }
            set
            {
                forceNewIPAddress = value;
                useIPAddrOffset = true;
            }
        }
        private bool useIPAddrOffset;

        public const long KHz = 1000;
        public const long MHz = KHz * KHz;
        public const long GHz = MHz * KHz;
        public static long BASECLOCK = (long)(4.77273 * MHz); // http://dosmandrivel.blogspot.com/2009/03/ibm-pc-design-antics.html
        private long mCyclesPerSecond = (long)(4.77273 * MHz);
        private long clkCyc = 0;

        private bool mDoReSchedule;
        private double mSimulationMultiplier = 1.0;
        private long leftCycleFrags;

        private bool cancelAllThreads;
        private AutoResetEvent debugWaiter;

        //Private trapEnabled As Boolean
        private static bool ignoreINTs;

        public Scheduler Sched;
        public DMAI8237 DMA;
        public PIC8259 PIC;
        public PIT8254 PIT;
        public PPI8255 PPI;
        //Public PPI As PPI8255_ALT
        public RTC RTC;
        //public x8087 FPU;
        public object FPU = null; //FPU not ported for now

        private bool picIsAvailable;

        public delegate void EmulationTerminatedEventHandler();
        private EmulationTerminatedEventHandler EmulationTerminatedEvent;

        public event EmulationTerminatedEventHandler EmulationTerminated
        {
            add
            {
                EmulationTerminatedEvent = (EmulationTerminatedEventHandler)System.Delegate.Combine(EmulationTerminatedEvent, value);
            }
            remove
            {
                EmulationTerminatedEvent = (EmulationTerminatedEventHandler)System.Delegate.Remove(EmulationTerminatedEvent, value);
            }
        }

        public delegate void EmulationHaltedEventHandler();
        private EmulationHaltedEventHandler EmulationHaltedEvent;

        public event EmulationHaltedEventHandler EmulationHalted
        {
            add
            {
                EmulationHaltedEvent = (EmulationHaltedEventHandler)System.Delegate.Combine(EmulationHaltedEvent, value);
            }
            remove
            {
                EmulationHaltedEvent = (EmulationHaltedEventHandler)System.Delegate.Remove(EmulationHaltedEvent, value);
            }
        }

        public delegate void InstructionDecodedEventHandler();
        private InstructionDecodedEventHandler InstructionDecodedEvent;

        public event InstructionDecodedEventHandler InstructionDecoded
        {
            add
            {
                InstructionDecodedEvent = (InstructionDecodedEventHandler)System.Delegate.Combine(InstructionDecodedEvent, value);
            }
            remove
            {
                InstructionDecodedEvent = (InstructionDecodedEventHandler)System.Delegate.Remove(InstructionDecodedEvent, value);
            }
        }

        public delegate void ErrorEventHandler(object sender, EmulatorErrorEventArgs e);
        private static ErrorEventHandler ErrorEvent;

        public static event ErrorEventHandler Error
        {
            add
            {
                ErrorEvent = (ErrorEventHandler)System.Delegate.Combine(ErrorEvent, value);
            }
            remove
            {
                ErrorEvent = (ErrorEventHandler)System.Delegate.Remove(ErrorEvent, value);
            }
        }

        public delegate void OutputEventHandler(string message, NotificationReasons reason, object[] arg);
        private static OutputEventHandler OutputEvent;

        public static event OutputEventHandler Output
        {
            add
            {
                OutputEvent = (OutputEventHandler)System.Delegate.Combine(OutputEvent, value);
            }
            remove
            {
                OutputEvent = (OutputEventHandler)System.Delegate.Remove(OutputEvent, value);
            }
        }

        public delegate void DebugModeChangedEventHandler(object sender, EventArgs e);
        private DebugModeChangedEventHandler DebugModeChangedEvent;

        public event DebugModeChangedEventHandler DebugModeChanged
        {
            add
            {
                DebugModeChangedEvent = (DebugModeChangedEventHandler)System.Delegate.Combine(DebugModeChangedEvent, value);
            }
            remove
            {
                DebugModeChangedEvent = (DebugModeChangedEventHandler)System.Delegate.Remove(DebugModeChangedEvent, value);
            }
        }

        public delegate void MIPsUpdatedEventHandler();
        private MIPsUpdatedEventHandler MIPsUpdatedEvent;

        public event MIPsUpdatedEventHandler MIPsUpdated
        {
            add
            {
                MIPsUpdatedEvent = (MIPsUpdatedEventHandler)System.Delegate.Combine(MIPsUpdatedEvent, value);
            }
            remove
            {
                MIPsUpdatedEvent = (MIPsUpdatedEventHandler)System.Delegate.Remove(MIPsUpdatedEvent, value);
            }
        }


        public delegate void RestartEmulation();
        private RestartEmulation restartCallback;

        public X8086(bool v20 = true, bool int13 = true, RestartEmulation restartEmulationCallback = null, Models model = X8086.Models.IBMPC_5160)
        {

            mVic20 = v20;
            mEmulateINT13 = int13;
            mAdapters = new Adapters(this);
            mPorts = new IOPorts(this);
            restartCallback = restartEmulationCallback;
            mModel = model;

            debugWaiter = new AutoResetEvent(false);
            addrMode = new AddressingMode();

            BASECLOCK = GetCpuSpeed() * X8086.MHz;

            BuildSZPTables();
            BuildDecoderCache();
            Init();
        }

        private void Init()
        {
            Sched = new Scheduler(this);

            //FPU = New x8087(Me)
            PIC = new PIC8259(this);
            DMA = new DMAI8237(this);
            PIT = new PIT8254(this, PIC.GetIrqLine((byte)0));
            PPI = new PPI8255(this, PIC.GetIrqLine((byte)1));
            //PPI = New PPI8255_ALT(Me, PIC.GetIrqLine(1))
            RTC = new RTC(this, PIC.GetIrqLine((byte)8));

            mPorts.Add(PIC);
            mPorts.Add(DMA);
            mPorts.Add(PIT);
            mPorts.Add(PPI);
            mPorts.Add(RTC);

            SetupSystem();

            Array.Clear(Memory, 0, Memory.Length);

            StopAllThreads();

            if (ReferenceEquals(mipsWaiter, null))
            {
                mipsWaiter = new AutoResetEvent(false);
                mipsThread = new Thread(new System.Threading.ThreadStart(MIPSCounterLoop));
                mipsThread.Start();
            }

            portsCache.Clear();

            mIsHalted = false;
            mIsExecuting = false;
            mEnableExceptions = false;
            mIsPaused = false;
            mDoReSchedule = false;

            ignoreINTs = false;
            mRepeLoopMode = REPLoopModes.None;
            IPAddrOffet = (ushort)0;
            useIPAddrOffset = false;

            mRegisters.ResetActiveSegment();

            mRegisters.AX = (ushort)0;
            mRegisters.BX = (ushort)0;
            mRegisters.CX = (ushort)0;
            mRegisters.DX = (ushort)0;

            mRegisters.BP = (ushort)0;
            mRegisters.IP = (ushort)0;
            mRegisters.SP = (ushort)0;

            mRegisters.CS = (ushort)0;
            mRegisters.DS = (ushort)0;
            mRegisters.ES = (ushort)0;
            mRegisters.SS = (ushort)0;

            mRegisters.SI = (ushort)0;
            mRegisters.DI = (ushort)0;

            mFlags.EFlags = 0;

            AddInternalHooks();
            LoadBIOS();
        }

        private void SetupSystem()
        {
            picIsAvailable = PIC != null;

            // http://docs.huihoo.com/help-pc/int-int_11.html
            byte equipmentByte = (byte)Binary.From("0 0 0 0 0 0 0 0 0 1 1 0 1 1 0 1".Replace(" ", ""));
            //                                     │F│E│D│C│B│A│9│8│7│6│5│4│3│2│1│0│─── AX
            //                                     │ │ │ │ │ │ │ │ │ │ │ │ │ │ │ └──── IPL diskette installed
            //                                     │ │ │ │ │ │ │ │ │ │ │ │ │ │ └───── math co-processor
            //                                     │ │ │ │ │ │ │ │ │ │ │ │ ├─┼────── old PC system board RAM < 256K (00=256k, 01=512k, 10=576k, 11=640k)
            //                                     │ │ │ │ │ │ │ │ │ │ │ │ │ └───── pointing device installed (PS/2)
            //                                     │ │ │ │ │ │ │ │ │ │ │ │ └────── not used on PS/2
            //                                     │ │ │ │ │ │ │ │ │ │ └─┴─────── initial video mode (00=EGA/VGA, 01=CGA 40x25, 10=CGA 80x25 color, 11=MDA 80x25)
            //                                     │ │ │ │ │ │ │ │ └─┴────────── # of diskette drives, less 1
            //                                     │ │ │ │ │ │ │ └───────────── 0 if DMA installed
            //                                     │ │ │ │ └─┴─┴────────────── number of serial ports
            //                                     │ │ │ └─────────────────── game adapter installed
            //                                     │ │ └──────────────────── unused, internal modem (PS/2)
            //                                     └─┴───────────────────── number of printer ports

            // VGA is not implemented in C# version, because i dont need it (use the orginal VB.NET version for this)
            //if (mVideoAdapter != null && mVideoAdapter is VGAAdapter)
            //{
            //	equipmentByte = (byte)(equipmentByte & + 0b11111111111001111); //equipmentByte = equipmentByte And &B11111111111001111
            //}

            // FPU support is also disabled in C# Version
            if (FPU != null)
            {
                equipmentByte = (byte)(equipmentByte & 0x1FFCF); //equipmentByte = equipmentByte Or &B10
            }

            if (PPI != null)
            {
                PPI.SwitchData = equipmentByte;
            }
        }

        private void LoadBIOS()
        {
            string unityPath = UnityEngine.Application.dataPath + "/CCC/8086Assets/";

            // BIOS
            LoadBIN(unityPath + "roms/pcxtbios.rom", (ushort)(0xFE00), (ushort)(0x0));
            //LoadBIN("..\..\Other Emulators & Resources\xtbios2\EPROMS\2764\XTBIOS.ROM", &HFE00, &H0)
            //LoadBIN("..\..\Other Emulators & Resources\xtbios25\EPROMS\2764\PCXTBIOS.ROM", &HFE00, &H0)
            //LoadBIN("..\..\Other Emulators & Resources\xtbios30\eproms\2764\pcxtbios.ROM", &HFE00, &H0)
            //LoadBIN("..\..\Other Emulators & Resources\xtbios31\pcxtbios.bin", &HFE00, &H0)
            //LoadBIN("..\..\Other Emulators & Resources\PCemV0.7\roms\genxt\pcxt.rom", &HFE00, &H0)
            //LoadBIN("..\..\Other Emulators & Resources\fake86-0.12.9.19-win32\Binaries\pcxtbios.bin", &HFE00, &H0)
            //LoadBIN("..\..\Other Emulators & Resources\award-2.05.rom", &HFE00, &H0)
            //LoadBIN("..\..\Other Emulators & Resources\phoenix-2.51.rom", &HFE00, &H0)
            //LoadBIN("..\..\Other Emulators & Resources\PCE - PC Emulator\bin\rom\ibm-pc-1982.rom", &HFE00, &H0)

            // BASIC C1.10
            LoadBIN(unityPath + "roms\\basicc11.bin", (ushort)(0xF600), (ushort)(0x0));
            //LoadBIN("..\..\Other Emulators & Resources\xtbios30\eproms\2764\basicf6.rom", &HF600, &H0)
            //LoadBIN("..\..\Other Emulators & Resources\xtbios30\eproms\2764\basicf8.rom", &HF800, &H0)
            //LoadBIN("..\..\Other Emulators & Resources\xtbios30\eproms\2764\basicfa.rom", &HFA00, &H0)
            //LoadBIN("..\..\Other Emulators & Resources\xtbios30\eproms\2764\basicfc.rom", &HFC00, &H0)

            // Lots of ROMs: http://www.hampa.ch/pce/download.html

            // XT IDE
            //LoadBIN("roms\ide_xt.bin", &HC800, &H0)
        }

        public void Close()
        {
            mRepeLoopMode = REPLoopModes.None;
            StopAllThreads();

            if (DebugMode)
            {
                debugWaiter.Set();
            }
            if (Sched != null)
            {
                Sched.Stop();
            }

            if (mipsWaiter != null)
            {
                mipsWaiter.Set();
            }

            foreach (Adapter adapter in mAdapters)
            {
                adapter.CloseAdapter();
            }
            mAdapters.Clear();
            mPorts.Clear();

            memHooks.Clear();
            intHooks.Clear();

            Sched = null;
            mipsWaiter = null;
        }

        public void SoftReset()
        {
            
            // Just as Bill would've have wanted it... ;)
            PPI.PutKeyData((int)KeyCode.LeftControl, false);
            PPI.PutKeyData((int)KeyCode.Menu, false);
            PPI.PutKeyData((int)KeyCode.Delete, false);
        }

        public void HardReset()
        {
            if (restartCallback != null)
            {
                Close();
                restartCallback.Invoke();
            }
            else
            {
                Close();
                Init();
            }
        }

        public void StepInto()
        {
            debugWaiter.Set();
        }

        public void Run(bool debugMode = false, ushort cs = 0xFFFF, ushort ip = 0)
        {
            SetSynchronization();

            mDebugMode = debugMode;
            cancelAllThreads = false;

#if Win32_dbg
			if (PIT?.Speaker != null)
			{
				PIT.Speaker.Enabled = true;
			}

			if (mVideoAdapter != null)
			{
				mVideoAdapter.Reset();
			}
#endif

            if (mDebugMode)
            {
                if (InstructionDecodedEvent != null)
                    InstructionDecodedEvent();
            }

            mRegisters.CS = cs;
            mRegisters.IP = ip;

            Sched.Start();
        }

        private void StopAllThreads()
        {
            cancelAllThreads = true;

            //If mVideoAdapter IsNot Nothing Then mVideoAdapter.Update()

            if (mipsThread != null)
            {
                do
                {
                    Thread.Sleep(100);
                } while (mipsThread.ThreadState == System.Threading.ThreadState.Running);

                mipsThread = null;
            }
        }

        private void MIPSCounterLoop()
        {
            const int delay = 1000;
            do
            {
                mipsWaiter.WaitOne(delay);

                mMPIs = (double)instrucionsCounter / delay / 1000;
                instrucionsCounter = (uint)0;

                if (cancelAllThreads)
                {
                    break;
                }
                if (MIPsUpdatedEvent != null)
                    MIPsUpdatedEvent();
            } while (true);
        }

        public void Pause()
        {
            if (mIsExecuting)
            {
                mIsPaused = true;

                do
                {
                    Thread.Sleep(10);
                } while (mIsExecuting);

#if Win32_dbg
				if (PIT?.Speaker != null)
				{
					PIT.Speaker.Enabled = false;
				}
#endif
            }
        }

        public void Resume()
        {
            mDoReSchedule = false;
            mIsPaused = false;
        }

        private void FlushCycles()
        {
            long t = clkCyc * Scheduler.BASECLOCK + leftCycleFrags;
            Sched.AdvanceTime((long)(t / mCyclesPerSecond));
            leftCycleFrags = t % mCyclesPerSecond;
            clkCyc = 0;

            mDoReSchedule = false;
        }

        public bool DoReschedule
        {
            get
            {
                return mDoReSchedule;
            }
            set
            {
                mDoReSchedule = value;
            }
        }

        private void SetSynchronization()
        {
            mDoReSchedule = true;

            Sched.SetSynchronization(true,
                (long)(Scheduler.BASECLOCK / 100),
                (long)(Scheduler.BASECLOCK / 1000));

            PIT?.UpdateClock();
        }

        public void RunEmulation()
        {
            if (mIsExecuting || mIsPaused)
            {
                return;
            }

            long maxRunTime = Sched.GetTimeToNextEvent();
            if (maxRunTime <= 0)
            {
                return;
            }
            if (maxRunTime > Scheduler.BASECLOCK)
            {
                maxRunTime = Scheduler.BASECLOCK;
            }
            long maxRunCycl = (maxRunTime * mCyclesPerSecond - leftCycleFrags + Scheduler.BASECLOCK - 1) / Scheduler.BASECLOCK;
            InitOpcodeTable();

            if (mDebugMode)
            {
                while (clkCyc < maxRunCycl && !mDoReSchedule && mDebugMode)
                {
                    debugWaiter.WaitOne();

                    lock (decoderSyncObj)
                    {
                        mIsExecuting = true;
                        PreExecute();
#if DEBUG
                        Execute_DEBUG();
#else
						opCodes[opCode].Invoke();
#endif
                        PostExecute();
                        mIsExecuting = false;
                    }

                    if (InstructionDecodedEvent != null)
                        InstructionDecodedEvent();
                }
            }
            else
            {
                mIsExecuting = true;
                while (clkCyc < maxRunCycl && !mDoReSchedule)
                {
                    PreExecute();
#if DEBUG
                    Execute_DEBUG();
#else
					opCodes[opCode].Invoke();
#endif
                    PostExecute();
                }
                mIsExecuting = false;
            }

            FlushCycles();
        }

        private void PreExecute()
        {
            if (mFlags.TF == 1)
            {
                // The addition of the "If ignoreINTs Then" not only fixes the dreaded "Interrupt Check" in CheckIt,
                // but it even allows it to pass it successfully!!!
                if (ignoreINTs)
                {
                    HandleInterrupt((byte)1, false);
                }
            }
            else if (ignoreINTs)
            {
                ignoreINTs = false;
            }
            else
            {
                HandlePendingInterrupt();
            }

            opCodeSize = (byte)1;
            newPrefix = false;
            instrucionsCounter++;

            opCode = get_RAM8(mRegisters.CS, mRegisters.IP, (byte)0, false);
        }

        private void Execute_DEBUG()
        {
            if (opCode >= 0x0 && opCode <= 0x3) // ADD Eb Gb | Ev Gv | Gb Eb | Gv Ev
            {
                SetAddressing();
                if (addrMode.IsDirect)
                {
                    mRegisters.set_Val(addrMode.Dst, Eval((uint)(mRegisters.get_Val(addrMode.Dst)), (uint)(mRegisters.get_Val(addrMode.Src)), Operation.Add, addrMode.Size));
                    clkCyc += 3;
                }
                else
                {
                    if (addrMode.Direction == 0)
                    {
                        //TODO: true or false?
                        set_RAMn(false, Eval((uint)(addrMode.IndMem), (uint)(mRegisters.get_Val(addrMode.Src)), Operation.Add, addrMode.Size));
                        clkCyc += 16;
                    }
                    else
                    {
                        mRegisters.set_Val(addrMode.Dst, Eval((uint)(mRegisters.get_Val(addrMode.Dst)), (uint)(addrMode.IndMem), Operation.Add, addrMode.Size));
                        clkCyc += 9;
                    }
                }
            } // ADD AL Ib
            else if (opCode == ((byte)(0x4)))
            {
                mRegisters.AL = (byte)(Eval((uint)(mRegisters.AL), (uint)(Param(index: ParamIndex.First, size: DataSize.Byte)), Operation.Add, DataSize.Byte));
                clkCyc += 4;
            } // ADD AX Iv
            else if (opCode == ((byte)(0x5)))
            {
                mRegisters.AX = Eval((uint)(mRegisters.AX), (uint)(Param(index: ParamIndex.First, size: DataSize.Word)), Operation.Add, DataSize.Word);
                clkCyc += 4;
            } // PUSH ES
            else if (opCode == ((byte)(0x6)))
            {
                PushIntoStack(mRegisters.ES);
                clkCyc += 10;
            } // POP ES
            else if (opCode == ((byte)(0x7)))
            {
                mRegisters.ES = PopFromStack();
                ignoreINTs = true;
                clkCyc += 8;
            } // OR Eb Gb | Ev Gv | Gb Eb | Gv Ev
            else if (opCode >= 0x8 && opCode <= 0xB)
            {
                SetAddressing();
                if (addrMode.IsDirect)
                {
                    mRegisters.set_Val(addrMode.Dst, Eval((uint)(mRegisters.get_Val(addrMode.Dst)), (uint)(mRegisters.get_Val(addrMode.Src)), Operation.LogicOr, addrMode.Size));
                    clkCyc += 3;
                }
                else
                {
                    if (addrMode.Direction == 0)
                    {
                        set_RAMn(false, Eval((uint)(addrMode.IndMem), (uint)(mRegisters.get_Val(addrMode.Src)), Operation.LogicOr, addrMode.Size));
                        clkCyc += 16;
                    }
                    else
                    {
                        mRegisters.set_Val(addrMode.Dst, Eval((uint)(mRegisters.get_Val(addrMode.Dst)), (uint)(addrMode.IndMem), Operation.LogicOr, addrMode.Size));
                        clkCyc += 9;
                    }
                }
            } // OR AL Ib
            else if (opCode == ((byte)(0xC)))
            {
                mRegisters.AL = (byte)(Eval((uint)(mRegisters.AL), (uint)(Param(index: ParamIndex.First, size: DataSize.Byte)), Operation.LogicOr, DataSize.Byte));
                clkCyc += 4;
            } // OR AX Iv
            else if (opCode == ((byte)(0xD)))
            {
                mRegisters.AX = Eval((uint)(mRegisters.AX), (uint)(Param(index: ParamIndex.First, size: DataSize.Word)), Operation.LogicOr, DataSize.Word);
                clkCyc += 4;
            } // PUSH CS
            else if (opCode == ((byte)(0xE)))
            {
                PushIntoStack(mRegisters.CS);
                clkCyc += 10;
            } // POP CS
            else if (opCode == ((byte)(0xF)))
            {
                if (!mVic20)
                {
                    mRegisters.CS = PopFromStack();
                    ignoreINTs = true;
                    clkCyc += 8;
                }
            } // ADC Eb Gb | Ev Gv | Gb Eb | Gv Ev
            else if (opCode >= 0x10 && opCode <= 0x13)
            {
                SetAddressing();
                if (addrMode.IsDirect)
                {
                    mRegisters.set_Val(addrMode.Dst, Eval((uint)(mRegisters.get_Val(addrMode.Dst)), (uint)(mRegisters.get_Val(addrMode.Src)), Operation.AddWithCarry, addrMode.Size));
                    clkCyc += 3;
                }
                else
                {
                    if (addrMode.Direction == 0)
                    {
                        set_RAMn(false, Eval((uint)(addrMode.IndMem), (uint)(mRegisters.get_Val(addrMode.Src)), Operation.AddWithCarry, addrMode.Size));
                        clkCyc += 16;
                    }
                    else
                    {
                        mRegisters.set_Val(addrMode.Dst, Eval((uint)(mRegisters.get_Val(addrMode.Dst)), (uint)(addrMode.IndMem), Operation.AddWithCarry, addrMode.Size));
                        clkCyc += 9;
                    }
                }
            } // ADC AL Ib
            else if (opCode == ((byte)(0x14)))
            {
                mRegisters.AL = (byte)(Eval((uint)(mRegisters.AL), (uint)(Param(index: ParamIndex.First, size: DataSize.Byte)), Operation.AddWithCarry, DataSize.Byte));
                clkCyc += 3;
            } // ADC AX Iv
            else if (opCode == ((byte)(0x15)))
            {
                mRegisters.AX = Eval((uint)(mRegisters.AX), (uint)(Param(index: ParamIndex.First, size: DataSize.Word)), Operation.AddWithCarry, DataSize.Word);
                clkCyc += 3;
            } // PUSH SS
            else if (opCode == ((byte)(0x16)))
            {
                PushIntoStack(mRegisters.SS);
                clkCyc += 10;
            } // POP SS
            else if (opCode == ((byte)(0x17)))
            {
                mRegisters.SS = PopFromStack();
                // Lesson 4: http://ntsecurity.nu/onmymind/2007/2007-08-22.html
                // http://zet.aluzina.org/forums/viewtopic.php?f=6&t=287
                // http://www.vcfed.org/forum/archive/index.php/t-41453.html
                ignoreINTs = true;
                clkCyc += 8;
            } // SBB Eb Gb | Ev Gv | Gb Eb | Gv Ev
            else if (opCode >= 0x18 && opCode <= 0x1B)
            {
                SetAddressing();
                if (addrMode.IsDirect)
                {
                    mRegisters.set_Val(addrMode.Dst, Eval((uint)(mRegisters.get_Val(addrMode.Dst)), (uint)(mRegisters.get_Val(addrMode.Src)), Operation.SubstractWithCarry, addrMode.Size));
                    clkCyc += 3;
                }
                else
                {
                    if (addrMode.Direction == 0)
                    {
                        set_RAMn(false, Eval((uint)(addrMode.IndMem), (uint)(mRegisters.get_Val(addrMode.Src)), Operation.SubstractWithCarry, addrMode.Size));
                        clkCyc += 16;
                    }
                    else
                    {
                        mRegisters.set_Val(addrMode.Dst, Eval((uint)(mRegisters.get_Val(addrMode.Dst)), (uint)(addrMode.IndMem), Operation.SubstractWithCarry, addrMode.Size));
                        clkCyc += 9;
                    }
                }
            } // SBB AL Ib
            else if (opCode == ((byte)(0x1C)))
            {
                mRegisters.AL = (byte)(Eval((uint)(mRegisters.AL), (uint)(Param(index: ParamIndex.First, size: DataSize.Byte)), Operation.SubstractWithCarry, DataSize.Byte));
                clkCyc += 4;
            } // SBB AX Iv
            else if (opCode == ((byte)(0x1D)))
            {
                mRegisters.AX = Eval((uint)(mRegisters.AX), (uint)(Param(index: ParamIndex.First, size: DataSize.Word)), Operation.SubstractWithCarry, DataSize.Word);
                clkCyc += 4;
            } // PUSH DS
            else if (opCode == ((byte)(0x1E)))
            {
                PushIntoStack(mRegisters.DS);
                clkCyc += 10;
            } // POP DS
            else if (opCode == ((byte)(0x1F)))
            {
                mRegisters.DS = PopFromStack();
                ignoreINTs = true;
                clkCyc += 8;
            } // AND Eb Gb | Ev Gv | Gb Eb | Gv Ev
            else if (opCode >= 0x20 && opCode <= 0x23)
            {
                SetAddressing();
                if (addrMode.IsDirect)
                {
                    mRegisters.set_Val(addrMode.Dst, Eval((uint)(mRegisters.get_Val(addrMode.Dst)), (uint)(mRegisters.get_Val(addrMode.Src)), Operation.LogicAnd, addrMode.Size));
                    clkCyc += 3;
                }
                else
                {
                    if (addrMode.Direction == 0)
                    {
                        set_RAMn(false, Eval((uint)(addrMode.IndMem), (uint)(mRegisters.get_Val(addrMode.Src)), Operation.LogicAnd, addrMode.Size));
                        clkCyc += 16;
                    }
                    else
                    {
                        mRegisters.set_Val(addrMode.Dst, Eval((uint)(mRegisters.get_Val(addrMode.Dst)), (uint)(addrMode.IndMem), Operation.LogicAnd, addrMode.Size));
                        clkCyc += 9;
                    }
                }
            } // AND AL Ib
            else if (opCode == ((byte)(0x24)))
            {
                mRegisters.AL = (byte)(Eval((uint)(mRegisters.AL), (uint)(Param(index: ParamIndex.First, size: DataSize.Byte)), Operation.LogicAnd, DataSize.Byte));
                clkCyc += 4;
            } // AND AX Iv
            else if (opCode == ((byte)(0x25)))
            {
                mRegisters.AX = Eval((uint)(mRegisters.AX), (uint)(Param(index: ParamIndex.First, size: DataSize.Word)), Operation.LogicAnd, DataSize.Word);
                clkCyc += 4;
            } // ES, CS, SS and DS segment override prefix
            else if ((((opCode == ((byte)(0x26))) || (opCode == ((byte)(0x2E)))) || (opCode == ((byte)(0x36)))) || (opCode == ((byte)(0x3E))))
            {
                addrMode.Decode(opCode, opCode);
                mRegisters.ActiveSegmentRegister = addrMode.Dst - GPRegisters.RegistersTypes.AH + GPRegisters.RegistersTypes.ES;
                newPrefix = true;
                clkCyc += 2;
            } // DAA
            else if (opCode == ((byte)(0x27)))
            {
                if (mRegisters.AL.LowNib() > 9 || mFlags.AF == 1)
                {
                    tmpUVal = (uint)((mRegisters.AL) + 6);
                    mRegisters.AL += (byte)6;
                    mFlags.AF = (byte)1;
                    mFlags.CF = (byte)((mFlags.CF | (((tmpUVal & 0xFF00) != 0) ? 1 : 0)));
                }
                else
                {
                    mFlags.AF = (byte)0;
                }
                if ((mRegisters.AL & 0xF0) > 0x90 || mFlags.CF == 1)
                {
                    tmpUVal = (uint)((mRegisters.AL) + 0x60);
                    mRegisters.AL += (byte)(0x60);
                    mFlags.CF = (byte)1;
                }
                else
                {
                    mFlags.CF = (byte)0;
                }
                SetSZPFlags(tmpUVal, DataSize.Byte);
                clkCyc += 4;
            } // SUB Eb Gb | Ev Gv | Gb Eb | Gv Ev
            else if (opCode >= 0x28 && opCode <= 0x2B)
            {
                SetAddressing();
                if (addrMode.IsDirect)
                {
                    mRegisters.set_Val(addrMode.Dst, Eval((uint)(mRegisters.get_Val(addrMode.Dst)), (uint)(mRegisters.get_Val(addrMode.Src)), Operation.Substract, addrMode.Size));
                    clkCyc += 3;
                }
                else
                {
                    if (addrMode.Direction == 0)
                    {
                        set_RAMn(false, Eval((uint)(addrMode.IndMem), (uint)(mRegisters.get_Val(addrMode.Src)), Operation.Substract, addrMode.Size));
                        clkCyc += 16;
                    }
                    else
                    {
                        mRegisters.set_Val(addrMode.Dst, Eval((uint)(mRegisters.get_Val(addrMode.Dst)), (uint)(addrMode.IndMem), Operation.Substract, addrMode.Size));
                        clkCyc += 9;
                    }
                }
            } // SUB AL Ib
            else if (opCode == ((byte)(0x2C)))
            {
                mRegisters.AL = (byte)(Eval((uint)(mRegisters.AL), (uint)(Param(index: ParamIndex.First, size: DataSize.Byte)), Operation.Substract, DataSize.Byte));
                clkCyc += 4;
            } // SUB AX, Iv
            else if (opCode == ((byte)(0x2D)))
            {
                mRegisters.AX = Eval((uint)(mRegisters.AX), (uint)(Param(index: ParamIndex.First, size: DataSize.Word)), Operation.Substract, DataSize.Word);
                clkCyc += 4;
            } // DAS
            else if (opCode == ((byte)(0x2F)))
            {
                tmpVal = mRegisters.AL;
                if (mRegisters.AL.LowNib() > 9 || mFlags.AF == 1)
                {
                    tmpUVal = (uint)((mRegisters.AL) - 6);
                    mRegisters.AL -= (byte)6;
                    mFlags.AF = (byte)1;
                    mFlags.CF = (byte)(mFlags.CF | (((tmpUVal & 0xFF00) != 0) ? 1 : 0));
                }
                else
                {
                    mFlags.AF = (byte)0;
                }
                if (tmpVal > 0x99 || mFlags.CF == 1)
                {
                    tmpUVal = (uint)((mRegisters.AL) - 0x60);
                    mRegisters.AL -= (byte)(0x60);
                    mFlags.CF = (byte)1;
                }
                else
                {
                    mFlags.CF = (byte)0;
                }
                SetSZPFlags(tmpUVal, DataSize.Byte);
                clkCyc += 4;
            } // XOR Eb Gb | Ev Gv | Gb Eb | Gv Ev
            else if (opCode >= 0x30 && opCode <= 0x33)
            {
                SetAddressing();
                if (addrMode.IsDirect)
                {
                    mRegisters.set_Val(addrMode.Dst, Eval((uint)(mRegisters.get_Val(addrMode.Dst)), (uint)(mRegisters.get_Val(addrMode.Src)), Operation.LogicXor, addrMode.Size));
                    clkCyc += 3;
                }
                else
                {
                    if (addrMode.Direction == 0)
                    {
                        set_RAMn(false, Eval((uint)(addrMode.IndMem), (uint)(mRegisters.get_Val(addrMode.Src)), Operation.LogicXor, addrMode.Size));
                        clkCyc += 16;
                    }
                    else
                    {
                        mRegisters.set_Val(addrMode.Dst, Eval((uint)(mRegisters.get_Val(addrMode.Dst)), (uint)(addrMode.IndMem), Operation.LogicXor, addrMode.Size));
                        clkCyc += 9;
                    }
                }
            } // XOR AL Ib
            else if (opCode == ((byte)(0x34)))
            {
                mRegisters.AL = (byte)(Eval((uint)(mRegisters.AL), (uint)(Param(index: ParamIndex.First, size: DataSize.Byte)), Operation.LogicXor, DataSize.Byte));
                clkCyc += 4;
            } // XOR AX Iv
            else if (opCode == ((byte)(0x35)))
            {
                mRegisters.AX = Eval((uint)(mRegisters.AX), (uint)(Param(index: ParamIndex.First, size: DataSize.Word)), Operation.LogicXor, DataSize.Word);
                clkCyc += 4;
            } // AAA
            else if (opCode == ((byte)(0x37)))
            {
                if (mRegisters.AL.LowNib() > 9 || mFlags.AF == 1)
                {
                    mRegisters.AX += (ushort)(0x106);
                    mFlags.AF = (byte)1;
                    mFlags.CF = (byte)1;
                }
                else
                {
                    mFlags.AF = (byte)0;
                    mFlags.CF = (byte)0;
                }
                mRegisters.AL = (byte)(mRegisters.AL.LowNib());
                clkCyc += 8;
            } // CMP Eb Gb | Ev Gv | Gb Eb | Gv Ev
            else if (opCode >= 0x38 && opCode <= 0x3B)
            {
                SetAddressing();
                if (addrMode.IsDirect)
                {
                    Eval((uint)(mRegisters.get_Val(addrMode.Dst)), (uint)(mRegisters.get_Val(addrMode.Src)), Operation.Compare, addrMode.Size);
                    clkCyc += 3;
                }
                else
                {
                    if (addrMode.Direction == 0)
                    {
                        Eval((uint)(addrMode.IndMem), (uint)(mRegisters.get_Val(addrMode.Src)), Operation.Compare, addrMode.Size);
                    }
                    else
                    {
                        Eval((uint)(mRegisters.get_Val(addrMode.Dst)), (uint)(addrMode.IndMem), Operation.Compare, addrMode.Size);
                    }
                    clkCyc += 9;
                }
            } // CMP AL Ib
            else if (opCode == ((byte)(0x3C)))
            {
                Eval((uint)(mRegisters.AL), (uint)(Param(index: ParamIndex.First, size: DataSize.Byte)), Operation.Compare, DataSize.Byte);
                clkCyc += 4;
            } // CMP AX Iv
            else if (opCode == ((byte)(0x3D)))
            {
                Eval((uint)(mRegisters.AX), (uint)(Param(index: ParamIndex.First, size: DataSize.Word)), Operation.Compare, DataSize.Word);
                clkCyc += 4;
            } // AAS
            else if (opCode == ((byte)(0x3F)))
            {
                if (mRegisters.AL.LowNib() > 9 || mFlags.AF == 1)
                {
                    mRegisters.AX -= (ushort)(0x106);
                    mFlags.AF = (byte)1;
                    mFlags.CF = (byte)1;
                }
                else
                {
                    mFlags.AF = (byte)0;
                    mFlags.CF = (byte)0;
                }
                mRegisters.AL = (byte)(mRegisters.AL.LowNib());
                clkCyc += 8;
            } // INC AX | CX | DX | BX | SP | BP | SI | DI
            else if (opCode >= 0x40 && opCode <= 0x47)
            {
                SetRegister1Alt(opCode);
                mRegisters.set_Val(addrMode.Register1, Eval((uint)(mRegisters.get_Val(addrMode.Register1)), (uint)1, Operation.Increment, DataSize.Word));
                clkCyc += 3;
            } // DEC AX | CX | DX | BX | SP | BP | SI | DI
            else if (opCode >= 0x48 && opCode <= 0x4F)
            {
                SetRegister1Alt(opCode);
                mRegisters.set_Val(addrMode.Register1, Eval((uint)(mRegisters.get_Val(addrMode.Register1)), (uint)1, Operation.Decrement, DataSize.Word));
                clkCyc += 3;
            } // PUSH AX | CX | DX | BX | SP | BP | SI | DI
            else if (opCode >= 0x50 && opCode <= 0x57)
            {
                if (opCode == 0x54) // SP
                {
                    // The 8086/8088 pushes the value of SP after it has been decremented
                    // http://css.csail.mit.edu/6.858/2013/readings/i386/s15_06.htm
                    PushIntoStack((ushort)(mRegisters.SP - 2));
                }
                else
                {
                    SetRegister1Alt(opCode);
                    PushIntoStack(mRegisters.get_Val(addrMode.Register1));
                }
                clkCyc += 11;
            } // POP AX | CX | DX | BX | SP | BP | SI | DI
            else if (opCode >= 0x58 && opCode <= 0x5F)
            {
                SetRegister1Alt(opCode);
                mRegisters.set_Val(addrMode.Register1, PopFromStack());
                clkCyc += 8;
            } // PUSHA (80186)
            else if (opCode == ((byte)(0x60)))
            {
                if (mVic20)
                {
                    tmpUVal = (uint)(mRegisters.SP);
                    PushIntoStack(mRegisters.AX);
                    PushIntoStack(mRegisters.CX);
                    PushIntoStack(mRegisters.DX);
                    PushIntoStack(mRegisters.BX);
                    PushIntoStack((ushort)(tmpUVal));
                    PushIntoStack(mRegisters.BP);
                    PushIntoStack(mRegisters.SI);
                    PushIntoStack(mRegisters.DI);
                    clkCyc += 19;
                }
                else
                {
                    OpCodeNotImplemented();
                }
            } // POPA (80186)
            else if (opCode == ((byte)(0x61)))
            {
                if (mVic20)
                {
                    mRegisters.DI = PopFromStack();
                    mRegisters.SI = PopFromStack();
                    mRegisters.BP = PopFromStack();
                    PopFromStack(); // SP
                    mRegisters.BX = PopFromStack();
                    mRegisters.DX = PopFromStack();
                    mRegisters.CX = PopFromStack();
                    mRegisters.AX = PopFromStack();
                    clkCyc += 19;
                }
                else
                {
                    OpCodeNotImplemented();
                }
            } // BOUND (80186)
            else if (opCode == ((byte)(0x62)))
            {
                if (mVic20)
                {
                    // PRE ALPHA CODE - UNTESTED
                    SetAddressing();
                    if (To32bitsWithSign(mRegisters.get_Val(addrMode.Register1)) < get_RAM16((ushort)(addrMode.IndAdr >> 4), (ushort)(addrMode.IndAdr & 15), (byte)0, false))
                    {
                        HandleInterrupt((byte)5, false);
                    }
                    else
                    {
                        addrMode.IndAdr += (ushort)2;
                        if (To32bitsWithSign(mRegisters.get_Val(addrMode.Register1)) < get_RAM16((ushort)(addrMode.IndAdr >> 4), (ushort)(addrMode.IndAdr & 15), (byte)0, false))
                        {
                            HandleInterrupt((byte)5, false);
                        }
                    }
                    clkCyc += 34;
                }
                else
                {
                    OpCodeNotImplemented();
                }
            } // PUSH Iv (80186)
            else if (opCode == ((byte)(0x68)))
            {
                // PRE ALPHA CODE - UNTESTED
                if (mVic20)
                {
                    PushIntoStack(Param(index: ParamIndex.First, size: DataSize.Word));
                    clkCyc += 3;
                }
                else
                {
                    OpCodeNotImplemented();
                }
            } // IMUL (80186)
            else if (opCode == ((byte)(0x69)))
            {
                if (mVic20)
                {
                    // PRE ALPHA CODE - UNTESTED
                    SetAddressing();
                    uint tmp1 = (uint)(mRegisters.get_Val(addrMode.Register1));
                    uint tmp2 = (uint)(Param(index: ParamIndex.First, size: DataSize.Word));
                    if ((tmp1 & 0x8000) == 0x8000)
                    {
                        tmp1 = (uint)(tmp1 | -65536);
                    }
                    if ((tmp2 & 0x8000) == 0x8000)
                    {
                        tmp2 = (uint)(tmp2 | -65536);
                    }
                    uint tmp3 = tmp1 * tmp2;
                    mRegisters.set_Val(addrMode.Register1, (ushort)(tmp3 & 0xFFFF));
                    if ((tmp3 & -65536) != 0L)
                    {
                        mFlags.CF = (byte)1;
                        mFlags.OF = (byte)1;
                    }
                    else
                    {
                        mFlags.CF = (byte)0;
                        mFlags.OF = (byte)0;
                    }
                    clkCyc += 27;
                }
                else
                {
                    OpCodeNotImplemented();
                }
            } // PUSH Ib (80186)
            else if (opCode == ((byte)(0x6A)))
            {
                if (mVic20)
                {
                    // PRE ALPHA CODE - UNTESTED
                    PushIntoStack(Param(index: ParamIndex.First, size: DataSize.Byte));
                    clkCyc += 3;
                }
                else
                {
                    OpCodeNotImplemented();
                }
            } // IMUL (80186)
            else if (opCode == ((byte)(0x6B)))
            {
                if (mVic20)
                {
                    // PRE ALPHA CODE - UNTESTED
                    SetAddressing();
                    uint tmp1 = (uint)(mRegisters.get_Val(addrMode.Register1));
                    uint tmp2 = (uint)(To16bitsWithSign(Param(index: ParamIndex.First, size: DataSize.Byte)));
                    if ((tmp1 & 0x8000) == 0x8000)
                    {
                        tmp1 = (uint)(tmp1 | -65536);
                    }
                    if ((tmp2 & 0x8000) == 0x8000)
                    {
                        tmp2 = (uint)(tmp2 | -65536);
                    }
                    uint tmp3 = tmp1 * tmp2;
                    mRegisters.set_Val(addrMode.Register1, (ushort)(tmp3 & 0xFFFF));
                    if ((tmp3 & -65536) != 0L)
                    {
                        mFlags.CF = (byte)1;
                        mFlags.OF = (byte)1;
                    }
                    else
                    {
                        mFlags.CF = (byte)0;
                        mFlags.OF = (byte)0;
                    }
                    clkCyc += 27;
                }
                else
                {
                    OpCodeNotImplemented();
                }
            } // Ignore 80186/V20 port operations... for now...
            else if (opCode >= 0x6C && opCode <= 0x6F)
            {
                opCodeSize++;
                clkCyc += 3;
            } // JO Jb
            else if (opCode == ((byte)(0x70)))
            {
                if (mFlags.OF == 1)
                {
                    IPAddrOffet = OffsetIP(DataSize.Byte);
                    clkCyc += 16;
                }
                else
                {
                    opCodeSize++;
                    clkCyc += 4;
                }
            } // JNO  Jb
            else if (opCode == ((byte)(0x71)))
            {
                if (mFlags.OF == 0)
                {
                    IPAddrOffet = OffsetIP(DataSize.Byte);
                    clkCyc += 16;
                }
                else
                {
                    opCodeSize++;
                    clkCyc += 4;
                }
            } // JB/JNAE/JC Jb
            else if (opCode == ((byte)(0x72)))
            {
                if (mFlags.CF == 1)
                {
                    IPAddrOffet = OffsetIP(DataSize.Byte);
                    clkCyc += 16;
                }
                else
                {
                    opCodeSize++;
                    clkCyc += 4;
                }
            } // JNB/JAE/JNC Jb
            else if (opCode == ((byte)(0x73)))
            {
                if (mFlags.CF == 0)
                {
                    IPAddrOffet = OffsetIP(DataSize.Byte);
                    clkCyc += 16;
                }
                else
                {
                    opCodeSize++;
                    clkCyc += 4;
                }
            } // JZ/JE Jb
            else if (opCode == ((byte)(0x74)))
            {
                if (mFlags.ZF == 1)
                {
                    IPAddrOffet = OffsetIP(DataSize.Byte);
                    clkCyc += 16;
                }
                else
                {
                    opCodeSize++;
                    clkCyc += 4;
                }
            } // JNZ/JNE Jb
            else if (opCode == ((byte)(0x75)))
            {
                if (mFlags.ZF == 0)
                {
                    IPAddrOffet = OffsetIP(DataSize.Byte);
                    clkCyc += 16;
                }
                else
                {
                    opCodeSize++;
                    clkCyc += 4;
                }
            } // JBE/JNA Jb
            else if (opCode == ((byte)(0x76)))
            {
                if (mFlags.CF == 1 || mFlags.ZF == 1)
                {
                    IPAddrOffet = OffsetIP(DataSize.Byte);
                    clkCyc += 16;
                }
                else
                {
                    opCodeSize++;
                    clkCyc += 4;
                }
            } // JA/JNBE Jb
            else if (opCode == ((byte)(0x77)))
            {
                if (mFlags.CF == 0 && mFlags.ZF == 0)
                {
                    IPAddrOffet = OffsetIP(DataSize.Byte);
                    clkCyc += 16;
                }
                else
                {
                    opCodeSize++;
                    clkCyc += 4;
                }
            } // JS Jb
            else if (opCode == ((byte)(0x78)))
            {
                if (mFlags.SF == 1)
                {
                    IPAddrOffet = OffsetIP(DataSize.Byte);
                    clkCyc += 16;
                }
                else
                {
                    opCodeSize++;
                    clkCyc += 4;
                }
            } // JNS Jb
            else if (opCode == ((byte)(0x79)))
            {
                if (mFlags.SF == 0)
                {
                    IPAddrOffet = OffsetIP(DataSize.Byte);
                    clkCyc += 16;
                }
                else
                {
                    opCodeSize++;
                    clkCyc += 4;
                }
            } // JPE/JP Jb
            else if (opCode == ((byte)(0x7A)))
            {
                if (mFlags.PF == 1)
                {
                    IPAddrOffet = OffsetIP(DataSize.Byte);
                    clkCyc += 16;
                }
                else
                {
                    opCodeSize++;
                    clkCyc += 4;
                }
            } // JPO/JNP Jb
            else if (opCode == ((byte)(0x7B)))
            {
                if (mFlags.PF == 0)
                {
                    IPAddrOffet = OffsetIP(DataSize.Byte);
                    clkCyc += 16;
                }
                else
                {
                    opCodeSize++;
                    clkCyc += 4;
                }
            } // JL/JNGE Jb
            else if (opCode == ((byte)(0x7C)))
            {
                if (mFlags.SF != mFlags.OF)
                {
                    IPAddrOffet = OffsetIP(DataSize.Byte);
                    clkCyc += 16;
                }
                else
                {
                    opCodeSize++;
                    clkCyc += 4;
                }
            } // JGE/JNL Jb
            else if (opCode == ((byte)(0x7D)))
            {
                if (mFlags.SF == mFlags.OF)
                {
                    IPAddrOffet = OffsetIP(DataSize.Byte);
                    clkCyc += 16;
                }
                else
                {
                    opCodeSize++;
                    clkCyc += 4;
                }
            } // JLE/JNG Jb
            else if (opCode == ((byte)(0x7E)))
            {
                if (mFlags.ZF == 1 || (mFlags.SF != mFlags.OF))
                {
                    IPAddrOffet = OffsetIP(DataSize.Byte);
                    clkCyc += 16;
                }
                else
                {
                    opCodeSize++;
                    clkCyc += 4;
                }
            } // JG/JNLE Jb
            else if (opCode == ((byte)(0x7F)))
            {
                if (mFlags.ZF == 0 && (mFlags.SF == mFlags.OF))
                {
                    IPAddrOffet = OffsetIP(DataSize.Byte);
                    clkCyc += 16;
                }
                else
                {
                    opCodeSize++;
                    clkCyc += 4;
                }
            }
            else if (opCode >= 0x80 && opCode <= 0x83)
            {
                ExecuteGroup1();
            } // TEST Gb Eb | Gv Ev
            else if (opCode >= 0x84 && opCode <= 0x85)
            {
                SetAddressing();
                if (addrMode.IsDirect)
                {
                    Eval((uint)(mRegisters.get_Val(addrMode.Dst)), (uint)(mRegisters.get_Val(addrMode.Src)), Operation.Test, addrMode.Size);
                    clkCyc += 3;
                }
                else
                {
                    Eval((uint)(addrMode.IndMem), (uint)(mRegisters.get_Val(addrMode.Dst)), Operation.Test, addrMode.Size);
                    clkCyc += 9;
                }
            } // XCHG Gb Eb | Gv Ev
            else if (opCode >= 0x86 && opCode <= 0x87)
            {
                SetAddressing();
                if (addrMode.IsDirect)
                {
                    tmpUVal = (uint)(mRegisters.get_Val(addrMode.Dst));
                    mRegisters.set_Val(addrMode.Dst, mRegisters.get_Val(addrMode.Src));
                    mRegisters.set_Val(addrMode.Src, (ushort)tmpUVal);
                    clkCyc += 4;
                }
                else
                {
                    set_RAMn(false, mRegisters.get_Val(addrMode.Dst));
                    mRegisters.set_Val(addrMode.Dst, addrMode.IndMem);
                    clkCyc += 17;
                }
            } // MOV Eb Gb | Ev Gv | Gb Eb | Gv Ev
            else if (opCode >= 0x88 && opCode <= 0x8B)
            {
                SetAddressing();
                if (addrMode.IsDirect)
                {
                    mRegisters.set_Val(addrMode.Dst, mRegisters.get_Val(addrMode.Src));
                    clkCyc += 2;
                }
                else
                {
                    if (addrMode.Direction == 0)
                    {
                        set_RAMn(false, mRegisters.get_Val(addrMode.Src));
                        clkCyc += 9;
                    }
                    else
                    {
                        mRegisters.set_Val(addrMode.Dst, addrMode.IndMem);
                        clkCyc += 8;
                    }
                }
            } // MOV Ew Sw
            else if (opCode == ((byte)(0x8C)))
            {
                SetAddressing(DataSize.Word);
                SetRegister2ToSegReg();
                if (addrMode.IsDirect)
                {
                    SetRegister1Alt(get_RAM8(mRegisters.CS, (ushort)(mRegisters.IP + 1), (byte)0, false));
                    mRegisters.set_Val(addrMode.Register1, mRegisters.get_Val(addrMode.Register2));
                    clkCyc += 2;
                }
                else
                {
                    set_RAMn(false, mRegisters.get_Val(addrMode.Register2));
                    clkCyc += 8;
                }
            } // LEA Gv M
            else if (opCode == ((byte)(0x8D)))
            {
                SetAddressing();
                mRegisters.set_Val(addrMode.Src, addrMode.IndAdr);
                clkCyc += 2;
            } // MOV Sw Ew
            else if (opCode == ((byte)(0x8E)))
            {
                SetAddressing(DataSize.Word);
                SetRegister2ToSegReg();
                if (addrMode.IsDirect)
                {
                    SetRegister1Alt(get_RAM8(mRegisters.CS, (ushort)(mRegisters.IP + 1), (byte)0, false));
                    mRegisters.set_Val(addrMode.Register2, mRegisters.get_Val(addrMode.Register1));
                    clkCyc += 2;
                }
                else
                {
                    mRegisters.set_Val(addrMode.Register2, addrMode.IndMem);
                    clkCyc += 8;
                }
                ignoreINTs = true;
                if (addrMode.Register2 == GPRegisters.RegistersTypes.CS)
                {
                    mDoReSchedule = true;
                }
            } // POP Ev
            else if (opCode == ((byte)(0x8F)))
            {
                SetAddressing();
                if (addrMode.IsDirect)
                {
                    addrMode.Decode(opCode, opCode);
                    mRegisters.set_Val(addrMode.Register1, PopFromStack());
                }
                else
                {
                    set_RAMn(false, PopFromStack());
                }
                clkCyc += 17;
            } // NOP
            else if (opCode == ((byte)(0x90)))
            {
                clkCyc += 3;
            } // XCHG CX AX
            else if (opCode == ((byte)(0x91)))
            {
                tmpUVal = (uint)(mRegisters.AX);
                mRegisters.AX = mRegisters.CX;
                mRegisters.CX = (ushort)(tmpUVal);
                clkCyc += 3;
            } // XCHG DX AX
            else if (opCode == ((byte)(0x92)))
            {
                tmpUVal = (uint)(mRegisters.AX);
                mRegisters.AX = mRegisters.DX;
                mRegisters.DX = (ushort)(tmpUVal);
                clkCyc += 3;
            } // XCHG BX AX
            else if (opCode == ((byte)(0x93)))
            {
                tmpUVal = (uint)(mRegisters.AX);
                mRegisters.AX = mRegisters.BX;
                mRegisters.BX = (ushort)(tmpUVal);
                clkCyc += 3;
            } // XCHG SP AX
            else if (opCode == ((byte)(0x94)))
            {
                tmpUVal = (uint)(mRegisters.AX);
                mRegisters.AX = mRegisters.SP;
                mRegisters.SP = (ushort)(tmpUVal);
                clkCyc += 3;
            } // XCHG BP AX
            else if (opCode == ((byte)(0x95)))
            {
                tmpUVal = (uint)(mRegisters.AX);
                mRegisters.AX = mRegisters.BP;
                mRegisters.BP = (ushort)(tmpUVal);
                clkCyc += 3;
            } // XCHG SI AX
            else if (opCode == ((byte)(0x96)))
            {
                tmpUVal = (uint)(mRegisters.AX);
                mRegisters.AX = mRegisters.SI;
                mRegisters.SI = (ushort)(tmpUVal);
                clkCyc += 3;
            } // XCHG DI AX
            else if (opCode == ((byte)(0x97)))
            {
                tmpUVal = (uint)(mRegisters.AX);
                mRegisters.AX = mRegisters.DI;
                mRegisters.DI = (ushort)(tmpUVal);
                clkCyc += 3;
            } // CBW
            else if (opCode == ((byte)(0x98)))
            {
                mRegisters.AX = To16bitsWithSign((ushort)(mRegisters.AL));
                clkCyc += 2;
            } // CWD
            else if (opCode == ((byte)(0x99)))
            {
                mRegisters.DX = (ushort)(((mRegisters.AH & 0x80) != 0) ? 0xFFFF : 0x0);
                clkCyc += 5;
            } // CALL Ap
            else if (opCode == ((byte)(0x9A)))
            {
                IPAddrOffet = Param(index: ParamIndex.First, size: DataSize.Word);
                tmpUVal = (uint)(Param(index: ParamIndex.Second, size: DataSize.Word));
                PushIntoStack(mRegisters.CS);
                PushIntoStack((ushort)(mRegisters.IP + opCodeSize));
                mRegisters.CS = (ushort)(tmpUVal);
                clkCyc += 28;
            } // WAIT
            else if (opCode == ((byte)(0x9B)))
            {
                clkCyc += 4;
            } // PUSHF
            else if (opCode == ((byte)(0x9C)))
            {
                //var tmpPsh = mModel == Models.IBMPC_5150 ? 0xFFF : 0xFFFF & mFlags.EFlags;
                PushIntoStack((ushort)(((mModel == Models.IBMPC_5150) ? 0xFFF : 0xFFFF) & mFlags.EFlags));
                //PushIntoStack((ushort)tmpPsh);
                clkCyc += 10;
            } // POPF
            else if (opCode == ((byte)(0x9D)))
            {
                mFlags.EFlags = PopFromStack();
                clkCyc += 8;
            } // SAHF
            else if (opCode == ((byte)(0x9E)))
            {
                var tmpmF = (mFlags.EFlags & 0xFF00) | mRegisters.AH;
                mFlags.EFlags = (ushort)tmpmF;
                clkCyc += 4;
            } // LAHF
            else if (opCode == ((byte)(0x9F)))
            {
                //Prepare 16bit FLAGS -> 8bit AH Register Workaround
                //https://mudongliang.github.io/x86/html/file_module_x86_id_148.html
                //GPFlags
                string binaryChain = mFlags.SF.ToString() + mFlags.ZF.ToString() + "0" + mFlags.AF.ToString() + "0"
                    + mFlags.PF.ToString() + "1" + mFlags.CF.ToString();
                byte tmpAHReg = (byte)Binary.From(binaryChain, Binary.Sizes.Byte);

                //mRegisters.AH = (byte)(mFlags.EFlags); //TODO: AH -> 134 on VB (undefined behaviour)
                mRegisters.AH = tmpAHReg;
                clkCyc += 4;
            } // MOV AL Ob
            else if (opCode == ((byte)(0xA0)))
            {
                mRegisters.AL = get_RAM8((ushort)(mRegisters.ActiveSegmentValue), Param(index: ParamIndex.First, size: DataSize.Word), (byte)0, false);
                clkCyc += 10;
            } // MOV AX Ov
            else if (opCode == ((byte)(0xA1)))
            {
                mRegisters.AX = get_RAM16((ushort)(mRegisters.ActiveSegmentValue), Param(index: ParamIndex.First, size: DataSize.Word), (byte)0, false);
                clkCyc += 10;
            } // MOV Ob AL
            else if (opCode == ((byte)(0xA2)))
            {
                set_RAM8((ushort)(mRegisters.ActiveSegmentValue), Param(index: ParamIndex.First, size: DataSize.Word), (byte)0, false, mRegisters.AL);
                clkCyc += 10;
            } // MOV Ov AX
            else if (opCode == ((byte)(0xA3)))
            {
                set_RAM16((ushort)(mRegisters.ActiveSegmentValue), Param(index: ParamIndex.First, size: DataSize.Word), (byte)0, false, mRegisters.AX);
                clkCyc += 10;
            }
            else if ((opCode >= 0xA4 && opCode <= 0xA7) || (opCode >= 0xAA && opCode <= 0xAF))
            {
                HandleREPMode();
            } // TEST AL Ib
            else if (opCode == ((byte)(0xA8)))
            {
                Eval((uint)(mRegisters.AL), (uint)(Param(index: ParamIndex.First, size: DataSize.Byte)), Operation.Test, DataSize.Byte);
                clkCyc += 4;
            } // TEST AX Iv
            else if (opCode == ((byte)(0xA9)))
            {
                Eval((uint)(mRegisters.AX), (uint)(Param(index: ParamIndex.First, size: DataSize.Word)), Operation.Test, DataSize.Word);
                clkCyc += 4;
            } // MOV AL Ib
            else if (opCode == ((byte)(0xB0)))
            {
                mRegisters.AL = (byte)Param(index: ParamIndex.First, size: DataSize.Byte);
                clkCyc += 4;
            } // MOV CL Ib
            else if (opCode == ((byte)(0xB1)))
            {
                mRegisters.CL = (byte)Param(index: ParamIndex.First, size: DataSize.Byte);
                clkCyc += 4;
            } // MOV DL Ib
            else if (opCode == ((byte)(0xB2)))
            {
                mRegisters.DL = (byte)Param(index: ParamIndex.First, size: DataSize.Byte);
                clkCyc += 4;
            } // MOV BL Ib
            else if (opCode == ((byte)(0xB3)))
            {
                mRegisters.BL = (byte)Param(index: ParamIndex.First, size: DataSize.Byte);
                clkCyc += 4;
            } // MOV AH Ib
            else if (opCode == ((byte)(0xB4)))
            {
                mRegisters.AH = (byte)Param(index: ParamIndex.First, size: DataSize.Byte);
                clkCyc += 4;
            } // MOV CH Ib
            else if (opCode == ((byte)(0xB5)))
            {
                mRegisters.CH = (byte)Param(index: ParamIndex.First, size: DataSize.Byte);
                clkCyc += 4;
            } // MOV DH Ib
            else if (opCode == ((byte)(0xB6)))
            {
                mRegisters.DH = (byte)Param(index: ParamIndex.First, size: DataSize.Byte);
                clkCyc += 4;
            } // MOV BH Ib
            else if (opCode == ((byte)(0xB7)))
            {
                mRegisters.BH = (byte)Param(index: ParamIndex.First, size: DataSize.Byte);
                clkCyc += 4;
            } // MOV AX Ib
            else if (opCode == ((byte)(0xB8)))
            {
                mRegisters.AX = Param(index: ParamIndex.First, size: DataSize.Word);
                clkCyc += 4;
            } // MOV CX Ib
            else if (opCode == ((byte)(0xB9)))
            {
                mRegisters.CX = Param(index: ParamIndex.First, size: DataSize.Word);
                clkCyc += 4;
            } // MOV DX Ib
            else if (opCode == ((byte)(0xBA)))
            {
                mRegisters.DX = Param(index: ParamIndex.First, size: DataSize.Word);
                clkCyc += 4;
            } // MOV BX Ib
            else if (opCode == ((byte)(0xBB)))
            {
                mRegisters.BX = Param(index: ParamIndex.First, size: DataSize.Word);
                clkCyc += 4;
            } // MOV SP Ib
            else if (opCode == ((byte)(0xBC)))
            {
                mRegisters.SP = Param(index: ParamIndex.First, size: DataSize.Word);
                clkCyc += 4;
            } // MOV BP Ib
            else if (opCode == ((byte)(0xBD)))
            {
                mRegisters.BP = Param(index: ParamIndex.First, size: DataSize.Word);
                clkCyc += 4;
            } // MOV SI Ib
            else if (opCode == ((byte)(0xBE)))
            {
                mRegisters.SI = Param(index: ParamIndex.First, size: DataSize.Word);
                clkCyc += 4;
            } // MOV DI Ib
            else if (opCode == ((byte)(0xBF)))
            {
                mRegisters.DI = Param(index: ParamIndex.First, size: DataSize.Word);
                clkCyc += 4;
            } // GRP2 byte/word imm8/16 ??? (80186)
            else if ((opCode == ((byte)(0xC0))) || (opCode == ((byte)(0xC1))))
            {
                if (mVic20)
                {
                    // PRE ALPHA CODE - UNTESTED
                    ExecuteGroup2();
                }
                else
                {
                    OpCodeNotImplemented();
                }
            } // RET Iw
            else if (opCode == ((byte)(0xC2)))
            {
                IPAddrOffet = PopFromStack();
                mRegisters.SP += Param(index: ParamIndex.First, size: DataSize.Word);
                clkCyc += 20;
            } // RET
            else if (opCode == ((byte)(0xC3)))
            {
                IPAddrOffet = PopFromStack();
                clkCyc += 16;
            } // LES / LDS Gv Mp
            else if (opCode >= 0xC4 && opCode <= 0xC5)
            {
                SetAddressing(DataSize.Word);
                if (((int)(addrMode.Register1) & shl2) == shl2)
                {
                    int tmpEnum = (int)addrMode.Register1 + (int)GPRegisters.RegistersTypes.ES;
                    addrMode.Register1 = (GPRegisters.RegistersTypes)tmpEnum;
                }

                var tmpval = (ushort)addrMode.Register1 | shl3;
                mRegisters.set_Val((GPRegisters.RegistersTypes)tmpval, addrMode.IndMem);
                mRegisters.set_Val(opCode == 0xC4 ? GPRegisters.RegistersTypes.ES : GPRegisters.RegistersTypes.DS, get_RAM16((ushort)(mRegisters.ActiveSegmentValue), addrMode.IndAdr, (byte)2, false));
                ignoreINTs = true;
                clkCyc += 16;
            } // MOV Eb Ib | MOV Ev Iv
            else if (opCode >= 0xC6 && opCode <= 0xC7)
            {
                SetAddressing();
                if (addrMode.IsDirect)
                {
                    mRegisters.set_Val(addrMode.Src, Param(ParamIndex.First, (ushort)(opCodeSize)));
                }
                else
                {
                    set_RAMn(false, Param(ParamIndex.First, (ushort)(opCodeSize)));
                }
                clkCyc += 10;
            } // ENTER (80186)
            else if (opCode == ((byte)(0xC8)))
            {
                if (mVic20)
                {
                    // PRE ALPHA CODE - UNTESTED
                    ushort stackSize = Param(index: ParamIndex.First, size: DataSize.Word);
                    ushort nestLevel = (ushort)(Param(index: ParamIndex.Second, size: DataSize.Byte) & 0x1F);
                    PushIntoStack(mRegisters.BP);
                    var frameTemp = mRegisters.SP;
                    if (nestLevel > 0)
                    {
                        for (int i = 1; i <= nestLevel - 1; i++)
                        {
                            mRegisters.BP -= (ushort)2;
                            //PushIntoStack(RAM16(frameTemp, mRegisters.BP))
                            PushIntoStack(mRegisters.BP);
                        }
                        PushIntoStack(frameTemp);
                    }
                    mRegisters.BP = frameTemp;
                    mRegisters.SP -= stackSize;

                    switch (nestLevel)
                    {
                        case (ushort)0:
                            clkCyc += 15;
                            break;
                        case (ushort)1:
                            clkCyc += 25;
                            break;
                        default:
                            clkCyc += 22 + 16 * (nestLevel - 1);
                            break;
                    }
                }
                else
                {
                    OpCodeNotImplemented();
                }
            } // LEAVE (80186)
            else if (opCode == ((byte)(0xC9)))
            {
                if (mVic20)
                {
                    mRegisters.SP = mRegisters.BP;
                    mRegisters.BP = PopFromStack();
                    clkCyc += 8;
                }
                else
                {
                    OpCodeNotImplemented();
                }
            } // RETF Iw
            else if (opCode == ((byte)(0xCA)))
            {
                tmpUVal = (uint)(Param(index: ParamIndex.First, size: DataSize.Word));
                IPAddrOffet = PopFromStack();
                mRegisters.CS = PopFromStack();
                mRegisters.SP += (ushort)(tmpUVal);
                clkCyc += 17;
            } // RETF
            else if (opCode == ((byte)(0xCB)))
            {
                IPAddrOffet = PopFromStack();
                mRegisters.CS = PopFromStack();
                clkCyc += 18;
            } // INT 3
            else if (opCode == ((byte)(0xCC)))
            {
                HandleInterrupt((byte)3, false);
                clkCyc++;
            } // INT Ib
            else if (opCode == ((byte)(0xCD)))
            {
                HandleInterrupt((byte)Param(index: ParamIndex.First, size: DataSize.Byte), false);
                clkCyc += 0;
            } // INTO
            else if (opCode == ((byte)(0xCE)))
            {
                if (mFlags.OF == 1)
                {
                    HandleInterrupt((byte)4, false);
                    clkCyc += 3;
                }
                else
                {
                    clkCyc += 4;
                }
            } // IRET
            else if (opCode == ((byte)(0xCF)))
            {
                IPAddrOffet = PopFromStack();
                mRegisters.CS = PopFromStack();
                mFlags.EFlags = PopFromStack();
                clkCyc += 32;
            }
            else if (opCode >= 0xD0 && opCode <= 0xD3)
            {
                ExecuteGroup2();
            } // AAM I0
            else if (opCode == ((byte)(0xD4)))
            {
                tmpUVal = (Param(index: ParamIndex.First, size: DataSize.Byte));
                if ((ulong)tmpUVal == 0)
                {
                    HandleInterrupt((byte)0, true);
                    return;
                }
                mRegisters.AH = (byte)(mRegisters.AL / tmpUVal);
                mRegisters.AL = (byte)(mRegisters.AL % tmpUVal);
                SetSZPFlags((uint)(mRegisters.AX), DataSize.Word);
                clkCyc += 83;
            } // AAD I0
            else if (opCode == ((byte)(0xD5)))
            {
                tmpUVal = (uint)(Param(index: ParamIndex.First, size: DataSize.Byte));
                tmpUVal = tmpUVal * mRegisters.AH + mRegisters.AL;
                mRegisters.AL = (byte)tmpUVal;
                mRegisters.AH = (byte)0;
                SetSZPFlags(tmpUVal, DataSize.Word);
                mFlags.SF = (byte)0;
                clkCyc += 60;
            } // XLAT for V20 / SALC
            else if (opCode == ((byte)(0xD6)))
            {
                if (mVic20)
                {
                    mRegisters.AL = get_RAM8((ushort)(mRegisters.ActiveSegmentValue), (ushort)(mRegisters.BX + mRegisters.AL), (byte)0, false);
                }
                else
                {
                    mRegisters.AL = (byte)(mFlags.CF == 1 ? 0xFF : 0x0);
                    clkCyc += 4;
                }
            } // XLATB
            else if (opCode == ((byte)(0xD7)))
            {
                mRegisters.AL = get_RAM8((ushort)(mRegisters.ActiveSegmentValue), (ushort)(mRegisters.BX + mRegisters.AL), (byte)0, false);
                clkCyc += 11;
            } // Ignore 8087 co-processor instructions
            else if (opCode >= 0xD8 && opCode <= 0xDF)
            {
                SetAddressing();
                //FPU.Execute(opCode, addrMode)

                // Lesson 2
                // http://ntsecurity.nu/onmymind/2007/2007-08-22.html

                //HandleInterrupt(7, False)
                OpCodeNotImplemented("FPU Not Available");
                clkCyc += 2;
            } // LOOPNE/LOOPNZ
            else if (opCode == ((byte)(0xE0)))
            {
                mRegisters.CX--;
                if (mRegisters.CX > 0 && mFlags.ZF == 0)
                {
                    IPAddrOffet = OffsetIP(DataSize.Byte);
                    clkCyc += 19;
                }
                else
                {
                    opCodeSize++;
                    clkCyc += 5;
                }
            } // LOOPE/LOOPZ
            else if (opCode == ((byte)(0xE1)))
            {
                mRegisters.CX--;
                if (mRegisters.CX > 0 && mFlags.ZF == 1)
                {
                    IPAddrOffet = OffsetIP(DataSize.Byte);
                    clkCyc += 18;
                }
                else
                {
                    opCodeSize++;
                    clkCyc += 6;
                }
            } // LOOP
            else if (opCode == ((byte)(0xE2)))
            {
                mRegisters.CX--;
                if (mRegisters.CX > 0)
                {
                    IPAddrOffet = OffsetIP(DataSize.Byte);
                    clkCyc += 17;
                }
                else
                {
                    opCodeSize++;
                    clkCyc += 5;
                }
            } // JCXZ/JECXZ
            else if (opCode == ((byte)(0xE3)))
            {
                if (mRegisters.CX == 0)
                {
                    IPAddrOffet = OffsetIP(DataSize.Byte);
                    clkCyc += 18;
                }
                else
                {
                    opCodeSize++;
                    clkCyc += 6;
                }
            } // IN AL Ib
            else if (opCode == ((byte)(0xE4)))
            {
                mRegisters.AL = (byte)(ReceiveFromPort((uint)(Param(index: ParamIndex.First, size: DataSize.Byte))));
                clkCyc += 10;
            } // IN AX Ib
            else if (opCode == ((byte)(0xE5)))
            {
                mRegisters.AX = (ushort)(ReceiveFromPort((uint)(Param(index: ParamIndex.First, size: DataSize.Byte))));
                clkCyc += 10;
            } // OUT Ib AL
            else if (opCode == ((byte)(0xE6)))
            {
                SendToPort((uint)(Param(index: ParamIndex.First, size: DataSize.Byte)), (uint)(mRegisters.AL));
                clkCyc += 10;
            } // OUT Ib AX
            else if (opCode == ((byte)(0xE7)))
            {
                SendToPort((uint)(Param(index: ParamIndex.First, size: DataSize.Byte)), (uint)(mRegisters.AX));
                clkCyc += 10;
            } // CALL Jv
            else if (opCode == ((byte)(0xE8)))
            {
                IPAddrOffet = OffsetIP(DataSize.Word);
                PushIntoStack((ushort)(Registers.IP + opCodeSize));
                clkCyc += 19;
            } // JMP Jv
            else if (opCode == ((byte)(0xE9)))
            {
                IPAddrOffet = OffsetIP(DataSize.Word);
                clkCyc += 15;
            } // JMP Ap
            else if (opCode == ((byte)(0xEA)))
            {
                IPAddrOffet = Param(index: ParamIndex.First, size: DataSize.Word);
                mRegisters.CS = Param(index: ParamIndex.Second, size: DataSize.Word);
                clkCyc += 15;
            } // JMP Jb
            else if (opCode == ((byte)(0xEB)))
            {
                IPAddrOffet = OffsetIP(DataSize.Byte);
                clkCyc += 15;
            } // IN AL DX
            else if (opCode == ((byte)(0xEC)))
            {
                mRegisters.AL = (byte)ReceiveFromPort((uint)(mRegisters.DX));
                clkCyc += 8;
            } // IN AX DX
            else if (opCode == ((byte)(0xED)))
            {
                mRegisters.AX = (ushort)(ReceiveFromPort((uint)(mRegisters.DX)));
                clkCyc += 8;
            } // OUT DX AL
            else if (opCode == ((byte)(0xEE)))
            {
                SendToPort((uint)(mRegisters.DX), (uint)(mRegisters.AL));
                clkCyc += 8;
            } // OUT DX AX
            else if (opCode == ((byte)(0xEF)))
            {
                SendToPort((uint)(mRegisters.DX), (uint)(mRegisters.AX));
                clkCyc += 8;
            } // LOCK
            else if (opCode == ((byte)(0xF0)))
            {
                OpCodeNotImplemented("LOCK");
                clkCyc += 2;
            } // REPBE/REPNZ
            else if (opCode == ((byte)(0xF2)))
            {
                mRepeLoopMode = REPLoopModes.REPENE;
                newPrefix = true;
                clkCyc += 2;
            } // repe/repz
            else if (opCode == ((byte)(0xF3)))
            {
                mRepeLoopMode = REPLoopModes.REPE;
                newPrefix = true;
                clkCyc += 2;
            } // HLT
            else if (opCode == ((byte)(0xF4)))
            {
                if (!mIsHalted)
                {
                    SystemHalted();
                }
                mRegisters.IP--;
                clkCyc += 2;
            } // CMC
            else if (opCode == ((byte)(0xF5)))
            {
                mFlags.CF = (byte)(mFlags.CF == 0 ? 1 : 0);
                clkCyc += 2;
            }
            else if (opCode >= 0xF6 && opCode <= 0xF7)
            {
                ExecuteGroup3();
            } // CLC
            else if (opCode == ((byte)(0xF8)))
            {
                mFlags.CF = (byte)0;
                clkCyc += 2;
            } // STC
            else if (opCode == ((byte)(0xF9)))
            {
                mFlags.CF = (byte)1;
                clkCyc += 2;
            } // CLI
            else if (opCode == ((byte)(0xFA)))
            {
                mFlags.IF = (byte)0;
                clkCyc += 2;
            } // STI
            else if (opCode == ((byte)(0xFB)))
            {
                mFlags.IF = (byte)1;
                ignoreINTs = true; // http://zet.aluzina.org/forums/viewtopic.php?f=6&t=287
                clkCyc += 2;
            } // CLD
            else if (opCode == ((byte)(0xFC)))
            {
                mFlags.DF = (byte)0;
                clkCyc += 2;
            } // STD
            else if (opCode == ((byte)(0xFD)))
            {
                mFlags.DF = (byte)1;
                clkCyc += 2;
            }
            else if ((opCode == ((byte)(0xFE))) || (opCode == ((byte)(0xFF))))
            {
                ExecuteGroup4_And_5();
            }
            else
            {
                OpCodeNotImplemented();
            }
        }

        private void PostExecute()
        {
            if (useIPAddrOffset)
            {
                mRegisters.IP = IPAddrOffet;
            }
            else
            {
                mRegisters.IP += (ushort)(opCodeSize);
            }

            clkCyc += opCodeSize * 4;

            if (!newPrefix)
            {
                if (mRepeLoopMode != REPLoopModes.None)
                {
                    mRepeLoopMode = REPLoopModes.None;
                }
                if (mRegisters.ActiveSegmentChanged)
                {
                    mRegisters.ResetActiveSegment();
                }
                newPrefixLast = 0;
            }
            else
            {
                newPrefixLast++;
            }
        }

        private void ExecuteGroup1() // &H80 To &H83
        {
            SetAddressing();

            ushort arg1 = (ushort)(addrMode.IsDirect ? (mRegisters.get_Val(addrMode.Register2)) : addrMode.IndMem);
            ushort arg2 = (ushort)(Param(ParamIndex.First, (ushort)(opCodeSize), opCode == 0x83 ? DataSize.Byte : addrMode.Size));
            if (opCode == 0x83)
            {
                arg2 = To16bitsWithSign(arg2);
            }

            if (addrMode.Reg == ((byte)0)) // ADD Eb Ib | Ev Iv | Ev Ib (opcode 83h only)
            {
                if (addrMode.IsDirect)
                {
                    mRegisters.set_Val(addrMode.Register2, Eval((uint)(arg1), (uint)(arg2), Operation.Add, addrMode.Size));
                    clkCyc += 4;
                }
                else
                {
                    set_RAMn(false, Eval((uint)(arg1), (uint)(arg2), Operation.Add, addrMode.Size));
                    clkCyc += 17;
                }
            } // OR Eb Ib | Ev Iv | Ev Ib (opcode 83h only)
            else if (addrMode.Reg == ((byte)1))
            {
                if (addrMode.IsDirect)
                {
                    mRegisters.set_Val(addrMode.Register2, Eval((uint)(arg1), (uint)(arg2), Operation.LogicOr, addrMode.Size));
                    clkCyc += 4;
                }
                else
                {
                    set_RAMn(false, Eval((uint)(arg1), (uint)(arg2), Operation.LogicOr, addrMode.Size));
                    clkCyc += 17;
                }
            } // ADC Eb Ib | Ev Iv | Ev Ib (opcode 83h only)
            else if (addrMode.Reg == ((byte)2))
            {
                if (addrMode.IsDirect)
                {
                    mRegisters.set_Val(addrMode.Register2, Eval((uint)(arg1), (uint)(arg2), Operation.AddWithCarry, addrMode.Size));
                    clkCyc += 4;
                }
                else
                {
                    set_RAMn(false, Eval((uint)(arg1), (uint)(arg2), Operation.AddWithCarry, addrMode.Size));
                    clkCyc += 17;
                }
            } // SBB Eb Ib | Ev Iv | Ev Ib (opcode 83h only)
            else if (addrMode.Reg == ((byte)3))
            {
                if (addrMode.IsDirect)
                {
                    mRegisters.set_Val(addrMode.Register2, Eval((uint)(arg1), (uint)(arg2), Operation.SubstractWithCarry, addrMode.Size));
                    clkCyc += 4;
                }
                else
                {
                    set_RAMn(false, Eval((uint)(arg1), (uint)(arg2), Operation.SubstractWithCarry, addrMode.Size));
                    clkCyc += 17;
                }
            } // AND Eb Ib | Ev Iv | Ev Ib (opcode 83h only)
            else if (addrMode.Reg == ((byte)4))
            {
                if (addrMode.IsDirect)
                {
                    mRegisters.set_Val(addrMode.Register2, Eval((uint)(arg1), (uint)(arg2), Operation.LogicAnd, addrMode.Size));
                    clkCyc += 4;
                }
                else
                {
                    set_RAMn(false, Eval((uint)(arg1), (uint)(arg2), Operation.LogicAnd, addrMode.Size));
                    clkCyc += 17;
                }
            } // SUB Eb Ib | Ev Iv | Ev Ib (opcode 83h only)
            else if (addrMode.Reg == ((byte)5))
            {
                if (addrMode.IsDirect)
                {
                    mRegisters.set_Val(addrMode.Register2, Eval((uint)(arg1), (uint)(arg2), Operation.Substract, addrMode.Size));
                    clkCyc += 4;
                }
                else
                {
                    set_RAMn(false, Eval((uint)(arg1), (uint)(arg2), Operation.Substract, addrMode.Size));
                    clkCyc += 17;
                }
            } // XOR Eb Ib | Ev Iv | Ev Ib (opcode 83h only)
            else if (addrMode.Reg == ((byte)6))
            {
                if (addrMode.IsDirect)
                {
                    mRegisters.set_Val(addrMode.Register2, Eval((uint)(arg1), (uint)(arg2), Operation.LogicXor, addrMode.Size));
                    clkCyc += 4;
                }
                else
                {
                    set_RAMn(false, Eval((uint)(arg1), (uint)(arg2), Operation.LogicXor, addrMode.Size));
                    clkCyc += 17;
                }
            } // CMP Eb Ib | Ev Iv | Ev Ib (opcode 83h only)
            else if (addrMode.Reg == ((byte)7))
            {
                Eval((uint)(arg1), (uint)(arg2), Operation.Compare, addrMode.Size);
                clkCyc += addrMode.IsDirect ? 4 : 10;
            }
        }

        private void ExecuteGroup2() // &HD0 To &HD3 / &HC0 To &HC1
        {
            SetAddressing();

            uint newValue = 0;
            uint count = 0;
            uint oldValue = 0;

            uint mask80_8000 = 0;
            uint mask07_15 = 0;
            uint maskFF_FFFF = 0;
            uint mask8_16 = 0;
            uint mask9_17 = 0;
            uint mask100_10000 = 0;
            uint maskFF00_FFFF0000 = 0;

            if (addrMode.Size == DataSize.Byte)
            {
                mask80_8000 = (uint)(0x80);
                mask07_15 = (uint)(0x7);
                maskFF_FFFF = (uint)(0xFF);
                mask8_16 = (uint)8;
                mask9_17 = (uint)9;
                mask100_10000 = (uint)(0x100);
                maskFF00_FFFF0000 = (uint)(0xFF00);
            }
            else
            {
                mask80_8000 = (uint)(0x8000);
                mask07_15 = (uint)(0xF);
                maskFF_FFFF = (uint)(0xFFFF);
                mask8_16 = (uint)16;
                mask9_17 = (uint)17;
                mask100_10000 = 65536u;
                maskFF00_FFFF0000 = 4294901760u;
            }

            if (addrMode.IsDirect)
            {
                oldValue = (uint)(mRegisters.get_Val(addrMode.Register2));
                if (opCode >= 0xD2)
                {
                    clkCyc += 8;
                }
                else
                {
                    clkCyc += 2;
                }
            }
            else
            {
                oldValue = (uint)(addrMode.IndMem);
                if (opCode >= 0xD2)
                {
                    clkCyc += 20;
                }
                else
                {
                    clkCyc += 13;
                }
            }

            if ((opCode == ((byte)(0xD0))) || (opCode == ((byte)(0xD1))))
            {
                count = (uint)1;
            }
            else if ((opCode == ((byte)(0xD2))) || (opCode == ((byte)(0xD3))))
            {
                count = (uint)(mRegisters.CL);
            }
            else if ((opCode == ((byte)(0xC0))) || (opCode == ((byte)(0xC1))))
            {
                count = (uint)(Param(index: ParamIndex.First, size: DataSize.Byte));
            }

            // 80186/V20 class CPUs limit shift count to 31
            if (mVic20)
            {
                count = count & 0x1F;
            }
            clkCyc += 4 * count;

            if (count == 0)
            {
                newValue = oldValue;
            }

            if (addrMode.Reg == ((byte)0)) // ROL Gb CL/Ib | Gv CL/Ib
            {
                if (count == 1)
                {
                    newValue = ((oldValue << 1) | (oldValue >> (int)mask07_15));
                    mFlags.CF = (byte)(((oldValue & mask80_8000) != 0) ? 1 : 0);
                    mFlags.OF = (byte)((((oldValue ^ newValue) & mask80_8000) != 0) ? 1 : 0);
                }
                else if (count > 1)
                {
                    newValue = ((oldValue << (int)(count & mask07_15)) | (oldValue >> (int)(mask8_16 - (count & mask07_15))));
                    mFlags.CF = (byte)(newValue & 1);
                    mFlags.OF = (byte)((((oldValue ^ newValue) & mask80_8000) != 0) ? 1 : 0);
                }
            } // ROR Gb CL/Ib | Gv CL/Ib
            else if (addrMode.Reg == ((byte)1))
            {
                if (count == 1)
                {
                    newValue = ((oldValue >> 1) | (oldValue << (int)mask07_15));
                    mFlags.CF = (byte)(oldValue & 1);
                    mFlags.OF = (byte)((((oldValue ^ newValue) & mask80_8000) != 0) ? 1 : 0);
                }
                else if (count > 1)
                {
                    newValue = ((oldValue >> (int)(count & mask07_15)) | (oldValue << (int)(mask8_16 - (count & mask07_15))));
                    mFlags.CF = (byte)(((newValue & mask80_8000) != 0) ? 1 : 0);
                    mFlags.OF = (byte)((((oldValue ^ newValue) & mask80_8000) != 0) ? 1 : 0);
                }
            } // RCL Gb CL/Ib | Gv CL/Ib
            else if (addrMode.Reg == ((byte)2))
            {
                if (count == 1)
                {
                    newValue = ((oldValue << 1) | mFlags.CF);
                    mFlags.CF = (byte)(((oldValue & mask80_8000) != 0) ? 1 : 0);
                    mFlags.OF = (byte)((((oldValue ^ newValue) & mask80_8000) != 0) ? 1 : 0);
                }
                else if (count > 1)
                {
                    oldValue = oldValue | ((uint)(mFlags.CF) << (int)mask8_16);
                    newValue = (uint)((oldValue << (int)(count % mask9_17)) | (oldValue >> (int)(mask9_17 - (count % mask9_17))));
                    mFlags.CF = (byte)(((newValue & mask100_10000) != 0) ? 1 : 0);
                    mFlags.OF = (byte)((((oldValue ^ newValue) & mask80_8000) != 0) ? 1 : 0);
                }
            } // RCR Gb CL/Ib | Gv CL/Ib
            else if (addrMode.Reg == ((byte)3))
            {
                if (count == 1)
                {
                    newValue = ((oldValue >> 1) | ((uint)(mFlags.CF) << (int)mask07_15));
                    mFlags.CF = (byte)(oldValue & 1);
                    mFlags.OF = (byte)((((oldValue ^ newValue) & mask80_8000) != 0) ? 1 : 0);
                }
                else if (count > 1)
                {
                    oldValue = oldValue | ((uint)mFlags.CF << (int)mask8_16);
                    newValue = (uint)((oldValue >> (int)(count % mask9_17)) | (oldValue << (int)(mask9_17 - (count % mask9_17))));
                    mFlags.CF = (byte)(((newValue & mask100_10000) != 0) ? 1 : 0);
                    mFlags.OF = (byte)((((oldValue ^ newValue) & mask80_8000) != 0) ? 1 : 0);
                }
                else
                {
                    mFlags.OF = (byte)0;
                }
            } // SHL/SAL Gb CL/Ib | Gv CL/Ib
            else if ((addrMode.Reg == ((byte)4)) || (addrMode.Reg == ((byte)6)))
            {
                if (count == 1)
                {
                    newValue = oldValue << 1;
                    mFlags.CF = (byte)(((oldValue & mask80_8000) != 0) ? 1 : 0);
                    mFlags.OF = (byte)((((oldValue ^ newValue) & mask80_8000) != 0) ? 1 : 0);
                }
                else if (count > 1)
                {
                    newValue = (uint)(count > mask8_16 ? 0 : oldValue << (int)count);
                    mFlags.CF = (byte)(((newValue & mask100_10000) != 0) ? 1 : 0);
                    mFlags.OF = (byte)((((oldValue ^ newValue) & mask80_8000) != 0) ? 1 : 0);
                }
                else
                {
                    mFlags.OF = (byte)0;
                }
                SetSZPFlags(newValue, addrMode.Size);
            } // SHR Gb CL/Ib | Gv CL/Ib
            else if (addrMode.Reg == ((byte)5))
            {
                if (count == 1)
                {
                    newValue = oldValue >> 1;
                    mFlags.CF = (byte)(oldValue & 1);
                    mFlags.OF = (byte)((((oldValue ^ newValue) & mask80_8000) != 0) ? 1 : 0);
                }
                else if (count > 1)
                {
                    newValue = (uint)(count > mask8_16 ? 0 : (oldValue >> (int)(count - 1)));
                    mFlags.CF = (byte)(newValue & 1);
                    newValue >>= (int)1;
                    mFlags.OF = (byte)((((oldValue ^ newValue) & mask80_8000) != 0) ? 1 : 0);
                }
                else
                {
                    mFlags.OF = (byte)0;
                }
                SetSZPFlags(newValue, addrMode.Size);
            } // SAR Gb CL/Ib | Gv CL/Ib
            else if (addrMode.Reg == ((byte)7))
            {
                if (count == 1)
                {
                    newValue = (uint)((oldValue >> 1) | (oldValue & mask80_8000));
                    mFlags.CF = (byte)(oldValue & 1);
                }
                else if (count > 1)
                {
                    oldValue = (uint)(oldValue | (((oldValue & mask80_8000) != 0) ? maskFF00_FFFF0000 : 0));
                    newValue = (uint)(oldValue >> (int)(count >= mask8_16 ? mask07_15 : count - 1));
                    mFlags.CF = (byte)(newValue & 1);
                    newValue = (uint)((newValue >> 1) & maskFF_FFFF);
                }
                mFlags.OF = (byte)0;
                SetSZPFlags(newValue, addrMode.Size);
            }
            else
            {
                OpCodeNotImplemented("Unknown Reg Mode {addrMode.Reg} for Opcode {opCode:X} (Group2)");
            }

            if (addrMode.IsDirect)
            {
                mRegisters.set_Val(addrMode.Register2, (ushort)newValue);
            }
            else
            {
                set_RAMn(false, (ushort)newValue);
            }
        }

        private void ExecuteGroup3() // &HF6 To &HF7
        {
            SetAddressing();

            if (addrMode.Reg == ((byte)0)) // TEST Eb Ib | Ev Iv
            {
                if (addrMode.IsDirect)
                {
                    Eval((uint)(mRegisters.get_Val(addrMode.Register2)), (uint)(Param(ParamIndex.First, (ushort)(opCodeSize))), Operation.Test, addrMode.Size);
                    clkCyc += 5;
                }
                else
                {
                    Eval((uint)(addrMode.IndMem), (uint)(Param(ParamIndex.First, (ushort)(opCodeSize))), Operation.Test, addrMode.Size);
                    clkCyc += 11;
                }
            } // NOT Eb | Ev
            else if (addrMode.Reg == ((byte)2))
            {
                if (addrMode.IsDirect)
                {
                    //TODO: VERIFY
                    mRegisters.set_Val(addrMode.Register2, (ushort)~mRegisters.get_Val(addrMode.Register2));
                    clkCyc += 3;
                }
                else
                {
                    set_RAMn(false, (ushort)~addrMode.IndMem);
                    clkCyc += 16;
                }
            } // NEG Eb | Ev
            else if (addrMode.Reg == ((byte)3))
            {
                if (addrMode.IsDirect)
                {
                    //Eval((uint) 0, (uint)(mRegisters.get_Val(addrMode.Register2)), Operation.Substract, addrMode.Size);
                    //tmpUVal = (uint)((~mRegisters.get_Val(addrMode.Register2)) + 1);
                    Eval(0u, mRegisters.get_Val(addrMode.Register2), Operation.Substract, addrMode.Size);
                    tmpUVal = (uint)((ushort)(~mRegisters.get_Val(addrMode.Register2)) + 1);
                    mRegisters.set_Val(addrMode.Register2, (ushort)tmpUVal);
                    clkCyc += 3;
                }
                else
                {
                    //Eval((uint) 0, (uint)(addrMode.IndMem), Operation.Substract, addrMode.Size);
                    //tmpUVal = (uint)((~addrMode.IndMem) + 1);
                    Eval(0u, addrMode.IndMem, Operation.Substract, addrMode.Size);
                    tmpUVal = (uint)((ushort)(~addrMode.IndMem) + 1);
                    set_RAMn(false, (ushort)tmpUVal);
                    clkCyc += 16;
                }
            } // MUL
            else if (addrMode.Reg == ((byte)4))
            {
                if (addrMode.IsDirect)
                {
                    if (addrMode.Size == DataSize.Byte)
                    {
                        tmpUVal = (uint)(mRegisters.get_Val(addrMode.Register2) * mRegisters.AL);
                        clkCyc += 70;
                    }
                    else
                    {
                        tmpUVal = (uint)((mRegisters.get_Val(addrMode.Register2)) * mRegisters.AX);
                        mRegisters.DX = (ushort)(tmpUVal >> 16);
                        clkCyc += 118;
                    }
                }
                else
                {
                    if (addrMode.Size == DataSize.Byte)
                    {
                        tmpUVal = (uint)(addrMode.IndMem * mRegisters.AL);
                        clkCyc += 76;
                    }
                    else
                    {
                        tmpUVal = (uint)((addrMode.IndMem) * mRegisters.AX);
                        mRegisters.DX = (ushort)(tmpUVal >> 16);
                        clkCyc += 134;
                    }
                }
                mRegisters.AX = (ushort)(tmpUVal);

                SetSZPFlags(tmpUVal, addrMode.Size);
                //if ((tmpUVal & (addrMode.Size == DataSize.Byte ? 0xFF00 : -65536)) != 0)
                if ((tmpUVal & ((addrMode.Size == DataSize.Byte) ? 65280 : (-65536))) != 0L)
                {
                    mFlags.CF = (byte)1;
                    mFlags.OF = (byte)1;
                }
                else
                {
                    mFlags.CF = (byte)0;
                    mFlags.OF = (byte)0;
                }
                mFlags.ZF = (byte)(mVic20 ? (tmpUVal != 0 ? 1 : 0) : 0); // This is the test the BIOS uses to detect a VIC20 (8018x)
            } // IMUL
            else if (addrMode.Reg == ((byte)5))
            {
                if (addrMode.IsDirect)
                {
                    if (addrMode.Size == DataSize.Byte)
                    {
                        uint m1 = (uint)(To16bitsWithSign((ushort)(mRegisters.AL)));
                        uint m2 = (uint)(To16bitsWithSign(mRegisters.get_Val(addrMode.Register2)));

                        //m1 = (uint)(((m1 & 0x80) != 0) ? m1 | 0xFFFF_FF00 : m1);
                        //m2 = (uint)(((m2 & 0x80) != 0) ? m2 | 0xFFFF_FF00 : m2);
                        m1 = (uint)((((long)m1 & 128L) != 0L) ? (m1 | -256) : m1);
                        m2 = (uint)((((long)m2 & 128L) != 0L) ? (m2 | -256) : m2);

                        tmpUVal = m1 * m2;
                        mRegisters.AX = (ushort)(tmpUVal);
                        clkCyc += 70;
                    }
                    else
                    {
                        uint m1 = To32bitsWithSign(mRegisters.AX);
                        uint m2 = To32bitsWithSign(mRegisters.get_Val(addrMode.Register2));

                        //m1 = (uint)(((m1 & 0x8000) != 0) ? m1 | -65536 : m1);
                        //m2 = (uint)(((m2 & 0x8000) != 0) ? m2 | -65536 : m2);
                        m1 = (uint)((((long)m1 & 32768L) != 0L) ? (m1 | -65536) : m1);
                        m2 = (uint)((((long)m2 & 32768L) != 0L) ? (m2 | -65536) : m2);

                        tmpUVal = m1 * m2;
                        mRegisters.AX = (ushort)(tmpUVal);
                        mRegisters.DX = (ushort)(tmpUVal >> 16);
                        clkCyc += 118;
                    }
                }
                else
                {
                    if (addrMode.Size == DataSize.Byte)
                    {
                        uint m1 = (uint)(To16bitsWithSign((ushort)(mRegisters.AL)));
                        uint m2 = (uint)(To16bitsWithSign(addrMode.IndMem));

                        //m1 = (uint)(((m1 & 0x80) != 0) ? m1 | 0xFFFF_FF00 : m1);
                        //m2 = (uint)(((m2 & 0x80) != 0) ? m2 | 0xFFFF_FF00 : m2);
                        m1 = (uint)((((long)m1 & 128L) != 0L) ? (m1 | -256) : m1);
                        m2 = (uint)((((long)m2 & 128L) != 0L) ? (m2 | -256) : m2);

                        tmpUVal = m1 * m2;
                        mRegisters.AX = (ushort)(tmpUVal);
                        clkCyc += 76;
                    }
                    else
                    {
                        uint m1 = To32bitsWithSign(mRegisters.AX);
                        uint m2 = To32bitsWithSign(addrMode.IndMem);

                        //m1 = (uint)(((m1 & 0x8000) != 0) ? m1 | -65536 : m1);
                        //m2 = (uint)(((m2 & 0x8000) != 0) ? m2 | -65536 : m2);
                        m1 = (uint)((((long)m1 & 32768L) != 0L) ? (m1 | -65536) : m1);
                        m2 = (uint)((((long)m2 & 32768L) != 0L) ? (m2 | -65536) : m2);

                        tmpUVal = m1 * m2;
                        mRegisters.AX = (ushort)(tmpUVal);
                        mRegisters.DX = (ushort)(tmpUVal >> 16);
                        clkCyc += 134;
                    }
                }

                if ((addrMode.Size == DataSize.Byte ? mRegisters.AH : mRegisters.DX) != 0)
                {
                    mFlags.CF = (byte)1;
                    mFlags.OF = (byte)1;
                }
                else
                {
                    mFlags.CF = (byte)0;
                    mFlags.OF = (byte)0;
                }
                if (!mVic20)
                {
                    mFlags.ZF = (byte)0;
                }
            } // DIV
            else if (addrMode.Reg == ((byte)6))
            {
                //uint div = 0;
                uint num = 0;
                uint result = 0;
                uint remain = 0;

                uint div = (!addrMode.IsDirect) ? addrMode.IndMem : mRegisters.get_Val(addrMode.Register2);
                //if (addrMode.IsDirect)
                //{
                //	div = (uint)(mRegisters.get_Val(addrMode.Register2));
                //}
                //else
                //{
                //	div = (uint)(addrMode.IndMem);
                //}

                if (addrMode.Size == DataSize.Byte)
                {
                    num = mRegisters.AX;
                    clkCyc += 86;
                }
                else
                {
                    num = (uint)((mRegisters.DX << 16) | mRegisters.AX);
                    clkCyc += 150;
                }

                if (div == 0)
                {
                    HandleInterrupt(0, true);
                    return;
                }

                result = num / div;
                remain = num % div;

                if (addrMode.Size == DataSize.Byte)
                {
                    if ((long)result > (long)0xFF)
                    {
                        HandleInterrupt(0, true);
                        return;
                    }
                    mRegisters.AL = (byte)result;
                    mRegisters.AH = (byte)remain;
                }
                else if ((long)result > (long)0xFFFF)
                {
                    HandleInterrupt((byte)0, true);
                }
                else
                {
                    mRegisters.AX = (ushort)result;
                    mRegisters.DX = (ushort)remain;
                }
                return;
            } // IDIV
            else if (addrMode.Reg == ((byte)7))
            {
                uint div = 0;
                uint num = 0;
                uint result = 0;
                uint remain = 0;
                bool signN = false;
                bool signD = false;

                if (addrMode.IsDirect)
                {
                    if (addrMode.Size == DataSize.Byte)
                    {
                        num = (uint)(mRegisters.AX);
                        div = (uint)(To16bitsWithSign(mRegisters.get_Val(addrMode.Register2)));

                        signN = (num & 0x8000) != 0;
                        signD = (div & 0x8000) != 0;
                        num = (uint)(signN ? (((long)(~num) + 1L) & 0xFFFF) : num);
                        div = (uint)(signD ? (((long)(~div) + 1L) & 0xFFFF) : div);

                        clkCyc += 80;
                    }
                    else
                    {
                        num = (uint)((mRegisters.DX << 16) | mRegisters.AX);
                        div = To32bitsWithSign(mRegisters.get_Val(addrMode.Register2));

                        //signN = ((long)num & 0x8000_0000) != 0;
                        //signD = ((long)div & 0x8000_0000) != 0;
                        signN = ((num & int.MinValue) != 0);
                        signD = ((div & int.MinValue) != 0);
                        num = (uint)(signN ? (((long)(~num) + 1) & -1) : num);
                        div = (uint)(signD ? (((long)(~div) + 1) & -1) : div);

                        clkCyc += 144;
                    }
                }
                else
                {
                    if (addrMode.Size == DataSize.Byte)
                    {
                        num = (uint)(mRegisters.AX);
                        div = (uint)(To16bitsWithSign(addrMode.IndMem));

                        signN = ((long)num & 0x8000) != 0;
                        signD = ((long)div & 0x8000) != 0;
                        num = (uint)(signN ? (((long)(~num) + 1) & 0xFFFF) : num);
                        div = (uint)(signD ? (((long)(~div) + 1) & 0xFFFF) : div);

                        clkCyc += 86;
                    }
                    else
                    {
                        num = (uint)((mRegisters.DX << 16) | mRegisters.AX);
                        div = To32bitsWithSign(addrMode.IndMem);

                        signN = (num & int.MinValue) != 0;
                        signD = (div & int.MinValue) != 0;
                        //num = (signN ? (((~num) + 1) & 0xFFFF_FFFF) : num);
                        //div = (signD ? (((~div) + 1) & 0xFFFF_FFFF) : div);
                        num = (uint)(signN ? (((long)(~num) + 1L) & -1) : num);
                        div = (uint)(signD ? (((long)(~div) + 1L) & -1) : div);

                        clkCyc += 150;
                    }
                }

                if (div == 0)
                {
                    HandleInterrupt((byte)0, true);
                    return;
                }

                result = num / div;
                remain = num % div;

                if (signN != signD)
                {
                    if (result > (addrMode.Size == DataSize.Byte ? 0x80 : 0x8000))
                    {
                        HandleInterrupt((byte)0, true);
                        return;
                    }
                    result = (uint)((long)(~result) + 1L);
                }
                else
                {
                    if (result > (addrMode.Size == DataSize.Byte ? 0x7F : 0x7FFF))
                    {
                        HandleInterrupt((byte)0, true);
                        return;
                    }
                }

                if (signN)
                {
                    remain = (uint)((long)(~remain) + 1L);
                }

                if (addrMode.Size == DataSize.Byte)
                {
                    mRegisters.AL = (byte)result;
                    mRegisters.AH = (byte)remain;
                }
                else
                {
                    mRegisters.AX = (ushort)(result);
                    mRegisters.DX = (ushort)(remain);
                }
            }
            else
            {
                OpCodeNotImplemented("Unknown Reg Mode {addrMode.Reg} for Opcode {opCode:X} (Group3)");
            }
        }

        private void ExecuteGroup4_And_5() // &HFE, &hFF
        {
            SetAddressing();

            if (addrMode.Reg == ((byte)0)) // INC Eb | Ev
            {
                if (addrMode.IsDirect)
                {
                    mRegisters.set_Val(addrMode.Register2, Eval((uint)(mRegisters.get_Val(addrMode.Register2)), (uint)1, Operation.Increment, addrMode.Size));
                    clkCyc += 3;
                }
                else
                {
                    set_RAMn(false, Eval((uint)(addrMode.IndMem), (uint)1, Operation.Increment, addrMode.Size));
                    clkCyc += 15;
                }
            } // DEC Eb | Ev
            else if (addrMode.Reg == ((byte)1))
            {
                if (addrMode.IsDirect)
                {
                    mRegisters.set_Val(addrMode.Register2, Eval((uint)(mRegisters.get_Val(addrMode.Register2)), (uint)1, Operation.Decrement, addrMode.Size));
                    clkCyc += 3;
                }
                else
                {
                    set_RAMn(false, Eval((uint)(addrMode.IndMem), (uint)1, Operation.Decrement, addrMode.Size));
                    clkCyc += 15;
                }
            } // CALL Mp
            else if (addrMode.Reg == ((byte)2))
            {
                PushIntoStack((ushort)(mRegisters.IP + opCodeSize));
                IPAddrOffet = (ushort)(addrMode.IsDirect ? (
                    mRegisters.get_Val(addrMode.Register2)) :
                    addrMode.IndMem);
                clkCyc += 11;
            } // JMP Ev
            else if (addrMode.Reg == ((byte)3))
            {
                PushIntoStack(mRegisters.CS);
                PushIntoStack((ushort)(mRegisters.IP + opCodeSize));
                IPAddrOffet = addrMode.IndMem;
                mRegisters.CS = get_RAM16((ushort)(mRegisters.ActiveSegmentValue), addrMode.IndAdr, (byte)2, false);
                clkCyc += 37;
            } // JMP Ev
            else if (addrMode.Reg == ((byte)4))
            {
                IPAddrOffet = (ushort)(addrMode.IsDirect ? (
                    mRegisters.get_Val(addrMode.Register2)) :
                    addrMode.IndMem);
                clkCyc += 15;
            } // JMP Mp
            else if (addrMode.Reg == ((byte)5))
            {
                IPAddrOffet = addrMode.IndMem;
                mRegisters.CS = get_RAM16((ushort)(mRegisters.ActiveSegmentValue), addrMode.IndAdr, (byte)2, false);
                clkCyc += 24;
            } // PUSH Ev
            else if (addrMode.Reg == ((byte)6))
            {
                if (addrMode.IsDirect)
                {
                    if (addrMode.Register2 == GPRegisters.RegistersTypes.SP)
                    {
                        PushIntoStack((ushort)(mRegisters.SP - 2));
                    }
                    else
                    {
                        PushIntoStack(mRegisters.get_Val(addrMode.Register2));
                    }
                }
                else
                {
                    PushIntoStack(addrMode.IndMem);
                }
                clkCyc += 16;
            }
            else
            {
                OpCodeNotImplemented("Unknown Reg Mode {addrMode.Reg} for Opcode {opCode:X} (Group4&5)");
            }
        }

        private void HandleREPMode()
        {
            tmpUVal = mRegisters.ActiveSegmentValue;
            tmpVal = (int)((((opCode & 1) == 1) ? 2 : 1) * (mFlags.DF == 0 ? 1 : -1));

            if (mRepeLoopMode == REPLoopModes.None)
            {
                ExecStringOpCode();
            }
            else if (mDebugMode && mRegisters.CX > 0)
            {
                mRegisters.CX--;
                if (ExecStringOpCode())
                {
                    if ((mRepeLoopMode == REPLoopModes.REPE && mFlags.ZF == 0) ||
                            (mRepeLoopMode == REPLoopModes.REPENE && mFlags.ZF == 1))
                    {
                        return;
                    }
                }

                mRegisters.IP -= (ushort)(opCodeSize + 1);
            }
            else
            {
                while (mRegisters.CX > 0)
                {
                    mRegisters.CX--;
                    if (ExecStringOpCode())
                    {
                        if ((mRepeLoopMode == REPLoopModes.REPE && mFlags.ZF == 0) ||
                                (mRepeLoopMode == REPLoopModes.REPENE && mFlags.ZF == 1))
                        {
                            break;
                        }
                    }
                }
            }
        }

        private bool ExecStringOpCode()
        {
            instrucionsCounter++;

            if (opCode == ((byte)(0xA4))) // MOVSB
            {
                set_RAM8(mRegisters.ES, mRegisters.DI, (byte)0, true, get_RAM8((ushort)(tmpUVal), mRegisters.SI, (byte)0, true));
                mRegisters.SI += (ushort)tmpVal;
                mRegisters.DI += (ushort)tmpVal;
                clkCyc += 18;
                return false;
            } // MOVSW
            else if (opCode == ((byte)(0xA5)))
            {
                set_RAM16(mRegisters.ES, mRegisters.DI, (byte)0, true, get_RAM16((ushort)(tmpUVal), mRegisters.SI, (byte)0, true));
                mRegisters.SI += (ushort)tmpVal;
                mRegisters.DI += (ushort)tmpVal;
                clkCyc += 18;
                return false;
            } // CMPSB
            else if (opCode == ((byte)(0xA6)))
            {
                Eval((uint)(get_RAM8((ushort)(tmpUVal), mRegisters.SI, (byte)0, true)), (uint)(get_RAM8(mRegisters.ES, mRegisters.DI, (byte)0, true)), Operation.Compare, DataSize.Byte);
                mRegisters.SI += (ushort)tmpVal;
                mRegisters.DI += (ushort)tmpVal;
                clkCyc += 22;
                return true;
            } // CMPSW
            else if (opCode == ((byte)(0xA7)))
            {
                Eval((uint)(get_RAM16((ushort)(tmpUVal), mRegisters.SI, (byte)0, true)), (uint)(get_RAM16(mRegisters.ES, mRegisters.DI, (byte)0, true)), Operation.Compare, DataSize.Word);
                mRegisters.SI += (ushort)tmpVal;
                mRegisters.DI += (ushort)tmpVal;
                clkCyc += 22;
                return true;
            } // STOSB
            else if (opCode == ((byte)(0xAA)))
            {
                set_RAM8(mRegisters.ES, mRegisters.DI, (byte)0, true, mRegisters.AL);
                mRegisters.DI += (ushort)tmpVal;
                clkCyc += 11;
                return false;
            } // STOSW
            else if (opCode == ((byte)(0xAB)))
            {
                set_RAM16(mRegisters.ES, mRegisters.DI, (byte)0, true, mRegisters.AX);
                mRegisters.DI += (ushort)tmpVal;
                clkCyc += 11;
                return false;
            } // LODSB
            else if (opCode == ((byte)(0xAC)))
            {
                mRegisters.AL = get_RAM8((ushort)(tmpUVal), mRegisters.SI, (byte)0, true);
                mRegisters.SI += (ushort)tmpVal;
                clkCyc += 12;
                return false;
            } // LODSW
            else if (opCode == ((byte)(0xAD)))
            {
                mRegisters.AX = get_RAM16((ushort)(tmpUVal), mRegisters.SI, (byte)0, true);
                mRegisters.SI += (ushort)tmpVal;
                clkCyc += 16;
                return false;
            } // SCASB
            else if (opCode == ((byte)(0xAE)))
            {
                Eval((uint)(mRegisters.AL), (uint)(get_RAM8(mRegisters.ES, mRegisters.DI, (byte)0, true)), Operation.Compare, DataSize.Byte);
                mRegisters.DI += (ushort)tmpVal;
                clkCyc += 15;
                return true;
            } // SCASW
            else if (opCode == ((byte)(0xAF)))
            {
                Eval((uint)(mRegisters.AX), (uint)(get_RAM16(mRegisters.ES, mRegisters.DI, (byte)0, true)), Operation.Compare, DataSize.Word);
                mRegisters.DI += (ushort)tmpVal;
                clkCyc += 15;
                return true;
            }

            return false;
        }
    }
}
