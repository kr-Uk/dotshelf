using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DotShelf.EditorTools
{
    public sealed class DotShelfLauncherPublishWindow : EditorWindow
    {
        private string _newVersion = "";
        private string _releaseNotes = "";
        private string _resultLog = "";
        private bool _isBusy;
        private Vector2 _logScroll;

        [MenuItem("Tools/DotShelf/Build & Publish Launcher Package")]
        private static void Open()
        {
            var window = GetWindow<DotShelfLauncherPublishWindow>(true, "Launcher Publish", true);
            window.minSize = new Vector2(540, 480);
            window.PrefillVersion();
            window.Show();
        }

        private void PrefillVersion()
        {
            try
            {
                var path = ManifestPath();
                if (File.Exists(path))
                {
                    var match = Regex.Match(
                        File.ReadAllText(path),
                        "\"latestVersion\"\\s*:\\s*\"(\\d+)\\.(\\d+)\\.(\\d+)\"");
                    if (match.Success)
                    {
                        var patch = int.Parse(match.Groups[3].Value) + 1;
                        _newVersion = $"{match.Groups[1].Value}.{match.Groups[2].Value}.{patch}";
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LauncherPublish] manifest prefill 실패 — {e.Message}");
            }
            _newVersion = "0.1.1";
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("DotShelf Unity Launcher Package", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "사전 조건: dev 브랜치 위 + clean working tree.\n" +
                "(master / feat 브랜치 위에서 누르면 release 자동화가 거절됩니다.)\n" +
                "\n" +
                "1) StandaloneWindows64 빌드\n" +
                "2) launcher/scripts/publish-unity.ps1 실행\n" +
                "    - Pre-flight: dev 브랜치 + clean tree + 신규 feat 브랜치 사용 가능 여부\n" +
                "    - bump-version.ps1 로 ProjectSettings / manifest / download page 동기 갱신\n" +
                "    - Build/Windows/* → Dotshelf-<ver>.zip + sha256\n" +
                "    - GitLab Generic Packages 로 PUT 업로드\n" +
                "    - feat/launcher/unity-<ver> 브랜치 commit + push + MR (dev 타깃)\n" +
                "환경변수 GITLAB_TOKEN 필수 (lab.ssafy.com PAT, scope=api).",
                MessageType.Info);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField($"현재 bundleVersion : {PlayerSettings.bundleVersion}");
            _newVersion = EditorGUILayout.TextField("새 Version (X.Y.Z)", _newVersion);

            EditorGUILayout.LabelField("Release Notes (한 줄 = 한 bullet)");
            _releaseNotes = EditorGUILayout.TextArea(_releaseNotes, GUILayout.MinHeight(120));

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_isBusy))
            {
                if (GUILayout.Button(_isBusy ? "Building..." : "Build & Publish",
                        GUILayout.Height(36)))
                {
                    EditorApplication.delayCall += BuildAndPublish;
                }
            }

            if (!string.IsNullOrEmpty(_resultLog))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Result", EditorStyles.boldLabel);
                _logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.MinHeight(160));
                EditorGUILayout.TextArea(_resultLog, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
        }

        private void BuildAndPublish()
        {
            if (!Regex.IsMatch(_newVersion ?? "", "^\\d+\\.\\d+\\.\\d+$"))
            {
                EditorUtility.DisplayDialog(
                    "Invalid version",
                    "Version 은 X.Y.Z (semver) 형식이어야 합니다.",
                    "OK");
                return;
            }
            if (string.IsNullOrWhiteSpace(_releaseNotes) &&
                !EditorUtility.DisplayDialog(
                    "Release notes 비어있음",
                    "Release notes 비어있는 채로 진행하시겠습니까?",
                    "예", "취소"))
            {
                return;
            }

            _isBusy = true;
            Repaint();
            try
            {
                var repoRoot = RepoRoot();
                var buildDir = Path.Combine(repoRoot, "frontend", "Build", "Windows");

                if (Directory.Exists(buildDir)) Directory.Delete(buildDir, true);
                Directory.CreateDirectory(buildDir);

                var scenes = EditorBuildSettings.scenes
                    .Where(s => s.enabled)
                    .Select(s => s.path)
                    .ToArray();
                if (scenes.Length == 0)
                {
                    EditorUtility.DisplayDialog(
                        "Build Settings",
                        "EditorBuildSettings 에 enabled 된 씬이 없습니다.",
                        "OK");
                    return;
                }

                // EXE 메타에 새 버전이 박히도록 BuildPlayer 직전에 bundleVersion 임시 갱신.
                // 빌드 후엔 옛 값으로 되돌려놔야 publish-unity.ps1 → bump-version.ps1 의
                // ProjectSettings 정규식 patch 가 매치된다.
                var savedBundleVersion = PlayerSettings.bundleVersion;
                PlayerSettings.bundleVersion = _newVersion;
                AssetDatabase.SaveAssets();

                BuildReport report;
                try
                {
                    report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                    {
                        scenes = scenes,
                        locationPathName = Path.Combine(buildDir, "Dotshelf.exe"),
                        target = BuildTarget.StandaloneWindows64,
                        targetGroup = BuildTargetGroup.Standalone,
                        options = BuildOptions.None,
                    });
                }
                finally
                {
                    PlayerSettings.bundleVersion = savedBundleVersion;
                    AssetDatabase.SaveAssets();
                }
                if (report.summary.result != BuildResult.Succeeded)
                {
                    _resultLog = $"Unity Build 실패 — result={report.summary.result}, " +
                                 $"errors={report.summary.totalErrors}";
                    Debug.LogError(_resultLog);
                    return;
                }

                var scriptPath = Path.Combine(repoRoot, "launcher", "scripts", "publish-unity.ps1");
                var notesB64 = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(_releaseNotes ?? ""));

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments =
                        $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" " +
                        $"-Version {_newVersion} " +
                        $"-BuildDir \"{buildDir}\" " +
                        $"-ReleaseNotesBase64 {notesB64}",
                    WorkingDirectory = repoRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                };

                using var proc = Process.Start(psi);
                if (proc == null)
                {
                    _resultLog = "powershell.exe 시작 실패";
                    Debug.LogError(_resultLog);
                    return;
                }
                var stdout = proc.StandardOutput.ReadToEnd();
                var stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                _resultLog =
                    $"exit={proc.ExitCode}\n" +
                    "--- stdout ---\n" + stdout + "\n" +
                    "--- stderr ---\n" + stderr;

                if (proc.ExitCode == 0)
                    Debug.Log("[LauncherPublish] publish-unity.ps1 succeeded\n" + _resultLog);
                else
                    Debug.LogError("[LauncherPublish] publish-unity.ps1 failed\n" + _resultLog);
            }
            catch (Exception e)
            {
                _resultLog = $"Exception: {e}";
                Debug.LogException(e);
            }
            finally
            {
                _isBusy = false;
                AssetDatabase.Refresh();
                Repaint();
            }
        }

        private static string RepoRoot()
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        private static string ManifestPath()
            => Path.Combine(
                RepoRoot(),
                "launcher", "manifests", "dev", "windows", "latest.json");
    }
}
