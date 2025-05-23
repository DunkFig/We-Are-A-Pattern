using UnityEngine;
using System.Diagnostics;
using System.IO;

public class PythonBridgeLauncher : MonoBehaviour
{
    [Header("Python Bridge Settings")]
    [Tooltip("Which python executable to call (e.g. python3 or python)")]
    public string pythonExecutable = "python3";
    [Tooltip("Relative to Application.dataPath")]
    public string scriptRelativePath = "StreamingAssets/Python/midi_serial_bridge.py";

    private Process _bridgeProcess;

    void Awake()
    {
        LaunchPythonBridge();
    }

    void LaunchPythonBridge()
    {
        // Construct the full path to the script
        var scriptPath = Path.Combine(Application.dataPath, scriptRelativePath);

        if (!File.Exists(scriptPath))
        {
            UnityEngine.Debug.LogError($"❌ Python script not found at: {scriptPath}");
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = pythonExecutable,
            Arguments = $"\"{scriptPath}\"",
            WorkingDirectory = Path.GetDirectoryName(scriptPath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        try
        {
            _bridgeProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _bridgeProcess.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) UnityEngine.Debug.Log(e.Data); };
            _bridgeProcess.ErrorDataReceived  += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) UnityEngine.Debug.LogError(e.Data); };
            _bridgeProcess.Start();
            _bridgeProcess.BeginOutputReadLine();
            _bridgeProcess.BeginErrorReadLine();

            UnityEngine.Debug.Log($"✅ Launched Python bridge: {pythonExecutable} {psi.Arguments}");
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"❌ Failed to launch Python bridge: {ex.Message}");
        }
    }

    void OnApplicationQuit()
    {
        // Clean up
        if (_bridgeProcess != null && !_bridgeProcess.HasExited)
        {
            _bridgeProcess.Kill();
            _bridgeProcess.WaitForExit();
            UnityEngine.Debug.Log("🔌 Python bridge shut down.");
        }
    }
}
