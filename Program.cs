using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;

namespace pakcomposer
{
    internal class Program
    {
        private static char gMode = 'N';
        private static char gSubMode = 'N';
        private static string gObject = "";
        private static bool gDoExtensions = false;
        private static bool gDoVerbose = false;
        private static bool gDoUnpack = false;
        private static bool gDoToD2 = false;
        private static bool gDoAlign = false;
        private static byte[] dMainFile;
        private static string dDirectoryName;
        private static int dFileCount;
        private static List<int> dFileOffsets = new List<int>();
        private static List<int> dFileSizes = new List<int>();
        private static List<byte[]> dFiles = new List<byte[]>();
        private static List<string> dFileNames = new List<string>();

        // File extension headers
        private static readonly byte[] ToD1RSCE4Head = new byte[] { 84, 79, 68, 49, 82, 83, 67, 69, 52 };
        private static readonly byte[] d1rxgmHead = new byte[] { 68, 49, 82, 88, 71, 77 };
        private static readonly byte[] tm2Head = new byte[] { 84, 73, 77, 50 };
        private static readonly byte[] tm2HeadAlt = new byte[] { 84, 77, 50, 64 };
        private static readonly byte[] MdlHead = new byte[] { 77, 68, 76, 64 };
        private static readonly byte[] EffeHead = new byte[] { 69, 70, 70, 69 };
        private static readonly byte[] Anp3Head = new byte[] { 97, 110, 112, 51 };
        private static readonly byte[] Se2Head = new byte[] { 105, 83, 69, 50 };
        private static readonly byte[] LvdHead = new byte[] { 105, 76, 86, 68 };
        private static readonly byte[] ScedHead = new byte[] { 83, 67, 69, 68 };
        private static readonly byte[] TheirsceHead = new byte[] { 84, 72, 69, 73 };
        private static readonly byte[] WeacHead = new byte[] { 87, 69, 65, 67 };
        private static readonly byte[] EnemHead = new byte[] { 69, 78, 69, 77 };
        private static readonly byte[] GrpcHead = new byte[] { 71, 82, 80, 67 };

        private static void ColorWrite(ConsoleColor Color, string str, params object[] args)
        {
            Console.ForegroundColor = Color;
            Console.Write(str, args);
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine();
        }

        private static void ColorWritePlus(
          ConsoleColor FrontColor,
          ConsoleColor BackColor,
          string str,
          params object[] args)
        {
            Console.ForegroundColor = FrontColor;
            Console.BackgroundColor = BackColor;
            Console.Write(str, args);
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.WriteLine();
        }

        private static string CutToExtension(string Input) => Input.Contains(".") ? Input.Remove(Input.LastIndexOf(".")) : Input;

        //Credit to Ethanol for fix

        private static string GetFileName(string DN, int number)
        {
            DN = Path.GetFileNameWithoutExtension(DN);
            if (number < 10)
                return DN + "_000" + number;
            if (number < 100)
                return DN + "_00" + number;
            return number < 1000 ? DN + "_0" + number : DN + "_" + number;
        }

        private static void GetFlags(string[] args)
        {
            for (int index = 0; index < args.Length; ++index)
            {
                if (args[index] == "-d" && gMode == 'N')
                {
                    gMode = 'D';
                    if (index + 1 < args.Length)
                        gObject = args[index + 1];
                }
                else if (args[index] == "-c" && gMode == 'N')
                {
                    gMode = 'C';
                    if (index + 1 < args.Length)
                        gObject = args[index + 1];
                }
                else if (args[index] == "-t" && gMode == 'N')
                {
                    gMode = 'T';
                    if (index + 1 < args.Length)
                        gObject = args[index + 1];
                }
                else
                {
                    if (args[index] == "-0" && gSubMode == 'N')
                        gSubMode = '0';
                    if (args[index] == "-1" && gSubMode == 'N')
                        gSubMode = '1';
                    if (args[index] == "-3" && gSubMode == 'N')
                        gSubMode = '3';
                    if (args[index] == "-x")
                        gDoExtensions = true;
                    if (args[index] == "-v")
                        gDoVerbose = true;
                    if (args[index] == "-a")
                        gDoAlign = true;
                    if (args[index] == "-u")
                        gDoUnpack = true;
                    if (args[index] == "-tod2_ps2_skit_padding")
                        gDoToD2 = true;
                }
            }
        }

        private static string GetExtension(byte[] head)
        {
            if (head.Length >= 9 && head.Take(9).SequenceEqual(ToD1RSCE4Head))
                return ".tod1rsce4";
            if (head.Length >= 8 && head.Take(8).SequenceEqual(ToD1RSCE4Head.Take(8)))
                return ".tod1rsce";
            if (head.Length >= 6 && head.Take(6).SequenceEqual(d1rxgmHead))
                return ".d1rxgm";

            if (head.Length < 4)
                return ".unknown";

            var first4Bytes = head.Take(4);

            if (first4Bytes.SequenceEqual(tm2Head) || first4Bytes.SequenceEqual(tm2HeadAlt))
                return ".tm2";
            if (first4Bytes.SequenceEqual(MdlHead))
                return ".mdl";
            if (first4Bytes.SequenceEqual(EffeHead))
                return ".effe";
            if (first4Bytes.SequenceEqual(Anp3Head))
                return ".anp3";
            if (first4Bytes.SequenceEqual(Se2Head))
                return ".se2";
            if (first4Bytes.SequenceEqual(LvdHead))
                return ".lvd";
            if (first4Bytes.SequenceEqual(ScedHead))
                return ".sced";
            if (first4Bytes.SequenceEqual(TheirsceHead))
                return ".theirsce";
            if (first4Bytes.SequenceEqual(WeacHead))
                return ".weac";
            if (first4Bytes.SequenceEqual(EnemHead))
                return ".enem";
            if (first4Bytes.SequenceEqual(GrpcHead))
                return ".grpc";

            return IsPacked(head) ? ".compress" : ".unknown";
        }

        private static void GetFileCount() => dFileCount = BitConverter.ToInt32(dMainFile, 0);

        private static void GetDeconstructiveInformation()
        {
            switch (gSubMode)
            {
                case '0':
                    for (int index = 0; index < dFileCount; ++index)
                        dFileSizes.Add(BitConverter.ToInt32(dMainFile, 4 + index * 4));
                    int num = 4 + 4 * dFileCount;
                    for (int index = 0; index < dFileCount; ++index)
                    {
                        dFileOffsets.Add(num);
                        num += dFileSizes[index];
                    }
                    break;
                case '1':
                    for (int index = 0; index < dFileCount; ++index)
                    {
                        dFileOffsets.Add(BitConverter.ToInt32(dMainFile, 4 + index * 8));
                        dFileSizes.Add(BitConverter.ToInt32(dMainFile, 8 + index * 8));
                    }
                    break;
                case '3':
                    for (int index = 0; index < dFileCount; ++index)
                        dFileOffsets.Add(BitConverter.ToInt32(dMainFile, 4 + index * 4));
                    for (int index = 0; index < dFileCount - 1; ++index)
                        dFileSizes.Add(dFileOffsets[index + 1] - dFileOffsets[index]);
                    dFileSizes.Add(dMainFile.Length - dFileOffsets[dFileCount - 1]);
                    break;
            }
        }

        private static bool CheckForFileSizeError()
        {
            for (int index = 0; index < dFileCount; ++index)
            {
                if (dFileSizes[index] == 0)
                    return true;
            }
            return false;
        }

        private static bool CheckForDupeOffsets()
        {
            for (int index1 = 0; index1 < dFileCount - 1; ++index1)
            {
                for (int index2 = index1 + 1; index2 < dFileCount; ++index2)
                {
                    if (dFileOffsets[index1] == dFileOffsets[index2])
                        return true;
                }
            }
            return false;
        }

        private static void CreateFilesInRam()
        {
            for (int index = 0; index < dFileCount; ++index)
            {
                byte[] numArray = new byte[dFileSizes[index]];
                Array.Copy(dMainFile, dFileOffsets[index], numArray, 0, dFileSizes[index]);
                dFiles.Add(numArray);
            }
        }

        private static void AssignFileNames()
        {
            for (int index = 0; index < dFileCount; ++index)
            {
                string fileName = GetFileName(dDirectoryName, index);
                if (gDoExtensions)
                    fileName += GetExtension(dFiles[index]);
                dFileNames.Add(fileName);
            }
        }

        private static void CreateFiles()
        {
            if (gDoUnpack && DoUnpackExist())
            {
                for (int index = 0; index < dFileCount; ++index)
                {
                    BinaryWriter binaryWriter1 = new BinaryWriter(File.Open(dDirectoryName + "/" + dFileNames[index], FileMode.Create));
                    binaryWriter1.Write(dFiles[index]);
                    binaryWriter1.Close();
                    if (gDoVerbose)
                        ColorWrite(ConsoleColor.DarkYellow, "File '{0}' has been created!", dFileNames[index]);
                    if (IsPacked(dFiles[index]))
                    {
                        string str = dDirectoryName + "/" + GetFileName(dDirectoryName, index) + "d";
                        DoExtract(dDirectoryName + "/" + dFileNames[index], str);
                        if (!File.Exists(str))
                        {
                            ColorWrite(ConsoleColor.Red, "File '{0}' has not been created!", str);
                        }
                        else
                        {
                            if (gDoExtensions)
                            {
                                byte[] numArray = File.ReadAllBytes(str);
                                File.Delete(str);
                                if (gDoVerbose)
                                    ColorWrite(ConsoleColor.DarkRed, "File '{0}' has been deleted!", str);
                                str += GetExtension(numArray);
                                BinaryWriter binaryWriter2 = new BinaryWriter(File.Open(str, FileMode.Create));
                                binaryWriter2.Write(numArray);
                                binaryWriter2.Close();
                            }
                            if (gDoVerbose)
                                ColorWrite(ConsoleColor.DarkGreen, "File '{0}' has been decrypted to '{1}'!", dFileNames[index], str.Substring(dDirectoryName.Length + 1));
                        }
                    }
                }
            }
            else
            {
                for (int index = 0; index < dFileCount; ++index)
                {
                    BinaryWriter binaryWriter = new BinaryWriter(File.Open(dDirectoryName + "/" + dFileNames[index], FileMode.Create));
                    binaryWriter.Write(dFiles[index]);
                    binaryWriter.Close();
                    if (gDoVerbose)
                        ColorWrite(ConsoleColor.DarkYellow, "File '{0}' has been created!", dFileNames[index]);
                }
            }
        }

        private static void GetFiles()
        {
            if (gDoUnpack && DoUnpackExist())
            {
                for (dFileCount = 0; Directory.GetFiles(dDirectoryName, GetFileName(dDirectoryName, dFileCount) + "*").Length > 0; ++dFileCount)
                {
                    string fileName = GetFileName(dDirectoryName, dFileCount);
                    string[] files1 = Directory.GetFiles(dDirectoryName, fileName + "d*");
                    if (files1.Length > 0)
                    {
                        DoCompress(files1[0], files1[0] + "c");
                        dFiles.Add(File.ReadAllBytes(files1[0] + "c"));
                        File.Delete(files1[0] + "c");
                        if (gDoVerbose)
                            ColorWrite(ConsoleColor.Yellow, "File '{0}' was compressed before adding to RAM!", files1[0].Substring(6));
                    }
                    else
                    {
                        string[] files2 = Directory.GetFiles(dDirectoryName, fileName + "*");
                        if (files2.Length == 0)
                        {
                            ColorWrite(ConsoleColor.Red, "ERROR! No file called '{0}' found!", fileName);
                            Environment.Exit(0);
                        }
                        dFiles.Add(File.ReadAllBytes(files2[0]));
                        if (gDoVerbose)
                            ColorWrite(ConsoleColor.Yellow, "File '{0}' added to RAM!", files2[0].Substring(6));
                    }
                }
            }
            else
            {
                for (dFileCount = 0; Directory.GetFiles(dDirectoryName, GetFileName(dDirectoryName, dFileCount) + "*").Length > 0; ++dFileCount)
                {
                    string fileName = GetFileName(dDirectoryName, dFileCount);
                    string[] files = Directory.GetFiles(dDirectoryName, fileName + "*");
                    if (files.Length > 0)
                    {
                        dFiles.Add(File.ReadAllBytes(files[0]));
                        if (gDoVerbose)
                            ColorWrite(ConsoleColor.Yellow, "File '{0}' added to RAM!", files[0].Substring(6));
                    }
                }
            }
        }

        private static bool CheckForNoneFiles() => dFileCount == 0;

        private static void SetConstructiveInformation()
        {
            int num = 0;
            for (int index = 0; index < dFileCount; ++index)
            {
                dFileSizes.Add(dFiles[index].Length);
                if (gDoAlign)
                    while (num % 16 != 0)
                        num++;
                dFileOffsets.Add(num);
                num += dFileSizes[index];
            }
            if (gDoToD2)
                ChangeFile();
        }

        private static void DoAssemble()
        {
            BinaryWriter binaryWriter = new BinaryWriter(File.Open(string.Format(gObject + ".pak{0}", gSubMode), FileMode.Create));
            switch (gSubMode)
            {
                case '0':
                    binaryWriter.Write(dFileCount);
                    for (int index = 0; index < dFileCount; ++index)
                        binaryWriter.Write(dFileSizes[index]);
                    for (int index = 0; index < dFileCount; ++index)
                        binaryWriter.Write(dFiles[index]);
                    break;
                case '1':
                    int num1 = 4 + 8 * dFileCount;

                    if (gDoAlign)
                        while (num1 % 16 != 0)
                            num1++;

                    binaryWriter.Write(dFileCount);
                    for (int index = 0; index < dFileCount; ++index)
                    {
                        binaryWriter.Write(dFileOffsets[index] + num1);
                        binaryWriter.Write(dFileSizes[index]);
                    }

                    if (gDoAlign)
                        while (binaryWriter.BaseStream.Position % 16 != 0)
                            binaryWriter.Write(0);

                    for (int index = 0; index < dFileCount; ++index)
                    {
                        binaryWriter.Write(dFiles[index]);
                        if (gDoAlign)
                            while (binaryWriter.BaseStream.Position % 16 != 0)
                                binaryWriter.Write(0);
                    }
                    break;
                case '3':
                    int num2 = 4 + 4 * dFileCount;
                    binaryWriter.Write(dFileCount);
                    for (int index = 0; index < dFileCount; ++index)
                        binaryWriter.Write(dFileOffsets[index] + num2);
                    for (int index = 0; index < dFileCount; ++index)
                        binaryWriter.Write(dFiles[index]);
                    break;
            }
            binaryWriter.Close();
        }

        private static void ChangeFile()
        {
            int num1 = (4 + 8 * dFileCount + dFileSizes[0] + dFileSizes[1] + dFileSizes[2]) % 16;
            if (num1 <= 0)
                return;
            int num2 = 16 - num1;
            byte[] numArray = new byte[dFileSizes[2] + num2];
            numArray.Initialize();
            dFiles[2].CopyTo(numArray, 0);
            dFiles[2] = numArray;
            dFileSizes[2] = dFiles[2].Length;
            int num3 = 0;
            for (int index = 0; index < dFileCount; ++index)
            {
                dFileOffsets[index] = num3;
                num3 += dFileSizes[index];
            }
        }

        private static bool DoUnpackExist() => File.Exists("comptoe.exe");

        private static bool IsPacked(byte[] file) => file[0] == 3 && (file[1] != 0 || file[2] != 0 || file[3] != 0);

        //shoutout to Ethanol
        private static void DoExtract(string fileoriginal, string filenew)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "comptoe.exe";
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = true;
            psi.Arguments = string.Format("-d {0} {1}", fileoriginal, filenew);
            psi.UseShellExecute = false;
            Process.Start(psi).WaitForExit();
        }

        private static void DoCompress(string filedecompressed, string filecompressed)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "comptoe.exe";
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = true;
            psi.Arguments = string.Format("-c {0} {1}", filedecompressed, filecompressed);
            psi.UseShellExecute = false;
            Process.Start(psi).WaitForExit();
        }

        private static void DoDeconstruct()
        {
            if (gDoVerbose)
                ColorWrite(ConsoleColor.White, "Reading all bytes of initial file into RAM...");
            dMainFile = File.ReadAllBytes(gObject);
            if (gDoVerbose)
                ColorWrite(ConsoleColor.White, "Reading first 4 bytes (count of files)...");
            GetFileCount();
            ColorWrite(ConsoleColor.Green, "Count of files inside archive: {0}", dFileCount);
            if (gDoVerbose)
                ColorWrite(ConsoleColor.White, "Extracting information about offsets and sizes...");
            GetDeconstructiveInformation();
            if (gDoVerbose)
                ColorWrite(ConsoleColor.White, "Information extracted. Showing information about sizes and offsets.");
            if (gDoVerbose)
                ColorWrite(ConsoleColor.Yellow, "File offsets: ");
            if (gDoVerbose)
            {
                for (int index = 0; index < dFileCount; ++index)
                    ColorWrite(ConsoleColor.White, dFileOffsets[index].ToString());
            }
            if (gDoVerbose)
                Console.WriteLine();
            if (gDoVerbose)
                ColorWrite(ConsoleColor.Yellow, "File sizes: ");
            if (gDoVerbose)
            {
                for (int index = 0; index < dFileCount; ++index)
                    ColorWrite(ConsoleColor.White, dFileSizes[index].ToString());
            }
            if (gDoVerbose)
                Console.WriteLine();
            if (CheckForFileSizeError())
                ColorWrite(ConsoleColor.Red, "Error! One of files have negative or zero file size!");
            else if (CheckForDupeOffsets())
            {
                ColorWrite(ConsoleColor.Red, "Error! One of files have same offset as another!");
            }
            else
            {
                ColorWrite(ConsoleColor.Green, "No errors found!");
                if (gDoVerbose)
                    ColorWrite(ConsoleColor.White, "Creating files inside RAM...");
                CreateFilesInRam();
                if (gDoVerbose)
                    ColorWrite(ConsoleColor.White, "Assigning file names...");
                AssignFileNames();
                if (File.Exists(dDirectoryName))
                    ColorWrite(ConsoleColor.Red, "Error! File '{0}' exists! Can't proceed!", dDirectoryName);
                else if (Directory.Exists(dDirectoryName))
                {
                    ColorWrite(ConsoleColor.Red, "Error! Directory '{0}' exists! Can't proceed!", dDirectoryName);
                }
                else
                {
                    ColorWrite(ConsoleColor.Green, "Creating new directory '{0}' to store files...", dDirectoryName);
                    Directory.CreateDirectory(dDirectoryName);
                    ColorWrite(ConsoleColor.Green, "Creating files...");
                    CreateFiles();
                    ColorWritePlus(ConsoleColor.White, ConsoleColor.Gray, "All work done!");
                }
            }
        }

        private static void DoConstruct()
        {
            if (gDoVerbose)
                ColorWrite(ConsoleColor.White, "Reading all files from folder into RAM...");
            GetFiles();
            if (CheckForNoneFiles())
            {
                ColorWrite(ConsoleColor.Red, "Error! There is no complitable files in folder!");
            }
            else
            {
                ColorWrite(ConsoleColor.Green, "Count of files inside directory: {0}", dFileCount);
                if (gDoVerbose)
                    ColorWrite(ConsoleColor.White, "Getting information for construction...");
                SetConstructiveInformation();
                string path = dDirectoryName + ".pak" + gSubMode;
                if (File.Exists(path))
                {
                    ColorWrite(ConsoleColor.Red, "Error! File '{0}' is already exists!", path);
                }
                else
                {
                    ColorWrite(ConsoleColor.Green, "Assembling '{0}'...", path);
                    DoAssemble();
                    ColorWritePlus(ConsoleColor.White, ConsoleColor.Gray, "All work done!");
                }
            }
        }

        private static void DoTest()
        {
            byte[] numArray = File.ReadAllBytes(gObject);
            List<int> intList = new List<int>();
            List<string> stringList = new List<string>();
            for (int index = 0; index < 30; ++index)
            {
                intList.Add(BitConverter.ToInt32(numArray, index * 4));
                stringList.Add(BitConverter.ToString(numArray, index * 4, 4));
            }
            ColorWritePlus(ConsoleColor.White, ConsoleColor.Blue, "Mode: 1/(1)");
            ColorWrite(ConsoleColor.Yellow, "First 4bytes: {0} ({1})", intList[0], stringList[0]);
            ColorWrite(ConsoleColor.Yellow, "Next bytes:");
            for (int index = 1; index < 30; ++index)
                ColorWrite(ConsoleColor.White, "{0} ({1})", intList[index], stringList[index]);
            ColorWritePlus(ConsoleColor.White, ConsoleColor.Blue, "Mode: 1/(2)");
            ColorWrite(ConsoleColor.Yellow, "First 4bytes: {0} ({1})", intList[0], stringList[0]);
            ColorWrite(ConsoleColor.Yellow, "Next bytes:");
            for (int index = 1; index < 15; ++index)
                ColorWrite(ConsoleColor.White, "{0} ({1}) | {2} ({3})", intList[2 * index - 1], stringList[2 * index - 1], intList[2 * index], stringList[2 * index]);
            ColorWritePlus(ConsoleColor.White, ConsoleColor.Blue, "Mode: 1/(3)");
            ColorWrite(ConsoleColor.Yellow, "First 4bytes: {0} ({1})", intList[0], stringList[0]);
            ColorWrite(ConsoleColor.Yellow, "Next bytes:");
            for (int index = 1; index < 10; ++index)
                ColorWrite(ConsoleColor.White, "{0} ({1}) | {2} ({3}) | {4} ({5})", intList[3 * index - 2], stringList[3 * index - 2], intList[3 * index - 1], stringList[3 * index - 1], intList[3 * index], stringList[3 * index]);
            ColorWrite(ConsoleColor.Cyan, "Test ended!");
        }

        private static void Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "-help")
            {
                string processName = Process.GetCurrentProcess().ProcessName;
                ColorWrite(ConsoleColor.Green, "Pakcomposer .NET 5.0 Version");
                ColorWrite(ConsoleColor.Green, "Generously donated by Temple of Tales Translations team");
                ColorWrite(ConsoleColor.Green, "http://temple-tales.ru/translations.html");
                ColorWrite(ConsoleColor.White, "Program that disassembles and assembles archives from Tales of... game series.");
                Console.WriteLine("Usage:");
                Console.WriteLine(processName + " ([action flag] [file/folder name]) ([mode flag]) ([addictional flags])");
                Console.WriteLine(" ");
                Console.WriteLine("Action flags:");
                Console.WriteLine("-d - decompose / split to files");
                Console.WriteLine("-c - compose / put everything in 1 file");
                Console.WriteLine("-t - test (debug) file");
                Console.WriteLine("Mode flags:");
                Console.WriteLine("-0 - work with pak0 files");
                Console.WriteLine("-1 - work with pak1 files");
                Console.WriteLine("-3 - work with pak3 files");
                Console.WriteLine("Additional flags:");
                Console.WriteLine("-x - try to set extensions to files");
                Console.WriteLine("-v - verbose mode");
                Console.WriteLine("-a - align files to 16 bytes");
                Console.WriteLine("-u - automatically use comptoe.exe (needs comptoe.exe be in the same folder as {0}.exe)", processName);
                Console.WriteLine("-tod2_ps2_skit_padding - padding addition mode");
                Console.WriteLine(" ");
                ColorWritePlus(ConsoleColor.Yellow, ConsoleColor.Blue, "WARNING! DON'T WORK WITH FILES WITH");
                ColorWritePlus(ConsoleColor.Yellow, ConsoleColor.Blue, "EXTENSIONS WHEN COMPRESSING BACK! BE CAREFUL!");
                Console.WriteLine(" ");
                Console.WriteLine("Examples of usage:");
                Console.WriteLine(processName + " -d 00000.pak0 -0 -u");
                Console.WriteLine(processName + " -d 00000.pak1 -1 -x -v");
                Console.WriteLine(processName + " -d 00000.pak3 -3 -x");
                Console.WriteLine(processName + " -c 00000 -0");
                Console.WriteLine(processName + " -c 00000 -1 -v");
                Console.WriteLine(processName + " -c 00000 -3 -v -u -x");
            }
            else
            {
                GetFlags(args);
                if (gMode == 'N')
                    ColorWrite(ConsoleColor.Red, "Error - you haven't specified main flag! (-d or -c)");
                else if (gObject == "" || gMode == 'D' && !File.Exists(gObject) || (gMode == 'T' && !File.Exists(gObject) || gMode == 'C' && !Directory.Exists(gObject)))
                    ColorWrite(ConsoleColor.Red, "Error - you specified wrong filename/directory!");
                else if (gSubMode == 'N')
                {
                    ColorWrite(ConsoleColor.Red, "Error - you haven't specified mode flag! (-0 or -1 or -3)");
                }
                else
                {
                    switch (gMode)
                    {
                        case 'C':
                            ColorWrite(ConsoleColor.White, "Composition to '{0}.pak{1}' started", gObject, gSubMode);
                            dDirectoryName = gObject;
                            DoConstruct();
                            break;
                        case 'D':
                            ColorWrite(ConsoleColor.White, "Decomposition of '{0}' started", gObject, gSubMode);
                            dDirectoryName = CutToExtension(gObject);
                            DoDeconstruct();
                            break;
                        case 'T':
                            ColorWrite(ConsoleColor.White, "Testing of '{0}' started!", gObject);
                            DoTest();
                            break;
                    }
                }
            }
            //Console.ReadKey();
        }
    }
}
