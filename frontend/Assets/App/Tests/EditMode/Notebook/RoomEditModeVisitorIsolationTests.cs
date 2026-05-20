using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using App.Application.Dashboard;
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
        public void OwnSpaceInventoryKey_CreatesRemoteEntryFromOwnedInventoryMap()
        {
            var controller = CreateController();
            SetReadOnlyPreview(controller, false);
            AddOwnedInventoryAssetMap(controller, inventoryId: 500L, assetId: 77L);

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
        public void ReplacingExternalLayout_DoesNotRebuildCacheFromSceneMarkers()
        {
            var controller = CreateController();
            InvokeEnsurePlacedItemsRoot(controller);
            var outside = new GameObject("OutsideDecorationMarker");
            outside.transform.SetParent(host.transform, false);
            AddDecorationMarker(outside, "asset:77");

            InvokeApplyExternalDecorationLayoutPayload(controller, null, clearExisting: true);

            Assert.AreEqual(0, GetDictionaryCount(controller, "placedByInventoryKey"));
        }

        [Test]
        public void ThemeContentsWithoutPlacementChildren_DoesNotSuppressLocalThemeSpecs()
        {
            var emptyContents = new ThemeContents
            {
                Children = new List<ThemeContentItem>()
            };
            var requiredOnlyContents = new ThemeContents
            {
                Children = new List<ThemeContentItem>
                {
                    new ThemeContentItem { Required = true }
                }
            };
            var placementContents = new ThemeContents
            {
                Children = new List<ThemeContentItem>
                {
                    new ThemeContentItem { AssetId = 77L }
                }
            };

            Assert.IsFalse(InvokeHasThemePlacementContents(emptyContents));
            Assert.IsFalse(InvokeHasThemePlacementContents(requiredOnlyContents));
            Assert.IsTrue(InvokeHasThemePlacementContents(placementContents));
        }

        [Test]
        public void EditModeRollbackHold_PreservesOriginalPlacementsAcrossRepeatedThemeSelection()
        {
            var controller = CreateController();
            InvokeEnsurePlacedItemsRoot(controller);
            SetEditModeActive(controller, true);
            var root = GetPlacedItemsRoot(controller);
            var first = new GameObject("FirstOriginalDecoration");
            first.transform.SetParent(root.transform, false);

            InvokeDetachPlacementForRollback(controller, detachAllPlacements: true);
            var hold = GetField<GameObject>(controller, "placementRollbackHoldRoot");

            Assert.IsNotNull(hold);
            Assert.AreEqual(1, hold.transform.childCount);
            Assert.AreEqual(0, root.transform.childCount);

            var second = new GameObject("SecondOriginalDecoration");
            second.transform.SetParent(root.transform, false);

            InvokeDetachPlacementForRollback(controller, detachAllPlacements: true);
            var currentHold = GetField<GameObject>(controller, "placementRollbackHoldRoot");

            Assert.AreSame(hold, currentHold);
            Assert.AreEqual(2, currentHold.transform.childCount);
            Assert.AreEqual(0, root.transform.childCount);
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

        [Test]
        public void RemoteFixedAssetSlot_MatchesCatalogBackedAssetByServerAssetId()
        {
            var remoteEntry = CreateInventoryEntry(remoteAssetId: 300020L, remoteInventoryId: 900L);
            SetEntryField(remoteEntry, "IsRemoteAsset", true);
            SetEntryField(remoteEntry, "PlacementCategory", CustomizationCategory.BookshelfSkin);
            SetEntryField(remoteEntry, "ThemeTag", "DeepSea");
            SetEntryField(remoteEntry, "ServerAssetType", "BOOKSHELF_SKIN");

            var catalogEntry = CreateCustomizationEntry(300020L, "깊은바다 책장", "DeepSea");

            Assert.IsTrue(InvokeDoesRemoteInventoryEntryMatchCatalogEntry(
                CustomizationCategory.BookshelfSkin,
                remoteEntry,
                catalogEntry));
        }

        [Test]
        public void RemoteFixedAssetSlot_DoesNotMatchDifferentCatalogAsset()
        {
            var remoteEntry = CreateInventoryEntry(remoteAssetId: 300020L, remoteInventoryId: 900L);
            SetEntryField(remoteEntry, "IsRemoteAsset", true);
            SetEntryField(remoteEntry, "PlacementCategory", CustomizationCategory.BookshelfSkin);
            SetEntryField(remoteEntry, "ThemeTag", string.Empty);
            SetEntryField(remoteEntry, "ServerAssetType", "BOOKSHELF_SKIN");

            var catalogEntry = CreateCustomizationEntry(400020L, "판타지 책장", "Fantasy");

            Assert.IsFalse(InvokeDoesRemoteInventoryEntryMatchCatalogEntry(
                CustomizationCategory.BookshelfSkin,
                remoteEntry,
                catalogEntry));
        }

        [Test]
        public void RemoteFixedAssetSlot_DoesNotMatchSameThemeDifferentServerAssetId()
        {
            var remoteEntry = CreateInventoryEntry(remoteAssetId: 900123L, remoteInventoryId: 901L);
            SetEntryField(remoteEntry, "IsRemoteAsset", true);
            SetEntryField(remoteEntry, "PlacementCategory", CustomizationCategory.BookshelfSkin);
            SetEntryField(remoteEntry, "ThemeTag", "DeepSea");
            SetEntryField(remoteEntry, "ServerAssetType", "BOOKSHELF_SKIN");

            var catalogEntry = CreateCustomizationEntry(300020L, "깊은바다 책장", "DeepSea");

            Assert.IsFalse(InvokeDoesRemoteInventoryEntryMatchCatalogEntry(
                CustomizationCategory.BookshelfSkin,
                remoteEntry,
                catalogEntry));
        }

        [Test]
        public void ReloadRoomState_SkipsApplyWhenEditModeHasPendingVisualChange()
        {
            var controller = CreateController();
            SetEditModeActive(controller, true);

            var capturedCategoryVersion = GetField<int>(controller, "categoryRequestVersion");
            var capturedEditVisualVersion = GetField<int>(controller, "editVisualChangeVersion");
            SetField(controller, "hasPendingThemeVisualSave", true);

            Assert.IsTrue(InvokeShouldSkipReloadedSpaceVisualApply(
                controller,
                capturedCategoryVersion,
                capturedEditVisualVersion));
        }

        [Test]
        public void ReloadRoomState_SkipsApplyWhenEditVisualVersionChanged()
        {
            var controller = CreateController();
            SetEditModeActive(controller, true);

            var capturedCategoryVersion = GetField<int>(controller, "categoryRequestVersion");
            var capturedEditVisualVersion = GetField<int>(controller, "editVisualChangeVersion");
            SetField(controller, "editVisualChangeVersion", capturedEditVisualVersion + 1);

            Assert.IsTrue(InvokeShouldSkipReloadedSpaceVisualApply(
                controller,
                capturedCategoryVersion,
                capturedEditVisualVersion));
        }

        [Test]
        public void BookshelfStyleSelection_PrefersExplicitShelfStyleOverCurrentTheme()
        {
            host = new GameObject(nameof(BookshelfStyleSelection_PrefersExplicitShelfStyleOverCurrentTheme));
            var generator = host.AddComponent(GeneratorType);
            var themeHost = new GameObject("RoomThemeApplier");
            themeHost.transform.SetParent(host.transform, false);
            var applier = themeHost.AddComponent(RoomThemeApplierType);

            var currentTheme = ScriptableObject.CreateInstance<RoomThemeAsset>();
            var catalog = ScriptableObject.CreateInstance(BookshelfThemeLayoutCatalogType);
            var deepSea = BookshelfThemeStyleValue("DeepSea");
            var fantasy = BookshelfThemeStyleValue("Fantasy");
            var deepSeaLayout = CreateBookshelfThemeLayout(deepSea);
            var fantasyLayout = CreateBookshelfThemeLayout(fantasy);
            try
            {
                SetField(currentTheme, "bookshelfStyleIndex", Convert.ToInt32(deepSea));
                SetField(applier, "currentTheme", currentTheme);
                SetField(catalog, "layouts", CreateBookshelfThemeLayoutArray(deepSeaLayout, fantasyLayout));
                SetField(generator, "cachedRoomThemeApplier", applier);
                SetField(generator, "bookshelfThemeLayoutCatalog", catalog);

                var styles = (IDictionary)GetField<object>(generator, "bookshelfDesignStyles");
                styles[0] = fantasy;

                Assert.AreEqual(fantasy, InvokeResolveActiveNavigationThemeStyle(generator));
                Assert.AreEqual(fantasy, InvokeResolveRenderedBookshelfDesignStyle(generator, 0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(currentTheme);
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate((UnityEngine.Object)deepSeaLayout);
                UnityEngine.Object.DestroyImmediate((UnityEngine.Object)fantasyLayout);
            }
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

        private static Type RoomThemeApplierType
        {
            get
            {
                var type = FindType("Dotshelf.Scripts.RoomTheme.RoomThemeApplier");
                Assert.IsNotNull(type);
                return type;
            }
        }

        private static Type BookshelfThemeStyleType
        {
            get
            {
                var type = FindType("BookshelfPorting.Runtime.BookshelfThemeStyle");
                Assert.IsNotNull(type);
                return type;
            }
        }

        private static Type BookshelfThemeLayoutType
        {
            get
            {
                var type = FindType("BookshelfPorting.Runtime.BookshelfThemeLayout");
                Assert.IsNotNull(type);
                return type;
            }
        }

        private static Type BookshelfThemeLayoutCatalogType
        {
            get
            {
                var type = FindType("BookshelfPorting.Runtime.BookshelfThemeLayoutCatalog");
                Assert.IsNotNull(type);
                return type;
            }
        }

        private static Type BookshelfSectionLayoutType
        {
            get
            {
                var type = FindType("BookshelfPorting.Runtime.BookshelfSectionLayout");
                Assert.IsNotNull(type);
                return type;
            }
        }

        private static Type ShelfSectionAlignmentType
        {
            get
            {
                var type = FindType("BookshelfPorting.Runtime.ShelfSectionAlignment");
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

        private static void SetEditModeActive(object controller, bool value)
        {
            var field = ControllerType.GetField(
                "<IsEditModeActive>k__BackingField",
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

        private static CustomizationEntry CreateCustomizationEntry(long assetId, string displayName, string themeTag)
        {
            var entry = new CustomizationEntry();
            SetField(entry, "assetId", assetId);
            SetField(entry, "displayName", displayName);
            SetField(entry, "themeTag", themeTag);
            return entry;
        }

        private static object BookshelfThemeStyleValue(string name)
        {
            return Enum.Parse(BookshelfThemeStyleType, name);
        }

        private static object CreateBookshelfThemeLayout(object style)
        {
            var layout = ScriptableObject.CreateInstance(BookshelfThemeLayoutType);
            SetField(layout, "style", style);
            SetField(layout, "focusLocalPosition", new Vector3(0f, 0.8f, -0.175f));
            SetField(layout, "focusSize", new Vector3(1.7f, 1.7f, 0.18f));
            var sections = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(BookshelfSectionLayoutType));
            sections.Add(Activator.CreateInstance(
                BookshelfSectionLayoutType,
                new[]
                {
                    0,
                    0,
                    -0.5f,
                    0.5f,
                    0.1f,
                    0.4f,
                    0f,
                    -0.2f,
                    0.2f,
                    Enum.Parse(ShelfSectionAlignmentType, "Left"),
                    false
                }));
            SetField(
                layout,
                "sections",
                sections);
            return layout;
        }

        private static Array CreateBookshelfThemeLayoutArray(params object[] layouts)
        {
            var array = Array.CreateInstance(BookshelfThemeLayoutType, layouts.Length);
            for (var i = 0; i < layouts.Length; i++)
            {
                array.SetValue(layouts[i], i);
            }

            return array;
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

        private static void AddDecorationMarker(GameObject instance, string inventoryKey)
        {
            var markerType = FindType("App.Bootstrap.DecorationItemMarker");
            Assert.IsNotNull(markerType);
            var marker = instance.AddComponent(markerType);
            var field = markerType.GetField("InventoryKey", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(field);
            field.SetValue(marker, inventoryKey);
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

        private static bool InvokeDoesRemoteInventoryEntryMatchCatalogEntry(
            CustomizationCategory category,
            object remoteEntry,
            CustomizationEntry catalogEntry)
        {
            var method = ControllerType.GetMethod(
                "DoesRemoteInventoryEntryMatchCatalogEntry",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            return (bool)method.Invoke(null, new object[] { category, remoteEntry, catalogEntry });
        }

        private static bool InvokeShouldSkipReloadedSpaceVisualApply(
            object controller,
            int capturedCategoryVersion,
            int capturedEditVisualVersion)
        {
            var method = ControllerType.GetMethod(
                "ShouldSkipReloadedSpaceVisualApply",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            return (bool)method.Invoke(
                controller,
                new object[] { capturedCategoryVersion, capturedEditVisualVersion });
        }

        private static object InvokeResolveActiveNavigationThemeStyle(object generator)
        {
            var method = GeneratorType.GetMethod(
                "ResolveActiveNavigationThemeStyle",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            return method.Invoke(generator, Array.Empty<object>());
        }

        private static object InvokeResolveRenderedBookshelfDesignStyle(object generator, int index)
        {
            var method = GeneratorType.GetMethod(
                "ResolveRenderedBookshelfDesignStyle",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            return method.Invoke(generator, new object[] { index });
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

        private static bool InvokeHasThemePlacementContents(ThemeContents contents)
        {
            var method = ControllerType.GetMethod(
                "HasThemePlacementContents",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            return (bool)method.Invoke(null, new object[] { contents });
        }

        private static void InvokeDetachPlacementForRollback(object controller, bool detachAllPlacements)
        {
            var method = ControllerType.GetMethod(
                "DetachPlacementForRollback",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            method.Invoke(controller, new object[] { null, detachAllPlacements });
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
