using System.Collections.Generic;
using System;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Xml.Linq;
using System.Collections;
using System.Windows.Forms;
using System.Threading.Tasks;

using x8086SharpEmu;

namespace x8086SharpEmu
{
    public abstract class Adapter : IOPortHandler
    {

        private X8086 mCPU;

        public enum AdapterType
        {
            Video,
            Keyboard,
            Floppy,
            IC,
            AudioDevice,
            Other,
            SerialMouseCOM1,
            Memory
        }

        public Adapter()
        {
        }

        public Adapter(X8086 cpu)
        {
            mCPU = cpu;
            Task.Run((Action)InitiAdapter);
            //System.Threading.Tasks.Task.Run(InitiAdapter);
        }

        public X8086 CPU
        {
            get
            {
                return mCPU;
            }
        }

        public abstract void InitiAdapter();
        public abstract void CloseAdapter();
        public abstract AdapterType Type { get; }
        public abstract string Vendor { get; }
        public abstract int VersionMajor { get; }
        public abstract int VersionMinor { get; }
        public abstract int VersionRevision { get; }

        //Public MustOverride Function [In](port As Integer) As integer Implements IIOPortHandler.In
        //Public MustOverride Sub Out(port As Integer, value As integer) Implements IIOPortHandler.Out
        //Public MustOverride ReadOnly Property Description As String Implements IIOPortHandler.Description
        //Public MustOverride ReadOnly Property Name As String Implements IIOPortHandler.Name

        new public List<uint> ValidPortAddress
        {
            get
            {
                return base.ValidPortAddress;
            }
        }

        public abstract override string Description { get; }
        public abstract override ushort In(uint port);
        public abstract override void Out(uint port, ushort value);
        public abstract override string Name { get; }
    }

}
