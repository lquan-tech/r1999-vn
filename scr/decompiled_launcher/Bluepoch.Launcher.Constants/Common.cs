using System.Collections.Generic;

namespace Bluepoch.Launcher.Constants;

public static class Common
{
	public const string Config_File_EN = "config.ini";

	public const string Config_File_CN = "config_cn.ini";

	public const string Config_File_JP = "config_jp.ini";

	public const string Config_File_KR = "config_kr.ini";

	public static Dictionary<int, string> WebView2RuntimeTips = new Dictionary<int, string>
	{
		[0] = "需要安装WebView2 Runtime以正常运行启动器。点击确认前往",
		[1] = "Webview 2 Runtime is required to start the launcher. Proceed to Install",
		[2] = "ランチャーの起動にはWebView2\u00a0Runtimeをインストールすることが必要です。「確認」をクリックして、WebView2\u00a0Runtimeをインストールしてください。",
		[3] = "Webview 2 Runtime is required to start the launcher. Proceed to Install",
		[4] = "Webview 2 Runtime is required to start the launcher. Proceed to Install"
	};

	public static Dictionary<int, string> LocalDialogTitle = new Dictionary<int, string>
	{
		[0] = "提示",
		[1] = "Notice",
		[2] = "確認",
		[3] = "확인",
		[4] = "確認"
	};

	public static Dictionary<int, string> LocalDialogConfirmButtonText = new Dictionary<int, string>
	{
		[0] = "确认",
		[1] = "Confirm",
		[2] = "確認",
		[3] = "확인",
		[4] = "確認"
	};

	public static Dictionary<int, string> LogUploadingTips = new Dictionary<int, string>
	{
		[0] = "正在上传中，请稍后再试。",
		[1] = "Network error. Upload failed.",
		[2] = "ネットワークエラー、アップロードできません。",
		[3] = "네트워크 오류, 업로드할 수 없습니다.",
		[4] = "網路異常，無法上傳。"
	};

	public static Dictionary<int, string> LogUploadNetworkErrorTips = new Dictionary<int, string>
	{
		[0] = "网络异常，无法上传。",
		[1] = "Network error. Upload failed.",
		[2] = "ネットワークエラー、アップロードできません。",
		[3] = "네트워크 오류, 업로드할 수 없습니다.",
		[4] = "網路異常，無法上傳。"
	};

	public static Dictionary<int, string> LogUploadNetworkTimeoutTips = new Dictionary<int, string>
	{
		[0] = "连接超时，请检查网络后重试。",
		[1] = "Connection timeout. Please check your network and retry.",
		[2] = "接続タイムアウト。接続状況を確認してからもう一度お試しください。",
		[3] = "연결 시간이 초과되었습니다. 네트워크 상태 확인 후 다시 시도해 주세요.",
		[4] = "連線逾時，請檢查網路後重試。"
	};

	public static Dictionary<int, string> LogUploadSuccessTips = new Dictionary<int, string>
	{
		[0] = "日志上传成功，请将反馈编号提供给客服。",
		[1] = "Log uploaded successfully. Please provide the following support ticket ID to customer service.",
		[2] = "ログのアップロードに成功しました。\nフィードバックIDをカスタマーサポートまでお知らせください。",
		[3] = "로그 업로드 성공! 문의 번호를 고객센터에 전달해 주세요.",
		[4] = "日誌上傳成功，請將回饋編號提供給客服。"
	};

	public static Dictionary<int, string> CopyTicketIdTips = new Dictionary<int, string>
	{
		[0] = "（已复制编号到剪贴板）",
		[1] = "(Ticket ID copied to clipboard)",
		[2] = "（コピーしました）",
		[3] = "(클립보드에 번호가 복사되었습니다.)",
		[4] = "（已複製編號到剪貼簿）"
	};

	public static Dictionary<int, string> LogUploadCosVerifyError = new Dictionary<int, string>
	{
		[0] = "上传校验失败，无法上传:",
		[1] = "Upload verification failed. Cannot upload:",
		[2] = "アップロードの検証に失敗したため、アップロードできません：",
		[3] = "업로드 검증에 실패하여 업로드할 수 없습니다:",
		[4] = "上傳驗證失敗，無法上傳:"
	};

	public static string GetConfigFileByGameId(int gameId)
	{
		return gameId switch
		{
			50001 => "config_cn.ini", 
			60001 => "config.ini", 
			70001 => "config_jp.ini", 
			90001 => "config_kr.ini", 
			_ => "config.ini", 
		};
	}
}
