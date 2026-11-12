using System;
using System.Collections.Generic;

namespace Bluepoch.Launcher.Source.Download.Files;

[Serializable]
internal class MultiFilesDownloadDirInfo
{
	public string packedhost;

	public long fileTotalSize;

	public string gameVersion;

	public List<MultiFilesInfo> smallFilesInfo;
}
