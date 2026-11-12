using System.IO;
using System.Windows.Forms;
using Bluepoch.Launcher.Config;

namespace Bluepoch.Launcher.Constants;

public static class AppDir
{
	public const string Dir_Download = "Downloads";

	public const string Dir_Download_Game = "DownloadGames";

	public const string Dir_Cache = "data";

	public const string Dir_Log = "logs";

	public const string Dir_Images = "Images";

	public const string Dir_Update = "updateProgram";

	public const string Dir_Update2 = "updateProgram2";

	public static string GetAppDownloadDir()
	{
		return Path.Combine(Application.StartupPath, AppSettings.Instance.AppDataDir, "Downloads");
	}

	public static string GetGameDownloadDir()
	{
		return Path.Combine(Application.StartupPath, AppSettings.Instance.AppDataDir, "DownloadGames");
	}

	public static string GetAppCacheDir()
	{
		return Path.Combine(Application.StartupPath, AppSettings.Instance.AppDataDir, "data");
	}

	public static string GetAppLogDir()
	{
		return Path.Combine(Application.StartupPath, AppSettings.Instance.AppDataDir, "logs");
	}

	public static string GetAppImageDir()
	{
		return Path.Combine(Application.StartupPath, AppSettings.Instance.AppDataDir, "Images");
	}

	public static string GetAppVideoDir()
	{
		return Path.Combine(GetAppWebDir(), "assets", "videos");
	}

	public static string GetAppWebDir()
	{
		return Path.Combine(Application.StartupPath, "runtimes/web");
	}

	public static string GetLibDir()
	{
		return Path.Combine(Application.StartupPath, "Lib");
	}
}
