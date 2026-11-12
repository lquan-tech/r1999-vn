using System;
using Bluepoch.Launcher.Source.Game;

namespace Bluepoch.Launcher.Source.Download;

internal abstract class ExtractInterface
{
	public delegate void MultiUnZipProgressCallback(double progress);

	public delegate void MultiUnZipFinishCallback();

	public delegate void MultiUnzipErrorCallback(string taskId, string msg);

	public delegate void MultiUnzipPauseCallback();

	public delegate void MultiUnzipStartCallback();

	protected int maxConcurrency = Math.Min(15, Environment.ProcessorCount * 2 / 3);

	public MultiUnZipProgressCallback multiUnZipProgressCallback { get; set; }

	public MultiUnZipFinishCallback multiUnZipFinishCallback { get; set; }

	public MultiUnzipErrorCallback multiUnzipErrorCallback { get; set; }

	public MultiUnzipPauseCallback multiUnzipPauseCallback { get; set; }

	public MultiUnzipStartCallback multiUnzipStartCallback { get; set; }

	public bool IsWorking { get; set; }

	public abstract void StartExtract(GameVersionInfo gameVersionInfo);

	public abstract void StopExtract();
}
