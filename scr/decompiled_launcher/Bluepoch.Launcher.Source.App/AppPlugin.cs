using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Bluepoch.Common.Bean;
using Bluepoch.Common.Download;
using Bluepoch.Common.Http;
using Bluepoch.Common.Util;
using Bluepoch.Launcher.App;
using Bluepoch.Launcher.Config;
using Bluepoch.Launcher.Constants;
using Bluepoch.Launcher.Properties;
using Bluepoch.Launcher.Source.Bean.Request;
using Bluepoch.Launcher.Source.Bean.Respond;
using Bluepoch.Launcher.Source.Config;
using Bluepoch.Launcher.Source.Utils;
using Bluepoch.Launcher.Utils;
using Newtonsoft.Json;

namespace Bluepoch.Launcher.Source.App;

internal class AppPlugin
{
	public delegate void AppUpdateDelegate(bool needToUnpdate, AppVersionInfo appVersionInfo, ApiResult<string> apiResult);

	public delegate void AppUpdateFromSettingDelegate(bool needToUnpdate, AppVersionInfo appVersionInfo, int errorCode = 200);

	public delegate void VideoUpdateDelegate(PosterVideoInfo posterVideoInfo);

	private static AppPlugin _instance = null;

	private int videoDownloadErrorCount;

	private static int MaxVideoDownloadErrorCount = 5;

	private AppUpdateDelegate updateDelegate;

	private AppUpdateFromSettingDelegate appUpdateFromSettingDelegate;

	private VideoUpdateDelegate videoUpdateDelegate;

	private bool isDownloading;

	public AppVersionInfo appVersionInfo { get; private set; }

	public static AppPlugin GetInstance()
	{
		if (_instance == null)
		{
			_instance = new AppPlugin();
		}
		return _instance;
	}

	private AppPlugin()
	{
	}

	public void SetAppUpdateDelegate(AppUpdateDelegate appUpdateDelegate)
	{
		updateDelegate = appUpdateDelegate;
	}

	public void SetAppUpdateFromSettingDelegate(AppUpdateFromSettingDelegate appUpdateFromSettingDelegate)
	{
		this.appUpdateFromSettingDelegate = appUpdateFromSettingDelegate;
	}

	public void SetVideoUpdateDelegate(VideoUpdateDelegate videoUpdateDelegate)
	{
		this.videoUpdateDelegate = videoUpdateDelegate;
	}

	public void CheckUpdateInServerFromSetting()
	{
		string postData = JsonConvert.SerializeObject(new AppVersionRequestInfo(LauncherUtils.GetExeVersion()));
		HttpManager httpManager = new HttpManager();
		string gamesdk_host = UrlManager.GetInstance().GetGamesdk_host("/pc/launcher/version");
		Dictionary<string, string> dictionary = new Dictionary<string, string> { 
		{
			"lang",
			LanguageHeaderString.LanguageHeaders[AppSettings.Instance.Language_index]
		} };
		HttpManager.AsyRequetCallback callback = (object userState, string respStr, int statusCode, WebException ex) =>
		{
			BluepochLogger.DebugLog($"statusCode :{statusCode}");
			if (ex == null && statusCode == 200)
			{
				BluepochLogger.DebugLog("respStr : " + respStr);
				ApiResult<AppVersionInfo> apiResult = JsonConvert.DeserializeObject<ApiResult<AppVersionInfo>>(respStr);
				if (LauncherUtils.VersionContrast(apiResult?.data.latestVersion, LauncherUtils.GetExeVersion()))
				{
					BluepochLogger.InfoLog("Need to Update : " + apiResult.data.latestVersion);
					appVersionInfo = apiResult.data;
					appUpdateFromSettingDelegate?.Invoke(needToUnpdate: true, apiResult.data);
				}
				else
				{
					appUpdateFromSettingDelegate?.Invoke(needToUnpdate: false, null);
				}
			}
			else
			{
				BluepochLogger.DebugLog("ex : " + ex?.Message);
			}
		};
		Dictionary<string, string> headers = dictionary;
		httpManager.PostAsync(gamesdk_host, postData, callback, "application/json", null, null, headers, isCodeContent: true);
	}

	public void CheckUpdateInServer()
	{
		GetVideoFromServer();
		if (SettingConfigManager.GetInstance().GetValueFromConfig("Launcher", "stop_to_update_e6492ceabf0b22bd4dbc15f67c090a4c") == "true")
		{
			updateDelegate?.Invoke(needToUnpdate: false, null, new ApiResult<string>(200, ""));
		}
		else
		{
			CheckUpdateInServerWithRetry(0);
		}
	}

	private void CheckUpdateInServerWithRetry(int retryCount)
	{
		string postData = JsonConvert.SerializeObject(new AppVersionRequestInfo(LauncherUtils.GetExeVersion()));
		HttpManager httpManager = new HttpManager();
		string gamesdk_host = UrlManager.GetInstance().GetGamesdk_host("/pc/launcher/version");
		Dictionary<string, string> dictionary = new Dictionary<string, string> { 
		{
			"lang",
			LanguageHeaderString.LanguageHeaders[AppSettings.Instance.Language_index]
		} };
		HttpManager.AsyRequetCallback callback = async (object userState, string respStr, int statusCode, WebException ex) =>
		{
			BluepochLogger.DebugLog($"CheckUpdateInServerWithRetry: statusCode = {statusCode}, retryCount = {retryCount}");
			if (ex == null && statusCode == 200)
			{
				BluepochLogger.DebugLog("CheckUpdateInServerWithRetry: success，respStr = " + respStr);
				try
				{
					ApiResult<AppVersionInfo> apiResult = JsonConvert.DeserializeObject<ApiResult<AppVersionInfo>>(respStr);
					if (LauncherUtils.VersionContrast(apiResult.data.latestVersion, LauncherUtils.GetExeVersion()))
					{
						BluepochLogger.InfoLog("CheckUpdateInServerWithRetry: need to update to version " + apiResult.data.latestVersion);
						appVersionInfo = apiResult.data;
						updateDelegate?.Invoke(needToUnpdate: true, apiResult.data, new ApiResult<string>(statusCode, ""));
					}
					else
					{
						BluepochLogger.InfoLog("CheckUpdateInServerWithRetry: current is the latest version");
						updateDelegate?.Invoke(needToUnpdate: false, null, new ApiResult<string>(statusCode, ""));
					}
					return;
				}
				catch (Exception ex2)
				{
					BluepochLogger.ErrorLog("CheckUpdateInServerWithRetry: parse response failed, error: " + ex2.Message);
					if (retryCount < 3)
					{
						await RetryRequest(retryCount, 3, 1000);
					}
					else
					{
						BluepochLogger.ErrorLog("CheckUpdateInServerWithRetry: reach maximum retry count, parse response failed");
						updateDelegate?.Invoke(needToUnpdate: true, null, new ApiResult<string>(statusCode, ex2.Message));
					}
					return;
				}
			}
			string text = ((ex != null) ? ex.Message : $"status code error: {statusCode}");
			BluepochLogger.DebugLog("CheckUpdateInServerWithRetry: request failed, error: " + text);
			if (retryCount < 3)
			{
				UrlManager.GetInstance().SwitchToAlternateHost();
				await RetryRequest(retryCount, 3, 1000);
			}
			else
			{
				BluepochLogger.ErrorLog("CheckUpdateInServerWithRetry: reach maximum retry count, request failed");
				updateDelegate?.Invoke(needToUnpdate: true, null, new ApiResult<string>(statusCode, text));
			}
		};
		Dictionary<string, string> headers = dictionary;
		httpManager.PostAsync(gamesdk_host, postData, callback, "application/json", null, null, headers, isCodeContent: true);
	}

	private async Task RetryRequest(int retryCount, int maxRetries, int initialRetryDelay)
	{
		retryCount++;
		int num = (int)((double)initialRetryDelay * Math.Pow(2.0, retryCount - 1));
		BluepochLogger.InfoLog($"CheckUpdateInServerWithRetry: retrying, about to retry {retryCount} time, delay {num} milliseconds");
		await Task.Delay(num);
		CheckUpdateInServerWithRetry(retryCount);
	}

	private void GetVideoFromServer()
	{
		if (isDownloading || videoDownloadErrorCount > MaxVideoDownloadErrorCount)
		{
			return;
		}
		HttpManager httpManager = new HttpManager();
		string actvity_host = UrlManager.GetInstance().GetActvity_host("/activity/pc/launcher/base-info");
		Dictionary<string, string> dictionary = new Dictionary<string, string> { 
		{
			"lang",
			LanguageHeaderString.LanguageHeaders[AppSettings.Instance.Language_index]
		} };
		string postData = JsonConvert.SerializeObject(new AppBaseInfoRequestInfo());
		HttpManager.AsyRequetCallback callback = (object userState, string respStr, int statusCode, WebException ex) =>
		{
			BluepochLogger.DebugLog($"statusCode :{statusCode}");
			if (ex == null && statusCode == 200)
			{
				BluepochLogger.DebugLog("respStr : " + respStr);
				ApiResult<PosterVideoInfo> apiResult = JsonConvert.DeserializeObject<ApiResult<PosterVideoInfo>>(respStr);
				if (apiResult != null && apiResult.code == 200 && apiResult?.data != null && !string.IsNullOrEmpty(apiResult?.data.versionVideoUrl))
				{
					HandlePosterVideoInfo(apiResult.data);
				}
				else
				{
					BluepochLogger.DebugLog("NotificationRespondInfo get fail!");
				}
			}
			else
			{
				BluepochLogger.DebugLog("ex : " + ex?.Message);
			}
		};
		Dictionary<string, string> headers = dictionary;
		httpManager.PostAsync(actvity_host, postData, callback, "application/json", null, null, headers);
	}

	private string GetDefaultVideoPath()
	{
		string text = "";
		int num = Convert.ToInt32(Resources.GameId ?? "-1");
		switch (num)
		{
		case 60001:
			text = Path.Combine(AppDir.GetAppWebDir(), "assets/en/default.mp4");
			break;
		case 70001:
			text = Path.Combine(AppDir.GetAppWebDir(), "assets/ja/default.mp4");
			break;
		case 90001:
			text = Path.Combine(AppDir.GetAppWebDir(), "assets/ko/default.mp4");
			break;
		default:
			BluepochLogger.InfoLog($"launcherType  :  {num}");
			break;
		case 50001:
			break;
		}
		BluepochLogger.InfoLog("defaultVideoPath  :  " + text);
		return text;
	}

	private string GetShowingVideoPath()
	{
		string bgVideoName = AppSettings.Instance.BgVideoName;
		if (!string.IsNullOrEmpty(bgVideoName))
		{
			string text = Path.Combine(AppDir.GetAppVideoDir(), bgVideoName);
			if (File.Exists(text))
			{
				return text;
			}
		}
		return GetDefaultVideoPath();
	}

	private void HandlePosterVideoInfo(PosterVideoInfo posterVideoInfo)
	{
		if (!posterVideoInfo.versionVideoUrl.EndsWith(".mp4"))
		{
			return;
		}
		string videoFilePathByUrl = LauncherUtils.GetVideoFilePathByUrl(posterVideoInfo.versionVideoUrl);
		string fileNameFromUrl = LauncherUtils.GetFileNameFromUrl(posterVideoInfo.versionVideoUrl);
		string text = fileNameFromUrl.Split('_')[^1].Split('.')[0];
		string showingVideoPath = GetShowingVideoPath();
		string fileMD = FileUtil.GetFileMD5(showingVideoPath);
		BluepochLogger.InfoLog("Showing video path is " + showingVideoPath);
		BluepochLogger.InfoLog("Showing video md5 is " + fileMD);
		BluepochLogger.InfoLog("Server video md5 is " + text);
		if (text.Equals(fileMD))
		{
			BluepochLogger.InfoLog("Showing video md5 is the same , do not need to download");
			return;
		}
		if (!File.Exists(videoFilePathByUrl))
		{
			DownloadVideo(posterVideoInfo);
			return;
		}
		isDownloading = false;
		string bgVideoName = AppSettings.Instance.BgVideoName;
		if (!fileNameFromUrl.Equals(bgVideoName))
		{
			AppSettings.Instance.BgVideoName = fileNameFromUrl;
			AppSettings.Instance.Save();
			videoUpdateDelegate?.Invoke(posterVideoInfo);
		}
		BluepochLogger.InfoLog("videoFileName is the same ， do not notify update");
	}

	private void DownloadVideo(PosterVideoInfo posterVideoInfo)
	{
		FileDownloader fileDownloader = new FileDownloader(posterVideoInfo.versionVideoUrl, 0L);
		isDownloading = true;
		Task.Run(async () =>
		{
			try
			{
				await fileDownloader.DownloadFileAsync(LauncherUtils.GetVideoTempFilePathByUrl(posterVideoInfo.versionVideoUrl), null, null, null, null, (string id, string path) =>
				{
					FileCompletedCallback(posterVideoInfo, id, path);
				}, (string id, string url, Exception msg) =>
				{
					LauncherApplication.GetInstance().InvokeOnUiThread(() =>
					{
						isDownloading = false;
					});
				});
			}
			catch (Exception ex)
			{
				isDownloading = false;
				BluepochLogger.InfoLog("DownloadVideo Exception : " + ex?.Message);
			}
		});
	}

	private void FileCompletedCallback(PosterVideoInfo posterVideoInfo, string downloadTaskId, string savePath)
	{
		try
		{
			string text = Path.GetFileName(savePath).Split('_')[^1].Split('.')[0];
			BluepochLogger.InfoLog("DownloadVideo md5 : " + text);
			string fileMD = FileUtil.GetFileMD5(savePath);
			if (text.Equals(fileMD))
			{
				RenameSavePathFileToRealFile(LauncherUtils.GetVideoFilePathByUrl(posterVideoInfo.versionVideoUrl), savePath);
				videoDownloadErrorCount = 0;
				LauncherApplication.GetInstance().InvokeOnUiThread(() =>
				{
					isDownloading = false;
					string fileNameFromUrl = LauncherUtils.GetFileNameFromUrl(posterVideoInfo.versionVideoUrl);
					AppSettings.Instance.BgVideoName = fileNameFromUrl;
					AppSettings.Instance.Save();
					videoUpdateDelegate?.Invoke(posterVideoInfo);
				});
			}
			else
			{
				videoDownloadErrorCount++;
				BluepochLogger.InfoLog($"DownloadVideo md5 not match , delete file , DownloadErrorCount:{videoDownloadErrorCount}");
				isDownloading = false;
				if (File.Exists(savePath))
				{
					File.Delete(savePath);
				}
			}
		}
		catch (Exception ex)
		{
			videoDownloadErrorCount++;
			BluepochLogger.InfoLog("DownloadVideo Exception : " + ex?.Message);
			isDownloading = false;
			if (File.Exists(savePath))
			{
				File.Delete(savePath);
			}
		}
	}

	private bool RenameSavePathFileToRealFile(string realFile, string savePath)
	{
		try
		{
			File.Move(savePath, realFile);
			return true;
		}
		catch (Exception ex)
		{
			BluepochLogger.ErrorLog("RenameSavePathFileToRealFile  Temp File Exception: " + ex.Message);
			return false;
		}
	}
}
