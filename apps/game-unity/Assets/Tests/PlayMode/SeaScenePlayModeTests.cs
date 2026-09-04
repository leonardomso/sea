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

        [UnityTest]
        public IEnumerator Owned_assets_load_for_runtime_and_release_idempotently()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            var world = Object.FindFirstObjectByType<SeaWorldView>();
            var lease = new SeaOwnedAssetLease();
            SeaOwnedAssetSet loaded = null;
            System.Exception failure = null;

            lease.Load(world.OwnedAssets, value => loaded = value, error => failure = error);
            for (var frame = 0; frame < 300 && loaded == null && failure == null; frame++)
            {
                yield return null;
            }

            Assert.That(failure, Is.Null);
            Assert.That(lease.IsReady, Is.True);
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.ShipModel(SeaOwnedShipRole.Player), Is.Not.Null);
            Assert.That(loaded.ShipModel(SeaOwnedShipRole.Skiff), Is.Not.Null);
            Assert.That(loaded.ShipModel(SeaOwnedShipRole.ReefCrab), Is.Not.Null);
            Assert.That(loaded.ShipModel(SeaOwnedShipRole.Fancy), Is.Not.Null);
            Assert.That(loaded.ShipModel(SeaOwnedShipRole.RedMary), Is.Not.Null);
            Assert.That(loaded.ShipMaterial, Is.Not.Null);

            lease.Release();
            lease.Release();
            Assert.That(lease.IsReleased, Is.True);
            Object.FindFirstObjectByType<SeaConnectionController>().Disconnect();
        }
    }
}
