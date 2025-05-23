using UnityEngine;
using System.Diagnostics;

public class ExternalProcessLauncher : MonoBehaviour
{
    public string scriptPath = "Python/launch_installation.sh";

    void Start()
    {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{Application.dataPath}/{scriptPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            Process process = new Process { StartInfo = psi };
            process.Start();

            UnityEngine.Debug.Log("🟢 Launched: " + scriptPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("❌ Failed to launch script: " + e.Message);
        }
#endif
    }
}
