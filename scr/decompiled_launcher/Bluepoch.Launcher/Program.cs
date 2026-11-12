using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Bluepoch.Launcher.App;
using Bluepoch.Launcher.Config;

namespace Bluepoch.Launcher;

internal static class Program
{
	public static readonly DateTime StartTime = DateTime.Now;

	private static nint _modulePtr;

	private static Mutex? appMutex;

	private const int SW_RESTORE = 9;

	private const nint DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4;

	[DllImport("user32.dll")]
	private static extern bool SetForegroundWindow(nint hWnd);

	[DllImport("user32.dll")]
	private static extern nint FindWindow(string lpClassName, string lpWindowName);

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(nint hWnd, int nCmdShow);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool SetProcessDPIAware();

	[DllImport("user32.dll")]
	private static extern bool SetProcessDpiAwarenessContext(nint dpiContext);

	[STAThread]
	private static void Main()
	{
		appMutex = new Mutex(initiallyOwned: true, "reverse1999-launcher_Mutex", out var createdNew);
		if (!createdNew)
		{
			string lpWindowName = SettingConfigManager.GetInstance().GetManifest()["app_displayname"];
			nint num = FindWindow(null, lpWindowName);
			if (num != IntPtr.Zero)
			{
				ShowWindow(num, 9);
				SetForegroundWindow(num);
			}
			return;
		}
		try
		{
			if (Environment.OSVersion.Version.Major >= 6)
			{
				if (Environment.OSVersion.Version.Build >= 17763)
				{
					SetProcessDpiAwarenessContext(-4);
				}
				else
				{
					SetProcessDPIAware();
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("Set DPI Awareness failed: " + ex.Message);
		}
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);
		LauncherApplication.GetInstance().Init();
		Application.Run(new MainForm());
	}
}
