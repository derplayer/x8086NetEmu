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
    public abstract class InterruptRequest : IInterruptRequest
    {
        void IInterruptRequest.RaiseIrq(bool enable)
        {
            this.Raise(enable);
        }

        public abstract void Raise(bool enable);
    }

}
