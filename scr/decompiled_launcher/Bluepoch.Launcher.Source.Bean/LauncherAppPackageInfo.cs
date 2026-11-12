using System;
using System.Windows.Forms;
using Bluepoch.Common.Bean;
using Bluepoch.Launcher.Config;
using Bluepoch.Launcher.Properties;
using Bluepoch.Launcher.Source.Utils;

namespace Bluepoch.Launcher.Source.Bean;

internal class LauncherAppPackageInfo : BaseAppPackageInfo
{
	public LauncherAppPackageInfo()
	{
		channelId = SettingConfigManager.GetInstance().GetManifest()["channelId"];
		sdkVersion = Application.ProductVersion;
		channelVersion = "1.0.0";
		appPackageName = SettingConfigManager.GetInstance().GetManifest()["appPackageName"];
		appVersion = Convert.ToInt32(Resources.AppBuildCode);
		appVersionName = LauncherUtils.GetExeVersion();
		gameId = Convert.ToInt32(Resources.GameId);
		gameCode = SettingConfigManager.GetInstance().GetManifest()["gameCode"];
		gameName = SettingConfigManager.GetInstance().GetManifest()["gameName"];
		subChannelId = SettingConfigManager.GetInstance().GetManifest()["subChannelId"];
	}
}
