using System.Collections.Generic;
using System;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Xml.Linq;
using System.Collections;

using System.Threading;

using x8086SharpEmu;

namespace x8086SharpEmu
{
    public partial class X8086
    {
        public struct Instruction
        {
            public byte OpCode;
            public string Mnemonic;
            public string Parameter1;
            public string Parameter2;
            public byte Size;
            public int CS;
            public int IP;
            public string Message;
            public int JumpAddress;
            public int IndMemoryData;
            public int IndAddress;
            public byte[] Bytes;
            public bool IsValid;
            public byte ClockCycles;
            public string SegmentOverride;

            private string str;
            private string strFull;

            public string ToString(bool includeOpCode)
            {
                if (string.IsNullOrEmpty(str))
                {
                    string s1 = "";

                    if (includeOpCode)
                    {
                        string r = "";
                        if (Bytes != null)
                        {
                            for (int i = 0; i <= Bytes.Length - 1; i++)
                            {
                                r += Bytes[i].ToString("X") + " ";
                            }
                        }

                        s1 = string.Format("{0}:{1} {2} {3}", CS.ToString("X4"),
                            IP.ToString("X4"),
                            r.PadRight(6 * 3),
                            Mnemonic.PadRight(6, ' '));
                    }
                    else
                    {
                        s1 = string.Format("{0}:{1} {2}", CS.ToString("X4"),
                            IP.ToString("X4"),
                            Mnemonic.PadRight(6, ' '));
                    }

                    if (!string.IsNullOrEmpty(Parameter1))
                    {
                        if (!string.IsNullOrEmpty(Parameter2))
                        {
                            s1 += string.Format("{0}, {1}", Parameter1, Parameter2);
                        }
                        else
                        {
                            s1 += Parameter1;
                        }
                    }
                    return s1;
                }
                else
                {
                    return str;
                }
            }

            public override string ToString()
            {
                return ToString(false);
            }

            public override bool Equals(object obj)
            {
                if (!(obj is Instruction))
                {
                    return false;
                }
                return this == ((Instruction)obj);
            }

            public static bool operator ==(Instruction i1, Instruction i2)
            {
                if (i1.Size == i2.Size && (i1.IP == i2.IP) && (i1.CS == i2.CS))
                {
                    for (int i = 0; i <= i1.Size - 1; i++)
                    {
                        if (i1.Bytes[i] != i2.Bytes[i])
                        {
                            return false;
                        }
                    }
                    return true;
                }
                else
                {
                    return false;
                }
            }

            public static bool operator !=(Instruction i1, Instruction i2)
            {
                return !(i1 == i2);
            }
        }

        private string indASM;
        private string opCodeASM;
        private string segOvr = "";
        private byte decoderClkCyc;
        private int decoderIPAddrOff;
        private AddressingMode decoderAddrMode;
        private object decoderSyncObj = new object();

        private Instruction InvalidOpCode()
        {
            var inst = new Instruction
            {
                Mnemonic = "",
                IsValid = false
            };
            return inst;
        }

        public Instruction Decode(bool force = false)
        {
            return Decode(mRegisters.CS, mRegisters.IP);
        }

        public Instruction Decode(ushort segment, ushort offset, bool force = false)
        {
            ushort cs = mRegisters.CS;
            ushort ip = mRegisters.IP;
            bool asc = mRegisters.ActiveSegmentChanged;
            GPRegisters.RegistersTypes asr = mRegisters.ActiveSegmentRegister;
            Instruction ins = new Instruction();

            mRegisters.CS = segment;
            mRegisters.IP = offset;

            if (force)
            {
                ins = DoDecode();
            }
            else
            {
                lock (decoderSyncObj)
                {
                    ins = DoDecode();
                }
            }

            mRegisters.CS = cs;
            mRegisters.IP = ip;

            if (asc)
            {
                mRegisters.ActiveSegmentRegister = asr;
            }
            else
            {
                mRegisters.ResetActiveSegment();
            }

            return ins;
        }

        private Instruction DoDecode()
        {
            newPrefix = false;
            opCodeSize = (byte)1;
            decoderIPAddrOff = 0;
            opCodeASM = "";
            decoderClkCyc = (byte)0;

            opCode = get_RAM8(mRegisters.CS, mRegisters.IP);

            if (opCode >= 0x0 && opCode <= 0x3) // add
            {
                SetDecoderAddressing();
                if (decoderAddrMode.IsDirect)
                {
                    if (decoderAddrMode.Direction == 0)
                    {
                        opCodeASM = "ADD " + decoderAddrMode.Register2.ToString() + ", " + decoderAddrMode.Register1.ToString();
                    }
                    else
                    {
                        opCodeASM = "ADD " + decoderAddrMode.Register1.ToString() + ", " + decoderAddrMode.Register2.ToString();
                    }
                    decoderClkCyc += (byte)3;
                }
                else
                {
                    if (decoderAddrMode.Direction == 0)
                    {
                        opCodeASM = "ADD " + indASM + ", " + decoderAddrMode.Register1.ToString();
                        decoderClkCyc += (byte)16;
                    }
                    else
                    {
                        opCodeASM = "ADD " + decoderAddrMode.Register1.ToString() + ", " + indASM;
                        decoderClkCyc += (byte)9;
                    }
                }
            } // add al and imm
            else if (opCode == ((byte)(0x4)))
            {
                opCodeASM = "ADD AL, " + DecoderParam(index: ParamIndex.First, size: DataSize.Byte).ToString("X2");
                decoderClkCyc += (byte)4;
            } // add ax and imm
            else if (opCode == ((byte)(0x5)))
            {
                opCodeASM = "ADD AX, " + DecoderParam(index: ParamIndex.First, size: DataSize.Word).ToString("X4");
                decoderClkCyc += (byte)4;
            } // push es
            else if (opCode == ((byte)(0x6)))
            {
                opCodeASM = "PUSH ES";
                decoderClkCyc += (byte)10;
            } // pop es
            else if (opCode == ((byte)(0x7)))
            {
                opCodeASM = "POP ES";
                decoderClkCyc += (byte)8;
            } // or
            else if (opCode >= 0x8 && opCode <= 0xB)
            {
                SetDecoderAddressing();
                if (decoderAddrMode.IsDirect)
                {
                    if (decoderAddrMode.Direction == 0)
                    {
                        opCodeASM = "OR " + decoderAddrMode.Register2.ToString() + ", " + decoderAddrMode.Register1.ToString();
                    }
                    else
                    {
                        opCodeASM = "OR " + decoderAddrMode.Register1.ToString() + ", " + decoderAddrMode.Register2.ToString();
                    }
                    decoderClkCyc += (byte)3;
                }
                else
                {
                    if (decoderAddrMode.Direction == 0)
                    {
                        opCodeASM = "OR " + indASM + ", " + decoderAddrMode.Register1.ToString();
                        decoderClkCyc += (byte)16;
                    }
                    else
                    {
                        opCodeASM = "OR " + decoderAddrMode.Register1.ToString() + ", " + indASM;
                        decoderClkCyc += (byte)9;
                    }
                }
            } // or al and imm
            else if (opCode == ((byte)(0xC)))
            {
                opCodeASM = "OR AL, " + DecoderParam(index: ParamIndex.First, size: DataSize.Byte).ToString("X2");
                decoderClkCyc += (byte)4;
            } // or ax and imm
            else if (opCode == ((byte)(0xD)))
            {
                opCodeASM = "OR AX, " + DecoderParam(index: ParamIndex.First, size: DataSize.Word).ToString("X4");
                decoderClkCyc += (byte)4;
            } // push cs
            else if (opCode == ((byte)(0xE)))
            {
                opCodeASM = "PUSH CS";
                decoderClkCyc += (byte)10;
            } // adc
            else if (opCode >= 0x10 && opCode <= 0x13)
            {
                SetDecoderAddressing();
                if (decoderAddrMode.IsDirect)
                {
                    if (decoderAddrMode.Direction == 0)
                    {
                        opCodeASM = "ADC " + decoderAddrMode.Register2.ToString() + ", " + decoderAddrMode.Register1.ToString();
                    }
                    else
                    {
                        opCodeASM = "ADC " + decoderAddrMode.Register1.ToString() + ", " + decoderAddrMode.Register2.ToString();
                    }
                    decoderClkCyc += (byte)3;
                }
                else
                {
                    if (decoderAddrMode.Direction == 0)
                    {
                        opCodeASM = "ADC " + indASM + ", " + decoderAddrMode.Register1.ToString();
                        decoderClkCyc += (byte)16;
                    }
                    else
                    {
                        opCodeASM = "ADC " + decoderAddrMode.Register1.ToString() + ", " + indASM;
                        decoderClkCyc += (byte)9;
                    }
                }
            } // adc al and imm
            else if (opCode == ((byte)(0x14)))
            {
                opCodeASM = "ADC AL, " + DecoderParam(index: ParamIndex.First, size: DataSize.Byte).ToString("X2");
                decoderClkCyc += (byte)3;
            } // adc ax and imm
            else if (opCode == ((byte)(0x15)))
            {
                opCodeASM = "ADC AX, " + DecoderParam(index: ParamIndex.First, size: DataSize.Word).ToString("X4");
                decoderClkCyc += (byte)3;
            } // push ss
            else if (opCode == ((byte)(0x16)))
            {
                opCodeASM = "PUSH SS";
                decoderClkCyc += (byte)10;
            } // pop ss
            else if (opCode == ((byte)(0x17)))
            {
                opCodeASM = "POP SS";
                decoderClkCyc += (byte)8;
            } // sbb
            else if (opCode >= 0x18 && opCode <= 0x1B)
            {
                SetDecoderAddressing();
                if (decoderAddrMode.IsDirect)
                {
                    if (decoderAddrMode.Direction == 0)
                    {
                        opCodeASM = "SBB " + decoderAddrMode.Register2.ToString() + ", " + decoderAddrMode.Register1.ToString();
                    }
                    else
                    {
                        opCodeASM = "SBB " + decoderAddrMode.Register1.ToString() + ", " + decoderAddrMode.Register2.ToString();
                    }
                    decoderClkCyc += (byte)3;
                }
                else
                {
                    if (decoderAddrMode.Direction == 0)
                    {
                        opCodeASM = "SBB " + indASM + ", " + decoderAddrMode.Register1.ToString();
                        decoderClkCyc += (byte)16;
                    }
                    else
                    {
                        opCodeASM = "SBB " + decoderAddrMode.Register1.ToString() + ", " + indASM;
                        decoderClkCyc += (byte)3;
                    }
                }
            } // sbb al and imm
            else if (opCode == ((byte)(0x1C)))
            {
                opCodeASM = "SBB AL, " + DecoderParam(index: ParamIndex.First, size: DataSize.Byte).ToString("X2");
                decoderClkCyc += (byte)4;
            } // sbb ax and imm
            else if (opCode == ((byte)(0x1D)))
            {
                opCodeASM = "SBB AX, " + DecoderParam(index: ParamIndex.First, size: DataSize.Word).ToString("X4");
                decoderClkCyc += (byte)4;
            } // push ds
            else if (opCode == ((byte)(0x1E)))
            {
                opCodeASM = "PUSH DS";
                decoderClkCyc += (byte)10;
            } // pop ds
            else if (opCode == ((byte)(0x1F)))
            {
                opCodeASM = "POP DS";

                decoderClkCyc += (byte)8;
            } // and reg/mem and reg to either | and imm to acc
            else if (opCode >= 0x20 && opCode <= 0x23)
            {
                SetDecoderAddressing();
                if (decoderAddrMode.IsDirect)
                {
                    if (decoderAddrMode.Direction == 0)
                    {
                        opCodeASM = "AND " + decoderAddrMode.Register2.ToString() + ", " + decoderAddrMode.Register1.ToString();
                    }
                    else
                    {
                        opCodeASM = "AND " + decoderAddrMode.Register1.ToString() + ", " + decoderAddrMode.Register2.ToString();
                    }
                    decoderClkCyc += (byte)3;
                }
                else
                {
                    if (decoderAddrMode.Direction == 0)
                    {
                        opCodeASM = "AND " + indASM + ", " + decoderAddrMode.Register1.ToString();
                        decoderClkCyc += (byte)16;
                    }
                    else
                    {
                        opCodeASM = "AND " + decoderAddrMode.Register1.ToString() + ", " + indASM;
                        decoderClkCyc += (byte)9;
                    }
                }
            } // and al and imm
            else if (opCode == ((byte)(0x24)))
            {
                opCodeASM = "AND AL, " + DecoderParam(index: ParamIndex.First, size: DataSize.Byte).ToString("X2");
                decoderClkCyc += (byte)4;
            } // and ax and imm
            else if (opCode == ((byte)(0x25)))
            {
                opCodeASM = "AND AX, " + DecoderParam(index: ParamIndex.First, size: DataSize.Word).ToString("X4");
                decoderClkCyc += (byte)4;
            } // daa
            else if (opCode == ((byte)(0x27)))
            {
                opCodeASM = "DAA";
                decoderClkCyc += (byte)4;
            } // sub reg/mem with reg to either | sub imm from acc
            else if (opCode >= 0x28 && opCode <= 0x2B)
            {
                SetDecoderAddressing();
                if (decoderAddrMode.IsDirect)
                {
                    if (decoderAddrMode.Direction == 0)
                    {
                        opCodeASM = "SUB " + decoderAddrMode.Register2.ToString() + ", " + decoderAddrMode.Register1.ToString();
                    }
                    else
                    {
                        opCodeASM = "SUB " + decoderAddrMode.Register1.ToString() + ", " + decoderAddrMode.Register2.ToString();
                    }
                    decoderClkCyc += (byte)3;
                }
                else
                {
                    if (decoderAddrMode.Direction == 0)
                    {
                        opCodeASM = "SUB " + indASM + ", " + decoderAddrMode.Register1.ToString();
                        decoderClkCyc += (byte)16;
                    }
                    else
                    {
                        opCodeASM = "SUB " + decoderAddrMode.Register1.ToString() + ", " + indASM;
                        decoderClkCyc += (byte)9;
                    }
                }
            } // sub al and imm
            else if (opCode == ((byte)(0x2C)))
            {
                opCodeASM = "SUB AL, " + DecoderParam(index: ParamIndex.First, size: DataSize.Byte).ToString("X2");
                decoderClkCyc += (byte)4;
            } // sub ax and imm
            else if (opCode == ((byte)(0x2D)))
            {
                opCodeASM = "SUB AX, " + DecoderParam(index: ParamIndex.First, size: DataSize.Word).ToString("X4");
                decoderClkCyc += (byte)4;
            } // das
            else if (opCode == ((byte)(0x2F)))
            {
                opCodeASM = "DAS";
                decoderClkCyc += (byte)4;
            } // xor reg/mem and reg to either | xor imm to acc
            else if (opCode >= 0x30 && opCode <= 0x33)
            {
                SetDecoderAddressing();
                if (decoderAddrMode.IsDirect)
                {
                    if (decoderAddrMode.Direction == 0)
                    {
                        opCodeASM = "XOR " + decoderAddrMode.Register2.ToString() + ", " + decoderAddrMode.Register1.ToString();
                    }
                    else
                    {
                        opCodeASM = "XOR " + decoderAddrMode.Register1.ToString() + ", " + decoderAddrMode.Register2.ToString();
                    }
                    decoderClkCyc += (byte)3;
                }
                else
                {
                    if (decoderAddrMode.Direction == 0)
                    {
                        opCodeASM = "XOR " + indASM + ", " + decoderAddrMode.Register1.ToString();
                        decoderClkCyc += (byte)16;
                    }
                    else
                    {
                        opCodeASM = "XOR " + decoderAddrMode.Register1.ToString() + ", " + indASM;
                        decoderClkCyc += (byte)9;
                    }
                }
            } // xor al and imm
            else if (opCode == ((byte)(0x34)))
            {
                opCodeASM = "XOR AL, " + DecoderParam(index: ParamIndex.First, size: DataSize.Byte).ToString("X2");
                decoderClkCyc += (byte)4;
            } // xor ax and imm
            else if (opCode == ((byte)(0x35)))
            {
                opCodeASM = "XOR AX, " + DecoderParam(index: ParamIndex.First, size: DataSize.Word).ToString("X4");
                decoderClkCyc += (byte)4;
            } // aaa
            else if (opCode == ((byte)(0x37)))
            {
                opCodeASM = "AAA";
                decoderClkCyc += (byte)8;
            } // cmp reg/mem and reg
            else if (opCode >= 0x38 && opCode <= 0x3B)
            {
                SetDecoderAddressing();
                if (decoderAddrMode.IsDirect)
                {
                    if (decoderAddrMode.Direction == 0)
                    {
                        opCodeASM = "CMP " + decoderAddrMode.Register2.ToString() + ", " + decoderAddrMode.Register1.ToString();
                    }
                    else
                    {
                        opCodeASM = "CMP " + decoderAddrMode.Register1.ToString() + ", " + decoderAddrMode.Register2.ToString();
                    }
                    decoderClkCyc += (byte)3;
                }
                else
                {
                    if (decoderAddrMode.Direction == 0)
                    {
                        opCodeASM = "CMP " + indASM + ", " + decoderAddrMode.Register1.ToString();
                    }
                    else
                    {
                        opCodeASM = "CMP " + decoderAddrMode.Register1.ToString() + ", " + indASM;
                    }
                    decoderClkCyc += (byte)9;
                }
            } // cmp al and imm
            else if (opCode == ((byte)(0x3C)))
            {
                opCodeASM = "CMP AL, " + DecoderParam(index: ParamIndex.First, size: DataSize.Byte).ToString("X2");
                decoderClkCyc += (byte)4;
            } // cmp ax and imm
            else if (opCode == ((byte)(0x3D)))
            {
                opCodeASM = "CMP AX, " + DecoderParam(index: ParamIndex.First, size: DataSize.Word).ToString("X4");
                decoderClkCyc += (byte)4;
            } // aas
            else if (opCode == ((byte)(0x3F)))
            {
                opCodeASM = "AAS";
                decoderClkCyc += (byte)8;
            } // segment override prefix
            else if ((((opCode == ((byte)(0x26))) || (opCode == ((byte)(0x2E)))) || (opCode == ((byte)(0x36)))) || (opCode == ((byte)(0x3E))))
            {
                decoderAddrMode.Decode(opCode, opCode);
                decoderAddrMode.Register1 = decoderAddrMode.Register1 - GPRegisters.RegistersTypes.AH + GPRegisters.RegistersTypes.ES;
                opCodeASM = decoderAddrMode.Register1.ToString() + ":";
                segOvr = opCodeASM;
                newPrefix = true;
                decoderClkCyc += (byte)2;
            } // inc reg
            else if (opCode >= 0x40 && opCode <= 0x47)
            {
                DecoderSetRegister1Alt(opCode);
                opCodeASM = "INC " + decoderAddrMode.Register1.ToString();
            } // dec reg
            else if (opCode >= 0x48 && opCode <= 0x4F)
            {
                DecoderSetRegister1Alt(opCode);
                opCodeASM = "DEC " + decoderAddrMode.Register1.ToString();
                decoderClkCyc += (byte)2;
            } // push reg
            else if (opCode >= 0x50 && opCode <= 0x57)
            {
                DecoderSetRegister1Alt(opCode);
                opCodeASM = "PUSH " + decoderAddrMode.Register1.ToString();
                decoderClkCyc += (byte)11;
            } // pop reg
            else if (opCode >= 0x58 && opCode <= 0x5F)
            {
                DecoderSetRegister1Alt(opCode);
                opCodeASM = "POP " + decoderAddrMode.Register1.ToString();
                decoderClkCyc += (byte)8;
            } // pusha
            else if (opCode == ((byte)(0x60)))
            {
                opCodeASM = "PUSHA";
                decoderClkCyc += (byte)19;
            } // popa
            else if (opCode == ((byte)(0x61)))
            {
                opCodeASM = "POPA";
                decoderClkCyc += (byte)19;
            } // jo
            else if (opCode == ((byte)(0x70)))
            {
                decoderIPAddrOff = OffsetIP(DataSize.Byte);
                opCodeASM = "JO " + decoderIPAddrOff.ToString("X4");
                decoderClkCyc += (byte)(mFlags.OF == 1 ? 16 : 4);
            } // jno
            else if (opCode == ((byte)(0x71)))
            {
                decoderIPAddrOff = OffsetIP(DataSize.Byte);
                opCodeASM = "JNO " + decoderIPAddrOff.ToString("X4");
                decoderClkCyc += (byte)(mFlags.OF == 0 ? 16 : 4);
            } // jb/jnae
            else if (opCode == ((byte)(0x72)))
            {
                decoderIPAddrOff = OffsetIP(DataSize.Byte);
                opCodeASM = "JB " + decoderIPAddrOff.ToString("X4");
                decoderClkCyc += (byte)(mFlags.CF == 1 ? 16 : 4);
            } // jnb/jae
            else if (opCode == ((byte)(0x73)))
            {
                decoderIPAddrOff = OffsetIP(DataSize.Byte);
                opCodeASM = "JNB " + decoderIPAddrOff.ToString("X4");
                decoderClkCyc += (byte)(mFlags.CF == 0 ? 16 : 4);
            } // je/jz
            else if (opCode == ((byte)(0x74)))
            {
                decoderIPAddrOff = OffsetIP(DataSize.Byte);
                opCodeASM = "JE " + decoderIPAddrOff.ToString("X4");
                decoderClkCyc += (byte)(mFlags.ZF == 1 ? 16 : 4);
            } // jne/jnz
            else if (opCode == ((byte)(0x75)))
            {
                decoderIPAddrOff = OffsetIP(DataSize.Byte);
                opCodeASM = "JNE " + decoderIPAddrOff.ToString("X4");
                decoderClkCyc += (byte)(mFlags.ZF == 0 ? 16 : 4);
            } // jbe/jna
            else if (opCode == ((byte)(0x76)))
            {
                decoderIPAddrOff = OffsetIP(DataSize.Byte);
                opCodeASM = "JBE " + decoderIPAddrOff.ToString("X4");
                decoderClkCyc += (byte)(mFlags.CF == 1 || mFlags.ZF == 1 ? 16 : 4);
            } // jnbe/ja
            else if (opCode == ((byte)(0x77)))
            {
                decoderIPAddrOff = OffsetIP(DataSize.Byte);
                opCodeASM = "JNBE " + decoderIPAddrOff.ToString("X4");
                decoderClkCyc += (byte)(mFlags.CF == 0 && mFlags.ZF == 0 ? 16 : 4);
            } // js
            else if (opCode == ((byte)(0x78)))
            {
                decoderIPAddrOff = OffsetIP(DataSize.Byte);
                opCodeASM = "JS " + decoderIPAddrOff.ToString("X4");
                decoderClkCyc += (byte)(mFlags.SF == 1 ? 16 : 4);
            } // jns
            else if (opCode == ((byte)(0x79)))
            {
                decoderIPAddrOff = OffsetIP(DataSize.Byte);
                opCodeASM = "JNS " + decoderIPAddrOff.ToString("X4");
                decoderClkCyc += (byte)(mFlags.SF == 0 ? 16 : 4);
            } // jp/jpe
            else if (opCode == ((byte)(0x7A)))
            {
                decoderIPAddrOff = OffsetIP(DataSize.Byte);
                opCodeASM = "JP " + decoderIPAddrOff.ToString("X4");
                decoderClkCyc += (byte)(mFlags.PF == 1 ? 16 : 4);
            } // jnp/jpo
            else if (opCode == ((byte)(0x7B)))
            {
                decoderIPAddrOff = OffsetIP(DataSize.Byte);
                opCodeASM = "JNP " + decoderIPAddrOff.ToString("X4");
                decoderClkCyc += (byte)(mFlags.PF == 0 ? 16 : 4);
            } // jl/jnge
            else if (opCode == ((byte)(0x7C)))
            {
                decoderIPAddrOff = OffsetIP(DataSize.Byte);
                opCodeASM = "JL " + decoderIPAddrOff.ToString("X4");
                decoderClkCyc += (byte)(mFlags.SF != mFlags.OF ? 16 : 4);
            } // jnl/jge
            else if (opCode == ((byte)(0x7D)))
            {
                decoderIPAddrOff = OffsetIP(DataSize.Byte);
                opCodeASM = "JNL " + decoderIPAddrOff.ToString("X4");
                decoderClkCyc += (byte)(mFlags.SF == mFlags.OF ? 16 : 4);
            } // jle/jng
            else if (opCode == ((byte)(0x7E)))
            {
                decoderIPAddrOff = OffsetIP(DataSize.Byte);
                opCodeASM = "JLE " + decoderIPAddrOff.ToString("X4");
                decoderClkCyc += (byte)((mFlags.ZF == 1 || (mFlags.SF != mFlags.OF)) ? 16 : 4);
            } // jnle/jg
            else if (opCode == ((byte)(0x7F)))
            {
                decoderIPAddrOff = OffsetIP(DataSize.Byte);
                opCodeASM = "JNLE " + decoderIPAddrOff.ToString("X4");
                decoderClkCyc += (byte)((mFlags.ZF == 0 || (mFlags.SF == mFlags.OF)) ? 16 : 4);
            }
            else if (opCode >= 0x80 && opCode <= 0x83)
            {
                DecodeGroup1();
            } // test reg with reg/mem
            else if (opCode >= 0x84 && opCode <= 0x85)
            {
                SetDecoderAddressing();
                if (decoderAddrMode.IsDirect)
                {
                    opCodeASM = "TEST " + decoderAddrMode.Register1.ToString() + ", " + decoderAddrMode.Register2.ToString();
                    decoderClkCyc += (byte)3;
                }
                else
                {
                    opCodeASM = "TEST " + indASM + ", " + decoderAddrMode.Register2.ToString();
                    decoderClkCyc += (byte)9;
                }
            } // xchg reg/mem with reg
            else if (opCode >= 0x86 && opCode <= 0x87)
            {
                SetDecoderAddressing();
                if (decoderAddrMode.IsDirect)
                {
                    opCodeASM = "XCHG " + decoderAddrMode.Register1.ToString() + ", " + decoderAddrMode.Register2.ToString();
                    decoderClkCyc += (byte)4;
                }
                else
                {
                    opCodeASM = "XCHG " + indASM + ", " + decoderAddrMode.Register1.ToString();
                    decoderClkCyc += (byte)17;
                }
            } // mov ind <-> reg8/reg16
            else if (opCode >= 0x88 && opCode <= 0x8B)
            {
                SetDecoderAddressing();
                if (decoderAddrMode.IsDirect)
                {
                    opCodeASM = "MOV " + decoderAddrMode.Dst.ToString() + ", " + decoderAddrMode.Src.ToString();
                    decoderClkCyc += (byte)2;
                }
                else
                {
                    if (decoderAddrMode.Direction == 0)
                    {
                        opCodeASM = "MOV " + indASM + ", " + decoderAddrMode.Src.ToString();
                        decoderClkCyc += (byte)9;
                    }
                    else
                    {
                        opCodeASM = "MOV " + decoderAddrMode.Dst.ToString() + ", " + indASM;
                        decoderClkCyc += (byte)8;
                    }
                }
            } // mov Ew, Sw
            else if (opCode == ((byte)(0x8C)))
            {
                SetDecoderAddressing(DataSize.Word);
                decoderAddrMode.Src += (int)GPRegisters.RegistersTypes.ES;
                if (decoderAddrMode.Dst > GPRegisters.RegistersTypes.BL)
                {
                    decoderAddrMode.Dst = ((decoderAddrMode.Dst + (int)GPRegisters.RegistersTypes.ES) | GPRegisters.RegistersTypes.AX);
                }
                else
                {
                    //decoderAddrMode.Dst = (int) (decoderAddrMode.Dst) | GPRegisters.RegistersTypes.AX;
                    decoderAddrMode.Dst |= GPRegisters.RegistersTypes.AX;
                }

                if (decoderAddrMode.IsDirect)
                {
                    opCodeASM = "MOV " + decoderAddrMode.Dst.ToString() + ", " + decoderAddrMode.Src.ToString();
                    decoderClkCyc += (byte)2;
                }
                else
                {
                    if (decoderAddrMode.Direction == 0)
                    {
                        opCodeASM = "MOV " + indASM + ", " + decoderAddrMode.Src.ToString();
                        decoderClkCyc += (byte)9;
                    }
                    else
                    {
                        opCodeASM = "MOV " + decoderAddrMode.Dst.ToString() + ", " + indASM;
                        decoderClkCyc += (byte)8;
                    }
                }
            } // lea
            else if (opCode == ((byte)(0x8D)))
            {
                SetDecoderAddressing();
                opCodeASM = "LEA " + decoderAddrMode.Register1.ToString() + ", " + indASM;
                decoderClkCyc += (byte)2;
            } // mov Sw, Ew
            else if (opCode == ((byte)(0x8E)))
            {
                SetDecoderAddressing(DataSize.Word);
                DecoderSetRegister2ToSegReg();
                if (decoderAddrMode.IsDirect)
                {
                    DecoderSetRegister1Alt(get_RAM8(mRegisters.CS, (ushort)(mRegisters.IP + 1)));
                    opCodeASM = "MOV " + decoderAddrMode.Register2.ToString() + ", " + decoderAddrMode.Register1.ToString();
                    decoderClkCyc += (byte)2;
                }
                else
                {
                    opCodeASM = "MOV " + decoderAddrMode.Register2.ToString() + ", " + indASM;
                    decoderClkCyc += (byte)8;
                }
            } // pop reg/mem
            else if (opCode == ((byte)(0x8F)))
            {
                SetDecoderAddressing();
                decoderAddrMode.Decode(opCode, opCode);
                if (decoderAddrMode.IsDirect)
                {
                    opCodeASM = "POP " + decoderAddrMode.Register1.ToString();
                }
                else
                {
                    opCodeASM = "POP " + indASM;
                }
                decoderClkCyc += (byte)17;
            } // nop
            else if (opCode == ((byte)(0x90)))
            {
                opCodeASM = "NOP";
                decoderClkCyc += (byte)3;
            } // xchg reg with acc
            else if (opCode >= 0x91 && opCode <= 0x97)
            {
                DecoderSetRegister1Alt(opCode);
                opCodeASM = "XCHG AX, " + decoderAddrMode.Register1.ToString();
                decoderClkCyc += (byte)3;
            } // cbw
            else if (opCode == ((byte)(0x98)))
            {
                opCodeASM = "CBW";
                decoderClkCyc += (byte)2;
            } // cwd
            else if (opCode == ((byte)(0x99)))
            {
                opCodeASM = "CWD";
                decoderClkCyc += (byte)5;
            } // call direct intersegment
            else if (opCode == ((byte)(0x9A)))
            {
                opCodeASM = "CALL " + DecoderParam(index: ParamIndex.Second, size: DataSize.Word).ToString("X4") + ":" + DecoderParam(index: ParamIndex.First, size: DataSize.Word).ToString("X4");
                decoderClkCyc += (byte)28;
            } // wait
            else if (opCode == ((byte)(0x9B)))
            {
                opCodeASM = "FWAIT";
            } // pushf
            else if (opCode == ((byte)(0x9C)))
            {
                opCodeASM = "PUSHF";
                decoderClkCyc += (byte)10;
            } // popf
            else if (opCode == ((byte)(0x9D)))
            {
                opCodeASM = "POPF";
                decoderClkCyc += (byte)8;
            } // sahf
            else if (opCode == ((byte)(0x9E)))
            {
                opCodeASM = "SAHF";
                decoderClkCyc += (byte)4;
            } // lahf
            else if (opCode == ((byte)(0x9F)))
            {
                opCodeASM = "LAHF";
                decoderClkCyc += (byte)4;
            } // mov mem to acc | mov acc to mem
            else if (opCode >= 0xA0 && opCode <= 0xA3)
            {
                decoderAddrMode.Decode(opCode, opCode);
                if (decoderAddrMode.Direction == 0)
                {
                    if (decoderAddrMode.Size == DataSize.Byte)
                    {
                        opCodeASM = "MOV AL, [" + DecoderParam(index: ParamIndex.First, size: DataSize.Word).ToString("X4") + "]";
                    }
                    else
                    {
                        opCodeASM = "MOV AX, [" + DecoderParam(index: ParamIndex.First, size: DataSize.Word).ToString("X4") + "]";
                    }
                }
                else
                {
                    if (decoderAddrMode.Size == DataSize.Byte)
                    {
                        opCodeASM = "MOV [" + DecoderParam(index: ParamIndex.First, size: DataSize.Word).ToString("X4") + "], AL";
                    }
                    else
                    {
                        opCodeASM = "MOV [" + DecoderParam(index: ParamIndex.First, size: DataSize.Word).ToString("X4") + "], AX";
                    }
                }
                decoderClkCyc += (byte)10;
            } // movsb
            else if (opCode == ((byte)(0xA4)))
            {
                opCodeASM = "MOVSB";
                decoderClkCyc += (byte)18;
            } // movsw
            else if (opCode == ((byte)(0xA5)))
            {
                opCodeASM = "MOVSW";
                decoderClkCyc += (byte)18;
            } // cmpsb
            else if (opCode == ((byte)(0xA6)))
            {
                opCodeASM = "CMPSB";
                decoderClkCyc += (byte)22;
            } // cmpsw
            else if (opCode == ((byte)(0xA7)))
            {
                opCodeASM = "CMPSW";
                decoderClkCyc += (byte)22;
            } // test
            else if (opCode >= 0xA8 && opCode <= 0xA9)
            {
                if ((opCode & 0x1) == 0)
                {
                    opCodeASM = "TEST AL, " + DecoderParam(index: ParamIndex.First, size: DataSize.Byte).ToString("X2");
                }
                else
                {
                    opCodeASM = "TEST AX, " + DecoderParam(index: ParamIndex.First, size: DataSize.Word).ToString("X4");
                }
                decoderClkCyc += (byte)4;
            } // stosb
            else if (opCode == ((byte)(0xAA)))
            {
                opCodeASM = "STOSB";
                decoderClkCyc += (byte)11;
            } //stosw
            else if (opCode == ((byte)(0xAB)))
            {
                opCodeASM = "STOSW";
                decoderClkCyc += (byte)11;
            } // lodsb
            else if (opCode == ((byte)(0xAC)))
            {
                opCodeASM = "LODSB";
                decoderClkCyc += (byte)12;
            } // lodsw
            else if (opCode == ((byte)(0xAD)))
            {
                opCodeASM = "LODSW";
                decoderClkCyc += (byte)16;
            } // scasb
            else if (opCode == ((byte)(0xAE)))
            {
                opCodeASM = "SCASB";
                decoderClkCyc += (byte)15;
            } // scasw
            else if (opCode == ((byte)(0xAF)))
            {
                opCodeASM = "SCASW";
                decoderClkCyc += (byte)15;
            } // mov imm to reg
            else if (opCode >= 0xB0 && opCode <= 0xBF)
            {
                decoderAddrMode.Register1 = (GPRegisters.RegistersTypes)(opCode & 0x7);
                if ((opCode & 0x8) == 0x8)
                {
                    decoderAddrMode.Register1 += (int)GPRegisters.RegistersTypes.AX;
                    if ((opCode & 0x4) == 0x4)
                    {
                        decoderAddrMode.Register1 += (int)GPRegisters.RegistersTypes.ES;
                    }
                    decoderAddrMode.Size = DataSize.Word;
                }
                else
                {
                    decoderAddrMode.Size = DataSize.Byte;
                }
                opCodeASM = "MOV " + decoderAddrMode.Register1.ToString() + ", " + DecoderParam(ParamIndex.First).ToHex(decoderAddrMode.Size);
                decoderClkCyc += (byte)4;
            }
            else if ((opCode == ((byte)(0xC0))) || (opCode == ((byte)(0xC1))))
            {
                DecodeGroup2();
            } // ret within segment adding imm to sp
            else if (opCode == ((byte)(0xC2)))
            {
                opCodeASM = "RET " + DecoderParam(ParamIndex.First).ToHex();
                decoderClkCyc += (byte)20;
            } // ret within segment
            else if (opCode == ((byte)(0xC3)))
            {
                opCodeASM = "RET";
                decoderClkCyc += (byte)16;
            } // les | lds
            else if (opCode >= 0xC4 && opCode <= 0xC5)
            {
                SetDecoderAddressing();
                GPRegisters.RegistersTypes targetRegister;
                if (opCode == 0xC4)
                {
                    opCodeASM = "LES ";
                    targetRegister = GPRegisters.RegistersTypes.ES;
                }
                else
                {
                    opCodeASM = "LDS ";
                    targetRegister = GPRegisters.RegistersTypes.DS;
                }

                if (((int)(decoderAddrMode.Register1) & shl2) == shl2)
                {
                    //decoderAddrMode.Register1 = (decoderAddrMode.Register1 + (int)GPRegisters.RegistersTypes.ES) | (GPRegisters.RegistersTypes)shl3;
                    decoderAddrMode.Register1 = (decoderAddrMode.Register1 + (int)GPRegisters.RegistersTypes.ES) | GPRegisters.RegistersTypes.AX;
                }
                else
                {
                    //decoderAddrMode.Register1 = (int) (decoderAddrMode.Register1) | (GPRegisters.RegistersTypes)shl3;
                    decoderAddrMode.Register1 |= GPRegisters.RegistersTypes.AX;
                }
                //If decoderAddrMode.IsDirect Then
                //    If (decoderAddrMode.Register2 And shl2) = shl2 Then
                //        decoderAddrMode.Register2 = (decoderAddrMode.Register2 + GPRegisters.RegistersTypes.BX + 1) Or shl3
                //    Else
                //        decoderAddrMode.Register2 = (decoderAddrMode.Register2 Or shl3)
                //    End If

                //    opCodeASM += decoderAddrMode.Register1.ToString() + ", " + decoderAddrMode.Register2.ToString()
                //Else
                opCodeASM += decoderAddrMode.Register1.ToString() + ", " + indASM;
                //End If
                decoderClkCyc += (byte)16;
            } // mov imm to reg/mem
            else if (opCode >= 0xC6 && opCode <= 0xC7)
            {
                SetDecoderAddressing();
                opCodeASM = "MOV " + indASM + ", " + DecoderParam(ParamIndex.First, (ushort)(opCodeSize)).ToHex(decoderAddrMode.Size);
                decoderClkCyc += (byte)10;
            } // enter
            else if (opCode == ((byte)(0xC8)))
            {
                opCodeASM = "ENTER";
                opCodeSize += (byte)3;
            } // leave
            else if (opCode == ((byte)(0xC9)))
            {
                opCodeASM = "LEAVE";
            } // ret intersegment adding imm to sp
            else if (opCode == ((byte)(0xCA)))
            {
                opCodeASM = "RETF " + DecoderParam(index: ParamIndex.First, size: DataSize.Word).ToString("X4");
                decoderClkCyc += (byte)17;
            } // ret intersegment (retf)
            else if (opCode == ((byte)(0xCB)))
            {
                opCodeASM = "RETF";
                decoderClkCyc += (byte)18;
            } // int with type 3
            else if (opCode == ((byte)(0xCC)))
            {
                opCodeASM = "INT 3";
                decoderClkCyc += (byte)52;
            } // int with type specified
            else if (opCode == ((byte)(0xCD)))
            {
                opCodeASM = "INT " + DecoderParam(index: ParamIndex.First, size: DataSize.Byte).ToString("X2");
                decoderClkCyc += (byte)51;
            } // into
            else if (opCode == ((byte)(0xCE)))
            {
                opCodeASM = "INTO";
                decoderClkCyc += (byte)(mFlags.OF == 1 ? 53 : 4);
            } // iret
            else if (opCode == ((byte)(0xCF)))
            {
                opCodeASM = "IRET";
                decoderClkCyc += (byte)32;
            }
            else if (opCode >= 0xD0 && opCode <= 0xD3)
            {
                DecodeGroup2();
            } // aam
            else if (opCode == ((byte)(0xD4)))
            {
                opCodeASM = "AAM " + DecoderParam(index: ParamIndex.First, size: DataSize.Byte).ToString("X2");
                decoderClkCyc += (byte)83;
            } // aad
            else if (opCode == ((byte)(0xD5)))
            {
                opCodeASM = "AAD " + DecoderParam(index: ParamIndex.First, size: DataSize.Byte).ToString("X2");
                decoderClkCyc += (byte)60;
            } // xlat
            else if (opCode == ((byte)(0xD6)))
            {
                opCodeASM = "XLAT";
                decoderClkCyc += (byte)4;
            } // xlatb
            else if (opCode == ((byte)(0xD7)))
            {
                opCodeASM = "XLATB";
                decoderClkCyc += (byte)11;
            } // fnstsw (required for BIOS to boot)
            else if (opCode == ((byte)(0xD9)))
            {
                if (DecoderParamNOPS(index: ParamIndex.First, size: DataSize.Byte) == 0x3C)
                {
                    opCodeASM = "FNSTSW {NOT IMPLEMENTED}";
                }
                else
                {
                    opCodeASM = opCode.ToString("X2") + " {NOT IMPLEMENTED}";
                }
                opCodeSize++;
            } // fninit (required for BIOS to boot)
            else if (opCode == ((byte)(0xDB)))
            {
                if (DecoderParamNOPS(index: ParamIndex.First, size: DataSize.Byte) == 0xE3)
                {
                    opCodeASM = "FNINIT {NOT IMPLEMENTED}";
                }
                else
                {
                    opCodeASM = opCode.ToString("X2") + " {NOT IMPLEMENTED}";
                }
                opCodeSize++;
            } // loopne
            else if (opCode == ((byte)(0xE0)))
            {
                decoderIPAddrOff = OffsetIP(DataSize.Byte);
                opCodeASM = "LOOPNE " + decoderIPAddrOff.ToString("X4");
                decoderClkCyc += (byte)(mRegisters.CX != 0 && mFlags.ZF == 0 ? 19 : 5);
            } // loope
            else if (opCode == ((byte)(0xE1)))
            {
                decoderIPAddrOff = OffsetIP(DataSize.Byte);
                opCodeASM = "LOOPE " + decoderIPAddrOff.ToString("X4");
                decoderClkCyc += (byte)(mRegisters.CX != 0 && mFlags.ZF == 1 ? 18 : 6);
            } // loop
            else if (opCode == ((byte)(0xE2)))
            {
                decoderIPAddrOff = OffsetIP(DataSize.Byte);
                opCodeASM = "LOOP " + decoderIPAddrOff.ToString("X4");
                decoderClkCyc += (byte)(mRegisters.CX != 0 ? 17 : 5);
            } // jcxz
            else if (opCode == ((byte)(0xE3)))
            {
                decoderIPAddrOff = OffsetIP(DataSize.Byte);
                opCodeASM = "JCXZ " + decoderIPAddrOff.ToString("X4");
                decoderClkCyc += (byte)(mRegisters.CX == 0 ? 18 : 6);
            } // in to al from fixed port
            else if (opCode == ((byte)(0xE4)))
            {
                opCodeASM = "IN AL, " + DecoderParam(index: ParamIndex.First, size: DataSize.Byte).ToString("X2");
                decoderClkCyc += (byte)10;
            } // inw to ax from fixed port
            else if (opCode == ((byte)(0xE5)))
            {
                opCodeASM = "IN AX, " + DecoderParam(index: ParamIndex.First, size: DataSize.Byte).ToString("X2");
                decoderClkCyc += (byte)10;
            } // out to al to fixed port
            else if (opCode == ((byte)(0xE6)))
            {
                opCodeASM = "OUT " + DecoderParam(index: ParamIndex.First, size: DataSize.Byte).ToString("X2") + ", AL";
                decoderClkCyc += (byte)10;
            } // outw to ax to fixed port
            else if (opCode == ((byte)(0xE7)))
            {
                opCodeASM = "OUT " + DecoderParam(index: ParamIndex.First, size: DataSize.Byte).ToString("X2") + ", Ax";
                decoderClkCyc += (byte)10;
            } // call direct within segment
            else if (opCode == ((byte)(0xE8)))
            {
                opCodeASM = "CALL " + OffsetIP(DataSize.Word).ToString("X4");
                decoderClkCyc += (byte)19;
            } // jmp direct within segment
            else if (opCode == ((byte)(0xE9)))
            {
                opCodeASM = "JMP " + OffsetIP(DataSize.Word).ToString("X4");
                decoderClkCyc += (byte)15;
            } // jmp direct intersegment
            else if (opCode == ((byte)(0xEA)))
            {
                opCodeASM = "JMP " + DecoderParam(index: ParamIndex.Second, size: DataSize.Word).ToString("X4") + ":" + DecoderParam(index: ParamIndex.First, size: DataSize.Word).ToString("X4");
                decoderClkCyc += (byte)15;
            } // jmp direct within segment short
            else if (opCode == ((byte)(0xEB)))
            {
                decoderIPAddrOff = OffsetIP(DataSize.Byte);
                opCodeASM = "JMP " + decoderIPAddrOff.ToString("X4");
                decoderClkCyc += (byte)15;
            } // in to al from variable port
            else if (opCode == ((byte)(0xEC)))
            {
                opCodeASM = "IN AL, DX";
                decoderClkCyc += (byte)8;
            } // inw to ax from variable port
            else if (opCode == ((byte)(0xED)))
            {
                opCodeASM = "IN AX, DX";
                decoderClkCyc += (byte)8;
            } // out to port dx from al
            else if (opCode == ((byte)(0xEE)))
            {
                opCodeASM = "OUT DX, AL";
                decoderClkCyc += (byte)8;
            } // out to port dx from ax
            else if (opCode == ((byte)(0xEF)))
            {
                opCodeASM = "OUT DX, AX";
                decoderClkCyc += (byte)8;
            } // lock
            else if (opCode == ((byte)(0xF0)))
            {
                opCodeASM = "LOCK";
                decoderClkCyc += (byte)2;
            } // repne/repnz
            else if (opCode == ((byte)(0xF2)))
            {
                opCodeASM = "REPNE";
                newPrefix = true;
                decoderClkCyc += (byte)2;
            } // rep/repe
            else if (opCode == ((byte)(0xF3)))
            {
                opCodeASM = "REPE";
                newPrefix = true;
                decoderClkCyc += (byte)2;
            } // hlt
            else if (opCode == ((byte)(0xF4)))
            {
                opCodeASM = "HLT";
                decoderClkCyc += (byte)2;
            } // cmc
            else if (opCode == ((byte)(0xF5)))
            {
                opCodeASM = "CMC";
                decoderClkCyc += (byte)2;
            }
            else if (opCode >= 0xF6 && opCode <= 0xF7)
            {
                DecodeGroup3();
            } // clc
            else if (opCode == ((byte)(0xF8)))
            {
                opCodeASM = "CLC";
                decoderClkCyc += (byte)2;
            } // stc
            else if (opCode == ((byte)(0xF9)))
            {
                opCodeASM = "STC";
                decoderClkCyc += (byte)2;
            } // cli
            else if (opCode == ((byte)(0xFA)))
            {
                opCodeASM = "CLI";
                decoderClkCyc += (byte)2;
            } // sti
            else if (opCode == ((byte)(0xFB)))
            {
                opCodeASM = "STI";
                decoderClkCyc += (byte)2;
            } // cld
            else if (opCode == ((byte)(0xFC)))
            {
                opCodeASM = "CLD";
                decoderClkCyc += (byte)2;
            } // std
            else if (opCode == ((byte)(0xFD)))
            {
                opCodeASM = "STD";
                decoderClkCyc += (byte)2;
            }
            else if (opCode >= 0xFE && opCode <= 0xFF)
            {
                DecodeGroup4_And_5();
            }
            else
            {
                opCodeASM = opCode.ToString("X2") + ": {NOT IMPLEMENTED}";
            }

            if (opCodeSize == 0)
            {
                throw (new Exception("Decoding error for opCode " + opCode.ToString("X2")));
            }

            if (string.IsNullOrEmpty(segOvr))
            {
                segOvr = mRegisters.ActiveSegmentRegister.ToString() + ":";
            }

            Instruction info = new Instruction()
            {
                IsValid = true,
                OpCode = opCode,
                CS = mRegisters.CS,
                IP = mRegisters.IP,
                Size = opCodeSize,
                JumpAddress = decoderIPAddrOff,
                IndMemoryData = decoderAddrMode.IndMem,
                IndAddress = decoderAddrMode.IndAdr,
                ClockCycles = decoderClkCyc,
                SegmentOverride = segOvr
            };

            if (!string.IsNullOrEmpty(opCodeASM))
            {
                if (opCodeSize > 0)
                {
                    info.Bytes = new byte[opCodeSize];
                    info.Bytes[0] = opCode;
                }
                for (int i = 1; i <= opCodeSize - 1; i++)
                {
                    info.Bytes[i] = (byte)DecoderParamNOPS(ParamIndex.First, (ushort)i, DataSize.Byte);
                }
                if (opCodeASM.Contains(" "))
                {
                    int space = opCodeASM.IndexOf(" ");
                    info.Mnemonic = opCodeASM.Substring(0, space);
                    opCodeASM = opCodeASM.Substring(space + 1);
                    if (opCodeASM.Contains("{"))
                    {
                        info.Message = opCodeASM;
                    }
                    else
                    {
                        if (opCodeASM.Contains(","))
                        {
                            info.Parameter1 = System.Convert.ToString(opCodeASM.Split(",".ToCharArray())[0]);
                            info.Parameter2 = System.Convert.ToString(opCodeASM.Split(",".ToCharArray())[1].Trim());
                        }
                        else
                        {
                            info.Parameter1 = opCodeASM;
                        }
                    }
                }
                else
                {
                    info.Mnemonic = opCodeASM;
                }
            }
            if (!string.IsNullOrEmpty(segOvr) && info.Mnemonic != segOvr)
            {
                segOvr = "";
            }
            decoderClkCyc += (byte)(opCodeSize * 4);

            if (!newPrefix && mRegisters.ActiveSegmentChanged)
            {
                mRegisters.ResetActiveSegment();
            }

            return info;
        }

        private void DecodeGroup1()
        {
            SetDecoderAddressing();
            DataSize paramSize = opCode == 0x81 ? DataSize.Word : DataSize.Byte;
            if (decoderAddrMode.Reg == ((byte)0)) // 000    --   add imm to reg/mem
            {
                opCodeASM = "ADD";
                decoderClkCyc += (byte)(decoderAddrMode.IsDirect ? 4 : 17);
            } // 001    --  or imm to reg/mem
            else if (decoderAddrMode.Reg == ((byte)1))
            {
                opCodeASM = "OR";
                decoderClkCyc += (byte)(decoderAddrMode.IsDirect ? 4 : 17);
            } // 010    --  adc imm to reg/mem
            else if (decoderAddrMode.Reg == ((byte)2))
            {
                opCodeASM = "ADC";
                decoderClkCyc += (byte)(decoderAddrMode.IsDirect ? 4 : 17);
            } // 011    --  sbb imm from reg/mem
            else if (decoderAddrMode.Reg == ((byte)3))
            {
                opCodeASM = "SBB";
                decoderClkCyc += (byte)(decoderAddrMode.IsDirect ? 4 : 17);
            } // 100    --  and imm to reg/mem
            else if (decoderAddrMode.Reg == ((byte)4))
            {
                opCodeASM = "AND";
                decoderClkCyc += (byte)(decoderAddrMode.IsDirect ? 4 : 17);
            } // 101    --  sub imm from reg/mem
            else if (decoderAddrMode.Reg == ((byte)5))
            {
                opCodeASM = "SUB";
                decoderClkCyc += (byte)(decoderAddrMode.IsDirect ? 4 : 17);
            } // 110    --  xor imm to reg/mem
            else if (decoderAddrMode.Reg == ((byte)6))
            {
                opCodeASM = "XOR";
                decoderClkCyc += (byte)(decoderAddrMode.IsDirect ? 4 : 17);
            } // 111    --  cmp imm with reg/mem
            else if (decoderAddrMode.Reg == ((byte)7))
            {
                opCodeASM = "CMP";
                decoderClkCyc += (byte)(decoderAddrMode.IsDirect ? 4 : 10);
            }
            if (decoderAddrMode.IsDirect)
            {
                opCodeASM += " " + decoderAddrMode.Register2.ToString() + ", " + DecoderParam(ParamIndex.First, (ushort)(opCodeSize), paramSize).ToHex(paramSize);
            }
            else
            {
                opCodeASM += " " + indASM + ", " + DecoderParam(ParamIndex.First, (ushort)(opCodeSize), paramSize).ToHex(paramSize);
            }
        }

        private void DecodeGroup2()
        {
            SetDecoderAddressing();

            if (decoderAddrMode.IsDirect)
            {
                if (opCode >= 0xD2)
                {
                    opCodeASM = decoderAddrMode.Register2.ToString() + ", CL";
                    decoderClkCyc += (byte)(8 + 4); //* count
                }
                else
                {
                    opCodeASM = decoderAddrMode.Register2.ToString() + ", 1";
                    decoderClkCyc += (byte)2;
                }
            }
            else
            {
                if ((opCode & 0x2) == 0x2)
                {
                    opCodeASM = indASM + ", CL";
                    decoderClkCyc += (byte)(20 + 4); //* count
                }
                else
                {
                    opCodeASM = indASM + ", 1";
                    decoderClkCyc += (byte)15;
                }
            }

            if (decoderAddrMode.Reg == ((byte)0))
            {
                opCodeASM = "ROL " + opCodeASM;
            }
            else if (decoderAddrMode.Reg == ((byte)1))
            {
                opCodeASM = "ROR " + opCodeASM;
            }
            else if (decoderAddrMode.Reg == ((byte)2))
            {
                opCodeASM = "RCL " + opCodeASM;
            }
            else if (decoderAddrMode.Reg == ((byte)3))
            {
                opCodeASM = "RCR " + opCodeASM;
            }
            else if ((decoderAddrMode.Reg == ((byte)4)) || (decoderAddrMode.Reg == ((byte)6)))
            {
                opCodeASM = "SHL " + opCodeASM;
            }
            else if (decoderAddrMode.Reg == ((byte)5))
            {
                opCodeASM = "SHR " + opCodeASM;
            }
            else if (decoderAddrMode.Reg == ((byte)7))
            {
                opCodeASM = "SAR " + opCodeASM;
            }
        }

        private void DecodeGroup3()
        {
            SetDecoderAddressing();

            if (decoderAddrMode.Reg == ((byte)0)) // 000    --  test
            {
                if (decoderAddrMode.IsDirect)
                {
                    opCodeASM = "TEST " + decoderAddrMode.Register2.ToString() + ", " + DecoderParam(ParamIndex.First, (ushort)(opCodeSize)).ToHex(decoderAddrMode.Size);
                    decoderClkCyc += (byte)5;
                }
                else
                {
                    opCodeASM = "TEST " + indASM + ", " + DecoderParam(ParamIndex.First, (ushort)(opCodeSize)).ToHex(decoderAddrMode.Size);
                    decoderClkCyc += (byte)11;
                }
            } // 010    --  not
            else if (decoderAddrMode.Reg == ((byte)2))
            {
                if (decoderAddrMode.IsDirect)
                {
                    opCodeASM = "NOT " + decoderAddrMode.Register2.ToString();
                    decoderClkCyc += (byte)3;
                }
                else
                {
                    opCodeASM = "NOT " + indASM;
                    decoderClkCyc += (byte)16;
                }
            } // 010    --  neg
            else if (decoderAddrMode.Reg == ((byte)3))
            {
                if (decoderAddrMode.IsDirect)
                {
                    opCodeASM = "NEG " + decoderAddrMode.Register2.ToString();
                    decoderClkCyc += (byte)3;
                }
                else
                {
                    opCodeASM = "NEG " + indASM;
                    decoderClkCyc += (byte)16;
                }
            } // 100    --  mul
            else if (decoderAddrMode.Reg == ((byte)4))
            {
                if (decoderAddrMode.IsDirect)
                {
                    opCodeASM = "MUL " + decoderAddrMode.Register2.ToString();
                    decoderClkCyc += (byte)(decoderAddrMode.Size == DataSize.Byte ? 70 : 118);
                }
                else
                {
                    opCodeASM = "MUL " + indASM;
                    decoderClkCyc += (byte)(decoderAddrMode.Size == DataSize.Byte ? 76 : 124);
                }
            } // 101    --  imul
            else if (decoderAddrMode.Reg == ((byte)5))
            {
                if (decoderAddrMode.IsDirect)
                {
                    opCodeASM = "IMUL " + decoderAddrMode.Register2.ToString();
                    decoderClkCyc += (byte)(decoderAddrMode.Size == DataSize.Byte ? 80 : 128);
                }
                else
                {
                    opCodeASM = "IMUL " + indASM;
                    decoderClkCyc += (byte)(decoderAddrMode.Size == DataSize.Byte ? 86 : 134);
                }
            } // 110    --  div
            else if (decoderAddrMode.Reg == ((byte)6))
            {
                if (decoderAddrMode.IsDirect)
                {
                    opCodeASM = "DIV " + decoderAddrMode.Register2.ToString();
                    decoderClkCyc += (byte)(decoderAddrMode.Size == DataSize.Byte ? 80 : 144);
                }
                else
                {
                    opCodeASM = "DIV " + indASM;
                    decoderClkCyc += (byte)(decoderAddrMode.Size == DataSize.Byte ? 86 : 168);
                }
            } // 111    --  idiv
            else if (decoderAddrMode.Reg == ((byte)7))
            {
                int div = mRegisters.get_Val(decoderAddrMode.Register2);
                if (decoderAddrMode.IsDirect)
                {
                    opCodeASM = "IDIV " + decoderAddrMode.Register2.ToString();
                    decoderClkCyc += (byte)(decoderAddrMode.Size == DataSize.Byte ? 101 : 165);
                }
                else
                {
                    opCodeASM = "IDIV " + indASM;
                    decoderClkCyc += (byte)(decoderAddrMode.Size == DataSize.Byte ? 107 : 171);
                }
            }
        }

        private void DecodeGroup4_And_5()
        {
            SetDecoderAddressing();

            if (decoderAddrMode.Reg == ((byte)0)) // 000    --  inc reg/mem
            {
                if (decoderAddrMode.IsDirect)
                {
                    opCodeASM = "INC " + decoderAddrMode.Register2.ToString();
                    decoderClkCyc += (byte)3;
                }
                else
                {
                    opCodeASM = "INC " + indASM;
                    decoderClkCyc += (byte)15;
                }
            } // 001    --  dec reg/mem
            else if (decoderAddrMode.Reg == ((byte)1))
            {
                if (decoderAddrMode.IsDirect)
                {
                    opCodeASM = "DEC " + decoderAddrMode.Register2.ToString();
                    decoderClkCyc += (byte)3;
                }
                else
                {
                    opCodeASM = "DEC " + indASM;
                    decoderClkCyc += (byte)15;
                }
            } // 010    --  call indirect within segment
            else if (decoderAddrMode.Reg == ((byte)2))
            {
                if (decoderAddrMode.IsDirect)
                {
                    opCodeASM = "CALL " + decoderAddrMode.Register2.ToString();
                }
                else
                {
                    opCodeASM = "CALL " + indASM;
                }
                decoderClkCyc += (byte)11;
            } // 011    --  call indirect intersegment
            else if (decoderAddrMode.Reg == ((byte)3))
            {
                if (decoderAddrMode.IsDirect)
                {
                    opCodeASM = "CALL " + decoderAddrMode.Register2.ToString() + " {NOT IMPLEMENTED}";
                }
                else
                {
                    opCodeASM = "CALL " + indASM;
                }

                decoderClkCyc += (byte)37;
            } // 100    --  jmp indirect within segment
            else if (decoderAddrMode.Reg == ((byte)4))
            {
                if (decoderAddrMode.IsDirect)
                {
                    opCodeASM = "JMP " + decoderAddrMode.Register2.ToString();
                }
                else
                {
                    opCodeASM = "JMP " + indASM;
                }
                decoderClkCyc += (byte)15;
            } // 101    --  jmp indirect intersegment
            else if (decoderAddrMode.Reg == ((byte)5))
            {
                if (decoderAddrMode.IsDirect)
                {
                    opCodeASM = "JMP " + decoderAddrMode.Register2.ToString() + " {NOT IMPLEMENTED}";
                }
                else
                {
                    opCodeASM = "JMP " + indASM;
                }
                decoderClkCyc += (byte)24;
            } // 110    --  push reg/mem
            else if (decoderAddrMode.Reg == ((byte)6))
            {
                opCodeASM = "PUSH " + indASM;
                decoderClkCyc += (byte)16;
            } // 111    --  BIOS DI
            else if (decoderAddrMode.Reg == ((byte)7))
            {
                opCodeASM = "BIOS DI";
                opCodeSize = (byte)2;
                decoderClkCyc += (byte)0;
            }
        }

        private void SetDecoderAddressing(DataSize forceSize = X8086.DataSize.UseAddressingMode)
        {
#if DEBUG
            decoderAddrMode.Decode(opCode, get_RAM8(mRegisters.CS, (ushort)(mRegisters.IP + 1)));
#else
			decoderAddrMode = decoderCache[(opCode << 8) | get_RAM8(mRegisters.CS, (ushort) (mRegisters.IP + 1))];
#endif

            if (forceSize != DataSize.UseAddressingMode)
            {
                decoderAddrMode.Size = forceSize;
            }

            // AS = SS when Rm = 2 or 3
            // If Rm = 6, AS will be set to SS, except for Modifier = 0
            // http://www.ic.unicamp.br/~celio/mc404s2-03/addr_modes/intel_addr.html

            if (!mRegisters.ActiveSegmentChanged)
            {
                if ((decoderAddrMode.Rm == ((byte)2)) || (decoderAddrMode.Rm == ((byte)3)))
                {
                    mRegisters.ActiveSegmentRegister = GPRegisters.RegistersTypes.SS;
                }
                else if (decoderAddrMode.Rm == ((byte)6))
                {
                    if (decoderAddrMode.Modifier != 0)
                    {
                        mRegisters.ActiveSegmentRegister = GPRegisters.RegistersTypes.SS;
                    }
                }
            }

            // http://umcs.maine.edu/~cmeadow/courses/cos335/Asm07-MachineLanguage.pdf
            // http://maven.smith.edu/~thiebaut/ArtOfAssembly/CH04/CH04-2.html#HEADING2-35
            if (decoderAddrMode.Modifier == ((byte)0)) // 00
            {
                decoderAddrMode.IsDirect = false;
                if (decoderAddrMode.Rm == ((byte)0))
                {
                    decoderAddrMode.IndAdr = (ushort)(mRegisters.BX + mRegisters.SI);
                    indASM = "[BX + SI]";
                    decoderClkCyc += (byte)7; // 000 [BX+SI]
                }
                else if (decoderAddrMode.Rm == ((byte)1))
                {
                    decoderAddrMode.IndAdr = (ushort)(mRegisters.BX + mRegisters.DI);
                    indASM = "[BX + DI]";
                    decoderClkCyc += (byte)8; // 001 [BX+DI]
                }
                else if (decoderAddrMode.Rm == ((byte)2))
                {
                    decoderAddrMode.IndAdr = (ushort)(mRegisters.BP + mRegisters.SI);
                    indASM = "[BP + SI]";
                    decoderClkCyc += (byte)8; // 010 [BP+SI]
                }
                else if (decoderAddrMode.Rm == ((byte)3))
                {
                    decoderAddrMode.IndAdr = (ushort)(mRegisters.BP + mRegisters.DI);
                    indASM = "[BP + DI]";
                    decoderClkCyc += (byte)7; // 011 [BP+DI]
                }
                else if (decoderAddrMode.Rm == ((byte)4))
                {
                    decoderAddrMode.IndAdr = mRegisters.SI;
                    indASM = "[SI]";
                    decoderClkCyc += (byte)5; // 100 [SI]
                }
                else if (decoderAddrMode.Rm == ((byte)5))
                {
                    decoderAddrMode.IndAdr = mRegisters.DI;
                    indASM = "[DI]";
                    decoderClkCyc += (byte)5; // 101 [DI]
                } // 110 Direct Addressing
                else if (decoderAddrMode.Rm == ((byte)6))
                {
                    decoderAddrMode.IndAdr = DecoderParamNOPS(ParamIndex.First, (ushort)2, DataSize.Word);
                    indASM = "[" + DecoderParamNOPS(ParamIndex.First, (ushort)2, DataSize.Word).ToString("X4") + "]";
                    opCodeSize += (byte)2;
                    decoderClkCyc += (byte)9;
                }
                else if (decoderAddrMode.Rm == ((byte)7))
                {
                    decoderAddrMode.IndAdr = mRegisters.BX;
                    indASM = "[BX]";
                    decoderClkCyc += (byte)5; // 111 [BX]
                }
                decoderAddrMode.IndMem = get_RAMn();
            } // 01 - 8bit
            else if (decoderAddrMode.Modifier == ((byte)1))
            {
                decoderAddrMode.IsDirect = false;
                if (decoderAddrMode.Rm == ((byte)0))
                {
                    decoderAddrMode.IndAdr = (ushort)(mRegisters.BX + mRegisters.SI);
                    indASM = "[BX + SI]";
                    decoderClkCyc += (byte)7; // 000 [BX+SI]
                }
                else if (decoderAddrMode.Rm == ((byte)1))
                {
                    decoderAddrMode.IndAdr = (ushort)(mRegisters.BX + mRegisters.DI);
                    indASM = "[BX + DI]";
                    decoderClkCyc += (byte)8; // 001 [BX+DI]
                }
                else if (decoderAddrMode.Rm == ((byte)2))
                {
                    decoderAddrMode.IndAdr = (ushort)(mRegisters.BP + mRegisters.SI);
                    indASM = "[BP + SI]";
                    decoderClkCyc += (byte)8; // 010 [BP+SI]
                }
                else if (decoderAddrMode.Rm == ((byte)3))
                {
                    decoderAddrMode.IndAdr = (ushort)(mRegisters.BP + mRegisters.DI);
                    indASM = "[BP + DI]";
                    decoderClkCyc += (byte)7; // 011 [BP+DI]
                }
                else if (decoderAddrMode.Rm == ((byte)4))
                {
                    decoderAddrMode.IndAdr = mRegisters.SI;
                    indASM = "[SI]";
                    decoderClkCyc += (byte)5; // 100 [SI]
                }
                else if (decoderAddrMode.Rm == ((byte)5))
                {
                    decoderAddrMode.IndAdr = mRegisters.DI;
                    indASM = "[DI]";
                    decoderClkCyc += (byte)5; // 101 [DI]
                }
                else if (decoderAddrMode.Rm == ((byte)6))
                {
                    decoderAddrMode.IndAdr = mRegisters.BP;
                    indASM = "[BP]";
                    decoderClkCyc += (byte)5; // 110 [BP]
                }
                else if (decoderAddrMode.Rm == ((byte)7))
                {
                    decoderAddrMode.IndAdr = mRegisters.BX;
                    indASM = "[BX]";
                    decoderClkCyc += (byte)5; // 111 [BX]
                }

                byte p = (byte)DecoderParamNOPS(ParamIndex.First, (ushort)2, DataSize.Byte);
                int s = 0;
                if (p > 0x80)
                {
                    p = (byte)(0x100 - p);
                    s = -1;
                }
                else
                {
                    s = 1;
                }
                indASM = System.Convert.ToString(indASM.Replace("]", System.Convert.ToString((s == -1 ? " - " : " + ") + p.ToString("X2") + "]")));
                decoderAddrMode.IndAdr += (ushort)(s * p);
                decoderAddrMode.IndMem = get_RAMn();
                opCodeSize++;
            } // 10 - 16bit
            else if (decoderAddrMode.Modifier == ((byte)2))
            {
                decoderAddrMode.IsDirect = false;
                if (decoderAddrMode.Rm == ((byte)0))
                {
                    decoderAddrMode.IndAdr = (ushort)(mRegisters.BX + mRegisters.SI);
                    indASM = "[BX + SI]";
                    decoderClkCyc += (byte)7; // 000 [BX+SI]
                }
                else if (decoderAddrMode.Rm == ((byte)1))
                {
                    decoderAddrMode.IndAdr = (ushort)(mRegisters.BX + mRegisters.DI);
                    indASM = "[BX + DI]";
                    decoderClkCyc += (byte)8; // 001 [BX+DI]
                }
                else if (decoderAddrMode.Rm == ((byte)2))
                {
                    decoderAddrMode.IndAdr = (ushort)(mRegisters.BP + mRegisters.SI);
                    indASM = "[BP + SI]";
                    decoderClkCyc += (byte)8; // 010 [BP+SI]
                }
                else if (decoderAddrMode.Rm == ((byte)3))
                {
                    decoderAddrMode.IndAdr = (ushort)(mRegisters.BP + mRegisters.DI);
                    indASM = "[BP + DI]";
                    decoderClkCyc += (byte)7; // 011 [BP+DI]
                }
                else if (decoderAddrMode.Rm == ((byte)4))
                {
                    decoderAddrMode.IndAdr = mRegisters.SI;
                    indASM = "[SI]";
                    decoderClkCyc += (byte)5; // 100 [SI]
                }
                else if (decoderAddrMode.Rm == ((byte)5))
                {
                    decoderAddrMode.IndAdr = mRegisters.DI;
                    indASM = "[DI]";
                    decoderClkCyc += (byte)5; // 101 [DI]
                }
                else if (decoderAddrMode.Rm == ((byte)6))
                {
                    decoderAddrMode.IndAdr = mRegisters.BP;
                    indASM = "[BP]";
                    decoderClkCyc += (byte)5; // 110 [BP]
                }
                else if (decoderAddrMode.Rm == ((byte)7))
                {
                    decoderAddrMode.IndAdr = mRegisters.BX;
                    indASM = "[BX]";
                    decoderClkCyc += (byte)5; // 111 [BX]
                }

                indASM = indASM.Replace("]", " + " + DecoderParamNOPS(ParamIndex.First, (ushort)2, DataSize.Word).ToString("X4") + "]");
                decoderAddrMode.IndAdr += DecoderParamNOPS(ParamIndex.First, (ushort)2, DataSize.Word);
                decoderAddrMode.IndMem = get_RAMn();
                opCodeSize += (byte)2;
            } // 11
            else if (decoderAddrMode.Modifier == ((byte)3))
            {
                decoderAddrMode.IsDirect = true;
            }
            opCodeSize++;
        }

        private void DecoderSetRegister1Alt(byte data)
        {
            //decoderAddrMode.Register1 = (GPRegisters.RegistersTypes)(data & 0x7) | shl3;
            decoderAddrMode.Register1 = (GPRegisters.RegistersTypes)((data & 0x7) | shl3); //TODO: works?
            if (decoderAddrMode.Register1 >= GPRegisters.RegistersTypes.ES)
            {
                decoderAddrMode.Register1 += (int)GPRegisters.RegistersTypes.ES;
            }
            decoderAddrMode.Size = DataSize.Word;
        }

        private void DecoderSetRegister2ToSegReg()
        {
            decoderAddrMode.Register2 = (GPRegisters.RegistersTypes)(decoderAddrMode.Reg + (int)GPRegisters.RegistersTypes.ES);
            decoderAddrMode.Size = DataSize.Word;
        }

        private ushort DecoderParam(ParamIndex index, ushort ipOffset = 1, DataSize size = X8086.DataSize.UseAddressingMode)
        {
            if (size == DataSize.UseAddressingMode)
            {
                size = decoderAddrMode.Size;
            }
            opCodeSize += (byte)(size + 1);
            return DecoderParamNOPS(index, ipOffset, size);
        }

        private ushort DecoderParamNOPS(ParamIndex index, ushort ipOffset = 1, DataSize size = X8086.DataSize.UseAddressingMode)
        {
            // Extra cycles for address misalignment
            // This is too CPU expensive, with few benefits, if any... not worth it
            //If (mRegisters.IP Mod 2) <> 0 Then clkCyc += 4

            return (ushort)((size == DataSize.Byte || (size == DataSize.UseAddressingMode && decoderAddrMode.Size == DataSize.Byte)) ? (
                get_RAM8(mRegisters.CS, mRegisters.IP, (byte)(ipOffset + index), true)) : (
                get_RAM16(mRegisters.CS, mRegisters.IP, (byte)(ipOffset + (int)index * 2), true)));
        }
    }
}
