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
    static class Extensions
    {
        public static byte LowByte(this int value)
        {
            return (byte)(value & 0xFF);
        }

        public static byte HighByte(this int value)
        {
            return (byte)(value >> 8);
        }

        public static byte LowNib(this byte value)
        {
            return (byte)(value & 0xF);
        }

        public static byte HighNib(this byte value)
        {
            return (byte)(value >> 4);
        }

        public static string ToBinary(this byte value)
        {
            return Convert.ToString(value, 2).PadLeft(8, '0');
        }

        public static string ToBinary(this int value)
        {
            return Convert.ToString(value, 2).PadLeft(16, '0');
        }

        public static string ToBinary(this X8086.GPRegisters.RegistersTypes value)
        {
            //return Convert.ToString(value, 2);
            return Convert.ToString((int)value, 2);
        }

        public static int ToBCD(this int value)
        {
            int v = 0;
            int r = 0;

            for (int i = 0; i <= 4 - 1; i++)
            {
                v = value % 10;
                value /= 10;
                v = v | ((value % 10) << 4);
                value /= 10;

                r += v << (4 * i);
            }

            return r;
        }

        public static string ToHex(this ushort value)
        {
            return value.ToString("X4");
        }

        public static string ToHex(this ushort value, X8086.DataSize size)
        {
            if (size == X8086.DataSize.Byte)
            {
                return value.ToString("X2");
            }
            else
            {
                return value.ToString("X4");
            }
        }
    }

}
