using System;

namespace Bluepoch.Launcher.Source.Bean;

[Serializable]
internal class DeleteGameResourceInfo
{
	public string deleteItemName = "";

	public double percent;

	public int code = 200;

	public string message = "";

	public DeleteGameResourceInfo()
	{
	}

	public DeleteGameResourceInfo(string deleteItemName, double processed, int code = 200, string message = "")
	{
		this.deleteItemName = deleteItemName;
		percent = processed;
		this.code = code;
		this.message = message;
	}
}
