using System;
using System.Threading;
using System.Windows.Forms;
using Bluepoch.Launcher.Source.Game;

namespace Bluepoch.Launcher.Source.Download;

internal abstract class DownloadInterface
{
	public delegate void ProgressPerSecondCallback(long bytesReceived, double progress, double speedBytesPerSecond, long totleSize = 0L);

	public delegate void MultiDownloadFinishCallback();

	public delegate void MultiDownloadErrorCallback(string taskId, string msg);

	public delegate void MultiDownloadPauseCallback();

	public delegate void MultiDownloadStartCallback(long bytesReceived, double progress, double speedBytesPerSecond, long totleSize = 0L);

	public bool Is_ByteWriteToFile;

	protected long bytesTotal;

	protected long bytesReceived;

	protected long bytesZip;

	protected long bytesLastSecond;

	protected GameVersionInfo m_gameVersionInfo;

	protected int maxConcurrency = Math.Min(15, Environment.ProcessorCount * 2 / 3);

	protected int downloadCompleteCount;

	protected int downloadTotalCount;

	protected CancellationTokenSource _downloadCts;

	public bool Is_Downloading { get; set; }

	public ProgressPerSecondCallback progressPerSecondCallback { get; set; }

	public MultiDownloadFinishCallback downloadFinishCallback { get; set; }

	public MultiDownloadErrorCallback downloadErrorCallback { get; set; }

	public MultiDownloadPauseCallback downloadPauseCallback { get; set; }

	public MultiDownloadStartCallback downloadStartCallback { get; set; }

	public abstract void StartDownload(GameVersionInfo gameVersionInfo);

	public abstract void Reset();

	public abstract void PreStart();

	public abstract void StopDownload();

	public abstract void CloseDownload();

	public void InvokeOnUiThread(Action action)
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
}
