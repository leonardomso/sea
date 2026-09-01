using System.Collections;
using NUnit.Framework;
using Sea.Bootstrap;
using Sea.Client;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Sea.Tests.PlayMode
{
    public sealed class SeaClientPlayModeTests
    {
        [UnityTest]
        public IEnumerator Main_scene_creates_the_real_client_graph()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;

            Assert.That(Object.FindFirstObjectByType<SeaLifetimeScope>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<SeaSceneComposer>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<SeaConnectionController>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<SeaGameController>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<SeaChartCameraController>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<SeaWorldView>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<SeaHudController>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<SeaInputController>(), Is.Not.Null);
            Assert.That(Camera.main, Is.Not.Null);

            Object.FindFirstObjectByType<SeaConnectionController>().Disconnect();
        }
    }
}
