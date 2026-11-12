using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Bluepoch.Common.Bean;
using Bluepoch.Common.Util;
using Bluepoch.Launcher.App;
using Bluepoch.Launcher.Bean;
using Bluepoch.Launcher.Config;
using Bluepoch.Launcher.Constants;
using Bluepoch.Launcher.Properties;
using Bluepoch.Launcher.Source.App;
using Bluepoch.Launcher.Source.Bean;
using Bluepoch.Launcher.Source.Bean.Respond;
using Bluepoch.Launcher.Source.Download;
using Bluepoch.Launcher.Source.Download.Data;
using Bluepoch.Launcher.Source.Game;
using Bluepoch.Launcher.Source.Ui;
using Bluepoch.Launcher.Source.Utils;
using Bluepoch.Launcher.Utils;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;
using ThinkingSDK.PC;

namespace Bluepoch.Launcher;

public class MainForm : Form
{
	private const int WM_NCLBUTTONDOWN = 161;

	private const int HT_CAPTION = 2;

	private const int SW_RESTORE = 9;

	private bool isWebView2RuntimeNoExists;

	private NotifyIcon notifyIcon;

	private ContextMenuStrip notifyContextMenu;

	private System.Windows.Forms.Timer gameRunningCheckTimer;

	private System.Windows.Forms.Timer checkUpdateTimer;

	public const string EXE_NAME_EN = "Reverse1999";

	private bool isAppnNeedToUnpdate = true;

	private DownloadInterface multiDownloader;

	private ExtractInterface multiExtractTool;

	private IContainer components;

	private PictureBox bgPicture;

	private WebView2 webView;

	[DllImport("user32.dll")]
	private static extern int SendMessage(nint hWnd, int Msg, int wParam, int lParam);

	[DllImport("user32.dll")]
	private static extern bool ReleaseCapture();

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool SetForegroundWindow(nint hWnd);

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(nint hWnd, int nCmdShow);

	private void EnsureHttps(object sender, CoreWebView2NavigationStartingEventArgs args)
	{
		string uri = args.Uri;
		if (!uri.StartsWith("https://") && !uri.StartsWith("file://") && !uri.StartsWith("http://"))
		{
			webView.CoreWebView2.ExecuteScriptAsync("alert('" + uri + " is not safe, try an https link')");
			args.Cancel = true;
		}
	}

	private bool IsSystemBrowserAvailable()
	{
		try
		{
			return !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString());
		}
		catch (Exception ex)
		{
			BluepochLogger.ErrorLog("Check system browser failed: " + ex.Message);
			return false;
		}
	}

	private async Task<bool> UseSystemWebView2RuntimeAsync(string userDataFolder)
	{
		_ = 1;
		try
		{
			CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
			await webView.EnsureCoreWebView2Async(environment);
			if (webView.CoreWebView2 == null)
			{
				return false;
			}
			return true;
		}
		catch
		{
			return false;
		}
	}

	private async Task<bool> UseAppWebView2RuntimeAsync(string userDataFolder)
	{
		_ = 1;
		try
		{
			string text = Path.Combine(Application.StartupPath, "runtimes/webview2");
			string text2 = Path.Combine(text, "msedgewebview2.exe");
			if (!File.Exists(text2))
			{
				BluepochLogger.ErrorLog("WebView2运行时文件不存在: " + text2);
				return false;
			}
			Environment.SetEnvironmentVariable("WEBVIEW2_BROWSER_EXECUTABLE_FOLDER", text);
			CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(text, userDataFolder);
			await webView.EnsureCoreWebView2Async(environment);
			if (webView.CoreWebView2 == null)
			{
				return false;
			}
			return true;
		}
		catch
		{
			return false;
		}
	}

	private async Task InitializeAsync()
	{
		_ = 2;
		try
		{
			string userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), SettingConfigManager.GetInstance().GetManifest()["userDataDir"]);
			if (IsSystemBrowserAvailable())
			{
				isWebView2RuntimeNoExists = !(await UseSystemWebView2RuntimeAsync(userDataFolder));
				if (isWebView2RuntimeNoExists)
				{
					isWebView2RuntimeNoExists = !(await UseAppWebView2RuntimeAsync(userDataFolder));
				}
			}
			else
			{
				isWebView2RuntimeNoExists = !(await UseAppWebView2RuntimeAsync(userDataFolder));
			}
			if (isWebView2RuntimeNoExists)
			{
				BluepochLogger.ErrorLog("CoreWebView2  init fail");
				ShowBgPng();
				return;
			}
			BluepochLogger.InfoLog("Release Mode In Production");
			if (LauncherApplication.GetInstance().IsDebugMode())
			{
				webView.CoreWebView2.OpenDevToolsWindow();
			}
			webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
			webView.CoreWebView2.Settings.IsSwipeNavigationEnabled = false;
			webView.ZoomFactor = 1.0;
			webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
			webView.CoreWebView2.WebMessageReceived += webMessageReceived;
			webView.CoreWebView2.NavigationCompleted += webLoadCompleted;
			string valueFromConfig = SettingConfigManager.GetInstance().GetValueFromConfig("Launcher", "debughtml_e6492ceabf0b22bd4dbc15f67c090a4c");
			if (valueFromConfig.StartsWith("http") && LauncherApplication.GetInstance().IsDebugMode())
			{
				webView.CoreWebView2.Navigate(valueFromConfig);
				webView.CoreWebView2.OpenDevToolsWindow();
				return;
			}
			string indexHtmlPath = LauncherApplication.GetInstance().GetIndexHtmlPath();
			if (!File.Exists(indexHtmlPath))
			{
				BluepochLogger.ErrorLog("indexPath is null: " + indexHtmlPath);
				isWebView2RuntimeNoExists = true;
				ShowBgPng(indexHtmlisNull: true);
			}
			else
			{
				webView.CoreWebView2.Navigate(new Uri(indexHtmlPath).AbsoluteUri);
			}
		}
		catch (Exception ex)
		{
			BluepochLogger.ErrorLog("webview2 init - fail : " + ex.Message);
			isWebView2RuntimeNoExists = true;
			ShowBgPng();
		}
	}

	public MainForm()
	{
		InitializeComponent();
		Text = SettingConfigManager.GetInstance().GetManifest()["app_displayname"];
		DoubleBuffered = true;
		SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint, value: true);
		FormBorderStyle = FormBorderStyle.None;
		StartPosition = FormStartPosition.CenterScreen;
		BluepochLogger.InfoLog($"width:{Width}");
		webView.DefaultBackgroundColor = Color.Transparent;
		webView.AllowExternalDrop = true;
		webView.NavigationStarting += EnsureHttps;
		Opacity = 0.0;
		ShowInTaskbar = true;
		IconSet();
		InitializeAsync().ContinueWith((Task t) =>
		{
			if (!isWebView2RuntimeNoExists)
			{
				Invoke(async () =>
				{
					if (!isWebView2RuntimeNoExists)
					{
						Controls.Remove(bgPicture);
					}
					for (double d = 0.0; d <= 1.0; d += 0.05)
					{
						Opacity = d;
						await Task.Delay(20);
					}
					Opacity = 1.0;
					SettingConfigManager.GetInstance().ReSetAppSettingByOutConfig();
					ClientSizeChanged += OnWindowSizeChanged;
					Size realPixelSize = GetRealPixelSize();
					BluepochLogger.InfoLog($"Window real size: {realPixelSize.Width}x{realPixelSize.Height}");
					BluepochLogger.InfoLog($"Client real size: {Width}x{Height}");
					BluepochLogger.InfoLog($"WebView real size: {webView.Width}x{webView.Height}");
					float value = (float)DeviceDpi / 96f;
					BluepochLogger.InfoLog($"DPI Scale: {value}");
					BluepochLogger.InfoLog($"DeviceDpi: {DeviceDpi}");
				});
			}
		});
		UpdateProgramDirAsync();
		ThinkingPCSDKManager.getInstance().TrackEvent(TrackEvent.EVENT_OPEN_LAUNCHER);
	}

	private async Task UpdateProgramDirAsync()
	{
		try
		{
			string text = Path.Combine(Application.StartupPath, "updateProgram2");
			if (!Directory.Exists(text))
			{
				return;
			}
			string text2 = Path.Combine(Application.StartupPath, "updateProgram");
			if (!Directory.Exists(text2))
			{
				Directory.Move(text, text2);
				BluepochLogger.InfoLog("UpdateProgramDir Successed By updateProgramPath Not Here");
				return;
			}
			Progress<FileUtil.DeleteProgress> progress = new Progress<FileUtil.DeleteProgress>((FileUtil.DeleteProgress p) =>
			{
				if ((int)p.Processed == 100)
				{
					string destDirName = Path.Combine(Application.StartupPath, "updateProgram");
					Directory.Move(Path.Combine(Application.StartupPath, "updateProgram2"), destDirName);
					BluepochLogger.InfoLog("UpdateProgramDir Successed By Delete");
				}
			});
			await FileUtil.DeleteDirectoryOptimized(text2, progress);
		}
		catch (Exception ex)
		{
			Dictionary<string, object> properties = new Dictionary<string, object>
			{
				[TrackEvent.EVENT_Result_Msg] = ex.Message,
				[TrackEvent.Exception_Location] = "UpdateProgramDirAsync"
			};
			ThinkingPCSDKManager.getInstance().TrackEvent(TrackEvent.EVENT_Exception, properties);
			BluepochLogger.ErrorLog("UpdateProgramDir fail -ex : " + ex.Message);
		}
	}

	private void ShowBgPng(bool indexHtmlisNull = false)
	{
		Opacity = 1.0;
		Controls.Add(bgPicture);
		int num = Convert.ToInt32(Resources.GameId ?? "-1");
		switch (num)
		{
		case 60001:
			bgPicture.Image = Image.FromFile(Path.Combine(Application.StartupPath, "Resources", "mainwindow_en.jpg"));
			break;
		case 70001:
			bgPicture.Image = Image.FromFile(Path.Combine(Application.StartupPath, "Resources", "mainwindow_jp.jpg"));
			break;
		case 90001:
			bgPicture.Image = Image.FromFile(Path.Combine(Application.StartupPath, "Resources", "mainwindow_kr.jpg"));
			break;
		case 50001:
			bgPicture.Image = Image.FromFile(Path.Combine(Application.StartupPath, "Resources", "mainwindow_en.png"));
			break;
		default:
			BluepochLogger.InfoLog($"launcherType  :  {num}");
			bgPicture.Image = Image.FromFile(Path.Combine(Application.StartupPath, "Resources", "mainwindow_en.png"));
			break;
		}
		if (indexHtmlisNull)
		{
			Dictionary<string, object> properties = new Dictionary<string, object> { [TrackEvent.EVENT_Result_Msg] = "Index Html file is not here" };
			ThinkingPCSDKManager.getInstance().TrackEvent(TrackEvent.EVENT_WEB_ERROR, properties);
			NaticeWarningDialog.ShowWarning(this, "App files is not full, Install again!", () =>
			{
				LauncherUtils.OpenWebInBrowser(SettingConfigManager.GetInstance().GetManifest()["officalweb"]);
				MainForm_ShutDown();
			});
		}
		else
		{
			Dictionary<string, object> properties2 = new Dictionary<string, object> { [TrackEvent.EVENT_Result_Msg] = "WebView2 Runtime is not installed" };
			ThinkingPCSDKManager.getInstance().TrackEvent(TrackEvent.EVENT_WEB_ERROR, properties2);
			NaticeWarningDialog.ShowWarning(this, Bluepoch.Launcher.Constants.Common.WebView2RuntimeTips[AppSettings.Instance.Language_index], () =>
			{
				InstallWebview2Runtime();
				MainForm_ShutDown();
			});
		}
	}

	private void InstallWebview2Runtime()
	{
		string exePath = Path.Combine(Application.StartupPath, "runtimes/webview2", "MicrosoftEdgeWebview2Setup.exe");
		StartExternalExe(exePath);
		MainForm_ShutDown();
	}

	private void IconSet()
	{
		string appIconFilePath = LauncherUtils.GetAppIconFilePath();
		Icon = new Icon(appIconFilePath);
		notifyIcon = new NotifyIcon();
		notifyIcon.Icon = new Icon(appIconFilePath);
		notifyIcon.Visible = true;
		notifyIcon.DoubleClick += MainForm_Activate;
		notifyContextMenu = new ContextMenuStrip();
		notifyIcon.ContextMenuStrip = notifyContextMenu;
		ToolStripItemCollection items = notifyContextMenu.Items;
		ToolStripItem[] toolStripItems = new ToolStripMenuItem[2]
		{
			new ToolStripMenuItem("Open", null, (object sender, EventArgs e) =>
			{
				MainForm_Activate(sender, e);
			}),
			new ToolStripMenuItem("Exit", null, (object sender, EventArgs e) =>
			{
				MainForm_ShutDown();
			})
		};
		items.AddRange(toolStripItems);
	}

	private void webLoadCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
	{
		PostWebMessage("init-app", LauncherApplication.GetInstance().GetAppSettingsInfo());
	}

	public void PostWebMessage<T>(string actionName, T t)
	{
		try
		{
			if (webView == null || webView.IsDisposed || !webView.IsHandleCreated)
			{
				BluepochLogger.WarningLog("WebView2不可用，跳过消息发送: " + actionName);
				return;
			}
			string webMessageAsString = JsonConvert.SerializeObject(new WebActionInfo<T>
			{
				actionName = actionName,
				actionData = t
			});
			if (webView.CoreWebView2 == null)
			{
				BluepochLogger.WarningLog("CoreWebView2不可用，跳过消息发送: " + actionName);
			}
			else
			{
				webView.CoreWebView2.PostWebMessageAsString(webMessageAsString);
			}
		}
		catch (InvalidCastException ex)
		{
			BluepochLogger.ErrorLog("WebView2 COM接口转换错误: " + ex.Message);
		}
		catch (Exception ex2)
		{
			BluepochLogger.ErrorLog("发送Web消息失败: " + actionName + ", 错误: " + ex2.Message);
		}
	}

	private void DragMove()
	{
		ReleaseCapture();
		SendMessage(Handle, 161, 2, 0);
	}

	private async void OpenDownloadPathSetDialog()
	{
		string downloadPath = GameCommon.GetGameExeInstallDirByDefault();
		await Task.Run(() =>
		{
			string selectedDirPath = null;
			Invoke(() =>
			{
				using FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
				folderBrowserDialog.Description = "Choose";
				folderBrowserDialog.ShowNewFolderButton = true;
				folderBrowserDialog.SelectedPath = new FileInfo(downloadPath).Directory.FullName;
				if (folderBrowserDialog.ShowDialog(this) == DialogResult.OK)
				{
					selectedDirPath = folderBrowserDialog.SelectedPath;
				}
				else
				{
					BluepochLogger.InfoLog("用户选择文件夹报错");
				}
			});
			if (!string.IsNullOrEmpty(selectedDirPath))
			{
				Invoke(() =>
				{
					try
					{
						string text = Path.Combine(selectedDirPath, SettingConfigManager.GetInstance().GetManifest()["gameExeDir"]);
						if (!Directory.Exists(text))
						{
							Directory.CreateDirectory(text);
						}
						AppSettings.Instance.GameExePath = text;
						AppSettings.Instance.Save();
						GamePlugin.GetInstance().GetGameVersionInfo().rootDirRoomSize = FileUtil.ComputerDirRootRoomSize(text);
						GamePlugin.GetInstance().GetGameVersionInfo().gameExeDownloadDir = text;
						PostWebMessage("downloaddirpath-set", GamePlugin.GetInstance().GetGameVersionInfo());
					}
					catch (Exception ex)
					{
						BluepochLogger.ErrorLog("OpenDownloadPathSetDialog -DirPath : " + selectedDirPath + "  ,  Error : " + ex.Message);
						WarningDialogInfo t = new WarningDialogInfo
						{
							message_lang = "GameDirChoose.Error_IllegalDirPath"
						};
						PostWebMessage("show-warningdialog", t);
					}
				});
			}
		});
	}

	private async void OpenGameExePathSetDialog(string initialDir = null)
	{
		await Task.Run(() =>
		{
			string selectedFilePath = null;
			Invoke(() =>
			{
				using OpenFileDialog openFileDialog = new OpenFileDialog();
				openFileDialog.Title = "ChooseFile（.exe）";
				openFileDialog.Filter = "(reverse1999.exe)|reverse1999.exe";
				openFileDialog.FilterIndex = 1;
				openFileDialog.RestoreDirectory = true;
				if (!string.IsNullOrEmpty(initialDir) && Directory.Exists(initialDir))
				{
					openFileDialog.InitialDirectory = initialDir;
					BluepochLogger.InfoLog("OpenGameExePathSetDialog initialDir : " + initialDir);
				}
				if (openFileDialog.ShowDialog() == DialogResult.OK)
				{
					selectedFilePath = openFileDialog.FileName;
				}
			});
			if (!string.IsNullOrEmpty(selectedFilePath))
			{
				Invoke(() =>
				{
					BluepochLogger.InfoLog("User Choose File Path：" + selectedFilePath);
					string directoryName = Path.GetDirectoryName(selectedFilePath);
					string gameVersion = FileUtil.ReadGameVerionInGameExeDir(directoryName);
					GameCommon.UpdateGameDirPathInCache(directoryName);
					GameCommon.UpdateGameStatusInCache(5);
					GameCommon.UpdateGameVersionInCache(gameVersion);
					GameCommon.RecordInstallPath(directoryName);
					GameCommon.RecordInstallVersion(gameVersion);
					PostWebMessage("gamedir-set", LauncherApplication.GetInstance().GetAppSettingsInfo());
					GamePlugin.GetInstance().CheckGameVersion(immediately: true);
				});
			}
		});
	}

	private void webMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs args)
	{
		try
		{
			string text = args.TryGetWebMessageAsString();
			BluepochLogger.InfoLog("action message:" + text);
			WebActionInfo<object> webActionInfo = JsonConvert.DeserializeObject<WebActionInfo<object>>(text);
			if (webActionInfo.actionName.Equals("start-drag"))
			{
				DragMove();
			}
			if (webActionInfo.actionName.Equals("close-app"))
			{
				BluepochLogger.InfoLog("Close_Type : " + AppSettings.Instance.Close_tpye);
				if (-1 == AppSettings.Instance.Close_tpye)
				{
					PostWebMessage("show-closetypechoosedialog", "");
					return;
				}
				if (AppSettings.Instance.Close_tpye == 1)
				{
					MainForm_ShutDown();
				}
				else
				{
					MainForm_Minimized();
					MainForm_NoIconInTaskBar();
				}
			}
			if (webActionInfo.actionName.Equals("min-app"))
			{
				MainForm_Minimized();
			}
			if (webActionInfo.actionName.Equals("init-finish"))
			{
				InitGamePlugin();
				NotifyMessageAfterInit();
				CheckAppUpadteAfterInit();
				DeleteQtFiles();
				DeleteStartUpOldFiles();
			}
			if (webActionInfo.actionName.Equals("confirm-update"))
			{
				OpenUpdateExe();
			}
			if (webActionInfo.actionName.Equals("auto-start"))
			{
				AppSettingsInfo appSettingsInfo = JsonConvert.DeserializeObject<AppSettingsInfo>(webActionInfo.actionData.ToString());
				string text2 = SettingConfigManager.GetInstance().GetManifest()["app_displayname"];
				if (!LauncherUtils.IsAutoStartEnabled(text2) && appSettingsInfo.isAutoStart)
				{
					LauncherUtils.SetAutoStart(text2);
				}
				if (LauncherUtils.IsAutoStartEnabled(text2) && !appSettingsInfo.isAutoStart)
				{
					LauncherUtils.UnsetAutoStart(text2);
				}
			}
			if (webActionInfo.actionName.Equals("close-set"))
			{
				if (JsonConvert.DeserializeObject<AppSettingsInfo>(webActionInfo.actionData.ToString()).isMinimizednByCloseBtn)
				{
					AppSettings.Instance.Close_tpye = 2;
					AppSettings.Instance.Save();
				}
				else
				{
					AppSettings.Instance.Close_tpye = 1;
					AppSettings.Instance.Save();
				}
			}
			if (webActionInfo.actionName.Equals("notify-set"))
			{
				AppSettingsInfo appSettingsInfo2 = JsonConvert.DeserializeObject<AppSettingsInfo>(webActionInfo.actionData.ToString());
				NotificationPlugin.GetInstance().UpdateNotificationsEnabled(appSettingsInfo2.notifyable);
			}
			if (webActionInfo.actionName.Equals("language-set"))
			{
				AppSettingsInfo appSettingsInfo3 = JsonConvert.DeserializeObject<AppSettingsInfo>(webActionInfo.actionData.ToString());
				AppSettings.Instance.Language_index = appSettingsInfo3.launcherLang;
				AppSettings.Instance.Save();
			}
			if (webActionInfo.actionName.Equals("check-app-version"))
			{
				AppPlugin.GetInstance().CheckUpdateInServerFromSetting();
			}
			if (webActionInfo.actionName.Equals("check-game-version"))
			{
				if (IsDownloaderAndExtraeBusy() || isAppnNeedToUnpdate)
				{
					return;
				}
				GamePlugin.GetInstance().CheckGameVersion(immediately: true, isComeFromSetting: true);
			}
			if (webActionInfo.actionName.Equals("open-url-terms"))
			{
				LauncherUtils.OpenWebInBrowser(SettingConfigManager.GetInstance().GetManifest()["userAgreementHtml"]);
			}
			if (webActionInfo.actionName.Equals("open-url-privacy"))
			{
				LauncherUtils.OpenWebInBrowser(SettingConfigManager.GetInstance().GetManifest()["protocolHtml"]);
			}
			if (webActionInfo.actionName.Equals("open-url-custom"))
			{
				BluepochLogger.InfoLog("url ： ", webActionInfo.actionData.ToString());
				Dictionary<string, string> dictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(webActionInfo.actionData.ToString());
				if (dictionary.ContainsKey("url"))
				{
					LauncherUtils.OpenWebInBrowser(dictionary["url"]);
				}
			}
			if (webActionInfo.actionName.Equals("open-launcher-log"))
			{
				LauncherUtils.OpenLauncherLogFolder();
			}
			if (webActionInfo.actionName.Equals("open-game-log"))
			{
				LauncherUtils.OpenGameLogFolder();
			}
			if (webActionInfo.actionName.Equals("update-log"))
			{
				LogSightUploader.PickAndUpload();
			}
			if (webActionInfo.actionName.Equals("copy-log-ticket"))
			{
				string text3 = webActionInfo.actionData?.ToString();
				if (!string.IsNullOrEmpty(text3))
				{
					Clipboard.SetText(text3);
				}
			}
			if (webActionInfo.actionName.Equals("downloadspeed-set"))
			{
				AppSettingsInfo appSettingsInfo4 = JsonConvert.DeserializeObject<AppSettingsInfo>(webActionInfo.actionData.ToString());
				AppSettings.Instance.DownloadSpeed = appSettingsInfo4.downloadSpeed;
				AppSettings.Instance.DownloadSpeedLimit = appSettingsInfo4.downloadSpeedLimit;
				AppSettings.Instance.Save();
			}
			if (webActionInfo.actionName.Equals("gameexepath-set"))
			{
				if (multiDownloader != null && multiDownloader.Is_Downloading)
				{
					WarningDialogInfo t = new WarningDialogInfo
					{
						message_lang = "GameDirChoose.Error_ChooseInDownloading"
					};
					PostWebMessage("show-warningdialog", t);
					return;
				}
				if (multiExtractTool != null && multiExtractTool.IsWorking)
				{
					WarningDialogInfo t2 = new WarningDialogInfo
					{
						message_lang = "GameDirChoose.Error_ChooseInInstalling"
					};
					PostWebMessage("show-warningdialog", t2);
					return;
				}
				string initialDir = null;
				try
				{
					if (webActionInfo.actionData != null)
					{
						Dictionary<string, string> dictionary2 = JsonConvert.DeserializeObject<Dictionary<string, string>>(webActionInfo.actionData.ToString());
						if (dictionary2 != null && dictionary2.ContainsKey("dirPath"))
						{
							initialDir = dictionary2["dirPath"];
						}
					}
				}
				catch (Exception ex)
				{
					BluepochLogger.WarningLog("GameExePath_Set parse dirPath error: " + ex.Message);
				}
				OpenGameExePathSetDialog(initialDir);
			}
			if (webActionInfo.actionName.Equals("downloaddirpath-set"))
			{
				OpenDownloadPathSetDialog();
			}
			if (webActionInfo.actionName.Equals("click-downloadbutton"))
			{
				int downloadType = DownloaderFactory.GetInstance().GetDownloadType();
				GamePlugin.GetInstance().GetGameVersionInfo().currentDownloadType = downloadType;
				GamePlugin.GetInstance().GetGameVersionInfo().rootDirRoomSize = FileUtil.ComputerDirRootRoomSize(GameCommon.GetGameExeInstallDirByDefault());
				GamePlugin.GetInstance().GetGameVersionInfo().gameExeDownloadDir = GameCommon.GetGameExeInstallDirByDefault();
				PostWebMessage("open-downloadpathsetting", GamePlugin.GetInstance().GetGameVersionInfo());
			}
			if (webActionInfo.actionName.Equals("start-download"))
			{
				ThinkingPCSDKManager.getInstance().TrackEvent("start_download");
				string gameExeInstallDirByDefault = GameCommon.GetGameExeInstallDirByDefault();
				if (!Directory.Exists(gameExeInstallDirByDefault))
				{
					Directory.CreateDirectory(gameExeInstallDirByDefault);
				}
				GameCommon.UpdateGameDirPathInCache(gameExeInstallDirByDefault);
				StartdownloadFile();
			}
			if (webActionInfo.actionName.Equals("download-continue"))
			{
				StartdownloadFile();
			}
			if (webActionInfo.actionName.Equals("download-pause"))
			{
				StopDownload();
			}
			if (webActionInfo.actionName.Equals("click-unzipbutton"))
			{
				ExtractZipFile();
			}
			if (webActionInfo.actionName.Equals("click-playgamebutton"))
			{
				PlayGame();
			}
			if (webActionInfo.actionName.Equals("clear-resource"))
			{
				ClearResource();
			}
			if (webActionInfo.actionName.Equals("click-updategamebutton"))
			{
				StartdownloadFile();
			}
			if (webActionInfo.actionName.Equals("exit-app"))
			{
				MainForm_ShutDown();
			}
			webActionInfo.actionName.Equals("log-out");
			if (webActionInfo.actionName.Equals("datatrack"))
			{
				Dictionary<string, object> dictionary3 = JsonConvert.DeserializeObject<Dictionary<string, object>>(webActionInfo.actionData.ToString());
				if (dictionary3 == null)
				{
					return;
				}
				ThinkingPCSDKManager.getInstance().AddSuperProperties(dictionary3);
			}
			if (webActionInfo.actionName.Equals("closetype-set"))
			{
				Dictionary<string, string> dictionary4 = JsonConvert.DeserializeObject<Dictionary<string, string>>(webActionInfo.actionData.ToString());
				if (dictionary4 == null)
				{
					MainForm_Minimized();
					return;
				}
				if (dictionary4.ContainsKey("closeType"))
				{
					string text4 = dictionary4["closeType"];
					BluepochLogger.InfoLog("closeType ： ", text4.ToString());
					AppSettings.Instance.Close_tpye = Convert.ToInt16(text4);
					AppSettings.Instance.Save();
					PostWebMessage("update-video", LauncherApplication.GetInstance().GetAppSettingsInfo());
				}
				if (AppSettings.Instance.Close_tpye == 1)
				{
					MainForm_ShutDown();
				}
				else
				{
					MainForm_Minimized();
					MainForm_NoIconInTaskBar();
				}
			}
			if (!webActionInfo.actionName.Equals("set-innerwidth-ratio"))
			{
				return;
			}
			BluepochLogger.InfoLog("Ratio ： ", webActionInfo.actionData.ToString());
			Dictionary<string, string> dictionary5 = JsonConvert.DeserializeObject<Dictionary<string, string>>(webActionInfo.actionData.ToString());
			if (dictionary5.ContainsKey("Ratio"))
			{
				double num = Convert.ToDouble(dictionary5["Ratio"]);
				BluepochLogger.InfoLog($"ratioValue ： {num}");
				if (num != 1.0)
				{
					int width = 1706;
					int height = 1080;
					Size = new Size(width, height);
					webView.Size = new Size(width, height);
					webView.Location = new Point(0, 0);
				}
			}
		}
		catch (Exception ex2)
		{
			Dictionary<string, object> properties = new Dictionary<string, object>
			{
				[TrackEvent.EVENT_Result_Msg] = ex2.Message,
				[TrackEvent.Exception_Location] = $"webMessageReceived - {args}"
			};
			ThinkingPCSDKManager.getInstance().TrackEvent(TrackEvent.EVENT_Exception, properties);
			BluepochLogger.InfoLog("----<webMessageReceived Exception>--->" + ex2.Message);
		}
	}

	private void OpenUpdateExe()
	{
		string exePath = Path.Combine(FileUtil.GetAppDirectory(), "updateProgram\\reverse1999-update.exe");
		string downloadUrl = AppPlugin.GetInstance().appVersionInfo.downloadUrl;
		string value = LanguageHeaderString.LanguageHeaders[AppSettings.Instance.Language_index];
		string value2 = "update_bg.png";
		string gameId = Resources.GameId;
		string mainUrl = AppPlugin.GetInstance().appVersionInfo.mainUrl;
		string arguments = $"{downloadUrl} {value} {value2} {gameId} {mainUrl}";
		if (StartExternalUpdateExe(exePath, arguments))
		{
			MainForm_ShutDown();
		}
	}

	private void InitGamePlugin()
	{
		GamePlugin.GetInstance().needToDownloadHandler += (object sender, GameVersionInfo gameVersionInfo) =>
		{
			if (!IsDownloaderAndExtraeBusy() && !isAppnNeedToUnpdate)
			{
				BluepochLogger.InfoLog($"This Game versiong [{gameVersionInfo.latestVersion}] need to dwonload by the [{gameVersionInfo.updateType}] Wey . ");
				PostWebMessage("gamestatus-needtodownload", gameVersionInfo);
			}
		};
		GamePlugin.GetInstance().downloadingHandler += (object sender, DownloadingInfo downloadingInfo) =>
		{
			BluepochLogger.InfoLog("Game is downloading ...");
			PostWebMessage("gamestatus-downloading", downloadingInfo);
		};
		GamePlugin.GetInstance().needToUnzipHandler += (object sender, EventArgs e) =>
		{
			BluepochLogger.InfoLog("Game is unZiping ...");
			PostWebMessage<GameVersionInfo>("gamestatus-needtounzip", null);
		};
		GamePlugin.GetInstance().installedHandler += (object sender, EventArgs e) =>
		{
			BluepochLogger.InfoLog("Game is installed .");
			PostWebMessage<GameVersionInfo>("gamestatus-hasinstall", null);
			FileUtil.RemoveFilesBySuffix(FileUtil.GetAppDataDirPath("Downloads"), ".7z");
			FileUtil.RemoveFilesBySuffix(FileUtil.GetAppDataDirPath("Downloads"), ".7z.tmp");
			FileUtil.RemoveFilesBySuffix(FileUtil.GetAppDataDirPath("DownloadGames"), ".7z");
			FileUtil.RemoveFilesBySuffix(FileUtil.GetAppDataDirPath("DownloadGames"), ".7z.tmp");
		};
		GamePlugin.GetInstance().needToUpdateHandler += (object sender, GameVersionInfo gameVersionInfo) =>
		{
			BluepochLogger.InfoLog("Game is need to update .");
			PostWebMessage("gamestatus-needtoupdate", gameVersionInfo);
		};
		GamePlugin.GetInstance().gameofflineHandler += (object sender, string msg) =>
		{
			WarningDialogInfo t = new WarningDialogInfo
			{
				message_lang = "GameInstallPlugin.CannotgetVersionInfo"
			};
			PostWebMessage("show-warningdialog", t);
		};
		GamePlugin.GetInstance().networkErrorHandler += (object sender, string msg) =>
		{
			ShowHttpErrorDialog();
		};
		GamePlugin.GetInstance().updateNewVersionNowHandler += (object sender, EventArgs e) =>
		{
			PostWebMessage("show-updategameDialog", GamePlugin.GetInstance().GetGameVersionInfo());
		};
		GamePlugin.GetInstance().versionIsNewHandler += (object sender, string e) =>
		{
			WarningDialogInfo warningDialogInfo = new WarningDialogInfo();
			if (AppSettings.Instance.GameInstallStatus == 0 || AppSettings.Instance.GameInstallStatus == 5)
			{
				warningDialogInfo.message_lang = "QObject.updated_latest_Version";
			}
			else
			{
				warningDialogInfo.message_lang = "QObject.DownloadingGameNow";
			}
			PostWebMessage("show-warningdialog", warningDialogInfo);
		};
	}

	private bool IsDownloaderAndExtraeBusy()
	{
		if (multiDownloader != null && multiDownloader.Is_Downloading)
		{
			return true;
		}
		if (multiExtractTool != null && multiExtractTool.IsWorking)
		{
			return true;
		}
		return false;
	}

	private void PlayGame()
	{
		Dictionary<string, object> properties = new Dictionary<string, object>
		{
			[TrackEvent.GameVersion] = GamePlugin.GetInstance().GetGameVersionInfo().latestVersion,
			[TrackEvent.GameDwonloadType] = AppSettings.Instance.GameDownloadType
		};
		ThinkingPCSDKManager.getInstance().TrackEvent(TrackEvent.EVENT_LauncherGame, properties);
		StartExternalExe(GameCommon.GetGameExeFilePath());
		MainForm_Minimized();
		MainForm_NoIconInTaskBar();
		string gameExeName = Path.GetFileName(GameCommon.GetGameExeFilePath());
		if (gameRunningCheckTimer == null)
		{
			gameRunningCheckTimer = new System.Windows.Forms.Timer
			{
				Interval = 5000
			};
			gameRunningCheckTimer.Tick += (object? s, EventArgs e) =>
			{
				BluepochLogger.DebugLog("Check Game Process Exits");
				if (Process.GetProcessesByName(Path.GetFileNameWithoutExtension(gameExeName)).Length == 0)
				{
					MainForm_Activate(null, null);
					gameRunningCheckTimer.Stop();
				}
			};
		}
		if (!gameRunningCheckTimer.Enabled)
		{
			gameRunningCheckTimer.Start();
		}
	}

	private void CheckAppAndGameUpdate()
	{
		AppPlugin.GetInstance().CheckUpdateInServer();
	}

	private void StartTimeToCheckUpdate()
	{
		if (checkUpdateTimer == null)
		{
			checkUpdateTimer = new System.Windows.Forms.Timer();
			checkUpdateTimer.Interval = AppSettings.Instance.NotityTimerInterval;
			checkUpdateTimer.Tick += (object? s, EventArgs e) =>
			{
				if (!IsDownloaderAndExtraeBusy() && !isAppnNeedToUnpdate)
				{
					BluepochLogger.InfoLog("Check App & Game Update~!");
					CheckAppAndGameUpdate();
				}
			};
		}
		if (!checkUpdateTimer.Enabled)
		{
			checkUpdateTimer.Start();
		}
	}

	private void StopToCheckUpdate()
	{
		checkUpdateTimer?.Stop();
		BluepochLogger.InfoLog("Stop Check App & Game Update~!");
	}

	private void DeleteStartUpOldFiles()
	{
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
		int num = Convert.ToInt32(Resources.GameId ?? "-1");
		string text = "";
		text = ((num != 60001) ? SettingConfigManager.GetInstance().GetManifest()["app_displayname"] : "Reverse1999");
		if (File.Exists(Path.Combine(folderPath, text + ".lnk")))
		{
			try
			{
				File.Delete(Path.Combine(folderPath, text + ".lnk"));
			}
			catch (Exception ex)
			{
				BluepochLogger.ErrorLog("Delete StartUp Old Files Exception, ex:" + ex.Message);
			}
		}
	}

	private static async void DeleteQtFiles()
	{
		string text = Encoding.UTF8.GetString(Resources.qt_exe_files_to_delete);
		string[] filesString = text.Split(new string[1] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
		await Task.Run(() =>
		{
			string[] array = filesString;
			foreach (string text2 in array)
			{
				try
				{
					string path = Path.Combine(Application.StartupPath, text2.Trim());
					if (Directory.Exists(path))
					{
						FileUtil.DeleteDirectoryWithRetry(path);
					}
					if (File.Exists(path))
					{
						File.Delete(path);
					}
				}
				catch (Exception ex)
				{
					BluepochLogger.ErrorLog("DeleteQtFiles happen Exception, ex:" + ex.Message);
				}
			}
		});
	}

	private void CheckAppUpadteAfterInit()
	{
		AppPlugin.GetInstance().SetAppUpdateFromSettingDelegate((bool needToUnpdate, AppVersionInfo appVersionInfo, int errorCode) =>
		{
			if (needToUnpdate)
			{
				PostWebMessage("update-app", appVersionInfo);
				isAppnNeedToUnpdate = true;
			}
			else
			{
				isAppnNeedToUnpdate = false;
				PostWebMessage("lastest-app", appVersionInfo);
			}
		});
		AppPlugin.GetInstance().SetAppUpdateDelegate((bool needToUnpdate, AppVersionInfo appVersionInfo, ApiResult<string> apiResult) =>
		{
			if (!IsDownloaderAndExtraeBusy())
			{
				if (apiResult.code != 200)
				{
					ShowHttpErrorDialog();
				}
				else if (needToUnpdate)
				{
					isAppnNeedToUnpdate = true;
					PostWebMessage("update-app", appVersionInfo);
				}
				else
				{
					isAppnNeedToUnpdate = false;
					GamePlugin.GetInstance().CheckGameVersion(immediately: true);
				}
			}
		});
		AppPlugin.GetInstance().SetVideoUpdateDelegate((PosterVideoInfo videoVersionInfo) =>
		{
			PostWebMessage("update-video", LauncherApplication.GetInstance().GetAppSettingsInfo());
		});
		AppPlugin.GetInstance().CheckUpdateInServer();
		StartTimeToCheckUpdate();
	}

	private void NotifyMessageAfterInit()
	{
		NotificationPlugin.GetInstance().SetNotificationDelegate((NotificationRespondInfo notificationRespondInfo) =>
		{
		});
		if (NotificationPlugin.GetInstance().isNotificationsEnabled())
		{
			NotificationPlugin.GetInstance().processNewMessage();
		}
	}

	public void ClearDownloadCache()
	{
		if (multiDownloader != null)
		{
			multiDownloader.CloseDownload();
		}
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();
		foreach (string item in Directory.EnumerateFiles(Path.Combine(AppDir.GetAppCacheDir())))
		{
			if (item.EndsWith(".db"))
			{
				using DownloadDbContext downloadDbContext = new DownloadDbContext(item);
				downloadDbContext.ClearAllData();
			}
		}
	}

	private async void ClearResource()
	{
		if (IsDownloaderAndExtraeBusy())
		{
			WarningDialogInfo t = new WarningDialogInfo
			{
				message_lang = "GameDirChoose.Error_ClearInDownloading"
			};
			PostWebMessage("show-warningdialog", t);
			return;
		}
		PostWebMessage("show-deletegameresourcedialog", "");
		FileUtil.RemoveFilesBySuffix(FileUtil.GetAppDataDirPath("Downloads"), ".7z");
		FileUtil.RemoveFilesBySuffix(FileUtil.GetAppDataDirPath("Downloads"), ".7z.tmp");
		FileUtil.RemoveFilesBySuffix(FileUtil.GetAppDataDirPath("DownloadGames"), ".7z");
		FileUtil.RemoveFilesBySuffix(FileUtil.GetAppDataDirPath("DownloadGames"), ".7z.tmp");
		FileUtil.RemoveFilesBySuffix(FileUtil.GetAppDataDirPath("data"), ".db");
		FileUtil.RemoveFilesBySuffix(FileUtil.GetAppDataDirPath("data"), ".txt");
		FileUtil.DeleteDirectoryWithRetry(FileUtil.GetAppDataDirPath("DownloadGames"));
		if (!Directory.Exists(GameCommon.GetGameExeInstallDirByDefault()))
		{
			GetInstance().ClearDownloadCache();
			GetInstance().ClearDownloadGameSettingCache();
			GetInstance().PostWebMessage("deletegameresource-processed", new DeleteGameResourceInfo("", 100.0));
			return;
		}
		Progress<FileUtil.DeleteProgress> progress = new Progress<FileUtil.DeleteProgress>((FileUtil.DeleteProgress p) =>
		{
			BluepochLogger.InfoLog($"Clear Processed ：{p.Processed}%  {p.CurrentItem}");
			try
			{
				if ((int)p.Processed == 100)
				{
					GetInstance().ClearDownloadCache();
					GetInstance().ClearDownloadGameSettingCache();
				}
				DeleteGameResourceInfo t4 = new DeleteGameResourceInfo
				{
					percent = p.Processed,
					deleteItemName = p.CurrentItem
				};
				GetInstance().PostWebMessage("deletegameresource-processed", t4);
			}
			catch (Exception ex3)
			{
				Dictionary<string, object> properties2 = new Dictionary<string, object>
				{
					[TrackEvent.EVENT_Result_Msg] = ex3.Message,
					[TrackEvent.Exception_Location] = "ClearResource-1"
				};
				ThinkingPCSDKManager.getInstance().TrackEvent(TrackEvent.EVENT_Exception, properties2);
				BluepochLogger.ErrorLog("Clear Resource Exception : " + ex3.Message);
			}
		});
		try
		{
			await FileUtil.DeleteDirectoryOptimized(GameCommon.GetGameExeInstallDirByDefault(), progress);
		}
		catch (OperationCanceledException)
		{
			DeleteGameResourceInfo t2 = new DeleteGameResourceInfo
			{
				code = -1,
				message = "Delete operation canceled"
			};
			PostWebMessage("deletegameresource-processed", t2);
		}
		catch (Exception ex2)
		{
			Dictionary<string, object> properties = new Dictionary<string, object>
			{
				[TrackEvent.EVENT_Result_Msg] = ex2.Message,
				[TrackEvent.Exception_Location] = "ClearResource-2"
			};
			ThinkingPCSDKManager.getInstance().TrackEvent(TrackEvent.EVENT_Exception, properties);
			DeleteGameResourceInfo t3 = new DeleteGameResourceInfo
			{
				code = -1,
				message = ex2.Message
			};
			PostWebMessage("deletegameresource-processed", t3);
		}
	}

	public void ClearDownloadGameSettingCache()
	{
		GameCommon.ClearDownloadGameSettingCache();
		GameCommon.RecordInstallPath("");
		GameCommon.RecordInstallVersion("");
		GamePlugin.GetInstance().CheckGameVersion(immediately: true);
		GetInstance().PostWebMessage("gamedir-set", LauncherApplication.GetInstance().GetAppSettingsInfo());
	}

	public static MainForm GetInstance()
	{
		return Application.OpenForms.OfType<MainForm>().FirstOrDefault();
	}

	public static void InvokeOnUiThread(Action action)
	{
		if (Application.OpenForms.Count > 0)
		{
			Application.OpenForms[0].Invoke(action);
		}
		else
		{
			action();
		}
	}

	private bool StartExternalUpdateExe(string exePath, string arguments = "")
	{
		if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
		{
			BluepochLogger.ErrorLog("Start ExternalExe Fail：EXE's path is not here（" + exePath + "）");
			return false;
		}
		try
		{
			Process process = Process.Start(new ProcessStartInfo
			{
				FileName = exePath,
				Arguments = arguments,
				UseShellExecute = true,
				WorkingDirectory = Path.GetDirectoryName(exePath)
			});
			if (process != null)
			{
				process.WaitForInputIdle(500);
				if (process.MainWindowHandle != IntPtr.Zero)
				{
					ShowWindow(process.MainWindowHandle, 9);
					SetForegroundWindow(process.MainWindowHandle);
				}
				else
				{
					Thread.Sleep(500);
					if (process.MainWindowHandle != IntPtr.Zero)
					{
						ShowWindow(process.MainWindowHandle, 9);
						SetForegroundWindow(process.MainWindowHandle);
					}
				}
			}
			return true;
		}
		catch (Exception ex)
		{
			Dictionary<string, object> properties = new Dictionary<string, object>
			{
				[TrackEvent.EVENT_Result_Msg] = ex.Message,
				[TrackEvent.Exception_Location] = "StartExternalExe-$" + exePath
			};
			ThinkingPCSDKManager.getInstance().TrackEvent(TrackEvent.EVENT_Exception, properties);
			BluepochLogger.ErrorLog($"Start ExternalExe Fail, Exception：{ex.Message}（{exePath}）");
			return false;
		}
	}

	private bool StartExternalExe(string exePath, string arguments = "")
	{
		if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
		{
			BluepochLogger.ErrorLog("Start ExternalExe Fail：EXE's path is not here（" + exePath + "）");
			return false;
		}
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = exePath,
				Arguments = arguments,
				UseShellExecute = true,
				WorkingDirectory = Path.GetDirectoryName(exePath)
			});
			return true;
		}
		catch (Exception ex)
		{
			Dictionary<string, object> properties = new Dictionary<string, object>
			{
				[TrackEvent.EVENT_Result_Msg] = ex.Message,
				[TrackEvent.Exception_Location] = "StartExternalExe-$" + exePath
			};
			ThinkingPCSDKManager.getInstance().TrackEvent(TrackEvent.EVENT_Exception, properties);
			BluepochLogger.ErrorLog($"Start ExternalExe Fail, Exception：{ex.Message}（{exePath}）");
			return false;
		}
	}

	private void StartdownloadFile()
	{
		StopToCheckUpdate();
		DownloadByHttp();
		Dictionary<string, object> properties = new Dictionary<string, object>
		{
			[TrackEvent.GameVersion] = GamePlugin.GetInstance().GetGameVersionInfo().latestVersion,
			[TrackEvent.GameDwonloadType] = AppSettings.Instance.GameDownloadType
		};
		ThinkingPCSDKManager.getInstance().TrackEvent(TrackEvent.EVENT_DownloadGame_Start, properties);
	}

	private void DownloadByHttp()
	{
		if (multiDownloader == null)
		{
			multiDownloader = DownloaderFactory.GetInstance().GetGameDownloader(GamePlugin.GetInstance().GetGameVersionInfo());
			BluepochLogger.InfoLog("Start Download File By " + multiDownloader.GetType().FullName);
			multiDownloader.progressPerSecondCallback = (long bytesReceived, double progress, double speedBytesPerSecond, long totleSize) =>
			{
				DownloadProgressCallback(bytesReceived, progress, speedBytesPerSecond, totleSize);
			};
			multiDownloader.downloadFinishCallback = () =>
			{
				BluepochLogger.InfoLog("Download task completed");
				Dictionary<string, object> properties = new Dictionary<string, object>
				{
					[TrackEvent.GameVersion] = GamePlugin.GetInstance().GetGameVersionInfo().latestVersion,
					[TrackEvent.GameDwonloadType] = AppSettings.Instance.GameDownloadType
				};
				ThinkingPCSDKManager.getInstance().TrackEvent(TrackEvent.EVENT_DownloadGame_Finish, properties);
				PostWebMessage<object>("downloadstatus-complete", null);
				GameCommon.UpdateGameStatusInCache(6);
				ExtractZipFile();
			};
			multiDownloader.downloadPauseCallback = () =>
			{
				StartTimeToCheckUpdate();
				GamePlugin.GetInstance().ComputerDownloadDirSize((DownloadingInfo downloadingInfo) =>
				{
					PostWebMessage("downloadstatus-pause", new DownloadingInfo());
				});
			};
			multiDownloader.downloadStartCallback = (long bytesReceived, double progress, double speedBytesPerSecond, long totleSize) =>
			{
				DownloadingInfo t = new DownloadingInfo();
				PostWebMessage("downloadstatus-start", t);
			};
			multiDownloader.downloadErrorCallback = (string taskId, string msg) =>
			{
				StartTimeToCheckUpdate();
				PostWebMessage("downloadstatus-pause", new DownloadingInfo());
				WarningDialogInfo t = new WarningDialogInfo
				{
					message_lang = msg
				};
				PostWebMessage("show-warningdialog", t);
			};
		}
		multiDownloader.StartDownload(GamePlugin.GetInstance().GetGameVersionInfo());
	}

	private void StopDownload()
	{
		multiDownloader.StopDownload();
	}

	private void DownloadProgressCallback(long bytesReceived, double p, double speedBytesPerSecond, long totleSize)
	{
		DownloadingInfo t = new DownloadingInfo
		{
			percent = p,
			strSpeed = LauncherUtils.ConvertBytesPerSecondToReadable(speedBytesPerSecond),
			totleSize = LauncherUtils.ConvertBytesToReadable(totleSize),
			timeRemain = LauncherUtils.FormatTimeFromSeconds((long)((double)(totleSize - bytesReceived) / speedBytesPerSecond)),
			currentSize = LauncherUtils.ConvertBytesToReadable(bytesReceived)
		};
		PostWebMessage("downloadstatus-precess", t);
	}

	private void ExtractZipFile()
	{
		Dictionary<string, object> properties = new Dictionary<string, object>
		{
			[TrackEvent.GameVersion] = GamePlugin.GetInstance().GetGameVersionInfo().latestVersion,
			[TrackEvent.GameDwonloadType] = AppSettings.Instance.GameDownloadType
		};
		ThinkingPCSDKManager.getInstance().TrackEvent(TrackEvent.EVENT_Install_Start, properties);
		StopToCheckUpdate();
		if (multiExtractTool == null)
		{
			multiExtractTool = DownloaderFactory.GetInstance().GetGameExtractTool(GamePlugin.GetInstance().GetGameVersionInfo());
			BluepochLogger.InfoLog("Start Extract File By " + multiExtractTool.GetType().FullName);
			UnzipingInfo unzipInfo = new UnzipingInfo(0.0);
			multiExtractTool.multiUnzipStartCallback = () =>
			{
				LauncherApplication.GetInstance().InvokeOnUiThread(() =>
				{
					GameCommon.UpdateGameStatusInCache(7);
					unzipInfo.percent = 0.0;
					PostWebMessage("unzipfile-start", (object)new UnzipingInfo(0.0));
				});
			};
			multiExtractTool.multiUnZipFinishCallback = () =>
			{
				LauncherApplication.GetInstance().InvokeOnUiThread(() =>
				{
					InstallGameFinish();
				});
			};
			multiExtractTool.multiUnZipProgressCallback = (double progress) =>
			{
				LauncherApplication.GetInstance().InvokeOnUiThread(() =>
				{
					GameCommon.UpdateGameStatusInCache(7);
					BluepochLogger.DebugLog($"unzip-progress ：{progress}%");
					unzipInfo.percent = progress;
					PostWebMessage("unzipfile-progress", unzipInfo);
				});
			};
			multiExtractTool.multiUnzipErrorCallback = (string taskId, string message) =>
			{
				LauncherApplication.GetInstance().InvokeOnUiThread(() =>
				{
					WarningDialogInfo t = new WarningDialogInfo
					{
						message_lang = "ZlibHelper.ExeError"
					};
					PostWebMessage("show-warningdialog", t);
					GameCommon.UpdateGameStatusInCache(1);
					GamePlugin.GetInstance().ComputerDownloadDirSize((DownloadingInfo downloadingInfo) =>
					{
						PostWebMessage("gamestatus-downloading", downloadingInfo);
					});
					CleanDownloaderAndExtractTool();
				});
			};
		}
		multiExtractTool.StartExtract(GamePlugin.GetInstance().GetGameVersionInfo());
	}

	private void InstallGameFinish()
	{
		StartTimeToCheckUpdate();
		GameCommon.UpdateGameStatusInCache(5);
		GameCommon.RecordInstallVersion(GamePlugin.GetInstance().GetGameVersionInfo().latestVersion);
		GameCommon.RecordInstallPath(GameCommon.GetGameExeInstallDirByDefault());
		PostWebMessage("unzipfile-complete", LauncherApplication.GetInstance().GetAppSettingsInfo());
		Dictionary<string, object> properties = new Dictionary<string, object>
		{
			[TrackEvent.GameVersion] = GamePlugin.GetInstance().GetGameVersionInfo().latestVersion,
			[TrackEvent.GameDwonloadType] = AppSettings.Instance.GameDownloadType
		};
		ThinkingPCSDKManager.getInstance().TrackEvent(TrackEvent.EVENT_Install_Finish, properties);
		CleanDownloaderAndExtractTool();
	}

	private void CleanDownloaderAndExtractTool()
	{
		if (multiExtractTool != null)
		{
			multiExtractTool.StopExtract();
			multiExtractTool = null;
		}
		if (multiDownloader != null)
		{
			multiDownloader.CloseDownload();
			multiDownloader = null;
		}
	}

	private void MainForm_ShutDown()
	{
		if (gameRunningCheckTimer != null)
		{
			gameRunningCheckTimer.Stop();
			gameRunningCheckTimer.Dispose();
			gameRunningCheckTimer = null;
		}
		if (multiExtractTool != null && multiExtractTool.IsWorking)
		{
			multiExtractTool.StopExtract();
		}
		if (notifyIcon != null)
		{
			notifyIcon.Visible = false;
			notifyIcon.Dispose();
			notifyIcon = null;
		}
		if (NotificationPlugin.GetInstance() != null)
		{
			NotificationPlugin.GetInstance().StopTimeToUpdateNotifications();
		}
		BluepochLogger.Shutdown();
		GC.Collect();
		GC.WaitForPendingFinalizers();
		Application.Exit();
	}

	private void MainForm_Shown(object sender, EventArgs e)
	{
		_ = DateTime.Now - Program.StartTime;
	}

	private void MainForm_Activate(object sender, EventArgs e)
	{
		StartTimeToCheckUpdate();
		WindowState = FormWindowState.Normal;
		ShowInTaskbar = true;
		BringToFront();
		Activate();
	}

	private void OnWindowSizeChanged(object sender, EventArgs e)
	{
		BluepochLogger.InfoLog($"WindowState changed: {WindowState}");
		if (webView.CoreWebView2 != null)
		{
			if (WindowState == FormWindowState.Minimized)
			{
				webView.CoreWebView2.ExecuteScriptAsync("document.querySelector('video')?.pause()");
			}
			else
			{
				webView.CoreWebView2.ExecuteScriptAsync("document.querySelector('video')?.play()");
			}
		}
	}

	private void MainForm_Minimized()
	{
		WindowState = FormWindowState.Minimized;
	}

	private void MainForm_NoIconInTaskBar()
	{
		StopToCheckUpdate();
		ShowInTaskbar = false;
	}

	private void ShowHttpErrorDialog()
	{
		WarningDialogInfo t = new WarningDialogInfo
		{
			message_lang = "Network has been broken."
		};
		PostWebMessage("show-warningdialog", t);
	}

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetWindowRect(nint hWnd, ref Rectangle lpRect);

	public Size GetRealPixelSize()
	{
		Rectangle lpRect = default;
		GetWindowRect(Handle, ref lpRect);
		return new Size(lpRect.Width - lpRect.X, lpRect.Height - lpRect.Y);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null)
		{
			components.Dispose();
		}
		base.Dispose(disposing);
	}

	private void InitializeComponent()
	{
		int width = 1280;
		int height = 720;
		this.bgPicture = new System.Windows.Forms.PictureBox();
		this.webView = new Microsoft.Web.WebView2.WinForms.WebView2();
		((System.ComponentModel.ISupportInitialize)this.bgPicture).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.webView).BeginInit();
		base.SuspendLayout();
		this.bgPicture.Location = new System.Drawing.Point(0, 0);
		this.bgPicture.Name = "bgPicture";
		this.bgPicture.Size = new System.Drawing.Size(width, height);
		this.bgPicture.TabIndex = 0;
		this.bgPicture.TabStop = false;
		this.webView.AccessibleName = "webView";
		this.webView.AllowExternalDrop = true;
		this.webView.CreationProperties = null;
		this.webView.DefaultBackgroundColor = System.Drawing.Color.Transparent;
		this.webView.Location = new System.Drawing.Point(0, 0);
		this.webView.Margin = new System.Windows.Forms.Padding(0);
		this.webView.Name = "webView";
		this.webView.Size = new System.Drawing.Size(width, height);
		this.webView.TabIndex = 0;
		this.webView.ZoomFactor = 1.0;
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
		base.ClientSize = new System.Drawing.Size(width, height);
		base.Controls.Add(this.webView);
		base.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
		base.Load += new System.EventHandler(this.MainForm_OnLoad);
		base.Shown += new System.EventHandler(this.MainForm_Shown);
		((System.ComponentModel.ISupportInitialize)this.bgPicture).EndInit();
		((System.ComponentModel.ISupportInitialize)this.webView).EndInit();
		base.ResumeLayout(false);
	}

	private void MainForm_OnLoad(object sender, EventArgs e)
	{
	}
}
