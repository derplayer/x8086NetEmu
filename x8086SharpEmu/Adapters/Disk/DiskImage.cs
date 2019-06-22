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
    // Reverse-Engineering DOS 1.0
    // http://www.pagetable.com/?p=165
    // IBMBIO: http://www.pagetable.com/?p=184
    // also look at the msdos MIT source code: https://github.com/microsoft/MS-DOS

    public class DiskImage
    {
        public const int EOF = -1;
        public const int EIO = -2;

        public enum ImageStatus
        {
            NoDisk,
            DiskLoaded,
            DiskImageNotFound,
            UnsupportedImageFormat
        }

        public enum DriveTypes
        {
            Dt360k = 1,
            Dt12M = 2,
            Dt720K = 3,
            Dt144M = 4
        }

        private int[,] geometryTable = new int[,] {
                {40, 1, 8, 160 * 1024},
                    {40, 2, 8, 320 * 1024},
                        {40, 1, 9, 180 * 1024},
                            {40, 2, 9, 360 * 1024},
                                {80, 2, 9, 720 * 1024},
                                    {80, 2, 15, 1200 * 1024},
                                        {80, 2, 18, 1440 * 1024},
                                            {80, 2, 36, 2880 * 1024}};

        private System.IO.FileStream file;
        protected internal ushort mCylinders;
        protected internal ushort mHeads;
        protected internal ushort mSectors;
        protected internal ushort mSectorSize;
        protected internal bool mReadOnly;
        protected internal ImageStatus mStatus = ImageStatus.NoDisk;
        protected internal ulong mFileLength;
        protected internal bool mIsHardDisk;
        protected internal string mFileName;
        protected internal DriveTypes mDriveType;

        protected internal static int mHardDiskCount;

        public DiskImage()
        {
        }

        public bool IsReadOnly
        {
            get
            {
                return mReadOnly;
            }
        }

        public ImageStatus Status
        {
            get
            {
                return mStatus;
            }
        }

        public bool IsHardDisk
        {
            get
            {
                return mIsHardDisk;
            }
        }

        public string FileName
        {
            get
            {
                return mFileName;
            }
        }

        public static int HardDiskCount
        {
            get
            {
                return mHardDiskCount;
            }
        }

        public DiskImage(string fileName, bool mountInReadOnlyMode = false, bool isHardDisk = false)
        {
            mFileName = X8086.FixPath(fileName);

            mCylinders = (ushort)(0);
            mHeads = (ushort)(0);
            mSectors = (ushort)(0);
            mFileLength = 0;

            if (!System.IO.File.Exists(mFileName))
            {
                mStatus = ImageStatus.DiskImageNotFound;
            }
            else
            {
                OpenImage(mountInReadOnlyMode, isHardDisk);
            }

            X8086.Notify("DiskImage '{0}': {1}", X8086.NotificationReasons.Info, mFileName, mStatus.ToString());
        }

        private void OpenImage(bool mountInReadOnlyMode, bool isHardDisk)
        {
            mReadOnly = mountInReadOnlyMode;

            try
            {
                if (mReadOnly)
                {
                    file = new System.IO.FileStream(mFileName, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
                }
                else
                {
                    file = new System.IO.FileStream(mFileName, System.IO.FileMode.Open, System.IO.FileAccess.ReadWrite, System.IO.FileShare.ReadWrite);
                }

                mFileLength = (ulong)file.Length;
                mIsHardDisk = isHardDisk;
                if (isHardDisk)
                {
                    mHardDiskCount++;
                }

                if (MatchGeometry())
                {
                    mStatus = ImageStatus.DiskLoaded;
                }
                else
                {
                    mStatus = ImageStatus.UnsupportedImageFormat;
                }
            }
            catch (UnauthorizedAccessException)
            {
                if (mountInReadOnlyMode)
                {
                    mStatus = ImageStatus.UnsupportedImageFormat;
                }
                else
                {
                    OpenImage(true, isHardDisk);
                }
            }
            catch (Exception)
            {
                mStatus = ImageStatus.UnsupportedImageFormat;
            }
        }

        // Guess the image's disk geometry based on its size
        protected internal bool MatchGeometry()
        {
            mSectorSize = (ushort)512;

            if (mIsHardDisk)
            {
                mStatus = ImageStatus.DiskLoaded;

                if (MatchGeometryMBR())
                {
                    return true;
                }
                if (MatchGeometryDOS())
                {
                    return true;
                }

                return false;
            }
            else
            {
                mCylinders = (ushort)(0);
                mHeads = (ushort)(0);
                mSectors = (ushort)(0);

                for (int i = 0; i <= (int)((double)geometryTable.Length / 4 - 1); i++)
                {
                    if (mFileLength == (ulong)geometryTable[i, 3])
                    {
                        mCylinders = (ushort)(geometryTable[i, 0]);
                        mHeads = (ushort)(geometryTable[i, 1]);
                        mSectors = (ushort)(geometryTable[i, 2]);
                        return true;
                    }
                }

                // Cheap trick to handle images with garbage at the end of the image file (such as the copyright crap inserted by DiskImage)
                for (int i = 0; i <= (int)((double)geometryTable.Length / 4 - 1); i++)
                {
                    if (Math.Abs((int)mFileLength - geometryTable[i, 3]) <= 512)
                    {
                        mCylinders = (ushort)(geometryTable[i, 0]);
                        mHeads = (ushort)(geometryTable[i, 1]);
                        mSectors = (ushort)(geometryTable[i, 2]);
                        return true;
                    }
                }

                return false;
            }
        }

        private bool MatchGeometryDOS()
        {
            byte[] b = new byte[512];
            if (Read(0, b) != 0)
            {
                return false;
            }
            if (b[510] != 0x55 || b[511] != 0xAA)
            {
                return false;
            }

            if ((b[11 + 1] << 8) + b[11] != mSectorSize)
            {
                return false;
            }

            int h = (b[26 + 1] << 8) + b[26];
            int s = (b[24 + 1] << 8) + b[24];

            if (h == 0 || h > 255)
            {
                return false;
            }
            if (s == 0 || s > 255)
            {
                return false;
            }

            int c = (int)((double)mFileLength / (h * s * mSectorSize));

            mCylinders = (ushort)c;
            mSectors = (ushort)s;
            mHeads = (ushort)h;

            return true;
        }

        private bool MatchGeometryMBR()
        {
            byte[] b = new byte[512];
            if (Read(0, b) != 0)
            {
                return false;
            }
            if (b[510] != 0x55 || b[511] != 0xAA)
            {
                return false;
            }

            int tc1 = 0;
            int th1 = 0;
            int ts1 = 0;

            int tc2 = 0;
            int th2 = 0;
            int ts2 = 0;

            int c = 0;
            int h = 0;
            int s = 0;

            int p = 0;
            for (int i = 0; i <= 4 - 1; i++)
            {
                p = 0x1BE + 16 * i;

                if ((b[p] & 0x7F) != 0)
                {
                    return false;
                }

                // Partition Start
                tc1 = (int)(b[p + 3] | ((b[p + 2] & 0xC0) << 2));
                th1 = b[p + 1];
                ts1 = (int)(b[p + 2] & 0x3F);
                h = (int)(th1 > h ? th1 : h);
                s = (int)(ts1 > s ? ts1 : s);

                // Partition End
                tc2 = (int)(b[p + 7] | ((b[p + 6] & 0xC0) << 2));
                th2 = b[p + 5];
                ts2 = (int)(b[p + 6] & 0x3F);
                h = (int)(th2 > h ? th2 : h);
                s = (int)(ts2 > s ? ts2 : s);

                if (tc2 < tc1)
                {
                    return false;
                }
                else if (tc2 == tc1)
                {
                    if (th2 < th1)
                    {
                        return false;
                    }
                    else if (th2 == th1)
                    {
                        if (ts2 < ts1)
                        {
                            return false;
                        }
                    }
                }
            }

            if (s == 0)
            {
                return false;
            }

            h++;
            c = (int)((double)mFileLength / (h * s * mSectorSize));

            mCylinders = (ushort)c;
            mSectors = (ushort)s;
            mHeads = (ushort)h;

            return true;
        }

        public uint LBA(uint cylinder, uint head, uint sector)
        {
            if ((int)mStatus != (int)ImageStatus.DiskLoaded)
            {
                return (uint)(0);
            }

            cylinder = cylinder | ((sector & 0xC0) << 2);
            sector = sector & 0x3F;

            if (cylinder >= mCylinders || sector == 0 || sector > mSectors || head >= mHeads)
            {
                return (uint)(0);
            }

            return (((cylinder * mHeads) + head) * mSectors + sector - 1) * mSectorSize;
        }

        public void Close()
        {
            try
            {
                if (mStatus == ImageStatus.DiskLoaded)
                {
                    file.Close();
                }
            }
            catch
            {
            }
            finally
            {
                mStatus = ImageStatus.NoDisk;
            }
        }

        public ulong FileLength
        {
            get
            {
                return mFileLength;
            }
        }

        public virtual int Read(ulong offset, byte[] data)
        {
            if ((int)mStatus != (int)ImageStatus.DiskLoaded)
            {
                return -1;
            }

            if (offset < 0 || offset + (ulong)data.Length > mFileLength)
            {
                return EOF;
            }

            try
            {
                file.Seek((long)offset, System.IO.SeekOrigin.Begin);
                file.Read(data, 0, data.Length);

                return 0;
            }
            catch (Exception)
            {
                return EIO;
            }
        }

        public virtual int Write(ulong offset, byte[] data)
        {
            if ((int)mStatus != (int)ImageStatus.DiskLoaded)
            {
                return -1;
            }

            if (offset < 0 || offset + (ulong)data.Length > mFileLength)
            {
                return EOF;
            }

            try
            {
                file.Seek((long)offset, System.IO.SeekOrigin.Begin);
                file.Write(data, 0, data.Length);
                return 0;
            }
            catch (Exception)
            {
                return EIO;
            }
        }

        public ushort Tracks
        {
            get
            {
                return mCylinders;
            }
        }

        public ushort Cylinders
        {
            get
            {
                return mCylinders;
            }
        }

        public ushort Heads
        {
            get
            {
                return mHeads;
            }
        }

        public ushort Sectors
        {
            get
            {
                return mSectors;
            }
        }

        public ushort SectorSize
        {
            get
            {
                return mSectorSize;
            }
        }
    }
}
