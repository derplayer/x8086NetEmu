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

    public class DummyAdapter : Adapter
    {

        public override AdapterType Type
        {
            get
            {
                return AdapterType.Other;
            }
        }

        public override string Vendor
        {
            get
            {
                return "xFX JumpStart";
            }
        }

        public override int VersionMajor
        {
            get
            {
                return 0;
            }
        }

        public override int VersionMinor
        {
            get
            {
                return 0;
            }
        }

        public override int VersionRevision
        {
            get
            {
                return 1;
            }
        }

        public override string Description
        {
            get
            {
                return "";
            }
        }

        public override string Name
        {
            get
            {
                return "";
            }
        }

        public override void InitiAdapter()
        {
        }

        public override void CloseAdapter()
        {
        }

        public override ushort In(uint port)
        {
            return (ushort)(0xFF);
        }

        public override void Out(uint port, ushort value)
        {
        }

        public override void Run()
        {
        }
    }

}
