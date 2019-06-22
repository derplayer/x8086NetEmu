using System.Collections.Generic;
using System;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Xml.Linq;
using System.Collections;
using System.Windows.Forms;
using System.Runtime.InteropServices;

using x8086SharpEmu;

namespace x8086SharpEmu
{

    public class FAT12
    {
        public enum EntryAttributes : byte
        {
            @ReadOnly = 1,
            Hidden = 2,
            System = 4,
            VolumeName = 8,
            Directory = 16,
            ArchiveFlag = 32
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ParameterBlock
        {
            public ushort BytesPerSector;
            public byte SectorsPerCluster;
            public ushort ReservedSectors;
            public byte NumberOfFATCopies;
            public ushort MaxRootEntries;
            public ushort TotalSectors;
            public byte MediaDescriptor;
            public ushort SectorsPerFAT;
            public ushort SectorsPerTrack;
            public ushort HeadsPerCylinder;
            public uint HiddenSectors;
            public uint TotalSectorsBig;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct ExtendedParameterBlock
        {
            public byte DriveNumber;
            public byte Reserved;
            public byte ExtendedBootSignature;
            public uint SerialNumber;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 11)] private readonly byte[] VolumeLabelChars;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] private readonly byte[] FileSystemTypeChars;

            public string VolumeLabel
            {
                get
                {
                    return System.Text.Encoding.ASCII.GetString(VolumeLabelChars).Replace("\0", "").TrimEnd();
                }
            }

            public string FileSystemType
            {
                get
                {
                    return System.Text.Encoding.ASCII.GetString(FileSystemTypeChars).TrimEnd();
                }
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct DirectoryEntry
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public byte[] FileNameChars;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] private readonly byte[] FileExtensionChars;
            public EntryAttributes Attribute;
            public byte ReservedNT;
            public byte Creation;
            public ushort CreationTime;
            public ushort CreationDate;
            public ushort LastAccessDate;
            public ushort ReservedFAT32;
            public ushort LastWriteTime;
            public ushort LastWriteDate;
            public ushort StartingCluster;
            public uint FileSize;

            public int StartingClusterValue
            {
                get
                {
                    return StartingCluster;
                }
            }

            public string FileName
            {
                get
                {
                    return System.Text.Encoding.ASCII.GetString(FileNameChars).TrimEnd();
                }
            }

            public string FileExtension
            {
                get
                {
                    return System.Text.Encoding.ASCII.GetString(FileExtensionChars).TrimEnd();
                }
            }

            public string FullFileName
            {
                get
                {
                    string fn = FileName.TrimEnd();
                    string fe = FileExtension.TrimEnd();
                    if (!string.IsNullOrEmpty(fe))
                    {
                        fe = "." + fe;
                    }

                    return fn + fe;
                }
            }

            public DateTime CreationDateTime
            {
                get
                {
                    int[] t = FSTimeToNative(CreationTime);
                    int[] d = FSDateToNative(CreationTime);
                    return new DateTime(d[2], d[1], d[0], t[2], t[1], t[0]);
                }
            }

            public DateTime WriteDateTime
            {
                get
                {
                    int[] t = FSTimeToNative(LastWriteTime);
                    int[] d = FSDateToNative(LastWriteDate);
                    try
                    {
                        return new DateTime(d[0], d[1], d[2], t[0], t[1], t[2]);
                    }
                    catch
                    {
                        return new DateTime(1980, 1, 1, 0, 0, 0);
                    }
                }
            }

            private int[] FSTimeToNative(ushort v)
            {
                int s = System.Convert.ToInt32((v & 0x1F) * 2);
                int m = System.Convert.ToInt32((v & 0x3E0) >> 5);
                int h = System.Convert.ToInt32((v & 0xF800) >> 11);
                return new[] { h, m, s };
            }

            private int[] FSDateToNative(ushort v)
            {
                int d = v & 0x1F;
                int m = System.Convert.ToInt32((v & 0x1E0) >> 5);
                int y = System.Convert.ToInt32(((v & 0xFE00) >> 9) + 1980);
                return new[] { y, m, d };
            }

            public new string ToString()
            {
                string[] attrs = Enum.GetNames(typeof(EntryAttributes));
                string attr = "";
                for (int i = 0; i <= attrs.Length - 1; i++)
                {
                    if (((long)(Math.Pow(2, i)) & (int)Attribute) != 0)
                    {
                        attr += attrs[i] + " ";
                    }
                }
                attr = attr.TrimEnd();

                return "{FullFileName} [{attr}]";
            }

            public static bool operator ==(DirectoryEntry d1, DirectoryEntry d2)
            {
                return d1.Attribute == d2.Attribute && d1.StartingClusterValue == d2.StartingClusterValue;
            }

            public static bool operator !=(DirectoryEntry d1, DirectoryEntry d2)
            {
                return !(d1 == d2);
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct BootSector
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public byte[] JumpCode;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] private readonly byte[] OemIdChars;
            [MarshalAs(UnmanagedType.Struct)] public ParameterBlock BIOSParameterBlock;
            [MarshalAs(UnmanagedType.Struct)] public ExtendedParameterBlock ExtendedBIOSParameterBlock;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 448)] public byte[] BootStrapCode;
            public ushort Signature;

            public string OemId
            {
                get
                {
                    return System.Text.Encoding.ASCII.GetString(OemIdChars).TrimEnd();
                }
            }
        }
    }

    public class FAT16 : FAT12
    {
    }

    public class FAT32 : FAT12
    {

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public new struct ParameterBlock
        {
            public ushort BytesPerSector;
            public byte SectorsPerCluster;
            public ushort ReservedSectors;
            public byte NumberOfFATCopies;
            public ushort MaxRootEntries; // Not used
            public ushort TotalSectors; // Not used
            public byte MediaDescriptor;
            public ushort SectorsPerFAT;
            public ushort SectorsPerTrack;
            public ushort HeadsPerCylinder;
            public uint HiddenSectors;
            public uint SectorsInPartition;
            public uint SectorsPerFATNew;
            public ushort FATHandlingFlags;
            public ushort Version;
            public uint ClusterStartRootDirectory;
            public ushort SectorStartFileSystemInformationSector;
            public ushort SectorStartBackupBootSector;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)] private byte[] Reserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public new struct DirectoryEntry
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public byte[] FileNameChars;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] private readonly byte[] FileExtensionChars;
            public byte Attribute;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public byte[] Unused01;
            public ushort StartingCluster;
            public ushort CreationTime;
            public ushort CreationDate;
            public ushort StartingClusterLowWord;
            public uint FileSize;

            public int StartingClusterValue
            {
                get
                {
                    return (StartingCluster << 8) | StartingClusterLowWord;
                }
            }

            public ushort LastWriteTime
            {
                get
                {
                    return CreationTime;
                }
            }

            public ushort LastWriteDate
            {
                get
                {
                    return CreationDate;
                }
            }

            public string FileName
            {
                get
                {
                    return System.Text.Encoding.ASCII.GetString(FileNameChars).TrimEnd();
                }
            }

            public string FileExtension
            {
                get
                {
                    return System.Text.Encoding.ASCII.GetString(FileExtensionChars).TrimEnd();
                }
            }

            public string FullFileName
            {
                get
                {
                    string fn = FileName.TrimEnd();
                    string fe = FileExtension.TrimEnd();
                    if (!string.IsNullOrEmpty(fe))
                    {
                        fe = "." + fe;
                    }

                    return fn + fe;
                }
            }

            public DateTime CreationDateTime
            {
                get
                {
                    int[] t = FSTimeToNative(CreationTime);
                    int[] d = FSDateToNative(CreationTime);
                    return new DateTime(d[2], d[1], d[0], t[2], t[1], t[0]);
                }
            }

            public DateTime WriteDateTime
            {
                get
                {
                    int[] t = FSTimeToNative(LastWriteTime);
                    int[] d = FSDateToNative(LastWriteDate);

                    try
                    {
                        return new DateTime(d[0], d[1], d[2], t[0], t[1], t[2]);
                    }
                    catch
                    {
                        return new DateTime(1980, 1, 1, 0, 0, 0);
                    }
                }
            }

            private int[] FSTimeToNative(ushort v)
            {
                int s = System.Convert.ToInt32((v & 0x1F) * 2);
                int m = System.Convert.ToInt32((v & 0x3E0) >> 5);
                int h = System.Convert.ToInt32((v & 0xF800) >> 11);
                return new[] { h, m, s };
            }

            private int[] FSDateToNative(ushort v)
            {
                int d = v & 0x1F;
                int m = System.Convert.ToInt32((v & 0x1E0) >> 5);
                int y = System.Convert.ToInt32(((v & 0xFE00) >> 9) + 1980);
                return new[] { y, m, d };
            }

            public new string ToString()
            {
                string[] attrs = Enum.GetNames(typeof(EntryAttributes));
                string attr = "";
                for (int i = 0; i <= attrs.Length - 1; i++)
                {
                    if (((long)(Math.Pow(2, i)) & Attribute) != 0)
                    {
                        attr += attrs[i] + " ";
                    }
                }
                attr = attr.TrimEnd();

                return "{FullFileName} [{attr}]";
            }

            public static bool operator ==(DirectoryEntry d1, DirectoryEntry d2)
            {
                return d1.Attribute == d2.Attribute && d1.StartingClusterValue == d2.StartingClusterValue;
            }

            public static bool operator !=(DirectoryEntry d1, DirectoryEntry d2)
            {
                return !(d1 == d2);
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public new struct BootSector
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public byte[] JumpCode;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] private readonly byte[] OemIdChars;
            [MarshalAs(UnmanagedType.Struct)] public ParameterBlock BIOSParameterBlock;
            [MarshalAs(UnmanagedType.Struct)] public ExtendedParameterBlock ExtendedBIOSParameterBlock;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 420)] public byte[] BootStrapCode;
            public ushort Signature;

            public string OemId
            {
                get
                {
                    return System.Text.Encoding.ASCII.GetString(OemIdChars).TrimEnd();
                }
            }
        }
    }
}
