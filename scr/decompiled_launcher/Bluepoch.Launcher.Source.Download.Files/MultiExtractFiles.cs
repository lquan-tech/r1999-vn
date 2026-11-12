using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Bluepoch.Common.Util;
using Bluepoch.Launcher.App;
using Bluepoch.Launcher.Constants;
using Bluepoch.Launcher.Source.Download.Data;
using Bluepoch.Launcher.Source.Game;
using Bluepoch.Launcher.Utils;
using ThinkingSDK.PC;

namespace Bluepoch.Launcher.Source.Download.Files;

internal class MultiExtractFiles : ExtractInterface
{
	private int checkedcount;

	private List<MultiFilesInfo> verifyErrorFile = new List<MultiFilesInfo>();

	private readonly object _errorFileLock = new object();

	public override async void StartExtract(GameVersionInfo gameVersionInfo)
	{
		checkedcount = 0;
		verifyErrorFile.Clear();
		IsWorking = true;
		string jsonFilePath = GameCommon.GetGameJsonFilePath(gameVersionInfo.currentPackageInfo.packageJson);
		MultiFilesDownloadDirInfo filesFromJson = await GameCommon.GetDownloadInfoFromJsonFileAsync(jsonFilePath);
		if (filesFromJson == null || filesFromJson.smallFilesInfo == null || filesFromJson.smallFilesInfo.Count == 0)
		{
			BluepochLogger.ErrorLog("get filesFromJson:" + jsonFilePath + " fail!!!");
			multiUnzipErrorCallback?.Invoke("MultiExtractFiles", "MultiExtractFiles.Error_DownloadGameJson");
			IsWorking = false;
		}
		else
		{
			multiUnzipStartCallback?.Invoke();
			Task.Run(() =>
			{
				VerifyFilesIntegrity(filesFromJson);
			});
		}
	}

	private void VerifyFilesIntegrity(MultiFilesDownloadDirInfo filesFromJson)
	{
		try
		{
			int totalFileCount = filesFromJson.smallFilesInfo.Count;
			BluepochLogger.InfoLog($"开始文件验证，总文件数：{totalFileCount}，线程数：{maxConcurrency}");
			List<List<MultiFilesInfo>> fileBatches = SplitFileList(filesFromJson.smallFilesInfo, maxConcurrency);
			List<Task> list = new List<Task>();
			for (int i = 0; i < maxConcurrency; i++)
			{
				int batchIndex = i;
				list.Add(Task.Run(() =>
				{
					VerifyFileBatch(fileBatches[batchIndex], totalFileCount);
				}));
			}
			Task.WaitAll(list.ToArray());
			ProcessVerificationResult(totalFileCount);
		}
		catch (Exception ex)
		{
			BluepochLogger.ErrorLog("VerifyFilesIntegrity Exception: " + ex.Message);
			HandleVerificationError("MultiExtractFiles.Error_VerifyUnknow");
		}
	}

	private List<List<MultiFilesInfo>> SplitFileList(List<MultiFilesInfo> fileList, int batchCount)
	{
		List<List<MultiFilesInfo>> list = new List<List<MultiFilesInfo>>();
		for (int i = 0; i < batchCount; i++)
		{
			list.Add(new List<MultiFilesInfo>());
		}
		for (int j = 0; j < fileList.Count; j++)
		{
			int index = j % batchCount;
			list[index].Add(fileList[j]);
		}
		return list;
	}

	private void VerifyFileBatch(List<MultiFilesInfo> fileBatch, int totalFileCount)
	{
		foreach (MultiFilesInfo item in fileBatch)
		{
			if (!IsWorking)
			{
				break;
			}
			if (!item.forceType)
			{
				int num = Interlocked.Increment(ref checkedcount);
				if (num % 10 == 0 || num == totalFileCount)
				{
					double progress = (double)num / (double)totalFileCount;
					LauncherApplication.GetInstance().InvokeOnUiThread(() =>
					{
						multiUnZipProgressCallback?.Invoke(Math.Round(progress * 100.0, 2));
					});
				}
				continue;
			}
			string text = GameCommon.GetGameExeInstallDirByDefault() + item.dest;
			if (File.Exists(text))
			{
				if (FileUtil.GetFileMD5(text).Equals(item.md5))
				{
					int num2 = Interlocked.Increment(ref checkedcount);
					if (num2 % 10 == 0 || num2 == totalFileCount)
					{
						double progress2 = (double)num2 / (double)totalFileCount;
						LauncherApplication.GetInstance().InvokeOnUiThread(() =>
						{
							multiUnZipProgressCallback?.Invoke(Math.Round(progress2 * 100.0, 2));
						});
					}
				}
				else
				{
					BluepochLogger.ErrorLog("文件MD5不匹配，文件路径：" + text + "，期望MD5：" + item.md5);
					lock (_errorFileLock)
					{
						verifyErrorFile.Add(item);
					}
				}
			}
			else
			{
				BluepochLogger.ErrorLog("文件缺失，文件路径：" + text);
				lock (_errorFileLock)
				{
					verifyErrorFile.Add(item);
				}
			}
		}
	}

	private void ProcessVerificationResult(int totalFileCount)
	{
		IsWorking = false;
		if (verifyErrorFile.Count > 0)
		{
			using (DownloadDbContext downloadDbContext = new DownloadDbContext(GameCommon.GetDbFilePath(GamePlugin.GetInstance().GetGameVersionInfo().currentPackageInfo.packageJson)))
			{
				downloadDbContext.Database.EnsureCreated();
				downloadDbContext.RemoveRange(verifyErrorFile);
				downloadDbContext.SaveChanges();
			}
			HandleVerificationError("MultiExtractFiles.Error_IllegalFiles");
		}
		else if (checkedcount == totalFileCount)
		{
			LauncherApplication.GetInstance().InvokeOnUiThread(() =>
			{
				multiUnZipFinishCallback?.Invoke();
			});
		}
		else
		{
			HandleVerificationError("MultiExtractFiles.Error_VerifyUnknow");
		}
	}

	private void HandleVerificationError(string errorMsg)
	{
		LauncherApplication.GetInstance().InvokeOnUiThread(() =>
		{
			Dictionary<string, object> properties = new Dictionary<string, object>
			{
				[TrackEvent.EVENT_Result_Msg] = errorMsg,
				[TrackEvent.Exception_Location] = $"VerifyFilesIntegrity-{verifyErrorFile[0].dest}，Count:{verifyErrorFile.Count}"
			};
			ThinkingPCSDKManager.getInstance().TrackEvent(TrackEvent.EVENT_Exception, properties);
			multiUnzipErrorCallback?.Invoke((verifyErrorFile.Count > 0) ? verifyErrorFile[0].dest : "MultiExtractFiles", errorMsg);
		});
	}

	public void StopVerify()
	{
		IsWorking = false;
	}

	public override void StopExtract()
	{
	}
}
