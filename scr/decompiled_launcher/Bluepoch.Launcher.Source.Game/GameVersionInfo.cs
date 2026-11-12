using System;
using Bluepoch.Common.Bean.Request;
using Bluepoch.Launcher.Constants;

namespace Bluepoch.Launcher.Source.Game;

[Serializable]
internal class GameVersionInfo : ApiRequestBean
{
	public string latestVersion;

	public bool forceUpdate;

	public GameUpdatePackageInfo currentPackageInfo;

	public string updateMessage = "";

	public long rootDirRoomSize;

	public string gameExeDownloadDir;

	public int currentDownloadType;

	public GameUpdateType updateType;

	public bool isEmpty()
	{
		if (latestVersion != null)
		{
			return latestVersion.Equals("");
		}
		return true;
	}
}
