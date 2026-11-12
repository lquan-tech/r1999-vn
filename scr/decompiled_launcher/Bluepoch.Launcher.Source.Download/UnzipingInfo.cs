using System;

namespace Bluepoch.Launcher.Source.Download;

[Serializable]
internal class UnzipingInfo
{
	public double percent;

	public UnzipingInfo(double percent)
	{
		this.percent = percent;
	}
}
