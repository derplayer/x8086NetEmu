using System.Collections.Generic;
using System;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Xml.Linq;
using System.Collections;
using System.Windows.Forms;
using System.Threading;

using x8086SharpEmu;

namespace x8086SharpEmu
{
    //
    // ConsoleCrayon.cs
    //
    // Author:
    //   Aaron Bockover <abockover@novell.com>
    //
    // Copyright (C) 2008 Novell, Inc.
    //
    // Permission is hereby granted, free of charge, to any person obtaining
    // a copy of this software and associated documentation files (the
    // "Software"), to deal in the Software without restriction, including
    // without limitation the rights to use, copy, modify, merge, publish,
    // distribute, sublicense, and/or sell copies of the Software, and to
    // permit persons to whom the Software is furnished to do so, subject to
    // the following conditions:
    //
    // The above copyright notice and this permission notice shall be
    // included in all copies or substantial portions of the Software.
    //
    // THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
    // EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
    // MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
    // NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
    // LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
    // OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
    // WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
    //

    // Modifications and implementation of a simple hyper-text langage by Xavier Flix | 2013
    // fc: Fore Color
    // bc: Back Color
    //
    // Example:
    // ConsoleCrayon.WriteToConsole("<bc:Red><fc:Gray>Hello World</fc></bc>")
    //
    // Documentation: http://ascii-table.com/ansi-escape-sequences.php

    public static class ConsoleCrayon
    {
        #region Public API
        public enum TextAlignment
        {
            Left,
            Center,
            Right
        }

        public const double toRadians = Math.PI / 180;
        public const double toDegrees = 180 / Math.PI;

        public static object SyncObject = new object();

        public static void WriteFast(string text, ConsoleColor foreColor, ConsoleColor backColor, int col, int row)
        {
            lock (SyncObject)
            {
                if (col < 0 || col >= Console.WindowWidth ||
                        row < 0 || row >= Console.WindowHeight)
                {
                    return;
                }

                if (ConsoleCrayon.XtermColors)
                {
                    Console.Write(ESC + (row + 1).ToString() + ";" + (col + 1).ToString() + "H" +
                        GetAnsiColorControlCode(foreColor, true) +
                        GetAnsiColorControlCode(backColor, false) +
                        text);
                }
                else
                {
                    if (Console.CursorLeft != col)
                    {
                        Console.CursorLeft = col;
                    }
                    if (Console.CursorTop != row)
                    {
                        Console.CursorTop = row;
                    }

                    if (foreColor != Console.ForegroundColor)
                    {
                        Console.ForegroundColor = foreColor;
                    }
                    if (backColor != Console.BackgroundColor)
                    {
                        Console.BackgroundColor = backColor;
                    }

                    int index = col + row * Console.WindowWidth;
                    int size = Console.WindowWidth * Console.WindowHeight;
                    if (index + text.Length >= size)
                    {
                        text = text.Substring(0, size - index - 1);
                    }

                    Console.Write(text);
                }
            }
        }

        public static ConsoleColor ForegroundColor
        {
            get
            {
                return Console.ForegroundColor;
            }
            set
            {
                if (Console.ForegroundColor != value)
                {
                    SetColor(value, true);
                }
            }
        }

        public static ConsoleColor BackgroundColor
        {
            get
            {
                return Console.BackgroundColor;
            }
            set
            {
                if (Console.BackgroundColor != value)
                {
                    SetColor(value, false);
                }
            }
        }

        public static void RemoveScrollbars()
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                case PlatformID.Win32S:
                case PlatformID.Win32Windows:
                case PlatformID.WinCE:
                    Console.BufferWidth = Console.WindowWidth;
                    Console.BufferHeight = Console.WindowHeight;
                    break;
            }
        }

        public static void ResetColor()
        {
            if (XtermColors)
            {
                Console.Write(ColorReset);
            }
            else if (Environment.OSVersion.Platform != PlatformID.Unix &&
                           !RuntimeIsMono)
            {
                Console.ResetColor();
            }
        }

        private static void SetColor(ConsoleColor color, bool isForeground)
        {
            if (color < ConsoleColor.Black || color > ConsoleColor.White)
            {
                throw (new ArgumentOutOfRangeException("color", "Not a ConsoleColor value"));
            }

            if (XtermColors)
            {
                Console.Write(GetAnsiColorControlCode(color, isForeground));
            }
            else if ((int)Environment.OSVersion.Platform != (int)PlatformID.Unix && !RuntimeIsMono)
            {
                if (isForeground)
                {
                    Console.ForegroundColor = color;
                }
                else
                {
                    Console.BackgroundColor = color;
                }
            }
        }

        delegate void delegate_WriteText();

        private static string WriteText(string textBuffer)
        {
            if (textBuffer != "")
            {
                Console.Write(textBuffer);
                textBuffer = "";
                return textBuffer;
            }

            return textBuffer;
        }

        public static void WriteToConsole(string text, bool addNewLine = true)
        {
            string textBuffer = "";
            string tmpText;

            //var WriteText = () =>
            //{
            //    if (textBuffer != "")
            //    {
            //        Console.Write(textBuffer);
            //        textBuffer = "";
            //    }
            //};

            for (int i = 0; i <= text.Length - 1; i++)
            {
                if (i + 4 <= text.Length)
                    tmpText = text.Substring(i, 4);
                else
                    tmpText = text[i].ToString();

                switch (tmpText)
                {
                    case "<fc:":
                        {
                            textBuffer = WriteText(textBuffer);
                            i += SetColorFrom(text.Substring(i + 4), true) + 4;
                            break;
                        }

                    case "<bc:":
                        {
                            textBuffer = WriteText(textBuffer);
                            i += SetColorFrom(text.Substring(i + 4), false) + 4;
                            break;
                        }

                    case "/fc>":
                    case "/bc>":
                        {
                            textBuffer = textBuffer.TrimEnd('<');
                            textBuffer = WriteText(textBuffer);
                            ResetColor();
                            i += 3;
                            break;
                        }

                    default:
                        {
                            textBuffer += text[i];
                            break;
                        }
                }
            }

            textBuffer = WriteText(textBuffer);
            if (addNewLine)
                Console.WriteLine("");
        }

        public static void DrawLine(char c, int fromCol, int fromRow, int toCol, int toRow, ConsoleColor foreColor, ConsoleColor backColor)
        {
            double angle = Atan2(toCol - fromCol, toRow - fromRow);
            int length = System.Convert.ToInt32(Math.Sqrt(Math.Pow((toCol - fromCol), 2) + Math.Pow((toRow - fromRow), 2)));
            int px = 0;
            int py = 0;
            double ca = Math.Cos(angle * toRadians);
            double sa = Math.Sin(angle * toRadians);

            for (int radius = 0; radius <= length; radius++)
            {
                px = System.Convert.ToInt32(radius * ca + fromCol);
                py = System.Convert.ToInt32(radius * sa + fromRow);

                ConsoleCrayon.WriteFast(c.ToString(), foreColor, backColor, px, py);
            }
        }

        private static int SetColorFrom(string data, bool isForeground)
        {
            var colorName = data.Substring(0, data.IndexOf(">"));
            ConsoleColor c = default(ConsoleColor);
            if (Enum.TryParse<ConsoleColor>(colorName, out c))
            {
                SetColor(c, isForeground);
            }
            return data.IndexOf(">", colorName.Length);
        }

        private static double Atan2(double dx, double dy)
        {
            double a = 0;

            if (dy == 0)
            {
                a = System.Convert.ToDouble(dx > 0 ? 0 : 180);
            }
            else
            {
                a = Math.Atan(dy / dx) * toDegrees;
                if (a > 0)
                {
                    if (dx < 0 && dy < 0)
                    {
                        a += 180;
                    }
                }
                else if (a == 0)
                {
                    if (dx < 0)
                    {
                        a = 180;
                    }
                }
                else if (a < 0)
                {
                    if (dy > 0)
                    {
                        if (dx > 0)
                        {
                            a = Math.Abs(a);
                        }
                        else
                        {
                            a += 180;
                        }
                    }
                    else
                    {
                        a += 360;
                    }
                }
            }

            return a;
        }

        public static string PadText(string text, int width, TextAlignment alignment = ConsoleCrayon.TextAlignment.Left)
        {
            if (text.Length == width)
            {
                return text;
            }
            else if (width < 0)
            {
                return "";
            }
            else if (text.Length > width)
            {
                return text.Substring(0, width);
            }
            else
            {
                switch (alignment)
                {
                    case TextAlignment.Left:
                        return text + new string(' ', width - text.Length);
                    case TextAlignment.Right:
                        return new string(' ', width - text.Length) + text;
                    case TextAlignment.Center:
                        return string.Format("{0}{1}{0}", new string(' ', System.Convert.ToInt32((width - text.Length) / 2)), text);
                    default:
                        return text;
                }
            }
        }
        #endregion

        #region Ansi/VT Code Calculation
        // Modified from Mono's System.TermInfoDriver
        // License: MIT/X11
        // Authors: Gonzalo Paniagua Javier <gonzalo@ximian.com>
        // (C) 2005-2006 Novell, Inc <http://www.novell.com>

        private static int TranslateColor(ConsoleColor desired, ref bool light)
        {
            light = false;
            switch (desired)
            {
                // Dark colors
                case ConsoleColor.Black:
                    return 0;
                case ConsoleColor.DarkRed:
                    return 1;
                case ConsoleColor.DarkGreen:
                    return 2;
                case ConsoleColor.DarkYellow:
                    return 3;
                case ConsoleColor.DarkBlue:
                    return 4;
                case ConsoleColor.DarkMagenta:
                    return 5;
                case ConsoleColor.DarkCyan:
                    return 6;
                case ConsoleColor.Gray:
                    return 7;

                // Light colors
                case ConsoleColor.DarkGray:
                    light = true;
                    return 0;
                case ConsoleColor.Red:
                    light = true;
                    return 1;
                case ConsoleColor.Green:
                    light = true;
                    return 2;
                case ConsoleColor.Yellow:
                    light = true;
                    return 3;
                case ConsoleColor.Blue:
                    light = true;
                    return 4;
                case ConsoleColor.Magenta:
                    light = true;
                    return 5;
                case ConsoleColor.Cyan:
                    light = true;
                    return 6;
                default: // ConsoleColor.White
                    light = true;
                    return 7;
            }
        }

        private static string ESC = (char)27 + "[";
        private static string ColorReset = ESC + "0m";
        private static string GetAnsiColorControlCode(ConsoleColor color, bool isForeground)
        {
            // lighter fg colours are 90 -> 97 rather than 30 -> 37
            // lighter bg colours are 100 -> 107 rather than 40 -> 47

            bool light = false;
            int code = System.Convert.ToInt32(TranslateColor(color, ref light) + (isForeground ? 30 : 40) + (light ? 60 : 0));

            return ESC + code.ToString() + "m";
        }
        #endregion

        #region xterm Detection
        private static bool? xterm_colors = null;

        public static bool XtermColors
        {
            get
            {
                if (ReferenceEquals(xterm_colors, null))
                {
                    DetectXtermColors();
                }
                return xterm_colors.Value;
            }
        }

        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "isatty")]
        private static extern int _isTty(int fd);

        private static bool IsTty(int fd)
        {
            try
            {
                return _isTty(fd) == 1;
            }
            catch
            {
                return false;
            }
        }

        private static void DetectXtermColors()
        {
            bool _xterm_colors = false;

            var term = Environment.GetEnvironmentVariable("TERM");
            if (ReferenceEquals(term, null))
            {
                term = "";
            }
            if (term.StartsWith("xterm"))
            {
                term = "xterm";
            }

            switch (term)
            {
                case "xterm":
                case "rxvt":
                case "rxvt-unicode":
                    //If Environment.GetEnvironmentVariable("COLORTERM") IsNot Nothing Then
                    _xterm_colors = true;
                    break;
                //End If
                case "xterm-color":
                    _xterm_colors = true;
                    break;
                case "linux":
                    _xterm_colors = true;
                    break;
            }

            xterm_colors = _xterm_colors && IsTty(1) && IsTty(2);
        }

        #endregion

        #region Runtime Detection
        private static bool? runtime_is_mono;
        public static bool RuntimeIsMono
        {
            get
            {
                if (runtime_is_mono == null)
                {
                    runtime_is_mono = Type.GetType("System.MonoType") != null;
                }

                return runtime_is_mono.Value;
            }
        }
        #endregion

        #region Tests
        public static void Test()
        {
            TestSelf();
            Console.WriteLine();
            TestAnsi();
            Console.WriteLine();
            TestRuntime();
        }

        private static void TestSelf()
        {
            Console.WriteLine("==SELF TEST==");
            foreach (ConsoleColor color in Enum.GetValues(typeof(ConsoleColor)))
            {
                ForegroundColor = color;
                Console.Write(color);
                ResetColor();
                Console.Write(" :: ");
                BackgroundColor = color;
                Console.Write(color);
                ResetColor();
                Console.WriteLine();
            }
        }

        private static void TestAnsi()
        {
            Console.WriteLine("==ANSI TEST==");
            foreach (ConsoleColor color in Enum.GetValues(typeof(ConsoleColor)))
            {
                string color_code_fg = GetAnsiColorControlCode(color, true);
                string color_code_bg = GetAnsiColorControlCode(color, false);
                Console.Write("{0}{1}: {2}{3} :: {4}{1}: {5}{3}", color_code_fg,
                    color,
                    color_code_fg.Substring(2),
                    ColorReset,
                    color_code_bg,
                    color_code_bg.Substring(2));
                Console.WriteLine();
            }
        }

        private static void TestRuntime()
        {
            Console.WriteLine("==RUNTIME TEST==");
            foreach (ConsoleColor color in Enum.GetValues(typeof(ConsoleColor)))
            {
                Console.ForegroundColor = color;
                Console.Write(color);
                Console.ResetColor();
                Console.Write(" :: ");
                Console.BackgroundColor = color;
                Console.Write(color);
                Console.ResetColor();
                Console.WriteLine();
            }
        }
        #endregion
    }
}
