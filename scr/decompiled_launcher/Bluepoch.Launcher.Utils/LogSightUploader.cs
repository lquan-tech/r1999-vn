using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Bluepoch.Common.Util;
using Bluepoch.Launcher.App;
using Bluepoch.Launcher.Config;
using Bluepoch.Launcher.Constants;
using Bluepoch.Launcher.Source.Bean;
using Bluepoch.Launcher.Source.Ui;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bluepoch.Launcher.Utils;

internal static class LogSightUploader
{
	private class LogSightException : Exception
	{
		public LogSightException(string message)
			: base(message)
		{
		}
	}

	private const long MaxUploadBytes = 20971520L;

	private const string MockUrlPrefix = "mock://";

	private static readonly HttpClient _httpClient = new HttpClient
	{
		Timeout = Timeout.InfiniteTimeSpan
	};

	private static int _uploading = 0;

	public static void PickAndUpload()
	{
		if (Interlocked.CompareExchange(ref _uploading, 1, 0) != 0)
		{
			ShowDialog(Msg(Bluepoch.Launcher.Constants.Common.LogUploadingTips), success: false, null, uploadFinished: false);
			return;
		}
		bool flag = false;
		try
		{
			string text = Path.Combine(Application.StartupPath, "logs");
			OpenFileDialog dialog = new OpenFileDialog();
			try
			{
				int langIndex = LauncherApplication.GetInstance().GetLangIndex();
				dialog.Title = (Bluepoch.Launcher.Constants.Common.LocalDialogTitle.TryGetValue(langIndex, out var value) ? value : "Notice");
				dialog.Filter = "Log Files (*.log)|*.log";
				if (Directory.Exists(text))
				{
					dialog.InitialDirectory = text;
				}
				if (dialog.ShowDialog() == DialogResult.OK)
				{
					flag = true;
					MainForm.GetInstance().PostWebMessage("logupload-state", t: true);
					Task.Run(() => UploadAsync(dialog.FileName));
				}
			}
			finally
			{
				if (dialog != null)
				{
					((IDisposable)dialog).Dispose();
				}
			}
		}
		finally
		{
			if (!flag)
			{
				Interlocked.Exchange(ref _uploading, 0);
			}
		}
	}

	private static async Task UploadAsync(string filePath)
	{
		_ = 2;
		try
		{
			LauncherDeviceInfo deviceInfo = LauncherApplication.GetInstance().GetDeviceInfo();
			LauncherAppPackageInfo packageInfo = LauncherApplication.GetInstance().GetAppPackageInfo();
			string deviceId = deviceInfo.deviceId;
			long length = new FileInfo(filePath).Length;
			bool flag = length > 20971520;
			byte[] uploadBytes = (flag ? ReadTailShared(filePath, 20971520L) : ReadAllBytesShared(filePath));
			long fileSize = uploadBytes.Length;
			string fileMd5 = Md5Hex(uploadBytes);
			string host = GetLogsightHost();
			string userAgent = "Bluepoch-Launcher/" + packageInfo.appVersionName;
			var body = new
			{
				deviceInfo = new { deviceId, deviceInfo.deviceName, deviceInfo.osVersion },
				appPackageInfo = new { packageInfo.gameId, packageInfo.channelId, packageInfo.appVersionName, packageInfo.gameCode },
				logType = "launcher",
				fileName = Path.GetFileName(filePath),
				fileSize = fileSize,
				fileMd5 = fileMd5,
				truncated = flag,
				originalSize = (flag ? new long?(length) : ((long?)null))
			};
			JObject jObject = await PostSignedAsync(host + "/pc/launcher/upload-ticket", body, deviceId, fileMd5, userAgent);
			EnsureSuccess(jObject);
			string ticketId = jObject["data"]?["ticketId"]?.ToString();
			string text = jObject["data"]?["uploadUrl"]?.ToString();
			if (string.IsNullOrEmpty(ticketId) || string.IsNullOrEmpty(text))
			{
				throw new LogSightException("upload-ticket 响应缺少 data.ticketId / data.uploadUrl");
			}
			if (!text.StartsWith("mock://", StringComparison.OrdinalIgnoreCase))
			{
				await PutFileAsync(text, uploadBytes);
			}
			var body2 = new
			{
				deviceInfo = new { deviceId },
				appPackageInfo = new { packageInfo.gameId },
				ticketId = ticketId,
				fileMd5 = fileMd5,
				fileSize = fileSize
			};
			EnsureSuccess(await PostSignedAsync(host + "/pc/launcher/upload-complete", body2, deviceId, fileMd5, userAgent));
			BluepochLogger.InfoLog("LogSight upload success, ticketId=" + ticketId);
			AppSettings.Instance.SetValue("LastLogTicketId", ticketId);
			LauncherApplication.GetInstance().InvokeOnUiThread(() =>
			{
				MainForm.GetInstance().PostWebMessage("logticket-update", ticketId);
			});
			ShowDialog($"{Msg(Bluepoch.Launcher.Constants.Common.LogUploadSuccessTips)} \r\n{ticketId}\r\n {Msg(Bluepoch.Launcher.Constants.Common.CopyTicketIdTips)}", success: true, ticketId);
		}
		catch (LogSightException ex)
		{
			BluepochLogger.ErrorLog("LogSight upload failed: " + ex.Message);
			ShowDialog(ex.Message, success: false);
		}
		catch (HttpRequestException value)
		{
			BluepochLogger.ErrorLog($"LogSight upload network error: {value}");
			ShowDialog(Msg(Bluepoch.Launcher.Constants.Common.LogUploadNetworkErrorTips, "Network error. Upload failed."), success: false);
		}
		catch (OperationCanceledException value2)
		{
			BluepochLogger.ErrorLog($"LogSight upload timeout: {value2}");
			ShowDialog(Msg(Bluepoch.Launcher.Constants.Common.LogUploadNetworkErrorTips, "Network error. Upload failed."), success: false);
		}
		catch (Exception ex2)
		{
			BluepochLogger.ErrorLog($"LogSight upload error: {ex2}");
			ShowDialog(ex2.Message, success: false);
		}
		finally
		{
			Interlocked.Exchange(ref _uploading, 0);
		}
	}

	private static async Task<JObject> PostSignedAsync(string url, object body, string deviceId, string fileMd5, string userAgent)
	{
		GetLogsightCredentials(out var gameId, out var gameKey);
		string value = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
		string value2 = Guid.NewGuid().ToString("N");
		string data = $"{gameId}|{value}|{value2}|{deviceId}|{fileMd5}";
		string value3 = HmacSha256Hex(gameKey, data);
		string content = JsonConvert.SerializeObject(body);
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
		request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
		request.Headers.TryAddWithoutValidation("X-Game-Key", gameId);
		request.Headers.TryAddWithoutValidation("X-Timestamp", value);
		request.Headers.TryAddWithoutValidation("X-Nonce", value2);
		request.Headers.TryAddWithoutValidation("X-Sign", value3);
		int langIndex = LauncherApplication.GetInstance().GetLangIndex();
		if (langIndex >= 0 && langIndex < LanguageHeaderString.LanguageHeaders.Length)
		{
			request.Headers.TryAddWithoutValidation("lang", LanguageHeaderString.LanguageHeaders[langIndex]);
		}
		request.Content = new StringContent(content, Encoding.UTF8, "application/json");
		using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(30.0));
		return JObject.Parse(await (await _httpClient.SendAsync(request, cts.Token)).Content.ReadAsStringAsync());
	}

	private static async Task PutFileAsync(string uploadUrl, byte[] bytes)
	{
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
		ByteArrayContent byteArrayContent = new ByteArrayContent(bytes);
		byteArrayContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
		request.Content = byteArrayContent;
		using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(120.0));
		HttpResponseMessage httpResponseMessage = await _httpClient.SendAsync(request, cts.Token);
		if (!httpResponseMessage.IsSuccessStatusCode)
		{
			throw new LogSightException($" {Msg(Bluepoch.Launcher.Constants.Common.LogUploadCosVerifyError)} {(int)httpResponseMessage.StatusCode}");
		}
	}

	private static void EnsureSuccess(JObject resp)
	{
		JToken? jToken = resp["code"];
		int num = ((jToken != null) ? jToken.Value<int>() : (-1));
		if (num != 0)
		{
			string value = resp["message"]?.ToString() ?? "";
			throw new LogSightException($"code={num} {value}");
		}
	}

	private static string HmacSha256Hex(string secret, string data)
	{
		using HMACSHA256 hMACSHA = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
		return BitConverter.ToString(hMACSHA.ComputeHash(Encoding.UTF8.GetBytes(data))).Replace("-", "").ToLowerInvariant();
	}

	private static byte[] ReadAllBytesShared(string path)
	{
		using FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		return ReadToEnd(fs);
	}

	private static byte[] ReadTailShared(string path, long tailBytes)
	{
		using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		long num = Math.Min(tailBytes, fileStream.Length);
		fileStream.Seek(-num, SeekOrigin.End);
		return ReadToEnd(fileStream);
	}

	private static byte[] ReadToEnd(FileStream fs)
	{
		byte[] array = new byte[fs.Length - fs.Position];
		int i;
		int num;
		for (i = 0; i < array.Length; i += num)
		{
			num = fs.Read(array, i, array.Length - i);
			if (num == 0)
			{
				break;
			}
		}
		if (i < array.Length)
		{
			Array.Resize(ref array, i);
		}
		return array;
	}

	private static string Md5Hex(byte[] bytes)
	{
		using MD5 mD = MD5.Create();
		return BitConverter.ToString(mD.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
	}

	private static string GetLogsightHost()
	{
		Dictionary<string, string> manifest = SettingConfigManager.GetInstance().GetManifest();
		if (manifest != null && manifest.TryGetValue(LauncherApplication.GetInstance().GetRunningType() switch
		{
			1 => "Dev_logsight_host", 
			2 => "Test_logsight_host", 
			_ => "Release_logsight_host", 
		}, out var value) && !string.IsNullOrWhiteSpace(value))
		{
			return value.Trim();
		}
		return "http://localhost:8080";
	}

	private static void GetLogsightCredentials(out string gameId, out string gameKey)
	{
		gameId = "50001";
		gameKey = "123456";
		Dictionary<string, string> manifest = SettingConfigManager.GetInstance().GetManifest();
		if (manifest != null)
		{
			string key;
			string key2;
			switch (LauncherApplication.GetInstance().GetRunningType())
			{
			case 1:
				key = "Dev_logsight_gameId";
				key2 = "Dev_logsight_gamekey";
				break;
			case 2:
				key = "Test_logsight_gameId";
				key2 = "Test_logsight_gamekey";
				break;
			default:
				key = "Release_logsight_gameId";
				key2 = "Release_logsight_gamekey";
				break;
			}
			if (manifest.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
			{
				gameId = value.Trim();
			}
			if (manifest.TryGetValue(key2, out var value2) && !string.IsNullOrWhiteSpace(value2))
			{
				gameKey = value2.Trim();
			}
		}
	}

	private static string GetConfig(string key, string defaultValue)
	{
		string valueFromConfig = SettingConfigManager.GetInstance().GetValueFromConfig("Launcher", key, defaultValue);
		if (!string.IsNullOrWhiteSpace(valueFromConfig))
		{
			return valueFromConfig;
		}
		return defaultValue;
	}

	private static string Msg(Dictionary<int, string> msg, string defaultValue = "")
	{
		if (!msg.TryGetValue(LauncherApplication.GetInstance().GetLangIndex(), out var value))
		{
			return defaultValue;
		}
		return value;
	}

	private static string NetworkErrorMsg()
	{
		int langIndex = LauncherApplication.GetInstance().GetLangIndex();
		if (!Bluepoch.Launcher.Constants.Common.LogUploadNetworkErrorTips.TryGetValue(langIndex, out var value))
		{
			return "";
		}
		return value;
	}

	private static void ShowDialog(string message, bool success, string ticketId = null, bool uploadFinished = true)
	{
		LauncherApplication.GetInstance().InvokeOnUiThread(() =>
		{
			if (uploadFinished)
			{
				MainForm.GetInstance().PostWebMessage("logupload-state", t: false);
			}
			if (success && !string.IsNullOrEmpty(ticketId))
			{
				Clipboard.SetText(ticketId);
			}
			NaticeWarningDialog.ShowWarning(MainForm.GetInstance(), message, () =>
			{
			});
		});
	}
}
