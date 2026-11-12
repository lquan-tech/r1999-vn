using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Bluepoch.Common.Util;
using Bluepoch.Launcher.Config;
using Bluepoch.Launcher.Constants;
using Bluepoch.Launcher.Source.Download.Files;
using Newtonsoft.Json;

namespace Bluepoch.Launcher.Source.Game;

internal class GameCommon
{
	public static string GetGameInstallVersion()
	{
		try
		{
			return Regeditmgr.ReadValue(SettingConfigManager.GetInstance().GetManifest()["projectgroupname"], "game_exe_install_version");
		}
		catch (Exception ex)
		{
			BluepochLogger.ErrorLog("GetGameInstallVersion Exception: " + ex.Message);
			return "";
		}
	}

	public static bool RecordInstallVersion(string gameVersion)
	{
		try
		{
			Regeditmgr.SetValue(SettingConfigManager.GetInstance().GetManifest()["projectgroupname"], "game_exe_install_version", gameVersion);
			return true;
		}
		catch (Exception ex)
		{
			BluepochLogger.ErrorLog("RecordInstallVersion Exception: " + ex.Message);
			return false;
		}
	}

	public static bool RecordInstallPath(string dir)
	{
		try
		{
			Regeditmgr.SetValue(SettingConfigManager.GetInstance().GetManifest()["projectgroupname"], "game_exe_install_dir", dir);
			return true;
		}
		catch (Exception ex)
		{
			BluepochLogger.ErrorLog("RecordInstallPath Exception: " + ex.Message);
			return false;
		}
	}

	public static string GetGameVersionInSettingFile()
	{
		return AppSettings.Instance.GameExeVersion;
	}

	public static string GetGameExeDefaultInstallDir()
	{
		return Path.Combine(Application.StartupPath, SettingConfigManager.GetInstance().GetManifest()["gameExeDir"]);
	}

	public static string GetGameExeInstalledDir()
	{
		return Regeditmgr.ReadValue(SettingConfigManager.GetInstance().GetManifest()["projectgroupname"], "game_exe_install_dir");
	}

	public static string GetGameExeInstallDirByDefault()
	{
		string text = AppSettings.Instance.GameExePath;
		if (string.IsNullOrEmpty(text))
		{
			text = Regeditmgr.ReadValue(SettingConfigManager.GetInstance().GetManifest()["projectgroupname"], "game_exe_install_dir");
		}
		if (string.IsNullOrEmpty(text))
		{
			text = GetGameExeDefaultInstallDir();
		}
		return text;
	}

	public static void UpdateGameDirPathInCache(string exeDirPath)
	{
		AppSettings.Instance.GameExePath = exeDirPath;
		AppSettings.Instance.Save();
	}

	public static void UpdateGameVersionInCache(string gameVersion)
	{
		AppSettings.Instance.GameExeVersion = gameVersion;
		AppSettings.Instance.Save();
	}

	public static void UpdateGameStatusInCache(int status)
	{
		AppSettings.Instance.GameInstallStatus = status;
		AppSettings.Instance.Save();
	}

	public static void UpdateGameSizeInCache(long size)
	{
		AppSettings.Instance.GameZipsTotalSize = size;
		AppSettings.Instance.Save();
	}

	public static void UpdateGameUpdateTypeInCache(int type)
	{
		AppSettings.Instance.GameUpdateType = type;
		AppSettings.Instance.Save();
	}

	public static void UpdateGameDownloadTypeInCache(int type)
	{
		if (type != AppSettings.Instance.GameDownloadType)
		{
			AppSettings.Instance.GameDownloadType = type;
			AppSettings.Instance.Save();
		}
	}

	public static string GetGameDownloadTempFilePath(string url)
	{
		try
		{
			string gameDownloadRealFilePath = GetGameDownloadRealFilePath(url);
			if (string.IsNullOrEmpty(gameDownloadRealFilePath))
			{
				return string.Empty;
			}
			return new StringBuilder(gameDownloadRealFilePath).Append(".tmp").ToString();
		}
		catch (Exception ex)
		{
			BluepochLogger.ErrorLog("GetGameDownloadTempFilePath error: " + ex.Message + " url=" + url);
			return string.Empty;
		}
	}

	public static long ComputerDownloadZipFileSize(string url)
	{
		string gameExeInstallDirByDefault = GetGameExeInstallDirByDefault();
		long num = 0L;
		FileInfo fileInfo = new FileInfo(Path.Combine(gameExeInstallDirByDefault, GetGameDownloadRealFilePath(url)));
		if (fileInfo.Exists && fileInfo.Length > 0)
		{
			num += fileInfo.Length;
		}
		else
		{
			FileInfo fileInfo2 = new FileInfo(Path.Combine(gameExeInstallDirByDefault, GetGameDownloadTempFilePath(url)));
			if (fileInfo2.Exists && fileInfo2.Length > 0)
			{
				num += fileInfo2.Length;
			}
		}
		return num;
	}

	public static string GetUrlFromFilePath(string filepath, List<string> urls)
	{
		string fileName = Path.GetFileName(filepath);
		foreach (string url in urls)
		{
			if (url.Contains(fileName))
			{
				return url;
			}
		}
		return null;
	}

	public static string GetGameDownloadRealFilePath(string url)
	{
		try
		{
			if (string.IsNullOrEmpty(url))
			{
				return string.Empty;
			}
			string fileName = Path.GetFileName(new Uri(url).LocalPath);
			if (string.IsNullOrEmpty(fileName))
			{
				return string.Empty;
			}
			return Path.Combine(GetGameExeInstallDirByDefault(), fileName);
		}
		catch (Exception ex)
		{
			BluepochLogger.ErrorLog("GetFileNameFromUrl error: " + ex.Message + " url=" + url);
			return string.Empty;
		}
	}

	public static long GetGameZipUnPackSizeFromPath(string path)
	{
		string fileName = Path.GetFileName(path);
		if (string.IsNullOrEmpty(fileName))
		{
			return 0L;
		}
		Match match = Regex.Match(fileName, "origin_(\\d+)");
		if (match.Success)
		{
			return Convert.ToInt64(match.Groups[1].Value);
		}
		return 0L;
	}

	public static long GetGameZipUnPackSizeFromUrl(string url)
	{
		if (string.IsNullOrEmpty(Path.GetFileName(new Uri(url).LocalPath)))
		{
			return 0L;
		}
		Match match = Regex.Match(url, "origin_(\\d+)");
		if (match.Success)
		{
			return Convert.ToInt64(match.Groups[1].Value);
		}
		return 0L;
	}

	public static long GetGameZipSizeFromUrl(string url)
	{
		string fileName = Path.GetFileName(new Uri(url).LocalPath);
		if (string.IsNullOrEmpty(fileName))
		{
			return 0L;
		}
		Match match = Regex.Match(fileName, "size_(\\d+)");
		if (match.Success)
		{
			return Convert.ToInt64(match.Groups[1].Value);
		}
		return 0L;
	}

	public static long GetGameRealTotalSizeFromUrl(string url)
	{
		if (string.IsNullOrEmpty(Path.GetFileName(new Uri(url).LocalPath)))
		{
			return 0L;
		}
		Match match = Regex.Match(url, "total_(\\d+)");
		if (match.Success)
		{
			return Convert.ToInt64(match.Groups[1].Value);
		}
		return 0L;
	}

	public static string GetGameExeFilePath()
	{
		return Path.Combine(GetGameExeInstallDirByDefault(), SettingConfigManager.GetInstance().GetManifest()["gameExeFile"]);
	}

	public static async Task<MultiFilesDownloadDirInfo> GetDownloadInfoFromJsonFileAsync(string jsonStringFilePath)
	{
		try
		{
			return JsonConvert.DeserializeObject<MultiFilesDownloadDirInfo>(await File.ReadAllTextAsync(jsonStringFilePath));
		}
		catch (Exception ex)
		{
			BluepochLogger.ErrorLog("GetDownloadInfoFromJsonFileAsync error: " + ex.Message);
			return null;
		}
	}

	public static string GetGameJsonFilePath(string jsonUrl)
	{
		return Path.Combine(AppDir.GetAppDownloadDir(), Path.GetFileName(new Uri(jsonUrl).LocalPath));
	}

	public static string GetDbFilePath(string gameFileJsonUrl)
	{
		string text = Path.GetFileName(new Uri(gameFileJsonUrl).LocalPath).Replace(".json", "");
		return Path.Combine(AppDir.GetAppCacheDir(), text + ".db");
	}

	public static void ClearDownloadGameSettingCache()
	{
		AppSettings.Instance.GameExeVersion = "";
		AppSettings.Instance.GameInstallStatus = 0;
		AppSettings.Instance.GameZipsTotalSize = 0L;
		AppSettings.Instance.GameUpdateType = 1;
		AppSettings.Instance.GameDownloadType = 0;
		AppSettings.Instance.Save();
	}

	public static HashSet<string> LoadExtractedFilesRecord()
	{
		HashSet<string> hashSet = new HashSet<string>();
		string path = Path.Combine(AppDir.GetAppCacheDir(), "extracted_files_record.txt");
		try
		{
			if (File.Exists(path))
			{
				string[] array = File.ReadAllLines(path);
				foreach (string text in array)
				{
					if (!string.IsNullOrWhiteSpace(text))
					{
						hashSet.Add(text.Trim());
					}
				}
				BluepochLogger.DebugLog($"Read extracted files record success, total {hashSet.Count} files");
				return hashSet;
			}
			BluepochLogger.DebugLog("Extracted files record not found, will create a new record");
			return hashSet;
		}
		catch (Exception ex)
		{
			BluepochLogger.ErrorLog("Read extracted files record failed: " + ex.Message);
			return hashSet;
		}
	}
}
