using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;
using Bluepoch.Common.Bean;
using Bluepoch.Common.Download;
using Bluepoch.Common.Http;
using Bluepoch.Common.Util;
using Bluepoch.Launcher.App;
using Bluepoch.Launcher.Config;
using Bluepoch.Launcher.Constants;
using Bluepoch.Launcher.Source.Bean.Request;
using Bluepoch.Launcher.Source.Bean.Respond;
using Bluepoch.Launcher.Source.Config;
using Bluepoch.Launcher.Source.Ui;
using Newtonsoft.Json;

namespace Bluepoch.Launcher.Source.App;

internal class NotificationPlugin
{
	public delegate void NotificationDelegate(NotificationRespondInfo notificationRespondInfo);

	private static NotificationPlugin _instance;

	private NotificationDelegate notificationDelegate;

	private NotificationRespondInfo notificationRespond;

	private bool isDownloading;

	private NotifyForm newWindow;

	private Timer timer;

	public static NotificationPlugin GetInstance()
	{
		if (_instance == null)
		{
			_instance = new NotificationPlugin();
		}
		return _instance;
	}

	private NotificationPlugin()
	{
	}

	public void SetNotificationDelegate(NotificationDelegate notificationDelegate)
	{
		this.notificationDelegate = notificationDelegate;
	}

	public void processNewMessage()
	{
		if (isDownloading)
		{
			return;
		}
		HttpManager httpManager = new HttpManager();
		string actvity_host = UrlManager.GetInstance().GetActvity_host("/activity/pc/launcher/message");
		Dictionary<string, string> dictionary = new Dictionary<string, string> { 
		{
			"lang",
			LanguageHeaderString.LanguageHeaders[AppSettings.Instance.Language_index]
		} };
		string postData = JsonConvert.SerializeObject(new NotificationRequsetInfo());
		HttpManager.AsyRequetCallback callback = (object userState, string respStr, int statusCode, WebException ex) =>
		{
			BluepochLogger.DebugLog($"statusCode :{statusCode}");
			if (ex == null && statusCode == 200)
			{
				BluepochLogger.DebugLog("respStr : " + respStr);
				ApiResult<NotificationRespondInfo> apiResult = JsonConvert.DeserializeObject<ApiResult<NotificationRespondInfo>>(respStr);
				if (apiResult != null && apiResult.code == 200 && apiResult?.data != null && (apiResult == null || apiResult.data.messageId != 0))
				{
					HandleNotificationRespond(apiResult.data);
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

	private void HandleNotificationRespond(NotificationRespondInfo notificationRespondInfo)
	{
		if (string.IsNullOrEmpty(notificationRespondInfo.bannerUrl))
		{
			return;
		}
		notificationRespond = notificationRespondInfo;
		string imageFilePath = GetImageFilePathByUrl(notificationRespondInfo.bannerUrl);
		if (File.Exists(imageFilePath))
		{
			NotifyUIToShowMessage();
			return;
		}
		FileDownloader fileDownloader = new FileDownloader(notificationRespondInfo.bannerUrl, 0L);
		isDownloading = true;
		Task.Run(async () =>
		{
			try
			{
				await fileDownloader.DownloadFileAsync(imageFilePath, null, null, null, null, (string id, string path) =>
				{
					LauncherApplication.GetInstance().InvokeOnUiThread(() =>
					{
						DownloadCompleteCallback(id, path);
					});
				}, (string id, string url, Exception ex2) =>
				{
					LauncherApplication.GetInstance().InvokeOnUiThread(() =>
					{
						DownloadFailedCallback(id, ex2);
					});
				});
			}
			catch (Exception ex)
			{
				isDownloading = false;
				BluepochLogger.InfoLog("DownloadFileAsync Exception : " + ex.Message);
			}
		});
	}

	private void DownloadCompleteCallback(string downloadTaskId, string savePath, int errorCode = 200, string errorMsg = "")
	{
		isDownloading = false;
		if (errorCode == 200)
		{
			BluepochLogger.InfoLog("Notify Image Download Finish!");
		}
		if (File.Exists(savePath))
		{
			NotifyUIToShowMessage();
		}
		else
		{
			BluepochLogger.InfoLog("Notify Image Download File Not Exist!");
		}
	}

	private void NotifyUIToShowMessage()
	{
		if (!isNotificationsEnabled())
		{
			BluepochLogger.InfoLog("Notifications is disabled!");
			return;
		}
		StartTimeToUpdateNotifications();
		if (notificationRespond.messageId == AppSettings.Instance.Notifications_Id)
		{
			BluepochLogger.InfoLog($"Notifications {notificationRespond.messageId} has been shown!");
			return;
		}
		AppSettings.Instance.Notifications_Id = notificationRespond.messageId;
		AppSettings.Instance.Save();
		notificationDelegate?.Invoke(notificationRespond);
		if (newWindow != null && !newWindow.IsDisposed)
		{
			newWindow.Close();
			newWindow.Dispose();
		}
		newWindow = new NotifyForm(notificationRespond);
		newWindow.Show();
	}

	private void DownloadFailedCallback(string downloadTaskId, Exception ex)
	{
		isDownloading = false;
		BluepochLogger.InfoLog("DownloadFailedCallback Exception : " + ex?.Message);
	}

	public string GetImageFilePathByUrl(string url)
	{
		string fileName = Path.GetFileName(new Uri(url).LocalPath);
		if (string.IsNullOrEmpty(fileName))
		{
			return string.Empty;
		}
		return Path.Combine(AppDir.GetAppImageDir(), fileName);
	}

	public bool isNotificationsEnabled()
	{
		return AppSettings.Instance.NotificationsEnabled;
	}

	public void UpdateNotificationsEnabled(bool enabled)
	{
		AppSettings.Instance.NotificationsEnabled = enabled;
		AppSettings.Instance.Save();
		if (!enabled)
		{
			StopTimeToUpdateNotifications();
		}
		else
		{
			StartTimeToUpdateNotifications();
		}
	}

	public void StartTimeToUpdateNotifications()
	{
		if (timer == null)
		{
			timer = new Timer();
			timer.Interval = AppSettings.Instance.NotityTimerInterval;
			timer.Tick += (object? s, EventArgs e) =>
			{
				processNewMessage();
			};
		}
		if (!timer.Enabled)
		{
			timer.Start();
		}
	}

	public void StopTimeToUpdateNotifications()
	{
		if (timer != null)
		{
			timer.Stop();
			timer.Dispose();
			timer = null;
		}
	}
}
