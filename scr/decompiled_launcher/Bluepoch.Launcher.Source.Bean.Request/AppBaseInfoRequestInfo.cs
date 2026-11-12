using Bluepoch.Launcher.App;
using Bluepoch.Launcher.Properties;

namespace Bluepoch.Launcher.Source.Bean.Request;

internal class AppBaseInfoRequestInfo : ApiRequestBean
{
	public string gameId;

	public int envType;

	public AppBaseInfoRequestInfo()
	{
		gameId = Resources.GameId;
		int num = LauncherApplication.GetInstance().GetRunningType();
		if (num == 3)
		{
			num = 4;
		}
		envType = num;
	}
}
