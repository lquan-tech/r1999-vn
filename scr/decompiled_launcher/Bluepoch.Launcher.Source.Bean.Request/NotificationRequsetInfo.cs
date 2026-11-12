using Bluepoch.Launcher.Properties;

namespace Bluepoch.Launcher.Source.Bean.Request;

internal class NotificationRequsetInfo : ApiRequestBean
{
	public string gameId;

	public NotificationRequsetInfo()
	{
		gameId = Resources.GameId;
	}
}
