using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using App.Infrastructure.Catalogs;
using App.Infrastructure.Api.Dtos;
using NUnit.Framework;
using UnityEngine;

namespace App.Tests.EditMode.Notebook
{
    public sealed class RoomEditModeVisitorIsolationTests
    {
        private GameObject host;

        [TearDown]
        public void TearDown()
        {
            if (host != null)
            {
                UnityEngine.Object.DestroyImmediate(host);
                host = null;
            }
        }

        [Test]
        public void OwnSpaceAssetKey_ResolvesLocalInventoryEntry()
        {
            var controller = CreateController();
            AddInventoryEntry(controller, remoteAssetId: 77L, remoteInventoryId: 500L);

            var resolved = InvokeTryFindInventoryEntry(controller, "asset:77", out var entry);

            Assert.IsTrue(resolved);
            Assert.AreEqual(77L, GetEntryField<long>(entry, "RemoteAssetId"));
        }

        [Test]
        public void VisitorPreviewAssetKey_DoesNotFallThroughToLocalInventory()
        {
            var controller = CreateController();
            SetReadOnlyPreview(controller, true);
            AddInventoryEntry(controller, remoteAssetId: 77L, remoteInventoryId: 500L);

            var resolved = InvokeTryFindInventoryEntry(controller, "asset:77", out _);

            Assert.IsFalse(resolved);
        }

        [Test]
        public void VisitorPreviewAssetKey_DoesNotCreateRemoteEntryWithoutVisibleManifest()
        {
            var controller = CreateController();
            SetReadOnlyPreview(controller, true);
            AddInventoryEntry(controller, remoteAssetId: 77L, remoteInventoryId: 500L);

            var created = InvokeTryCreateRemoteInventoryEntry(controller, "asset:77", out _);

            Assert.IsFalse(created);
        }

        [Test]
        public void VisitorPreviewAssetKey_CreatesRemoteEntryFromVisibleManifestMap()
        {
            var controller = CreateController();
            SetReadOnlyPreview(controller, true);
            AddVisibleRemoteManifest(controller, assetId: 77L);

            var created = InvokeTryCreateRemoteInventoryEntry(controller, "asset:77", out var entry);

            Assert.IsTrue(created);
            Assert.IsTrue(GetEntryField<bool>(entry, "IsRemoteAsset"));
            Assert.AreEqual(77L, GetEntryField<long>(entry, "RemoteAssetId"));
            Assert.AreEqual(0L, GetEntryField<long>(entry, "RemoteInventoryId"));
        }

        [Test]
        public void VisitorPreviewRemoteLoad_RequiresVisibleManifest()
        {
            var controller = CreateController();
            SetReadOnlyPreview(controller, true);
            var entry = CreateInventoryEntry(remoteAssetId: 77L, remoteInventoryId: 0L);
            string error = null;

            var routine = InvokeLoadRemoteAssetGameObject(
                controller,
                entry,
                _ => Assert.Fail("visitor preview must not load a remote asset without a visible manifest"),
                message => error = message);
            while (routine.MoveNext())
            {
            }

            Assert.AreEqual("visible space manifest is missing.", error);
        }

        [Test]
        public void VisitorPreviewInventoryKey_DoesNotFallThroughToLocalInventory()
        {
            var controller = CreateController();
            SetReadOnlyPreview(controller, true);
            AddInventoryEntry(controller, remoteAssetId: 77L, remoteInventoryId: 500L);

            var resolved = InvokeTryFindInventoryEntry(controller, "inv:500", out _);

            Assert.IsFalse(resolved);
        }

        [Test]
        public void VisitorPreviewPathKey_DoesNotFallThroughToLocalInventory()
        {
            var controller = CreateController();
            SetReadOnlyPreview(controller, true);
            AddInventoryEntry(
                controller,
                remoteAssetId: 77L,
                remoteInventoryId: 500L,
                sourceAssetPath: "Assets/Arts/Models/spooky_bed.glb");

            var resolved = InvokeTryFindInventoryEntry(controller, "path:Assets/Arts/Models/spooky_bed.glb", out _);

            Assert.IsFalse(resolved);
        }

        [Test]
        public void VisitorPreviewInventoryKey_CreatesRemoteEntryFromVisibleManifestMap()
        {
            var controller = CreateController();
            SetReadOnlyPreview(controller, true);
            AddVisibleInventoryAssetMap(controller, inventoryId: 500L, assetId: 77L);

            var created = InvokeTryCreateRemoteInventoryEntry(controller, "inv:500", out var entry);

            Assert.IsTrue(created);
            Assert.IsTrue(GetEntryField<bool>(entry, "IsRemoteAsset"));
            Assert.AreEqual(77L, GetEntryField<long>(entry, "RemoteAssetId"));
            Assert.AreEqual(500L, GetEntryField<long>(entry, "RemoteInventoryId"));
        }

        [Test]
        public void VisitorPreviewPathKey_CreatesRemoteEntryFromVisibleManifestMap()
        {
            var controller = CreateController();
            SetReadOnlyPreview(controller, true);
            AddVisibleResourceAssetMap(controller, "Assets/Arts/Models/spooky_bed.glb", assetId: 77L);

            var created = InvokeTryCreateRemoteInventoryEntry(controller, "path:Assets/Arts/Models/spooky_bed.glb", out var entry);

            Assert.IsTrue(created);
            Assert.IsTrue(GetEntryField<bool>(entry, "IsRemoteAsset"));
            Assert.AreEqual(77L, GetEntryField<long>(entry, "RemoteAssetId"));
            Assert.AreEqual(0L, GetEntryField<long>(entry, "RemoteInventoryId"));
        }

        [Test]
        public void RegisterVisibleSpaceAssetManifests_PopulatesVisitorOnlyLookupMaps()
        {
            var controller = CreateController();
            SetReadOnlyPreview(controller, true);

            InvokeRegisterVisibleSpaceAssetManifests(
                controller,
                new List<DownloadManifestResponseDto>
                {
                    new DownloadManifestResponseDto
                    {
                        AssetId = 77L,
                        InventoryId = 500L,
                        UnityResourceKey = "Assets/Arts/Models/spooky_bed.glb"
                    }
                });

            Assert.AreEqual(1, GetDictionaryCount(controller, "visibleRemoteAssetManifests"));
            Assert.AreEqual(1, GetDictionaryCount(controller, "visibleAssetIdByInventoryId"));
            Assert.AreEqual(1, GetDictionaryCount(controller, "visibleAssetIdByResourceKey"));

            Assert.IsTrue(InvokeTryCreateRemoteInventoryEntry(controller, "inv:500", out var inventoryEntry));
            Assert.AreEqual(77L, GetEntryField<long>(inventoryEntry, "RemoteAssetId"));
            Assert.AreEqual(500L, GetEntryField<long>(inventoryEntry, "RemoteInventoryId"));

            Assert.IsTrue(InvokeTryCreateRemoteInventoryEntry(controller, "path:Assets/Arts/Models/spooky_bed.glb", out var pathEntry));
            Assert.AreEqual(77L, GetEntryField<long>(pathEntry, "RemoteAssetId"));
            Assert.AreEqual(0L, GetEntryField<long>(pathEntry, "RemoteInventoryId"));
        }

        [Test]
        public void LeavingVisitorPreview_ClearsVisibleManifestMaps()
        {
            var controller = CreateController();
            SetReadOnlyPreview(controller, true);
            AddVisibleInventoryAssetMap(controller, inventoryId: 500L, assetId: 77L);
            AddVisibleRemoteManifest(controller, assetId: 77L);

            InvokeSetReadOnlySpacePreview(controller, false);

            Assert.AreEqual(0, GetDictionaryCount(controller, "visibleAssetIdByInventoryId"));
            Assert.AreEqual(0, GetDictionaryCount(controller, "visibleRemoteAssetManifests"));
            Assert.AreEqual(0, GetDictionaryCount(controller, "visibleLocalAssetEntries"));
            Assert.AreEqual(0, GetDictionaryCount(controller, "visibleLocalAssetEntriesByInventoryId"));
            Assert.AreEqual(0, GetDictionaryCount(controller, "visibleLocalAssetEntriesByResourceKey"));
            Assert.AreEqual(0, GetDictionaryCount(controller, "visibleAssetIdByResourceKey"));
        }

        [Test]
        public void LeavingOwnPreview_ClearsVisibleManifestMapsEvenWhenAlreadyWritable()
        {
            var controller = CreateController();
            SetReadOnlyPreview(controller, false);
            AddVisibleInventoryAssetMap(controller, inventoryId: 500L, assetId: 77L);
            AddVisibleRemoteManifest(controller, assetId: 77L);
            AddVisibleResourceAssetMap(controller, "Assets/Arts/Models/spooky_bed.glb", assetId: 77L);

            InvokeSetReadOnlySpacePreview(controller, false);

            Assert.AreEqual(0, GetDictionaryCount(controller, "visibleAssetIdByInventoryId"));
            Assert.AreEqual(0, GetDictionaryCount(controller, "visibleRemoteAssetManifests"));
            Assert.AreEqual(0, GetDictionaryCount(controller, "visibleLocalAssetEntries"));
            Assert.AreEqual(0, GetDictionaryCount(controller, "visibleLocalAssetEntriesByInventoryId"));
            Assert.AreEqual(0, GetDictionaryCount(controller, "visibleLocalAssetEntriesByResourceKey"));
            Assert.AreEqual(0, GetDictionaryCount(controller, "visibleAssetIdByResourceKey"));
        }

        [Test]
        public void VisitorPreviewSave_IsRejectedBeforeAnySuccessPath()
        {
            var controller = CreateController();
            SetReadOnlyPreview(controller, true);
            var succeeded = false;
            string error = null;

            InvokePerformSave(
                controller,
                onSuccess: () => succeeded = true,
                onError: message => error = message);

            Assert.IsFalse(succeeded);
            Assert.AreEqual("read-only space preview", error);
        }

        [Test]
        public void VisitorPreviewInventoryResolution_UsesVisibleOwnerMapNotLocalOwnerMap()
        {
            var controller = CreateController();
            SetApplyingReadOnlyPreview(controller, true);
            AddOwnedInventoryAssetMap(controller, inventoryId: 500L, assetId: 11L);
            AddVisibleInventoryAssetMap(controller, inventoryId: 500L, assetId: 77L);

            var resolved = InvokeTryResolveOwnedAssetIdForInventory(controller, 500L, out var assetId);

            Assert.IsTrue(resolved);
            Assert.AreEqual(77L, assetId);
        }

        [Test]
        public void OwnSpaceInventoryResolution_UsesLocalOwnerMap()
        {
            var controller = CreateController();
            SetApplyingReadOnlyPreview(controller, false);
            AddOwnedInventoryAssetMap(controller, inventoryId: 500L, assetId: 11L);
            AddVisibleInventoryAssetMap(controller, inventoryId: 500L, assetId: 77L);

            var resolved = InvokeTryResolveOwnedAssetIdForInventory(controller, 500L, out var assetId);

            Assert.IsTrue(resolved);
            Assert.AreEqual(11L, assetId);
        }

        [Test]
        public void ClearingExternalLayout_RemovesVisitorPlacedDecorations()
        {
            var controller = CreateController();
            InvokeEnsurePlacedItemsRoot(controller);
            var root = GetPlacedItemsRoot(controller);
            var child = new GameObject("VisitorDecoration");
            child.transform.SetParent(root.transform, false);
            AddPlacedByInventoryKey(controller, "asset:77", child);

            InvokeApplyExternalDecorationLayoutPayload(controller, null, clearExisting: true);

            Assert.AreEqual(0, root.transform.childCount);
            Assert.AreEqual(0, GetDictionaryCount(controller, "placedByInventoryKey"));
        }

        [Test]
        public void ClearingExternalLayout_InvalidatesPendingRemoteLayoutRestores()
        {
            var controller = CreateController();

            var initialVersion = GetField<int>(controller, "externalDecorationLayoutVersion");
            InvokeApplyExternalDecorationLayoutPayload(controller, null, clearExisting: true);
            var clearedVersion = GetField<int>(controller, "externalDecorationLayoutVersion");
            InvokeInvalidateExternalDecorationLayoutRestores(controller);
            var invalidatedVersion = GetField<int>(controller, "externalDecorationLayoutVersion");

            Assert.Greater(clearedVersion, initialVersion);
            Assert.Greater(invalidatedVersion, clearedVersion);
        }

        [Test]
        public void PreparingOwnSpaceRefresh_ClearsVisitorPreviewStateAndLayout()
        {
            var controller = CreateController();
            SetReadOnlyPreview(controller, true);
            AddVisibleRemoteManifest(controller, assetId: 77L);
            AddVisibleInventoryAssetMap(controller, inventoryId: 500L, assetId: 77L);
            AddVisibleResourceAssetMap(controller, "Assets/Arts/Models/spooky_bed.glb", assetId: 77L);
            InvokeEnsurePlacedItemsRoot(controller);
            var root = GetPlacedItemsRoot(controller);
            var child = new GameObject("VisitorDecoration");
            child.transform.SetParent(root.transform, false);
            AddPlacedByInventoryKey(controller, "asset:77", child);

            var sync = host.AddComponent(SyncControllerType);
            SetField(sync, "currentSpaceId", (long?)1005L);
            SetField(sync, "isViewingOtherUserSpace", true);
            SetField(controller, "hasPrePreviewAuthoritativeVisualSnapshot", true);

            InvokePrivate(sync, "PrepareOwnSpaceRefreshVisualIsolation");

            Assert.IsFalse(GetProperty<bool>(controller, "IsReadOnlySpacePreview"));
            Assert.AreEqual(0, GetDictionaryCount(controller, "visibleAssetIdByInventoryId"));
            Assert.AreEqual(0, GetDictionaryCount(controller, "visibleRemoteAssetManifests"));
            Assert.AreEqual(0, GetDictionaryCount(controller, "visibleLocalAssetEntries"));
            Assert.AreEqual(0, GetDictionaryCount(controller, "visibleLocalAssetEntriesByInventoryId"));
            Assert.AreEqual(0, GetDictionaryCount(controller, "visibleLocalAssetEntriesByResourceKey"));
            Assert.AreEqual(0, GetDictionaryCount(controller, "visibleAssetIdByResourceKey"));
            Assert.AreEqual(0, root.transform.childCount);
            Assert.AreEqual(0, GetDictionaryCount(controller, "placedByInventoryKey"));
            Assert.IsFalse(GetField<bool>(controller, "hasPrePreviewAuthoritativeVisualSnapshot"));
            Assert.IsNull(GetField<long?>(sync, "currentSpaceId"));
            Assert.IsFalse(GetField<bool>(sync, "isViewingOtherUserSpace"));
        }

        [Test]
        public void PreparingOwnSpaceRefresh_PreservesOwnLayoutWhenNoVisitorPreviewIsActive()
        {
            var controller = CreateController();
            SetReadOnlyPreview(controller, false);
            InvokeEnsurePlacedItemsRoot(controller);
            var root = GetPlacedItemsRoot(controller);
            var child = new GameObject("OwnDecoration");
            child.transform.SetParent(root.transform, false);
            AddPlacedByInventoryKey(controller, "asset:77", child);

            var sync = host.AddComponent(SyncControllerType);
            SetField(sync, "currentSpaceId", (long?)1001L);
            SetField(sync, "isViewingOtherUserSpace", false);

            InvokePrivate(sync, "PrepareOwnSpaceRefreshVisualIsolation");

            Assert.AreEqual(1, root.transform.childCount);
            Assert.AreEqual(1, GetDictionaryCount(controller, "placedByInventoryKey"));
            Assert.IsNull(GetField<long?>(sync, "currentSpaceId"));
            Assert.IsFalse(GetField<bool>(sync, "isViewingOtherUserSpace"));
        }

        [Test]
        public void VisitorPreviewVisuals_DoNotReplaceAuthoritativeSpaceOrAppliedEntries()
        {
            var controller = CreateController();
            var ownSpace = new SpaceResponseDto
            {
                Id = 1001L,
                OwnerUserId = 10L,
                SpaceName = "Mine"
            };
            var visitedSpace = new SpaceResponseDto
            {
                Id = 2002L,
                OwnerUserId = 20L,
                SpaceName = "Visited",
                CurrentDeskInventoryId = 500L
            };
            SetReadOnlyPreview(controller, true);
            SetField(controller, "lastServerSpace", ownSpace);
            AddLastAppliedEntry(controller, CustomizationCategory.Desk, 11L);
            AddLastAppliedEntry(controller, CustomizationCategory.Wallpaper, 22L);

            InvokeApplyServerSpacePreviewVisuals(controller, visitedSpace);

            Assert.AreSame(ownSpace, GetField<SpaceResponseDto>(controller, "lastServerSpace"));
            Assert.AreEqual(2, GetDictionaryCount(controller, "lastAppliedEntries"));
            Assert.AreEqual(11L, GetLastAppliedEntry(controller, CustomizationCategory.Desk));
            Assert.AreEqual(22L, GetLastAppliedEntry(controller, CustomizationCategory.Wallpaper));
            Assert.IsTrue(GetField<bool>(controller, "spaceVisualsRequireAuthoritativeReapply"));
            Assert.IsTrue(GetField<bool>(controller, "hasPrePreviewAuthoritativeVisualSnapshot"));
        }

        [Test]
        public void RefreshFromServer_WithoutTokenStillClearsVisitorPreviewState()
        {
            var controller = CreateController();
            SetReadOnlyPreview(controller, true);
            AddVisibleRemoteManifest(controller, assetId: 77L);
            AddVisibleInventoryAssetMap(controller, inventoryId: 500L, assetId: 77L);
            InvokeEnsurePlacedItemsRoot(controller);
            var root = GetPlacedItemsRoot(controller);
            var child = new GameObject("VisitorDecoration");
            child.transform.SetParent(root.transform, false);
            AddPlacedByInventoryKey(controller, "asset:77", child);

            var sync = host.AddComponent(SyncControllerType);
            SetField(sync, "client", CreateNotebookApiClient());
            SetField(sync, "generator", host.AddComponent(GeneratorType));
            SetField(sync, "configured", true);
            SetField(sync, "currentSpaceId", (long?)1005L);
            SetField(sync, "isViewingOtherUserSpace", true);
            SetField(sync, "isRefreshing", true);
            SetField(sync, "activeRefreshTargetsOtherUserSpace", true);
            var completed = false;
            var completionSuccess = true;

            InvokeRefreshFromServer(
                sync,
                (success, _) =>
                {
                    completed = true;
                    completionSuccess = success;
                });

            Assert.IsTrue(completed);
            Assert.IsFalse(completionSuccess);
            Assert.IsFalse(GetProperty<bool>(controller, "IsReadOnlySpacePreview"));
            Assert.AreEqual(0, GetDictionaryCount(controller, "visibleRemoteAssetManifests"));
            Assert.AreEqual(0, GetDictionaryCount(controller, "visibleAssetIdByInventoryId"));
            Assert.AreEqual(0, root.transform.childCount);
            Assert.AreEqual(0, GetDictionaryCount(controller, "placedByInventoryKey"));
            Assert.IsNull(GetField<long?>(sync, "currentSpaceId"));
            Assert.IsFalse(GetField<bool>(sync, "isViewingOtherUserSpace"));
            Assert.IsFalse(GetField<bool>(sync, "isRefreshing"));
            Assert.IsFalse(GetField<bool>(sync, "activeRefreshTargetsOtherUserSpace"));
        }

        [Test]
        public void RefreshFromServer_CancelsActiveVisitorCompletionBeforeOwnRefresh()
        {
            var controller = CreateController();
            SetReadOnlyPreview(controller, true);
            InvokeEnsurePlacedItemsRoot(controller);
            var root = GetPlacedItemsRoot(controller);
            var child = new GameObject("VisitorDecoration");
            child.transform.SetParent(root.transform, false);
            AddPlacedByInventoryKey(controller, "asset:77", child);

            var sync = host.AddComponent(SyncControllerType);
            SetField(sync, "client", CreateNotebookApiClient());
            SetField(sync, "generator", host.AddComponent(GeneratorType));
            SetField(sync, "configured", true);
            SetField(sync, "currentSpaceId", (long?)1005L);
            SetField(sync, "isViewingOtherUserSpace", true);
            SetField(sync, "isRefreshing", true);
            SetField(sync, "activeRefreshTargetsOtherUserSpace", true);

            var staleCompleted = false;
            var staleSuccess = true;
            var newCompleted = false;
            var newSuccess = true;
            SetField(
                sync,
                "refreshCompletion",
                new Action<bool, string>((success, _) =>
                {
                    staleCompleted = true;
                    staleSuccess = success;
                }));

            InvokeRefreshFromServer(
                sync,
                (success, _) =>
                {
                    newCompleted = true;
                    newSuccess = success;
                });

            Assert.IsTrue(staleCompleted);
            Assert.IsFalse(staleSuccess);
            Assert.IsTrue(newCompleted);
            Assert.IsFalse(newSuccess);
            Assert.IsFalse(GetProperty<bool>(controller, "IsReadOnlySpacePreview"));
            Assert.AreEqual(0, root.transform.childCount);
            Assert.AreEqual(0, GetDictionaryCount(controller, "placedByInventoryKey"));
        }

        [Test]
        public void RefreshFromServer_WhenNotConfiguredStillClearsVisitorPreviewState()
        {
            var controller = CreateController();
            SetReadOnlyPreview(controller, true);
            AddVisibleRemoteManifest(controller, assetId: 77L);
            AddVisibleInventoryAssetMap(controller, inventoryId: 500L, assetId: 77L);
            InvokeEnsurePlacedItemsRoot(controller);
            var root = GetPlacedItemsRoot(controller);
            var child = new GameObject("VisitorDecoration");
            child.transform.SetParent(root.transform, false);
            AddPlacedByInventoryKey(controller, "asset:77", child);

            var sync = host.AddComponent(SyncControllerType);
            SetField(sync, "currentSpaceId", (long?)1005L);
            SetField(sync, "isViewingOtherUserSpace", true);
            SetField(sync, "isRefreshing", true);
            SetField(sync, "activeRefreshTargetsOtherUserSpace", true);
            var completed = false;
            var completionSuccess = true;

            InvokeRefreshFromServer(
                sync,
                (success, _) =>
                {
                    completed = true;
                    completionSuccess = success;
                });

            Assert.IsTrue(completed);
            Assert.IsFalse(completionSuccess);
            Assert.IsFalse(GetProperty<bool>(controller, "IsReadOnlySpacePreview"));
            Assert.AreEqual(0, GetDictionaryCount(controller, "visibleRemoteAssetManifests"));
            Assert.AreEqual(0, GetDictionaryCount(controller, "visibleAssetIdByInventoryId"));
            Assert.AreEqual(0, root.transform.childCount);
            Assert.AreEqual(0, GetDictionaryCount(controller, "placedByInventoryKey"));
            Assert.IsNull(GetField<long?>(sync, "currentSpaceId"));
            Assert.IsFalse(GetField<bool>(sync, "isViewingOtherUserSpace"));
            Assert.IsFalse(GetField<bool>(sync, "isRefreshing"));
            Assert.IsFalse(GetField<bool>(sync, "activeRefreshTargetsOtherUserSpace"));
        }

        private static Type ControllerType
        {
            get
            {
                var type = FindType("App.Bootstrap.RoomEditModeController");
                Assert.IsNotNull(type);
                return type;
            }
        }

        private static Type SyncControllerType
        {
            get
            {
                var type = FindType("BookshelfPorting.Runtime.BookshelfApiSyncController");
                Assert.IsNotNull(type);
                return type;
            }
        }

        private static Type GeneratorType
        {
            get
            {
                var type = FindType("BookshelfPorting.Runtime.BookshelfGenerator");
                Assert.IsNotNull(type);
                return type;
            }
        }

        private static Type EntryType
        {
            get
            {
                var type = ControllerType.GetNestedType("InventoryEntry", BindingFlags.NonPublic);
                Assert.IsNotNull(type);
                return type;
            }
        }

        private object CreateController()
        {
            host = new GameObject(nameof(RoomEditModeVisitorIsolationTests));
            return host.AddComponent(ControllerType);
        }

        private static void SetReadOnlyPreview(object controller, bool value)
        {
            var field = ControllerType.GetField(
                "<IsReadOnlySpacePreview>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(controller, value);
        }

        private static void SetApplyingReadOnlyPreview(object controller, bool value)
        {
            var field = ControllerType.GetField(
                "isApplyingReadOnlySpacePreviewVisuals",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(controller, value);
        }

        private static void AddInventoryEntry(
            object controller,
            long remoteAssetId,
            long remoteInventoryId,
            string sourceAssetPath = "")
        {
            var entry = CreateInventoryEntry(remoteAssetId, remoteInventoryId, sourceAssetPath);

            var field = ControllerType.GetField("inventoryEntries", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            var entries = (IList)field.GetValue(controller);
            entries.Add(entry);
        }

        private static object CreateInventoryEntry(
            long remoteAssetId,
            long remoteInventoryId,
            string sourceAssetPath = "")
        {
            var entry = Activator.CreateInstance(EntryType);
            SetEntryField(entry, "DisplayName", "Local Asset");
            SetEntryField(entry, "SourceAssetPath", sourceAssetPath ?? string.Empty);
            SetEntryField(entry, "RemoteAssetId", remoteAssetId);
            SetEntryField(entry, "RemoteInventoryId", remoteInventoryId);
            return entry;
        }

        private static void AddVisibleInventoryAssetMap(object controller, long inventoryId, long assetId)
        {
            var field = ControllerType.GetField(
                "visibleAssetIdByInventoryId",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            var map = (IDictionary)field.GetValue(controller);
            map[inventoryId] = assetId;
        }

        private static void AddVisibleResourceAssetMap(object controller, string resourceKey, long assetId)
        {
            var field = ControllerType.GetField(
                "visibleAssetIdByResourceKey",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            var map = (IDictionary)field.GetValue(controller);
            map[NormalizeResourceKey(resourceKey)] = assetId;
        }

        private static void AddVisibleRemoteManifest(object controller, long assetId)
        {
            var field = ControllerType.GetField(
                "visibleRemoteAssetManifests",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            var map = (IDictionary)field.GetValue(controller);
            map[assetId] = new DownloadManifestResponseDto
            {
                AssetId = assetId,
                Name = "Visible Remote Asset"
            };
        }

        private static void AddOwnedInventoryAssetMap(object controller, long inventoryId, long assetId)
        {
            var field = ControllerType.GetField(
                "ownedAssetIdByInventoryId",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            var map = (IDictionary)field.GetValue(controller);
            map[inventoryId] = assetId;
        }

        private static void AddLastAppliedEntry(object controller, CustomizationCategory category, long assetId)
        {
            var field = ControllerType.GetField(
                "lastAppliedEntries",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            var map = (IDictionary)field.GetValue(controller);
            map[category] = assetId;
        }

        private static long GetLastAppliedEntry(object controller, CustomizationCategory category)
        {
            var field = ControllerType.GetField(
                "lastAppliedEntries",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            var map = (IDictionary)field.GetValue(controller);
            Assert.IsTrue(map.Contains(category));
            return (long)map[category];
        }

        private static void AddPlacedByInventoryKey(object controller, string inventoryKey, GameObject instance)
        {
            var field = ControllerType.GetField(
                "placedByInventoryKey",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            var map = (IDictionary)field.GetValue(controller);
            map[inventoryKey] = instance;
        }

        private static string NormalizeResourceKey(string key)
        {
            return string.IsNullOrWhiteSpace(key)
                ? string.Empty
                : key.Trim().Replace('\\', '/').TrimStart('/').ToLowerInvariant();
        }

        private static void InvokeSetReadOnlySpacePreview(object controller, bool value)
        {
            var method = ControllerType.GetMethod(
                "SetReadOnlySpacePreview",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(method);
            method.Invoke(controller, new object[] { value });
        }

        private static void InvokeRegisterVisibleSpaceAssetManifests(
            object controller,
            IReadOnlyList<DownloadManifestResponseDto> manifests)
        {
            var method = ControllerType.GetMethod(
                "RegisterVisibleSpaceAssetManifests",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(method);
            method.Invoke(controller, new object[] { manifests });
        }

        private static int GetDictionaryCount(object controller, string fieldName)
        {
            var field = ControllerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            return ((IDictionary)field.GetValue(controller)).Count;
        }

        private static bool InvokeTryFindInventoryEntry(object controller, string key, out object entry)
        {
            var method = ControllerType.GetMethod(
                "TryFindInventoryEntryByKey",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);

            var args = new object[] { key, null };
            var result = (bool)method.Invoke(controller, args);
            entry = args[1];
            return result;
        }

        private static bool InvokeTryCreateRemoteInventoryEntry(object controller, string key, out object entry)
        {
            var method = ControllerType.GetMethod(
                "TryCreateRemoteInventoryEntryFromKey",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);

            var args = new object[] { key, null };
            var result = (bool)method.Invoke(controller, args);
            entry = args[1];
            return result;
        }

        private static IEnumerator InvokeLoadRemoteAssetGameObject(
            object controller,
            object entry,
            Action<GameObject> onSuccess,
            Action<string> onError)
        {
            var method = ControllerType.GetMethod(
                "LoadRemoteAssetGameObject",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);

            return (IEnumerator)method.Invoke(controller, new object[] { entry, onSuccess, onError });
        }

        private static bool InvokeTryResolveOwnedAssetIdForInventory(object controller, long inventoryId, out long assetId)
        {
            var method = ControllerType.GetMethod(
                "TryResolveOwnedAssetIdForInventory",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);

            var args = new object[] { inventoryId, 0L };
            var result = (bool)method.Invoke(controller, args);
            assetId = (long)args[1];
            return result;
        }

        private static void InvokeEnsurePlacedItemsRoot(object controller)
        {
            var method = ControllerType.GetMethod(
                "EnsurePlacedItemsRoot",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            method.Invoke(controller, Array.Empty<object>());
        }

        private static GameObject GetPlacedItemsRoot(object controller)
        {
            var field = ControllerType.GetField(
                "placedItemsRoot",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            return (GameObject)field.GetValue(controller);
        }

        private static void InvokeApplyExternalDecorationLayoutPayload(object controller, string layoutPayload, bool clearExisting)
        {
            var method = ControllerType.GetMethod(
                "ApplyExternalDecorationLayoutPayload",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(method);
            method.Invoke(controller, new object[] { layoutPayload, clearExisting });
        }

        private static void InvokeInvalidateExternalDecorationLayoutRestores(object controller)
        {
            var method = ControllerType.GetMethod(
                "InvalidateExternalDecorationLayoutRestores",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(method);
            method.Invoke(controller, Array.Empty<object>());
        }

        private static void InvokeRefreshFromServer(object sync, Action<bool, string> onCompleted)
        {
            var method = sync.GetType().GetMethod(
                "RefreshFromServer",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(method);
            method.Invoke(sync, new object[] { onCompleted });
        }

        private static void InvokeApplyServerSpacePreviewVisuals(object controller, SpaceResponseDto space)
        {
            var method = ControllerType.GetMethod(
                "ApplyServerSpacePreviewVisuals",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(method);
            method.Invoke(controller, new object[] { space });
        }

        private static void InvokePerformSave(object controller, Action onSuccess, Action<string> onError)
        {
            var method = ControllerType.GetMethod(
                "PerformSave",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            method.Invoke(controller, new object[] { onSuccess, onError });
        }

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            var parameters = method.GetParameters();
            var args = parameters.Length == 0
                ? Array.Empty<object>()
                : new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : Type.Missing;
            }

            method.Invoke(target, args);
        }

        private static object CreateNotebookApiClient()
        {
            var type = FindType("App.Infrastructure.Api.NotebookApiClient");
            Assert.IsNotNull(type);
            return Activator.CreateInstance(type, new object[] { null });
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(target, value);
        }

        private static T GetField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            return (T)field.GetValue(target);
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(property);
            return (T)property.GetValue(target);
        }

        private static void SetEntryField(object entry, string fieldName, object value)
        {
            var field = EntryType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(field);
            field.SetValue(entry, value);
        }

        private static T GetEntryField<T>(object entry, string fieldName)
        {
            var field = EntryType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(field);
            return (T)field.GetValue(entry);
        }

        private static Type FindType(string fullName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                var type = assemblies[i].GetType(fullName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
