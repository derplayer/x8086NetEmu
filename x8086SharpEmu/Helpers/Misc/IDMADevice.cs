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
    public interface IDMADevice
    {
        // Transfers a byte from memory to the device.
        // This method is called one or more times by the DMA controller
        // in response to a DMA request in read mode.  The device must
        // read and process one byte.
        // <p>
        // The device may optionally call channel.dmaRequest(false) and/or
        // channel.dmaEop() to indicate that no more bytes should be transferred
        // in the current transaction.
        // @param v data byte
        void DMARead(byte v);

        // Transfers a byte from the device to memory.
        // This method is called one or more times by the DMA controller
        // in response to a DMA request in write mode.  The device must
        // produce and write one byte.
        // <p>
        // The device may optionally call channel.dmaRequest(false) and/or
        // channel.dmaEop() to indicate that no more bytes should be transferred
        // in the current transaction.
        // @return data byte
        byte DMAWrite();

        // Called by the DMA controller to signal an internally generated EOP
        // (caused by terminal count).
        void DMAEOP();
    }

}
