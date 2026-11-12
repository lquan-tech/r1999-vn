using System.Collections.Generic;

namespace Bluepoch.Launcher.Source.Utils;

internal static class EnumerableExtensions
{
	public static IEnumerable<IEnumerable<string>> Batch(this IEnumerable<string> source, int batchSize)
	{
		List<string> list = new List<string>(batchSize);
		foreach (string item in source)
		{
			list.Add(item);
			if (list.Count >= batchSize)
			{
				yield return list;
				list = new List<string>(batchSize);
			}
		}
		if (list.Count > 0)
		{
			yield return list;
		}
	}
}
