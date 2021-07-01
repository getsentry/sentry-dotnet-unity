using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Sentry.Unity.Tests
{
    public class SessionIntegrationTests
    {
        [UnityTest]
        public IEnumerator SessionIntegration_Init_SentryMonoBehaviourCreated()
        {
            yield return null;

            using var _ = IntegrationTests.InitSentrySdk(o =>
            {
                // o.AutoSessionTracking = true; We expect this to be true by default
            });

            var sentryGameObject = GameObject.Find("SentryMonoBehaviour");
            var sentryMonoBehaviour = sentryGameObject.GetComponent<SentryMonoBehaviour>();

            Assert.IsNotNull(sentryGameObject);
            Assert.IsNotNull(sentryMonoBehaviour);
        }
    }
}
