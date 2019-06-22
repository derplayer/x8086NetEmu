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

    public class HostFolderAsDisk : DiskImage
    {

        private int mDiskSize = 32 * 1024 * 1204; // 32MB
        private int bytesPerSector = 512;
        private byte[] buffer;

        private System.IO.FileStream s;
        private StandardDiskFormat image;

        public HostFolderAsDisk(string fileName, bool mountInReadOnlyMode = false)
        {
            throw (new NotImplementedException());

            mFileName = fileName;

            s = new System.IO.FileStream(mFileName, System.IO.FileMode.Open);
            image = new StandardDiskFormat(s);

            //mSectorSize = image.BootSectors(0).BIOSParameterBlock.BytesPerSector
            //mCylinders = image.Partitions(0).Cylinders + 1
            //mHeads = image.Partitions(0).Heads * 2
            //mSectors = image.Partitions(0).Sectors + 1
            //mFileLength = s.Length

            //mReadOnly = mountInReadOnlyMode
            //mIsHardDisk = CType(image.Partitions(0).FileSystem, FAT12_16.BootSector).BIOSParameterBlock.MediaDescriptor = &HF8
            //mStatus = ImageStatus.DiskLoaded

            X8086.Notify("DiskImage '{0}': {1}", X8086.NotificationReasons.Info, mFileName, mStatus.ToString());
        }

        public override int Read(ulong offset, byte[] data)
        {
            if ((int)mStatus != (int)ImageStatus.DiskLoaded)
            {
                return -1;
            }

            if (offset < 0 || offset + (ushort)data.Length > mFileLength)
            {
                //return System.Convert.ToInt32(FileSystem.EOF());
                return -1;
            }

            try
            {
                s.Seek((long)offset, System.IO.SeekOrigin.Begin);
                s.Read(data, 0, data.Length);

                return 0;
            }
            catch (Exception)
            {
                return EIO;
            }
        }

        public override int Write(ulong offset, byte[] data)
        {
            //If mStatus <> ImageStatus.DiskLoaded Then Return -1

            //If offset < 0 OrElse offset + data.Length > mFileLength Then Return EOF

            //Try
            //    file.Seek(offset, IO.SeekOrigin.Begin)
            //    file.Write(data, 0, data.Length)
            //    Return 0
            //Catch e As Exception
            //    Return EIO
            //End Try

            return EIO; // Just to suppress the warning
        }
    }

}
