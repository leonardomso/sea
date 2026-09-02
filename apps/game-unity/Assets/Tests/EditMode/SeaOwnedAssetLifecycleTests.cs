#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using Sea.Client;
using Sea.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Sea.Tests
{
    public sealed class SeaOwnedAssetLifecycleTests
    {
        [Test]
        public void Catalog_has_one_replaceable_entry_for_every_owned_asset_slot()
        {
            var catalog = SeaOwnedAssetEditorLifecycle.EnsureCatalog();

            Assert.That(
                catalog.Entries.Select(entry => entry.SlotId),
                Is.EquivalentTo(SeaOwnedAssetPolicy.Definitions.Select(definition => definition.Id)));
            Assert.That(catalog.Entries.Select(entry => entry.SlotId), Is.Unique);
            Assert.That(catalog.Entries.Where(entry => entry.Required)
                .All(entry => !string.IsNullOrEmpty(entry.AssetGuid)), Is.True);
            Assert.That(catalog.Entries.Where(entry => !entry.Required)
                .All(entry => entry.Fallback != SeaOwnedAssetFallback.None), Is.True);
        }

        [Test]
        public void Owned_asset_import_addressables_and_material_contract_is_valid()
        {
            var catalog = SeaOwnedAssetEditorLifecycle.EnsureCatalog();

            Assert.That(SeaOwnedAssetEditorLifecycle.Validate(catalog), Is.Empty);
        }

        [Test]
        public void Main_scene_references_the_catalog_instead_of_a_ship_asset_directly()
        {
            var catalog = SeaOwnedAssetEditorLifecycle.EnsureCatalog();
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
            var world = Object.FindFirstObjectByType<SeaWorldView>();

            Assert.That(world, Is.Not.Null);
            Assert.That(world.OwnedAssets, Is.SameAs(catalog));
            Assert.That(world.ShipModel, Is.Null);
            Assert.That(world.ShipMaterial, Is.Null);
        }

        [Test]
        public void Owned_ship_model_and_procedural_lods_share_scale_and_forward_axis()
        {
            var catalog = SeaOwnedAssetEditorLifecycle.EnsureCatalog();
            var modelPath = AssetDatabase.GUIDToAssetPath(
                catalog.Require(SeaOwnedAssetSlots.PlayerShip).AssetGuid);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var near = SeaShipVisualFactory.Create(
                model,
                "Near LOD",
                targetFootprint: 10f,
                modelYawOffsetDegrees: 270f);
            var medium = SeaShipVisualFactory.CreateMediumLod("Medium LOD", 10f);
            var distant = SeaShipVisualFactory.CreateDistantLod("Distant LOD", 10f);

            AssertFootprint(near, 10f);
            AssertFootprint(medium, 10f);
            AssertFootprint(distant, 10f);
            Assert.That(Vector3.Dot(near.transform.forward, medium.transform.forward),
                Is.GreaterThan(0.999f));
            Assert.That(Vector3.Dot(near.transform.forward, distant.transform.forward),
                Is.GreaterThan(0.999f));

            Object.DestroyImmediate(near);
            Object.DestroyImmediate(medium);
            Object.DestroyImmediate(distant);
        }

        [Test]
        public void Npc_archetypes_have_distinct_owned_material_variants()
        {
            var patrol = SeaShipVariantPolicy.Tint(factionCode: 2, archetypeCode: 1);
            var raider = SeaShipVariantPolicy.Tint(factionCode: 2, archetypeCode: 2);
            var gunship = SeaShipVariantPolicy.Tint(factionCode: 2, archetypeCode: 3);

            Assert.That(patrol, Is.Not.EqualTo(raider));
            Assert.That(raider, Is.Not.EqualTo(gunship));
            Assert.That(gunship, Is.Not.EqualTo(patrol));
            Assert.That(SeaShipVariantPolicy.Tint(1, 0), Is.EqualTo(Color.white));
        }

        [TestCase(1, 0, SeaOwnedShipRole.Player)]
        [TestCase(2, 1, SeaOwnedShipRole.Patrol)]
        [TestCase(2, 2, SeaOwnedShipRole.Raider)]
        [TestCase(2, 3, SeaOwnedShipRole.Gunship)]
        public void Every_ship_kind_resolves_to_its_replaceable_catalog_slot(
            byte factionCode,
            byte archetypeCode,
            SeaOwnedShipRole expectedRole)
        {
            var role = SeaOwnedAssetPolicy.ShipRole(factionCode, archetypeCode);
            var catalog = SeaOwnedAssetEditorLifecycle.EnsureCatalog();

            Assert.That(role, Is.EqualTo(expectedRole));
            Assert.That(catalog.Require(SeaOwnedAssetPolicy.ShipSlot(role)).HasValidReference,
                Is.True);
        }

        private static void AssertFootprint(GameObject value, float expected)
        {
            var bounds = SeaShipVisualFactory.CalculateRendererBounds(value);
            Assert.That(Mathf.Max(bounds.size.x, bounds.size.z),
                Is.EqualTo(expected).Within(0.05f));
        }
    }
}
#endif
