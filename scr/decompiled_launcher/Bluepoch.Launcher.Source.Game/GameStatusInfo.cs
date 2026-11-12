using System;
using Bluepoch.Launcher.Source.Download;

namespace Bluepoch.Launcher.Source.Game;

[Serializable]
internal class GameStatusInfo
{
	public int installStatus;

	public DownloadingInfo downloadingInfo;
}
