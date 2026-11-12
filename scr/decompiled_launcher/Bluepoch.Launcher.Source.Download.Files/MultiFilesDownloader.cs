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
using Bluepoch.Launcher.Source.Download.Data;
using Bluepoch.Launcher.Source.Game;
using Bluepoch.Launcher.Utils;
using Microsoft.EntityFrameworkCore;
using ThinkingSDK.PC;

namespace Bluepoch.Launcher.Source.Download.Files;

internal class MultiFilesDownloader : DownloadInterface
{
	private MultiFilesDownloadDirInfo filesFromJson;

	private List<MultiFilesInfo> needToDownloadFileInfo;

	private static int MaxErrorFilesCount = 300;

	private DownloadDbContext downloadDbContext;

	private List<MultiFilesInfo> downloadErrorFiles = new List<MultiFilesInfo>();

	private DateTime _lastSpeedCheckTime = DateTime.Now;

	private long _lastDownloadedBytes;

	public override void CloseDownload()
	{
		try
		{
			needToDownloadFileInfo?.Clear();
			filesFromJson = null;
			downloadCompleteCount = 0;
			downloadTotalCount = 0;
			downloadDbContext.Dispose();
			downloadDbContext = null;
		}
		catch (Exception ex)
		{
			Dictionary<string, object> properties = new Dictionary<string, object>
			{
				[TrackEvent.EVENT_Result_Msg] = ex.Message,
				[TrackEvent.Exception_Location] = "CloseDownload"
			};
			ThinkingPCSDKManager.getInstance().TrackEvent(TrackEvent.EVENT_Exception, properties);
			BluepochLogger.ErrorLog("CloseDownload - ex : $" + ex.Message);
		}
	}

	public override void PreStart()
	{
	}

	public override void Reset()
	{
		Is_ByteWriteToFile = false;
		Is_Downloading = false;
	}

	public override void StartDownload(GameVersionInfo gameVersionInfo)
	{
		if (Is_Downloading)
		{
			BluepochLogger.DebugLog("已经在下载中，无法重复启动");
			return;
		}
		if (m_gameVersionInfo != null && needToDownloadFileInfo != null && m_gameVersionInfo.currentPackageInfo.packageJson.Equals(gameVersionInfo.currentPackageInfo.packageJson))
		{
			if (needToDownloadFileInfo.Count > 0)
			{
				BluepochLogger.DebugLog("use [needToDownloadFileInfo] mc data，start to download");
				StartDownloadMultiFiles(needToDownloadFileInfo);
				return;
			}
			if (filesFromJson != null && filesFromJson.smallFilesInfo.Count > 0)
			{
				BluepochLogger.DebugLog("use [filesFromJson] mc data，start to download");
				MixDBAndJsonFileData();
			}
		}
		Reset();
		PreStart();
		m_gameVersionInfo = gameVersionInfo;
		FileDownloader jsonFileDownloader = new FileDownloader(m_gameVersionInfo.currentPackageInfo.packageJson, 0L);
		if (SettingConfigManager.GetInstance().GetManifest().TryGetValue("download_host_bak", out var value))
		{
			jsonFileDownloader.FallbackDomain = value;
		}
		Task.Run(async () =>
		{
			try
			{
				FileDownloader fileDownloader = jsonFileDownloader;
				string gameJsonFilePath = GameCommon.GetGameJsonFilePath(m_gameVersionInfo.currentPackageInfo.packageJson);
				FileDownloader.DownloadCompletedCallback completedCallback = (string id, string path) =>
				{
					InvokeOnUiThread(async () =>
					{
						await JsonFileDownloadCompleteCallback(id, path);
					});
				};
				FileDownloader.DownloadFailedCallback failedCallback = (string id, string url, Exception ex4) =>
				{
					InvokeOnUiThread(() =>
					{
						JsonFileDownloadFailedCallback(id, ex4);
					});
				};
				FileDownloader.DownloadErrorTrackCallback errorTrackCallback = ErrorTrackCallback;
				await fileDownloader.DownloadFileAsync(gameJsonFilePath, null, null, null, null, completedCallback, failedCallback, default, IsElapsedSecond: false, errorTrackCallback);
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Exception ex3 = ex2;
				InvokeOnUiThread(() =>
				{
					JsonFileDownloadFailedCallback(jsonFileDownloader.DownloadTaskId, ex3);
				});
			}
		});
	}

	private void ErrorTrackCallback(string downloadTaskId, int errorCode, Exception ex, bool isRetry)
	{
		Dictionary<string, object> properties = new Dictionary<string, object>
		{
			[TrackEvent.EVENT_Result_Msg] = $"{ex.Message} - {ex.GetType()} - ErrorCode:{errorCode} - Retry:{isRetry}",
			[TrackEvent.Exception_Location] = "FileDownloader-" + downloadTaskId
		};
		ThinkingPCSDKManager.getInstance().TrackEvent(TrackEvent.EVENT_Exception, properties);
	}

	private void JsonFileDownloadFailedCallback(string downloadTaskId, Exception ex)
	{
		int value = (ex as FileDownloader.DownloadException)?.ExceptionCode ?? 1999;
		BluepochLogger.DebugLog($"Json file get fail , Please Contact us! ErrorCode:{value} {ex.Message}");
		NotifyDownloadError(downloadTaskId, "MultiFilesDownloader.Error_GameJson");
	}

	private async Task JsonFileDownloadCompleteCallback(string downloadTaskId, string savePath, int errorCode = 200, string errorMsg = "")
	{
		string jsonFilePath = GameCommon.GetGameJsonFilePath(m_gameVersionInfo.currentPackageInfo.packageJson);
		if (!File.Exists(jsonFilePath))
		{
			BluepochLogger.DebugLog("Json file is not here " + jsonFilePath + ", Please Contact us!");
			NotifyDownloadError(downloadTaskId, "MultiFilesDownloader.Error_GameJson");
			return;
		}
		try
		{
			string text = Path.GetFileName(savePath).Split('_')[^1].Split('.')[0];
			BluepochLogger.InfoLog("jsonFile md5 : " + text);
			string fileMD = FileUtil.GetFileMD5(savePath);
			if (!text.Equals(fileMD))
			{
				BluepochLogger.DebugLog("Json file md5 is not match " + jsonFilePath + ", Please Contact us!");
				NotifyDownloadError(downloadTaskId, "MultiFilesDownloader.Error_GameJson");
				return;
			}
			filesFromJson = await GameCommon.GetDownloadInfoFromJsonFileAsync(jsonFilePath);
			if (filesFromJson == null || filesFromJson.smallFilesInfo.Count == 0)
			{
				BluepochLogger.DebugLog("Json file is not good " + jsonFilePath + ", Please Contact us!");
				NotifyDownloadError(downloadTaskId, "MultiFilesDownloader.Error_GameJson");
				return;
			}
		}
		catch (Exception ex)
		{
			BluepochLogger.DebugLog("Json file download complete callback Exception: " + ex.Message);
			NotifyDownloadError(downloadTaskId, "MultiFilesDownloader.Error_GameJson");
			return;
		}
		MixDBAndJsonFileData();
	}

	private async Task MixDBAndJsonFileData()
	{
		bytesReceived = 0L;
		using (downloadDbContext = new DownloadDbContext(GameCommon.GetDbFilePath(m_gameVersionInfo.currentPackageInfo.packageJson)))
		{
			downloadDbContext.Database.EnsureCreated();
			List<MultiFilesInfo> list = await downloadDbContext.multiFilesInfo.ToListAsync();
			if (list.Count == filesFromJson.smallFilesInfo.Count)
			{
				BluepochLogger.InfoLog("数据库中已经存在相同的数据，且数量一致，证明可能已经下载过了");
				needToDownloadFileInfo = new List<MultiFilesInfo>();
			}
			else
			{
				needToDownloadFileInfo = ComputerNotDownloadFileInfo(filesFromJson.smallFilesInfo, list);
				if (needToDownloadFileInfo == null || needToDownloadFileInfo.Count == 0)
				{
					BluepochLogger.InfoLog("[needToDownloadFileInfo] compare error!");
					needToDownloadFileInfo = filesFromJson.smallFilesInfo;
				}
			}
			if (needToDownloadFileInfo == null || needToDownloadFileInfo.Count == 0)
			{
				NotifyDownloadFinish();
				Is_Downloading = false;
				return;
			}
			BluepochLogger.InfoLog($"need to download fils count：{needToDownloadFileInfo.Count}");
			bytesLastSecond = list.Sum((MultiFilesInfo f) => f.size);
			bytesReceived = bytesLastSecond;
			bytesTotal = m_gameVersionInfo.currentPackageInfo.packageRealSize;
			StartDownloadMultiFiles(needToDownloadFileInfo);
			list.Clear();
		}
	}

	private void StartDownloadMultiFiles(List<MultiFilesInfo> notDownloadFileInfo)
	{
		Is_Downloading = true;
		downloadCompleteCount = 0;
		downloadTotalCount = needToDownloadFileInfo.Count;
		GameCommon.UpdateGameVersionInCache(m_gameVersionInfo.latestVersion);
		GameCommon.UpdateGameStatusInCache(1);
		GameCommon.UpdateGameSizeInCache(m_gameVersionInfo.currentPackageInfo.packageRealSize);
		GameCommon.UpdateGameUpdateTypeInCache((int)m_gameVersionInfo.updateType);
		new SemaphoreSlim(maxConcurrency);
		new List<Task>();
		BluepochLogger.InfoLog($"Start to download files，totalCount：{notDownloadFileInfo.Count}，bytesReceived：{bytesReceived}，threadCount：{maxConcurrency}");
		_downloadCts = new CancellationTokenSource();
		downloadErrorFiles.Clear();
		Task.Run(async () =>
		{
			await ProcessDownloadQueue(notDownloadFileInfo, maxConcurrency);
		}, _downloadCts.Token);
	}

	private async Task ProcessDownloadQueue(List<MultiFilesInfo> fileInfos, int maxConcurrency)
	{
		Queue<MultiFilesInfo> downloadQueue = new Queue<MultiFilesInfo>(fileInfos);
		List<Task> activeTasks = new List<Task>();
		int completedCount = 0;
		int totalCount = fileInfos.Count;
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
				MultiFilesInfo fileInfo = downloadQueue.Dequeue();
				Task item = CreateDownloadTask(fileInfo);
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
		Is_Downloading = false;
		BluepochLogger.DebugLog($"下载任务结束，更新状态，已完成数(暂停也是结束)：{completedCount}，队列剩余：{downloadQueue.Count}");
		if (_downloadCts.Token.IsCancellationRequested)
		{
			BluepochLogger.DebugLog("下载已暂停");
		}
	}

	private async Task CreateDownloadTask(MultiFilesInfo fileInfo)
	{
		InvokeOnUiThread(() =>
		{
			DownloadStartCallback(fileInfo.dest);
		});
		string url = filesFromJson.packedhost + fileInfo.dest;
		long speedLimitBytesPerSecond = 0L;
		if (AppSettings.Instance.DownloadSpeedLimit)
		{
			speedLimitBytesPerSecond = AppSettings.Instance.DownloadSpeed * 1024 / (maxConcurrency - 2);
		}
		FileDownloader fileDownloader = new FileDownloader(url, speedLimitBytesPerSecond);
		if (SettingConfigManager.GetInstance().GetManifest().TryGetValue("download_host_bak", out var value))
		{
			fileDownloader.FallbackDomain = value;
		}
		string text = GameCommon.GetGameExeInstallDirByDefault() + fileInfo.dest;
		if (File.Exists(text))
		{
			if (FileUtil.GetFileMD5(text).Equals(fileInfo.md5))
			{
				InvokeOnUiThread(() =>
				{
					DownloadProgressChanged(fileDownloader.DownloadTaskId, fileInfo, 100, fileInfo.size);
				});
				InvokeOnUiThread(() =>
				{
					DownloadCompleteCallback(fileDownloader.DownloadTaskId, fileInfo, "", 200, "", fileExists: true);
				});
				return;
			}
			File.Delete(text);
			BluepochLogger.DebugLog("文件MD5校验失败，删除已存在的文件，重新下载：" + fileInfo.dest);
		}
		string savePath = GameCommon.GetGameExeInstallDirByDefault() + fileInfo.dest + ".tmp";
		await fileDownloader.DownloadFileAsync(savePath, null, (string id) =>
		{
			InvokeOnUiThread(() =>
			{
				DownloadStopCallback(id);
			});
		}, null, (string id, int p, long downloadedBytes) =>
		{
			InvokeOnUiThread(() =>
			{
				DownloadProgressChanged(id, fileInfo, p, downloadedBytes);
			});
		}, (string id, string path) =>
		{
			InvokeOnUiThread(() =>
			{
				DownloadCompleteCallback(id, fileInfo, path);
			});
		}, (string id, string text2, Exception ex) =>
		{
			InvokeOnUiThread(() =>
			{
				DownloadFailedCallback(id, fileInfo, ex);
			});
		}, _downloadCts.Token, IsElapsedSecond: false, ErrorTrackCallback);
	}

	private void DownloadFailedCallback(string downloadTaskId, MultiFilesInfo multiFilesInfo, Exception ex)
	{
		int value = (ex as FileDownloader.DownloadException)?.ExceptionCode ?? 1999;
		BluepochLogger.InfoLog($"DownloadFailedCallback downloadTaskId：{downloadTaskId}，ErrorCode:{value}");
		if (multiFilesInfo.forceType)
		{
			DownloadCompleteCallback(downloadTaskId, multiFilesInfo, string.Empty, 206, ex.Message);
			return;
		}
		DownloadProgressChanged(downloadTaskId, multiFilesInfo, 100, multiFilesInfo.size);
		BluepochLogger.InfoLog("This File " + multiFilesInfo.dest + " is not force！Exception : " + ex.Message);
		DownloadCompleteCallback(downloadTaskId, multiFilesInfo, string.Empty, 200, ex.Message, fileExists: true);
	}

	private void DownloadCompleteCallback(string downloadTaskId, MultiFilesInfo multiFilesInfo, string savePath = "", int errorCode = 200, string errorMsg = "", bool fileExists = false)
	{
		downloadCompleteCount++;
		if (errorCode == 200)
		{
			if (!fileExists && !RenameSavePathFileToRealFile(multiFilesInfo))
			{
				DownloadCompleteCallback(downloadTaskId, multiFilesInfo, string.Empty, 206, "Rename SavePathFile ToRealFile Fail!");
				return;
			}
			using (downloadDbContext = new DownloadDbContext(GameCommon.GetDbFilePath(m_gameVersionInfo.currentPackageInfo.packageJson)))
			{
				MultiFilesInfo multiFilesInfo2 = downloadDbContext.multiFilesInfo.FirstOrDefault((MultiFilesInfo m) => m.id == multiFilesInfo.id);
				if (multiFilesInfo2 != null)
				{
					multiFilesInfo2.dest = multiFilesInfo.dest;
					multiFilesInfo2.md5 = multiFilesInfo.md5;
					multiFilesInfo2.size = multiFilesInfo.size;
					multiFilesInfo2.forceType = multiFilesInfo.forceType;
					BluepochLogger.DebugLog($"更新数据库记录：id={multiFilesInfo.id}");
				}
				else
				{
					downloadDbContext.multiFilesInfo.Add(multiFilesInfo);
				}
				downloadDbContext.SaveChanges();
				needToDownloadFileInfo?.Remove(multiFilesInfo);
				if (downloadCompleteCount != downloadTotalCount)
				{
					return;
				}
				if (downloadErrorFiles.Count > 0)
				{
					foreach (MultiFilesInfo downloadErrorFile in downloadErrorFiles)
					{
						BluepochLogger.InfoLog("this is file" + downloadErrorFile.dest + " downlaod error!");
					}
					NotifyDownloadError(string.Empty, "MultiFilesDownloader.Error_DownloadFiles");
				}
				else
				{
					BluepochLogger.InfoLog($"All files downloaded ，total：{downloadCompleteCount}");
					Is_Downloading = false;
					Is_ByteWriteToFile = false;
					needToDownloadFileInfo?.Clear();
					NotifyDownloadFinish();
				}
				return;
			}
		}
		downloadErrorFiles.Add(multiFilesInfo);
		if (downloadErrorFiles.Count > MaxErrorFilesCount)
		{
			if (Is_Downloading)
			{
				StopDownload();
				BluepochLogger.InfoLog("Game File downlaod error is too much!");
				NotifyDownloadError(string.Empty, "MultiFilesDownloader.Error_DownloadFiles");
			}
		}
		else
		{
			if (downloadCompleteCount != downloadTotalCount)
			{
				return;
			}
			foreach (MultiFilesInfo downloadErrorFile2 in downloadErrorFiles)
			{
				BluepochLogger.InfoLog("this is file" + downloadErrorFile2.dest + " downlaod error!");
			}
			NotifyDownloadError(string.Empty, "MultiFilesDownloader.Error_DownloadFiles");
		}
	}

	private bool RenameSavePathFileToRealFile(MultiFilesInfo multiFilesInfo)
	{
		try
		{
			string text = GameCommon.GetGameExeInstallDirByDefault() + multiFilesInfo.dest;
			string text2 = GameCommon.GetGameExeInstallDirByDefault() + multiFilesInfo.dest + ".tmp";
			if (File.Exists(text2) && !File.Exists(text))
			{
				File.Move(text2, text);
				return true;
			}
			return false;
		}
		catch (Exception ex)
		{
			Dictionary<string, object> properties = new Dictionary<string, object>
			{
				[TrackEvent.EVENT_Result_Msg] = ex.Message,
				[TrackEvent.Exception_Location] = "RenameSavePathFileToRealFile-" + multiFilesInfo.dest
			};
			ThinkingPCSDKManager.getInstance().TrackEvent(TrackEvent.EVENT_Exception, properties);
			BluepochLogger.ErrorLog("RenameSavePathFileToRealFile  Temp File Exception: " + ex.Message);
			return false;
		}
	}

	private void DownloadProgressChanged(string downloadTaskId, MultiFilesInfo filesInfo, int progress, long downloadedBytes)
	{
		if (_downloadCts.Token.IsCancellationRequested)
		{
			return;
		}
		bytesReceived += downloadedBytes;
		DateTime now = DateTime.Now;
		TimeSpan timeSpan = now - _lastSpeedCheckTime;
		if (timeSpan.TotalSeconds >= 0.5)
		{
			double speedBytesPerSecond = (double)(bytesReceived - _lastDownloadedBytes) / timeSpan.TotalSeconds;
			if (progressPerSecondCallback != null && timeSpan.TotalSeconds > 0.0 && downloadedBytes > 0)
			{
				double progress2 = Math.Round((double)bytesReceived * 100.0 / (double)bytesTotal, 2);
				progressPerSecondCallback(bytesReceived, progress2, speedBytesPerSecond, bytesTotal);
			}
			_lastSpeedCheckTime = now;
			_lastDownloadedBytes = bytesReceived;
		}
	}

	private void DownloadStartCallback(string downloadTaskId)
	{
		if (!Is_ByteWriteToFile)
		{
			_lastSpeedCheckTime = DateTime.Now;
			bytesTotal = m_gameVersionInfo.currentPackageInfo.packageRealSize;
			downloadStartCallback?.Invoke(bytesReceived, Math.Round((double)bytesReceived * 100.0 / (double)bytesTotal, 2), 0.0, bytesTotal);
			Is_ByteWriteToFile = true;
		}
	}

	private void DownloadStopCallback(string downloadTaskId)
	{
		BluepochLogger.DebugLog("下载任务停止 " + downloadTaskId);
	}

	private void NotifyDownloadError(string taskId, string msg)
	{
		downloadErrorCallback?.Invoke(taskId, msg);
	}

	private void NotifyDownloadFinish()
	{
		downloadFinishCallback();
	}

	private List<MultiFilesInfo> ComputerNotDownloadFileInfo(List<MultiFilesInfo> list1, List<MultiFilesInfo> list2)
	{
		return list1.Except(list2).ToList();
	}

	public override void StopDownload()
	{
		if (_downloadCts != null)
		{
			_downloadCts.Cancel();
			Is_Downloading = false;
			Is_ByteWriteToFile = false;
			downloadPauseCallback?.Invoke();
		}
	}
}
