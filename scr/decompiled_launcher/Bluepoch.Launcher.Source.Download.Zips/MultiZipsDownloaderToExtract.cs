using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bluepoch.Common.Download;
using Bluepoch.Common.Util;
using Bluepoch.Launcher.Config;
using Bluepoch.Launcher.Constants;
using Bluepoch.Launcher.Source.Game;
using Bluepoch.Launcher.Source.Utils;
using ThinkingSDK.PC;

namespace Bluepoch.Launcher.Source.Download.Zips;

internal class MultiZipsDownloaderToExtract : DownloadInterface
{
	private List<string> needToDownloadUrls;

	private Dictionary<string, int> downloadErrorFiles = new Dictionary<string, int>();

	private bool _networkAllFailed;

	private DateTime _lastSpeedCheckTime = DateTime.Now;

	private long _lastDownloadedBytes;

	public MultiZipsDownloaderToExtract()
	{
		maxConcurrency = 1;
	}

	public override void StartDownload(GameVersionInfo gameVersionInfo)
	{
		if (Is_Downloading)
		{
			BluepochLogger.DebugLog("Already downloading, cannot start again");
			NotifyDownloadError(string.Empty, 2002);
			return;
		}
		Is_Downloading = true;
		string gameExeInstallDirByDefault = GameCommon.GetGameExeInstallDirByDefault();
		if (!Directory.Exists(gameExeInstallDirByDefault))
		{
			Directory.CreateDirectory(gameExeInstallDirByDefault);
		}
		m_gameVersionInfo = gameVersionInfo;
		Reset();
		PreStart();
		GameCommon.UpdateGameVersionInCache(gameVersionInfo.latestVersion);
		GameCommon.UpdateGameStatusInCache(1);
		GameCommon.UpdateGameSizeInCache(gameVersionInfo.currentPackageInfo.packageSize);
		GameCommon.UpdateGameUpdateTypeInCache((int)gameVersionInfo.updateType);
		HashSet<string> hashSet = GameCommon.LoadExtractedFilesRecord();
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		foreach (string packageUrl in gameVersionInfo.currentPackageInfo.packageUrls)
		{
			if (hashSet != null && hashSet.Contains(GameCommon.GetGameDownloadRealFilePath(packageUrl)))
			{
				bytesZip += GameCommon.GetGameZipSizeFromUrl(packageUrl);
				continue;
			}
			FileInfo fileInfo = new FileInfo(GameCommon.GetGameDownloadRealFilePath(packageUrl));
			if (fileInfo.Exists && fileInfo.Length > 0)
			{
				bytesZip += fileInfo.Length;
				BluepochLogger.InfoLog("File is exists, skip download " + packageUrl);
				MultiExtractZipWith7zShellByDownloader.GetInstance().AddExtractUrl(packageUrl);
				continue;
			}
			FileInfo fileInfo2 = new FileInfo(GameCommon.GetGameDownloadTempFilePath(packageUrl));
			if (fileInfo2.Exists && fileInfo2.Length > 0)
			{
				bytesZip += fileInfo2.Length;
				list.Add(packageUrl);
			}
			else
			{
				list2.Add(packageUrl);
			}
		}
		needToDownloadUrls.AddRange(list);
		needToDownloadUrls.AddRange(list2);
		if (needToDownloadUrls.Count == 0)
		{
			NotifyDownloadFinish();
			Is_Downloading = false;
			return;
		}
		downloadTotalCount = needToDownloadUrls.Count;
		bytesLastSecond = bytesZip;
		bytesReceived = bytesZip;
		bytesTotal = m_gameVersionInfo.currentPackageInfo.packageSize;
		new SemaphoreSlim(maxConcurrency);
		new List<Task>();
		_downloadCts = new CancellationTokenSource();
		Task.Run(async () =>
		{
			await ProcessDownloadQueue(needToDownloadUrls, maxConcurrency);
		}, _downloadCts.Token);
	}

	private async Task ProcessDownloadQueue(List<string> needToDownloadUrls, int maxConcurrency)
	{
		Queue<string> downloadQueue = new Queue<string>(needToDownloadUrls);
		List<Task> activeTasks = new List<Task>();
		int completedCount = 0;
		int totalCount = needToDownloadUrls.Count;
		BluepochLogger.DebugLog($"Start processing download queue, total files: {totalCount}, max concurrency: {maxConcurrency}");
		while (downloadQueue.Count > 0 || activeTasks.Count > 0)
		{
			if (_downloadCts.Token.IsCancellationRequested || !Is_Downloading)
			{
				BluepochLogger.DebugLog("Download paused, stop processing queue");
				break;
			}
			if (_networkAllFailed)
			{
				BluepochLogger.WarningLog("Primary and fallback domains both failed, stop download queue, cancel all in-progress tasks");
				break;
			}
			while (activeTasks.Count < maxConcurrency && downloadQueue.Count > 0)
			{
				if (_downloadCts.Token.IsCancellationRequested || !Is_Downloading)
				{
					BluepochLogger.DebugLog($"Download paused, stop starting new tasks, active tasks: {activeTasks.Count}");
					break;
				}
				string downloadUrl = downloadQueue.Dequeue();
				Task item = CreateDownloadTask(downloadUrl);
				activeTasks.Add(item);
				await Task.Delay(10);
			}
			if (activeTasks.Count > 0)
			{
				Task task = await Task.WhenAny(activeTasks);
				activeTasks.Remove(task);
				completedCount++;
				try
				{
					await task;
				}
				catch (Exception ex)
				{
					BluepochLogger.ErrorLog("Download task exception: " + ex.Message);
				}
				BluepochLogger.DebugLog($"Queue progress: {completedCount}/{totalCount}, remaining in queue: {downloadQueue.Count}, active tasks: {activeTasks.Count}");
			}
		}
		BluepochLogger.InfoLog($"All Download task stop or finish, completedCount: {completedCount}, downloadQueue-Count: {downloadQueue.Count}");
		if (_downloadCts.Token.IsCancellationRequested)
		{
			BluepochLogger.InfoLog("Download paused");
		}
	}

	private async Task CreateDownloadTask(string downloadUrl)
	{
		InvokeOnUiThread(() =>
		{
			DownloadStartCallback(LauncherUtils.GetFileNameFromUrl(downloadUrl));
		});
		long speedLimitBytesPerSecond = 0L;
		if (AppSettings.Instance.DownloadSpeedLimit)
		{
			int num = maxConcurrency - 2;
			speedLimitBytesPerSecond = ((num > 0) ? (AppSettings.Instance.DownloadSpeed * 1024 / num) : 0);
		}
		FileDownloader fileDownloader = new FileDownloader(downloadUrl, speedLimitBytesPerSecond);
		if (SettingConfigManager.GetInstance().GetManifest().TryGetValue("download_host_bak", out var value))
		{
			fileDownloader.FallbackDomain = value;
		}
		string gameDownloadTempFilePath = GameCommon.GetGameDownloadTempFilePath(downloadUrl);
		await fileDownloader.DownloadFileAsync(gameDownloadTempFilePath, null, (string id) =>
		{
			InvokeOnUiThread(() =>
			{
				DownloadStopCallback(id);
			});
		}, null, (string id, int p, long downloadedBytes) =>
		{
			InvokeOnUiThread(() =>
			{
				DownloadProgressChanged(id, p, downloadedBytes);
			});
		}, (string id, string path) =>
		{
			InvokeOnUiThread(() =>
			{
				DownloadCompleteCallback(id, path, downloadUrl);
			});
		}, (string id, string actualUrl, Exception ex) =>
		{
			InvokeOnUiThread(() =>
			{
				DownloadFailedCallback(id, ex, downloadUrl, actualUrl);
			});
		}, _downloadCts.Token, IsElapsedSecond: false, (string taskId, int errorCode, Exception ex, bool isRetry) =>
		{
			ErrorTrackCallback(taskId, errorCode, ex, isRetry, downloadUrl);
		});
	}

	private void ErrorTrackCallback(string downloadTaskId, int errorCode, Exception ex, bool isRetry, string downloadUrl)
	{
		Dictionary<string, object> properties = new Dictionary<string, object>
		{
			[TrackEvent.EVENT_Result_Msg] = $"{ex.Message} - {ex.GetType()} - ErrorCode:{errorCode} - Retry:{isRetry}",
			[TrackEvent.Exception_Location] = "FileDownloader-" + downloadTaskId,
			[TrackEvent.Exception_Url] = downloadUrl
		};
		ThinkingPCSDKManager.getInstance().TrackEvent(TrackEvent.EVENT_Exception, properties);
	}

	private void DownloadFailedCallback(string downloadTaskId, Exception ex, string downloadUrl, string actualUrl = null)
	{
		int num = (ex as FileDownloader.DownloadException)?.ExceptionCode ?? 1999;
		BluepochLogger.InfoLog($"DownloadFailedCallback downloadTaskId: {downloadTaskId}, ErrorCode:{num}, actualUrl: {actualUrl}");
		switch (num)
		{
		case 1004:
			_networkAllFailed = true;
			BluepochLogger.ErrorLog($"[DiskFull] Disk is full! downloadTaskId: {downloadTaskId}, ErrorCode: {num}, stop the whole download queue");
			_downloadCts.Cancel();
			NotifyDownloadError(downloadTaskId, num);
			return;
		case 1009:
			_networkAllFailed = true;
			BluepochLogger.ErrorLog($"[DeviceError] Disk device error! downloadTaskId: {downloadTaskId}, ErrorCode: {num}, stop the whole download queue");
			_downloadCts.Cancel();
			NotifyDownloadError(downloadTaskId, num);
			return;
		}
		if (!string.IsNullOrEmpty(actualUrl) && actualUrl != downloadUrl && FileDownloader.ShouldSwitchToFallback(num))
		{
			_networkAllFailed = true;
			BluepochLogger.ErrorLog($"[NetworkAllFailed] Primary and fallback domains both failed! downloadTaskId: {downloadTaskId}, ErrorCode: {num}, stop the whole download queue");
			_downloadCts.Cancel();
			NotifyDownloadError(downloadTaskId, num);
		}
		else
		{
			DownloadCompleteCallback(downloadTaskId, "", downloadUrl, num, ex.Message);
		}
	}

	private void DownloadCompleteCallback(string downloadTaskId, string savePath, string downloadUrl, int errorCode = 200, string errorMsg = "")
	{
		downloadCompleteCount++;
		if (errorCode == 200)
		{
			if (!RenameSavePathFileToRealFile(savePath, downloadUrl))
			{
				DownloadCompleteCallback(downloadTaskId, savePath, downloadUrl, 2001, "Rename SavePathFile ToRealFile Fail!");
				return;
			}
			needToDownloadUrls?.Remove(downloadUrl);
			BluepochLogger.InfoLog("DownloadComplete downloadTaskId: " + downloadTaskId);
			if (downloadCompleteCount == downloadTotalCount)
			{
				if (downloadErrorFiles.Count > 0)
				{
					int errorCode2 = downloadErrorFiles.Values.FirstOrDefault();
					foreach (KeyValuePair<string, int> downloadErrorFile in downloadErrorFiles)
					{
						BluepochLogger.DebugLog($"this is file {downloadErrorFile.Key} downlaod error! ErrorCode:{downloadErrorFile.Value}");
					}
					NotifyDownloadError(string.Empty, errorCode2);
				}
				else
				{
					BluepochLogger.InfoLog($"All files downloaded, total: {downloadCompleteCount}");
					Is_Downloading = false;
					Is_ByteWriteToFile = false;
					needToDownloadUrls?.Clear();
					NotifyDownloadFinish();
				}
			}
			else
			{
				MultiExtractZipWith7zShellByDownloader.GetInstance().AddExtractUrl(downloadUrl);
			}
			return;
		}
		downloadErrorFiles[downloadUrl] = errorCode;
		if (downloadCompleteCount == downloadTotalCount)
		{
			foreach (KeyValuePair<string, int> downloadErrorFile2 in downloadErrorFiles)
			{
				BluepochLogger.InfoLog($"this is file {downloadErrorFile2.Key} downlaod error! ErrorCode:{downloadErrorFile2.Value}");
			}
			NotifyDownloadError(string.Empty, errorCode);
		}
		else
		{
			BluepochLogger.InfoLog($"DownloadComplete downloadTaskId: {downloadTaskId}, ErrorCode:{errorCode}, ErrorMsg:{errorMsg} , But there are other downloadTaskId is downloading");
		}
	}

	private void DownloadStopCallback(string downloadTaskId)
	{
		BluepochLogger.DebugLog("Download task stopped, downloadTaskId - " + downloadTaskId);
		if (Is_Downloading)
		{
			NotifyDownloadStop();
		}
	}

	private void DownloadStartCallback(string downloadTaskId)
	{
		if (!Is_ByteWriteToFile)
		{
			BluepochLogger.DebugLog("Download task start, downloadTaskId:  " + downloadTaskId);
			_lastSpeedCheckTime = DateTime.Now;
			bytesTotal = m_gameVersionInfo.currentPackageInfo.packageSize;
			downloadStartCallback?.Invoke(bytesReceived, Math.Round((double)bytesReceived * 100.0 / (double)bytesTotal, 2), 0.0, bytesTotal);
			Is_ByteWriteToFile = true;
		}
	}

	private Task DownloadProgressChanged(string downloadTaskId, int progress, long downloadedBytes)
	{
		bytesReceived += downloadedBytes;
		DateTime now = DateTime.Now;
		TimeSpan timeSpan = now - _lastSpeedCheckTime;
		if (timeSpan.TotalSeconds >= 0.5 || downloadedBytes >= bytesTotal)
		{
			double num = (double)(bytesReceived - _lastDownloadedBytes) / timeSpan.TotalSeconds;
			BluepochLogger.DebugLogAsyn($"Update once every {timeSpan.TotalSeconds}s, speed: {num}");
			if (progressPerSecondCallback != null && timeSpan.TotalSeconds > 0.0 && bytesTotal > 0)
			{
				double progress2 = Math.Round((double)bytesReceived * 100.0 / (double)bytesTotal, 2);
				progressPerSecondCallback(bytesReceived, progress2, num, bytesTotal);
			}
			_lastSpeedCheckTime = now;
			_lastDownloadedBytes = bytesReceived;
		}
		return Task.CompletedTask;
	}

	private bool RenameSavePathFileToRealFile(string savePath, string downloadUrl)
	{
		try
		{
			string gameDownloadRealFilePath = GameCommon.GetGameDownloadRealFilePath(downloadUrl);
			File.Move(savePath, gameDownloadRealFilePath);
			return true;
		}
		catch (Exception ex)
		{
			BluepochLogger.ErrorLog("RenameSavePathFileToRealFile  Temp File Exception: " + ex.Message);
			return false;
		}
	}

	private void NotifyDownloadFinish()
	{
		downloadFinishCallback?.Invoke();
	}

	private static string GetErrorMessage(int errorCode)
	{
		return errorCode switch
		{
			1001 => "ExceptionMessage.NetworkError", 
			1002 => "ExceptionMessage.Http2ProtocolError", 
			1003 => "ExceptionMessage.DnsError", 
			1004 => "ExceptionMessage.DiskFull", 
			1005 => "ExceptionMessage.AccessDenied", 
			1006 => "ExceptionMessage.SslError", 
			1007 => "ExceptionMessage.Timeout", 
			1008 => "ExceptionMessage.Unknown", 
			1009 => "ExceptionMessage.DeviceError", 
			2001 => "ExceptionMessage.ReNameFileError", 
			2002 => "ExceptionMessage.ErrorStartDownload", 
			206 => "ExceptionMessage.", 
			_ => "ExceptionMessage.Unknown", 
		};
	}

	private void NotifyDownloadError(string taskId, int errorCode)
	{
		string errorMessage = GetErrorMessage(errorCode);
		BluepochLogger.InfoLog($"Download error, ErrorCode:{errorCode}, error message: {errorMessage}, failed file count: {downloadErrorFiles.Count}");
		Is_Downloading = false;
		Is_ByteWriteToFile = false;
		downloadErrorCallback?.Invoke(taskId, errorMessage);
	}

	private void NotifyDownloadStop()
	{
		Is_Downloading = false;
		Is_ByteWriteToFile = false;
		downloadPauseCallback?.Invoke();
	}

	public override void Reset()
	{
		if (needToDownloadUrls == null)
		{
			needToDownloadUrls = new List<string>();
		}
		if (downloadErrorFiles == null)
		{
			downloadErrorFiles = new Dictionary<string, int>();
		}
		downloadCompleteCount = 0;
		bytesZip = 0L;
		bytesReceived = 0L;
		_networkAllFailed = false;
	}

	public override void PreStart()
	{
		needToDownloadUrls.Clear();
		downloadErrorFiles.Clear();
	}

	public override void StopDownload()
	{
		_downloadCts.Cancel();
	}

	public override void CloseDownload()
	{
	}
}
