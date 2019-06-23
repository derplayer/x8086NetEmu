using System.Collections.Generic;
using System;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Xml.Linq;
using System.Collections;

using System.Runtime.CompilerServices;

using x8086SharpEmu;

namespace x8086SharpEmu
{
    // http://www.delorie.com/djgpp/doc/rbinter/ix/13/

    public partial class X8086
    {
        private bool HandleINT13()
        {
            if (mFloppyController == null)
            {
                ThrowException("Disk Adapter Not Found");
                return true;
            }

            int ret = 0;
            int AL = 0;
            long offset = 0;

            DiskImage dskImg = mFloppyController.get_DiskImage((int)mRegisters.DL);
            int bufSize = 0;

            if (mRegisters.AH == ((byte)(0x0))) // Reset drive
            {
                X8086.Notify("Drive {0:000} Reset", NotificationReasons.Info, mRegisters.DL);
                ret = (int)(ReferenceEquals(dskImg, null) ? 0xAA : 0);
            } // Get last operation status
            else if (mRegisters.AH == ((byte)(0x1)))
            {
                X8086.Notify("Drive {0:000} Get Last Operation Status", NotificationReasons.Info, mRegisters.DL);
                mRegisters.AH = (byte)lastAH[mRegisters.DL];
                mFlags.CF = lastCF[mRegisters.DL];
                ret = 0;
            } // Read sectors
            else if (mRegisters.AH == ((byte)(0x2)))
            {
                if (dskImg == null)
                {
                    X8086.Notify("Invalid Drive Number: Drive {0:000} Not Ready", NotificationReasons.Info, mRegisters.DL);
                    ret = 0xAA; // fixed disk drive not ready
                }

                offset = dskImg.LBA((uint)(mRegisters.CH), (uint)(mRegisters.DH), (uint)(mRegisters.CL));
                bufSize = mRegisters.AL * dskImg.SectorSize;

                if (offset < 0 || (int)(offset + bufSize) > (int)dskImg.FileLength)
                {
                    X8086.Notify("Read Sectors: Drive {0:000} Seek Fail", NotificationReasons.Warn, mRegisters.DL);
                    ret = 0x40; // seek failed
                }

                X8086.Notify("Drive {0:000} Read  H{1:00} T{2:000} S{3:000} x {4:000} {5:X6} -> {6:X4}:{7:X4}", NotificationReasons.Info,
                    mRegisters.DL,
                    mRegisters.DH,
                    mRegisters.CH,
                    mRegisters.CL,
                    mRegisters.AL,
                    offset,
                    mRegisters.ES,
                    mRegisters.BX);

                byte[] buf = new byte[bufSize];
                ret = dskImg.Read((ulong)offset, buf);
                if (ret == DiskImage.EIO)
                {
                    X8086.Notify("Read Sectors: Drive {0:000} CRC Error", NotificationReasons.Warn, mRegisters.DL);
                    ret = 0x10; // CRC error
                }
                else if (ret == DiskImage.EOF)
                {
                    X8086.Notify("Read Sectors: Drive {0:000} Sector Not Found", NotificationReasons.Warn, mRegisters.DL);
                    ret = 0x4; // sector not found
                }
                CopyToMemory(buf, X8086.SegmentOffetToAbsolute(mRegisters.ES, mRegisters.BX));
                AL = bufSize / dskImg.SectorSize;
            } // Write sectors
            else if (mRegisters.AH == ((byte)(0x3)))
            {
                if (ReferenceEquals(dskImg, null))
                {
                    X8086.Notify("Invalid Drive Number: Drive {0:000} Not Ready", NotificationReasons.Info, mRegisters.DL);
                    ret = 0xAA; // fixed disk drive not ready
                }

                if (dskImg.IsReadOnly)
                {
                    X8086.Notify("Write Sectors: Drive {0:000} Failed / Read Only", NotificationReasons.Warn, mRegisters.DL);
                    ret = 0x3; // write protected
                }

                offset = dskImg.LBA((uint)(mRegisters.CH), (uint)(mRegisters.DH), (uint)(mRegisters.CL));
                bufSize = mRegisters.AL * dskImg.SectorSize;

                if (offset < 0 || (int)(offset + bufSize) > (int)dskImg.FileLength)
                {
                    X8086.Notify("Write Sectors: Drive {0:000} Seek Failed", NotificationReasons.Warn, mRegisters.DL);
                    ret = 0x40; // seek failed
                }

                X8086.Notify("Drive {0:000} Write H{1:00} T{2:000} S{3:000} x {4:000} {5:X6} <- {6:X4}:{7:X4}", NotificationReasons.Info,
                    mRegisters.DL,
                    mRegisters.DH,
                    mRegisters.CH,
                    mRegisters.CL,
                    mRegisters.AL,
                    offset,
                    mRegisters.ES,
                    mRegisters.BX);

                byte[] buf = new byte[bufSize];
                CopyFromMemory(buf, X8086.SegmentOffetToAbsolute(mRegisters.ES, mRegisters.BX));
                ret = dskImg.Write((ulong)offset, buf);
                if (ret == DiskImage.EIO)
                {
                    X8086.Notify("Write Sectors: Drive {0:000} CRC Error", NotificationReasons.Warn, mRegisters.DL);
                    ret = 0x10; // CRC error
                }
                else if (ret == DiskImage.EOF)
                {
                    X8086.Notify("Write Sectors: Drive {0:000} Sector Not Found", NotificationReasons.Warn, mRegisters.DL);
                    ret = 0x4; // sector not found
                }
                AL = bufSize / dskImg.SectorSize;
            } // Verify Sectors
            else if (mRegisters.AH == ((byte)(0x4)))
            {
                if (ReferenceEquals(dskImg, null))
                {
                    X8086.Notify("Invalid Drive Number: Drive {0:000} Not Ready", NotificationReasons.Info, mRegisters.DL);
                    ret = 0xAA; // fixed disk drive not ready
                }

                offset = dskImg.LBA((uint)(mRegisters.CH), (uint)(mRegisters.DH), (uint)(mRegisters.CL));
                bufSize = mRegisters.AL * dskImg.SectorSize;

                if (offset < 0 || (int)(offset + bufSize) > (int)dskImg.FileLength)
                {
                    X8086.Notify("Verify Sector: Drive {0} Seek Failed", NotificationReasons.Warn, mRegisters.DL);
                    ret = 0x40; // seek failed
                }

                X8086.Notify("Drive {0:000} Verify Sectors H{1:00} T{2:000} S{3:000} ? {4:000} {5:X6} ? {6:X4}:{7:X4}", NotificationReasons.Info,
                    mRegisters.DL,
                    mRegisters.DH,
                    mRegisters.CH,
                    mRegisters.CL,
                    mRegisters.AL,
                    offset,
                    mRegisters.ES,
                    mRegisters.BX);

                AL = bufSize / dskImg.SectorSize;
                ret = 0;
            } // Format Track
            else if (mRegisters.AH == ((byte)(0x5)))
            {
                if (ReferenceEquals(dskImg, null))
                {
                    X8086.Notify("Invalid Drive Number: Drive {0:000} Not Ready", NotificationReasons.Info, mRegisters.DL);
                    ret = 0xAA; // fixed disk drive not ready
                }

                offset = dskImg.LBA((uint)(mRegisters.CH), (uint)(mRegisters.DH), (uint)(mRegisters.CL));
                bufSize = mRegisters.AL * dskImg.SectorSize;

                if (offset < 0 || (int)(offset + bufSize) > (int)dskImg.FileLength)
                {
                    X8086.Notify("Format Track: Drive {0:000} Seek Failed", NotificationReasons.Warn, mRegisters.DL);
                    ret = 0x40; // seek failed
                }

                X8086.Notify("Drive {0:000} Format Track H{1:00} T{2:000} S{3:000} ? {4:000} {5:X6} = {6:X4}:{7:X4}", NotificationReasons.Info,
                    mRegisters.DL,
                    mRegisters.DH,
                    mRegisters.CH,
                    mRegisters.CL,
                    mRegisters.AL,
                    offset,
                    mRegisters.ES,
                    mRegisters.BX);
                ret = 0;
            } // Format Track - Set Bad Sector Flag
            else if (mRegisters.AH == ((byte)(0x6)))
            {
                if (ReferenceEquals(dskImg, null))
                {
                    X8086.Notify("Invalid Drive Number: Drive {0} Not Ready", NotificationReasons.Info, mRegisters.DL);
                    ret = 0xAA; // fixed disk drive not ready
                }

                X8086.Notify("Drive {0:000} Format Track (SBSF) H{1:00} T{2:000} S{3:000} ? {4:000}", NotificationReasons.Info,
                    mRegisters.DL,
                    mRegisters.DH,
                    mRegisters.CH,
                    mRegisters.CL,
                    mRegisters.AL);
                ret = 0;
            } // Format Drive Starting at Track
            else if (mRegisters.AH == ((byte)(0x7)))
            {
                if (ReferenceEquals(dskImg, null))
                {
                    X8086.Notify("Invalid Drive Number: Drive {0:000} Not Ready", NotificationReasons.Info, mRegisters.DL);
                    ret = 0xAA; // fixed disk drive not ready
                }

                X8086.Notify("Drive {0:000} Format Drive H{1:00} T{2:000} S{3:000}", NotificationReasons.Info,
                    mRegisters.DL,
                    mRegisters.DH,
                    mRegisters.CH,
                    mRegisters.CL,
                    mRegisters.AL);
                ret = 0;
            } // Get Drive Parameters
            else if (mRegisters.AH == ((byte)(0x8)))
            {
                if (ReferenceEquals(dskImg, null))
                {
                    X8086.Notify("Invalid Drive Number: Drive {0:000} Not Ready", NotificationReasons.Info, mRegisters.DL);
                    ret = 0xAA; // fixed disk drive not ready
                }
                else
                {
                    if (dskImg.Tracks <= 0)
                    {
                        X8086.Notify("Get Drive Parameters: Drive {0:000} Unknown Geometry", NotificationReasons.Warn, mRegisters.DL);
                        ret = 0xAA;
                    }
                    else
                    {
                        mRegisters.CH = (byte)((dskImg.Cylinders - 1) & 0xFF);
                        mRegisters.CL = (byte)(dskImg.Sectors & 63);
                        mRegisters.CL += (byte)(((dskImg.Cylinders - 1) / 256) * 64);
                        mRegisters.DH = (byte)(dskImg.Heads - 1);

                        if (mRegisters.DL < 0x80)
                        {
                            mRegisters.BL = (byte)4;
                            mRegisters.DL = (byte)2;
                        }
                        else
                        {
                            mRegisters.DL = (byte)DiskImage.HardDiskCount;
                        }

                        X8086.Notify("Drive {0:000} Get Parameters", NotificationReasons.Info, mRegisters.DL);
                        ret = 0;
                    }
                }
            } // Initialize Drive Pair Characteristic
            else if (mRegisters.AH == ((byte)(0x9)))
            {
                if (ReferenceEquals(dskImg, null))
                {
                    X8086.Notify("Invalid Drive Number: Drive {0:000} Not Ready", NotificationReasons.Info, mRegisters.DL);
                    ret = 0xAA; // fixed disk drive not ready
                }
                X8086.Notify("Drive {0:000} Init Drive Pair Characteristic", NotificationReasons.Info, mRegisters.DL);
                ret = 0;

                // The following are meant to keep diagnostic tools happy ;)
            } // Read Long Sectors
            else if (mRegisters.AH == ((byte)(0xA)))
            {
                if (ReferenceEquals(dskImg, null))
                {
                    X8086.Notify("Invalid Drive Number: Drive {0:000} Not Ready", NotificationReasons.Info, mRegisters.DL);
                    ret = 0xAA; // fixed disk drive not ready
                }

                offset = dskImg.LBA((uint)(mRegisters.CH), (uint)(mRegisters.DH), (uint)(mRegisters.CL));
                bufSize = mRegisters.AL * dskImg.SectorSize;

                if (offset < 0 || (int)(offset + bufSize) > (int)dskImg.FileLength)
                {
                    X8086.Notify("Read Sectors Long: Drive {0:000} Seek Fail", NotificationReasons.Warn, mRegisters.DL);
                    ret = 0x40; // seek failed
                }

                X8086.Notify("Drive {0:000} Read Long H{1:00} T{2:000} S{3:000} x {4:000} {5:X6} -> {6:X4}:{7:X4}", NotificationReasons.Info,
                    mRegisters.DL,
                    mRegisters.DH,
                    mRegisters.CH,
                    mRegisters.CL,
                    mRegisters.AL,
                    offset,
                    mRegisters.ES,
                    mRegisters.BX);

                byte[] buf = new byte[bufSize];
                ret = dskImg.Read((ulong)offset, buf);
                if (ret == DiskImage.EIO)
                {
                    X8086.Notify("Read Sectors Long: Drive {0:000} CRC Error", NotificationReasons.Warn, mRegisters.DL);
                    ret = 0x10; // CRC error
                }
                else if (ret == DiskImage.EOF)
                {
                    X8086.Notify("Read Sectors Long: Drive {0:000} Sector Not Found", NotificationReasons.Warn, mRegisters.DL);
                    ret = 0x4; // sector not found
                }
                byte[] ecc = BitConverter.GetBytes(buf.Sum(b => b));
                Array.Resize(ref buf, buf.Length + 4 + 1);
                buf[buf.Length - 4] = ecc[1];
                buf[buf.Length - 3] = ecc[0];
                buf[buf.Length - 2] = ecc[3];
                buf[buf.Length - 1] = ecc[2];
                CopyToMemory(buf, X8086.SegmentOffetToAbsolute(mRegisters.ES, mRegisters.BX));
                AL = bufSize / dskImg.SectorSize;
            } // Seek to Cylinder
            else if (mRegisters.AH == ((byte)(0xC)))
            {
                X8086.Notify("Drive {0:000} Seek to Cylinder ", NotificationReasons.Info, mRegisters.DL);
                ret = 0;
            } // Alternate Disk Reset
            else if (mRegisters.AH == ((byte)(0xD)))
            {
                X8086.Notify("Drive {0:000} Alternate Disk Reset", NotificationReasons.Info, mRegisters.DL);
                ret = 0;
            } // Controller Internal Diagnostic
            else if (mRegisters.AH == ((byte)(0x14)))
            {
                X8086.Notify("Drive {0:000} Controller Internal Diagnostic", NotificationReasons.Info, mRegisters.DL);
                ret = 0;
            } // Recalibrate
            else if (mRegisters.AH == ((byte)(0x11)))
            {
                X8086.Notify("Drive {0:000} Recalibrate", NotificationReasons.Info, mRegisters.DL);
                ret = 0;
            } // Read DASD Type
            else if (mRegisters.AH == ((byte)(0x15)))
            {
                if (ReferenceEquals(dskImg, null))
                {
                    X8086.Notify("Invalid Drive Number: Drive {0:000} Not Ready", NotificationReasons.Info, mRegisters.DL);
                    ret = 0xAA; // fixed disk drive not ready
                }

                if (mRegisters.DL < 0x80)
                {
                    ret = 0x64;
                }
                else
                {
                    mRegisters.CX = (ushort)(dskImg.Sectors / 256);
                    mRegisters.DX = (ushort)(dskImg.Sectors & 0xFF);
                    ret = 0x12C;
                }
                X8086.Notify("Drive {0:000} Read DASD Type", NotificationReasons.Info, mRegisters.DL);
            } // Controller RAM Diagnostic
            else if (mRegisters.AH == ((byte)(0x12)))
            {
                X8086.Notify("Drive {0:000} Controller RAM Diagnostic", NotificationReasons.Info, mRegisters.DL);
                ret = 0;
            } // Drive Diagnostic
            else if (mRegisters.AH == ((byte)(0x13)))
            {
                X8086.Notify("Drive {0:000} Drive Diagnostic", NotificationReasons.Info, mRegisters.DL);
                ret = 0;
            } // Check Extensions Support
            else if (mRegisters.AH == ((byte)(0x41)))
            {
                X8086.Notify("Drive {0:000} Extensions Check", NotificationReasons.Info, mRegisters.DL);
                if (mRegisters.BX == 0x55AA)
                {
                    mFlags.CF = (byte)0;
                    mRegisters.AH = (byte)(0x1);
                    mRegisters.CX = (ushort)(0x4);
                }
                else
                {
                    mFlags.CF = (byte)1;
                    mRegisters.AH = (byte)(0xFF);
                }
                return true;
            } // Extended Sectors Read
            else if (mRegisters.AH == ((byte)(0x42)))
            {
                if (ReferenceEquals(dskImg, null))
                {
                    X8086.Notify("Invalid Drive Number: Drive {0:000} Not Ready", NotificationReasons.Info, mRegisters.DL);
                    ret = 0xAA; // fixed disk drive not ready
                }

                uint dap = X8086.SegmentOffetToAbsolute(mRegisters.DS, mRegisters.SI);
                bufSize = (int)(get_RAM(dap + 3) << 8 | get_RAM(dap + 2));
                int seg = (int)(get_RAM(dap + 7) << 8 | get_RAM(dap + 6));
                int Off = (int)(get_RAM(dap + 5) << 8 | get_RAM(dap + 4));
                offset = (long)(get_RAM(dap + 0xF) << 56 | get_RAM(dap + 0xE) << 48 |
                    get_RAM(dap + 0xD) << 40 | get_RAM(dap + 0xC) << 32 |
                    get_RAM(dap + 0xB) << 24 | get_RAM(dap + 0xA) << 16 |
                    get_RAM(dap + 0x9) << 8 | get_RAM(dap + 0x8));

                if (offset < 0 || (int)(offset + bufSize) > (int)dskImg.FileLength)
                {
                    X8086.Notify("Read Sectors: Drive {0:000} Seek Fail", NotificationReasons.Warn, mRegisters.DL);
                    ret = 0x40; // seek failed
                }

                X8086.Notify("Drive {0:000} Read {4:000} {5:X6} -> {6:X4}:{7:X4}", NotificationReasons.Info,
                    mRegisters.DL,
                    bufSize,
                    offset,
                    seg,
                    Off);

                byte[] buf = new byte[bufSize];
                ret = dskImg.Read((ulong)offset, buf);
                if (ret == DiskImage.EIO)
                {
                    X8086.Notify("Read Sectors: Drive {0:000} CRC Error", NotificationReasons.Warn, mRegisters.DL);
                    ret = 0x10; // CRC error
                }
                else if (ret == DiskImage.EOF)
                {
                    X8086.Notify("Read Sectors: Drive {0:000} Sector Not Found", NotificationReasons.Warn, mRegisters.DL);
                    ret = 0x4; // sector not found
                }
                CopyToMemory(buf, X8086.SegmentOffetToAbsolute((ushort)seg, (ushort)(Off)));
                AL = bufSize / dskImg.SectorSize;
            } // Extended Sectors Write
            else if (mRegisters.AH == ((byte)(0x43)))
            {
                if (ReferenceEquals(dskImg, null))
                {
                    X8086.Notify("Invalid Drive Number: Drive {0:000} Not Ready", NotificationReasons.Info, mRegisters.DL);
                    ret = 0xAA; // fixed disk drive not ready
                }

                uint dap = X8086.SegmentOffetToAbsolute(mRegisters.DS, mRegisters.SI);
                bufSize = (int)(get_RAM(dap + 3) << 8 | get_RAM(dap + 2));
                int seg = (int)(get_RAM(dap + 7) << 8 | get_RAM(dap + 6));
                int Off = (int)(get_RAM(dap + 5) << 8 | get_RAM(dap + 4));
                offset = (long)(get_RAM(dap + 0xF) << 56 | get_RAM(dap + 0xE) << 48 |
                    get_RAM(dap + 0xD) << 40 | get_RAM(dap + 0xC) << 32 |
                    get_RAM(dap + 0xB) << 24 | get_RAM(dap + 0xA) << 16 |
                    get_RAM(dap + 0x9) << 8 | get_RAM(dap + 0x8));

                if (offset < 0 || (int)(offset + bufSize) > (int)dskImg.FileLength)
                {
                    X8086.Notify("Write Sectors: Drive {0:000} Seek Fail", NotificationReasons.Warn, mRegisters.DL);
                    ret = 0x40; // seek failed
                }

                X8086.Notify("Drive {0:000} Write {4:000} {5:X6} <- {6:X4}:{7:X4}", NotificationReasons.Info,
                    mRegisters.DL,
                    bufSize,
                    offset,
                    seg,
                    Off);

                byte[] buf = new byte[bufSize];
                CopyFromMemory(buf, X8086.SegmentOffetToAbsolute((ushort)seg, (ushort)(Off)));
                ret = dskImg.Write((ulong)offset, buf);
                if (ret == DiskImage.EIO)
                {
                    X8086.Notify("Write Sectors: Drive {0:000} CRC Error", NotificationReasons.Warn, mRegisters.DL);
                    ret = 0x10; // CRC error
                }
                else if (ret == DiskImage.EOF)
                {
                    X8086.Notify("Write Sectors: Drive {0:000} Sector Not Found", NotificationReasons.Warn, mRegisters.DL);
                    ret = 0x4; // sector not found
                }
                AL = bufSize / dskImg.SectorSize;
            } // Extended get Drive Parameters
            else if (mRegisters.AH == ((byte)(0x48)))
            {
                if (ReferenceEquals(dskImg, null))
                {
                    X8086.Notify("Invalid Drive Number: Drive {0:000} Not Ready", NotificationReasons.Info, mRegisters.DL);
                    ret = 0xAA; // fixed disk drive not ready
                }

                if (dskImg.Tracks <= 0)
                {
                    X8086.Notify("Get Drive Parameters: Drive {0:000} Unknown Geometry", NotificationReasons.Warn, mRegisters.DL);
                    ret = 0xAA;
                }
                else
                {
                    throw (new NotImplementedException("Extended get Drive Parameters is not Implemented"));
                    X8086.Notify("Drive {0:000} Get Parameters", NotificationReasons.Info, mRegisters.DL);
                    ret = 0;
                }
            }
            else
            {
                X8086.Notify("Drive {0:000} Unknown Request {1}", NotificationReasons.Err,
                    mRegisters.DL,
                    ((mRegisters.AX & 0xFF00) >> 8).ToString("X2"));
                ret = 0x1;
            }

            if (mRegisters.AH != 0)
            {
                set_RAM8((ushort)(0x40), (ushort)(0x41), 0, false, (byte)ret);
                mRegisters.AX = (ushort)((ret << 8) | AL);
            }
            mFlags.CF = (byte)(ret != 0 ? 1 : 0);

            lastAH[mRegisters.DL] = (ushort)(mRegisters.AH);
            lastCF[mRegisters.DL] = mFlags.CF;

            if ((mRegisters.DL & 0x80) != 0)
            {
                Memory[0x474] = mRegisters.AH;
            }

            return true;
        }
    }
}
