﻿using System;
using Sentry.Integrations;
using UnityEngine;

namespace Sentry.Unity.Integrations
{
    internal sealed class UnityApplicationLoggingIntegration : ISdkIntegration
    {
        internal readonly ErrorTimeDebounce ErrorTimeDebounce = new(TimeSpan.FromSeconds(1));
        internal readonly LogTimeDebounce LogTimeDebounce = new(TimeSpan.FromSeconds(1));
        internal readonly WarningTimeDebounce WarningTimeDebounce = new(TimeSpan.FromSeconds(1));

        // TODO: remove 'IEventCapture' in  further iteration
        private readonly IEventCapture? _eventCapture;
        private readonly IApplication _application;

        private IHub? _hub;
        private SentryOptions? _sentryOptions;

        public UnityApplicationLoggingIntegration(IApplication? appDomain = null, IEventCapture? eventCapture = null)
        {
            _application = appDomain ?? ApplicationAdapter.Instance;
            _eventCapture = eventCapture;
        }

        public void Register(IHub hub, SentryOptions sentryOptions)
        {
            _hub = hub;
            _sentryOptions = sentryOptions;

            _application.LogMessageReceived += OnLogMessageReceived;
            _application.Quitting += OnQuitting;
        }

        // Internal for testability
        internal void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            var debounced = type switch
            {
                LogType.Error or LogType.Exception or LogType.Assert => ErrorTimeDebounce.Debounced(),
                LogType.Log => LogTimeDebounce.Debounced(),
                LogType.Warning => WarningTimeDebounce.Debounced(),
                _ => true
            };
            if (!debounced || _hub is null)
            {
                return;
            }

            // TODO: to check against 'MinBreadcrumbLevel'
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            {
                // TODO: MinBreadcrumbLevel
                // options.MinBreadcrumbLevel
                _hub.AddBreadcrumb(condition, level: ToBreadcrumbLevel(type));
                return;
            }

            var sentryEvent = new SentryEvent(new UnityLogException(condition, stackTrace))
            {
                Level = ToEventTagType(type)
            };

            _eventCapture?.Capture(sentryEvent); // TODO: remove, for current integration tests compatibility
            _hub.CaptureEvent(sentryEvent);

            // So the next event includes this error as a breadcrumb:
            _hub.AddBreadcrumb(condition, level: ToBreadcrumbLevel(type));
        }

        private void OnQuitting()
        {
            // Note: iOS applications are usually suspended and do not quit. You should tick "Exit on Suspend" in Player settings for iOS builds to cause the game to quit and not suspend, otherwise you may not see this call.
            //   If "Exit on Suspend" is not ticked then you will see calls to OnApplicationPause instead.
            // Note: On Windows Store Apps and Windows Phone 8.1 there is no application quit event. Consider using OnApplicationFocus event when focusStatus equals false.
            // Note: On WebGL it is not possible to implement OnApplicationQuit due to nature of the browser tabs closing.
            _application.LogMessageReceived -= OnLogMessageReceived;
            _hub?.FlushAsync(_sentryOptions?.ShutdownTimeout ?? TimeSpan.FromSeconds(1)).GetAwaiter().GetResult();
        }

        private static SentryLevel ToEventTagType(LogType logType)
            => logType switch
            {
                LogType.Assert => SentryLevel.Error,
                LogType.Error => SentryLevel.Error,
                LogType.Exception => SentryLevel.Error,
                LogType.Log => SentryLevel.Info,
                LogType.Warning => SentryLevel.Warning,
                _ => SentryLevel.Fatal
            };

        private static BreadcrumbLevel ToBreadcrumbLevel(LogType logType)
            => logType switch
            {
                LogType.Assert => BreadcrumbLevel.Error,
                LogType.Error => BreadcrumbLevel.Error,
                LogType.Exception => BreadcrumbLevel.Error,
                LogType.Log => BreadcrumbLevel.Info,
                LogType.Warning => BreadcrumbLevel.Warning,
                _ => BreadcrumbLevel.Info
            };
    }
}
