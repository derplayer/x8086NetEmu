using System.Collections.Generic;
using System;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Xml.Linq;
using System.Collections;
using System.Windows.Forms;
using System.Threading;
using System.Runtime.InteropServices;

using x8086SharpEmu;

namespace x8086SharpEmu
{

    public class FloppyControllerAdapter : Adapter, IDMADevice
    {

        // Command execution time (50 us, arbitrary value)
        private const int COMMANDDELAY = 50000;

        // Time to transfer one byte of a sector in non-DMA mode (27 us, from 8272 spec)
        private const int BYTEDELAY = 27000;

        // Time to transfer one sector in DMA mode (5 ms, arbitrary value)
        private const int SECTORDELAY = 5000000;

        // Status register 0
        private byte regSt0;

        // Digital output register
        private byte regDOR;

        // Configuration
        private byte ctlStepRateTime;
        private byte ctlHeadUnloadTime;
        private byte ctlHeadLoadTime;
        private bool ctlNonDma;

        // Current cylinder for each drive
        private byte[] curCylinder;

        // Bit mask of drives with a seek operation in progress
        private byte driveSeeking;

        // Bit mask of drives with a pending ready notification
        private byte pendingReadyChange;

        // Input buffer for command bytes
        private byte[] commandbuf;
        private int commandlen;
        private int commandptr;
        private Commands cmdCmd;
        private byte cmdDrive;
        private byte cmdHead;
        private byte cmdCylinder;
        private byte cmdRecord;
        private byte cmdEot;
        private bool cmdMultitrack;

        private enum States
        {
            IDLE = 0,
            COMMAND = 1,
            EXECUTE = 2,
            TRANSFER_IN = 3,
            TRANSFER_OUT = 4,
            TRANSWAIT_IN = 5,
            TRANSWAIT_OUT = 6,
            TRANSFER_EOP = 7,
            RESULT = 8
        }
        private States state;

        // Output buffer for result bytes
        private byte[] resultbuf;
        private int resultptr;

        // Data buffer
        private byte[] databuf;
        private int dataptr;

        // Floppy disk drives
        private DiskImage[] diskimg;

        // Simulation scheduler
        private Scheduler sched;

        // Interrupt request signal
        private InterruptRequest irq;

        // DMA request signal
        private IDMAChannel dma;

        private enum Commands
        {
            READ = 0x6,
            READ_DEL = 0xC,
            WRITE = 0x5,
            WRITE_DEL = 0x9,
            READ_TRACK = 0x2,
            READ_ID = 0xA,
            FORMAT = 0xD,
            SCAN_EQ = 0x11,
            SCAN_LE = 0x19,
            SCAN_GE = 0x1D,
            CALIBRATE = 0x7,
            SENSE_INT = 0x8,
            SPECIFY = 0x3,
            SENSE_DRIVE = 0x4,
            SEEK = 0xF
        }

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

        public FloppyControllerAdapter(X8086 cpu) : base(cpu)
        {
            task = new TaskSC(this);
            this.sched = cpu.Sched;
            if (cpu.PIC != null)
            {
                this.irq = cpu.PIC.GetIrqLine((byte)6);
            }
            if (cpu.DMA != null)
            {
                this.dma = cpu.DMA.GetChannel(2);
                cpu.DMA.BindChannel(2, this);
            }

            curCylinder = new byte[4];
            diskimg = new DiskImage[512];
            regDOR = (byte)(0xC);
            ctlStepRateTime = (byte)0;
            ctlHeadUnloadTime = (byte)0;
            ctlHeadLoadTime = (byte)0;
            ctlNonDma = false;

            for (uint i = 0x3F0; i <= 0x3F7; i++)
            {
                ValidPortAddress.Add(i);
            }
        }

        public override void InitiAdapter()
        {
            Reset();
        }

        public DiskImage get_DiskImage(int driveNumber)
        {
            if (driveNumber >= diskimg.Length)
            {
                return null;
            }
            else
            {
                return diskimg[driveNumber];
            }
        }
        public void set_DiskImage(int driveNumber, DiskImage value)
        {
            if (diskimg[driveNumber] != null)
            {
                diskimg[driveNumber].Close();
            }

            diskimg[driveNumber] = value;
        }

        // Resets the controller
        public void Reset()
        {
            driveSeeking = (byte)0;
            pendingReadyChange = (byte)0;
            regSt0 = (byte)0;
            commandbuf = new byte[9];
            commandptr = 0;
            resultbuf = null;
            databuf = null;
            if (irq != null)
            {
                irq.Raise(false);
            }
            if (dma != null)
            {
                dma.DMARequest(false);
            }
            state = States.IDLE;
            task?.Cancel();
        }

        // Prepare to transfer next byte(s)
        public void KickTransfer()
        {
            if (ctlNonDma)
            {
                // prepare to transfer next byte in non-DMA mode
                sched.RunTaskAfter(task, BYTEDELAY);
                if (irq != null)
                {
                    irq.Raise(true);
                }
            }
            else
            {
                // prepare to transfer multiple bytes in DMA mode
                sched.RunTaskAfter(task, SECTORDELAY);
                if (dma != null)
                {
                    dma.DMARequest(true);
                }
            }
        }

        // Determines the length of a command from the first command byte
        private int CommandLength()
        {
            switch (cmdCmd)
            {
                case Commands.READ:
                case Commands.READ_DEL:
                case Commands.WRITE:
                case Commands.WRITE_DEL:
                    return 9;
                case Commands.READ_TRACK:
                    return 9;
                case Commands.READ_ID:
                    return 2;
                case Commands.FORMAT:
                    return 6;
                case Commands.SCAN_EQ:
                case Commands.SCAN_LE:
                case Commands.SCAN_GE:
                    return 9;
                case Commands.CALIBRATE:
                    return 2;
                case Commands.SENSE_INT:
                    return 1;
                case Commands.SPECIFY:
                    return 3;
                case Commands.SENSE_DRIVE:
                    return 2;
                case Commands.SEEK:
                    return 3;
                default:
                    return 1;
            }
        }

        private void CommandStart()
        {
            // Decode command parameters
            cmdMultitrack = (commandbuf[0] & 0x80) == 0x80;
            cmdDrive = System.Convert.ToByte(commandbuf[1] & 3);
            cmdCylinder = commandbuf[2];
            cmdRecord = commandbuf[4];
            cmdEot = commandbuf[6];
            switch (cmdCmd)
            {
                case Commands.READ:
                case Commands.READ_DEL:
                case Commands.WRITE:
                case Commands.WRITE_DEL:
                case Commands.READ_TRACK:
                case Commands.SCAN_EQ:
                case Commands.SCAN_LE:
                case Commands.SCAN_GE:
                    cmdHead = commandbuf[3];
                    break;
                default:
                    cmdHead = System.Convert.ToByte((commandbuf[1] >> 2) & 1);
                    break;
            }

            // Start execution
            switch (cmdCmd)
            {
                case Commands.READ: //  READ: go to EXECUTE state
                case Commands.READ_DEL:
                    state = States.EXECUTE;
                    sched.RunTaskAfter(task, COMMANDDELAY);
                    break;

                case Commands.WRITE: // WRITE: go to EXECUTE state
                case Commands.WRITE_DEL:
                    state = States.EXECUTE;
                    sched.RunTaskAfter(task, COMMANDDELAY);
                    break;

                case Commands.READ_TRACK: // READ TRACK: go to EXECUTE state
                    state = States.EXECUTE;
                    sched.RunTaskAfter(task, COMMANDDELAY);
                    break;

                case Commands.READ_ID: // READ ID: go to execute state
                    state = States.EXECUTE;
                    sched.RunTaskAfter(task, COMMANDDELAY);
                    break;

                case Commands.FORMAT: // FORMAT: go to EXECUTE state
                    state = States.EXECUTE;
                    sched.RunTaskAfter(task, COMMANDDELAY);
                    break;

                case Commands.SCAN_EQ: // SCAN: go to EXECUTE state
                case Commands.SCAN_LE:
                case Commands.SCAN_GE:
                    state = States.EXECUTE;
                    sched.RunTaskAfter(task, COMMANDDELAY);
                    break;

                case Commands.CALIBRATE: // CALIBRATE: go to EXECUTE state
                    cmdCylinder = (byte)0;
                    driveSeeking = (byte)(driveSeeking | (1 << cmdDrive));
                    state = States.EXECUTE;
                    sched.RunTaskAfter(task, COMMANDDELAY);
                    break;

                case Commands.SENSE_INT: // SENSE INTERRUPT: respond immediately
                    if (irq != null)
                    {
                        irq.Raise(false);
                    }

                    // Respond to a completed seek command.
                    for (int i = 0; i <= 4 - 1; i++)
                    {
                        if ((driveSeeking & (1 << i)) != 0)
                        {
                            driveSeeking = (byte)(driveSeeking & System.Convert.ToByte((~(1 << i)) & 0xFF));
                            pendingReadyChange = (byte)(pendingReadyChange & System.Convert.ToByte((~(1 << i)) & 0xFF));
                            CommandEndSense(System.Convert.ToByte(0x20 | i), curCylinder[i]);
                            return;
                        }
                    }

                    // Respond to a disk-ready change.
                    for (int i = 0; i <= 4 - 1; i++)
                    {
                        if ((pendingReadyChange & (1 << i)) != 0)
                        {
                            pendingReadyChange = (byte)(pendingReadyChange & System.Convert.ToByte((~(1 << i)) & 0xFF));
                            CommandEndSense(System.Convert.ToByte(0xC0 | i), curCylinder[i]);
                            return;
                        }
                    }

                    // No pending interrupt condition respond with invalid command.
                    CommandEndSense((byte)(0x80));
                    break;

                case Commands.SPECIFY: // SPECIFY: no response
                    ctlStepRateTime = System.Convert.ToByte((commandbuf[1] >> 4) & 0xF);
                    ctlHeadUnloadTime = System.Convert.ToByte(commandbuf[1] & 0xF);
                    ctlHeadLoadTime = System.Convert.ToByte((commandbuf[2] >> 1) & 0x7F);
                    ctlNonDma = (commandbuf[2] & 1) == 1;
                    CommandEndVoid();
                    break;

                case Commands.SENSE_DRIVE: // SENSE DRIVE: respond immediately
                    byte st3 = System.Convert.ToByte(commandbuf[1] & 0x7);
                    if (curCylinder[cmdDrive] == 0)
                    {
                        st3 = (byte)(st3 | 0x10); // track 0
                    }
                    st3 = (byte)(st3 | 0x20); // ready line is tied to true
                    if (diskimg[cmdDrive] != null)
                    {
                        if (diskimg[cmdDrive].Heads > 1)
                        {
                            st3 = (byte)(st3 | 0x8); // two side
                        }
                        if (diskimg[cmdDrive].IsReadOnly)
                        {
                            st3 = (byte)(st3 | 0x40); // write protected
                        }
                    }
                    CommandEndSense(st3);
                    break;

                case Commands.SEEK: // SEEK: go to EXECUTE state
                    driveSeeking = (byte)(driveSeeking | (byte)(1 << cmdDrive));
                    state = States.EXECUTE;
                    sched.RunTaskAfter(task, COMMANDDELAY);
                    break;

                default: // INVALID: respond immediately
                    regSt0 = (byte)(0x80);
                    CommandEndSense(regSt0);
                    break;
            }
        }

        // Next step in the execution of a command
        private void CommandExecute()
        {
            long offs = 0;
            int n = 0;
            int k = 0;

            // Handle Seek and Recalibrate commands.
            switch (cmdCmd)
            {
                case Commands.CALIBRATE:
                case Commands.SEEK:
                    curCylinder[cmdDrive] = cmdCylinder;
                    CommandEndSeek();
                    return;
            }

            // Check for NOT READY.
            if (ReferenceEquals(diskimg[cmdDrive], null))
            {
                if (state == States.EXECUTE)
                {
                    // No floppy image attached at start of command respond with NOT READY.
                    CommandEndIO(0x48, 0x0, 0x0); // abnormal, not ready
                }
                else
                {
                    // Drive changed ready state during command respond with NOT READY.
                    CommandEndIO(0xC8, 0x0, 0x0); // abnormal (ready change), not ready
                }
                return;
            }

            // Check for valid cylinder/head/sector numbers.
            switch (cmdCmd)
            {
                case Commands.READ:
                case Commands.READ_DEL:
                case Commands.WRITE:
                case Commands.WRITE_DEL:
                case Commands.READ_TRACK:
                case Commands.SCAN_EQ:
                case Commands.SCAN_LE:
                case Commands.SCAN_GE:

                    // Check cylinder number.
                    if ((cmdCylinder & 0xFF) != curCylinder[cmdDrive])
                    {
                        // Requested cylinder does not match current head position
                        // respond with NO DATA and WRONG CYLINDER.
                        CommandEndIO(0x40, 0x4, 0x10); // abnormal, no data, wrong cylinder
                        return;
                    }

                    // Check head number.
                    if ((cmdHead & 0xFF) >= diskimg[cmdDrive].Heads)
                    {
                        // Head out-of-range respond with NOT READY
                        CommandEndIO(0x48, 0x0, 0x0); // abnormal, not ready
                        return;
                    }

                    if (cmdCmd == Commands.READ_TRACK)
                    {
                        break;
                    }

                    // Check sector number.
                    if (cmdRecord == 0 || (cmdRecord & 0xFF) > diskimg[cmdDrive].Sectors)
                    {
                        // Sector out-of-range respond with NO DATA.
                        CommandEndIO(0x40, 0x4, 0x0); // abnormal, no data
                        return;
                    }
                    break;
            }

            switch (cmdCmd)
            {
                case Commands.READ_DEL:
                case Commands.WRITE_DEL:
                case Commands.FORMAT:
                    // Not implemented respond with NO DATA and MISSING ADDRESS.
                    CommandEndIO(0x40, 0x5, 0x1); // abnormal, no data, missing address
                    break;

                case Commands.READ:
                    // Read sector.
                    databuf = new byte[512];
                    offs = System.Convert.ToInt64(diskimg[cmdDrive].LBA(System.Convert.ToUInt32(cmdCylinder), System.Convert.ToUInt32(cmdHead), System.Convert.ToUInt32(cmdRecord)));
                    k = System.Convert.ToInt32(diskimg[cmdDrive].Read((ulong)offs, databuf));
                    if (k < 0)
                    {
                        // Read error respond with DATA ERROR.
                        CommandEndIO(0x40, 0x20, 0x0); // abnormal, data error
                        return;
                    }

                    // Go to TRANSFER state.
                    dataptr = 0;
                    state = States.TRANSFER_OUT;
                    KickTransfer();
                    break;

                case Commands.WRITE:
                    // Check for WRITE PROTECTED.
                    if (diskimg[cmdDrive].IsReadOnly)
                    {
                        CommandEndIO(0x40, 0x2, 0x0); // abnormal, write protected
                        return;
                    }

                    // Go to TRANSFER state.
                    databuf = new byte[512];
                    dataptr = 0;
                    state = States.TRANSFER_IN;
                    KickTransfer();
                    break;

                case Commands.READ_TRACK:
                    // Read track.
                    n = System.Convert.ToInt32(diskimg[cmdDrive].Sectors);
                    if ((cmdEot & 0xFF) < n)
                    {
                        n = cmdEot & 0xFF;
                    }
                    databuf = new byte[n * 512];
                    offs = System.Convert.ToInt64(diskimg[cmdDrive].LBA(System.Convert.ToUInt32(cmdCylinder), System.Convert.ToUInt32(cmdHead), (uint)1));
                    k = System.Convert.ToInt32(diskimg[cmdDrive].Read((ulong)offs, databuf));
                    if (k < 0)
                    {
                        // Read error respond with DATA ERROR.
                        CommandEndIO(0x40, 0x20, 0x0); // abnormal, data error
                        return;
                    }

                    // Go to TRANSFER state.
                    dataptr = 0;
                    state = States.TRANSFER_OUT;
                    KickTransfer();
                    break;

                case Commands.READ_ID:
                    // Exit Sub current cylinder, sector 1.
                    cmdCylinder = curCylinder[cmdDrive];
                    cmdRecord = (byte)1;
                    CommandEndIO(0x0, 0x0, 0x0); // normal termination
                    break;

                case Commands.SCAN_EQ:
                case Commands.SCAN_LE:
                case Commands.SCAN_GE:
                    // Go to TRANSFER state.
                    databuf = new byte[512];
                    dataptr = 0;
                    state = States.TRANSFER_IN;
                    KickTransfer();
                    break;
            }
        }

        // Called when a buffer has been transferred (or EOP-ed)
        private void CommandTransferDone()
        {
            long offs = 0;
            int n = 0;
            int k = 0;
            byte[] tmpbuf;
            bool scanEq = false;
            bool scanLe = false;
            bool scanGe = false;
            byte st2 = (byte)(0x0);
            byte sectorStep = (byte)1;

            switch (cmdCmd)
            {
                case Commands.READ:
                    break;

                case Commands.WRITE:
                    // Write sector.
                    if (ReferenceEquals(diskimg[cmdDrive], null))
                    {
                        // No floppy image attached respond with NOT READY.
                        CommandEndIO(0xC8, 0x0, 0x0); // abnormal (ready change), not ready
                        return;
                    }
                    offs = System.Convert.ToInt64(diskimg[cmdDrive].LBA(System.Convert.ToUInt32(cmdCylinder), System.Convert.ToUInt32(cmdHead), System.Convert.ToUInt32(cmdRecord)));
                    k = System.Convert.ToInt32(diskimg[cmdDrive].Write((ulong)offs, databuf));
                    if (k < 0)
                    {
                        // Write error respond with DATA ERROR.
                        CommandEndIO(0x40, 0x20, 0x0); // abnormal, data error
                        return;
                    }
                    break;

                case Commands.READ_TRACK:
                    // Track done.
                    // Did we encounter a sector matching cmdRecord
                    n = System.Convert.ToInt32((double)(dataptr + 511) / 512);
                    if (cmdRecord != 0 && (cmdRecord & 0xFF) <= n)
                    {
                        CommandEndIO(0x0, 0x0, 0x0); // normal termination
                    }
                    else
                    {
                        CommandEndIO(0x0, 0x4, 0x0); // normal termination, no data
                        return;
                    }
                    break;

                case Commands.SCAN_EQ:
                case Commands.SCAN_LE:
                case Commands.SCAN_GE:
                    // Read sector from disk.
                    if (ReferenceEquals(diskimg[cmdDrive], null))
                    {
                        // No floppy image attached respond with NOT READY.
                        CommandEndIO(0xC8, 0x0, 0x0); // abnormal (ready change), not ready
                        return;
                    }
                    offs = System.Convert.ToInt64(diskimg[cmdDrive].LBA(System.Convert.ToUInt32(cmdCylinder), System.Convert.ToUInt32(cmdHead), System.Convert.ToUInt32(cmdRecord)));
                    tmpbuf = new byte[512];
                    k = System.Convert.ToInt32(diskimg[cmdDrive].Read((ulong)offs, tmpbuf));
                    if (k < 0)
                    {
                        // Read error respond with DATA ERROR.
                        CommandEndIO(0x40, 0x20, 0x0); // abnormal, data error
                        return;
                    }
                    // Compare supplied data to on-disk data.
                    scanEq = scanLe == scanGe == true;
                    for (int i = 0; i <= 512 - 1; i++)
                    {
                        if ((databuf[i] & 0xFF) < (tmpbuf[i] & 0xFF))
                        {
                            scanEq = false;
                            scanGe = false;
                        }
                        else if ((databuf[i] & 0xFF) > (tmpbuf[i] & 0xFF))
                        {
                            scanEq = false;
                            scanLe = false;
                        }
                    }
                    if ((cmdCmd == Commands.SCAN_EQ && scanEq) ||
                            (cmdCmd == Commands.SCAN_LE && scanLe) ||
                            (cmdCmd == Commands.SCAN_GE && scanGe))
                    {
                        // Scan condition met.
                        st2 = System.Convert.ToByte(scanEq ? 0x8 : 0x0); // if equal, set scan hit flag
                        CommandEndIO(0x0, 0x0, st2); // normal termination
                        return;
                    }
                    st2 = (byte)(0x4); // set scan not satisfied flag
                    sectorStep = commandbuf[8]; // sector increment supplied by command word
                    break;
            }

            if (dataptr == 512)
            {
                // Complete sector transferred increment sector number.
                if (cmdRecord == cmdEot)
                {
                    cmdRecord = sectorStep;
                    if (cmdMultitrack)
                    {
                        cmdHead = (byte)(cmdHead ^ 1);
                    }
                    if (!cmdMultitrack || (cmdHead & 1) == 0)
                    {
                        cmdCylinder++;
                    }
                }
                else
                {
                    cmdRecord += sectorStep;
                }
            }

            if (state == States.TRANSFER_EOP || (cmdRecord == 1 && (!cmdMultitrack || (cmdHead & 1) == 0)))
            {
                // Transferred last sector or got EOP.
                CommandEndIO(0x0, 0x0, st2); // normal termination
            }
            else
            {
                // Start transfer of next sector.
                CommandExecute();
            }
        }

        // Ends a command which does not return response data
        private void CommandEndVoid()
        {
            commandptr = 0;
            state = States.IDLE;
        }

        // Ends a command which returns data without an IRQ signal
        private void CommandEndSense(byte st)
        {
            resultbuf = new byte[1];
            resultbuf[0] = st;
            resultptr = 0;
            state = States.RESULT;
        }

        // Ends a command which returns data without an IRQ signal
        private void CommandEndSense(byte st, byte pcn)
        {
            resultbuf = new byte[2];
            resultbuf[0] = st;
            resultbuf[1] = pcn;
            resultptr = 0;
            state = States.RESULT;
        }

        // Ends a command which returns no data but raises an IRQ signal
        private void CommandEndSeek()
        {
            commandptr = 0;
            state = States.IDLE;
            if (irq != null)
            {
                irq.Raise(true);
            }
        }

        // Ends a command which reports I/O status and raises an IRQ signal
        private void CommandEndIO(int st0, int st1, int st2)
        {
            resultbuf = new byte[7];
            regSt0 = (byte)(st0 | (commandbuf[1] & 7));
            resultbuf[0] = regSt0;
            resultbuf[1] = (byte)st1;
            resultbuf[2] = (byte)st2;
            resultbuf[3] = cmdCylinder;
            resultbuf[4] = cmdHead;
            resultbuf[5] = cmdRecord;
            resultbuf[6] = (byte)2; // always assume 512-byte sectors
            resultptr = 0;
            state = States.RESULT;

            //For i As Integer = 0 To 7 - 1
            //    mCPU.Memory(X8086.SegmentOffetToAbsolute(&H40, &H42 + i)) = resultbuf(i)
            //Next

            if (irq != null)
            {
                irq.Raise(true);
            }
        }

        // Called from the scheduled task to handle the next step of a command
        private void Update()
        {
            switch (state)
            {
                case States.EXECUTE:
                    // Start/continue command execution.
                    CommandExecute();
                    break;

                case States.TRANSFER_IN:
                case States.TRANSFER_OUT:
                    // Timeout during I/O transfer terminate command.
                    if (dma != null)
                    {
                        dma.DMARequest(false);
                    }
                    // A real floppy controller would probably complete the current sector here.
                    // But a timeout is in itself a pretty serious error, so we don't care so much
                    // about the exact behavior. (TODO)
                    CommandEndIO(0x48, 0x10, 0x0); // abnormal, overrun
                    break;

                case States.TRANSWAIT_IN:
                case States.TRANSWAIT_OUT:
                    if (dataptr < databuf.Length)
                    {
                        // Continue the current transfer.
                        state = state == States.TRANSWAIT_IN ? States.TRANSFER_IN : States.TRANSFER_OUT;
                        KickTransfer();
                    }
                    else
                    {
                        // Transfer completed.
                        CommandTransferDone();
                    }
                    break;

                case States.TRANSFER_EOP:
                    // Transfer EOP-ed.
                    CommandTransferDone();
                    break;
                case States.RESULT:
                    Debugger.Break();
                    break;
            }
        }

        // Returns current value of main status register
        private byte GetMainStatus()
        {
            byte stmain = 0;
            switch (state)
            {
                case States.IDLE:
                    stmain = (byte)(0x80); // RQM, WR
                    break;

                case States.COMMAND:
                    stmain = (byte)(0x90); // RQM, WR, CMDBSY
                    break;

                case States.EXECUTE:
                    stmain = (byte)(0x10); // CMDBSY
                    break;

                case States.TRANSFER_IN:
                    stmain = (byte)(0x10); // CMDBSY
                    if (ctlNonDma)
                    {
                        stmain = (byte)(stmain | 0xC0); // RQM, WR, NONDMA
                    }
                    break;

                case States.TRANSFER_OUT:
                    stmain = (byte)(0x10);
                    if (ctlNonDma)
                    {
                        stmain = (byte)(stmain | 0xE0); // RQM, RD, NONDMA
                    }
                    break;

                case States.RESULT:
                    stmain = (byte)(0xD0); // RQM, RD, CMDBSY
                    break;

                default:
                    stmain = (byte)(0x10); // CMDBSY
                    if (ctlNonDma)
                    {
                        stmain = (byte)(stmain | 0x20); // NONDMA
                    }
                    break;
            }

            stmain = (byte)(stmain | driveSeeking); // bit mask of seeking drives
                                                    //mCPU.Memory(X8086.SegmentOffetToAbsolute(&H40, &H3E)) = stmain

            return stmain;
        }

        public override void CloseAdapter()
        {

        }

        public override string Description
        {
            get
            {
                return "I8272 Floppy Disk Controller";
            }
        }

        public override ushort In(uint port)
        {
            if ((port & 3) == 0)
            {
                // main status register
                return System.Convert.ToUInt16(GetMainStatus());
            }
            else if ((port & 3) == 1)
            {
                // read from data register
                if (irq != null)
                {
                    irq.Raise(false);
                }
                if (state == States.RESULT)
                {
                    // read next byte of result
                    int v = System.Convert.ToInt32(resultbuf[resultptr] & 0xFF);
                    resultptr++;
                    if (resultptr == resultbuf.Length)
                    {
                        // end of result phase
                        commandptr = 0;
                        databuf = null;
                        resultbuf = null;
                        dataptr = 0;
                        resultptr = 0;
                        state = States.IDLE;
                    }
                    return (ushort)v;
                }
                else if (state == States.TRANSFER_OUT && ctlNonDma)
                {
                    // read next I/O byte in non-DMA mode
                    int v = System.Convert.ToInt32(databuf[dataptr] & 0xFF);
                    dataptr++;
                    state = States.TRANSWAIT_OUT;
                    return (ushort)v;
                }
            }

            // unexpected read
            return (ushort)(0xFF);
        }

        public override void Out(uint port, ushort value)
        {
            if ((port & 3) == 2)
            {
                // write to digital output register
                if ((value & 0x4) == 0)
                {
                    // reset controller
                    Reset();
                }
                else if ((regDOR & 0x4) == 0)
                {
                    // awake from reset condition send disk-ready notification
                    if (irq != null)
                    {
                        irq.Raise(true);
                    }
                    pendingReadyChange = (byte)(0xF);
                }
                regDOR = (byte)(value & 0xFF);

            }
            else if ((port & 3) == 1)
            {
                // write to data register
                if (state == States.IDLE)
                {
                    // CPU writes first command byte
                    state = States.COMMAND;
                    cmdCmd = (Commands)(value & 0x1F);
                    commandlen = CommandLength();
                }

                if (state == States.COMMAND)
                {
                    // CPU writes a command byte
                    commandbuf[commandptr] = (byte)value;
                    commandptr++;
                    if (commandptr == commandlen)
                    {
                        CommandStart();
                    }
                }
                else if (state == States.TRANSFER_IN && ctlNonDma)
                {
                    // CPU writes data byte
                    databuf[dataptr] = (byte)value;
                    dataptr++;
                    state = States.TRANSWAIT_IN;
                    if (irq != null)
                    {
                        irq.Raise(false);
                    }
                }
                else
                {
                    // unexpected write
                }
            }
            else
            {
                // write to unknown port
            }
        }

        public override void Run()
        {
            Update();
        }

        public override string Name
        {
            get
            {
                return "Floppy";
            }
        }

        public override Adapter.AdapterType Type
        {
            get
            {
                return AdapterType.Floppy;
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
                return 7;
            }
        }

        public void DMARead(byte v)
        {
            if (state == States.TRANSFER_IN)
            {
                databuf[dataptr] = v;
                dataptr++;
                if (dataptr == databuf.Length)
                {
                    state = States.TRANSWAIT_IN;
                    if (dma != null)
                    {
                        dma.DMARequest(false);
                    }
                }
            }
            else
            {
                // unexpected dmaRead
            }
        }

        public byte DMAWrite()
        {
            if (state == States.TRANSFER_OUT)
            {
                byte v = databuf[dataptr];
                dataptr++;
                if (dataptr == databuf.Length)
                {
                    state = States.TRANSWAIT_OUT;
                    if (dma != null)
                    {
                        dma.DMARequest(false);
                    }
                }
                return v;
            }
            else
            {
                // unexpected dmaWrite
                return (byte)(0xFF);
            }
        }

        // Handles EOP signal from the DMA controller
        public void DMAEOP()
        {
            switch (state)
            {
                case States.TRANSFER_IN:
                case States.TRANSFER_OUT:
                case States.TRANSWAIT_IN:
                case States.TRANSWAIT_OUT:
                    // Terminate command
                    if (dma != null)
                    {
                        dma.DMARequest(false);
                    }
                    state = States.TRANSFER_EOP;
                    break;
                default:
                    break;
                    // unexpected dmaEop
            }
        }
    }

}
