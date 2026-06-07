using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Hcp.HeadTracking.Domain;
using Debug = UnityEngine.Debug;

namespace Hcp.HeadTracking.Infrastructure
{
    // Launches and stops the external OpenSeeFace tracker (facetracker.exe) via System.Diagnostics.Process.
    // The exe is a PyInstaller bundle: it MUST run with its own folder as WorkingDirectory so the
    // bundled python/onnxruntime/opencv DLLs resolve, and its child processes must be killed too.
    public sealed class FaceTrackerProcessAdapter : ITrackerProcess
    {
        private readonly string exePath;
        private readonly string workingDir;
        private readonly string arguments;
        private readonly bool showWindow;

        private Process proc;

        public FaceTrackerProcessAdapter(string exePath, string arguments, bool showWindow, string workingDir = null)
        {
            this.exePath = exePath;
            this.arguments = arguments ?? string.Empty;
            this.showWindow = showWindow;
            this.workingDir = string.IsNullOrEmpty(workingDir)
                ? Path.GetDirectoryName(exePath)
                : workingDir;
        }

        public bool IsRunning
        {
            get
            {
                try { return proc != null && !proc.HasExited; }
                catch { return false; }
            }
        }

        public void Start()
        {
            if (IsRunning) return;

            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                Debug.LogError($"[FaceTrackerProcess] Executable not found: '{exePath}'. " +
                               "Set the path on the HeadTrackingConfig asset.");
                return;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = arguments,
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    CreateNoWindow = !showWindow
                };
                proc = Process.Start(psi);
                if (proc != null)
                    Debug.Log($"[FaceTrackerProcess] Started '{exePath}' {arguments} (pid {proc.Id})");
            }
            catch (Exception e)
            {
                Debug.LogError($"[FaceTrackerProcess] Failed to start '{exePath}': {e.Message}");
                proc = null;
            }
        }

        public void Stop()
        {
            if (proc == null) return;

            try
            {
                if (!proc.HasExited)
                {
                    // PyInstaller spawns children; kill the whole tree to avoid orphans.
                    // taskkill /T is the reliable cross-runtime way on Windows
                    // (Process.Kill(bool) isn't guaranteed under Unity's .NET Standard profile).
                    TaskkillTree(proc.Id);
                    if (!proc.WaitForExit(1500))
                    {
                        try { proc.Kill(); } catch { /* ignore */ }
                        proc.WaitForExit(1000);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FaceTrackerProcess] Error while stopping: {e.Message}");
            }
            finally
            {
                try { proc.Dispose(); } catch { /* ignore */ }
                proc = null;
            }
        }

        private static void TaskkillTree(int pid)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = $"/PID {pid} /T /F",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(psi)?.WaitForExit(2000);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FaceTrackerProcess] taskkill fallback failed: {e.Message}");
            }
        }
    }
}
