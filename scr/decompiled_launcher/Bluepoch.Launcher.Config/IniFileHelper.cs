using System.Runtime.InteropServices;
using System.Text;

namespace Bluepoch.Launcher.Config;

public class IniFileHelper
{
	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetPrivateProfileString(string section, string key, string defaultValue, StringBuilder retVal, int size, string filePath);

	public static string ReadValue(string filePath, string section, string key, string defaultValue = "")
	{
		StringBuilder stringBuilder = new StringBuilder(255);
		GetPrivateProfileString(section, key, defaultValue, stringBuilder, 255, filePath);
		return stringBuilder.ToString();
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	public static extern int WritePrivateProfileString(string section, string key, string value, string filePath);

	public static void WriteValue(string filePath, string section, string key, string value)
	{
		WritePrivateProfileString(section, key, value, filePath);
	}
}
