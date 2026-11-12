using System;
using System.Diagnostics;
using System.IO;

namespace Reverse1999Launcher
{
    class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                return 1;
            }

            string exePath = args[0].Trim('"', '\'');
            if (!File.Exists(exePath))
            {
                return 2;
            }

            string workingDir = Path.GetDirectoryName(exePath);
            string gameArgs = args.Length > 1 ? args[1] : "";

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = workingDir,
                    Arguments = gameArgs,
                    UseShellExecute = true
                };

                Process p = Process.Start(psi);
                return p != null ? 0 : 3;
            }
            catch
            {
                try
                {
                    ProcessStartInfo psiAdmin = new ProcessStartInfo
                    {
                        FileName = exePath,
                        WorkingDirectory = workingDir,
                        Arguments = gameArgs,
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    Process pAdmin = Process.Start(psiAdmin);
                    return pAdmin != null ? 0 : 4;
                }
                catch
                {
                    return 5;
                }
            }
        }
    }
}
