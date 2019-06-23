using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Data;
using System.Xml.Linq;

using System.Collections;
using System.Linq;

using x8086SharpEmu;
using x8086SharpEmuConsole;

namespace x8086SharpEmuConsole
{
    //CRITICAL TODO: 5 div opcodes are still broken
    sealed class MainModule
    {
        private static X8086 cpu;

        public static void Run8086Console()
        {
            X8086.LogToConsole = true;

            X8086.Error += (object s, X8086.EmulatorErrorEventArgs e) =>
            {
                if (e.Message.ToLower().StartsWith("opcode"))
                    return;
                cpu?.Pause();
                Console.WriteLine(e.Message);
                cpu?.Close();
                //Environment.Exit(1);
                UnityEngine.Application.Quit();
            };

            cpu = new X8086(true, true, null);

            cpu.EmulationTerminated += () => UnityEngine.Application.Quit();// Environment.Exit(0);

            cpu.Adapters.Add(new FloppyControllerAdapter(cpu));
            cpu.Adapters.Add(new CGAConsole(cpu));
            cpu.Adapters.Add(new KeyboardAdapter(cpu));
            // cpu.Adapters.Add(New MouseAdapter(cpu)) ' So far, useless in Console mode
#if Win32_dbg
            cpu.Adapters.Add(new SpeakerAdpater(cpu));
#endif

#if Win32
			cpu.Adapters.Add(new AdlibAdapter(cpu));
#endif

            LoadSettings();

            cpu.Run();

            //do
            //{
            //    System.Threading.Thread.Sleep(500);
            //} while (true);
        }

        private static void LoadSettings()
        {
            //return; // disabled for now
            if (System.IO.File.Exists("settings.dat"))
            {
                var xml = XDocument.Load("settings.dat");
                ParseSettings(xml.Elements(XName.Get("settings", "")).ElementAtOrDefault(0));
                //ParseSettings(System.Xml.Elements("settings").ElementAtOrDefault(0));
            }
        }

        private static void ParseSettings(System.Xml.Linq.XElement xml)
        {
            cpu.SimulationMultiplier = double.Parse(System.Convert.ToString(cpu.SimulationMultiplier));

            cpu.Clock = double.Parse(System.Convert.ToString(cpu.Clock));

            for (int i = 0; i <= 512 - 1; i++)
            {
                if (cpu.FloppyContoller.get_DiskImage(i) != null)
                {
                    cpu.FloppyContoller.get_DiskImage(i).Close();
                }
            }

            foreach (var f in xml.Element("floppies").Elements("floppy"))
            {
                int index = (char)(System.Convert.ToChar(f.Element("letter").Value)) - 65;
                string image = System.Convert.ToString(f.Element("image").Value);
                bool ro = bool.Parse(System.Convert.ToString(f.Element("readOnly").Value));

                cpu.FloppyContoller.set_DiskImage(index, new DiskImage(image, ro));
            }

            foreach (var d in xml.Element("disks").Elements("disk"))
            {
                int index = (char)(System.Convert.ToChar(d.Element("letter").Value)) - 67 + 128;
                string image = System.Convert.ToString(d.Element("image").Value);
                bool ro = bool.Parse(System.Convert.ToString(d.Element("readOnly").Value));

                cpu.FloppyContoller.set_DiskImage(index, new DiskImage(image, ro, true));
            }
        }
    }

}
