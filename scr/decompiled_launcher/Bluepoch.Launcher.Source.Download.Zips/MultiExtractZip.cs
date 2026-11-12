using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Bluepoch.Common.Util;
using Bluepoch.Launcher.App;
using Bluepoch.Launcher.Constants;
using Bluepoch.Launcher.Source.Game;
using Bluepoch.Launcher.Source.Utils;
using SevenZip;
using ThinkingSDK.PC;

namespace Bluepoch.Launcher.Source.Download.Zips;

internal class MultiExtractZip : ExtractInterface, IDisposable
{
	private ConcurrentQueue<string> unZipTaskQueue;

	private Dictionary<string, string> failTask;

	private ConcurrentBag<string> finishTask;

	private long filesTotalSize;

	private long unZipedFileSize;

	private string outputDir;

	private SemaphoreSlim semaphore;

	private CancellationTokenSource cancellationTokenSource;

	private int activeTasks;

	private object progressLock = new object();

	private HashSet<string> successfullyExtractedFiles;

	private string extractedFilesRecordPath;

	private const string ERROR_TAG = "MultiExtractZipError";

	public MultiExtractZip()
	{
		unZipTaskQueue = new ConcurrentQueue<string>();
		failTask = new Dictionary<string, string>();
		finishTask = new ConcurrentBag<string>();
		string startupPath = Application.StartupPath;
		SevenZipBase.SetLibraryPath(LauncherUtils.is64BitProcess() ? Path.Combine(startupPath, "x64", "7z.dll") : Path.Combine(startupPath, "x86", "7z.dll"));
		outputDir = GameCommon.GetGameExeInstallDirByDefault();
		Directory.CreateDirectory(outputDir);
		extractedFilesRecordPath = Path.Combine(AppDir.GetAppCacheDir(), "extracted_files_record.txt");
		successfullyExtractedFiles = new HashSet<string>();
		LoadExtractedFilesRecord();
	}

	private void LoadExtractedFilesRecord()
	{
		try
		{
			if (File.Exists(extractedFilesRecordPath))
			{
				string[] array = File.ReadAllLines(extractedFilesRecordPath);
				foreach (string text in array)
				{
					if (!string.IsNullOrWhiteSpace(text))
					{
						successfullyExtractedFiles.Add(text.Trim());
					}
				}
				BluepochLogger.InfoLog($"Read extracted files record success, total {successfullyExtractedFiles.Count} files");
			}
			else
			{
				BluepochLogger.InfoLog("Extracted files record not found, will create a new record");
			}
		}
		catch (Exception ex)
		{
			BluepochLogger.ErrorLog("Read extracted files record failed: " + ex.Message);
		}
	}

	private void SaveExtractedFilesRecord()
	{
		try
		{
			if (successfullyExtractedFiles.Count > 0)
			{
				File.WriteAllLines(extractedFilesRecordPath, successfullyExtractedFiles);
				BluepochLogger.InfoLog($"Record extracted files record success, total {successfullyExtractedFiles.Count} files");
			}
		}
		catch (Exception ex)
		{
			BluepochLogger.ErrorLog("Record extracted files record failed: " + ex.Message);
		}
	}

	private bool IsFileAlreadyExtracted(string archivePath)
	{
		return successfullyExtractedFiles.Contains(archivePath);
	}

	private void RecordExtractedFile(string archivePath)
	{
		if (!successfullyExtractedFiles.Contains(archivePath))
		{
			successfullyExtractedFiles.Add(archivePath);
			BluepochLogger.InfoLog("Record successfully extracted file: " + archivePath);
		}
		SaveExtractedFilesRecord();
	}

	public override void StartExtract(GameVersionInfo gameVersionInfo)
	{
		if (IsWorking)
		{
			return;
		}
		IsWorking = true;
		finishTask.Clear();
		string result;
		while (unZipTaskQueue.TryDequeue(out result))
		{
		}
		unZipTaskQueue = new ConcurrentQueue<string>();
		filesTotalSize = gameVersionInfo.currentPackageInfo.packageSize;
		unZipedFileSize = 0L;
		failTask.Clear();
		int count = gameVersionInfo.currentPackageInfo.packageUrls.Count;
		maxConcurrency = Math.Min(maxConcurrency, count);
		semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
		cancellationTokenSource = new CancellationTokenSource();
		BluepochLogger.InfoLog($"Start multi-thread unzipping, max concurrency: {maxConcurrency}, total files: {count}");
		int num = 0;
		int num2 = 0;
		foreach (string packageUrl in gameVersionInfo.currentPackageInfo.packageUrls)
		{
			string gameDownloadRealFilePath = GameCommon.GetGameDownloadRealFilePath(packageUrl);
			if (File.Exists(gameDownloadRealFilePath))
			{
				if (IsFileAlreadyExtracted(gameDownloadRealFilePath))
				{
					BluepochLogger.InfoLog("File already extracted, skipped: " + gameDownloadRealFilePath);
					unZipedFileSize += GameCommon.GetGameZipSizeFromUrl(packageUrl);
					num++;
				}
				else
				{
					unZipTaskQueue.Enqueue(gameDownloadRealFilePath);
					num2++;
				}
				continue;
			}
			BluepochLogger.ErrorLog("Unzip file not found, cannot unzip: " + gameDownloadRealFilePath + ", please redownload the file");
			notifyErtraError(gameDownloadRealFilePath, "ZlibHelper.ExeError");
			return;
		}
		BluepochLogger.InfoLog($"Total unzip file size: {filesTotalSize}, skipped {num} files, actually need unzip {num2} files");
		if (num2 == 0)
		{
			BluepochLogger.InfoLog("All files already unzipped, no need to unzip again");
			IsWorking = false;
			multiUnZipFinishCallback?.Invoke();
		}
		else
		{
			multiUnzipStartCallback?.Invoke();
			StartMultiThreadUnZip();
		}
	}

	private async Task ProcessUnzipTasks()
	{
		string archivePath;
		while (!cancellationTokenSource.Token.IsCancellationRequested && unZipTaskQueue.TryDequeue(out archivePath))
		{
			await semaphore.WaitAsync();
			try
			{
				Interlocked.Increment(ref activeTasks);
				BluepochLogger.InfoLog($"Start unzipping file: {archivePath}, active tasks: {activeTasks}");
				await ExtractorZip(archivePath);
			}
			finally
			{
				Interlocked.Decrement(ref activeTasks);
				semaphore.Release();
			}
		}
	}

	private async Task ExtractorZip(string archivePath)
	{
		try
		{
			await Task.Run(() =>
			{
				using SevenZipExtractor sevenZipExtractor = new SevenZipExtractor(archivePath, InArchiveFormat.SevenZip);
				sevenZipExtractor.FileExtractionFinished += (object? sender, FileInfoEventArgs e) =>
				{
					lock (progressLock)
					{
						unZipedFileSize += (long)e.FileInfo.Size;
						double totalProgress = Math.Round((double)unZipedFileSize / (double)filesTotalSize * 100.0, 2);
						LauncherApplication.GetInstance().InvokeOnUiThread(() =>
						{
							multiUnZipProgressCallback?.Invoke(totalProgress);
						});
					}
				};
				sevenZipExtractor.ExtractArchive(outputDir);
			});
			finishTask.Add(archivePath);
			RecordExtractedFile(archivePath);
			BluepochLogger.InfoLog("Unzip success: " + archivePath);
		}
		catch (Exception ex)
		{
			failTask.Add(archivePath, ex.Message);
			BluepochLogger.ErrorLog("Unzip failed: " + archivePath + ", error: " + ex.Message);
		}
	}

	private async void StartMultiThreadUnZip()
	{
		try
		{
			int initialTaskCount = unZipTaskQueue.Count;
			BluepochLogger.InfoLog($"Start multi-thread unzipping, initial task count: {initialTaskCount}");
			List<Task> list = new List<Task>();
			for (int i = 0; i < maxConcurrency; i++)
			{
				list.Add(Task.Run(async () =>
				{
					await ProcessUnzipTasks();
				}));
			}
			await Task.WhenAll(list);
			BluepochLogger.InfoLog($"All unzipping tasks completed, success: {finishTask.Count}, fail: {failTask.Count}, initial task count: {initialTaskCount}");
			if (finishTask.Count + failTask.Count == initialTaskCount)
			{
				InstallGameFinish();
				return;
			}
			BluepochLogger.WarningLog($"Task count mismatch: success {finishTask.Count} + fail {failTask.Count} != initial task count {initialTaskCount}");
			InstallGameFinish();
		}
		catch (Exception ex)
		{
			BluepochLogger.ErrorLog("Error occurred during multi-thread unzipping: " + ex.Message);
			IsWorking = false;
		}
	}

	private void InstallGameFinish()
	{
		BluepochLogger.InfoLog($"Unzip completed, success: {finishTask.Count}, fail: {failTask.Count}");
		if (failTask.Count > 0)
		{
			BluepochLogger.InfoLog("Unzipped files is been recorded, saved unzip records for next continue unzipping");
			notifyErtraError(failTask.Keys.ToArray()[0], failTask.Values.ToArray()[0]);
		}
		else
		{
			notifyErtraFinish();
		}
	}

	private void notifyErtraError(string fileName, string msg)
	{
		IsWorking = false;
		multiUnzipErrorCallback?.Invoke(fileName, msg);
		Dictionary<string, object> properties = new Dictionary<string, object>
		{
			[TrackEvent.EVENT_Result_Msg] = msg,
			[TrackEvent.Exception_Location] = $"{"MultiExtractZipError"}-{fileName}，Count:{failTask.Count}"
		};
		ThinkingPCSDKManager.getInstance().TrackEvent(TrackEvent.EVENT_Exception, properties);
	}

	private void notifyErtraFinish()
	{
		DeleteSuccessfullyExtractedFiles();
		multiUnZipFinishCallback?.Invoke();
	}

	private void DeleteSuccessfullyExtractedFiles()
	{
		if (finishTask.Count == 0)
		{
			BluepochLogger.InfoLog("No finishTask for unzip !");
			return;
		}
		int num = 0;
		int num2 = 0;
		foreach (string item in finishTask)
		{
			try
			{
				if (File.Exists(item))
				{
					File.Delete(item);
					num++;
					BluepochLogger.InfoLog("Deleted zip file: " + item);
				}
				else
				{
					BluepochLogger.WarningLog("Zip file not found, cannot delete: " + item);
				}
			}
			catch (Exception ex)
			{
				num2++;
				BluepochLogger.ErrorLog("Failed to delete zip file: " + item + ", error: " + ex.Message);
			}
		}
		BluepochLogger.InfoLog($"Zip file deletion completed: successfully deleted {num} files, failed {num2} files");
	}

	public void Dispose()
	{
		SaveExtractedFilesRecord();
	}

	public override void StopExtract()
	{
		if (IsWorking)
		{
			cancellationTokenSource?.Cancel();
			IsWorking = false;
			BluepochLogger.InfoLog("Extract task has been stopped");
		}
	}
}
