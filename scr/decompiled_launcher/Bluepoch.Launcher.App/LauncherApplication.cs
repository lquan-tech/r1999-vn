using System;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using Bluepoch.Common.Util;
using Bluepoch.Launcher.Bean;
using Bluepoch.Launcher.Config;
using Bluepoch.Launcher.Constants;
using Bluepoch.Launcher.Properties;
using Bluepoch.Launcher.Source.Bean;
using Bluepoch.Launcher.Source.Game;
using Bluepoch.Launcher.Source.Utils;
using ThinkingSDK.PC;

namespace Bluepoch.Launcher.App;

internal class LauncherApplication
{
	private LauncherDeviceInfo deviceInfo;

	private LauncherAppPackageInfo appPackageInfo;

	private int runningType = 3;

	private int langIndex = 1;

	private bool isDebugMode;

	private static LauncherApplication _instance = null;

	private static string[] FontStyleList = new string[5]
	{
		Resources.Font_Name_Cn,
		Resources.Font_Name_En,
		Resources.Font_Name_Jp,
		Resources.Font_Name_Kr,
		Resources.Font_Name_CnTW
	};

	public static LauncherApplication GetInstance()
	{
		if (_instance == null)
		{
			_instance = new LauncherApplication();
		}
		return _instance;
	}

	private LauncherApplication()
	{
	}

	public void Init()
	{
		BluepochLogger.BluepochLoggerLevel bluepochLoggerLevel = (BluepochLogger.BluepochLoggerLevel)Convert.ToInt32(SettingConfigManager.GetInstance().GetValueFromConfig("Launcher", "loglevel_e6492ceabf0b22bd4dbc15f67c090a4c", "0"));
		deviceInfo = new LauncherDeviceInfo();
		appPackageInfo = new LauncherAppPackageInfo();
		runningType = Convert.ToInt16(SettingConfigManager.GetInstance().GetValueFromConfig("Launcher", "runningtype_e6492ceabf0b22bd4dbc15f67c090a4c", "3"));
		string value = ((runningType == 2) ? "Debug Mode" : "Release Mode");
		isDebugMode = Convert.ToBoolean(SettingConfigManager.GetInstance().GetValueFromConfig("Launcher", "debugmode_e6492ceabf0b22bd4dbc15f67c090a4c", "false"));
		bool isDebugLogAsynForTest = Convert.ToBoolean(SettingConfigManager.GetInstance().GetValueFromConfig("Launcher", "logasyn_e6492ceabf0b22bd4dbc15f67c090a4c", "false"));
		BluepochLogger.Init("logs/app-.log", bluepochLoggerLevel, isDebugMode, isDebugLogAsynForTest);
		BluepochLogger.InfoLog("Application Version : " + LauncherUtils.GetExeVersion());
		BluepochLogger.InfoLog($"Application Start Running: [{value}]-[{bluepochLoggerLevel}]-[{isDebugMode}]-[{deviceInfo.deviceId}]");
		BluepochLogger.InfoLog("Application Start Path : " + Application.StartupPath);
		InitLangIndexAndFont();
		InitDataTrackSDK();
		CreateNecessaryDir();
	}

	private void InitDataTrackSDK()
	{
		ThinkingPCSDKManager instance = ThinkingPCSDKManager.getInstance();
		if (runningType == 2)
		{
			instance.InitThinkingPCSDK(SettingConfigManager.GetInstance().GetManifest()["Datasdk_appid_test"], SettingConfigManager.GetInstance().GetManifest()["Data_Test_host"], SettingConfigManager.GetInstance().GetManifest()["projectgroupname"], isLog: true);
		}
		else
		{
			instance.InitThinkingPCSDK(SettingConfigManager.GetInstance().GetManifest()["Datasdk_appid_prod"], SettingConfigManager.GetInstance().GetManifest()["Data_Release_host"], SettingConfigManager.GetInstance().GetManifest()["projectgroupname"], isLog: true);
		}
		ProppertiesBuild proppertiesBuild = new ProppertiesBuild();
		proppertiesBuild.GameId(Resources.GameId).GameName(SettingConfigManager.GetInstance().GetManifest()["gameName"]).GameCode(SettingConfigManager.GetInstance().GetManifest()["gameCode"])
			.AppVersion(LauncherUtils.GetExeVersion())
			.PackageName(SettingConfigManager.GetInstance().GetManifest()["appPackageName"])
			.ChannelId(SettingConfigManager.GetInstance().GetManifest()["channelId"])
			.SubChannelId(SettingConfigManager.GetInstance().GetManifest()["subChannelId"])
			.BuildCode(Resources.AppBuildCode);
		instance.SetSuperPropperties(proppertiesBuild);
	}

	private void InitLangIndexAndFont()
	{
		if (Convert.ToInt32(Resources.GameId ?? "-1") == 90001)
		{
			langIndex = 3;
			AppSettings.Instance.Language_index = langIndex;
			AppSettings.Instance.Save();
			return;
		}
		langIndex = AppSettings.Instance.Language_index;
		if (langIndex == -1)
		{
			string name = CultureInfo.InstalledUICulture.Name;
			if (name.Equals("zh-cn"))
			{
				langIndex = 0;
			}
			else if (name.Equals("zh-tw") || name.Equals("zh-hk") || name.Equals("zh_tw") || name.Equals("zh_hk") || name.Equals("zh-mo") || name.Equals("zh-mo"))
			{
				langIndex = 4;
			}
			else if (name.StartsWith("en"))
			{
				langIndex = 1;
			}
			else if (name.StartsWith("ja"))
			{
				langIndex = 2;
			}
			else if (name.StartsWith("ko"))
			{
				langIndex = 3;
			}
			else
			{
				langIndex = 1;
			}
			AppSettings.Instance.Language_index = langIndex;
			AppSettings.Instance.Save();
		}
	}

	public LauncherDeviceInfo GetDeviceInfo()
	{
		return deviceInfo;
	}

	public LauncherAppPackageInfo GetAppPackageInfo()
	{
		return appPackageInfo;
	}

	public int GetRunningType()
	{
		return runningType;
	}

	public bool IsDebugMode()
	{
		return isDebugMode;
	}

	public int GetLangIndex()
	{
		return AppSettings.Instance.Language_index;
	}

	public string GetFontStyle()
	{
		return FontStyleList[AppSettings.Instance.Language_index];
	}

	public AppSettingsInfo GetAppSettingsInfo()
	{
		string appDisplayName = SettingConfigManager.GetInstance().GetManifest()["app_displayname"];
		int close_tpye = AppSettings.Instance.Close_tpye;
		return new AppSettingsInfo
		{
			gameId = Resources.GameId,
			runningType = GetInstance().GetRunningType(),
			launcherLang = GetInstance().GetLangIndex(),
			appVersion = LauncherUtils.GetExeVersion(),
			isAutoStart = LauncherUtils.IsAutoStartEnabled(appDisplayName),
			isMinimizednByCloseBtn = (close_tpye == 2),
			notifyable = AppSettings.Instance.NotificationsEnabled,
			downloadSpeed = AppSettings.Instance.DownloadSpeed,
			downloadSpeedLimit = AppSettings.Instance.DownloadSpeedLimit,
			gameExeDownloadDir = GameCommon.GetGameExeInstallDirByDefault(),
			gameExeInstallDir = GameCommon.GetGameExeInstalledDir(),
			gameVersion = GameCommon.GetGameInstallVersion(),
			langFontFamily = GetFontStyle(),
			bgVideoName = GetVideoForWeb(AppSettings.Instance.BgVideoName),
			lastLogTicketId = AppSettings.Instance.GetValue("LastLogTicketId", "")
		};
	}

	private void CreateNecessaryDir()
	{
		try
		{
			string appCacheDir = AppDir.GetAppCacheDir();
			if (!Directory.Exists(appCacheDir))
			{
				Directory.CreateDirectory(appCacheDir);
			}
			string appDownloadDir = AppDir.GetAppDownloadDir();
			if (!Directory.Exists(appDownloadDir))
			{
				Directory.CreateDirectory(appDownloadDir);
			}
			string appImageDir = AppDir.GetAppImageDir();
			if (!Directory.Exists(appImageDir))
			{
				Directory.CreateDirectory(appImageDir);
			}
			string appVideoDir = AppDir.GetAppVideoDir();
			if (!Directory.Exists(appVideoDir))
			{
				Directory.CreateDirectory(appVideoDir);
			}
		}
		catch (Exception value)
		{
			BluepochLogger.ErrorLog($"CreateNecessaryDir error : {value}");
		}
	}

	public void InvokeOnUiThread(Action action)
	{
		if (Application.OpenForms.Count > 0)
		{
			Application.OpenForms[0].Invoke(action);
		}
		else
		{
			action();
		}
	}

	public string GetIndexHtmlPath()
	{
		return Path.Combine(AppDir.GetAppWebDir(), "index.html");
	}

	public string GetVideoForWeb(string nameFromSettings)
	{
		if (string.IsNullOrEmpty(nameFromSettings))
		{
			return string.Empty;
		}
		string videoFileInWebByFileName = LauncherUtils.GetVideoFileInWebByFileName(nameFromSettings);
		if (File.Exists(Path.Combine(AppDir.GetAppWebDir(), videoFileInWebByFileName)))
		{
			return nameFromSettings;
		}
		return string.Empty;
	}
}
