using System;

namespace Bluepoch.Launcher.Source.Bean.Request;

[Serializable]
internal class AppVersionRequestInfo : ApiRequestBean
{
	public string currentVersion;

	public AppVersionRequestInfo(string currentVersion)
	{
		this.currentVersion = currentVersion;
	}
}
