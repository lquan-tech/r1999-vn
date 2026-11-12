using System;

namespace Bluepoch.Launcher.Source.Download;

[Serializable]
internal class DownloadingInfo
{
	public string strSpeed = "";

	public string timeRemain = "";

	public string totleSize = "";

	public double percent;

	public string currentSize = "";

	public string downloadUrl = "";

	public long bytesTotal;

	public long bytesReceived;
}
