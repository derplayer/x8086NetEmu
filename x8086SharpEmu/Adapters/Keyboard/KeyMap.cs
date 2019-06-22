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
    public class KeyMap
    {
        // Enables sending virtual shift up/down events to deal with non-XT (int)Keys.
        private bool useVirtualShift = true;

        // Map Java e.KeyCode code to PC scan code and extra flags.
        private int[] keytbl;

        // Must send E0 escape with this scancode
        private const int KEY_EXTEND = 0x100;

        // Need negative shift/numlock state
        private const int KEY_NONUM = 0x200;

        // Need negative shift state
        private const int KEY_NOSHIFT = 0x400;

        // Could be numpad or edit block depending on e.KeyCode location
        private const int KEY_EDIT = 0x800;

        // Scan code of left Shift e.KeyCode
        public const int SCAN_LSHIFT = 42;

        // Scan code of right Shift e.KeyCode
        public const int SCAN_RSHIFT = 54;

        // Scan code of Ctrl e.KeyCode
        public const int SCAN_CTRL = 29;

        // Scan code of Alt e.KeyCode
        public const int SCAN_ALT = 56;

        // True if we inverted the shift state by sending a virtual shift code.
        private bool virtualShiftState;

        // True if PrintScreen is down in its SysRq role.
        private bool isSysRq;

        // Mask of state (int)Keys that we believe to be physically down.
        private const int MASK_LSHIFT = 1;
        private const int MASK_RSHIFT = 2;
        private const int MASK_LALT = 4;
        private const int MASK_RALT = 8;
        private const int MASK_LCTRL = 16;
        private const int MASK_RCTRL = 32;
        private const int MASK_NUMLOCK = 64;
        private int stateKeyMask;

        public KeyMap()
        {
            virtualShiftState = false;
            isSysRq = false;
            stateKeyMask = 0;

            keytbl = new int[256];
            keytbl[(int)(int)Keys.Escape] = 1;
            keytbl[(int)(int)Keys.D1] = 2;
            keytbl[(int)(int)Keys.D2] = 3;
            keytbl[(int)(int)Keys.D3] = 4;
            keytbl[(int)Keys.D4] = 5;
            keytbl[(int)Keys.D5] = 6;
            keytbl[(int)Keys.D6] = 7;
            keytbl[(int)Keys.D7] = 8;
            keytbl[(int)Keys.D8] = 9;
            keytbl[(int)Keys.D9] = 10;
            keytbl[(int)Keys.D0] = 11;
            keytbl[(int)Keys.OemMinus] = 12;
            keytbl[(int)Keys.Oemplus] = 13;
            keytbl[(int)Keys.Back] = 14;
            keytbl[(int)Keys.Tab] = 15;
            keytbl[(int)Keys.Q] = 16;
            keytbl[(int)Keys.W] = 17;
            keytbl[(int)Keys.E] = 18;
            keytbl[(int)Keys.R] = 19;
            keytbl[(int)Keys.T] = 20;
            keytbl[(int)Keys.Y] = 21;
            keytbl[(int)Keys.U] = 22;
            keytbl[(int)Keys.I] = 23;
            keytbl[(int)Keys.O] = 24;
            keytbl[(int)Keys.P] = 25;
            keytbl[(int)Keys.OemOpenBrackets] = 26;
            keytbl[(int)Keys.OemCloseBrackets] = 27;
            keytbl[(int)Keys.Enter] = 28;
            keytbl[(int)Keys.ControlKey] = 29;
            keytbl[(int)Keys.A] = 30;
            keytbl[(int)Keys.S] = 31;
            keytbl[(int)Keys.D] = 32;
            keytbl[(int)Keys.F] = 33;
            keytbl[(int)Keys.G] = 34;
            keytbl[(int)Keys.H] = 35;
            keytbl[(int)Keys.J] = 36;
            keytbl[(int)Keys.K] = 37;
            keytbl[(int)Keys.L] = 38;
            keytbl[(int)Keys.OemSemicolon] = 39;
            keytbl[(int)Keys.OemQuotes] = 40;
            keytbl[(int)Keys.Oemtilde] = 41;
            keytbl[(int)Keys.ShiftKey] = SCAN_LSHIFT;
            keytbl[(int)Keys.OemPipe] = 43; // (int)Keys.OemBackslash
            keytbl[(int)Keys.Z] = 44;
            keytbl[(int)Keys.X] = 45;
            keytbl[(int)Keys.C] = 46;
            keytbl[(int)Keys.V] = 47;
            keytbl[(int)Keys.B] = 48;
            keytbl[(int)Keys.N] = 49;
            keytbl[(int)Keys.M] = 50;
            keytbl[(int)Keys.Oemcomma] = 51;
            keytbl[(int)Keys.OemPeriod] = 52;
            keytbl[(int)Keys.OemQuestion] = 53;
            keytbl[(int)Keys.Divide] = System.Convert.ToInt32(53 | KEY_EXTEND);
            keytbl[(int)Keys.Multiply] = 55;
            keytbl[18] = 56; // ALT
            keytbl[(int)Keys.Space] = 57;
            keytbl[(int)Keys.CapsLock] = 58;
            keytbl[(int)Keys.F1] = 59;
            keytbl[(int)Keys.F2] = 60;
            keytbl[(int)Keys.F3] = 61;
            keytbl[(int)Keys.F4] = 62;
            keytbl[(int)Keys.F5] = 63;
            keytbl[(int)Keys.F6] = 64;
            keytbl[(int)Keys.F7] = 65;
            keytbl[(int)Keys.F8] = 66;
            keytbl[(int)Keys.F9] = 67;
            keytbl[(int)Keys.F10] = 68;
            keytbl[(int)Keys.NumLock] = 69;
            keytbl[(int)Keys.Scroll] = 70;
            keytbl[(int)Keys.NumPad7] = 71;
            keytbl[(int)Keys.Home] = System.Convert.ToInt32(71 | KEY_EDIT | KEY_NOSHIFT);
            keytbl[(int)Keys.NumPad8] = 72;
            keytbl[(int)Keys.Up] = System.Convert.ToInt32(72 | KEY_EXTEND | KEY_NONUM);
            keytbl[(int)Keys.NumPad9] = 73;
            keytbl[(int)Keys.PageUp] = System.Convert.ToInt32(73 | KEY_EDIT);
            keytbl[(int)Keys.Subtract] = 74;
            keytbl[(int)Keys.NumPad4] = 75;
            keytbl[(int)Keys.Left] = System.Convert.ToInt32(75 | KEY_EXTEND | KEY_NONUM);
            keytbl[(int)Keys.NumPad5] = 76;
            keytbl[(int)Keys.NumPad6] = 77;
            keytbl[(int)Keys.Right] = System.Convert.ToInt32(77 | KEY_EXTEND | KEY_NONUM);
            keytbl[(int)Keys.Add] = 78;
            keytbl[(int)Keys.NumPad1] = 79;
            keytbl[(int)Keys.End] = System.Convert.ToInt32(79 | KEY_EDIT);
            keytbl[(int)Keys.NumPad2] = 80;
            keytbl[(int)Keys.Down] = System.Convert.ToInt32(80 | KEY_EXTEND | KEY_NONUM);
            keytbl[(int)Keys.NumPad3] = 81;
            keytbl[(int)Keys.PageDown] = System.Convert.ToInt32(81 | KEY_EDIT);
            keytbl[(int)Keys.NumPad0] = 82;
            keytbl[(int)Keys.Insert] = System.Convert.ToInt32(82 | KEY_EDIT);
            keytbl[(int)Keys.Decimal] = 83;
            keytbl[(int)Keys.Delete] = System.Convert.ToInt32(83 | KEY_EDIT);
            keytbl[(int)Keys.PrintScreen] = 84;
            keytbl[(int)Keys.F11] = 87;
            keytbl[(int)Keys.F12] = 88;
        }

        public int GetScanCode(int keyValue)
        {
            return keytbl[keyValue & 0xFF];
        }
    }

}
