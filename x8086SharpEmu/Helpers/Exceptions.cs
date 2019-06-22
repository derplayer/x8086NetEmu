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
        public class EmulatorErrorEventArgs : EventArgs
        {

            public string Message { get; set; }

            public EmulatorErrorEventArgs(string msg)
            {
                Message = msg;
            }
        }

        private void OpCodeNotImplemented() { OpCodeNotImplemented(""); }

        private void OpCodeNotImplemented(string comment = "")
        {
            int originalOpCodeSize = opCodeSize;
            ThrowException(System.Convert.ToString(string.Format("OpCode '{0}' at {1} Not Implemented{2}", Decode(true).Mnemonic?.Replace("h:", ""),
                mRegisters.PointerAddressToString.Replace("h", ""), comment == "" ? "" : ": " + comment)));
            opCodeSize = (byte)originalOpCodeSize;
            if (mVic20)
            {
                HandleInterrupt((byte)6, false);
            }
        }

        private void SystemHalted()
        {
            mIsHalted = true;
            ThrowException("System Halted");

            if (EmulationHaltedEvent != null)
                EmulationHaltedEvent();

#if DEBUG
            if (InstructionDecodedEvent != null)
                InstructionDecodedEvent();
#endif
        }

        private void NoIOPort(int port)
        {
            X8086.Notify("No IO port response from {0} called at {1}:{2}", NotificationReasons.Warn,
                port.ToString("X4"),
                mRegisters.CS.ToString("X4"),
                mRegisters.IP.ToString("X4"));
        }

        public void RaiseException(string message)
        {
            ThrowException(message);
        }

        private void ThrowException(string message)
        {
            if (mEnableExceptions)
            {
                throw (new Exception(message));
            }
            else
            {
                X8086.Notify(message, NotificationReasons.Err);
                if (ErrorEvent != null)
                    ErrorEvent(this, new EmulatorErrorEventArgs(message));
            }
        }

        public enum NotificationReasons
        {
            Info,
            Warn,
            Err,
            Fck,
            Dbg
        }

        public static void Notify(string message, NotificationReasons reason, params object[] arg)
        {
            var formattedMessage = "";
            try
            {
                formattedMessage = reason.ToString().PadRight(4) + " " + string.Format(message, arg);
            }
            catch (Exception e)
            {
                formattedMessage = reason.ToString().PadRight(4) + " " + message;
                //throw;
            }

            //if (LogToConsole)
            if (true)
            {
                //Console.WriteLine(formattedMessage);
                Debug.WriteLine(formattedMessage);
#if DEBUG
                if (reason == NotificationReasons.Dbg)
                {
                    Debug.WriteLine(formattedMessage);
                }
#endif
            }

            if (OutputEvent != null)
                OutputEvent(message, reason, arg);
        }
    }

}
