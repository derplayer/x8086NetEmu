using System.Collections.Generic;
using System;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Xml.Linq;
using System.Collections;


using x8086SharpEmu;

namespace x8086SharpEmu
{
    // http://jorisvr.nl/retro/
    public abstract class IOPortHandler : IInterruptController, IIOPortHandler
    {
        private X8086 mEmulator;
        private List<UInt32> mValidPortAddresses;

        public IOPortHandler()
        {
            mValidPortAddresses = new List<UInt32>();
        }

        public List<UInt32> ValidPortAddress
        {
            get
            {
                return mValidPortAddresses;
            }
        }

        public abstract void Out(UInt32 port, UInt16 value);
        public abstract UInt16 In(UInt32 port);
        public abstract string Description { get; }
        public abstract string Name { get; }
        public abstract void Run();

        public virtual byte GetPendingInterrupt()
        {
            //return -1;
            return 0;
        }

        public virtual UInt16 Read(UInt32 address)
        {
            return 0;
        }

        public virtual void Write(UInt32 address, UInt16 value)
        {
        }
    }
}