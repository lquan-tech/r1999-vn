using System;
using System.Linq;
using Bluepoch.Launcher.Constants;

namespace Bluepoch.Launcher.Source.Game;

[Serializable]
internal class GameFileDownloadInfo
{
	public string localgameVersion;

	public string latestGameVersion;

	public string targetVersion;

	public bool forceUpdate;

	public GameUpdatePackageInfo fullPackage;

	public GameUpdatePackageInfo diffPackage;

	public GameUpdatePackageInfo currentPackageInfo;

	public GameUpdateType updateType;

	public void LoadCurrentPackageInfo()
	{
		if (diffPackage != null && Enumerable.Count(diffPackage.packageUrls) > 0)
		{
			currentPackageInfo = diffPackage;
			updateType = GameUpdateType.DiffPackage;
		}
		else
		{
			currentPackageInfo = fullPackage;
			updateType = GameUpdateType.FullPackage;
		}
	}

	public bool isEmpty()
	{
		if (fullPackage == null)
		{
			return diffPackage == null;
		}
		return false;
	}
}
