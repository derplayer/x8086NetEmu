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
    //The MAIN Guide: http://docs.huihoo.com/help-pc/index.html or http://stanislavs.org/helppc/ (mirror)
    // http://www.delorie.com/djgpp/doc/rbinter/ix/

    public partial class X8086
    {
        private ushort[] lastAH = new ushort[256];
        private byte[] lastCF = new byte[256];

        public void HandleHardwareInterrupt(byte intNum)
        {
            HandleInterrupt(intNum, true);
            mRegisters.IP = IPAddrOffet;
        }

        private void HandlePendingInterrupt()
        {
            // Lesson 5 (mRegisters.ActiveSegmentChanged = False)
            // http://ntsecurity.nu/onmymind/2007/2007-08-22.html

            if (mFlags.IF == 1 &&
                    mFlags.TF == 0 &&
                    !mRegisters.ActiveSegmentChanged &&
                    !newPrefix &&
                    picIsAvailable)
            {

                byte pendingIntNum = PIC.GetPendingInterrupt();
                if (pendingIntNum != 0xFF)
                {
                    if (mIsHalted)
                    {
                        mIsHalted = false;
                        // https://docs.oracle.com/cd/E19455-01/806-3773/instructionset-130/index.html
                        mRegisters.IP++; // Is this right??
                    }
                    HandleHardwareInterrupt(pendingIntNum);
                }
            }
        }

        private void HandleInterrupt(byte intNum, bool isHard)
        {
            if (!(intHooks.ContainsKey(intNum) && intHooks[intNum].Invoke()))
            {
                PushIntoStack((ushort)(mFlags.EFlags));
                PushIntoStack(mRegisters.CS);

                if (isHard)
                {
                    PushIntoStack((ushort)(mRegisters.IP - newPrefixLast));
                }
                else
                {
                    PushIntoStack((ushort)(mRegisters.IP + opCodeSize));
                }

                tmpUVal = (uint)(intNum * 4);
                IPAddrOffet = get_RAM16((ushort)0, (ushort)(tmpUVal), (byte)0, true);
                mRegisters.CS = get_RAM16((ushort)0, (ushort)(tmpUVal), (byte)2, true);

                if (intNum == 0)
                {
                    ThrowException("Division By Zero");
                }
            }

            mFlags.IF = (byte)0;
            mFlags.TF = (byte)0;

            clkCyc += 51;
        }
    }
}
