using System;
using Sentry.Unity.Json;
using UnityEngine;

namespace Sentry.Unity
{
    public class ScriptableSentryUnityOptions : ScriptableObject
    {
        /// <summary>
        /// Relative to Assets/Resources
        /// </summary>
        internal const string ConfigRootFolder = "Sentry";

        /// <summary>
        /// Main Sentry config name for Unity
        /// </summary>
        internal const string ConfigName = "SentryOptions";

        /// <summary>
        /// Path for the config for Unity
        /// </summary>
        internal static string GetConfigPath(string? notDefaultConfigName = null)
            => $"Assets/Resources/{ConfigRootFolder}/{notDefaultConfigName ?? ConfigName}.asset";

        [field: SerializeField] internal bool Enabled { get; set; }

        [field: SerializeField] internal string? Dsn { get; set; }
        [field: SerializeField] internal bool CaptureInEditor { get; set; }
        [field: SerializeField] internal double TracesSampleRate { get; set; }
        [field: SerializeField] internal bool AutoSessionTracking { get; set; }
        [field: SerializeField] internal int AutoSessionTrackingInterval { get; set; }


        [field: SerializeField] internal string ReleaseOverride { get; set; } = string.Empty;
        [field: SerializeField] internal string EnvironmentOverride { get; set; } = string.Empty;
        [field: SerializeField] internal bool AttachStacktrace { get; set; }
        [field: SerializeField] internal int MaxBreadcrumbs { get; set; }
        [field: SerializeField] internal ReportAssembliesMode ReportAssembliesMode { get; set; }
        [field: SerializeField] internal bool SendDefaultPii { get; set; }
        [field: SerializeField] internal bool IsEnvironmentUser { get; set; }

        [field: SerializeField] internal bool EnableOfflineCaching { get; set; }
        [field: SerializeField] internal int MaxCacheItems { get; set; }
        [field: SerializeField] internal int InitCacheFlushTimeout { get; set; }
        [field: SerializeField] internal float? SampleRate { get; set; }
        [field: SerializeField] internal int ShutdownTimeout { get; set; }
        [field: SerializeField] internal int MaxQueueItems { get; set; }

        [field: SerializeField] internal bool Debug { get; set; }
        [field: SerializeField] internal bool DebugOnlyInEditor { get; set; }
        [field: SerializeField] internal SentryLevel DiagnosticLevel { get; set; }

        internal static SentryUnityOptions? LoadSentryUnityOptions()
        {
            // TODO: Deprecated and to be removed once we update far enough.
            var sentryOptionsTextAsset = Resources.Load<TextAsset>($"{ConfigRootFolder}/{ConfigName}");
            if (sentryOptionsTextAsset != null)
            {
                var options = JsonSentryUnityOptions.LoadFromJson(sentryOptionsTextAsset);
                return options;
            }

            var scriptableOptions = Resources.Load<ScriptableSentryUnityOptions>($"{ConfigRootFolder}/{ConfigName}");
            if (scriptableOptions is not null)
            {
                return ToSentryUnityOptions(scriptableOptions);
            }

            return null;
        }

        internal static SentryUnityOptions ToSentryUnityOptions(ScriptableSentryUnityOptions scriptableOptions)
        {
            var options = new SentryUnityOptions();
            SentryOptionsUtility.SetDefaults(options);

            options.Enabled = scriptableOptions.Enabled;

            options.Dsn = scriptableOptions.Dsn;
            options.CaptureInEditor = scriptableOptions.CaptureInEditor;
            options.TracesSampleRate = scriptableOptions.TracesSampleRate;
            options.AutoSessionTracking = scriptableOptions.AutoSessionTracking;
            options.AutoSessionTrackingInterval = TimeSpan.FromMilliseconds(scriptableOptions.AutoSessionTrackingInterval);

            options.AttachStacktrace = scriptableOptions.AttachStacktrace;
            options.MaxBreadcrumbs = scriptableOptions.MaxBreadcrumbs;
            options.ReportAssembliesMode = scriptableOptions.ReportAssembliesMode;
            options.SendDefaultPii = scriptableOptions.SendDefaultPii;
            options.IsEnvironmentUser = scriptableOptions.IsEnvironmentUser;

            options.MaxCacheItems = scriptableOptions.MaxCacheItems;
            options.InitCacheFlushTimeout = TimeSpan.FromMilliseconds(scriptableOptions.InitCacheFlushTimeout);
            options.SampleRate = scriptableOptions.SampleRate;
            options.ShutdownTimeout = TimeSpan.FromMilliseconds(scriptableOptions.ShutdownTimeout);
            options.MaxQueueItems = scriptableOptions.MaxQueueItems;

            if (!string.IsNullOrWhiteSpace(scriptableOptions.ReleaseOverride))
            {
                options.Release = scriptableOptions.ReleaseOverride;
            }

            if (!string.IsNullOrWhiteSpace(scriptableOptions.EnvironmentOverride))
            {
                options.Environment = scriptableOptions.EnvironmentOverride;
            }

            if (!scriptableOptions.EnableOfflineCaching)
            {
                options.CacheDirectoryPath = null;
            }

            options.Debug = scriptableOptions.Debug;
            options.DebugOnlyInEditor = scriptableOptions.DebugOnlyInEditor;
            options.DiagnosticLevel = scriptableOptions.DiagnosticLevel;

            SentryOptionsUtility.TryAttachLogger(options);
            return options;
        }
    }
}
