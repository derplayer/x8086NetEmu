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
    public interface IIOPortHandler
    {
        List<uint> ValidPortAddress { get; }

        void Out(uint port, ushort value);
        ushort In(uint port);
        string Name { get; }
        string Description { get; }
        void Run();
    }

}
