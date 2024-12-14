using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading.Tasks;

namespace RAMLimiter
{
    class Program
    {
        // Importing the Windows API function for clearing the working set memory
        [DllImport("psapi.dll")]
        static extern int EmptyWorkingSet(IntPtr hProcess);

        // Check if the program is running with administrator privileges
        static bool IsAdmin()
        {
            WindowsIdentity id = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(id);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        // Attempt to restart the program with elevated privileges if not already running as admin
        static void ElevatePrivileges()
        {
            if (!IsAdmin())
            {
                var procInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = Assembly.GetEntryAssembly().Location,
                    Verb = "runas"
                };
                try
                {
                    Process.Start(procInfo);
                    Environment.Exit(0); // Exit the current process after launching the elevated one
                }
                catch
                {
                    Console.WriteLine("Failed to request administrator privileges.");
                }
            }
        }

        // Get a list of processes by their names
        static List<Process> GetProcessesByNames(IEnumerable<string> processNames)
        {
            return processNames.SelectMany(name => Process.GetProcessesByName(name)).ToList();
        }

        // Reduce the memory usage of a specific process by clearing its working set
        static void ReduceRamUsage(Process process)
        {
            try
            {
                if (!process.HasExited && process.Id != 0)
                {
                    EmptyWorkingSet(process.Handle);
                    Console.WriteLine($"Memory of process {process.ProcessName} ({process.Id}) has been reduced.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reducing memory for {process.ProcessName}: {ex.Message}");
            }
        }

        // Display the current memory usage of a process
        static void DisplayMemoryUsage(Process process)
        {
            try
            {
                process.Refresh(); // Refresh process properties
                Console.WriteLine($"Process: {process.ProcessName}, Memory: {process.WorkingSet64 / (1024 * 1024)} MB");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving memory usage: {ex.Message}");
            }
        }

        // Monitor specified processes and reduce their memory usage periodically
        static async Task MonitorAndReduceRamAsync(IEnumerable<string> processNames)
        {
            while (true)
            {
                var processes = GetProcessesByNames(processNames);
                foreach (var process in processes)
                {
                    DisplayMemoryUsage(process);
                    ReduceRamUsage(process);
                }
                Console.WriteLine("Waiting 3 seconds...");
                await Task.Delay(3000); // Wait for 3 seconds before the next cycle
            }
        }

        static async Task Main(string[] args)
        {
            ElevatePrivileges(); // Ensure the program has elevated privileges

            Console.WriteLine("Enter the process names separated by commas (e.g., chrome,discord,obs):");
            var input = Console.ReadLine();
            var processNames = input.Split(',').Select(p => p.Trim()).ToList();

            Console.WriteLine("Starting memory optimization...");
            await MonitorAndReduceRamAsync(processNames); // Begin monitoring and reducing memory usage
        }
    }
}
