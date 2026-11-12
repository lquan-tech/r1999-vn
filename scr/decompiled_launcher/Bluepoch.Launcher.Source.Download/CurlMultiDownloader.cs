using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Bluepoch.Common.Util;

namespace Bluepoch.Launcher.Source.Download;

internal class CurlMultiDownloader
{
	private enum CURLoption
	{
		CURLOPT_URL = 10002,
		CURLOPT_WRITEFUNCTION = 20011,
		CURLOPT_WRITEDATA = 10001,
		CURLOPT_PROGRESSFUNCTION = 20056,
		CURLOPT_PROGRESSDATA = 10056,
		CURLOPT_NOPROGRESS = 43,
		CURLOPT_FOLLOWLOCATION = 52,
		CURLOPT_MAXREDIRS = 68,
		CURLOPT_SSL_VERIFYPEER = 64,
		CURLOPT_SSL_VERIFYHOST = 81,
		CURLOPT_TIMEOUT = 13,
		CURLOPT_CONNECTTIMEOUT = 15,
		CURLOPT_LOW_SPEED_LIMIT = 10009,
		CURLOPT_LOW_SPEED_TIME = 10010,
		CURLOPT_USERAGENT = 10018,
		CURLOPT_RANGE = 10007,
		CURLOPT_RESUME_FROM = 10019
	}

	private enum CURLINFO
	{
		CURLINFO_RESPONSE_CODE = 2097154,
		CURLINFO_TOTAL_TIME = 1048590,
		CURLINFO_SPEED_DOWNLOAD = 1048593
	}

	public delegate void DownloadProgressCallback(string taskId, double progress, double speed, double totalSize, double downloadedSize);

	public delegate void DownloadCompleteCallback(string taskId, bool success, string errorMessage);

	public delegate void DownloadStartCallback(string taskId);

	public delegate void DownloadStopCallback(string taskId);

	private delegate int WriteCallback(nint ptr, uint size, uint nmemb, nint userData);

	private delegate int ProgressCallback(nint clientp, double dltotal, double dlnow, double ultotal, double ulnow);

	private class DownloadTask
	{
		public string TaskId { get; set; }

		public string Url { get; set; }

		public string FilePath { get; set; }

		public nint EasyHandle { get; set; }

		public FileStream FileStream { get; set; }

		public long ResumeOffset { get; set; }

		public DateTime StartTime { get; set; }

		public bool IsPaused { get; set; }

		public DownloadProgressCallback ProgressCallback { get; set; }

		public DownloadCompleteCallback CompleteCallback { get; set; }

		public DownloadStartCallback StartCallback { get; set; }

		public DownloadStopCallback StopCallback { get; set; }
	}

	private const int CURLMSG_DONE = 1;

	private const int CURLM_OK = 0;

	private const int CURLM_CALL_MULTI_PERFORM = -1;

	private nint multiHandle;

	private Dictionary<string, DownloadTask> tasks;

	private bool isRunning;

	private Thread workerThread;

	private long fileSizeTotal;

	private long fileSizeTotal_real = 3127536424L;

	[DllImport("libcurl-x64.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
	private static extern nint curl_global_init(int flags);

	[DllImport("libcurl-x64.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern void curl_global_cleanup();

	[DllImport("libcurl-x64.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern nint curl_multi_init();

	[DllImport("libcurl-x64.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int curl_multi_cleanup(nint multiHandle);

	[DllImport("libcurl-x64.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int curl_multi_add_handle(nint multiHandle, nint easyHandle);

	[DllImport("libcurl-x64.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int curl_multi_remove_handle(nint multiHandle, nint easyHandle);

	[DllImport("libcurl-x64.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int curl_multi_perform(nint multiHandle, out int runningHandles);

	[DllImport("libcurl-x64.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int curl_multi_info_read(nint multiHandle, out int messagesInQueue);

	[DllImport("libcurl-x64.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern nint curl_easy_init();

	[DllImport("libcurl-x64.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern void curl_easy_cleanup(nint easyHandle);

	[DllImport("libcurl-x64.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
	private static extern int curl_easy_setopt(nint easyHandle, CURLoption option, string value);

	[DllImport("libcurl-x64.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int curl_easy_setopt(nint easyHandle, CURLoption option, long value);

	[DllImport("libcurl-x64.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int curl_easy_setopt(nint easyHandle, CURLoption option, nint value);

	[DllImport("libcurl-x64.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int curl_easy_setopt(nint easyHandle, CURLoption option, WriteCallback callback);

	[DllImport("libcurl-x64.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int curl_easy_setopt(nint easyHandle, CURLoption option, ProgressCallback callback);

	[DllImport("libcurl-x64.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int curl_easy_getinfo(nint easyHandle, CURLINFO info, out double value);

	[DllImport("libcurl-x64.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int curl_easy_getinfo_string(nint easyHandle, CURLINFO info, out string value);

	public CurlMultiDownloader()
	{
		curl_global_init(0);
		multiHandle = curl_multi_init();
		tasks = new Dictionary<string, DownloadTask>();
		isRunning = false;
		AppDomain.CurrentDomain.ProcessExit += (object? sender, EventArgs e) =>
		{
			StopAllDownloads();
			curl_multi_cleanup(multiHandle);
			curl_global_cleanup();
		};
	}

	public string AddDownloadTask(string url, string filePath, long resumeOffset = 0L)
	{
		string text = Guid.NewGuid().ToString();
		DownloadTask value = new DownloadTask
		{
			TaskId = text,
			Url = url,
			FilePath = filePath,
			ResumeOffset = resumeOffset,
			IsPaused = false
		};
		tasks[text] = value;
		return text;
	}

	public void SetTaskCallbacks(string taskId, DownloadProgressCallback progressCallback, DownloadCompleteCallback completeCallback, DownloadStartCallback startCallback, DownloadStopCallback stopCallback)
	{
		if (tasks.TryGetValue(taskId, out var value))
		{
			value.ProgressCallback = progressCallback;
			value.CompleteCallback = completeCallback;
			value.StartCallback = startCallback;
			value.StopCallback = stopCallback;
		}
	}

	public bool StartDownloadTask(string taskId)
	{
		if (!tasks.TryGetValue(taskId, out var value))
		{
			return false;
		}
		if (value.EasyHandle != IntPtr.Zero)
		{
			return true;
		}
		try
		{
			value.EasyHandle = curl_easy_init();
			if (value.EasyHandle == IntPtr.Zero)
			{
				return false;
			}
			curl_easy_setopt(value.EasyHandle, CURLoption.CURLOPT_URL, value.Url);
			string directoryName = Path.GetDirectoryName(value.FilePath);
			if (!Directory.Exists(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			if (value.ResumeOffset > 0)
			{
				curl_easy_setopt(value.EasyHandle, CURLoption.CURLOPT_RESUME_FROM, value.ResumeOffset);
				value.FileStream = new FileStream(value.FilePath, FileMode.Append, FileAccess.Write, FileShare.None);
			}
			else
			{
				value.FileStream = new FileStream(value.FilePath, FileMode.Create, FileAccess.Write, FileShare.None);
			}
			WriteCallback callback = (nint ptr, uint size, uint nmemb, nint userData) =>
			{
				if (userData == IntPtr.Zero)
				{
					return 0;
				}
				DownloadTask downloadTask = (DownloadTask)GCHandle.FromIntPtr(userData).Target;
				if (downloadTask.IsPaused)
				{
					return 0;
				}
				uint num = size * nmemb;
				if (num == 0)
				{
					return 0;
				}
				try
				{
					if (downloadTask.FileStream != null)
					{
						byte[] array = new byte[num];
						Marshal.Copy(ptr, array, 0, (int)num);
						BluepochLogger.InfoLog($"写入文件: {downloadTask.FilePath}, 字节数: {num}");
						fileSizeTotal += num;
						downloadTask.FileStream.Write(array, 0, Convert.ToInt32(num));
						downloadTask.FileStream.Flush();
					}
					if (fileSizeTotal == fileSizeTotal_real)
					{
						BluepochLogger.InfoLog("下载完成----------》");
					}
					return Convert.ToInt32(num);
				}
				catch (Exception ex2)
				{
					BluepochLogger.ErrorLog("写入文件时出错: " + ex2.Message);
					return 0;
				}
			};
			ProgressCallback callback2 = (nint clientp, double dltotal, double dlnow, double ultotal, double ulnow) =>
			{
				if (clientp == IntPtr.Zero)
				{
					return 0;
				}
				DownloadTask downloadTask = (DownloadTask)GCHandle.FromIntPtr(clientp).Target;
				if (downloadTask.IsPaused)
				{
					return 0;
				}
				if (downloadTask.ProgressCallback != null)
				{
					double num = ((dltotal > 0.0) ? ((dlnow + (double)downloadTask.ResumeOffset) / (dltotal + (double)downloadTask.ResumeOffset) * 100.0) : 0.0);
					double num2 = dltotal + (double)downloadTask.ResumeOffset;
					double num3 = dlnow + (double)downloadTask.ResumeOffset;
					double totalSeconds = (DateTime.Now - downloadTask.StartTime).TotalSeconds;
					double num4 = ((totalSeconds > 0.0) ? (num3 / totalSeconds) : 0.0);
					BluepochLogger.ErrorLog($"下载百分比: {num}");
					BluepochLogger.ErrorLog($"下载大小: {num3}");
					BluepochLogger.ErrorLog($"下载总大小: {num2}");
					BluepochLogger.ErrorLog($"下载剩余时间: {totalSeconds}");
					BluepochLogger.ErrorLog($"下载速度: {num4}");
					downloadTask.ProgressCallback(downloadTask.TaskId, num, num4, num2, num3);
				}
				return 0;
			};
			nint value2 = GCHandle.ToIntPtr(GCHandle.Alloc(value));
			curl_easy_setopt(value.EasyHandle, CURLoption.CURLOPT_WRITEFUNCTION, callback);
			curl_easy_setopt(value.EasyHandle, CURLoption.CURLOPT_WRITEDATA, value2);
			curl_easy_setopt(value.EasyHandle, CURLoption.CURLOPT_NOPROGRESS, 0);
			curl_easy_setopt(value.EasyHandle, CURLoption.CURLOPT_PROGRESSFUNCTION, callback2);
			curl_easy_setopt(value.EasyHandle, CURLoption.CURLOPT_PROGRESSDATA, value2);
			curl_easy_setopt(value.EasyHandle, CURLoption.CURLOPT_FOLLOWLOCATION, 1);
			curl_easy_setopt(value.EasyHandle, CURLoption.CURLOPT_MAXREDIRS, 5);
			curl_easy_setopt(value.EasyHandle, CURLoption.CURLOPT_SSL_VERIFYPEER, 0);
			curl_easy_setopt(value.EasyHandle, CURLoption.CURLOPT_SSL_VERIFYHOST, 0);
			curl_easy_setopt(value.EasyHandle, CURLoption.CURLOPT_TIMEOUT, 300);
			curl_easy_setopt(value.EasyHandle, CURLoption.CURLOPT_USERAGENT, "Mozilla/5.0");
			curl_multi_add_handle(multiHandle, value.EasyHandle);
			value.StartTime = DateTime.Now;
			value.StartCallback?.Invoke(value.TaskId);
			if (!isRunning)
			{
				isRunning = true;
				workerThread = new Thread(RunDownloadLoop);
				workerThread.IsBackground = true;
				workerThread.Start();
			}
			return true;
		}
		catch (Exception)
		{
			if (value.EasyHandle != IntPtr.Zero)
			{
				curl_easy_cleanup(value.EasyHandle);
				value.EasyHandle = IntPtr.Zero;
			}
			value.FileStream?.Close();
			value.FileStream = null;
			return false;
		}
	}

	public void PauseDownloadTask(string taskId)
	{
		if (tasks.TryGetValue(taskId, out var value))
		{
			value.IsPaused = true;
			value.StopCallback?.Invoke(value.TaskId);
		}
	}

	public void StopDownloadTask(string taskId)
	{
		if (tasks.TryGetValue(taskId, out var value))
		{
			if (value.EasyHandle != IntPtr.Zero)
			{
				curl_multi_remove_handle(multiHandle, value.EasyHandle);
				curl_easy_cleanup(value.EasyHandle);
				value.EasyHandle = IntPtr.Zero;
			}
			value.FileStream?.Close();
			value.FileStream = null;
			value.StopCallback?.Invoke(value.TaskId);
		}
	}

	public void StopAllDownloads()
	{
		isRunning = false;
		foreach (string item in new List<string>(tasks.Keys))
		{
			StopDownloadTask(item);
		}
		workerThread?.Join(1000);
	}

	private void RunDownloadLoop()
	{
		while (isRunning)
		{
			int num;
			int runningHandles;
			do
			{
				num = curl_multi_perform(multiHandle, out runningHandles);
			}
			while (num == -1);
			CheckCompletedTasks();
			if (runningHandles == 0 && !CheckAnyTaskActive())
			{
				isRunning = false;
				break;
			}
			Thread.Sleep(100);
		}
	}

	private bool CheckAnyTaskActive()
	{
		foreach (DownloadTask value in tasks.Values)
		{
			if (value.EasyHandle != IntPtr.Zero && !value.IsPaused)
			{
				return true;
			}
		}
		return false;
	}

	private void CheckCompletedTasks()
	{
		int messagesInQueue;
		while (curl_multi_info_read(multiHandle, out messagesInQueue) != IntPtr.Zero)
		{
		}
	}
}
