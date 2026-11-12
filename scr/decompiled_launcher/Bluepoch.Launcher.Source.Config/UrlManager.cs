using System.Text;
using Bluepoch.Launcher.App;
using Bluepoch.Launcher.Config;

namespace Bluepoch.Launcher.Source.Config;

internal class UrlManager
{
	private static UrlManager _instance;

	private string actvity_host;

	private string actvity_host_bak;

	private string gamesdk_host;

	private string gamesdk_host_bak;

	private string hotupdate_host;

	private string hotupdate_host_bak;

	private bool use_bak;

	public static UrlManager GetInstance()
	{
		if (_instance == null)
		{
			_instance = new UrlManager();
		}
		return _instance;
	}

	private UrlManager()
	{
		use_bak = AppSettings.Instance.Use_host_bak;
		GetRunningHost();
	}

	private void GetRunningHost()
	{
		switch (LauncherApplication.GetInstance().GetRunningType())
		{
		case 1:
			actvity_host = SettingConfigManager.GetInstance().GetManifest()["Dev_activity_host"];
			gamesdk_host = SettingConfigManager.GetInstance().GetManifest()["Dev_host"];
			hotupdate_host = SettingConfigManager.GetInstance().GetManifest()["Dev_hotupdate_host"];
			break;
		case 2:
			actvity_host = SettingConfigManager.GetInstance().GetManifest()["Test_activity_host"];
			gamesdk_host = SettingConfigManager.GetInstance().GetManifest()["Test_host"];
			hotupdate_host = SettingConfigManager.GetInstance().GetManifest()["Test_hotupdate_host"];
			break;
		case 3:
			actvity_host = SettingConfigManager.GetInstance().GetManifest()["Release_activity_host"];
			gamesdk_host = SettingConfigManager.GetInstance().GetManifest()["Release_host"];
			hotupdate_host = SettingConfigManager.GetInstance().GetManifest()["Release_hotupdate_host"];
			actvity_host_bak = SettingConfigManager.GetInstance().GetManifest()["Release_activity_host"];
			gamesdk_host_bak = SettingConfigManager.GetInstance().GetManifest()["Release_host_bak"];
			hotupdate_host_bak = SettingConfigManager.GetInstance().GetManifest()["Release_hotupdate_host_bak"];
			break;
		}
	}

	public string GetActvity_host(string path)
	{
		if (use_bak)
		{
			return new StringBuilder(actvity_host_bak).Append(path).ToString();
		}
		return new StringBuilder(actvity_host).Append(path).ToString();
	}

	public string GetGamesdk_host(string path)
	{
		if (use_bak)
		{
			return new StringBuilder(gamesdk_host_bak).Append(path).ToString();
		}
		return new StringBuilder(gamesdk_host).Append(path).ToString();
	}

	public string GetHotupdate_host(string path)
	{
		if (use_bak)
		{
			return new StringBuilder(hotupdate_host_bak).Append(path).ToString();
		}
		return new StringBuilder(hotupdate_host).Append(path).ToString();
	}

	public void SwitchToAlternateHost()
	{
		use_bak = !use_bak;
		AppSettings.Instance.Use_host_bak = use_bak;
		AppSettings.Instance.Save();
	}
}
