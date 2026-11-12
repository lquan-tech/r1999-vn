using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Bluepoch.Common.Util;
using Bluepoch.Launcher.Config;
using Bluepoch.Launcher.Source.Utils;

namespace Bluepoch.Launcher.Utils;

internal static class FileUtil
{
	public class DeleteProgress
	{
		public string CurrentItem { get; set; }

		public int TotalItems { get; set; }

		public bool IsDirectory { get; set; }

		public double Processed { get; set; }
	}

	public static string GetFileMD5(string filePath)
	{
		using MD5 mD = MD5.Create();
		using FileStream inputStream = File.OpenRead(filePath);
		return BitConverter.ToString(mD.ComputeHash(inputStream)).Replace("-", "").ToLowerInvariant();
	}

	public static string GetAppDirectory()
	{
		return AppDomain.CurrentDomain.BaseDirectory;
	}

	public static bool RemoveFile(string filePath)
	{
		if (!File.Exists(filePath))
		{
			return true;
		}
		try
		{
			File.Delete(filePath);
			BluepochLogger.InfoLog("RemoveFile delete file : " + filePath);
			return true;
		}
		catch (Exception ex)
		{
			BluepochLogger.ErrorLog("RemoveFile delete file error : " + ex.Message);
			return false;
		}
	}

	public static bool RemoveFilesBySuffix(string dir, string fileSuffix)
	{
		try
		{
			if (!Directory.Exists(dir))
			{
				BluepochLogger.WarningLog("RemoveFilesBySuffix error : dir not exist " + dir);
				return false;
			}
			foreach (string item in Directory.EnumerateFiles(dir))
			{
				if (item.EndsWith(fileSuffix, StringComparison.OrdinalIgnoreCase))
				{
					try
					{
						File.Delete(item);
						BluepochLogger.InfoLog("RemoveFilesBySuffix delete file : " + item);
					}
					catch (Exception ex)
					{
						BluepochLogger.ErrorLog("RemoveFilesBySuffix delete file error : " + ex.Message);
					}
				}
			}
			return true;
		}
		catch (Exception ex2)
		{
			BluepochLogger.ErrorLog("RemoveFilesBySuffix error : " + ex2.Message);
			return false;
		}
	}

	public static string GetAppDataDirPath(string dirName)
	{
		return Path.Combine(Application.StartupPath, AppSettings.Instance.AppDataDir, dirName);
	}

	public static long ComputerDirRootRoomSize(string dirPath)
	{
		try
		{
			DriveInfo driveInfo = new DriveInfo(Path.GetPathRoot(dirPath));
			if (!driveInfo.IsReady)
			{
				return -2L;
			}
			_ = driveInfo.TotalSize;
			return driveInfo.AvailableFreeSpace;
		}
		catch (Exception ex)
		{
			BluepochLogger.ErrorLog("获取磁盘空间信息失败: " + ex.Message);
			return 0L;
		}
	}

	public static string ReadGameVerionInGameExeDir(string dir)
	{
		try
		{
			string path = Path.Combine(dir, "reverse1999_version.ini");
			if (!File.Exists(path))
			{
				BluepochLogger.WarningLog("ReadGameVerionInGameExeDir version.txt not exist in dir: " + dir);
				return "";
			}
			return File.ReadAllText(path).Trim();
		}
		catch (Exception ex)
		{
			BluepochLogger.ErrorLog("ReadGameVerionInGameExeDir Exception: " + ex.Message);
			return "";
		}
	}

	public static string ComputerDirRoomSize(string dirPath)
	{
		try
		{
			if (!Directory.Exists(dirPath))
			{
				return "路径不存在";
			}
			DriveInfo driveInfo = new DriveInfo(Path.GetPathRoot(dirPath));
			if (!driveInfo.IsReady)
			{
				return "驱动器未就绪";
			}
			_ = driveInfo.TotalSize;
			return LauncherUtils.ConvertBytesToReadable(driveInfo.AvailableFreeSpace);
		}
		catch (Exception ex)
		{
			BluepochLogger.ErrorLog("获取磁盘空间信息失败: " + ex.Message);
			return "获取磁盘空间信息失败";
		}
	}

	public static async Task DeleteDirectoryOptimized(string path, IProgress<DeleteProgress> progress)
	{
		if (!Directory.Exists(path))
		{
			throw new DirectoryNotFoundException("Directory not found: " + path);
		}
		(List<string>, List<string>) tuple = await Task.Run(() =>
		{
			List<string> item3 = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).ToList();
			List<string> item4 = Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories).Reverse().ToList();
			return (f: item3, d: item4);
		});
		List<string> item = tuple.Item1;
		List<string> item2 = tuple.Item2;
		int num = item.Count + item2.Count + 1;
		BluepochLogger.DebugLogAsyn($"totalItems : {num}");
		int deleteItems = 0;
		foreach (IEnumerable<string> item5 in EnumerableExtensions.Batch(item, 100))
		{
			Parallel.ForEach(item5, (string file) =>
			{
				if (File.Exists(file))
				{
					File.Delete(file);
					deleteItems++;
				}
			});
			progress.Report(new DeleteProgress
			{
				Processed = Math.Round((double)deleteItems * 100.0 / (double)num, 2),
				TotalItems = num,
				CurrentItem = $"已批量删除{deleteItems}个文件"
			});
		}
		foreach (string item6 in item2)
		{
			DeleteDirectoryWithRetry(item6);
			deleteItems++;
			progress.Report(new DeleteProgress
			{
				Processed = Math.Round((double)deleteItems * 100.0 / (double)num, 2),
				TotalItems = num,
				CurrentItem = Path.GetFileName(item6)
			});
		}
		Directory.Delete(path);
		progress.Report(new DeleteProgress
		{
			Processed = 100.0,
			TotalItems = num
		});
	}

	public static IEnumerable<List<T>> Batch<T>(this IEnumerable<T> source, int size)
	{
		List<T> list = null;
		foreach (T item in source)
		{
			if (list == null)
			{
				list = new List<T>(size);
			}
			list.Add(item);
			if (list.Count == size)
			{
				yield return list;
				list = null;
			}
		}
		if (list != null && list.Count > 0)
		{
			yield return list;
		}
	}

	public static bool DeleteFileWithRetry(string path, int maxRetries = 3, int retryInterval = 100)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			BluepochLogger.ErrorLog("DeleteFileWithRetry: path is none");
			return false;
		}
		if (!File.Exists(path))
		{
			BluepochLogger.ErrorLog("DeleteFileWithRetry: !File.Exists: " + path);
			return true;
		}
		for (int i = 0; i < maxRetries; i++)
		{
			try
			{
				FileInfo fileInfo = new FileInfo(path);
				if (fileInfo.IsReadOnly)
				{
					fileInfo.IsReadOnly = false;
				}
				File.Delete(path);
				if (!File.Exists(path))
				{
					BluepochLogger.InfoLog($"DeleteFileWithRetry: success: {path}，retry: {i}");
					return true;
				}
			}
			catch (UnauthorizedAccessException ex)
			{
				BluepochLogger.ErrorLog("DeleteFileWithRetry: 权限不足，无法删除文件: " + path + "，错误: " + ex.Message);
				return false;
			}
			catch (PathTooLongException ex2)
			{
				BluepochLogger.ErrorLog("DeleteFileWithRetry: 文件路径太长，无法删除: " + path + "，错误: " + ex2.Message);
				return false;
			}
			catch (DirectoryNotFoundException ex3)
			{
				BluepochLogger.ErrorLog("DeleteFileWithRetry: 目录不存在，无法删除文件: " + path + "，错误: " + ex3.Message);
				return false;
			}
			catch (ArgumentException ex4)
			{
				BluepochLogger.ErrorLog("DeleteFileWithRetry: 参数错误，无法删除文件: " + path + "，错误: " + ex4.Message);
				return false;
			}
			catch (Exception ex5)
			{
				BluepochLogger.InfoLog($"DeleteFileWithRetry: 尝试删除文件失败，将重试: {path}，错误: {ex5.Message}，重试次数: {i + 1}/{maxRetries}");
				Thread.Sleep((int)((double)retryInterval * Math.Pow(2.0, i)));
			}
		}
		BluepochLogger.ErrorLog("DeleteFileWithRetry: retry finish，can not delete: " + path);
		return false;
	}

	public static void DeleteDirectoryWithRetry(string path, int maxRetries = 3)
	{
		for (int i = 0; i < maxRetries; i++)
		{
			try
			{
				if (!Directory.Exists(path))
				{
					continue;
				}
				Directory.EnumerateFileSystemEntries(path).ToList().ForEach((string p) =>
				{
					if (File.Exists(p))
					{
						File.Delete(p);
					}
					else
					{
						DeleteDirectoryWithRetry(p);
					}
				});
				Directory.Delete(path);
				break;
			}
			catch (Exception ex)
			{
				BluepochLogger.InfoLog("DeleteDirectoryWithRetry--" + path + " Exception : " + ex.Message);
				Thread.Sleep(100);
			}
		}
	}

	public static async Task DeleteDirectoryStreaming(string path)
	{
		foreach (string item in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
		{
			File.Delete(item);
			await Task.Yield();
		}
		Directory.Delete(path, recursive: true);
	}
}
