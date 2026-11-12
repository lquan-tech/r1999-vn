using System;
using System.Collections.Generic;
using System.IO;
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

internal class MultiZipsDownloader : DownloadInterface
{
	private List<string> needToDownloadUrls;

	private List<string> downloadErrorFiles = new List<string>();

	private DateTime _lastSpeedCheckTime = DateTime.Now;

	private long _lastDownloadedBytes;

	public override void StartDownload(GameVersionInfo gameVersionInfo)
	{
		if (Is_Downloading)
		{
			BluepochLogger.DebugLog("已经在下载中，无法重复启动");
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
		foreach (string packageUrl in gameVersionInfo.currentPackageInfo.packageUrls)
		{
			FileInfo fileInfo = new FileInfo(GameCommon.GetGameDownloadRealFilePath(packageUrl));
			if (fileInfo.Exists && fileInfo.Length > 0)
			{
				bytesZip += fileInfo.Length;
				BluepochLogger.DebugLog("文件已经存在，跳过下载 " + packageUrl);
				continue;
			}
			FileInfo fileInfo2 = new FileInfo(GameCommon.GetGameDownloadTempFilePath(packageUrl));
			if (fileInfo2.Exists && fileInfo2.Length > 0)
			{
				bytesZip += fileInfo2.Length;
			}
			needToDownloadUrls.Add(packageUrl);
		}
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
		BluepochLogger.DebugLog($"开始处理下载队列，总文件数：{totalCount}，最大并发数：{maxConcurrency}");
		while (downloadQueue.Count > 0 || activeTasks.Count > 0)
		{
			if (_downloadCts.Token.IsCancellationRequested || !Is_Downloading)
			{
				BluepochLogger.DebugLog("下载已暂停，停止处理队列");
				break;
			}
			while (activeTasks.Count < maxConcurrency && downloadQueue.Count > 0)
			{
				if (_downloadCts.Token.IsCancellationRequested || !Is_Downloading)
				{
					BluepochLogger.DebugLog($"下载已暂停，停止启动新任务,活动任务：{activeTasks.Count}");
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
				await task;
				BluepochLogger.DebugLog($"队列进度：{completedCount}/{totalCount}，队列剩余：{downloadQueue.Count}，活动任务：{activeTasks.Count}");
			}
		}
		BluepochLogger.DebugLog($"下载任务结束，更新状态，已完成数(暂停也是结束)：{completedCount}，队列剩余：{downloadQueue.Count}");
		if (_downloadCts.Token.IsCancellationRequested)
		{
			BluepochLogger.DebugLog("下载已暂停");
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
			speedLimitBytesPerSecond = AppSettings.Instance.DownloadSpeed * 1024 / (maxConcurrency - 2);
		}
		FileDownloader fileDownloader = new FileDownloader(downloadUrl, speedLimitBytesPerSecond);
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
		}, (string id, string url, Exception ex) =>
		{
			InvokeOnUiThread(() =>
			{
				DownloadFailedCallback(id, ex, url);
			});
		}, _downloadCts.Token);
	}

	private void DownloadFailedCallback(string downloadTaskId, Exception ex, string downloadUrl)
	{
		BluepochLogger.InfoLog("文件下载异常 downloadTaskId：" + downloadTaskId);
		Dictionary<string, object> properties = new Dictionary<string, object>
		{
			[TrackEvent.EVENT_Result_Msg] = $"{ex.Message} - {ex.GetType()}  - {ex.HResult}",
			[TrackEvent.Exception_Location] = "DownloadFailedCallback-" + downloadTaskId
		};
		ThinkingPCSDKManager.getInstance().TrackEvent(TrackEvent.EVENT_Exception, properties);
		DownloadCompleteCallback(downloadTaskId, "", downloadUrl, 206, ex.Message);
	}

	private void DownloadCompleteCallback(string downloadTaskId, string savePath, string downloadUrl, int errorCode = 200, string errorMsg = "")
	{
		downloadCompleteCount++;
		if (errorCode == 200)
		{
			if (!RenameSavePathFileToRealFile(savePath, downloadUrl))
			{
				DownloadCompleteCallback(downloadTaskId, savePath, string.Empty, 206, "Rename SavePathFile ToRealFile Fail!");
				return;
			}
			needToDownloadUrls?.Remove(downloadUrl);
			BluepochLogger.InfoLog("文件下载完成 downloadTaskId：" + downloadTaskId);
			if (downloadCompleteCount != downloadTotalCount)
			{
				return;
			}
			if (downloadErrorFiles.Count > 0)
			{
				foreach (string downloadErrorFile in downloadErrorFiles)
				{
					_ = downloadErrorFile;
					BluepochLogger.InfoLog("this is file" + downloadTaskId + " downlaod error!");
				}
				NotifyDownloadError(string.Empty, "MultiFilesDownloader.Error_DownloadFiles");
			}
			else
			{
				BluepochLogger.InfoLog($"All files downloaded ，total：{downloadCompleteCount}");
				Is_Downloading = false;
				Is_ByteWriteToFile = false;
				needToDownloadUrls?.Clear();
				NotifyDownloadFinish();
			}
			return;
		}
		downloadErrorFiles.Add(downloadUrl);
		if (downloadCompleteCount != downloadTotalCount)
		{
			return;
		}
		foreach (string downloadErrorFile2 in downloadErrorFiles)
		{
			_ = downloadErrorFile2;
			BluepochLogger.InfoLog("this is file" + downloadTaskId + " downlaod error!");
		}
		NotifyDownloadError(string.Empty, "MultiFilesDownloader.Error_DownloadFiles");
	}

	private void DownloadStopCallback(string downloadTaskId)
	{
		BluepochLogger.DebugLog("下载任务停止 downloadTaskId -  " + downloadTaskId);
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
			BluepochLogger.DebugLogAsyn($"每{timeSpan.TotalSeconds}进行一次更新，速度：{num}");
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
		downloadFinishCallback();
	}

	private void NotifyDownloadError(string taskId, string msg)
	{
		Is_Downloading = false;
		Is_ByteWriteToFile = false;
		downloadErrorCallback?.Invoke(taskId, msg);
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
			downloadErrorFiles = new List<string>();
		}
		downloadCompleteCount = 0;
		bytesZip = 0L;
		bytesReceived = 0L;
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
