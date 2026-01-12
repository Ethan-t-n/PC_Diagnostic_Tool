using System;
using System.IO;
using System.Runtime.InteropServices;

class Program
{
    static void Main()
    {
        Console.WriteLine("PC Diagnostic Tool v0.1");
        Console.WriteLine("------------------------");

        PrintBasicSystemInfo();
        PrintDiskInfo();

        Console.WriteLine("\nScan complete.");
    }

    static void PrintBasicSystemInfo()
    {
        Console.WriteLine("\n== System Info ==");

        Console.WriteLine($"OS: {RuntimeInformation.OSDescription}");
        Console.WriteLine($"Architecture: {RuntimeInformation.OSArchitecture}");
        Console.WriteLine($"Machine Name: {Environment.MachineName}");
        Console.WriteLine($"User: {Environment.UserName}");
        Console.WriteLine($"CPU Cores: {Environment.ProcessorCount}");
        Console.WriteLine($".NET Version: {Environment.Version}");
    }

    static void PrintDiskInfo()
    {
        Console.WriteLine("\n== Disk Info ==");

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady) continue;

            Console.WriteLine($"Drive: {drive.Name}");
            Console.WriteLine($"  Format: {drive.DriveFormat}");
            Console.WriteLine($"  Total: {ToGb(drive.TotalSize)} GB");
            Console.WriteLine($"  Free:  {ToGb(drive.AvailableFreeSpace)} GB");
        }
    }

    static long ToGb(long bytes) => bytes / 1024 / 1024 / 1024;
}
