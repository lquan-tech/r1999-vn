using System;
using Bluepoch.Common.Util;
using Bluepoch.Launcher.App;
using Bluepoch.Launcher.Config;
using Bluepoch.Launcher.Properties;

namespace Bluepoch.Launcher.Source.Bean.Request;

[Serializable]
internal class GameVersionRequestInfo : ApiRequestBean
{
	public string gameId;

	public int osType;

	public int envType;

	public int channelId;

	public string subChannelId;

	public string currentVersion;

	public string sign;

	public string targetVersion;

	public GameVersionRequestInfo(string currentGameVersion)
	{
		gameId = Resources.GameId;
		osType = 2;
		int num = LauncherApplication.GetInstance().GetRunningType();
		if (num == 3)
		{
			num = 4;
		}
		envType = num;
		currentVersion = currentGameVersion;
		channelId = Convert.ToInt16(SettingConfigManager.GetInstance().GetManifest()["channelId"]);
		subChannelId = SettingConfigManager.GetInstance().GetManifest()["subChannelId"];
		string stringMd = StringUtils.GetStringMd5($"{gameId}_{osType}_{envType}_{channelId}_{subChannelId}_{currentVersion}");
		sign = stringMd.ToLower();
	}
}
