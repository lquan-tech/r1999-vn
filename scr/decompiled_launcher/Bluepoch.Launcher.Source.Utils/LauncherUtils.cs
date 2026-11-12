using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;
using Bluepoch.Common.Util;
using Bluepoch.Launcher.Config;
using Bluepoch.Launcher.Constants;
using Bluepoch.Launcher.Properties;
using Microsoft.Win32;

namespace Bluepoch.Launcher.Source.Utils;

public class LauncherUtils
{
	private const string _regPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";

	public static bool IsValiadURL(string url)
	{
		if (url != null && url.Length != 0 && url.Contains("http"))
		{
			return url.Contains("https");
		}
		return false;
	}

	public static string GetExeVersion()
	{
		return Assembly.GetExecutingAssembly().GetName().Version.ToString();
	}

	public static bool VersionContrast(string serverVersion, string targetVersion)
	{
		if (string.IsNullOrEmpty(serverVersion))
		{
			return false;
		}
		if (string.IsNullOrEmpty(targetVersion))
		{
			return true;
		}
		int[] array = SplitVersion(serverVersion);
		int[] array2 = SplitVersion(targetVersion);
		int num = Math.Max(array.Length, array2.Length);
		for (int i = 0; i < num; i++)
		{
			int num2 = ((i < array.Length) ? array[i] : 0);
			int num3 = ((i < array2.Length) ? array2[i] : 0);
			if (num2 > num3)
			{
				return true;
			}
			if (num2 < num3)
			{
				return false;
			}
		}
		return false;
	}

	private static int[] SplitVersion(string version)
	{
		return version.Split('.').Select((string part) =>
		{
			int.TryParse(part, out var result);
			return result;
		}).ToArray();
	}

	public static bool SetAutoStart(string _appDisplayName)
	{
		try
		{
			string executablePath = Application.ExecutablePath;
			if (!File.Exists(executablePath))
			{
				BluepochLogger.ErrorLog("自启动失败：EXE 路径不存在");
				return false;
			}
			using Microsoft.Win32.RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: true);
			if (registryKey == null)
			{
				BluepochLogger.ErrorLog("自启动失败：无法打开注册表项");
				return false;
			}
			registryKey.SetValue(_appDisplayName, executablePath);
			BluepochLogger.InfoLog("自启动已开启");
			return true;
		}
		catch (Exception ex)
		{
			BluepochLogger.ErrorLog("自启动设置失败：" + ex.Message);
			return false;
		}
	}

	public static bool UnsetAutoStart(string _appDisplayName)
	{
		try
		{
			using Microsoft.Win32.RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: true);
			if (registryKey == null)
			{
				return false;
			}
			if (registryKey.GetValue(_appDisplayName) != null)
			{
				registryKey.DeleteValue(_appDisplayName);
				BluepochLogger.InfoLog("自启动已关闭");
				return true;
			}
		}
		catch (Exception ex)
		{
			BluepochLogger.ErrorLog("自启动取消失败：" + ex.Message);
		}
		return false;
	}

	public static bool IsAutoStartEnabled(string _appDisplayName)
	{
		using Microsoft.Win32.RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run");
		if (registryKey == null)
		{
			return false;
		}
		string executablePath = Application.ExecutablePath;
		string value = registryKey.GetValue(_appDisplayName) as string;
		return executablePath.Equals(value, StringComparison.OrdinalIgnoreCase);
	}

	public static string ConvertBytesPerSecondToReadable(double speedBytesPerSecond)
	{
		(string, double)[] array = new (string, double)[4]
		{
			("GB/s", 1073741824.0),
			("MB/s", 1048576.0),
			("KB/s", 1024.0),
			("B/s", 1.0)
		};
		for (int i = 0; i < array.Length; i++)
		{
			var (value, num) = array[i];
			if (speedBytesPerSecond >= num)
			{
				double value2 = speedBytesPerSecond / num;
				return $"{value2:F2} {value}";
			}
		}
		return $"{speedBytesPerSecond:F2} B/s";
	}

	public static string ConvertBytesToReadable(double totalBytes)
	{
		(string, double)[] array = new (string, double)[4]
		{
			("GB", 1073741824.0),
			("MB", 1048576.0),
			("KB", 1024.0),
			("B", 1.0)
		};
		for (int i = 0; i < array.Length; i++)
		{
			var (value, num) = array[i];
			if (totalBytes >= num)
			{
				double value2 = totalBytes / num;
				return $"{value2:F2} {value}";
			}
		}
		return $"{totalBytes:F2} B";
	}

	public static bool IsRunningAsAdmin()
	{
		try
		{
			return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
		}
		catch (Exception ex)
		{
			BluepochLogger.ErrorLog("IsRunningAsAdmin ex ：" + ex.Message);
			return false;
		}
	}

	internal static string FormatTimeFromSeconds(long timeRemain)
	{
		try
		{
			TimeSpan timeSpan = TimeSpan.FromSeconds(timeRemain);
			return (timeSpan.Hours > 0) ? $"{timeSpan.Hours}h{timeSpan.Minutes}m" : $"{timeSpan.Minutes}m{timeSpan.Seconds}s";
		}
		catch (Exception ex)
		{
			BluepochLogger.ErrorLog("FormatTimeFromSeconds ex ：" + ex.Message);
			return "--:--";
		}
	}

	public static Image ByteArrayToImage(byte[] byteArray)
	{
		using MemoryStream stream = new MemoryStream(byteArray);
		try
		{
			return Image.FromStream(stream, useEmbeddedColorManagement: true, validateImageData: true);
		}
		catch (ArgumentException)
		{
			return null;
		}
	}

	public static string GetAppIconFilePath()
	{
		return Path.Combine(Application.StartupPath, "Resources", Resources.AppIcon);
	}

	public static string GetVideoFilePathByUrl(string url)
	{
		string fileNameFromUrl = GetFileNameFromUrl(url);
		if (string.IsNullOrEmpty(fileNameFromUrl))
		{
			return string.Empty;
		}
		return Path.Combine(AppDir.GetAppVideoDir(), fileNameFromUrl);
	}

	public static string GetVideoTempFilePathByUrl(string url)
	{
		string fileNameFromUrl = GetFileNameFromUrl(url);
		if (string.IsNullOrEmpty(fileNameFromUrl))
		{
			return string.Empty;
		}
		return Path.Combine(AppDir.GetAppVideoDir(), new StringBuilder(fileNameFromUrl).Append(".tmp").ToString());
	}

	public static string GetFileNameFromUrl(string url)
	{
		return Path.GetFileName(new Uri(url).LocalPath);
	}

	public static string GetVideoFileInWebByFileName(string fileName)
	{
		if (string.IsNullOrEmpty(fileName))
		{
			return string.Empty;
		}
		return Path.Combine("assets", "videos", fileName);
	}

	public static bool is64BitProcess()
	{
		return IntPtr.Size == 8;
	}

	public static void OpenWebInBrowser(string targetUrl)
	{
		Process.Start(new ProcessStartInfo
		{
			FileName = targetUrl,
			UseShellExecute = true
		});
	}

	public static void OpenLauncherLogFolder()
	{
		OpenFolderInExplorer(Path.Combine(Application.StartupPath, "logs"));
	}

	public static void OpenGameLogFolder()
	{
		string value = "";
		SettingConfigManager.GetInstance().GetManifest()?.TryGetValue("gameLogDirName", out value);
		if (string.IsNullOrEmpty(value))
		{
			BluepochLogger.WarningLog("OpenGameLogFolder - gameLogDirName not configured in manifest");
			return;
		}
		OpenFolderInExplorer(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "bluepoch", value));
	}

	private static void OpenFolderInExplorer(string folderPath)
	{
		try
		{
			if (!Directory.Exists(folderPath))
			{
				Directory.CreateDirectory(folderPath);
			}
			Process.Start(new ProcessStartInfo
			{
				FileName = "explorer.exe",
				Arguments = "\"" + folderPath + "\"",
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			BluepochLogger.ErrorLog("OpenFolderInExplorer error: " + ex.Message + ", path=" + folderPath);
		}
	}

	public static void GetAvailableDiskDrives()
	{
		DriveInfo[] drives = DriveInfo.GetDrives();
		foreach (DriveInfo driveInfo in drives)
		{
			if (driveInfo.IsReady)
			{
				BluepochLogger.InfoLog($" Drive {0} ", driveInfo.Name);
				BluepochLogger.InfoLog($"  Drive type: {driveInfo.DriveType}");
				BluepochLogger.InfoLog($"  Volume label: {0}", driveInfo.VolumeLabel);
				BluepochLogger.InfoLog($"  File system: {0}", driveInfo.DriveFormat);
				BluepochLogger.InfoLog($"  Available space to current user:{driveInfo.AvailableFreeSpace} bytes");
				BluepochLogger.InfoLog($"  Total available space:          {driveInfo.TotalFreeSpace} bytes");
				BluepochLogger.InfoLog($"  Total size of drive:            {driveInfo.TotalSize} bytes ");
			}
		}
	}
}
