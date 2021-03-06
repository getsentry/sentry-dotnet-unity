using System;
using System.Globalization;
using System.IO;
using Sentry.Extensibility;
using Sentry.Unity.Json;
using UnityEditor;
using UnityEngine;

namespace Sentry.Unity.Editor
{
    public class SentryWindow : EditorWindow
    {
        private const string LinkXmlPath = "Assets/Plugins/Sentry/link.xml";

        [MenuItem("Tools/Sentry")]
        public static SentryWindow OpenSentryWindow()
        {
            var window = (SentryWindow)GetWindow(typeof(SentryWindow));
            window.minSize = new Vector2(400, 350);
            return window;
        }

        protected virtual string SentryOptionsAssetName { get; } = ScriptableSentryUnityOptions.ConfigName;

        public ScriptableSentryUnityOptions Options { get; private set; } = null!; // Set by OnEnable()

        public event Action<ValidationError> OnValidationError = _ => { };

        private int _currentTab = 0;
        private string[] _tabs = new[] {"Core", "Enrichment", "Transport", "Sessions", "Debug"};

        private void OnEnable()
        {
            SetTitle();
            CopyLinkXmlToPlugins();

            CheckForAndConvertJsonConfig();
            Options = LoadOptions();
        }

        private ScriptableSentryUnityOptions LoadOptions()
        {
            var options = AssetDatabase.LoadAssetAtPath(
                ScriptableSentryUnityOptions.GetConfigPath(SentryOptionsAssetName),
                typeof(ScriptableSentryUnityOptions)) as ScriptableSentryUnityOptions;

            if (options is null)
            {
                options = CreateOptions(SentryOptionsAssetName);
            }

            return options;
        }

        internal static ScriptableSentryUnityOptions CreateOptions(string? notDefaultConfigName = null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            if (!AssetDatabase.IsValidFolder($"Assets/Resources/{ScriptableSentryUnityOptions.ConfigRootFolder}"))
            {
                AssetDatabase.CreateFolder("Assets/Resources", ScriptableSentryUnityOptions.ConfigRootFolder);
            }

            var scriptableOptions = CreateInstance<ScriptableSentryUnityOptions>();
            SentryOptionsUtility.SetDefaults(scriptableOptions);

            AssetDatabase.CreateAsset(scriptableOptions,
                ScriptableSentryUnityOptions.GetConfigPath(notDefaultConfigName));
            AssetDatabase.SaveAssets();

            return scriptableOptions;
        }

        private void CheckForAndConvertJsonConfig()
        {
            var sentryOptionsTextAsset =
                AssetDatabase.LoadAssetAtPath(JsonSentryUnityOptions.GetConfigPath(), typeof(TextAsset)) as TextAsset;
            if (sentryOptionsTextAsset is null)
            {
                // Json config not found, nothing to do.
                return;
            }

            var scriptableOptions = CreateOptions(SentryOptionsAssetName);
            JsonSentryUnityOptions.ToScriptableOptions(sentryOptionsTextAsset, scriptableOptions);

            EditorUtility.SetDirty(scriptableOptions);
            AssetDatabase.SaveAssets();

            AssetDatabase.DeleteAsset(JsonSentryUnityOptions.GetConfigPath());
        }

        // ReSharper disable once UnusedMember.Local
        private void OnGUI()
        {
            EditorGUILayout.Space();
            GUILayout.Label("SDK Options", EditorStyles.boldLabel);

            Options.Enabled = EditorGUILayout.ToggleLeft(
                new GUIContent("Enable Sentry", "Controls if the SDK should initialize itself or not."),
                Options.Enabled);

            EditorGUILayout.Space();
            EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), Color.gray);
            EditorGUILayout.Space();

            _currentTab = GUILayout.Toolbar(_currentTab, _tabs);
            EditorGUI.BeginDisabledGroup(!Options.Enabled);
            EditorGUILayout.Space();

            switch (_currentTab)
            {
                case 0:
                    DisplayCore();
                    break;
                case 1:
                    DisplayEnrichment();
                    break;
                case 2:
                    DisplayTransport();
                    break;
                case 3:
                    DisplaySessions();
                    break;
                case 4:
                    DisplayDebug();
                    break;
                default:
                    break;
            }

            EditorGUI.EndDisabledGroup();
        }

        private void DisplayCore()
        {
            GUILayout.Label("Base Options", EditorStyles.boldLabel);

            var dsn = Options.Dsn;
            dsn = EditorGUILayout.TextField(
                new GUIContent("DSN", "The URL to your Sentry project. " +
                                      "Get yours on sentry.io -> Project Settings."),
                dsn);
            if (!string.IsNullOrWhiteSpace(dsn))
            {
                Options.Dsn = dsn;
            }

            Options.CaptureInEditor = EditorGUILayout.Toggle(
                new GUIContent("Capture In Editor", "Capture errors while running in the Editor."),
                Options.CaptureInEditor);

            EditorGUILayout.Space();
            EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), Color.gray);
            EditorGUILayout.Space();

            GUILayout.Label("Transactions", EditorStyles.boldLabel);

            var traceSampleRate = (float?)Options.TracesSampleRate;
            Options.TracesSampleRate = EditorGUILayout.Slider(
                new GUIContent("Trace Sample Rate", "Indicates the percentage of the transactions that is " +
                                                    "collected. Setting this to 0 discards all trace data. " +
                                                    "Setting this to 1.0 collects all trace data."),
                traceSampleRate ??= 0.0f, 0.0f, 1.0f);
            if (traceSampleRate > 0.0f)
            {
                Options.TracesSampleRate = (double)traceSampleRate;
            }
        }

        private void DisplayEnrichment()
        {
            GUILayout.Label("Tag Overrides", EditorStyles.boldLabel);

            Options.ReleaseOverride = EditorGUILayout.TextField(
                new GUIContent("Override Release", "By default release is built from " +
                                                   "'Application.productName'@'Application.version'. " +
                                                   "This option is an override."),
                Options.ReleaseOverride);

            Options.EnvironmentOverride = EditorGUILayout.TextField(
                new GUIContent("Override Environment", "Auto detects 'production' or 'editor' by " +
                                                       "default based on 'Application.isEditor." +
                                                       "\nThis option is an override."),
                Options.EnvironmentOverride);

            EditorGUILayout.Space();
            EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), Color.gray);
            EditorGUILayout.Space();

            GUILayout.Label("Stacktrace", EditorStyles.boldLabel);

            Options.AttachStacktrace = EditorGUILayout.Toggle(
                new GUIContent("Stacktrace For Logs", "Whether to include a stack trace for non " +
                                                      "error events like logs. Even when Unity didn't include and no " +
                                                      "exception was thrown. Refer to AttachStacktrace on sentry docs."),
                Options.AttachStacktrace);

            // Enhanced not supported on IL2CPP so not displaying this for the time being:
            // Options.StackTraceMode = (StackTraceMode) EditorGUILayout.EnumPopup(
            //     new GUIContent("Stacktrace Mode", "Enhanced is the default." +
            //                                       "\n - Enhanced: Include async, return type, args,..." +
            //                                       "\n - Original - Default .NET stack trace format."),
            //     Options.StackTraceMode);

            EditorGUILayout.Space();
            EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), Color.gray);
            EditorGUILayout.Space();

            Options.SendDefaultPii = EditorGUILayout.BeginToggleGroup(
                new GUIContent("Send default Pii", "Whether to include default Personal Identifiable " +
                                                   "Information."),
                Options.SendDefaultPii);

            Options.IsEnvironmentUser = EditorGUILayout.Toggle(
                new GUIContent("Auto Set UserName", "Whether to report the 'Environment.UserName' as " +
                                                    "the User affected in the event. Should be disabled for " +
                                                    "Android and iOS."),
                Options.IsEnvironmentUser);

            EditorGUILayout.EndToggleGroup();

            EditorGUILayout.Space();
            EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), Color.gray);
            EditorGUILayout.Space();

            Options.MaxBreadcrumbs = EditorGUILayout.IntField(
                new GUIContent("Max Breadcrumbs", "Maximum number of breadcrumbs that get captured." +
                                                  "\nDefault: 100"),
                Options.MaxBreadcrumbs);

            Options.ReportAssembliesMode = (ReportAssembliesMode)EditorGUILayout.EnumPopup(
                new GUIContent("Report Assemblies Mode", "Whether or not to include referenced assemblies " +
                                                         "Version or InformationalVersion in each event sent to sentry."),
                Options.ReportAssembliesMode);
        }

        private void DisplayTransport()
        {
            Options.EnableOfflineCaching = EditorGUILayout.BeginToggleGroup(
                new GUIContent("Enable Offline Caching", ""),
                Options.EnableOfflineCaching);
            Options.MaxCacheItems = EditorGUILayout.IntField(
                new GUIContent("Max Cache Items", "The maximum number of files to keep in the disk cache. " +
                                                  "The SDK deletes the oldest when the limit is reached.\nDefault: 30"),
                Options.MaxCacheItems);

            Options.InitCacheFlushTimeout = EditorGUILayout.IntField(
                new GUIContent("Init Flush Timeout [ms]", "The timeout that limits how long the SDK " +
                                                          "will attempt to flush existing cache during initialization." +
                                                          "\nThis features allows capturing errors that happen during " +
                                                          "game startup and would not be captured because the process " +
                                                          "would be killed before Sentry had a chance to capture the event."),
                Options.InitCacheFlushTimeout);

            EditorGUILayout.EndToggleGroup();

            EditorGUILayout.Space();
            EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), Color.gray);
            EditorGUILayout.Space();

            // Options.RequestBodyCompressionLevel = (CompressionLevelWithAuto)EditorGUILayout.EnumPopup(
            //     new GUIContent("Compress Payload", "The level of which to compress the Sentry event " +
            //                                        "before sending to Sentry."),
            //     Options.RequestBodyCompressionLevel);

            var sampleRate = Options.SampleRate ??= 1.0f;
            sampleRate = EditorGUILayout.Slider(
                new GUIContent("Event Sample Rate", "Indicates the percentage of events that are " +
                                                    "captured. Setting this to 0.1 captures 10% of events. " +
                                                    "Setting this to 1.0 captures all events." +
                                                    "\nThis affects only errors and logs, not performance " +
                                                    "(transactions) data. See TraceSampleRate for that."),
                sampleRate, 0.01f, 1);
            Options.SampleRate = (sampleRate < 1.0f) ? sampleRate : null;

            Options.ShutdownTimeout = EditorGUILayout.IntField(
                new GUIContent("Shut Down Timeout [ms]", "How many seconds to wait before shutting down to " +
                                                         "give Sentry time to send events from the background queue."),
                Options.ShutdownTimeout);

            Options.MaxQueueItems = EditorGUILayout.IntField(
                new GUIContent("Max Queue Items", "The maximum number of events to keep in memory while " +
                                                  "the worker attempts to send them."),
                Options.MaxQueueItems
            );
        }

        private void DisplaySessions()
        {
            Options.AutoSessionTracking = EditorGUILayout.BeginToggleGroup(
                new GUIContent("Auto Session Tracking", "Whether the SDK should start and end sessions " +
                                                        "automatically. If the timeout is reached the old session will" +
                                                        "be ended and a new one started."),
                Options.AutoSessionTracking);

            Options.AutoSessionTrackingInterval = EditorGUILayout.IntField(
                new GUIContent("Session Timeout [ms]", "The duration of time a session can stay paused " +
                                                       "(i.e. the application has been put in the background) before " +
                                                       "it is considered ended."),
                Options.AutoSessionTrackingInterval);

            EditorGUILayout.EndToggleGroup();
        }

        private void DisplayDebug()
        {
            Options.Debug = EditorGUILayout.BeginToggleGroup(
                new GUIContent("Enable Debug Output", "Whether the Sentry SDK should print its " +
                                                      "diagnostic logs to the console."),
                Options.Debug);

            Options.DebugOnlyInEditor = EditorGUILayout.Toggle(
                new GUIContent("Only In Editor", "Only print logs when in the editor. Development " +
                                                 "builds of the player will not include Sentry's SDK diagnostics."),
                Options.DebugOnlyInEditor);

            Options.DiagnosticLevel = (SentryLevel)EditorGUILayout.EnumPopup(
                new GUIContent("Verbosity Level", "The minimum level allowed to be printed to the console. " +
                                                  "Log messages with a level below this level are dropped."),
                Options.DiagnosticLevel);

            EditorGUILayout.EndToggleGroup();
        }

        private void OnLostFocus()
        {
            // Make sure the actual config asset exists before validating/saving. Crashes the editor otherwise.
            if (!File.Exists(ScriptableSentryUnityOptions.GetConfigPath(SentryOptionsAssetName)))
            {
                new UnityLogger(new SentryOptions()).LogWarning("Sentry option could not been saved. " +
                                                                "The configuration asset is missing.");
                return;
            }

            Validate();

            EditorUtility.SetDirty(Options);
            AssetDatabase.SaveAssets();
        }

        private void Validate()
        {
            if (!Options.Enabled)
            {
                return;
            }

            ValidateDsn();
        }

        internal void ValidateDsn()
        {
            if (string.IsNullOrWhiteSpace(Options.Dsn))
            {
                return;
            }

            if (Uri.IsWellFormedUriString(Options.Dsn, UriKind.Absolute))
            {
                return;
            }

            var fullFieldName = $"{nameof(Options)}.{nameof(Options.Dsn)}";
            var validationError = new ValidationError(fullFieldName, "Invalid DSN format. Expected a URL.");
            OnValidationError(validationError);

            new UnityLogger(new SentryOptions()).LogWarning(validationError.ToString());
        }

        /// <summary>
        /// Creates Sentry folder 'Plugins/Sentry' and copies the link.xml into it
        /// </summary>
        private void CopyLinkXmlToPlugins()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Plugins"))
            {
                AssetDatabase.CreateFolder("Assets", "Plugins");
            }

            if (!AssetDatabase.IsValidFolder("Assets/Plugins/Sentry"))
            {
                AssetDatabase.CreateFolder("Assets/Plugins", "Sentry");
            }

            if (!File.Exists(LinkXmlPath))
            {
                using var fileStream = File.Create(LinkXmlPath);
                using var resourceStream =
                    GetType().Assembly.GetManifestResourceStream("Sentry.Unity.Editor.Resources.link.xml");
                resourceStream.CopyTo(fileStream);

                AssetDatabase.Refresh();
            }
        }

        private void SetTitle()
        {
            var isDarkMode = EditorGUIUtility.isProSkin;
            var texture = new Texture2D(16, 16);
            using var memStream = new MemoryStream();
            using var stream = GetType().Assembly
                .GetManifestResourceStream(
                    $"Sentry.Unity.Editor.Resources.SentryLogo{(isDarkMode ? "Light" : "Dark")}.png");
            stream.CopyTo(memStream);
            stream.Flush();
            memStream.Position = 0;
            texture.LoadImage(memStream.ToArray());

            titleContent = new GUIContent("Sentry", texture, "Sentry SDK Options");
        }
    }

    public readonly struct ValidationError
    {
        public readonly string PropertyName;

        public readonly string Reason;

        public ValidationError(string propertyName, string reason)
        {
            PropertyName = propertyName;
            Reason = reason;
        }

        public override string ToString()
            => $"[{PropertyName}] Reason: {Reason}";
    }
}
