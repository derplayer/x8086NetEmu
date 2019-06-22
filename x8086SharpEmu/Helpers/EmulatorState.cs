using System.Collections.Generic;
using System;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Xml.Linq;
using System.Collections;
using System.Windows.Forms;
using System.Runtime.CompilerServices;

using x8086SharpEmu;

namespace x8086SharpEmu
{
    public class EmulatorState
    {
        private X8086 mCPU;

        public EmulatorState(X8086 cpu)
        {
            mCPU = cpu;
        }

        public void SaveSettings(string fileName, XElement extras = null)
        {
            System.Xml.Linq.XDocument doc = new System.Xml.Linq.XDocument(GetSettings());

            if (extras != null)
            {
                doc.Elements("settings").ElementAtOrDefault(0).Add(extras);
            }

            doc.Save(fileName);
        }

        public void SaveState(string fileName)
        {
            /*
			XDocument doc = new XDocument();
			
			doc.Add(System.Xml.Linq.XElement.Parse("<state>" + System.Convert.ToString(GetSettings()) + ("<flags>" + mCPU.Flags.EFlags + "</flags>") + System.Convert.ToString(GetRegisters()) + System.Convert.ToString(GetMemory()) + ("<videoMode>" + (mCPU.VideoAdapter != null ? mCPU.VideoAdapter.VideoMode : "Mode3_Text_Color_80x25") + "</videoMode>") + ("<debugMode>" + mCPU.DebugMode + "</debugMode>") + "</state>"));
			
			doc.Save(fileName);
            */
            XDocument doc = new XDocument();
            XDocument xDocument = doc;
            XElement xElement = new XElement(XName.Get("state", ""));
            xElement.Add(GetSettings());
            XElement xElement2 = xElement;
            XElement xElement3 = new XElement(XName.Get("flags", ""));
            xElement3.Add(mCPU.Flags.EFlags);
            xElement2.Add(xElement3);
            xElement.Add(GetRegisters());
            xElement.Add(GetMemory());
            XElement xElement4 = xElement;
            xElement3 = new XElement(XName.Get("videoMode", ""));
            xElement3.Add(RuntimeHelpers.GetObjectValue((mCPU.VideoAdapter != null) ? ((object)mCPU.VideoAdapter.VideoMode) : "Mode3_Text_Color_80x25"));
            xElement4.Add(xElement3);
            XElement xElement5 = xElement;
            xElement3 = new XElement(XName.Get("debugMode", ""));
            xElement3.Add(mCPU.DebugMode);
            xElement5.Add(xElement3);
            xDocument.Add(xElement);
            doc.Save(fileName);
        }

        private XElement GetSettings()
        {
            return System.Xml.Linq.XElement.Parse("<settings>" + (double.Parse("<simulationMultiplier>") + mCPU.SimulationMultiplier + "</simulationMultiplier>") + (double.Parse("<clockSpeed>") + mCPU.Clock + "</clockSpeed>") + ("<videoZoom>" + (mCPU.VideoAdapter?.Zoom != null ? mCPU.VideoAdapter?.Zoom : 1) + "</videoZoom>") + System.Convert.ToString(GetFloppyImages()) + System.Convert.ToString(GetDiskImages()) + "</settings>");
        }

        private XElement GetFloppyImages()
        {
            //var curPath = (new Microsoft.VisualBasic.ApplicationServices.ConsoleApplicationBase()).Info.DirectoryPath + "\\";
            string curPath = System.IO.Directory.GetParent(Application.ExecutablePath) + "\\";
            var xml = System.Xml.Linq.XElement.Parse("<floppies></floppies>");

            if (mCPU.FloppyContoller != null)
            {
                for (int i = 0; i <= 128 - 1; i++)
                {
                    if (mCPU.FloppyContoller.get_DiskImage(i) != null)
                    {
                        var di = mCPU.FloppyContoller.get_DiskImage(i);

                        if (!di.IsHardDisk)
                        {
                            xml.Add(System.Xml.Linq.XElement.Parse("<floppy>" + ("<letter>" + Convert.ToChar(65 + i) + "</letter>") + (double.Parse("<index>") + i + "</index>") + ("<image>" + di.FileName.Replace(curPath, "") + "</image>") + ("<readOnly>" + di.IsReadOnly.ToString() + "</readOnly>") + "</floppy>"));
                        }
                    }
                }
            }

            return xml;
        }

        private XElement GetDiskImages()
        {
            string curPath = System.IO.Directory.GetParent(Application.ExecutablePath) + "\\";
            var xml = System.Xml.Linq.XElement.Parse("<disks></disks>");

            for (int i = 128; i <= 1000 - 1; i++)
            {
                if (mCPU.FloppyContoller.get_DiskImage(i) != null)
                {
                    var di = mCPU.FloppyContoller.get_DiskImage(i);

                    if (di.IsHardDisk)
                    {
                        xml.Add(System.Xml.Linq.XElement.Parse("<disk>" + ("<letter>" + Convert.ToChar(67 + (i - 128)) + "</letter>") + (double.Parse("<index>") + i + "</index>") + ("<image>" + di.FileName.Replace(curPath, "") + "</image>") + ("<readOnly>" + di.IsReadOnly.ToString() + "</readOnly>") + "</disk>"));
                    }
                }
            }

            return xml;
        }

        private XElement GetRegisters()
        {
            return System.Xml.Linq.XElement.Parse("<registers>" + (double.Parse("<AX>") + mCPU.Registers.AX + "</AX>") + (double.Parse("<BX>") + mCPU.Registers.BX + "</BX>") + (double.Parse("<CX>") + mCPU.Registers.CX + "</CX>") + (double.Parse("<DX>") + mCPU.Registers.DX + "</DX>") + (double.Parse("<CS>") + mCPU.Registers.CS + "</CS>") + (double.Parse("<IP>") + mCPU.Registers.IP + "</IP>") + (double.Parse("<SS>") + mCPU.Registers.SS + "</SS>") + (double.Parse("<SP>") + mCPU.Registers.SP + "</SP>") + (double.Parse("<DS>") + mCPU.Registers.DS + "</DS>") + (double.Parse("<SI>") + mCPU.Registers.SI + "</SI>") + (double.Parse("<ES>") + mCPU.Registers.ES + "</ES>") + (double.Parse("<DI>") + mCPU.Registers.DI + "</DI>") + (double.Parse("<BP>") + mCPU.Registers.BP + "</BP>") + ("<AS>" + mCPU.Registers.ActiveSegmentRegister + "</AS>") + "</registers>");
        }

        private XElement GetMemory()
        {
            return System.Xml.Linq.XElement.Parse("<memory>" + Convert.ToBase64String(mCPU.Memory) + "</memory>");
        }
    }

}
