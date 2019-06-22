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

    public partial class X8086
    {
        public const uint MemSize = 0x10_0000U; // 1MB
        public const uint ROMStart = 0xC_0000U;

        public readonly byte[] Memory = new byte[MemSize];

        private uint address;
        private const ushort shl2 = 1 << 2;
        private const ushort shl3 = 1 << 3;

        public class MemoryAccessEventArgs : EventArgs
        {

            public enum AccessModes
            {
                Read,
                Write
            }

            public AccessModes AccessMode { get; set; }
            public uint Address { get; set; }

            public MemoryAccessEventArgs(uint address, AccessModes accesMode)
            {
                this.Address = address;
                this.AccessMode = accesMode;
            }
        }

        public delegate void MemoryAccessEventHandler(object sender, MemoryAccessEventArgs e);
        private MemoryAccessEventHandler MemoryAccessEvent;

        public event MemoryAccessEventHandler MemoryAccess
        {
            add
            {
                MemoryAccessEvent = (MemoryAccessEventHandler)System.Delegate.Combine(MemoryAccessEvent, value);
            }
            remove
            {
                MemoryAccessEvent = (MemoryAccessEventHandler)System.Delegate.Remove(MemoryAccessEvent, value);
            }
        }


        [StructLayout(LayoutKind.Explicit)]
        public class GPRegisters : ICloneable
        {

            public enum RegistersTypes
            {
                NONE = -1,

                AL = 0,
                AH = AL | shl2,
                AX = AL | shl3,

                BL = 3,
                BH = BL | shl2,
                BX = BL | shl3,

                CL = 1,
                CH = CL | shl2,
                CX = CL | shl3,

                DL = 2,
                DH = DL | shl2,
                DX = DL | shl3,

                ES = 12,
                CS = (int)ES + 1,
                SS = (int)ES + 2,
                DS = (int)ES + 3,

                SP = 24,
                BP = (int)SP + 1,
                SI = (int)SP + 2,
                DI = (int)SP + 3,
                IP = (int)SP + 4
            }

            [FieldOffset(0)] public ushort AX;
            [FieldOffset(0)] public byte AL;
            [FieldOffset(1)] public byte AH;

            [FieldOffset(2)] public ushort BX;
            [FieldOffset(2)] public byte BL;
            [FieldOffset(3)] public byte BH;

            [FieldOffset(4)] public ushort CX;
            [FieldOffset(4)] public byte CL;
            [FieldOffset(5)] public byte CH;

            [FieldOffset(6)] public ushort DX;
            [FieldOffset(6)] public byte DL;
            [FieldOffset(7)] public byte DH;

            [FieldOffset(8)] public ushort CS;
            [FieldOffset(10)] public ushort IP;

            [FieldOffset(12)] public ushort SS;
            [FieldOffset(14)] public ushort SP;

            [FieldOffset(16)] public ushort DS;
            [FieldOffset(18)] public ushort SI;

            [FieldOffset(20)] public ushort ES;
            [FieldOffset(22)] public ushort DI;

            [FieldOffset(24)] public ushort BP;

            [FieldOffset(26)] private RegistersTypes mActiveSegmentRegister;
            [FieldOffset(30)] private bool mActiveSegmentChanged;

            public ushort get_Val(RegistersTypes reg)
            {
                switch (reg)
                {
                    case RegistersTypes.AX:
                        return AX;
                    case RegistersTypes.AH:
                        return (ushort)(AH);
                    case RegistersTypes.AL:
                        return (ushort)(AL);

                    case RegistersTypes.BX:
                        return BX;
                    case RegistersTypes.BH:
                        return (ushort)(BH);
                    case RegistersTypes.BL:
                        return (ushort)(BL);

                    case RegistersTypes.CX:
                        return CX;
                    case RegistersTypes.CH:
                        return (ushort)(CH);
                    case RegistersTypes.CL:
                        return (ushort)(CL);

                    case RegistersTypes.DX:
                        return DX;
                    case RegistersTypes.DH:
                        return (ushort)(DH);
                    case RegistersTypes.DL:
                        return (ushort)(DL);

                    case RegistersTypes.CS:
                        return CS;
                    case RegistersTypes.IP:
                        return IP;

                    case RegistersTypes.SS:
                        return SS;
                    case RegistersTypes.SP:
                        return SP;

                    case RegistersTypes.DS:
                        return DS;
                    case RegistersTypes.SI:
                        return SI;

                    case RegistersTypes.ES:
                        return ES;
                    case RegistersTypes.DI:
                        return DI;

                    case RegistersTypes.BP:
                        return BP;

                    default:
                        throw (new Exception("Invalid Register: {reg}"));
                }
            }
            public void set_Val(RegistersTypes reg, ushort value)
            {
                switch (reg)
                {
                    case RegistersTypes.AX:
                        AX = value;
                        break;
                    case RegistersTypes.AH:
                        AH = (byte)value;
                        break;
                    case RegistersTypes.AL:
                        AL = (byte)value;
                        break;

                    case RegistersTypes.BX:
                        BX = value;
                        break;
                    case RegistersTypes.BH:
                        BH = (byte)value;
                        break;
                    case RegistersTypes.BL:
                        BL = (byte)value;
                        break;

                    case RegistersTypes.CX:
                        CX = value;
                        break;
                    case RegistersTypes.CH:
                        CH = (byte)value;
                        break;
                    case RegistersTypes.CL:
                        CL = (byte)value;
                        break;

                    case RegistersTypes.DX:
                        DX = value;
                        break;
                    case RegistersTypes.DH:
                        DH = (byte)value;
                        break;
                    case RegistersTypes.DL:
                        DL = (byte)value;
                        break;

                    case RegistersTypes.CS:
                        CS = value;
                        break;
                    case RegistersTypes.IP:
                        IP = value;
                        break;

                    case RegistersTypes.SS:
                        SS = value;
                        break;
                    case RegistersTypes.SP:
                        SP = value;
                        break;

                    case RegistersTypes.DS:
                        DS = value;
                        break;
                    case RegistersTypes.SI:
                        SI = value;
                        break;

                    case RegistersTypes.ES:
                        ES = value;
                        break;
                    case RegistersTypes.DI:
                        DI = value;
                        break;

                    case RegistersTypes.BP:
                        BP = value;
                        break;

                    default:
                        throw (new Exception("Invalid Register: {reg}"));
                }
            }

            public void ResetActiveSegment()
            {
                mActiveSegmentChanged = false;
                mActiveSegmentRegister = RegistersTypes.DS;
            }

            public RegistersTypes ActiveSegmentRegister
            {
                get
                {
                    return mActiveSegmentRegister;
                }
                set
                {
                    mActiveSegmentRegister = value;
                    mActiveSegmentChanged = true;
                }
            }

            public uint ActiveSegmentValue
            {
                get
                {
                    return (uint)(get_Val(mActiveSegmentRegister));
                }
            }

            public bool ActiveSegmentChanged
            {
                get
                {
                    return mActiveSegmentChanged;
                }
            }

            public string PointerAddressToString
            {
                get
                {
                    return CS.ToString("X4") + ":" + IP.ToString("X4");
                }
            }

            public dynamic Clone()
            {
                GPRegisters reg = new GPRegisters()
                {
                    AX = AX,
                    BX = BX,
                    CX = CX,
                    DX = DX,
                    ES = ES,
                    CS = CS,
                    SS = SS,
                    DS = DS,
                    SP = SP,
                    BP = BP,
                    SI = SI,
                    DI = DI,
                    IP = IP
                };
                if (mActiveSegmentChanged)
                {
                    reg.ActiveSegmentRegister = mActiveSegmentRegister;
                }
                return reg;
            }
        }

        public class GPFlags : ICloneable
        {

            public enum FlagsTypes
            {
                CF = 1,
                PF = 4,
                AF = 16,
                ZF = 64,
                SF = 128,
                TF = 256,
                @IF = 512,
                DF = 1024,
                @OF = 2048
            }

            public byte CF { get; set; }
            public byte PF { get; set; }
            public byte AF { get; set; }
            public byte ZF { get; set; }
            public byte SF { get; set; }
            public byte TF { get; set; }
            public byte IF { get; set; }
            public byte DF { get; set; }
            public byte OF { get; set; }

            //Note: that arent E(expanded)Flags, just the normal INTEL x86 Flags
            // https://en.wikipedia.org/wiki/FLAGS_register
            public ushort EFlags
            {
                get
                {
                    /*
                    // IOPL, NT and bit 15 are always "1" on 8086
                    return (ushort)((int) (CF * (int) FlagsTypes.CF) |  
						1 * Math.Pow(2, 1) |
						PF * (int) FlagsTypes.PF |
						0 * Math.Pow(2, 3) |
						AF * (int) FlagsTypes.AF |
						0 * Math.Pow(2, 5) |
						ZF * (int) FlagsTypes.ZF |
						SF * (int) FlagsTypes.SF |
						TF * (int) FlagsTypes.TF |
						IF * (int) FlagsTypes.IF |
						DF * (int) FlagsTypes.DF |
						OF * (int) FlagsTypes.OF |
						+ 0xF000); //HF000
                    */
                    return (ushort)((CF * (int)FlagsTypes.CF) |
                        (int)(1 * Math.Pow(2, 1)) | PF * (int)FlagsTypes.PF |
                        (int)(0 * Math.Pow(2, 3)) | AF * (int)FlagsTypes.AF |
                        (int)(0 * Math.Pow(2, 5)) | ZF * (int)FlagsTypes.ZF |
                        SF * (int)FlagsTypes.SF | TF * (int)FlagsTypes.TF |
                        IF * (int)FlagsTypes.IF | DF * (int)FlagsTypes.DF |
                        OF * (int)FlagsTypes.OF | 0xF000);
                }
                set
                {
                    CF = (byte)(((value & (ushort)FlagsTypes.CF) == (ushort)FlagsTypes.CF) ? 1 : 0);
                    // Reserved 1
                    PF = (byte)(((value & (ushort)FlagsTypes.PF) == (ushort)FlagsTypes.PF) ? 1 : 0);
                    // Reserved 0
                    AF = (byte)(((value & (ushort)FlagsTypes.AF) == (ushort)FlagsTypes.AF) ? 1 : 0);
                    // Reserved 0
                    ZF = (byte)(((value & (ushort)FlagsTypes.ZF) == (ushort)FlagsTypes.ZF) ? 1 : 0);
                    SF = (byte)(((value & (ushort)FlagsTypes.SF) == (ushort)FlagsTypes.SF) ? 1 : 0);
                    TF = (byte)(((value & (ushort)FlagsTypes.TF) == (ushort)FlagsTypes.TF) ? 1 : 0);
                    IF = (byte)(((value & (ushort)FlagsTypes.IF) == (ushort)FlagsTypes.IF) ? 1 : 0);
                    DF = (byte)(((value & (ushort)FlagsTypes.DF) == (ushort)FlagsTypes.DF) ? 1 : 0);
                    OF = (byte)(((value & (ushort)FlagsTypes.OF) == (ushort)FlagsTypes.OF) ? 1 : 0);
                }
            }

            public dynamic Clone()
            {
                return new GPFlags { EFlags = EFlags };
            }

        }

        public void LoadBIN(string fileName, ushort segment, ushort offset)
        {
            //X8086.Notify("Loading: {fileName} @ {segment:X4}:{offset:X4}", NotificationReasons.Info, null);
            X8086.Notify($"Loading: {fileName} @ {segment:X4}:{offset:X4}", NotificationReasons.Info);
            fileName = X8086.FixPath(fileName);

            if (System.IO.File.Exists(fileName))
            {
                CopyToMemory(System.IO.File.ReadAllBytes(fileName), segment, offset);
            }
            else
            {
                ThrowException(System.Convert.ToString("File Not Found: " + "\r\n" + fileName));
            }
        }

        public void CopyToMemory(byte[] bytes, ushort segment, ushort offset)
        {
            CopyToMemory(bytes, X8086.SegmentOffetToAbsolute(segment, offset));
        }

        public void CopyToMemory(byte[] bytes, uint address)
        {
            // TODO: We need to implement some checks to prevent loading code into ROM areas.
            //       Something like this, for example:
            //       If address + bytes.Length >= ROMStart Then ...
            Array.Copy(bytes, 0, Memory, (int)address, bytes.Length);
        }

        public void CopyFromMemory(byte[] bytes, uint address)
        {
            Array.Copy(Memory, (int)address, bytes, 0, bytes.Length);
        }

        public GPRegisters Registers
        {
            get
            {
                return mRegisters;
            }
            set
            {
                mRegisters = value;
            }
        }

        public GPFlags Flags
        {
            get
            {
                return mFlags;
            }
            set
            {
                mFlags = value;
            }
        }

        private void PushIntoStack(ushort value)
        {
            mRegisters.SP -= (ushort)2;
            set_RAM16(mRegisters.SS, mRegisters.SP, 0x00, true, value);
            //set_RAM16(segment: mRegisters.SS, offset: mRegisters.SP, ignoreHooks: true, value);
        }

        private ushort PopFromStack()
        {
            mRegisters.SP += (ushort)2;
            return get_RAM16(segment: mRegisters.SS, offset: (ushort)(mRegisters.SP - 2), ignoreHooks: true);
        }

        public static uint SegmentOffetToAbsolute(ushort segment, ushort offset)
        {
            return (uint)((int)(segment << 4) + offset);
        }

        public static ushort AbsoluteToSegment(uint address)
        {
            return (ushort)((address >> 4) & 0xF_FF00);
        }

        public static ushort AbsoluteToOffset(uint address)
        {
            return (ushort)(address & 0xFFF);
        }

        public byte get_RAM(uint address, bool ignoreHooks = false)
        {
            //If mDebugMode Then RaiseEvent MemoryAccess(Me, New MemoryAccessEventArgs(address, MemoryAccessEventArgs.AccessModes.Read))
            //Return FromPreftch(address)

            if (!ignoreHooks)
            {
                for (int i = 0; i <= memHooks.Count - 1; i++)
                {
                    if (memHooks[i].Invoke(address, (ushort)tmpUVal, MemHookMode.Read))
                    {
                        return (byte)(tmpUVal);
                    }
                }
            }

            return Memory[address & 0xF_FFFF]; // "Call 5" Legacy Interface: http://www.os2museum.com/wp/?p=734
        }
        public void set_RAM(uint address, bool ignoreHooks, byte value)
        {
            if (!ignoreHooks)
            {
                for (int i = 0; i <= memHooks.Count - 1; i++)
                {
                    if (memHooks[i].Invoke(address, value, MemHookMode.Write))
                    {
                        return;
                    }
                }
            }

            Memory[address & 0xF_FFFF] = value;

            //If mDebugMode Then RaiseEvent MemoryAccess(Me, New MemoryAccessEventArgs(address, MemoryAccessEventArgs.AccessModes.Write))
        }

        public byte get_RAM8(ushort segment, ushort offset, byte inc = 0, bool ignoreHooks = false)
        {
            return get_RAM(SegmentOffetToAbsolute(segment, (ushort)(offset + inc)), ignoreHooks);
        }
        public void set_RAM8(ushort segment, ushort offset, byte inc, bool ignoreHooks, byte value)
        {
            set_RAM(SegmentOffetToAbsolute(segment, (ushort)(offset + inc)), ignoreHooks, value);
        }

        public ushort get_RAM16(ushort segment, ushort offset, byte inc = 0, bool ignoreHooks = false)
        {
            address = SegmentOffetToAbsolute(segment, (ushort)(offset + inc));
            return (ushort)(((ushort)(get_RAM(address + 1, ignoreHooks) << 8)) | (int)(get_RAM(address, ignoreHooks)));
        }
        public void set_RAM16(ushort segment, ushort offset, byte inc, bool ignoreHooks, ushort value)
        {
            address = SegmentOffetToAbsolute(segment, (ushort)(offset + inc));
            set_RAM(address, ignoreHooks, (byte)value);
            set_RAM(address + 1, ignoreHooks, (byte)(value >> 8));
        }

        public ushort get_RAMn(bool ignoreHooks = false)
        {
            return (ushort)(addrMode.Size == DataSize.Byte ? (
                get_RAM8((ushort)(mRegisters.ActiveSegmentValue), addrMode.IndAdr, (byte)0, ignoreHooks)) : (
                get_RAM16((ushort)(mRegisters.ActiveSegmentValue), addrMode.IndAdr, (byte)0, ignoreHooks)));
        }
        public void set_RAMn(bool ignoreHooks, ushort value)
        {
            if (addrMode.Size == DataSize.Byte)
            {
                set_RAM8((ushort)(mRegisters.ActiveSegmentValue), addrMode.IndAdr, (byte)0, ignoreHooks, (byte)value);
            }
            else
            {
                set_RAM16((ushort)(mRegisters.ActiveSegmentValue), addrMode.IndAdr, (byte)0, ignoreHooks, value);
            }
        }
    }
}
