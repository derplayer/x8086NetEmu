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
    public class Binary
    {
        public enum Sizes
        {
            Bit = 1,
            Nibble = 4,
            @Byte = 8,
            Word = 16,
            DoubleWord = 32,
            QuadWord = 64,
            //DoubleQuadWord = 128
            Undefined = -1
        }

        private readonly long binaryValue;

        public Sizes Size { get; set; }

        public Binary()
        {
            Size = Sizes.Word;
        }

        public Binary(long value, Sizes size = Binary.Sizes.Undefined) : this()
        {
            binaryValue = Math.Abs(value);
            if (size == Sizes.Undefined)
            {
                CalculateMinimumSize();
            }
            else
            {
                this.Size = size;
                binaryValue = binaryValue & Mask(size);
            }
        }

        public Binary(string value, Sizes size = Binary.Sizes.Undefined)
        {
            Binary binValue = (Binary)0;
            long result = binValue;

            TryParse(value, ref result);
            binValue = (Binary)result;
            binaryValue = binValue;

            if (size == Sizes.Undefined)
            {
                this.Size = binValue.Size;
            }
            else
            {
                this.Size = Sizes.Word;
            }
        }

        public static bool TryParse(string value, ref long result)
        {
            try
            {
                if (value.Last() == 'd')
                {
                    result = long.Parse(value.TrimEnd("d".ToCharArray()));
                    return true;
                }
                else if (value.Last() == 'h')
                {
                    result = Convert.ToInt32(value.TrimEnd("h".ToCharArray()), 16);
                    return true;
                }
                else if (value.Last() == 'b')
                {
                    result = Convert.ToInt32(value.TrimEnd("b".ToCharArray()), 2);
                    return true;
                }
                else if (value.Last() == 'o')
                {
                    result = Convert.ToInt32(value.TrimEnd("o".ToCharArray()), 8);
                    return true;
                }
                else
                {
                    int @base = 2;
                    foreach (char c in value)
                    {
                        if (c != '0' && c != '1')
                        {
                            if (c >= 'A' && c <= 'F')
                            {
                                @base = 16;
                            }
                            else if (c < '0' || c > 'F')
                            {
                                @base = -1;
                                break;
                            }
                            else if (@base != 16)
                            {
                                @base = 10;
                            }
                        }
                    }

                    if (@base == -1)
                    {
                        return false;
                    }
                    else
                    {
                        result = Convert.ToInt32(value, @base);
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        public static explicit operator Binary(long value)
        {
            return new Binary(value);
        }

        public static explicit operator Binary(int value)
        {
            return new Binary((uint)(Math.Abs(value)));
        }

        //public static explicit operator Binary (string value)
        //{
        //	Binary result = 0;
        //	Binary.TryParse(value, out result);
        //	return result;
        //}

        public static explicit operator Binary(string value)
        {
            Binary result = (Binary)0;
            long result2 = result;
            TryParse(value, ref result2);
            return (Binary)result2;
        }

        public static implicit operator long(Binary value)
        {
            return value.ToLong();
        }

        public static bool operator ==(Binary value1, Binary value2)
        {
            return value1.ToLong() == value2.ToLong();
        }

        public static bool operator !=(Binary value1, Binary value2)
        {
            return !(value1 == value2);
        }

        public static bool operator >(Binary value1, Binary value2)
        {
            return value1.ToLong() > value2.ToLong();
        }

        public static bool operator <(Binary value1, Binary value2)
        {
            return !(value1 > value2);
        }

        public static bool operator >=(Binary value1, Binary value2)
        {
            return value1.ToLong() >= value2.ToLong();
        }

        public static bool operator <=(Binary value1, Binary value2)
        {
            return !(value1 >= value2);
        }

        public static Binary operator +(Binary value1, Binary value2)
        {
            return AdjustSize((long)(value1.ToLong() + value2.ToLong()), value1.Size);
        }

        public static Binary operator -(Binary value1, Binary value2)
        {
            return AdjustSize((long)(value1.ToLong() - value2.ToLong()), value1.Size);
        }

        public static Binary operator *(Binary value1, Binary value2)
        {
            return AdjustSize((long)(value1.ToLong() * value2.ToLong()), value1.Size);
        }

        public static Binary operator /(Binary value1, Binary value2)
        {
            return AdjustSize((long)(value1.ToLong() / value2.ToLong()), value1.Size);
        }

        //public static Binary operator \(Binary value1, Binary value2)
        //{
        //	return value1 / value2;
        //}
        public static Binary op_IntegerDivision(Binary value1, Binary value2)
        {
            return value1 / value2;
        }

        public static Binary operator ^(Binary value1, Binary value2)
        {
            return AdjustSize((long)(Math.Pow(value1.ToLong(), value2.ToLong())), value1.Size);
        }

        public static Binary operator %(Binary value1, Binary value2)
        {
            return AdjustSize((long)(value1.ToLong() % value2.ToLong()), value1.Size);
        }

        public static Binary operator &(Binary value1, Binary value2)
        {
            return (Binary)(value1.ToLong() & value2.ToLong());
        }

        public static Binary operator |(Binary value1, Binary value2)
        {
            return (Binary)(value1.ToLong() | value2.ToLong());
        }

        //public static Binary operator ^(Binary value1, Binary value2)
        //{
        //	return value1.ToLong() ^ value2.ToLong();
        //}

        public static Binary operator !(Binary value1)
        {
            return AdjustSize(~value1.ToLong(), value1.Size);
        }

        public static Binary operator <<(Binary value1, int value2)
        {
            return AdjustSize((long)(value1.ToLong() << value2), value1.Size);
        }

        public static Binary operator >>(Binary value1, int value2)
        {
            return AdjustSize((long)(value1.ToLong() >> value2), value1.Size);
        }

        public static Binary From(string value, Sizes size = Binary.Sizes.Undefined)
        {
            return new Binary(value, size);
        }

        public static Binary From(long value, Sizes size = Binary.Sizes.Undefined)
        {
            return new Binary(value, size);
        }

        public static Binary From(int value, Sizes size = Binary.Sizes.Undefined)
        {
            return Binary.From(System.Convert.ToString((uint)(Math.Abs(value))), size);
        }

        private static Binary AdjustSize(long value, Sizes size)
        {
            return new Binary(value & Mask(size), size);
        }

        private static long Mask(Sizes size)
        {
            //return (Math.Pow(2, size)) - 1;
            return (long)Math.Round(Math.Pow(2.0, (double)size) - 1.0);
        }

        public long ToLong()
        {
            return binaryValue;
        }

        public override string ToString()
        {
            return ConvertToBase((short)2).PadLeft((System.Int32)Size, '0');
        }

        public string ToHex()
        {
            return ConvertToBase((short)16);
        }

        public string ToOctal()
        {
            return ConvertToBase((short)8);
        }

        private void CalculateMinimumSize()
        {
            if (binaryValue <= Math.Pow(2, 8))
            {
                Size = Sizes.Byte;
            }
            else if (binaryValue <= Math.Pow(2, 16))
            {
                Size = Sizes.Word;
            }
            else if (binaryValue <= Math.Pow(2, 32))
            {
                Size = Sizes.DoubleWord;
            }
            else if (binaryValue <= Math.Pow(2, 64))
            {
                Size = Sizes.QuadWord;
            }
            else
            {
                throw (new OverflowException());
            }
        }

        private string ConvertToBase(short @base)
        {
            if (Size <= Sizes.DoubleWord)
            {
                return Convert.ToString((int)(binaryValue), @base).ToUpper();
            }
            else
            {
                if (@base == 10)
                {
                    return binaryValue.ToString();
                }
                else
                {
                    string result = "";

                    long i = 0;
                    long r = 0;
                    long n = binaryValue;
                    do
                    {
                        i = n / @base;
                        r = (long)(n - (int)(i * @base));
                        result = Convert.ToString(r, @base) + result;
                        n = i;
                    } while (n > 0);

                    return result;
                }
            }
        }
    }
}
