using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using x8086SharpEmu;
using System.Threading;
using System.Windows.Forms;

static class x8086SharpEmuTests
{
    private static X8086 cpu;
    private static byte[] validData = null;
    private static int testsTotal = 0;
    private static int failedTotal = 0;
    private static string prefix;
    private static List<string> inst = new List<string>();

    public static void Main()
    {
        AutoResetEvent waiter = new AutoResetEvent(false);

        // X8086.Models.IBMPC_5150 is required as fake86 does not properly handle eflags
        cpu = new X8086(true, false, null/* Conversion error: Set to default value for this argument */, X8086.Models.IBMPC_5150) { Clock = 47700000 };
        cpu.EmulationHalted += () =>
        {
            Compare();
            Console.WriteLine();
            waiter.Set();
        };

        X8086.LogToConsole = false;

        foreach (System.IO.FileInfo f in (new System.IO.DirectoryInfo(System.IO.Path.Combine(System.IO.Directory.GetParent(Application.ExecutablePath).ToString(), "80186_tests\\"))).GetFiles("*.bin"))
        {
            string fileName = f.Name.Replace(f.Extension, "");
            string dataFileName = System.IO.Path.Combine(f.DirectoryName, $"res_{fileName}.bin");

            // If fileName <> "segpr" Then Continue For

            if (!System.IO.File.Exists(dataFileName))
                continue;
            validData = System.IO.File.ReadAllBytes(dataFileName);

            prefix = $"Running: {fileName}";
            Console.Write(prefix);

            if (cpu.IsHalted)
                cpu.HardReset();
            cpu.LoadBIN(f.FullName, 0xF000, 0x0);
            cpu.Run(false, 0xF000, 0x0);

            // While Not cpu.IsHalted
            // DisplayInstructions()
            // cpu.StepInto()
            // End While

            waiter.WaitOne();
        }
        cpu.Close();

        int passedTotal = testsTotal - failedTotal;
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"Score: {passedTotal}/{testsTotal} [{passedTotal / (double)testsTotal * 100}%]");
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine();

        Console.WriteLine("Press any key to exit");
        Console.ReadKey();
    }

    private static void DisplayInstructions()
    {
        if (inst.Count > 10)
            inst.RemoveAt(0);
        inst.Add(cpu.Decode().ToString());

        int c = Console.CursorLeft;
        int r = Console.CursorTop;

        for (int i = 0; i <= inst.Count - 1; i++)
        {
            Console.SetCursorPosition(Console.WindowWidth / 2, i);
            Console.Write(inst[i]);
        }

        Console.SetCursorPosition(c, r);
    }

    private static void Compare()
    {
        const int p = 28;

        string txt = "";
        string v1;
        string v2;
        List<string> invalidData = new List<string>();
        int dataLen = validData.Length / 2;

        testsTotal += dataLen;

        //bug in 16, 20, 22, 24,28
        for (int i = 0; i <= dataLen - 1; i += 2)
        {
            v1 = cpu.get_RAM16(0, (ushort)i).ToString("X4");
            v2 = BitConverter.ToInt16(validData, i).ToString("X4");
            if (v1 != v2)
                invalidData.Add($"0000:{i} {v1} <> {v2}");
        }

        if (invalidData.Any())
        {
            txt = $" > FAILED [{invalidData.Count}/{dataLen}]";
            Console.WriteLine(txt.PadLeft(p - prefix.Length + txt.Length));
            invalidData.ForEach(id =>
            {
                string[] t = id.Split(' ');
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"  {t[0]}");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write($" {t[1]}");
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write($" {t[2]}");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($" {t[3]}");
                Console.ForegroundColor = ConsoleColor.Gray;
            });
            failedTotal += invalidData.Count;
        }
        else
        {
            txt = $" > PASSED [{dataLen}]";
            Console.WriteLine(txt.PadLeft(p - prefix.Length + txt.Length));
        }
    }
}
