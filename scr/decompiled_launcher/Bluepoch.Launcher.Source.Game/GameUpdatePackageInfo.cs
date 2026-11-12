using System;
using System.Collections.Generic;

namespace Bluepoch.Launcher.Source.Game;

[Serializable]
internal class GameUpdatePackageInfo
{
	public int downloadType;

	public List<string> packageUrls;

	public long packageRealSize;

	public long packageSize;

	public string packageJson;

	public string packageMd5;

	public bool isEmpty()
	{
		if (packageUrls != null && packageUrls.Count != 0)
		{
			return packageSize == 0;
		}
		return true;
	}
}
