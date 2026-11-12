using System;

namespace Bluepoch.Launcher.Source.Download.Files;

[Serializable]
internal class MultiFilesInfo
{
	public string dest { get; set; }

	public string md5 { get; set; }

	public long size { get; set; }

	public int id { get; set; }

	public bool forceType { get; set; }

	public override bool Equals(object obj)
	{
		if (obj is MultiFilesInfo multiFilesInfo)
		{
			return id == multiFilesInfo.id;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(id);
	}
}
