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
    public partial class X8086
    {
        private byte tmpF;
        private Dictionary<uint, IOPortHandler> portsCache = new Dictionary<uint, IOPortHandler>();
        private GPFlags.FlagsTypes[] szpLUT8 = new GPFlags.FlagsTypes[256];
        private GPFlags.FlagsTypes[] szpLUT16 = new GPFlags.FlagsTypes[65536];
        private AddressingMode[] decoderCache = new AddressingMode[65536];

        public enum ParamIndex
        {
            First = 0,
            Second = 1,
            Thrid = 2
        }

        public enum DataSize
        {
            UseAddressingMode = -1,
            @Byte = 0,
            Word = 1,
            DWord = 2
        }

        public enum Operation
        {
            Add,
            AddWithCarry,
            Substract,
            SubstractWithCarry,
            LogicOr,
            LogicAnd,
            LogicXor,
            Increment,
            Decrement,
            Compare,
            Test,
            Unknown
        }

        public struct AddressingMode
        {
            public byte Direction;
            public DataSize Size;
            public byte Modifier;
            public byte Rm;
            public byte Reg;
            public GPRegisters.RegistersTypes Register1;
            public GPRegisters.RegistersTypes Register2;
            public bool IsDirect;
            public ushort IndAdr; // Indirect Address
            public ushort IndMem; // Indirect Memory Contents

            public GPRegisters.RegistersTypes Src;
            public GPRegisters.RegistersTypes Dst;

            private byte regOffset;

            // http://aturing.umcs.maine.edu/~meadow/courses/cos335/8086-instformat.pdf
            public void Decode(byte data, byte addressingModeByte)
            {
                Size = (DataSize)(data & 1); // (0000 0001)
                Direction = System.Convert.ToByte((data >> 1) & 1); // (0000 0010)
                Modifier = (byte)(addressingModeByte >> 6); // (1100 0000)
                Reg = System.Convert.ToByte((addressingModeByte >> 3) & 7); // (0011 1000)
                Rm = (byte)(addressingModeByte & 7); // (0000 0111)

                regOffset = (byte)((int)Size << 3);

                Register1 = (GPRegisters.RegistersTypes)(Reg | regOffset);
                if (Register1 >= GPRegisters.RegistersTypes.ES)
                {
                    Register1 += (int)GPRegisters.RegistersTypes.ES;
                }

                Register2 = (GPRegisters.RegistersTypes)(Rm | regOffset);
                if (Register2 >= GPRegisters.RegistersTypes.ES)
                {
                    Register2 += (int)GPRegisters.RegistersTypes.ES;
                }

                if (Direction == 0)
                {
                    Src = Register1;
                    Dst = Register2;
                }
                else
                {
                    Src = Register2;
                    Dst = Register1;
                }
            }
        }

        private void SetRegister1Alt(byte data)
        {
            addrMode.Register1 = (GPRegisters.RegistersTypes)((data & 0x7) | shl3);
            if (addrMode.Register1 >= GPRegisters.RegistersTypes.ES)
            {
                addrMode.Register1 += (int)GPRegisters.RegistersTypes.ES;
            }
            addrMode.Size = DataSize.Word;
        }

        private void SetRegister2ToSegReg()
        {
            addrMode.Register2 = (GPRegisters.RegistersTypes)(addrMode.Reg + (int)GPRegisters.RegistersTypes.ES);
            addrMode.Size = DataSize.Word;
        }

        private void SetAddressing(DataSize forceSize = X8086.DataSize.UseAddressingMode)
        {
#if DEBUG
            addrMode.Decode(opCode, get_RAM8(mRegisters.CS, (ushort)(mRegisters.IP + 1), (byte)0, false));
#else
			addrMode = decoderCache[(opCode << 8) | get_RAM8(mRegisters.CS, (ushort) (mRegisters.IP + 1), (byte) 0, false)];
#endif

            if (forceSize != DataSize.UseAddressingMode)
            {
                addrMode.Size = forceSize;
            }

            // AS = Active Segment
            // AS = SS when Rm = 2, 3 or 6
            // If Rm = 6 and Modifier = 0, AS will be set to DS instead
            // http://www.ic.unicamp.br/~celio/mc404s2-03/addr_modes/intel_addr.html

            if (!mRegisters.ActiveSegmentChanged)
            {
                if (addrMode.Rm == 2 || addrMode.Rm == 3)
                {
                    mRegisters.ActiveSegmentRegister = GPRegisters.RegistersTypes.SS;
                }
                else if (addrMode.Rm == 6 && addrMode.Modifier != 0)
                {
                    mRegisters.ActiveSegmentRegister = GPRegisters.RegistersTypes.SS;
                }
            }

            // http://umcs.maine.edu/~cmeadow/courses/cos335/Asm07-MachineLanguage.pdf
            // http://maven.smith.edu/~thiebaut/ArtOfAssembly/CH04/CH04-2.html#HEADING2-35
            if (addrMode.Modifier == ((byte)0)) // 00
            {
                addrMode.IsDirect = false;
                if (addrMode.Rm == ((byte)0))
                {
                    addrMode.IndAdr = (ushort)(mRegisters.BX + mRegisters.SI);
                    clkCyc += 7; // 000 [BX+SI]
                }
                else if (addrMode.Rm == ((byte)1))
                {
                    addrMode.IndAdr = (ushort)(mRegisters.BX + mRegisters.DI);
                    clkCyc += 8; // 001 [BX+DI]
                }
                else if (addrMode.Rm == ((byte)2))
                {
                    addrMode.IndAdr = (ushort)(mRegisters.BP + mRegisters.SI);
                    clkCyc += 8; // 010 [BP+SI]
                }
                else if (addrMode.Rm == ((byte)3))
                {
                    addrMode.IndAdr = (ushort)(mRegisters.BP + mRegisters.DI);
                    clkCyc += 7; // 011 [BP+DI]
                }
                else if (addrMode.Rm == ((byte)4))
                {
                    addrMode.IndAdr = mRegisters.SI;
                    clkCyc += 5; // 100 [SI]
                }
                else if (addrMode.Rm == ((byte)5))
                {
                    addrMode.IndAdr = mRegisters.DI;
                    clkCyc += 5; // 101 [DI]
                }
                else if (addrMode.Rm == ((byte)6))
                {
                    addrMode.IndAdr = System.Convert.ToUInt16(To32bitsWithSign(Param(ParamIndex.First, (ushort)2, DataSize.Word)));
                    clkCyc += 9; // 110 Direct Addressing
                }
                else if (addrMode.Rm == ((byte)7))
                {
                    addrMode.IndAdr = mRegisters.BX;
                    clkCyc += 5; // 111 [BX]
                }
                addrMode.IndMem = get_RAMn();
            } // 01 - 8bit
            else if (addrMode.Modifier == ((byte)1))
            {
                addrMode.IsDirect = false;
                if (addrMode.Rm == ((byte)0))
                {
                    addrMode.IndAdr = (ushort)(mRegisters.BX + mRegisters.SI);
                    clkCyc += 7; // 000 [BX+SI]
                }
                else if (addrMode.Rm == ((byte)1))
                {
                    addrMode.IndAdr = (ushort)(mRegisters.BX + mRegisters.DI);
                    clkCyc += 8; // 001 [BX+DI]
                }
                else if (addrMode.Rm == ((byte)2))
                {
                    addrMode.IndAdr = (ushort)(mRegisters.BP + mRegisters.SI);
                    clkCyc += 8; // 010 [BP+SI]
                }
                else if (addrMode.Rm == ((byte)3))
                {
                    addrMode.IndAdr = (ushort)(mRegisters.BP + mRegisters.DI);
                    clkCyc += 7; // 011 [BP+DI]
                }
                else if (addrMode.Rm == ((byte)5))
                {
                    addrMode.IndAdr = mRegisters.DI;
                    clkCyc += 5; // 101 [DI]
                }
                else if (addrMode.Rm == ((byte)4))
                {
                    addrMode.IndAdr = mRegisters.SI;
                    clkCyc += 5; // 100 [SI]
                }
                else if (addrMode.Rm == ((byte)6))
                {
                    addrMode.IndAdr = mRegisters.BP;
                    clkCyc += 5; // 110 [BP]
                }
                else if (addrMode.Rm == ((byte)7))
                {
                    addrMode.IndAdr = mRegisters.BX;
                    clkCyc += 5; // 111 [BX]
                }
                addrMode.IndAdr += To16bitsWithSign(Param(ParamIndex.First, (ushort)2, DataSize.Byte));
                addrMode.IndMem = get_RAMn();
            } // 10 - 16bit
            else if (addrMode.Modifier == ((byte)2))
            {
                addrMode.IsDirect = false;
                if (addrMode.Rm == ((byte)0))
                {
                    addrMode.IndAdr = (ushort)(mRegisters.BX + mRegisters.SI);
                    clkCyc += 7; // 000 [BX+SI]
                }
                else if (addrMode.Rm == ((byte)1))
                {
                    addrMode.IndAdr = (ushort)(mRegisters.BX + mRegisters.DI);
                    clkCyc += 8; // 001 [BX+DI]
                }
                else if (addrMode.Rm == ((byte)2))
                {
                    addrMode.IndAdr = (ushort)(mRegisters.BP + mRegisters.SI);
                    clkCyc += 8; // 010 [BP+SI]
                }
                else if (addrMode.Rm == ((byte)3))
                {
                    addrMode.IndAdr = (ushort)(mRegisters.BP + mRegisters.DI);
                    clkCyc += 7; // 011 [BP+DI]
                }
                else if (addrMode.Rm == ((byte)4))
                {
                    addrMode.IndAdr = mRegisters.SI;
                    clkCyc += 5; // 100 [SI]
                }
                else if (addrMode.Rm == ((byte)5))
                {
                    addrMode.IndAdr = mRegisters.DI;
                    clkCyc += 5; // 101 [DI]
                }
                else if (addrMode.Rm == ((byte)6))
                {
                    addrMode.IndAdr = mRegisters.BP;
                    clkCyc += 5; // 110 [BP]
                }
                else if (addrMode.Rm == ((byte)7))
                {
                    addrMode.IndAdr = mRegisters.BX;
                    clkCyc += 5; // 111 [BX]
                }
                //var dbgTmpValue = System.Convert.ToUInt16(To32bitsWithSign(Param(ParamIndex.First, (ushort)2, DataSize.Word)));
                addrMode.IndAdr += ((Param(ParamIndex.First, (ushort)2, DataSize.Word)));
                addrMode.IndMem = get_RAMn();
            } // 11
            else if (addrMode.Modifier == ((byte)3))
            {
                addrMode.IsDirect = true;
            }

            opCodeSize++;
        }

        private ushort To16bitsWithSign(ushort v)
        {
            return System.Convert.ToUInt16(((v & 0x80) != 0) ? 0xFF00U | v : v);
        }

        private uint To32bitsWithSign(ushort v)
        {
            //return System.Convert.ToUInt32(((v & 0x8000) != 0) ? 0xFFFF_0000 | v : v);
            return (uint)(((v & 0x8000) != 0) ? (-65536 | v) : v);
        }

        private uint ToXbitsWithSign(uint v)
        {
            return System.Convert.ToUInt32(addrMode.Size == DataSize.Byte ? (To16bitsWithSign(System.Convert.ToUInt16(v))) : (To32bitsWithSign(System.Convert.ToUInt16(v))));
        }

        private void SendToPort(uint portAddress, uint value)
        {
            mDoReSchedule = true;

            if (portsCache.ContainsKey(portAddress))
            {
                portsCache[portAddress].Out(portAddress, (ushort)value);
                //X8086.Notify(String.Format("Write {0} to Port {1} on Adapter '{2}'", value.ToString("X2"), portAddress.ToString("X4"), portsCache(portAddress).Name), NotificationReasons.Info)
                return;
            }
            else
            {
                foreach (IOPortHandler p in mPorts)
                {
                    if (p.ValidPortAddress.Contains(portAddress))
                    {
                        p.Out(portAddress, System.Convert.ToUInt16(value));
                        //X8086.Notify(String.Format("Write {0} to Port {1} on Adapter '{2}'", value.ToString("X2"), portAddress.ToString("X4"), p.Name), NotificationReasons.Info)
                        portsCache.Add(portAddress, p);
                        return;
                    }
                }

                foreach (Adapter a in mAdapters)
                {
                    if (a.ValidPortAddress.Contains(portAddress))
                    {
                        a.Out(portAddress, System.Convert.ToUInt16(value));
                        //X8086.Notify(String.Format("Write {0} to Port {1} on Adapter '{2}'", value.ToString("X2"), portAddress.ToString("X4"), a.Name), NotificationReasons.Info)
                        portsCache.Add(portAddress, a);
                        return;
                    }
                }
            }

            NoIOPort((int)portAddress);
        }

        private uint ReceiveFromPort(uint portAddress)
        {
            mDoReSchedule = true;

            if (portsCache.ContainsKey(portAddress))
            {
                //X8086.Notify(String.Format("Read From Port {0} on Adapter '{1}'", portAddress.ToString("X4"), portsCache(portAddress).Name), NotificationReasons.Info)
                return portsCache[portAddress].In(portAddress);
            }
            else
            {
                foreach (IOPortHandler p in mPorts)
                {
                    if (p.ValidPortAddress.Contains(portAddress))
                    {
                        //X8086.Notify(String.Format("Read From Port {0} on Adapter '{1}'", portAddress.ToString("X4"), p.Name), NotificationReasons.Info)
                        portsCache.Add(portAddress, p);
                        return System.Convert.ToUInt32(p.In(portAddress));
                    }
                }

                foreach (Adapter a in mAdapters)
                {
                    if (a.ValidPortAddress.Contains(portAddress))
                    {
                        //X8086.Notify(String.Format("Read From Port {0} on Adapter '{1}'", portAddress.ToString("X4"), a.Name), NotificationReasons.Info)
                        portsCache.Add(portAddress, a);
                        return System.Convert.ToUInt32(a.In(portAddress));
                    }
                }
            }

            NoIOPort((int)portAddress);
            return (uint)(0xFF);
        }

        private ushort Param(ParamIndex index, ushort ipOffset = 1, DataSize size = X8086.DataSize.UseAddressingMode)
        {
            if (size == DataSize.UseAddressingMode)
            {
                size = addrMode.Size;
            }
            opCodeSize += (byte)(size + 1);
            return ParamNOPS(index, ipOffset, size);
        }

        private ushort ParamNOPS(ParamIndex index, ushort ipOffset = 1, DataSize size = X8086.DataSize.UseAddressingMode)
        {
            // Extra cycles for address misalignment
            // This is too CPU expensive, with few benefits, if any... not worth it
            //If (mRegisters.IP Mod 2) <> 0 Then clkCyc += 4

            return System.Convert.ToUInt16((size == DataSize.Byte || (size == DataSize.UseAddressingMode && addrMode.Size == DataSize.Byte)) ? (
                get_RAM8(mRegisters.CS, mRegisters.IP, (byte)(ipOffset + index), true)) : (
                get_RAM16(mRegisters.CS, mRegisters.IP, (byte)(ipOffset + (int)index * 2), true)));
        }

        private ushort OffsetIP(DataSize size)
        {
            //return System.Convert.ToUInt16(size == DataSize.Byte ? (
            //	mRegisters.IP + To16bitsWithSign(Param(index: ParamIndex.First, size: size)) + opCodeSize) : (
            //	mRegisters.IP + Param(index: ParamIndex.First, size: size) + opCodeSize));
            return (size == DataSize.Byte) ? ((ushort)((ushort)(mRegisters.IP + To16bitsWithSign(Param(ParamIndex.First, (ushort)1, size))) + opCodeSize)) : ((ushort)((ushort)(mRegisters.IP + Param(ParamIndex.First, (ushort)1, size)) + opCodeSize));
        }

        private ushort Eval(uint v1, uint v2, Operation opMode, DataSize size)
        {
            uint result = 0;
            switch (opMode)
            {
                case Operation.Add:
                    result = v1 + v2;
                    SetAddSubFlags(result, v1, v2, size, false);
                    break;

                case Operation.AddWithCarry:
                    result = v1 + v2 + mFlags.CF;
                    SetAddSubFlags(result, v1, v2, size, false);
                    break;

                case Operation.Substract:
                case Operation.Compare:
                    result = v1 - v2;
                    SetAddSubFlags(result, v1, v2, size, true);
                    break;

                case Operation.SubstractWithCarry:
                    result = v1 - v2 - mFlags.CF;
                    SetAddSubFlags(result, v1, v2, size, true);
                    break;

                case Operation.LogicOr:
                    result = v1 | v2;
                    SetLogicFlags(result, size);
                    break;

                case Operation.LogicAnd:
                case Operation.Test:
                    result = v1 & v2;
                    SetLogicFlags(result, size);
                    break;

                case Operation.LogicXor:
                    result = v1 ^ v2;
                    SetLogicFlags(result, size);
                    break;

                case Operation.Increment:
                    result = v1 + v2;
                    tmpF = mFlags.CF;
                    SetAddSubFlags(result, v1, v2, size, false);
                    mFlags.CF = tmpF;
                    break;

                case Operation.Decrement:
                    result = v1 - v2;
                    tmpF = mFlags.CF;
                    SetAddSubFlags(result, v1, v2, size, true);
                    mFlags.CF = tmpF;
                    break;

            }

            if (size == DataSize.Byte)
                return (byte)(result & 0xFF);
            else if (size == DataSize.Word)
                return (ushort)(result & 0xFFFF);
            else
                return (ushort)(result);
        }

        private void SetSZPFlags(uint result, DataSize size)
        {
            GPFlags.FlagsTypes ft = size == DataSize.Byte ? (
                szpLUT8[result & 0xFF]) : (
                szpLUT16[result & 0xFFFF]);

            mFlags.PF = (byte)(((ft & GPFlags.FlagsTypes.PF) != 0) ? 1 : 0);
            mFlags.ZF = (byte)(((ft & GPFlags.FlagsTypes.ZF) != 0) ? 1 : 0);
            mFlags.SF = (byte)(((ft & GPFlags.FlagsTypes.SF) != 0) ? 1 : 0);
        }

        private void SetLogicFlags(uint result, DataSize size)
        {
            SetSZPFlags(result, size);

            mFlags.CF = (byte)0;
            mFlags.OF = (byte)0;
        }

        private void SetAddSubFlags(uint result, uint v1, uint v2, DataSize size, bool isSubstraction)
        {
            SetSZPFlags(result, size);

            if (size == DataSize.Byte)
            {
                mFlags.CF = (byte)((((long)result & 0xFF00) != 0) ? 1 : 0);
                mFlags.OF = (byte)(((((long)result ^ v1) & ((isSubstraction ? v1 : result) ^ v2) & 0x80) != 0) ? 1 : 0);
            }
            else
            {
                mFlags.CF = (byte)(((result & 0xFFFF_0000) != 0) ? 1 : 0);
                mFlags.OF = (byte)(((((long)result ^ v1) & ((isSubstraction ? v1 : result) ^ v2) & 0x8000) != 0) ? 1 : 0);
            }

            mFlags.AF = (byte)((((long)(v1 ^ v2 ^ result) & 0x10) != 0) ? 1 : 0);
        }

        public static ushort BitsArrayToWord(bool[] b)
        {
            ushort r = (ushort)0;
            for (int i = 0; i <= b.Length - 1; i++)
            {
                if (b[i])
                {
                    r += System.Convert.ToUInt16(Math.Pow(2, i));
                }
            }
            return r;
        }

        public static void WordToBitsArray(ushort value, bool[] a)
        {
            for (int i = 0; i <= a.Length - 1; i++)
            {
                //a[i] = (value & Math.Pow(2, i)) != 0;
                a[i] = ((value & (long)Math.Round(Math.Pow(2.0, i))) != 0);
            }
        }

        protected internal void SetUpAdapter(Adapter adptr)
        {
            if (adptr.Type == Adapter.AdapterType.Keyboard)
            {
                mKeyboard = (KeyboardAdapter)adptr;
            }
            else if (adptr.Type == Adapter.AdapterType.SerialMouseCOM1)
            {
                mMouse = (MouseAdapter)adptr;
            }
            else if (adptr.Type == Adapter.AdapterType.Video)
            {
                mVideoAdapter = (VideoAdapter)adptr;
                SetupSystem();
            }
            else if (adptr.Type == Adapter.AdapterType.Floppy)
            {
                mFloppyController = (FloppyControllerAdapter)adptr;
            }
        }

        private void AddInternalHooks()
        {
            if (mEmulateINT13)
            {
                TryAttachHook((byte)(0x13), HandleINT13); // Disk I/O Emulation
            }
        }

        private void BuildDecoderCache()
        {
            for (int i = 0; i <= 255; i++)
            {
                for (int j = 0; j <= 255; j++)
                {
                    addrMode.Decode((byte)i, (byte)j);
                    decoderCache[(i << 8) | j] = addrMode;
                }
            }
        }

        private void BuildSZPTables()
        {
            uint d = 0;

            for (int c = 0; c <= szpLUT8.Length - 1; c++)
            {
                d = (uint)0;
                if ((c & 1) != 0)
                {
                    d++;
                }
                if ((c & 2) != 0)
                {
                    d++;
                }
                if ((c & 4) != 0)
                {
                    d++;
                }
                if ((c & 8) != 0)
                {
                    d++;
                }
                if ((c & 16) != 0)
                {
                    d++;
                }
                if ((c & 32) != 0)
                {
                    d++;
                }
                if ((c & 64) != 0)
                {
                    d++;
                }
                if ((c & 128) != 0)
                {
                    d++;
                }

                szpLUT8[c] = ((d & 1) != 0) ? 0 : GPFlags.FlagsTypes.PF;
                if (c == 0)
                {
                    szpLUT8[c] = szpLUT8[c] | GPFlags.FlagsTypes.ZF;
                }
                if ((c & 0x80) != 0)
                {
                    szpLUT8[c] = szpLUT8[c] | GPFlags.FlagsTypes.SF;
                }
            }

            for (int c = 0; c <= szpLUT16.Length - 1; c++)
            {
                d = (uint)0;
                if ((c & 1) != 0)
                {
                    d++;
                }
                if ((c & 2) != 0)
                {
                    d++;
                }
                if ((c & 4) != 0)
                {
                    d++;
                }
                if ((c & 8) != 0)
                {
                    d++;
                }
                if ((c & 16) != 0)
                {
                    d++;
                }
                if ((c & 32) != 0)
                {
                    d++;
                }
                if ((c & 64) != 0)
                {
                    d++;
                }
                if ((c & 128) != 0)
                {
                    d++;
                }

                szpLUT16[c] = ((d & 1) != 0) ? 0 : GPFlags.FlagsTypes.PF;
                if (c == 0)
                {
                    szpLUT16[c] = szpLUT16[c] | GPFlags.FlagsTypes.ZF;
                }
                if ((c & 0x8000) != 0)
                {
                    szpLUT16[c] = szpLUT16[c] | GPFlags.FlagsTypes.SF;
                }
            }
        }

        // If necessary, in future versions we could implement support for
        //   multiple hooks attached to the same interrupt and execute them based on some priority condition
        public bool TryAttachHook(byte intNum, IntHandler handler)
        {
            if (intHooks.ContainsKey(intNum))
            {
                intHooks.Remove(intNum);
            }
            intHooks.Add(intNum, handler);
            return true;
        }

        public bool TryAttachHook(MemHandler handler)
        {
            memHooks.Add(handler);
            return true;
        }

        public bool TryDetachHook(byte intNum)
        {
            if (!intHooks.ContainsKey(intNum))
            {
                return false;
            }
            intHooks.Remove(intNum);
            return true;
        }

        public bool TryDetachHook(MemHandler memHandler)
        {
            if (!memHooks.Contains(memHandler))
            {
                return false;
            }
            memHooks.Remove(memHandler);
            return true;
        }

        public List<Adapter> GetAdaptersByType(Adapter.AdapterType adapterType)
        {
            return (from adptr in mAdapters where adptr.Type == adapterType select adptr).ToList();
        }

        public static bool IsRunningOnMono
        {
            get
            {
                return Type.GetType("Mono.Runtime") != null;
            }
        }

        public static string FixPath(string fileName)
        {
#if Win32 || Win32_dbg
            return fileName;
#else
            //			return System.Convert.ToString( Environment.OSVersion.Platform == PlatformID.Unix ? (
            //			fileName.Replace("\\", System.IO.Path.DirectorySeparatorChar.ToString())) :
            //			fileName);
#endif
        }

        private void PrintOpCodes(ushort n)
        {
            for (int i = mRegisters.IP; i <= mRegisters.IP + n - 1; i++)
            {
                Debug.Write(get_RAM8(mRegisters.CS, (ushort)i, (byte)0, false).ToString("X") + " ");
            }
        }

        private void PrintRegisters()
        {
            X8086.Notify("AX: {0}   SP: {1} ", NotificationReasons.Info, mRegisters.AX.ToString("X4"), mRegisters.SP.ToString("X4"));
            X8086.Notify("BX: {0}   DI: {1} ", NotificationReasons.Info, mRegisters.BX.ToString("X4"), mRegisters.DI.ToString("X4"));
            X8086.Notify("CX: {0}   BP: {1} ", NotificationReasons.Info, mRegisters.CX.ToString("X4"), mRegisters.BP.ToString("X4"));
            X8086.Notify("DX: {0}   SI: {1} ", NotificationReasons.Info, mRegisters.DX.ToString("X4"), mRegisters.SI.ToString("X4"));
            X8086.Notify("ES: {0}   CS: {1} ", NotificationReasons.Info, mRegisters.ES.ToString("X4"), mRegisters.CS.ToString("X4"));
            X8086.Notify("SS: {0}   DS: {1} ", NotificationReasons.Info, mRegisters.SS.ToString("X4"), mRegisters.DS.ToString("X4"));
            X8086.Notify("IP: {0} FLGS: {1}{2}{3}{4}{5}{6}{7}{8}", NotificationReasons.Info,
                mRegisters.IP.ToString("X4"),
                mFlags.CF,
                mFlags.ZF,
                mFlags.SF,
                mFlags.OF,
                mFlags.PF,
                mFlags.AF,
                mFlags.IF,
                mFlags.DF);
            X8086.Notify("                CZSOPAID", NotificationReasons.Info);
            for (int i = 0; i <= 3; i++)
            {
                Debug.Write(get_RAM8(mRegisters.CS, (ushort)(mRegisters.IP + i), (byte)0, false).ToString("X2") + " ");
            }
        }

        private void PrintFlags()
        {
            X8086.Notify("{0}{1}{2}{3}{4}{5}{6}{7}", NotificationReasons.Info,
                mFlags.CF,
                mFlags.ZF,
                mFlags.SF,
                mFlags.OF,
                mFlags.PF,
                mFlags.AF,
                mFlags.IF,
                mFlags.DF);
            X8086.Notify("CZSOPAID", NotificationReasons.Info);
        }

        private void PrintStack()
        {
            int f = Math.Min(mRegisters.SP + (0xFFFF - mRegisters.SP) - 1, mRegisters.SP + 10);
            int t = Math.Max(0, mRegisters.SP - 10);

            for (int i = f; i >= t; i -= 2)
            {
                X8086.Notify("{0}:{1}  {2}{3}", NotificationReasons.Info,
                    mRegisters.SS.ToString("X4"),
                    i.ToString("X4"),
                    get_RAM16(mRegisters.SS, (ushort)i, (byte)0, true).ToString("X4"), i == mRegisters.SP ? "<<" : "");
            }
        }
    }
}
