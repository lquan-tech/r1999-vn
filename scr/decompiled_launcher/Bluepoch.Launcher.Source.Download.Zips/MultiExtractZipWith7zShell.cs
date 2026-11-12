using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
using Bluepoch.Launcher.Utils;
using ThinkingSDK.PC;

namespace Bluepoch.Launcher.Source.Download.Zips;

internal class MultiExtractZipWith7zShell : ExtractInterface
{
	private ConcurrentQueue<string> unZipTaskQueue;

	private Dictionary<string, string> failTask;

	private ConcurrentBag<string> finishTask;

	private string sevenZipPath;

	private string outputDir;

	private SemaphoreSlim semaphore;

	private int activeTasks;

	private object progressLock = new object();

	private long filesTotalSize;

	private long unZipedFileSize;

	private CancellationToken cancellationToken;

	private HashSet<string> successfullyExtractedFiles;

	private string extractedFilesRecordPath;

	private GameVersionInfo mGameVersionInfo;

	private ConcurrentDictionary<string, int> fileProgressMap;

	private const string ERROR_TAG = "MultiExtractZipError";

	public MultiExtractZipWith7zShell()
	{
		unZipTaskQueue = new ConcurrentQueue<string>();
		failTask = new Dictionary<string, string>();
		finishTask = new ConcurrentBag<string>();
		fileProgressMap = new ConcurrentDictionary<string, int>();
		sevenZipPath = (LauncherUtils.is64BitProcess() ? Path.Combine(Application.StartupPath, "Lib/7z/x64", "7za.exe") : Path.Combine(Application.StartupPath, "Lib/7z/x86", "7za.exe"));
		outputDir = GameCommon.GetGameExeInstallDirByDefault();
		Directory.CreateDirectory(outputDir);
		extractedFilesRecordPath = Path.Combine(AppDir.GetAppCacheDir(), "extracted_files_record.txt");
		successfullyExtractedFiles = new HashSet<string>();
	}

	public override async void StartExtract(GameVersionInfo gameVersionInfo)
	{
		if (IsWorking)
		{
			return;
		}
		mGameVersionInfo = gameVersionInfo;
		IsWorking = true;
		finishTask.Clear();
		fileProgressMap.Clear();
		string result;
		while (unZipTaskQueue.TryDequeue(out result))
		{
		}
		unZipTaskQueue = new ConcurrentQueue<string>();
		filesTotalSize = gameVersionInfo.currentPackageInfo.packageSize;
		unZipedFileSize = 0L;
		failTask.Clear();
		successfullyExtractedFiles.Clear();
		LoadExtractedFilesRecord();
		int count = gameVersionInfo.currentPackageInfo.packageUrls.Count;
		maxConcurrency = Math.Min(maxConcurrency, count);
		semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
		cancellationToken = default;
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
		BluepochLogger.InfoLog($"Total unzip file size: {filesTotalSize}, skipped {num} files, actually need to unzip {num2} files");
		if (num2 == 0)
		{
			BluepochLogger.InfoLog("All files have been extracted, no need to extract again");
			IsWorking = false;
			multiUnZipFinishCallback?.Invoke();
		}
		else
		{
			multiUnzipStartCallback?.Invoke();
			StartMultiThreadUnZip();
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
				ExtractZipsFinish();
				return;
			}
			BluepochLogger.WarningLog($"Task count mismatch: success {finishTask.Count} + fail {failTask.Count} != initial task count {initialTaskCount}");
			ExtractZipsFinish();
		}
		catch (Exception ex)
		{
			BluepochLogger.ErrorLog("Error occurred during multi-thread unzipping: " + ex.Message);
			IsWorking = false;
		}
	}

	private async Task ProcessUnzipTasks()
	{
		string archivePath;
		while (!cancellationToken.IsCancellationRequested && unZipTaskQueue.TryDequeue(out archivePath))
		{
			await semaphore.WaitAsync();
			try
			{
				Interlocked.Increment(ref activeTasks);
				BluepochLogger.InfoLog($"Start unzipping file: {archivePath}, active tasks: {activeTasks}");
				await ExtractWith7zExe(archivePath);
			}
			finally
			{
				Interlocked.Decrement(ref activeTasks);
				semaphore.Release();
			}
		}
	}

	private async Task<bool> ExtractWith7zExe(string archivePath)
	{
		int value;
		try
		{
			if (!File.Exists(sevenZipPath))
			{
				BluepochLogger.ErrorLog("7z.exe not found at: " + sevenZipPath);
				return false;
			}
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = sevenZipPath,
				Arguments = $"x \"{archivePath}\" -o\"{outputDir}\" -y -mmt=on -bsp1",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};
			using Process process = new Process();
			process.StartInfo = startInfo;
			process.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
			{
				if (!string.IsNullOrEmpty(e.Data))
				{
					Parse7zProgress(e.Data, archivePath);
				}
			};
			process.Start();
			process.BeginOutputReadLine();
			process.BeginErrorReadLine();
			await process.WaitForExitAsync(cancellationToken);
			bool flag = process.ExitCode == 0;
			if (flag)
			{
				fileProgressMap[archivePath] = 100;
				finishTask.Add(archivePath);
				RecordExtractedFile(archivePath);
				BluepochLogger.DebugLog($"7z.exe Extract success, exit code: {process.ExitCode}, file: {archivePath}");
			}
			else
			{
				fileProgressMap.TryRemove(archivePath, out value);
				failTask[archivePath] = $"process.ExitCode:{process.ExitCode}";
				BluepochLogger.ErrorLog($"7z.exe Extract failed, exit code: {process.ExitCode}, file: {archivePath}");
				FileUtil.DeleteFileWithRetry(archivePath);
			}
			return flag;
		}
		catch (Exception ex)
		{
			fileProgressMap.TryRemove(archivePath, out value);
			failTask[archivePath] = ex.Message;
			BluepochLogger.ErrorLog("7z.exe Extract failed: " + archivePath + ", error: " + ex.Message);
			return false;
		}
	}

	private void Parse7zProgress(string output, string archivePath)
	{
		if (!output.Contains("%"))
		{
			return;
		}
		string[] array = output.Split(new char[1] { '%' }, StringSplitOptions.RemoveEmptyEntries);
		if (array.Length != 0 && int.TryParse(array[0].Trim(), out var result))
		{
			BluepochLogger.InfoLog($"percent : {result}");
			double totalProgress = CalculateTotalProgress(result, archivePath);
			LauncherApplication.GetInstance().InvokeOnUiThread(() =>
			{
				multiUnZipProgressCallback?.Invoke(totalProgress);
			});
		}
	}

	public override void StopExtract()
	{
	}

	private void ExtractZipsFinish()
	{
		BluepochLogger.DebugLog($"Extract completed, success: {finishTask.Count}, fail: {failTask.Count}");
		if (failTask.Count > 0)
		{
			BluepochLogger.DebugLog("Exist extracted files failed, saved extraction record for next continue extraction");
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
		IsWorking = false;
		DeleteSuccessfullyExtractedFiles();
		multiUnZipFinishCallback?.Invoke();
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
				BluepochLogger.InfoLog($"Save extracted files record success, total {successfullyExtractedFiles.Count} files");
			}
		}
		catch (Exception ex)
		{
			BluepochLogger.ErrorLog("Save extracted files record failed: " + ex.Message);
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

	private void DeleteSuccessfullyExtractedFiles()
	{
		FileUtil.DeleteFileWithRetry(extractedFilesRecordPath);
		int num = 0;
		int num2 = 0;
		foreach (string packageUrl in mGameVersionInfo.currentPackageInfo.packageUrls)
		{
			string text = "unKnown-archivePath";
			try
			{
				text = GameCommon.GetGameDownloadRealFilePath(packageUrl);
				num++;
				FileUtil.DeleteFileWithRetry(text);
			}
			catch (Exception ex)
			{
				num2++;
				BluepochLogger.ErrorLog("Delete compressed file failed: " + text + ", error: " + ex.Message);
			}
		}
		BluepochLogger.InfoLog($"Compressed file deletion completed: successfully deleted {num} files, failed {num2} files");
	}

	private long GetFileSize(string filePath)
	{
		try
		{
			if (File.Exists(filePath))
			{
				return new FileInfo(filePath).Length;
			}
			BluepochLogger.WarningLog("File does not exist, cannot get size: " + filePath);
			return 0L;
		}
		catch (Exception ex)
		{
			BluepochLogger.ErrorLog("Get file size failed: " + filePath + ", error: " + ex.Message);
			return 0L;
		}
	}

	private double CalculateTotalProgress(int fileProgress, string archivePath)
	{
		fileProgressMap[archivePath] = fileProgress;
		try
		{
			lock (progressLock)
			{
				if (filesTotalSize <= 0)
				{
					BluepochLogger.WarningLog("Total file size is 0, cannot calculate total progress");
					return 0.0;
				}
				long num = 0L;
				foreach (KeyValuePair<string, int> item in fileProgressMap)
				{
					string key = item.Key;
					int value = item.Value;
					long fileSize = GetFileSize(key);
					if (fileSize > 0)
					{
						long num2 = (long)((double)(fileSize * value) / 100.0);
						num += num2;
					}
				}
				double val = (double)(unZipedFileSize + num) / (double)filesTotalSize * 100.0;
				val = Math.Min(Math.Max(val, 0.0), 100.0);
				val = Math.Round(val, 2);
				BluepochLogger.DebugLog($"CalculateTotalProgress: file progress={fileProgress}%, current file={archivePath}, total size={filesTotalSize}, total progress={val}%");
				return val;
			}
		}
		catch (Exception ex)
		{
			BluepochLogger.ErrorLog("Calculate total progress failed: " + ex.Message);
			return 0.0;
		}
	}
}
