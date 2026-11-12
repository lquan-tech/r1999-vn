using System;
using Bluepoch.Launcher.App;

namespace Bluepoch.Launcher.Source.Bean.Request;

[Serializable]
internal class ApiRequestBean
{
	public LauncherDeviceInfo deviceInfo;

	public LauncherAppPackageInfo appPackageInfo;

	public ApiRequestBean()
	{
		deviceInfo = LauncherApplication.GetInstance().GetDeviceInfo();
		appPackageInfo = LauncherApplication.GetInstance().GetAppPackageInfo();
	}
}
