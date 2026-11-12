using System;

namespace Bluepoch.Launcher.Source.Bean;

[Serializable]
internal class WarningDialogInfo
{
	public string buttonAction = "";

	public string title = "";

	public string message_lang = "";

	public WarningDialogInfo()
	{
	}

	public WarningDialogInfo(string buttonAction, string title, string message_lang)
	{
		this.buttonAction = buttonAction;
		this.title = title;
		this.message_lang = message_lang;
	}
}
