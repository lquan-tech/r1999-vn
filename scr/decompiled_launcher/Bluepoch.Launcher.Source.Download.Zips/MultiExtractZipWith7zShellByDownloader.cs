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

internal class MultiExtractZipWith7zShellByDownloader : ExtractInterface
{
	private static volatile MultiExtractZipWith7zShellByDownloader instance;

	private static readonly object lockObject = new object();

	private ConcurrentQueue<string> unZipTaskQueue;

	private Dictionary<string, string> failTask;

	private ConcurrentBag<string> finishTask;

	private string sevenZipPath;

	private string outputDir;

	private SemaphoreSlim semaphore;

	private object progressLock = new object();

	private long filesTotalSize;

	private long unZipedFileSize;

	private CancellationToken cancellationToken;

	private CancellationTokenSource cancellationTokenSource;

	private HashSet<string> successfullyExtractedFiles;

	private string extractedFilesRecordPath;

	private ConcurrentDictionary<string, int> fileProgressMap;

	private const string ERROR_TAG = "MultiExtractZipWith7zShellByDownloader";

	private Task queueMonitoringTask;

	private readonly object monitoringLock = new object();

	private ConcurrentDictionary<string, byte> runningTask = new ConcurrentDictionary<string, byte>();

	public static MultiExtractZipWith7zShellByDownloader GetInstance()
	{
		if (instance == null)
		{
			lock (lockObject)
			{
				if (instance == null)
				{
					instance = new MultiExtractZipWith7zShellByDownloader();
				}
			}
		}
		return instance;
	}

	private MultiExtractZipWith7zShellByDownloader()
	{
		BluepochLogger.DebugLog("MultiExtractZipWith7zShellByDownloader constructor called, initializing...");
		unZipTaskQueue = new ConcurrentQueue<string>();
		failTask = new Dictionary<string, string>();
		finishTask = new ConcurrentBag<string>();
		fileProgressMap = new ConcurrentDictionary<string, int>();
		sevenZipPath = (LauncherUtils.is64BitProcess() ? Path.Combine(Application.StartupPath, "Lib/7z/x64", "7za.exe") : Path.Combine(Application.StartupPath, "Lib/7z/x86", "7za.exe"));
		outputDir = GameCommon.GetGameExeInstallDirByDefault();
		Directory.CreateDirectory(outputDir);
		extractedFilesRecordPath = Path.Combine(AppDir.GetAppCacheDir(), "extracted_files_record.txt");
		filesTotalSize = GamePlugin.GetInstance().GetGameVersionInfo().currentPackageInfo.packageRealSize;
		LoadExtractedFilesRecord();
	}

	private void StartQueueMonitoring()
	{
		lock (monitoringLock)
		{
			if (IsWorking)
			{
				return;
			}
			IsWorking = true;
			cancellationTokenSource = new CancellationTokenSource();
			cancellationToken = cancellationTokenSource.Token;
			queueMonitoringTask = Task.Run(async () =>
			{
				BluepochLogger.DebugLog("Starting queue monitoring...");
				while (!cancellationToken.IsCancellationRequested)
				{
					try
					{
						if (unZipTaskQueue.Count <= 0)
						{
							await Task.Delay(100, cancellationToken);
						}
						else
						{
							await StartMultiThreadUnZip();
						}
					}
					catch (OperationCanceledException)
					{
						BluepochLogger.DebugLog("Queue monitoring task was cancelled");
						break;
					}
					catch (Exception ex2)
					{
						BluepochLogger.ErrorLog("Error occurred during queue monitoring: " + ex2.Message);
						await Task.Delay(1000, cancellationToken);
					}
				}
				BluepochLogger.DebugLog("Queue monitoring stopped");
			});
		}
	}

	private void StopQueueMonitoring()
	{
		lock (monitoringLock)
		{
			if (IsWorking)
			{
				IsWorking = false;
				cancellationTokenSource?.Cancel();
				try
				{
					queueMonitoringTask?.Wait(5000);
				}
				catch (AggregateException)
				{
				}
				cancellationTokenSource?.Dispose();
				cancellationTokenSource = null;
				queueMonitoringTask = null;
				BluepochLogger.DebugLog("Queue monitoring stopped");
			}
		}
	}

	public void AddExtractUrl(string url)
	{
		if (!File.Exists(sevenZipPath))
		{
			notifyErtraError(sevenZipPath ?? "", "Not found!");
			return;
		}
		string gameDownloadRealFilePath = GameCommon.GetGameDownloadRealFilePath(url);
		if (!IsFileInQueue(gameDownloadRealFilePath) && !IsFileInRunning(gameDownloadRealFilePath))
		{
			unZipTaskQueue.Enqueue(gameDownloadRealFilePath);
			BluepochLogger.InfoLog("Add unzip task: " + gameDownloadRealFilePath);
			if (!IsWorking)
			{
				multiUnzipStartCallback?.Invoke();
				StartQueueMonitoring();
			}
		}
		else
		{
			BluepochLogger.DebugLog("File already in queue or extracted, skipped adding: " + gameDownloadRealFilePath);
		}
	}

	private bool IsFileInRunning(string filePath)
	{
		return runningTask.ContainsKey(filePath);
	}

	private bool IsFileInQueue(string filePath)
	{
		Queue<string> queue = new Queue<string>();
		bool result = false;
		string result2;
		while (unZipTaskQueue.TryDequeue(out result2))
		{
			if (result2 == filePath)
			{
				result = true;
			}
			queue.Enqueue(result2);
		}
		while (queue.Count > 0)
		{
			unZipTaskQueue.Enqueue(queue.Dequeue());
		}
		return result;
	}

	private async Task StartMultiThreadUnZip()
	{
		try
		{
			if (semaphore == null)
			{
				semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
			}
			List<Task> list = new List<Task>();
			for (int i = 0; i < maxConcurrency; i++)
			{
				list.Add(Task.Run(async () =>
				{
					await ProcessUnzipTasks();
				}));
			}
			await Task.WhenAll(list);
			BluepochLogger.DebugLog($"All unzip tasks completed, currently idle, success: {finishTask.Count}, fail: {failTask.Count}");
			CheckAllZipExtract();
		}
		catch (Exception ex)
		{
			BluepochLogger.ErrorLog("Error occurred during multi-thread unzipping: " + ex.Message);
			IsWorking = false;
		}
	}

	private void CheckAllZipExtract()
	{
		List<string> packageUrls = GamePlugin.GetInstance().GetGameVersionInfo().currentPackageInfo.packageUrls;
		HashSet<string> hashSet = new HashSet<string>();
		foreach (string item in packageUrls)
		{
			string gameDownloadRealFilePath = GameCommon.GetGameDownloadRealFilePath(item);
			hashSet.Add(gameDownloadRealFilePath);
		}
		HashSet<string> processedFiles = new HashSet<string>();
		foreach (string successfullyExtractedFile in successfullyExtractedFiles)
		{
			processedFiles.Add(successfullyExtractedFile);
		}
		foreach (string key in failTask.Keys)
		{
			processedFiles.Add(key);
		}
		bool flag = hashSet.All((string file) => processedFiles.Contains(file));
		BluepochLogger.DebugLog($"Check if all files have been extracted: total {hashSet.Count} files to extract, processed {processedFiles.Count} files, all expected files processed: {flag}, failed file count: {failTask.Count}");
		if (flag && hashSet.Count > 0)
		{
			BluepochLogger.DebugLog("All files have been extracted (success or fail)");
			ExtractZipsFinish();
		}
		else if (unZipTaskQueue.Count > 0)
		{
			BluepochLogger.DebugLog($"Still {unZipTaskQueue.Count} unzip tasks in queue, keep waiting");
		}
		else
		{
			BluepochLogger.DebugLog($"No unzip tasks in queue, possibly {runningTask.Count} extraction(s) in progress");
		}
	}

	public override void StartExtract(GameVersionInfo gameVersionInfo)
	{
		foreach (string packageUrl in gameVersionInfo.currentPackageInfo.packageUrls)
		{
			string gameDownloadRealFilePath = GameCommon.GetGameDownloadRealFilePath(packageUrl);
			if (!IsFileAlreadyExtracted(gameDownloadRealFilePath))
			{
				AddExtractUrl(packageUrl);
			}
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
				runningTask.TryAdd(archivePath, 0);
				BluepochLogger.DebugLog($"Start unzipping file: {archivePath}, active tasks: {runningTask.Count}");
				await ExtractWith7zExe(archivePath);
				runningTask.TryRemove(archivePath, out var _);
			}
			finally
			{
				semaphore.Release();
			}
		}
	}

	private async Task<bool> ExtractWith7zExe(string archivePath)
	{
		int value;
		try
		{
			if (!File.Exists(archivePath))
			{
				BluepochLogger.DebugLog("process.ExitCode:Not found - " + archivePath);
				failTask[archivePath] = "process.ExitCode:Not found";
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
				failTask.Remove(archivePath);
				RecordExtractedFile(archivePath);
				BluepochLogger.DebugLog($"7z.exe Extract success, exit code: {process.ExitCode}, file: {archivePath}");
				FileUtil.DeleteFileWithRetry(archivePath);
			}
			else
			{
				fileProgressMap.TryRemove(archivePath, out value);
				string value2 = Get7zExitCodeMessage(process.ExitCode);
				failTask[archivePath] = $"process.ExitCode:{process.ExitCode} ({value2})";
				BluepochLogger.ErrorLog($"7z.exe Extract failed, exit code: {process.ExitCode} ({value2}), file: {archivePath}");
				Dictionary<string, object> properties = new Dictionary<string, object>
				{
					[TrackEvent.EVENT_Result_Msg] = value2,
					[TrackEvent.Exception_Location] = "MultiExtractZipWith7zShellByDownloader-" + archivePath
				};
				ThinkingPCSDKManager.getInstance().TrackEvent(TrackEvent.EVENT_Exception, properties);
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

	private static string Get7zExitCodeMessage(int exitCode)
	{
		return exitCode switch
		{
			0 => "Success", 
			1 => "Warning (non-fatal errors on some files)", 
			2 => "Fatal error (7z internal error or insufficient memory)", 
			7 => "Command line error", 
			8 => "Not enough memory", 
			225 => "User cancelled", 
			255 => "Write error (insufficient disk space or permission)", 
			_ => $"Unknown exit code {exitCode}", 
		};
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
			BluepochLogger.DebugLog($"percent : {result}");
			double totalProgress = CalculateTotalProgress(result, archivePath);
			LauncherApplication.GetInstance().InvokeOnUiThread(() =>
			{
				multiUnZipProgressCallback?.Invoke(totalProgress);
			});
		}
	}

	public override void StopExtract()
	{
		StopQueueMonitoring();
		BluepochLogger.DebugLog("StopExtract : instance set to null");
		instance = null;
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
		multiUnzipErrorCallback?.Invoke(fileName, msg);
		Reset();
		Dictionary<string, object> properties = new Dictionary<string, object>
		{
			[TrackEvent.EVENT_Result_Msg] = msg,
			[TrackEvent.Exception_Location] = $"{"MultiExtractZipWith7zShellByDownloader"}-{fileName}，Count:{failTask.Count}"
		};
		ThinkingPCSDKManager.getInstance().TrackEvent(TrackEvent.EVENT_Exception, properties);
	}

	private void notifyErtraFinish()
	{
		DeleteSuccessfullyExtractedFiles();
		Reset();
		multiUnZipFinishCallback?.Invoke();
	}

	private void Reset()
	{
		lock (monitoringLock)
		{
			unZipTaskQueue.Clear();
			failTask.Clear();
			finishTask.Clear();
			successfullyExtractedFiles.Clear();
			runningTask.Clear();
			fileProgressMap.Clear();
			filesTotalSize = 0L;
			unZipedFileSize = 0L;
			IsWorking = false;
		}
	}

	private void LoadExtractedFilesRecord()
	{
		successfullyExtractedFiles = GameCommon.LoadExtractedFilesRecord();
		if (successfullyExtractedFiles.Count == 0)
		{
			return;
		}
		foreach (string successfullyExtractedFile in successfullyExtractedFiles)
		{
			if (!string.IsNullOrWhiteSpace(successfullyExtractedFile))
			{
				fileProgressMap[successfullyExtractedFile.Trim()] = 100;
				BluepochLogger.DebugLog("Load extracted files record: " + successfullyExtractedFile.Trim());
			}
		}
	}

	private void SaveExtractedFilesRecord()
	{
		try
		{
			if (successfullyExtractedFiles.Count > 0)
			{
				File.WriteAllLines(extractedFilesRecordPath, successfullyExtractedFiles);
				BluepochLogger.DebugLog($"Save extracted files record success, total {successfullyExtractedFiles.Count} files");
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
			BluepochLogger.DebugLog("Record successfully extracted file: " + archivePath);
		}
		SaveExtractedFilesRecord();
	}

	private void DeleteSuccessfullyExtractedFiles()
	{
		FileUtil.DeleteFileWithRetry(extractedFilesRecordPath);
		int num = 0;
		int num2 = 0;
		foreach (string packageUrl in GamePlugin.GetInstance().GetGameVersionInfo().currentPackageInfo.packageUrls)
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
					long gameZipUnPackSizeFromPath = GameCommon.GetGameZipUnPackSizeFromPath(key);
					if (gameZipUnPackSizeFromPath > 0)
					{
						long num2 = (long)((double)(gameZipUnPackSizeFromPath * value) / 100.0);
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
