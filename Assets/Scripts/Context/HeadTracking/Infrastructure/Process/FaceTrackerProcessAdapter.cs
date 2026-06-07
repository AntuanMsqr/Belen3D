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
        private readonly string _exePath;
        private readonly string _workingDir;
        private readonly string _arguments;
        private readonly bool _showWindow;

        private Process _proc;

        public FaceTrackerProcessAdapter(string exePath, string arguments, bool showWindow, string workingDir = null)
        {
            _exePath = exePath;
            _arguments = arguments ?? string.Empty;
            _showWindow = showWindow;
            _workingDir = string.IsNullOrEmpty(workingDir)
                ? Path.GetDirectoryName(exePath)
                : workingDir;
        }

        public bool IsRunning
        {
            get
            {
                try { return _proc != null && !_proc.HasExited; }
                catch { return false; }
            }
        }

        public void Start()
        {
            if (IsRunning) return;

            if (string.IsNullOrEmpty(_exePath) || !File.Exists(_exePath))
            {
                Debug.LogError($"[FaceTrackerProcess] Executable not found: '{_exePath}'. " +
                               "Set the path on the HeadTrackingConfig asset.");
                return;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _exePath,
                    Arguments = _arguments,
                    WorkingDirectory = _workingDir,
                    UseShellExecute = false,
                    CreateNoWindow = !_showWindow
                };
                _proc = Process.Start(psi);
                if (_proc != null)
                    Debug.Log($"[FaceTrackerProcess] Started '{_exePath}' {_arguments} (pid {_proc.Id})");
            }
            catch (Exception e)
            {
                Debug.LogError($"[FaceTrackerProcess] Failed to start '{_exePath}': {e.Message}");
                _proc = null;
            }
        }

        public void Stop()
        {
            if (_proc == null) return;

            try
            {
                if (!_proc.HasExited)
                {
                    // PyInstaller spawns children; kill the whole tree to avoid orphans.
                    // taskkill /T is the reliable cross-runtime way on Windows
                    // (Process.Kill(bool) isn't guaranteed under Unity's .NET Standard profile).
                    TaskkillTree(_proc.Id);
                    if (!_proc.WaitForExit(1500))
                    {
                        try { _proc.Kill(); } catch { /* ignore */ }
                        _proc.WaitForExit(1000);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FaceTrackerProcess] Error while stopping: {e.Message}");
            }
            finally
            {
                try { _proc.Dispose(); } catch { /* ignore */ }
                _proc = null;
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
