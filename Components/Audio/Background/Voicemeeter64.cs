using System.Runtime.InteropServices;

namespace StreamAssist.Components.Audio.Background;



public sealed class Voicemeeter64
{
    private const string DLL = "VoicemeeterRemote64.dll";



    [DllImport(DLL, EntryPoint = "VBVMR_Login")]
    public static extern int VBVMR_Login();



    [DllImport(DLL, EntryPoint = "VBVMR_Logout")]
    public static extern int VBVMR_Logout();



    [DllImport(DLL, EntryPoint = "VBVMR_SetParameterFloat", CharSet = CharSet.Ansi)]
    public static extern int VBVMR_SetParameterFloat(string paramName, float value);



    [DllImport(DLL, EntryPoint = "VBVMR_GetParameterFloat", CharSet = CharSet.Ansi)]
    public static extern int VBVMR_GetParameterFloat(string paramName, ref float value);



    // refreshes the values GetParameterFloat gives (Voicemeeter wants it called before reading), 0 or 1 = fine
    [DllImport(DLL, EntryPoint = "VBVMR_IsParametersDirty")]
    public static extern int VBVMR_IsParametersDirty();



    // which edition is running: 1 = Voicemeeter, 2 = Banana, 3 = Potato
    [DllImport(DLL, EntryPoint = "VBVMR_GetVoicemeeterType")]
    public static extern int VBVMR_GetVoicemeeterType(ref int type);



    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool SetDllDirectory(string lpPathName);
}
