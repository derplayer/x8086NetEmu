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
        private delegate void ExecOpcode();
        private ExecOpcode[] opCodes;

        private void _00_03() // ADD Gv Ev
        {
            SetAddressing();
            if (addrMode.IsDirect)
            {
                mRegisters.set_Val(addrMode.Dst, Eval(System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Dst)), System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Src)), Operation.Add, addrMode.Size));
                clkCyc += 3;
            }
            else
            {
                if (addrMode.Direction == 0)
                {
                    set_RAMn(false, Eval(System.Convert.ToUInt32(addrMode.IndMem), System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Src)), Operation.Add, addrMode.Size));
                    clkCyc += 16;
                }
                else
                {
                    mRegisters.set_Val(addrMode.Dst, Eval(System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Dst)), System.Convert.ToUInt32(addrMode.IndMem), Operation.Add, addrMode.Size));
                    clkCyc += 9;
                }
            }
        }

        private void _04() // ADD AL Ib
        {
            mRegisters.AL = (byte)Eval(System.Convert.ToUInt32(mRegisters.AL), System.Convert.ToUInt32(Param(index: ParamIndex.First, size: DataSize.Byte)), Operation.Add, DataSize.Byte);
            clkCyc += 4;
        }

        private void _05() // ADD AX Iv
        {
            mRegisters.AX = Eval(System.Convert.ToUInt32(mRegisters.AX), System.Convert.ToUInt32(Param(index: ParamIndex.First, size: DataSize.Word)), Operation.Add, DataSize.Word);
            clkCyc += 4;
        }

        private void _06() // PUSH ES
        {
            PushIntoStack(mRegisters.ES);
            clkCyc += 10;
        }

        private void _07() // POP ES
        {
            mRegisters.ES = PopFromStack();
            ignoreINTs = true;
            clkCyc += 8;
        }

        private void _08_0B() // OR Gv Ev
        {
            SetAddressing();
            if (addrMode.IsDirect)
            {
                mRegisters.set_Val(addrMode.Dst, Eval(System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Dst)), System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Src)), Operation.LogicOr, addrMode.Size));
                clkCyc += 3;
            }
            else
            {
                if (addrMode.Direction == 0)
                {
                    set_RAMn(false, Eval(System.Convert.ToUInt32(addrMode.IndMem), System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Src)), Operation.LogicOr, addrMode.Size));
                    clkCyc += 16;
                }
                else
                {
                    mRegisters.set_Val(addrMode.Dst, Eval(System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Dst)), System.Convert.ToUInt32(addrMode.IndMem), Operation.LogicOr, addrMode.Size));
                    clkCyc += 9;
                }
            }
        }

        private void _0C() // OR AL Ib
        {
            mRegisters.AL = (byte)Eval(System.Convert.ToUInt32(mRegisters.AL), System.Convert.ToUInt32(Param(index: ParamIndex.First, size: DataSize.Byte)), Operation.LogicOr, DataSize.Byte);
            clkCyc += 4;
        }

        private void _0D() // OR AX Iv
        {
            mRegisters.AX = Eval(System.Convert.ToUInt32(mRegisters.AX), System.Convert.ToUInt32(Param(index: ParamIndex.First, size: DataSize.Word)), Operation.LogicOr, DataSize.Word);
            clkCyc += 4;
        }

        private void _0E() // PUSH CS
        {
            PushIntoStack(mRegisters.CS);
            clkCyc += 10;
        }

        private void _0F() // POP CS
        {
            if (!mVic20)
            {
                mRegisters.CS = PopFromStack();
                ignoreINTs = true;
                clkCyc += 8;
            }
        }

        private void _10_13() // ADC Gv Ev
        {
            SetAddressing();
            if (addrMode.IsDirect)
            {
                mRegisters.set_Val(addrMode.Dst, Eval(System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Dst)), System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Src)), Operation.AddWithCarry, addrMode.Size));
                clkCyc += 3;
            }
            else
            {
                if (addrMode.Direction == 0)
                {
                    set_RAMn(false, Eval(System.Convert.ToUInt32(addrMode.IndMem), System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Src)), Operation.AddWithCarry, addrMode.Size));
                    clkCyc += 16;
                }
                else
                {
                    mRegisters.set_Val(addrMode.Dst, Eval(System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Dst)), System.Convert.ToUInt32(addrMode.IndMem), Operation.AddWithCarry, addrMode.Size));
                    clkCyc += 9;
                }
            }
        }

        private void _14() // ADC AL Ib
        {
            mRegisters.AL = (byte)Eval(System.Convert.ToUInt32(mRegisters.AL), System.Convert.ToUInt32(Param(index: ParamIndex.First, size: DataSize.Byte)), Operation.AddWithCarry, DataSize.Byte);
            clkCyc += 3;
        }

        private void _15() // ADC AX Iv
        {
            mRegisters.AX = Eval(System.Convert.ToUInt32(mRegisters.AX), System.Convert.ToUInt32(Param(index: ParamIndex.First, size: DataSize.Word)), Operation.AddWithCarry, DataSize.Word);
            clkCyc += 3;
        }

        private void _16() // PUSH SS
        {
            PushIntoStack(mRegisters.SS);
            clkCyc += 10;
        }

        private void _17() // POP SS
        {
            mRegisters.SS = PopFromStack();
            // Lesson 4: http://ntsecurity.nu/onmymind/2007/2007-08-22.html
            // http://zet.aluzina.org/forums/viewtopic.php?f=6&t=287
            // http://www.vcfed.org/forum/archive/index.php/t-41453.html
            ignoreINTs = true;
            clkCyc += 8;
        }

        private void _18_1B() // SBB Gv Ev
        {
            SetAddressing();
            if (addrMode.IsDirect)
            {
                mRegisters.set_Val(addrMode.Dst, Eval(System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Dst)), System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Src)), Operation.SubstractWithCarry, addrMode.Size));
                clkCyc += 3;
            }
            else
            {
                if (addrMode.Direction == 0)
                {
                    set_RAMn(false, Eval(System.Convert.ToUInt32(addrMode.IndMem), System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Src)), Operation.SubstractWithCarry, addrMode.Size));
                    clkCyc += 16;
                }
                else
                {
                    mRegisters.set_Val(addrMode.Dst, Eval(System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Dst)), System.Convert.ToUInt32(addrMode.IndMem), Operation.SubstractWithCarry, addrMode.Size));
                    clkCyc += 9;
                }
            }
        }

        private void _1C() // SBB AL Ib
        {
            mRegisters.AL = (byte)Eval(System.Convert.ToUInt32(mRegisters.AL), System.Convert.ToUInt32(Param(index: ParamIndex.First, size: DataSize.Byte)), Operation.SubstractWithCarry, DataSize.Byte);
            clkCyc += 4;
        }

        private void _1D() // SBB AX Iv
        {
            mRegisters.AX = Eval(System.Convert.ToUInt32(mRegisters.AX), System.Convert.ToUInt32(Param(index: ParamIndex.First, size: DataSize.Word)), Operation.SubstractWithCarry, DataSize.Word);
            clkCyc += 4;
        }

        private void _1E() // PUSH DS
        {
            PushIntoStack(mRegisters.DS);
            clkCyc += 10;
        }

        private void _1F() // POP DS
        {
            mRegisters.DS = PopFromStack();
            ignoreINTs = true;
            clkCyc += 8;
        }

        private void _20_23() // AND Gv Ev
        {
            SetAddressing();
            if (addrMode.IsDirect)
            {
                mRegisters.set_Val(addrMode.Dst, Eval(System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Dst)), System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Src)), Operation.LogicAnd, addrMode.Size));
                clkCyc += 3;
            }
            else
            {
                if (addrMode.Direction == 0)
                {
                    set_RAMn(false, Eval(System.Convert.ToUInt32(addrMode.IndMem), System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Src)), Operation.LogicAnd, addrMode.Size));
                    clkCyc += 16;
                }
                else
                {
                    mRegisters.set_Val(addrMode.Dst, Eval(System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Dst)), System.Convert.ToUInt32(addrMode.IndMem), Operation.LogicAnd, addrMode.Size));
                    clkCyc += 9;
                }
            }
        }

        private void _24() // AND AL Ib
        {
            mRegisters.AL = (byte)Eval(System.Convert.ToUInt32(mRegisters.AL), System.Convert.ToUInt32(Param(index: ParamIndex.First, size: DataSize.Byte)), Operation.LogicAnd, DataSize.Byte);
            clkCyc += 4;
        }

        private void _25() // AND AX Iv
        {
            mRegisters.AX = Eval(System.Convert.ToUInt32(mRegisters.AX), System.Convert.ToUInt32(Param(index: ParamIndex.First, size: DataSize.Word)), Operation.LogicAnd, DataSize.Word);
            clkCyc += 4;
        }

        private void _26_2E_36_3E() // ES, CS, SS and DS segment override prefix
        {
            addrMode.Decode(opCode, opCode);
            mRegisters.ActiveSegmentRegister = addrMode.Dst - GPRegisters.RegistersTypes.AH + GPRegisters.RegistersTypes.ES;
            newPrefix = true;
            clkCyc += 2;
        }

        private void _27() // DAA
        {
            if (mRegisters.AL.LowNib() > 9 || mFlags.AF == 1)
            {
                tmpUVal = System.Convert.ToUInt32((mRegisters.AL) + 6);
                mRegisters.AL += (byte)6;
                mFlags.AF = (byte)1;
                mFlags.CF = (byte)(mFlags.CF | (((tmpUVal & 0xFF00) != 0) ? 1 : 0));
            }
            else
            {
                mFlags.AF = (byte)0;
            }
            if ((mRegisters.AL & 0xF0) > 0x90 || mFlags.CF == 1)
            {
                tmpUVal = System.Convert.ToUInt32((mRegisters.AL) + 0x60);
                mRegisters.AL += (byte)(0x60);
                mFlags.CF = (byte)1;
            }
            else
            {
                mFlags.CF = (byte)0;
            }
            SetSZPFlags(tmpUVal, DataSize.Byte);
            clkCyc += 4;
        }

        private void _28_2B() // SUB Gv Ev
        {
            SetAddressing();
            if (addrMode.IsDirect)
            {
                mRegisters.set_Val(addrMode.Dst, Eval(System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Dst)), System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Src)), Operation.Substract, addrMode.Size));
                clkCyc += 3;
            }
            else
            {
                if (addrMode.Direction == 0)
                {
                    set_RAMn(false, Eval(System.Convert.ToUInt32(addrMode.IndMem), System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Src)), Operation.Substract, addrMode.Size));
                    clkCyc += 16;
                }
                else
                {
                    mRegisters.set_Val(addrMode.Dst, Eval(System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Dst)), System.Convert.ToUInt32(addrMode.IndMem), Operation.Substract, addrMode.Size));
                    clkCyc += 9;
                }
            }
        }

        private void _2C() // SUB AL Ib
        {
            mRegisters.AL = (byte)Eval(System.Convert.ToUInt32(mRegisters.AL), System.Convert.ToUInt32(Param(index: ParamIndex.First, size: DataSize.Byte)), Operation.Substract, DataSize.Byte);
            clkCyc += 4;
        }

        private void _2D() // SUB AX, Iv
        {
            mRegisters.AX = Eval(System.Convert.ToUInt32(mRegisters.AX), System.Convert.ToUInt32(Param(index: ParamIndex.First, size: DataSize.Word)), Operation.Substract, DataSize.Word);
            clkCyc += 4;
        }

        private void _2F() // DAS
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
        }

        private void _30_33() // XOR Gv Ev
        {
            SetAddressing();
            if (addrMode.IsDirect)
            {
                mRegisters.set_Val(addrMode.Dst, Eval(System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Dst)), System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Src)), Operation.LogicXor, addrMode.Size));
                clkCyc += 3;
            }
            else
            {
                if (addrMode.Direction == 0)
                {
                    set_RAMn(false, Eval(System.Convert.ToUInt32(addrMode.IndMem), System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Src)), Operation.LogicXor, addrMode.Size));
                    clkCyc += 16;
                }
                else
                {
                    mRegisters.set_Val(addrMode.Dst, Eval(System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Dst)), System.Convert.ToUInt32(addrMode.IndMem), Operation.LogicXor, addrMode.Size));
                    clkCyc += 9;
                }
            }
        }

        private void _34() // XOR AL Ib
        {
            mRegisters.AL = (byte)Eval(System.Convert.ToUInt32(mRegisters.AL), System.Convert.ToUInt32(Param(index: ParamIndex.First, size: DataSize.Byte)), Operation.LogicXor, DataSize.Byte);
            clkCyc += 4;
        }

        private void _35() // XOR AX Iv
        {
            mRegisters.AX = Eval(System.Convert.ToUInt32(mRegisters.AX), System.Convert.ToUInt32(Param(index: ParamIndex.First, size: DataSize.Word)), Operation.LogicXor, DataSize.Word);
            clkCyc += 4;
        }

        private void _37() // AAA
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
            mRegisters.AL = System.Convert.ToByte(mRegisters.AL.LowNib());
            clkCyc += 8;
        }

        private void _38_3B() // CMP Gv Ev
        {
            SetAddressing();
            if (addrMode.IsDirect)
            {
                Eval(System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Dst)), System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Src)), Operation.Compare, addrMode.Size);
                clkCyc += 3;
            }
            else
            {
                if (addrMode.Direction == 0)
                {
                    Eval(System.Convert.ToUInt32(addrMode.IndMem), System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Src)), Operation.Compare, addrMode.Size);
                }
                else
                {
                    Eval(System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Dst)), System.Convert.ToUInt32(addrMode.IndMem), Operation.Compare, addrMode.Size);
                }
                clkCyc += 9;
            }
        }

        private void _3C() // CMP AL Ib
        {
            Eval(System.Convert.ToUInt32(mRegisters.AL), System.Convert.ToUInt32(Param(index: ParamIndex.First, size: DataSize.Byte)), Operation.Compare, DataSize.Byte);
            clkCyc += 4;
        }

        private void _3D() // CMP AX Iv
        {
            Eval(System.Convert.ToUInt32(mRegisters.AX), System.Convert.ToUInt32(Param(index: ParamIndex.First, size: DataSize.Word)), Operation.Compare, DataSize.Word);
            clkCyc += 4;
        }

        private void _3F() // AAS
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
            mRegisters.AL = System.Convert.ToByte(mRegisters.AL.LowNib());
            clkCyc += 8;
        }

        private void _40_47() // INC DI
        {
            SetRegister1Alt(opCode);
            mRegisters.set_Val(addrMode.Register1, Eval(System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Register1)), (uint)1, Operation.Increment, DataSize.Word));
            clkCyc += 3;
        }

        private void _48_4F() // DEC DI
        {
            SetRegister1Alt(opCode);
            mRegisters.set_Val(addrMode.Register1, Eval(System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Register1)), (uint)1, Operation.Decrement, DataSize.Word));
            clkCyc += 3;
        }

        private void _50_57() // PUSH DI
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
        }

        private void _58_5F() // POP DI
        {
            SetRegister1Alt(opCode);
            mRegisters.set_Val(addrMode.Register1, PopFromStack());
            clkCyc += 8;
        }

        private void _60() // PUSHA (80186)
        {
            if (mVic20)
            {
                tmpUVal = System.Convert.ToUInt32(mRegisters.SP);
                PushIntoStack(mRegisters.AX);
                PushIntoStack(mRegisters.CX);
                PushIntoStack(mRegisters.DX);
                PushIntoStack(mRegisters.BX);
                PushIntoStack(System.Convert.ToUInt16(tmpUVal));
                PushIntoStack(mRegisters.BP);
                PushIntoStack(mRegisters.SI);
                PushIntoStack(mRegisters.DI);
                clkCyc += 19;
            }
            else
            {
                OpCodeNotImplemented();
            }
        }

        private void _61() // POPA (80186)
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
        }

        private void _62() // BOUND (80186)
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
        }

        private void _68() // PUSH Iv (80186)
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
        }

        private void _69() // IMUL (80186)
        {
            if (mVic20)
            {
                // PRE ALPHA CODE - UNTESTED
                SetAddressing();
                uint tmp1 = System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Register1));
                uint tmp2 = System.Convert.ToUInt32(Param(index: ParamIndex.First, size: DataSize.Word));
                if ((tmp1 & 0x8000) == 0x8000)
                {
                    tmp1 = tmp1 | 0xFFFF_0000;
                }
                if ((tmp2 & 0x8000) == 0x8000)
                {
                    tmp2 = tmp2 | 0xFFFF_0000;
                }
                uint tmp3 = tmp1 * tmp2;
                mRegisters.set_Val(addrMode.Register1, (ushort)(tmp3 & 0xFFFF));
                if ((tmp3 & 0xFFFF_0000) != 0)
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
        }

        private void _6A() // PUSH Ib (80186)
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
        }

        private void _6B() // IMUL (80186)
        {
            if (mVic20)
            {
                // PRE ALPHA CODE - UNTESTED
                SetAddressing();
                uint tmp1 = System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Register1));
                uint tmp2 = System.Convert.ToUInt32(To16bitsWithSign(Param(index: ParamIndex.First, size: DataSize.Byte)));
                if ((tmp1 & 0x8000) == 0x8000)
                {
                    tmp1 = tmp1 | 0xFFFF_0000;
                }
                if ((tmp2 & 0x8000) == 0x8000)
                {
                    tmp2 = tmp2 | 0xFFFF_0000;
                }
                uint tmp3 = tmp1 * tmp2;
                mRegisters.set_Val(addrMode.Register1, (ushort)(tmp3 & 0xFFFF));
                if ((tmp3 & 0xFFFF_0000) != 0)
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
        }

        private void _6C_6F() // Ignore 80186/V20 port operations... for now...
        {
            opCodeSize++;
            clkCyc += 3;
        }

        private void _70() // JO Jb
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
        }

        private void _71() // JNO  Jb
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
        }

        private void _72() // JB/JNAE/JC Jb
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
        }

        private void _73() // JNB/JAE/JNC Jb
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
        }

        private void _74() // JZ/JE Jb
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
        }

        private void _75() // JNZ/JNE Jb
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
        }

        private void _76() // JBE/JNA Jb
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
        }

        private void _77() // JA/JNBE Jb
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
        }

        private void _78() // JS Jb
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
        }

        private void _79() // JNS Jb
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
        }

        private void _7A() // JPE/JP Jb
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
        }

        private void _7B() // JPO/JNP Jb
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
        }

        private void _7C() // JL/JNGE Jb
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
        }

        private void _7D() // JGE/JNL Jb
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
        }

        private void _7E() // JLE/JNG Jb
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
        }

        private void _7F() // JG/JNLE Jb
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

        private void _80_83()
        {
            ExecuteGroup1();
        }

        private void _84_85() // TEST Gv Ev
        {
            SetAddressing();
            if (addrMode.IsDirect)
            {
                Eval(System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Dst)), System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Src)), Operation.Test, addrMode.Size);
                clkCyc += 3;
            }
            else
            {
                Eval(System.Convert.ToUInt32(addrMode.IndMem), System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Dst)), Operation.Test, addrMode.Size);
                clkCyc += 9;
            }
        }

        private void _86_87() // XCHG Gv Ev
        {
            SetAddressing();
            if (addrMode.IsDirect)
            {
                tmpUVal = System.Convert.ToUInt32(mRegisters.get_Val(addrMode.Dst));
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
        }

        private void _88_8B() // MOV Gv Ev
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
        }

        private void _8C() // MOV Ew Sw
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
        }

        private void _8D() // LEA Gv M
        {
            SetAddressing();
            mRegisters.set_Val(addrMode.Src, addrMode.IndAdr);
            clkCyc += 2;
        }

        private void _8E() // MOV Sw Ew
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
        }

        private void _8F() // POP Ev
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
        }

        private void _90() // NOP
        {
            clkCyc += 3;
        }

        private void _91() // XCHG CX AX
        {
            tmpUVal = System.Convert.ToUInt32(mRegisters.AX);
            mRegisters.AX = mRegisters.CX;
            mRegisters.CX = System.Convert.ToUInt16(tmpUVal);
            clkCyc += 3;
        }

        private void _92() // XCHG DX AX
        {
            tmpUVal = System.Convert.ToUInt32(mRegisters.AX);
            mRegisters.AX = mRegisters.DX;
            mRegisters.DX = System.Convert.ToUInt16(tmpUVal);
            clkCyc += 3;
        }

        private void _93() // XCHG BX AX
        {
            tmpUVal = System.Convert.ToUInt32(mRegisters.AX);
            mRegisters.AX = mRegisters.BX;
            mRegisters.BX = System.Convert.ToUInt16(tmpUVal);
            clkCyc += 3;
        }

        private void _94() // XCHG SP AX
        {
            tmpUVal = System.Convert.ToUInt32(mRegisters.AX);
            mRegisters.AX = mRegisters.SP;
            mRegisters.SP = System.Convert.ToUInt16(tmpUVal);
            clkCyc += 3;
        }

        private void _95() // XCHG BP AX
        {
            tmpUVal = System.Convert.ToUInt32(mRegisters.AX);
            mRegisters.AX = mRegisters.BP;
            mRegisters.BP = System.Convert.ToUInt16(tmpUVal);
            clkCyc += 3;
        }

        private void _96() // XCHG SI AX
        {
            tmpUVal = System.Convert.ToUInt32(mRegisters.AX);
            mRegisters.AX = mRegisters.SI;
            mRegisters.SI = System.Convert.ToUInt16(tmpUVal);
            clkCyc += 3;
        }

        private void _97() // XCHG DI AX
        {
            tmpUVal = System.Convert.ToUInt32(mRegisters.AX);
            mRegisters.AX = mRegisters.DI;
            mRegisters.DI = System.Convert.ToUInt16(tmpUVal);
            clkCyc += 3;
        }

        private void _98() // CBW
        {
            mRegisters.AX = To16bitsWithSign(System.Convert.ToUInt16(mRegisters.AL));
            clkCyc += 2;
        }

        private void _99() // CWD
        {
            mRegisters.DX = System.Convert.ToUInt16(((mRegisters.AH & 0x80) != 0) ? 0xFFFF : 0x0);
            clkCyc += 5;
        }

        private void _9A() // CALL Ap
        {
            IPAddrOffet = Param(index: ParamIndex.First, size: DataSize.Word);
            tmpUVal = System.Convert.ToUInt32(Param(index: ParamIndex.Second, size: DataSize.Word));
            PushIntoStack(mRegisters.CS);
            PushIntoStack((ushort)(mRegisters.IP + opCodeSize));
            mRegisters.CS = System.Convert.ToUInt16(tmpUVal);
            clkCyc += 28;
        }

        private void _9B() // WAIT
        {
            clkCyc += 4;
        }

        private void _9C() // PUSHF
        {
            PushIntoStack(System.Convert.ToUInt16((mModel == Models.IBMPC_5150 ? 0xFFF : 0xFFFF) & mFlags.EFlags));
            clkCyc += 10;
        }

        private void _9D() // POPF
        {
            mFlags.EFlags = PopFromStack();
            clkCyc += 8;
        }

        private void _9E() // SAHF
        {
            mFlags.EFlags = (ushort)((mFlags.EFlags & 0xFF00) | mRegisters.AH);
            clkCyc += 4;
        }

        private void _9F() // LAHF
        {
            mRegisters.AH = System.Convert.ToByte(mFlags.EFlags);
            clkCyc += 4;
        }

        private void _A0() // MOV AL Ob
        {
            mRegisters.AL = get_RAM8(System.Convert.ToUInt16(mRegisters.ActiveSegmentValue), Param(index: ParamIndex.First, size: DataSize.Word), (byte)0, false); //
            clkCyc += 10;
        }

        private void _A1() // MOV AX Ov
        {
            mRegisters.AX = get_RAM16(System.Convert.ToUInt16(mRegisters.ActiveSegmentValue), Param(index: ParamIndex.First, size: DataSize.Word), (byte)0, false); //
            clkCyc += 10;
        }

        private void _A2() // MOV Ob AL
        {
            set_RAM8(System.Convert.ToUInt16(mRegisters.ActiveSegmentValue), Param(index: ParamIndex.First, size: DataSize.Word), (byte)0, false, mRegisters.AL); //
            clkCyc += 10;
        }

        private void _A3() // MOV Ov AX
        {
            set_RAM16(System.Convert.ToUInt16(mRegisters.ActiveSegmentValue), Param(index: ParamIndex.First, size: DataSize.Word), (byte)0, false, mRegisters.AX); //
            clkCyc += 10;
        }

        private void _A4_A7()
        {
            HandleREPMode();
        }

        private void _AA_AF()
        {
            HandleREPMode();
        }

        private void _A8() // TEST AL Ib
        {
            Eval(System.Convert.ToUInt32(mRegisters.AL), System.Convert.ToUInt32(Param(index: ParamIndex.First, size: DataSize.Byte)), Operation.Test, DataSize.Byte);
            clkCyc += 4;
        }

        private void _A9() // TEST AX Iv
        {
            Eval(System.Convert.ToUInt32(mRegisters.AX), System.Convert.ToUInt32(Param(index: ParamIndex.First, size: DataSize.Word)), Operation.Test, DataSize.Word);
            clkCyc += 4;
        }

        private void _B0() // MOV AL Ib
        {
            mRegisters.AL = (byte)Param(index: ParamIndex.First, size: DataSize.Byte); //
            clkCyc += 4;
        }

        private void _B1() // MOV CL Ib
        {
            mRegisters.CL = (byte)Param(index: ParamIndex.First, size: DataSize.Byte); //
            clkCyc += 4;
        }

        private void _B2() // MOV DL Ib
        {
            mRegisters.DL = (byte)Param(index: ParamIndex.First, size: DataSize.Byte); //
            clkCyc += 4;
        }

        private void _B3() // MOV BL Ib
        {
            mRegisters.BL = (byte)Param(index: ParamIndex.First, size: DataSize.Byte); //
            clkCyc += 4;
        }

        private void _B4() // MOV AH Ib
        {
            mRegisters.AH = (byte)Param(index: ParamIndex.First, size: DataSize.Byte); //
            clkCyc += 4;
        }

        private void _B5() // MOV CH Ib
        {
            mRegisters.CH = (byte)Param(index: ParamIndex.First, size: DataSize.Byte); //
            clkCyc += 4;
        }

        private void _B6() // MOV DH Ib
        {
            mRegisters.DH = (byte)Param(index: ParamIndex.First, size: DataSize.Byte); //
            clkCyc += 4;
        }

        private void _B7() // MOV BH Ib
        {
            mRegisters.BH = (byte)Param(index: ParamIndex.First, size: DataSize.Byte); //
            clkCyc += 4;
        }

        private void _B8() // MOV AX Ib
        {
            mRegisters.AX = Param(index: ParamIndex.First, size: DataSize.Word); //
            clkCyc += 4;
        }

        private void _B9() // MOV CX Ib
        {
            mRegisters.CX = Param(index: ParamIndex.First, size: DataSize.Word); //
            clkCyc += 4;
        }

        private void _BA() // MOV DX Ib
        {
            mRegisters.DX = Param(index: ParamIndex.First, size: DataSize.Word); //
            clkCyc += 4;
        }

        private void _BB() // MOV BX Ib
        {
            mRegisters.BX = Param(index: ParamIndex.First, size: DataSize.Word); //
            clkCyc += 4;
        }

        private void _BC() // MOV SP Ib
        {
            mRegisters.SP = Param(index: ParamIndex.First, size: DataSize.Word); //
            clkCyc += 4;
        }

        private void _BD() // MOV BP Ib
        {
            mRegisters.BP = Param(index: ParamIndex.First, size: DataSize.Word); //
            clkCyc += 4;
        }

        private void _BE() // MOV SI Ib
        {
            mRegisters.SI = Param(index: ParamIndex.First, size: DataSize.Word); //
            clkCyc += 4;
        }

        private void _BF() // MOV DI Ib
        {
            mRegisters.DI = Param(index: ParamIndex.First, size: DataSize.Word); //
            clkCyc += 4;
        }

        private void _C0_C1() // GRP2 byte/word imm8/16 ??? (80186)
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
        }

        private void _C2() // RET Iw
        {
            IPAddrOffet = PopFromStack();
            mRegisters.SP += Param(index: ParamIndex.First, size: DataSize.Word);
            clkCyc += 20;
        }

        private void _C3() // RET
        {
            IPAddrOffet = PopFromStack();
            clkCyc += 16;
        }

        private void _C4_C5() // LES / LDS Gv Mp
        {
            SetAddressing(DataSize.Word);
            if (((int)(addrMode.Register1) & shl2) == shl2)
            {
                addrMode.Register1 += (int)GPRegisters.RegistersTypes.ES;
            }
            //mRegisters.set_Val((int) (addrMode.Register1) | shl3, addrMode.IndMem);
            mRegisters.set_Val((addrMode.Register1) | GPRegisters.RegistersTypes.AX, addrMode.IndMem);
            mRegisters.set_Val(opCode == 0xC4 ? GPRegisters.RegistersTypes.ES : GPRegisters.RegistersTypes.DS, get_RAM16(System.Convert.ToUInt16(mRegisters.ActiveSegmentValue), addrMode.IndAdr, (byte)2, false));
            ignoreINTs = true;
            clkCyc += 16;
        }

        private void _C6_C7() // MOV MOV Ev Iv
        {
            SetAddressing();
            if (addrMode.IsDirect)
            {
                mRegisters.set_Val(addrMode.Src, Param(ParamIndex.First, System.Convert.ToUInt16(opCodeSize)));
            }
            else
            {
                set_RAMn(false, Param(ParamIndex.First, System.Convert.ToUInt16(opCodeSize)));
            }
            clkCyc += 10;
        }

        private void _C8() // ENTER (80186)
        {
            if (mVic20)
            {
                // PRE ALPHA CODE - UNTESTED
                ushort stackSize = Param(index: ParamIndex.First, size: DataSize.Word);
                ushort nestLevel = System.Convert.ToUInt16(Param(index: ParamIndex.Second, size: DataSize.Byte) & 0x1F);
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
                    case (ushort)0: //
                        clkCyc += 15;
                        break;
                    case (ushort)1: //
                        clkCyc += 25;
                        break;
                    default: //
                        clkCyc += 22 + 16 * (nestLevel - 1);
                        break;
                }
            }
            else
            {
                OpCodeNotImplemented();
            }
        }

        private void _C9() // LEAVE (80186)
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
        }

        private void _CA() // RETF Iw
        {
            tmpUVal = System.Convert.ToUInt32(Param(index: ParamIndex.First, size: DataSize.Word));
            IPAddrOffet = PopFromStack();
            mRegisters.CS = PopFromStack();
            mRegisters.SP += System.Convert.ToUInt16(tmpUVal);
            clkCyc += 17;
        }

        private void _CB() // RETF
        {
            IPAddrOffet = PopFromStack();
            mRegisters.CS = PopFromStack();
            clkCyc += 18;
        }

        private void _CC() // INT 3
        {
            HandleInterrupt((byte)3, false);
            clkCyc++;
        }

        private void _CD() // INT Ib
        {
            HandleInterrupt((byte)Param(index: ParamIndex.First, size: DataSize.Byte), false);
            clkCyc += 0;
        }

        private void _CE() // INTO
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
        }

        private void _CF() // IRET
        {
            IPAddrOffet = PopFromStack();
            mRegisters.CS = PopFromStack();
            mFlags.EFlags = PopFromStack();
            clkCyc += 32;
        }

        private void _D0_D3()
        {
            ExecuteGroup2();
        }

        private void _D4() // AAM I0
        {
            tmpUVal = System.Convert.ToUInt32(Param(index: ParamIndex.First, size: DataSize.Byte));
            if (tmpUVal == 0)
            {
                HandleInterrupt((byte)0, true);
                return;
            }
            mRegisters.AH = (byte)(mRegisters.AL / tmpUVal);
            mRegisters.AL = (byte)(mRegisters.AL % tmpUVal);
            SetSZPFlags(System.Convert.ToUInt32(mRegisters.AX), DataSize.Word);
            clkCyc += 83;
        }

        private void _D5() // AAD I0
        {
            tmpUVal = System.Convert.ToUInt32(Param(index: ParamIndex.First, size: DataSize.Byte));
            tmpUVal = tmpUVal * mRegisters.AH + mRegisters.AL;
            mRegisters.AL = (byte)(tmpUVal);
            mRegisters.AH = (byte)0;
            SetSZPFlags(tmpUVal, DataSize.Word);
            mFlags.SF = (byte)0;
            clkCyc += 60;
        }

        private void _D6() // XLAT for V20 / SALC
        {
            if (mVic20)
            {
                mRegisters.AL = get_RAM8(System.Convert.ToUInt16(mRegisters.ActiveSegmentValue), (ushort)(mRegisters.BX + mRegisters.AL), (byte)0, false);
            }
            else
            {
                mRegisters.AL = (byte)(mFlags.CF == 1 ? 0xFF : 0x0);
                clkCyc += 4;
            }
        }

        private void _D7() // XLATB
        {
            mRegisters.AL = get_RAM8(System.Convert.ToUInt16(mRegisters.ActiveSegmentValue), (ushort)(mRegisters.BX + mRegisters.AL), (byte)0, false);
            clkCyc += 11;
        }

        private void _D8_DF() // Ignore 8087 co-processor instructions
        {
            SetAddressing();
            //FPU.Execute(opCode, addrMode)

            // Lesson 2
            // http://ntsecurity.nu/onmymind/2007/2007-08-22.html

            //HandleInterrupt(7, False)
            OpCodeNotImplemented("FPU Not Available");
            clkCyc += 2;
        }

        private void _E0() // LOOPNE/LOOPNZ
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
        }

        private void _E1() // LOOPE/LOOPZ
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
        }

        private void _E2() // LOOP
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
        }

        private void _E3() // JCXZ/JECXZ
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
        }

        private void _E4() // IN AL Ib
        {
            mRegisters.AL = (byte)(ReceiveFromPort(System.Convert.ToUInt32(Param(index: ParamIndex.First, size: DataSize.Byte))));
            clkCyc += 10;
        }

        private void _E5() // IN AX Ib
        {
            mRegisters.AX = System.Convert.ToUInt16(ReceiveFromPort(System.Convert.ToUInt32(Param(index: ParamIndex.First, size: DataSize.Byte))));
            clkCyc += 10;
        }

        private void _E6() // OUT Ib AL
        {
            SendToPort(System.Convert.ToUInt32(Param(index: ParamIndex.First, size: DataSize.Byte)), System.Convert.ToUInt32(mRegisters.AL));
            clkCyc += 10;
        }

        private void _E7() // OUT Ib AX
        {
            SendToPort(System.Convert.ToUInt32(Param(index: ParamIndex.First, size: DataSize.Byte)), System.Convert.ToUInt32(mRegisters.AX));
            clkCyc += 10;
        }

        private void _E8() // CALL Jv
        {
            IPAddrOffet = OffsetIP(DataSize.Word);
            PushIntoStack((ushort)(Registers.IP + opCodeSize));
            clkCyc += 19;
        }

        private void _E9() // JMP Jv
        {
            IPAddrOffet = OffsetIP(DataSize.Word);
            clkCyc += 15;
        }

        private void _EA() // JMP Ap
        {
            IPAddrOffet = Param(index: ParamIndex.First, size: DataSize.Word);
            mRegisters.CS = Param(index: ParamIndex.Second, size: DataSize.Word);
            clkCyc += 15;
        }

        private void _EB() // JMP Jb
        {
            IPAddrOffet = OffsetIP(DataSize.Byte);
            clkCyc += 15;
        }

        private void _EC() // IN AL DX
        {
            mRegisters.AL = (byte)(ReceiveFromPort(System.Convert.ToUInt32(mRegisters.DX)));
            clkCyc += 8;
        }

        private void _ED() // IN AX DX
        {
            mRegisters.AX = System.Convert.ToUInt16(ReceiveFromPort(System.Convert.ToUInt32(mRegisters.DX)));
            clkCyc += 8;
        }

        private void _EE() // OUT DX AL
        {
            SendToPort(System.Convert.ToUInt32(mRegisters.DX), System.Convert.ToUInt32(mRegisters.AL));
            clkCyc += 8;
        }

        private void _EF() // OUT DX AX
        {
            SendToPort(System.Convert.ToUInt32(mRegisters.DX), System.Convert.ToUInt32(mRegisters.AX));
            clkCyc += 8;
        }

        private void _F0() // LOCK
        {
            OpCodeNotImplemented("LOCK");
            clkCyc += 2;
        }

        private void _F2() // REPBE/REPNZ
        {
            mRepeLoopMode = REPLoopModes.REPENE;
            newPrefix = true;
            clkCyc += 2;
        }

        private void _F3() // repe/repz
        {
            mRepeLoopMode = REPLoopModes.REPE;
            newPrefix = true;
            clkCyc += 2;
        }

        private void _F4() // HLT
        {
            if (!mIsHalted)
            {
                SystemHalted();
            }
            mRegisters.IP--;
            clkCyc += 2;
        }

        private void _F5() // CMC
        {
            mFlags.CF = (byte)(mFlags.CF == 0 ? 1 : 0);
            clkCyc += 2;
        }

        private void _F6_F7()
        {
            ExecuteGroup3();
        }

        private void _F8() // CLC
        {
            mFlags.CF = (byte)0;
            clkCyc += 2;
        }

        private void _F9() // STC
        {
            mFlags.CF = (byte)1;
            clkCyc += 2;
        }

        private void _FA() // CLI
        {
            mFlags.IF = (byte)0;
            clkCyc += 2;
        }

        private void _FB() // STI
        {
            mFlags.IF = (byte)1;
            ignoreINTs = true; // http://zet.aluzina.org/forums/viewtopic.php?f=6&t=287
            clkCyc += 2;
        }

        private void _FC() // CLD
        {
            mFlags.DF = (byte)0;
            clkCyc += 2;
        }

        private void _FD() // STD
        {
            mFlags.DF = (byte)1;
            clkCyc += 2;
        }

        private void _FE_FF()
        {
            ExecuteGroup4_And_5();
        }


    }
}
