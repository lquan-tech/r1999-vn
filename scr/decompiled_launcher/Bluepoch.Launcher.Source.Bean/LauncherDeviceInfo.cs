using System;
using Bluepoch.Common.Bean;
using Bluepoch.Common.Util;
using Bluepoch.Launcher.Config;

namespace Bluepoch.Launcher.Source.Bean;

[Serializable]
internal class LauncherDeviceInfo : BaseDeviceInfo
{
	public LauncherDeviceInfo()
	{
		string regeditmgrGroupName = SettingConfigManager.GetInstance().GetManifest()["projectgroupname"];
		distinctId = DeviceIdHelper.getDeviceId(regeditmgrGroupName);
		anonymousId = distinctId;
		deviceId = distinctId;
	}
}
