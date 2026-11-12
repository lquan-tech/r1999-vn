using Bluepoch.Launcher.Config;
using Bluepoch.Launcher.Source.Download.Files;
using Bluepoch.Launcher.Source.Download.Zips;
using Bluepoch.Launcher.Source.Game;

namespace Bluepoch.Launcher.Source.Download;

internal class DownloaderFactory
{
	private static DownloaderFactory _instance;

	private ExtractInterface _extractTool;

	private int downloadType = -1;

	public static DownloaderFactory GetInstance()
	{
		if (_instance == null)
		{
			_instance = new DownloaderFactory();
		}
		return _instance;
	}

	private DownloaderFactory()
	{
	}

	public DownloadInterface GetGameDownloader(GameVersionInfo gameVersionInfo)
	{
		if (AppSettings.Instance.GameDownloadType == 1 && !string.IsNullOrEmpty(gameVersionInfo.currentPackageInfo.packageJson))
		{
			return new MultiFilesDownloader();
		}
		return new MultiZipsDownloaderToExtract();
	}

	public ExtractInterface GetGameExtractTool(GameVersionInfo gameVersionInfo)
	{
		if (AppSettings.Instance.GameDownloadType == 1 && !string.IsNullOrEmpty(gameVersionInfo.currentPackageInfo.packageJson))
		{
			return new MultiExtractFiles();
		}
		return MultiExtractZipWith7zShellByDownloader.GetInstance();
	}

	public int GetDownloadType()
	{
		return AppSettings.Instance.GameDownloadType;
	}

	public void SetDownloadType(int type)
	{
		switch (AppSettings.Instance.GameInstallStatus)
		{
		case 1:
		case 3:
		case 6:
		case 7:
			break;
		case 0:
		case 5:
			GameCommon.UpdateGameDownloadTypeInCache(type);
			break;
		case 2:
		case 4:
			break;
		}
	}
}
