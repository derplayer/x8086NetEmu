using System.Collections.Generic;
using System;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Xml.Linq;
using System.Collections;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using x8086SharpEmu;

namespace x8086SharpEmu
{

    // http://www.maverick-os.dk/Mainmenu_NoFrames.html
    // http://www.patersontech.com/dos/byte%E2%80%93inside-look.aspx
    // https://506889e3-a-62cb3a1a-s-sites.googlegroups.com/site/pcdosretro/fatgen.pdf?attachauth=ANoY7cr_oPcwxUv1I3jB9eaDf2z368nLQUQBc6_zKTfZw8FAs47xAK7Mf3btR_bQEpE5UwDLFgDJrTMovoZOrlC4Eg2qMn935KsT6IAvl5GxhoO_fqmzH7lcAY-7u9y-pbrUKVweCor3XkJPcSg1p-c7COBrRPjHhCgmAIJz1KCZ0iDBzxeE-pGWJ7gbj9-51DovkOLBzmYEcdJVH8xGIwGR_qufNhUuvQ%3D%3D&attredirects=0

    public class StandardDiskFormat
    {
        private int[,] geometryTable = new int[,] {
                {40, 1, 8, 160 * 1024},
                    {40, 2, 8, 320 * 1024},
                        {40, 1, 9, 180 * 1024},
                            {40, 2, 9, 360 * 1024},
                                {80, 2, 9, 720 * 1024},
                                    {80, 2, 15, 1200 * 1024},
                                        {80, 2, 18, 1440 * 1024},
                                            {80, 2, 36, 2880 * 1024}};

        public enum BootIndicators : byte
        {
            NonBootable = 0,
            SystemPartition = 0x80
        }

        public enum SystemIds : byte
        {
            EMPTY = 0,
            FAT_12 = 1,
            XENIX_ROOT = 2,
            XENIX_USER = 3,
            FAT_16 = 4,
            EXTENDED = 5,
            FAT_BIGDOS = 6,
            NTFS_HPFS = 7,
            AIX = 8,
            AIX_INIT = 9,
            OS2_BOOT_MGR = 10,
            PRI_FAT32_INT13 = 11,
            EXT_FAT32_INT13 = 12,
            EXT_FAT16_INT13 = 14,
            PRI_FAT16_INT13 = 15,
            OPUS = 16,
            CPQ_DIAGNOSTIC = 18,
            OMEGA_FS = 20,
            SWAP_PARTITION = 21,
            NEC_MSDOS = 36,
            VENIX = 64,
            SFS = 66,
            DISK_MANAGER = 80,
            NOVEL1 = 81,
            CPM_MICROPORT = 82,
            GOLDEN_BOW = 86,
            SPEEDSTOR = 97,
            UNIX_SYSV386 = 99, // GNU_HURD
            NOVEL2 = 100,
            PC_IX = 117,
            MINUX_OLD = 128,
            MINUX_LINUX = 129,
            LINUX_SWAP = 130,
            LINUX_NATIVE = 131,
            AMOEBA = 147,
            AMOEBA_BBT = 148,
            BSD_386 = 165,
            BSDI_FS = 183,
            BSDI_SWAP = 184,
            SYRINX = 199,
            CP_M = 219,
            ACCESS_DOS = 225,
            DOS_R_O = 227,
            DOS_SECONDARY = 242,
            LAN_STEP = 254,
            BBT = 255
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Partition
        {
            public BootIndicators BootIndicator;
            public byte StartingHead; // FEDCBA9876 543210
            public ushort StartingSectorCylinder; // cccccccccc ssssss
            public SystemIds SystemId;
            public byte EndingHead;
            public ushort EndingSectorCylinder; // cccccccccc ssssss
            public uint RelativeSector;
            public uint TotalSectors;

            public new string ToString()
            {
                return "{SystemId}: {BootIndicator}";
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct MBR
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 446)] public byte[] BootCode;
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.Struct, SizeConst = 4)] public Partition[] Partitions;
            public ushort Signature;

            public bool IsBootable
            {
                get
                {
                    return Signature == 0xAA55;
                }
            }
        }

        private MBR mMasterBootRecord;
        private readonly object[] mBootSectors = new object[4]; // FAT12.BootSector
        private readonly ushort[][] mFATDataPointers = new ushort[4][];
        private readonly object[][] mRootDirectoryEntries = new object[4][]; // FAT12.DirectoryEntry

        private System.IO.Stream strm;
        private readonly long[] FATRegionStart = new long[4];

        public StandardDiskFormat(System.IO.Stream s)
        {
            GCHandle pb = new GCHandle();
            byte[] b = new byte[512];

            strm = s;

            // Assume Floppy Image (No partitions)
            // FIXME: There has to be a better way to know if the image is a floppy or a hard disk
            //        Perhaps some better way to detect if the image has a master boot record or something...
            strm.Position = 0;
            strm.Read(b, 0, b.Length);
            pb = GCHandle.Alloc(b, GCHandleType.Pinned);
            FAT12.BootSector bs = (FAT12.BootSector)Marshal.PtrToStructure(pb.AddrOfPinnedObject(), typeof(FAT12.BootSector));
            pb.Free();
            if (bs.BIOSParameterBlock.BytesPerSector == 512)
            {
                LoadAsFloppyImage();
            }
            else
            {
                LoadAsHardDiskImage();
            }
        }

        private void LoadAsFloppyImage()
        {
            GCHandle pb = new GCHandle();
            byte[] b = new byte[512];

            strm.Position = 0;

            mMasterBootRecord.Partitions = new Partition[1];
            mMasterBootRecord.Partitions[0] = new Partition { BootIndicator = BootIndicators.SystemPartition };

            for (int i = 0; i <= (int)((double)geometryTable.Length / 4 - 1); i++)
            {
                if (strm.Length == geometryTable[i, 3])
                {
                    mMasterBootRecord.Partitions[0].EndingSectorCylinder = (ushort)(((geometryTable[i, 0] & 0x3FC) << 8) |
                        ((geometryTable[i, 0] & 0x3) << 6) |
                        geometryTable[i, 2]);
                    mMasterBootRecord.Partitions[0].EndingHead = (byte)(geometryTable[i, 1]);
                    break;
                }
            }

            strm.Position = 0;
            strm.Read(b, 0, b.Length);

            pb = GCHandle.Alloc(b, GCHandleType.Pinned);
            mBootSectors[0] = Marshal.PtrToStructure(pb.AddrOfPinnedObject(), typeof(FAT12.BootSector));
            pb.Free();

            if ((string)(((FAT12.BootSector)mBootSectors[0]).ExtendedBIOSParameterBlock.FileSystemType) == "FAT12")
            {
                mMasterBootRecord.Partitions[0].SystemId = SystemIds.FAT_12;
            }
            else if ((string)(((FAT12.BootSector)mBootSectors[0]).ExtendedBIOSParameterBlock.FileSystemType) == "FAT16")
            {
                mMasterBootRecord.Partitions[0].SystemId = SystemIds.FAT_16;
            }
            else
            {
                mMasterBootRecord.Partitions[0].SystemId = SystemIds.EMPTY;
            }

            mMasterBootRecord.Signature = (ushort)(((FAT12.BootSector)mBootSectors[0]).Signature);

            ReadFAT(0);
        }

        private void LoadAsHardDiskImage()
        {
            GCHandle pb = new GCHandle();
            byte[] b = new byte[512];

            strm.Position = 0;
            strm.Read(b, 0, b.Length);
            pb = GCHandle.Alloc(b, GCHandleType.Pinned);
            mMasterBootRecord = (MBR)Marshal.PtrToStructure(pb.AddrOfPinnedObject(), typeof(MBR));
            pb.Free();

            for (int partitionNumber = 0; partitionNumber <= 4 - 1; partitionNumber++)
            {
                strm.Position = (long)(mMasterBootRecord.Partitions[partitionNumber].RelativeSector * 512);
                strm.Read(b, 0, b.Length);
                pb = GCHandle.Alloc(b, GCHandleType.Pinned);

                if (mMasterBootRecord.Partitions[partitionNumber].SystemId == StandardDiskFormat.SystemIds.FAT_12)
                {
                    mBootSectors[partitionNumber] = Marshal.PtrToStructure(pb.AddrOfPinnedObject(), typeof(FAT12.BootSector));
                }
                else if (mMasterBootRecord.Partitions[partitionNumber].SystemId == StandardDiskFormat.SystemIds.FAT_16)
                {
                    mBootSectors[partitionNumber] = Marshal.PtrToStructure(pb.AddrOfPinnedObject(), typeof(FAT16.BootSector));
                }
                else if (mMasterBootRecord.Partitions[partitionNumber].SystemId == StandardDiskFormat.SystemIds.FAT_BIGDOS)
                {
                    mBootSectors[partitionNumber] = Marshal.PtrToStructure(pb.AddrOfPinnedObject(), typeof(FAT32.BootSector));
                }
                else if (mMasterBootRecord.Partitions[partitionNumber].SystemId == SystemIds.EMPTY)
                {
                    pb.Free();
                    continue;
                }

                pb.Free();
                ReadFAT(partitionNumber);
            }
        }

        private void ReadFAT(int partitionNumber)
        {
            FATRegionStart[partitionNumber] = strm.Position;

            mFATDataPointers[partitionNumber] = new ushort[((uint)(((FAT12.BootSector)mBootSectors[partitionNumber]).BIOSParameterBlock.SectorsPerFAT)) * ((FAT12.BootSector)mBootSectors[partitionNumber]).BIOSParameterBlock.BytesPerSector / 2];
            for (int j = 0; j <= mFATDataPointers[partitionNumber].Length - 1; j++)
            {
                // mBootSectors(partitionNumber).ExtendedBIOSParameterBlock.FileSystemType
                if (mMasterBootRecord.Partitions[partitionNumber].SystemId == StandardDiskFormat.SystemIds.FAT_12) // FIXME: FAT cluster chain is not correctly built when the file system if FAT12
                {
                    mFATDataPointers[partitionNumber][j] = BitConverter.ToUInt16(new[] { (byte)strm.ReadByte(), (byte)strm.ReadByte() }, 0);
                }
                else if (mMasterBootRecord.Partitions[partitionNumber].SystemId == StandardDiskFormat.SystemIds.FAT_16)
                {
                    mFATDataPointers[partitionNumber][j] = BitConverter.ToUInt16(new[] { (byte)strm.ReadByte(), (byte)strm.ReadByte() }, 0);
                }
                else if (mMasterBootRecord.Partitions[partitionNumber].SystemId == StandardDiskFormat.SystemIds.FAT_BIGDOS)
                {
                    mFATDataPointers[partitionNumber][j] = BitConverter.ToUInt16(new[] { (byte)strm.ReadByte(), (byte)strm.ReadByte() }, 0);
                }
            }

            if ((mFATDataPointers[partitionNumber][0] & 0xFF) == ((FAT12.BootSector)mBootSectors[partitionNumber]).BIOSParameterBlock.MediaDescriptor)
            {
                mRootDirectoryEntries[partitionNumber] = GetDirectoryEntries(partitionNumber, -1);
            }
            else
            {
                // Invalid boot sector
            }
        }

        public object[] GetDirectoryEntries(int partitionNumber, int clusterIndex = -1) // FAT12.DirectoryEntry()
        {
            ushort bytesInCluster = (ushort)(((FAT12.BootSector)mBootSectors[partitionNumber]).BIOSParameterBlock.SectorsPerCluster * ((FAT12.BootSector)mBootSectors[partitionNumber]).BIOSParameterBlock.BytesPerSector);
            GCHandle pb = new GCHandle();
            object[] des = null; // FAT12.DirectoryEntry = Nothing
            byte[] b = new byte[32];
            uint bytesRead = 0;
            int dirEntryCount = -1;
            dynamic de = null;

            while (clusterIndex < 0xFFF8)
            {
                if (clusterIndex == -1)
                {
                    strm.Position = (long)(FATRegionStart[partitionNumber] + ((FAT12.BootSector)mBootSectors[partitionNumber]).BIOSParameterBlock.NumberOfFATCopies * mFATDataPointers[partitionNumber].Length * 2);
                }
                else
                {
                    strm.Position = ClusterIndexToSector(partitionNumber, clusterIndex);
                }

                do
                {
                    strm.Read(b, 0, b.Length);
                    // First char of FileName
                    if (b[0] == ((byte)0))
                    {
                        clusterIndex = -1;
                        goto endOfDoLoop;
                    }
                    else if (b[0] == ((byte)5))
                    {
                        b[0] = (byte)(0xE5);
                    }
                    pb = GCHandle.Alloc(b, GCHandleType.Pinned);
                    if ((mMasterBootRecord.Partitions[partitionNumber].SystemId == SystemIds.FAT_12) || (mMasterBootRecord.Partitions[partitionNumber].SystemId == SystemIds.FAT_16))
                    {
                        de = Marshal.PtrToStructure(pb.AddrOfPinnedObject(), typeof(FAT12.DirectoryEntry));
                    }
                    else if (mMasterBootRecord.Partitions[partitionNumber].SystemId == SystemIds.FAT_BIGDOS)
                    {
                        de = Marshal.PtrToStructure(pb.AddrOfPinnedObject(), typeof(FAT32.DirectoryEntry));
                    }
                    pb.Free();

                    if (de.StartingClusterValue > 0)
                    {
                        dirEntryCount++;
                        Array.Resize(ref des, dirEntryCount + 1);
                        des[dirEntryCount] = de;
                    }

                    if (clusterIndex != -1)
                    {
                        bytesRead += (uint)b.Length;
                        if (bytesRead % bytesInCluster == 0)
                        {
                            clusterIndex = (int)(mFATDataPointers[partitionNumber][clusterIndex]);
                            break;
                        }
                    }
                } while (true);
                endOfDoLoop:

                if (clusterIndex == -1)
                {
                    break;
                }
            }

            return des;
        }

        private long ClusterIndexToSector(int partitionNumber, int clusterIndex)
        {
            dynamic bs = mBootSectors[partitionNumber];
            long rootDirectoryRegionStart = (long)(FATRegionStart[partitionNumber] + bs.BIOSParameterBlock.NumberOfFATCopies * bs.BIOSParameterBlock.SectorsPerFAT * bs.BIOSParameterBlock.BytesPerSector);
            long dataRegionStart = rootDirectoryRegionStart + bs.BIOSParameterBlock.MaxRootEntries * 32;
            return dataRegionStart + (clusterIndex - 2) * bs.BIOSParameterBlock.SectorsPerCluster * bs.BIOSParameterBlock.BytesPerSector;
        }

        public byte[] ReadFile(int partitionNumber, dynamic de)
        {
            uint bytesInCluster = (uint)(((FAT12.BootSector)mBootSectors[partitionNumber]).BIOSParameterBlock.SectorsPerCluster * ((FAT12.BootSector)mBootSectors[partitionNumber]).BIOSParameterBlock.BytesPerSector);
            uint clustersInFile = (uint)(Math.Ceiling(System.Convert.ToDecimal(de.FileSize / bytesInCluster)));
            byte[] b = new byte[clustersInFile * bytesInCluster];
            ushort clusterIndex = (ushort)(de.StartingClusterValue);
            long bytesRead = 0;

            while (clusterIndex < 0xFFF8 && bytesRead < de.FileSize)
            {
                strm.Position = ClusterIndexToSector(partitionNumber, clusterIndex);

                do
                {
                    b[bytesRead] = (byte)(strm.ReadByte());
                    bytesRead++;

                    if (bytesRead >= de.FileSize && (bytesRead % bytesInCluster) == 0)
                    {
                        clusterIndex = (ushort)(mFATDataPointers[partitionNumber][clusterIndex]);
                        break;
                    }
                } while (true);
            }

            Array.Resize(ref b, de.FileSize);
            return b;
        }

        public MBR MasterBootRecord
        {
            get
            {
                return mMasterBootRecord;
            }
        }

        public dynamic BootSector(int partitionIndex)
        {
            return mBootSectors[partitionIndex];
        }

        public bool IsClean(int partitionIndex)
        {
            return (mFATDataPointers[partitionIndex][1] & 0x8000) != 0;
        }

        public bool IsBootable(int partitionIndex)
        {
            return mMasterBootRecord.Partitions[partitionIndex].BootIndicator == BootIndicators.SystemPartition &&
                ((FAT12.BootSector)mBootSectors[partitionIndex]).Signature == 0xAA55;
        }

        public bool ReadWriteError(int partitionIndex)
        {
            return (mFATDataPointers[partitionIndex][1] & 0x4000) == 0;
        }

        public object[] RootDirectoryEntries(int partitionIndex)
        {
            return mRootDirectoryEntries[partitionIndex];
        }

        public short Cylinders(int partitionIndex)
        {
            short sc = (short)(mMasterBootRecord.Partitions[partitionIndex].StartingSectorCylinder >> 6);
            sc = (short)((sc >> 2) | ((sc & 0x3) << 8));
            short ec = (short)(mMasterBootRecord.Partitions[partitionIndex].EndingSectorCylinder >> 6);
            ec = (short)((ec >> 2) | ((ec & 0x3) << 8));
            return (short)(ec - sc + 1);
        }

        public short Sectors(int partitionIndex)
        {
            short ss = (short)(mMasterBootRecord.Partitions[partitionIndex].StartingSectorCylinder & 0x3F);
            short es = (short)(mMasterBootRecord.Partitions[partitionIndex].EndingSectorCylinder & 0x3F);
            return (short)(es - ss);
        }

        public short Heads(int partitionIndex)
        {
            return (short)(mMasterBootRecord.Partitions[partitionIndex].EndingHead - mMasterBootRecord.Partitions[partitionIndex].StartingHead);
        }
    }
}
