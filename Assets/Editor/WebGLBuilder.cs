using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BlockBlast.EditorTools
{
    /// <summary>
    /// Command-line entry point for the WebGL build that gets published to GitHub Pages.
    ///
    ///   Unity.exe -quit -batchmode -nographics -projectPath &lt;project&gt; \
    ///             -buildTarget WebGL \
    ///             -executeMethod BlockBlast.EditorTools.WebGLBuilder.Build \
    ///             -logFile &lt;log&gt;
    ///
    /// Settings are applied here rather than left in ProjectSettings so the published
    /// build cannot silently drift from what GitHub Pages needs.
    /// </summary>
    public static class WebGLBuilder
    {
        const string DefaultOutputDirectory = "docs";

        /// <summary>
        /// Where the player is written. Overridable so CI can build somewhere other than
        /// the folder Pages used to serve, without the two build paths diverging:
        ///   -buildOutput &lt;path&gt;   command line
        ///   BUILD_OUTPUT=&lt;path&gt;   environment
        /// </summary>
        static string OutputDirectory
        {
            get
            {
                var args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                    if (args[i] == "-buildOutput" && !string.IsNullOrEmpty(args[i + 1]))
                        return args[i + 1];

                string fromEnv = Environment.GetEnvironmentVariable("BUILD_OUTPUT");
                return string.IsNullOrEmpty(fromEnv) ? DefaultOutputDirectory : fromEnv;
            }
        }

        public static void Build()
        {
            int exitCode = 0;
            try
            {
                exitCode = Run();
            }
            catch (Exception e)
            {
                Debug.LogError("[WebGLBuilder] " + e);
                exitCode = 2;
            }

            // -quit alone always exits 0, which would make a failed build look successful.
            if (Application.isBatchMode) EditorApplication.Exit(exitCode);
        }

        static int Run()
        {
            ApplySettings();

            string requested = OutputDirectory;
            string output = Path.IsPathRooted(requested)
                ? requested
                : Path.Combine(Directory.GetCurrentDirectory(), requested);
            if (Directory.Exists(output))
            {
                Debug.Log("[WebGLBuilder] Clearing " + output);
                Directory.Delete(output, true);
            }

            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[WebGLBuilder] No enabled scenes in build settings.");
                return 3;
            }

            Debug.Log("[WebGLBuilder] Building " + string.Join(", ", scenes) + " -> " + output);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None
            });

            var summary = report.summary;
            Debug.Log($"[WebGLBuilder] result={summary.result} errors={summary.totalErrors} " +
                      $"warnings={summary.totalWarnings} size={summary.totalSize / 1048576}MB");

            // Unity can report Succeeded while the post-processor failed, so check both the
            // error count and that the loader actually landed on disk.
            bool loaderExists = Directory.Exists(output) &&
                                File.Exists(Path.Combine(output, "index.html"));

            if (summary.result != BuildResult.Succeeded || summary.totalErrors > 0 || !loaderExists)
            {
                Debug.LogError("[WebGLBuilder] Build failed " +
                               $"(result={summary.result}, errors={summary.totalErrors}, indexWritten={loaderExists})");
                return 1;
            }

            // GitHub Pages publishes the folder as-is; keep Jekyll away from it.
            File.WriteAllText(Path.Combine(output, ".nojekyll"), string.Empty);

            Debug.Log("[WebGLBuilder] Build succeeded: " + output);
            return 0;
        }

        /// <summary>Everything the published build depends on, set explicitly.</summary>
        public static void ApplySettings()
        {
            var nbt = UnityEditor.Build.NamedBuildTarget.WebGL;

            PlayerSettings.WebGL.template = "PROJECT:BlockBlast";

            // GitHub Pages serves static files without a Content-Encoding header, so the
            // player has to decompress the build itself. Without the fallback the page
            // loads to a black screen.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;

            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.showDiagnostics = false;

            PlayerSettings.defaultWebScreenWidth = 1080;
            PlayerSettings.defaultWebScreenHeight = 1920;
            PlayerSettings.runInBackground = true;

            PlayerSettings.SetIl2CppCompilerConfiguration(nbt, Il2CppCompilerConfiguration.Release);
            PlayerSettings.SetManagedStrippingLevel(nbt, ManagedStrippingLevel.Low);

            AssetDatabase.SaveAssets();
        }
    }
}
