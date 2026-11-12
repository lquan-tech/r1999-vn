using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Bluepoch.Common.Util;
using Bluepoch.Launcher.Constants;
using Bluepoch.Launcher.Properties;
using Bluepoch.Launcher.Source.Game;
using Bluepoch.Launcher.Utils;

namespace Bluepoch.Launcher.Config;

internal class SettingConfigManager
{
	private static SettingConfigManager _instance;

	private Dictionary<string, string> manifest;

	private string config_path;

	private int launcherType;

	private ConfigKey.Config_Group currentGroup;

	private Dictionary<string, string> launcherConfig;

	private Dictionary<string, string> gameConfig;

	public static SettingConfigManager GetInstance()
	{
		if (_instance == null)
		{
			_instance = new SettingConfigManager();
		}
		return _instance;
	}

	private SettingConfigManager()
	{
		InitResource();
		InitOutConfig();
	}

	private void InitResource()
	{
		launcherType = Convert.ToInt32(Resources.GameId ?? "-1");
		if (launcherType == 60001)
		{
			string content = Encoding.UTF8.GetString(Resources.launchermanifest_pjm_en);
			loadManifest(content);
		}
		else if (launcherType == 70001)
		{
			string content2 = Encoding.UTF8.GetString(Resources.launchermanifest_pjm_jp);
			loadManifest(content2);
		}
		else if (launcherType == 90001)
		{
			string content3 = Encoding.UTF8.GetString(Resources.launchermanifest_pjm_kr);
			loadManifest(content3);
		}
		else if (launcherType != 50001)
		{
			BluepochLogger.InfoLog($"launcherType  :  {launcherType}");
		}
	}

	private void loadManifest(string content)
	{
		string[] array = content.Split(new string[1] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
		manifest = new Dictionary<string, string>();
		string[] array2 = array;
		foreach (string text in array2)
		{
			if (!string.IsNullOrWhiteSpace(text) && !text.StartsWith("#"))
			{
				string[] array3 = text.Split('=');
				if (array3.Length == 2)
				{
					string text2 = array3[0].Trim();
					string text3 = array3[1].Trim();
					BluepochLogger.DebugLog(text2 + ": " + text3);
					manifest.Add(text2, text3);
				}
			}
		}
	}

	private void InitOutConfig()
	{
		int num = Convert.ToInt32(Resources.GameId);
		BluepochLogger.DebugLog($"InitOutConfig - GameID  :  {num}");
		try
		{
			config_path = Path.Combine(FileUtil.GetAppDirectory(), Bluepoch.Launcher.Constants.Common.GetConfigFileByGameId(num));
			if (!new FileInfo(config_path).Exists)
			{
				return;
			}
			launcherConfig = new Dictionary<string, string>();
			gameConfig = new Dictionary<string, string>();
			using StreamReader streamReader = new StreamReader(config_path, Encoding.UTF8);
			string text;
			while ((text = streamReader.ReadLine()) != null)
			{
				if (text.Equals("[Launcher]"))
				{
					currentGroup = ConfigKey.Config_Group.Launcher;
				}
				if (currentGroup == ConfigKey.Config_Group.Launcher)
				{
					string[] array = text.Split("=");
					if (array.Length == 2)
					{
						launcherConfig[array[0]] = array[1];
					}
				}
				if (text.Equals("[Game]"))
				{
					currentGroup = ConfigKey.Config_Group.Game;
				}
				if (currentGroup == ConfigKey.Config_Group.Game)
				{
					string[] array2 = text.Split("=");
					if (array2.Length == 2)
					{
						gameConfig[array2[0]] = array2[1];
					}
				}
			}
		}
		catch (Exception)
		{
		}
	}

	public async Task ReSetAppSettingByOutConfig()
	{
		await Task.Run(() =>
		{
			try
			{
				if (GetValueFromConfig("Launcher", "runningtype_e6492ceabf0b22bd4dbc15f67c090a4c", "3").Trim() == "3")
				{
					File.Delete(config_path);
				}
				if (launcherConfig != null && launcherConfig.Count > 0)
				{
					foreach (string key in launcherConfig.Keys)
					{
						if (key.Trim().Equals("language_index"))
						{
							string value = launcherConfig[key];
							AppSettings.Instance.Language_index = Convert.ToInt16(value);
						}
						if (key.Trim().Equals("downloadspeed"))
						{
							string value2 = launcherConfig[key];
							AppSettings.Instance.DownloadSpeed = Convert.ToInt16(value2);
						}
						if (key.Trim().Equals("notification_enable"))
						{
							string value3 = launcherConfig[key];
							AppSettings.Instance.NotificationsEnabled = Convert.ToBoolean(value3);
						}
						if (key.Trim().Equals("close_tpye"))
						{
							string value4 = launcherConfig[key];
							AppSettings.Instance.Close_tpye = Convert.ToInt16(value4);
						}
						if (key.Trim().Equals("game_install_path"))
						{
							string gameExePath = launcherConfig[key];
							AppSettings.Instance.GameExePath = gameExePath;
						}
					}
				}
				if (gameConfig != null && gameConfig.Count > 0)
				{
					foreach (string key2 in gameConfig.Keys)
					{
						if (key2.Trim().Equals("gameversion"))
						{
							string gameExeVersion = gameConfig[key2];
							AppSettings.Instance.GameExeVersion = gameExeVersion;
						}
						if (key2.Trim().Equals("status"))
						{
							string value5 = gameConfig[key2];
							AppSettings.Instance.GameInstallStatus = Convert.ToInt16(value5);
						}
						if (key2.Trim().Equals("updatetype"))
						{
							string value6 = gameConfig[key2];
							AppSettings.Instance.GameUpdateType = Convert.ToInt16(value6);
						}
						if (key2.Trim().Equals("totalsize"))
						{
							string value7 = gameConfig[key2];
							AppSettings.Instance.GameZipsTotalSize = Convert.ToInt64(value7);
						}
					}
				}
				AppSettings.Instance.Save();
				string[] reSetUpExeFiles = ReSetUpExeName.ReSetUpExeFiles;
				foreach (string path in reSetUpExeFiles)
				{
					string path2 = Path.Combine(GameCommon.GetGameExeInstallDirByDefault(), path);
					if (File.Exists(path2))
					{
						File.Delete(path2);
					}
				}
			}
			catch (Exception)
			{
			}
		});
	}

	public string GetValueFromConfig(string group, string key, string defaultValue = "")
	{
		if (!new FileInfo(config_path).Exists)
		{
			return defaultValue;
		}
		if (launcherConfig == null || launcherConfig.Count == 0)
		{
			return defaultValue;
		}
		if (group.Equals("Launcher"))
		{
			if (launcherConfig.ContainsKey(key))
			{
				return launcherConfig[key];
			}
			return defaultValue;
		}
		return "unknow_config";
	}

	public Dictionary<string, string> GetManifest()
	{
		return manifest;
	}
}
