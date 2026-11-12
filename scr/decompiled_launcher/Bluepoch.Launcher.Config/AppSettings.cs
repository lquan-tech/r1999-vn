using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Bluepoch.Common.Util;

namespace Bluepoch.Launcher.Config;

public class AppSettings
{
	private static AppSettings _instance;

	private readonly string _configFilePath;

	private Dictionary<string, object> _settings;

	private readonly JsonSerializerOptions _jsonOptions;

	public static AppSettings Instance => _instance ?? (_instance = new AppSettings());

	public int Language_index
	{
		get
		{
			return GetValue("Language_index", -1);
		}
		set
		{
			SetValue("Language_index", value);
		}
	}

	public int Close_tpye
	{
		get
		{
			return GetValue("Close_tpye", -1);
		}
		set
		{
			SetValue("Close_tpye", value);
		}
	}

	public bool NotificationsEnabled
	{
		get
		{
			return GetValue("NotificationsEnabled", defaultValue: true);
		}
		set
		{
			SetValue("NotificationsEnabled", value);
		}
	}

	public int DownloadSpeed
	{
		get
		{
			return GetValue("DownloadSpeed", 0);
		}
		set
		{
			SetValue("DownloadSpeed", value);
		}
	}

	public bool DownloadSpeedLimit
	{
		get
		{
			return GetValue("DownloadSpeedLimit", defaultValue: false);
		}
		set
		{
			SetValue("DownloadSpeedLimit", value);
		}
	}

	public string GameExePath
	{
		get
		{
			return GetValue("GameExePath", "");
		}
		set
		{
			SetValue("GameExePath", value);
		}
	}

	public int GameInstallStatus
	{
		get
		{
			return GetValue("GameInstallStatus", 0);
		}
		set
		{
			SetValue("GameInstallStatus", value);
		}
	}

	public string GameExeVersion
	{
		get
		{
			return GetValue("GameExeVersion", "");
		}
		set
		{
			SetValue("GameExeVersion", value);
		}
	}

	public int GameUpdateType
	{
		get
		{
			return GetValue("GameUpdateType", 1);
		}
		set
		{
			SetValue("GameUpdateType", value);
		}
	}

	public int GameDownloadType
	{
		get
		{
			return GetValue("GameDownloadType", 0);
		}
		set
		{
			SetValue("GameDownloadType", value);
		}
	}

	public string AppDataDir
	{
		get
		{
			return GetValue("AppDataDir", "AppData");
		}
		set
		{
			SetValue("AppDataDir", value);
		}
	}

	public long GameZipsTotalSize
	{
		get
		{
			return GetValue("GameZipsTotalSize", 0L);
		}
		set
		{
			SetValue("GameZipsTotalSize", value);
		}
	}

	public int Notifications_Id
	{
		get
		{
			return GetValue("Notifications_Id", 0);
		}
		set
		{
			SetValue("Notifications_Id", value);
		}
	}

	public string BgVideoName
	{
		get
		{
			return GetValue("BgVideoName", "");
		}
		set
		{
			SetValue("BgVideoName", value);
		}
	}

	public int NotityTimerInterval
	{
		get
		{
			return GetValue("NotityTimerInterval", 30000);
		}
		set
		{
			SetValue("NotityTimerInterval", value);
		}
	}

	public bool Use_host_bak
	{
		get
		{
			return GetValue("Use_host_bak", defaultValue: false);
		}
		set
		{
			SetValue("Use_host_bak", value);
		}
	}

	private AppSettings()
	{
		string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), SettingConfigManager.GetInstance().GetManifest()["userDataDir"]);
		_configFilePath = Path.Combine(path, "appsettings.json");
		_jsonOptions = new JsonSerializerOptions
		{
			WriteIndented = true,
			PropertyNameCaseInsensitive = true
		};
		string directoryName = Path.GetDirectoryName(_configFilePath);
		if (!Directory.Exists(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
		LoadSettings();
	}

	private void LoadSettings()
	{
		try
		{
			if (File.Exists(_configFilePath))
			{
				string json = File.ReadAllText(_configFilePath);
				_settings = JsonSerializer.Deserialize<Dictionary<string, object>>(json, _jsonOptions) ?? new Dictionary<string, object>();
			}
			else
			{
				_settings = new Dictionary<string, object>();
				InitializeDefaults();
			}
		}
		catch
		{
			_settings = new Dictionary<string, object>();
			InitializeDefaults();
		}
	}

	private void InitializeDefaults()
	{
		SetValue("Language_index", -1);
		SetValue("Close_tpye", -1);
		SetValue("NotificationsEnabled", value: true);
		SetValue("DownloadSpeed", 0);
		SetValue("DownloadSpeedLimit", value: false);
		SetValue("GameExePath", "");
		SetValue("GameInstallStatus", 0);
		SetValue("GameExeVersion", "");
		SetValue("GameUpdateType", 1);
		SetValue("AppDataDir", "AppData");
		SetValue("GameZipsTotalSize", 0L);
		SetValue("Notifications_Id", 0);
		SetValue("BgVideoName", "");
		SetValue("NotityTimerInterval", 30000);
		SetValue("GameDownloadType", 1);
		SetValue("Use_host_bak", value: false);
	}

	public T GetValue<T>(string key, T defaultValue = default(T))
	{
		if (_settings.TryGetValue(key, out var value))
		{
			try
			{
				if (value is JsonElement element)
				{
					return element.Deserialize<T>(_jsonOptions);
				}
				return (T)Convert.ChangeType(value, typeof(T));
			}
			catch
			{
				return defaultValue;
			}
		}
		return defaultValue;
	}

	public void SetValue<T>(string key, T value)
	{
		_settings[key] = value;
		Save();
	}

	public void Save()
	{
		try
		{
			string contents = JsonSerializer.Serialize(_settings, _jsonOptions);
			File.WriteAllText(_configFilePath, contents);
		}
		catch (Exception ex)
		{
			BluepochLogger.ErrorLog("Save Setting Data Error: " + ex.Message);
		}
	}
}
