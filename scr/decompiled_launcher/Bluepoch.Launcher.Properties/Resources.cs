using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

namespace Bluepoch.Launcher.Properties;

[GeneratedCode("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
[DebuggerNonUserCode]
[CompilerGenerated]
internal class Resources
{
	private static ResourceManager resourceMan;

	private static CultureInfo resourceCulture;

	[EditorBrowsable(EditorBrowsableState.Advanced)]
	internal static ResourceManager ResourceManager
	{
		get
		{
			if (resourceMan == null)
			{
				resourceMan = new ResourceManager("Bluepoch.Launcher.Properties.Resources", typeof(Resources).Assembly);
			}
			return resourceMan;
		}
	}

	[EditorBrowsable(EditorBrowsableState.Advanced)]
	internal static CultureInfo Culture
	{
		get
		{
			return resourceCulture;
		}
		set
		{
			resourceCulture = value;
		}
	}

	internal static string AppBuildCode => ResourceManager.GetString("AppBuildCode", resourceCulture);

	internal static string AppIcon => ResourceManager.GetString("AppIcon", resourceCulture);

	internal static string Font_Name_Cn => ResourceManager.GetString("Font_Name_Cn", resourceCulture);

	internal static string Font_Name_CnTW => ResourceManager.GetString("Font_Name_CnTW", resourceCulture);

	internal static string Font_Name_En => ResourceManager.GetString("Font_Name_En", resourceCulture);

	internal static string Font_Name_Jp => ResourceManager.GetString("Font_Name_Jp", resourceCulture);

	internal static string Font_Name_Kr => ResourceManager.GetString("Font_Name_Kr", resourceCulture);

	internal static string GameId => ResourceManager.GetString("GameId", resourceCulture);

	internal static byte[] launchermanifest_pjm_cn => (byte[])ResourceManager.GetObject("launchermanifest-pjm-cn", resourceCulture);

	internal static byte[] launchermanifest_pjm_en => (byte[])ResourceManager.GetObject("launchermanifest-pjm-en", resourceCulture);

	internal static byte[] launchermanifest_pjm_jp => (byte[])ResourceManager.GetObject("launchermanifest-pjm-jp", resourceCulture);

	internal static byte[] launchermanifest_pjm_kr => (byte[])ResourceManager.GetObject("launchermanifest-pjm-kr", resourceCulture);

	internal static byte[] qt_exe_files_to_delete => (byte[])ResourceManager.GetObject("qt_exe_files_to_delete", resourceCulture);

	internal Resources()
	{
	}
}
