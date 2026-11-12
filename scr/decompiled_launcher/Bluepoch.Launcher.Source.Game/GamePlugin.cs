using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Bluepoch.Common.Bean;
using Bluepoch.Common.Http;
using Bluepoch.Common.Util;
using Bluepoch.Launcher.App;
using Bluepoch.Launcher.Config;
using Bluepoch.Launcher.Constants;
using Bluepoch.Launcher.Source.Bean.Request;
using Bluepoch.Launcher.Source.Config;
using Bluepoch.Launcher.Source.Download;
using Bluepoch.Launcher.Source.Download.Data;
using Bluepoch.Launcher.Source.Download.Files;
using Bluepoch.Launcher.Source.Utils;
using Bluepoch.Launcher.Utils;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace Bluepoch.Launcher.Source.Game;

internal class GamePlugin
{
	public delegate void ComputerHasDownloadedFilesSize(DownloadingInfo downloadingInfo);

	private static GamePlugin _instance;

	private bool isNeedToUpdate;

	private GameVersionInfo gameVersionInfo;

	public event EventHandler<GameVersionInfo> needToDownloadHandler;

	public event EventHandler<GameVersionInfo> needToUpdateHandler;

	public event EventHandler installedHandler;

	public event EventHandler<string> networkErrorHandler;

	public event EventHandler<string> gameofflineHandler;

	public event EventHandler<DownloadingInfo> downloadingHandler;

	public event EventHandler needToUnzipHandler;

	public event EventHandler updateNewVersionNowHandler;

	public event EventHandler<string> versionIsNewHandler;

	public static GamePlugin GetInstance()
	{
		if (_instance == null)
		{
			_instance = new GamePlugin();
		}
		return _instance;
	}

	private GamePlugin()
	{
	}

	public void CheckGameVersion(bool immediately, bool isComeFromSetting = false)
	{
		CheckGameVersionWithRetry(0, isComeFromSetting);
	}

	private void CheckGameVersionWithRetry(int retryCount, bool isComeFromSetting = false)
	{
		string postData = JsonConvert.SerializeObject(new GameVersionRequestInfo(GameCommon.GetGameVersionInSettingFile()));
		HttpManager httpManager = new HttpManager();
		Dictionary<string, string> dictionary = new Dictionary<string, string> { ["lang"] = LanguageHeaderString.LanguageHeaders[AppSettings.Instance.Language_index] };
		string hotupdate_host = UrlManager.GetInstance().GetHotupdate_host("/diff-update/version");
		HttpManager.AsyRequetCallback callback = async (object userState, string respStr, int statusCode, WebException ex) =>
		{
			BluepochLogger.DebugLog($"CheckGameVersionWithRetry: statusCode = {statusCode}, retryCount = {retryCount}");
			if (ex == null && statusCode == 200)
			{
				try
				{
					ApiResult<GameVersionInfo> apiResult = JsonConvert.DeserializeObject<ApiResult<GameVersionInfo>>(respStr);
					if (apiResult.data == null || apiResult.data.isEmpty())
					{
						BluepochLogger.DebugLog("CheckGameVersionWithRetry--apiResult.data is null");
						NotifySeverError();
					}
					else
					{
						BluepochLogger.InfoLog($"**Game** - latestGameVersion from server: {apiResult.data.latestVersion} , forceUpdate :{apiResult.data.forceUpdate}");
						handleGameVersionInfo(apiResult.data, isComeFromSetting);
					}
					return;
				}
				catch (Exception)
				{
					NotifyNetError();
					return;
				}
			}
			if (statusCode == 4001)
			{
				BluepochLogger.DebugLog("ex : " + ex?.Message);
				NotifySeverError();
			}
			else
			{
				string text = ((ex != null) ? ex.Message : $"Status code error: {statusCode}");
				BluepochLogger.DebugLog("CheckGameVersionWithRetry: Request failed, error: " + text);
				if (retryCount < 3)
				{
					UrlManager.GetInstance().SwitchToAlternateHost();
					await RetryRequest(retryCount, 3, 1000, isComeFromSetting);
				}
				else
				{
					BluepochLogger.ErrorLog("CheckGameVersionWithRetry: Max retries reached, request failed");
					NotifyNetError();
				}
			}
		};
		Dictionary<string, string> headers = dictionary;
		httpManager.PostAsync(hotupdate_host, postData, callback, "application/json", null, null, headers);
	}

	private async Task RetryRequest(int retryCount, int maxRetries, int initialRetryDelay, bool isComeFromSetting = false)
	{
		retryCount++;
		int num = (int)((double)initialRetryDelay * Math.Pow(2.0, retryCount - 1));
		BluepochLogger.InfoLog($"CheckGameVersionWithRetry: About to make retry attempt {retryCount}, delay {num} ms");
		await Task.Delay(num);
		CheckGameVersionWithRetry(retryCount, isComeFromSetting);
	}

	private void NotifyNetError()
	{
		if (AppSettings.Instance.GameInstallStatus == 5)
		{
			gameVersionInfo = new GameVersionInfo();
			gameVersionInfo.latestVersion = AppSettings.Instance.GameExeVersion;
			notifyInstalled();
		}
		else
		{
			networkErrorHandler?.Invoke(this, "Game is offline");
		}
	}

	private void NotifySeverError()
	{
		gameofflineHandler?.Invoke(this, "Game is offline");
	}

	public static bool IsVersionEqual(string iniPath, string lastVersion)
	{
		if (!File.Exists(iniPath))
		{
			return false;
		}
		try
		{
			return File.ReadAllText(iniPath).Trim() == lastVersion;
		}
		catch (Exception ex)
		{
			BluepochLogger.ErrorLog("Read local version file reverse1999_version.ini error: " + ex.Message);
			return false;
		}
	}

	private void handleGameVersionInfo(GameVersionInfo latestGameVersionInfo, bool isComeFromSetting = false)
	{
		string gameExeVersion = AppSettings.Instance.GameExeVersion;
		string gameInstallVersion = GameCommon.GetGameInstallVersion();
		int gameUpdateType = AppSettings.Instance.GameUpdateType;
		int gameInstallStatus = AppSettings.Instance.GameInstallStatus;
		gameVersionInfo = latestGameVersionInfo;
		switch (gameInstallStatus)
		{
		case 0:
			if (IsVersionEqual(GameCommon.GetGameExeInstalledDir() + "/reverse1999_version.ini", gameVersionInfo.latestVersion))
			{
				GameCommon.UpdateGameStatusInCache(5);
				notifyInstalled();
				return;
			}
			isNeedToUpdate = false;
			RequestGameDownloadFileResource("", gameVersionInfo.latestVersion);
			if (isComeFromSetting)
			{
				updateNewVersionNowHandler?.Invoke(this, null);
			}
			return;
		case 5:
			if (gameVersionInfo.forceUpdate && LauncherUtils.VersionContrast(latestGameVersionInfo.latestVersion, gameInstallVersion))
			{
				isNeedToUpdate = true;
				BluepochLogger.DebugLog("**Game** - updateMessage:" + latestGameVersionInfo.updateMessage);
				ClearGameDownloadDir();
				RequestGameDownloadFileResource(gameInstallVersion, gameVersionInfo.latestVersion);
				if (isComeFromSetting)
				{
					updateNewVersionNowHandler?.Invoke(this, null);
				}
			}
			else
			{
				if (isComeFromSetting)
				{
					versionIsNewHandler?.Invoke(this, "");
				}
				notifyInstalled();
			}
			return;
		}
		isNeedToUpdate = LauncherUtils.VersionContrast(latestGameVersionInfo.latestVersion, gameExeVersion);
		if (!isNeedToUpdate)
		{
			if (isComeFromSetting)
			{
				versionIsNewHandler?.Invoke(this, "");
			}
			if (gameUpdateType == 2)
			{
				RequestGameDownloadFileResource(gameInstallVersion, gameVersionInfo.latestVersion);
			}
			else
			{
				RequestGameDownloadFileResource("", gameVersionInfo.latestVersion);
			}
		}
		else
		{
			if (isComeFromSetting)
			{
				updateNewVersionNowHandler?.Invoke(this, null);
			}
			HandleOldGameVersionInfoInDonwloading(gameUpdateType, gameInstallVersion, gameExeVersion);
		}
	}

	private void HandleOldGameVersionInfoInDonwloading(int updateType, string installVersion, string cacheVersion)
	{
		if (gameVersionInfo.forceUpdate)
		{
			isNeedToUpdate = true;
			ClearGameDownloadDir();
			RequestGameDownloadFileResource(gameVersionInfo.latestVersion, gameVersionInfo.latestVersion);
			return;
		}
		isNeedToUpdate = false;
		if (updateType == 2)
		{
			BluepochLogger.InfoLog("**Game** - Request DiffPackage gameVersion: " + installVersion + ",targetVersion:" + cacheVersion);
			RequestGameDownloadFileResource(installVersion, cacheVersion);
		}
		else
		{
			BluepochLogger.InfoLog("**Game** - Request FullPackage gameVersion: " + cacheVersion + ",targetVersion:" + cacheVersion);
			RequestGameDownloadFileResource(cacheVersion, cacheVersion);
		}
	}

	private void ClearGameDownloadDir()
	{
		FileUtil.RemoveFilesBySuffix(FileUtil.GetAppDataDirPath("Downloads"), ".7z");
		FileUtil.RemoveFilesBySuffix(FileUtil.GetAppDataDirPath("Downloads"), ".7z.tmp");
	}

	private void RequestGameDownloadFileResource(string gameVersion, string targetVersion)
	{
		GameVersionRequestInfo gameVersionRequestInfo = new GameVersionRequestInfo(gameVersion);
		if (targetVersion == null || targetVersion.Equals(""))
		{
			gameVersionRequestInfo.targetVersion = gameVersionInfo.latestVersion;
		}
		else
		{
			gameVersionRequestInfo.targetVersion = targetVersion;
		}
		string postData = JsonConvert.SerializeObject(gameVersionRequestInfo);
		HttpManager httpManager = new HttpManager();
		Dictionary<string, string> dictionary = new Dictionary<string, string> { ["lang"] = LanguageHeaderString.LanguageHeaders[AppSettings.Instance.Language_index] };
		string hotupdate_host = UrlManager.GetInstance().GetHotupdate_host("/diff-update/resource");
		HttpManager.AsyRequetCallback callback = (object userState, string respStr, int statusCode, WebException ex) =>
		{
			if (ex == null && statusCode == 200)
			{
				BluepochLogger.DebugLog("GetFullRes--respStr : " + respStr);
				ApiResult<GameFileDownloadInfo> apiResult = JsonConvert.DeserializeObject<ApiResult<GameFileDownloadInfo>>(respStr);
				if (apiResult.data == null || apiResult.data.isEmpty())
				{
					BluepochLogger.InfoLog("GetFullRes--apiResult.data is null");
					if (gameVersionInfo.currentPackageInfo == null)
					{
						NotifySeverError();
					}
				}
				else
				{
					apiResult.data.LoadCurrentPackageInfo();
					gameVersionInfo.latestVersion = apiResult.data.targetVersion;
					gameVersionInfo.currentPackageInfo = apiResult.data.currentPackageInfo;
					gameVersionInfo.updateType = apiResult.data.updateType;
					BluepochLogger.InfoLog($"**Game** - targetVersion:{apiResult.data.targetVersion} , updateType:{apiResult.data.updateType} --  full or diff!");
					HandleGameVersionDownloadResourceInfo();
				}
			}
			else
			{
				if (gameVersionInfo.currentPackageInfo == null)
				{
					NotifyNetError();
				}
				BluepochLogger.DebugLog("ex : " + ex.Message);
			}
		};
		Dictionary<string, string> headers = dictionary;
		httpManager.PostAsync(hotupdate_host, postData, callback, "application/json", null, null, headers);
	}

	private void HandleGameVersionDownloadResourceInfo()
	{
		if (isNeedToUpdate)
		{
			needToUpdateHandler?.Invoke(this, gameVersionInfo);
			return;
		}
		if (gameVersionInfo.currentPackageInfo.downloadType == 1 && !string.IsNullOrEmpty(gameVersionInfo.currentPackageInfo.packageJson))
		{
			DownloaderFactory.GetInstance().SetDownloadType(1);
		}
		else
		{
			DownloaderFactory.GetInstance().SetDownloadType(0);
		}
		switch (AppSettings.Instance.GameInstallStatus)
		{
		case 0:
			notifyToDownload();
			break;
		case 1:
			notifyDownloading();
			break;
		case 3:
		case 6:
		case 7:
			notifyUnZip();
			break;
		case 5:
			notifyInstalled();
			break;
		case 2:
		case 4:
			break;
		}
	}

	private void notifyDownloading()
	{
		ComputerDownloadDirSize((DownloadingInfo downloadingInfo) =>
		{
			downloadingHandler?.Invoke(this, downloadingInfo);
		});
	}

	private void notifyInstalled()
	{
		if (!Directory.Exists(GameCommon.GetGameExeInstalledDir()))
		{
			needToDownloadHandler?.Invoke(this, gameVersionInfo);
			GameCommon.UpdateGameStatusInCache(0);
		}
		else
		{
			installedHandler?.Invoke(this, null);
		}
	}

	private void notifyUnZip()
	{
		needToUnzipHandler?.Invoke(this, null);
	}

	private void notifyToDownload()
	{
		needToDownloadHandler?.Invoke(this, gameVersionInfo);
	}

	public GameVersionInfo GetGameVersionInfo()
	{
		return gameVersionInfo;
	}

	public void ComputerDownloadDirSize(ComputerHasDownloadedFilesSize computerHasDownloadedFilesSize)
	{
		DownloadingInfo downloadingInfo = new DownloadingInfo();
		if (AppSettings.Instance.GameDownloadType == 0)
		{
			HashSet<string> hashSet = GameCommon.LoadExtractedFilesRecord();
			foreach (string packageUrl in gameVersionInfo.currentPackageInfo.packageUrls)
			{
				if (hashSet != null && hashSet.Contains(GameCommon.GetGameDownloadRealFilePath(packageUrl)))
				{
					downloadingInfo.bytesReceived += GameCommon.GetGameZipSizeFromUrl(packageUrl);
				}
				else
				{
					downloadingInfo.bytesReceived += GameCommon.ComputerDownloadZipFileSize(packageUrl);
				}
			}
			if (downloadingInfo.bytesReceived == 0L)
			{
				computerHasDownloadedFilesSize(downloadingInfo);
				return;
			}
			downloadingInfo.totleSize = LauncherUtils.ConvertBytesToReadable(gameVersionInfo.currentPackageInfo.packageSize);
			downloadingInfo.currentSize = LauncherUtils.ConvertBytesToReadable(downloadingInfo.bytesReceived);
			double percent = Math.Round((double)downloadingInfo.bytesReceived * 100.0 / (double)gameVersionInfo.currentPackageInfo.packageSize, 2);
			downloadingInfo.percent = percent;
			computerHasDownloadedFilesSize(downloadingInfo);
			return;
		}
		Task.Run(async () =>
		{
			downloadingInfo.totleSize = LauncherUtils.ConvertBytesToReadable(gameVersionInfo.currentPackageInfo.packageRealSize);
			downloadingInfo.bytesTotal = gameVersionInfo.currentPackageInfo.packageRealSize;
			if (string.IsNullOrEmpty(gameVersionInfo.currentPackageInfo.packageJson))
			{
				LauncherApplication.GetInstance().InvokeOnUiThread(() =>
				{
					computerHasDownloadedFilesSize(downloadingInfo);
				});
				return;
			}
			using DownloadDbContext downloadDbContext = new DownloadDbContext(GameCommon.GetDbFilePath(gameVersionInfo.currentPackageInfo.packageJson));
			downloadDbContext.Database.EnsureCreated();
			List<MultiFilesInfo> source = await downloadDbContext.multiFilesInfo.ToListAsync();
			downloadingInfo.bytesReceived = source.Sum((MultiFilesInfo f) => f.size);
			downloadingInfo.currentSize = LauncherUtils.ConvertBytesToReadable(downloadingInfo.bytesReceived);
			double percent2 = Math.Round((double)downloadingInfo.bytesReceived * 100.0 / (double)gameVersionInfo.currentPackageInfo.packageRealSize, 2);
			downloadingInfo.percent = percent2;
			LauncherApplication.GetInstance().InvokeOnUiThread(() =>
			{
				computerHasDownloadedFilesSize(downloadingInfo);
			});
		});
	}
}
