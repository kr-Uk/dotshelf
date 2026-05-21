using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using App.Application.Notebook;
using App.Auth;
using App.Bootstrap;
using App.Domain;
using App.Infrastructure.Api;
using App.Infrastructure.Api.Dtos;
using App.Infrastructure.Assets;
using Dotshelf.Scripts.RoomTheme;
using UnityEngine;

namespace BookshelfPorting.Runtime
{
    public sealed class BookshelfApiSyncController : MonoBehaviour
    {
        private NotebookApiClient client;
        private NotebookRuntimeUseCase notebookRuntimeUseCase;
        private RemoteAssetCacheService remoteAssetCacheService;
        private BookshelfGenerator generator;
        private BookshelfExperienceManager experienceManager;
        private CameraController cameraController;
        private long? currentSpaceId;
        private bool configured;
        private bool isRefreshing;
        private bool isViewingOtherUserSpace;
        private bool isThumbnailUploading;
        private bool thumbnailUploadPending;
        private int refreshRequestVersion;
        private bool activeRefreshTargetsOtherUserSpace;
        private Action<NotebookSpaceThumbnailUploadResult> pendingThumbnailUploadSucceeded;
        private Action<string> pendingThumbnailUploadFailed;
        private Action<bool, string> refreshCompletion;
        private readonly List<Action<bool, string>> queuedRefreshCompletions = new List<Action<bool, string>>();
        private bool hasCompletedInitialRefresh;
        private bool lastInitialRefreshSucceeded;
        private string lastInitialRefreshMessage;
        private bool hasAppliedInitialOwnSpaceVisuals;
        private bool hasResolvedInitialOwnSpaceLayout;
        private int itemsLoadFailureCount;
        private string firstItemsLoadFailureMessage;
        private bool isBookshelfSkinCatalogLoading;
        private InventoryResponseDto cachedAssetInventory;
        private readonly Dictionary<long, long> visibleBookshelfSkinAssetIdByInventoryId = new Dictionary<long, long>();
        private long? visibleFallbackBookshelfSkinAssetId;
        private const int RoomThumbnailCaptureWidth = 2048;
        private const int RoomThumbnailCaptureHeight = 1152;
        private const int RoomThumbnailMaxUploadBytes = 2 * 1024 * 1024;
        private const float RoomThumbnailCaptureFieldOfViewScale = 0.74f;
        private const float RoomThumbnailCapturePitchDownDegrees = 3.0f;
        private const int RoomThumbnailCaptureSettleFrames = 3;
        private const float RoomThumbnailRefreshWaitTimeoutSeconds = 10f;
        private const string RoomThumbnailPngContentType = "image/png";
        private const string RoomThumbnailJpegContentType = "image/jpeg";
        private static readonly int[] RoomThumbnailJpegQualities = { 92, 86, 80, 72, 64 };
        // Lifetime: per-controller instance. BE visit endpoint is not strictly
        // idempotent (visit count) so we de-dupe at the FE level — re-entering
        // the same other-user space within a single play session counts once.
        // Resets when MainScene unloads (controller is destroyed with scene).
        private readonly HashSet<long> visitedSpaceIds = new HashSet<long>();

        public bool IsConfigured => configured && client != null && generator != null;

        private bool CanUseServerApi => IsConfigured;

        public bool IsServerSyncEnabled => CanUseServerApi;

        public bool IsRefreshing => isRefreshing;

        public bool IsViewingOtherUserSpace => isViewingOtherUserSpace;

        public long? CurrentSpaceId => currentSpaceId;

        public bool HasCompletedInitialRefresh => hasCompletedInitialRefresh;

        public bool LastInitialRefreshSucceeded => lastInitialRefreshSucceeded;

        public string LastInitialRefreshMessage => lastInitialRefreshMessage;

        public bool HasAppliedInitialOwnSpaceVisuals => hasAppliedInitialOwnSpaceVisuals;

        public bool HasResolvedInitialOwnSpaceLayout => hasResolvedInitialOwnSpaceLayout;

        public bool HasAccessToken => APIManager.Instance != null && APIManager.Instance.HasAccessToken;

        public void WaitForInitialRefresh(Action<bool, string> onCompleted)
        {
            if (onCompleted == null)
            {
                return;
            }

            if (hasCompletedInitialRefresh)
            {
                onCompleted(lastInitialRefreshSucceeded, lastInitialRefreshMessage);
                return;
            }

            RefreshFromServer(onCompleted);
        }

        public void Configure(
            NotebookApiClient apiClient,
            BookshelfGenerator bookshelfGenerator,
            BookshelfExperienceManager manager,
            CameraController controller = null,
            NotebookRuntimeUseCase runtimeUseCase = null)
        {
            client = apiClient;
            notebookRuntimeUseCase = runtimeUseCase ?? (apiClient != null
                ? new NotebookRuntimeUseCase(new ApiNotebookRuntimeRepository(apiClient, () => APIManager.Instance != null && APIManager.Instance.HasAccessToken))
                : null);
            remoteAssetCacheService ??= new RemoteAssetCacheService();
            generator = bookshelfGenerator;
            experienceManager = manager;
            cameraController = controller;
            configured = client != null && generator != null;

            if (configured && Application.isPlaying)
            {
                StartCoroutine(RefreshAfterSceneSettles());
            }
        }

        public void RefreshFromServer(Action<bool, string> onCompleted = null)
        {
            var cancellingVisitorRefresh = isRefreshing && activeRefreshTargetsOtherUserSpace;
            if (!IsConfigured)
            {
                if (cancellingVisitorRefresh)
                {
                    CompleteSupersededRefreshCallbacks("Visitor space refresh was cancelled.");
                }

                PrepareOwnSpaceRefreshVisualIsolation(cancellingVisitorRefresh);
                ResetRefreshState();
                CompleteRefreshWithoutRequest(onCompleted, false, "책장 API 동기화가 아직 준비되지 않았습니다.");
                return;
            }

            if (isRefreshing && !activeRefreshTargetsOtherUserSpace)
            {
                if (onCompleted != null)
                {
                    queuedRefreshCompletions.Add(onCompleted);
                }
                return;
            }

            if (cancellingVisitorRefresh)
            {
                CompleteSupersededRefreshCallbacks("Visitor space refresh was cancelled.");
            }

            var requestVersion = BeginRefresh(onCompleted, targetsOtherUserSpace: false);
            PrepareOwnSpaceRefreshVisualIsolation(cancellingVisitorRefresh);

            // /v1/spaces/me requires a Bearer token. Skip when anonymous
            // (token wiped on logout, or never set on first launch) so we
            // do not 401-spam BE. In RealApi mode, the bookshelf stays empty
            // until login instead of falling back to local catalog data.
            if (APIManager.Instance == null || !APIManager.Instance.HasAccessToken)
            {
                FinishRefreshIfCurrent(requestVersion, false, "로그인 정보가 없어 책장 새로고침을 건너뜁니다.");
                return;
            }

            RefreshBookshelfSkinCatalog(() =>
            {
                if (!IsRefreshRequestCurrent(requestVersion))
                {
                    return;
                }

                RefreshOwnSpaceBookshelves(requestVersion);
            });
        }

        private void RefreshOwnSpaceBookshelves(int requestVersion)
        {
            client.GetMySpace(
                space =>
                {
                    if (!IsRefreshRequestCurrent(requestVersion))
                    {
                        return;
                    }

                    currentSpaceId = space?.Id;
                    SetViewingOtherUserSpace(false);
                    experienceManager?.SetNotebookScreenSuppressed(false);
                    ApplyRoomVisuals(space, false);
                    hasAppliedInitialOwnSpaceVisuals = true;
                    // The main room is not a visitor-action surface; those
                    // actions live on notebook space cards.
                    SpaceVisitorOverlayController.Instance?.HideOverlay();
                    if (!currentSpaceId.HasValue || currentSpaceId.Value <= 0)
                    {
                        FinishRefreshIfCurrent(requestVersion, false, "내 공간 정보를 찾을 수 없습니다.");
                        return;
                    }

                    var ownSpaceId = currentSpaceId.Value;
                    LoadSpaceLayout(
                        ownSpaceId,
                        false,
                        requestVersion,
                        () =>
                        {
                            if (!IsRefreshRequestCurrent(requestVersion) ||
                                !IsCurrentSpaceView(ownSpaceId, false))
                            {
                                return;
                            }

                            client.GetBookshelves(
                                ownSpaceId,
                                response => LoadBookshelfItems(response, ownSpaceId, false, requestVersion, space.CurrentBookshelfSkinInventoryId),
                                error => FinishRefreshIfCurrent(requestVersion, false, FormatError(error)));
                        });
                },
                error => FinishRefreshIfCurrent(requestVersion, false, FormatError(error)));
        }

        private int BeginRefresh(Action<bool, string> onCompleted, bool targetsOtherUserSpace)
        {
            var requestVersion = ++refreshRequestVersion;
            isRefreshing = true;
            activeRefreshTargetsOtherUserSpace = targetsOtherUserSpace;
            refreshCompletion = onCompleted;
            queuedRefreshCompletions.Clear();
            itemsLoadFailureCount = 0;
            firstItemsLoadFailureMessage = null;
            if (!targetsOtherUserSpace)
            {
                hasAppliedInitialOwnSpaceVisuals = false;
                hasResolvedInitialOwnSpaceLayout = false;
            }
            return requestVersion;
        }

        private void CompleteSupersededRefreshCallbacks(string message)
        {
            var completion = refreshCompletion;
            var queuedCompletions = queuedRefreshCompletions.Count > 0
                ? queuedRefreshCompletions.ToArray()
                : Array.Empty<Action<bool, string>>();

            refreshCompletion = null;
            queuedRefreshCompletions.Clear();

            completion?.Invoke(false, message);
            for (var i = 0; i < queuedCompletions.Length; i++)
            {
                queuedCompletions[i]?.Invoke(false, message);
            }
        }

        private bool IsRefreshRequestCurrent(int requestVersion)
        {
            return requestVersion == refreshRequestVersion;
        }

        private void PrepareOwnSpaceRefreshVisualIsolation(bool forceClearVisitorLayout = false)
        {
            var wasViewingOtherUserSpace = isViewingOtherUserSpace;
            currentSpaceId = null;
            SetViewingOtherUserSpace(false);
            experienceManager?.SetNotebookScreenSuppressed(false);
            SpaceVisitorOverlayController.Instance?.HideOverlay();
            ClearVisibleSpaceAssetManifests();
            InvalidateExternalDecorationLayoutRestores();

            var roomEditModeController = FindRoomEditModeController();
            if (roomEditModeController == null)
            {
                return;
            }

            var shouldClearVisitorLayout = forceClearVisitorLayout ||
                                           wasViewingOtherUserSpace ||
                                           roomEditModeController.IsReadOnlySpacePreview;

            roomEditModeController.SetReadOnlySpacePreview(false);
            if (shouldClearVisitorLayout)
            {
                roomEditModeController.ClearReadOnlyPreviewArtifactsForOwnSpaceRefresh();
            }

            roomEditModeController.RestoreAuthoritativeSpaceVisuals();
        }

        private void CompleteRefreshWithoutRequest(Action<bool, string> onCompleted, bool success, string message)
        {
            hasCompletedInitialRefresh = true;
            lastInitialRefreshSucceeded = success;
            lastInitialRefreshMessage = message;
            onCompleted?.Invoke(success, message);
        }

        private void ResetRefreshState()
        {
            isRefreshing = false;
            activeRefreshTargetsOtherUserSpace = false;
            refreshCompletion = null;
            queuedRefreshCompletions.Clear();
            itemsLoadFailureCount = 0;
            firstItemsLoadFailureMessage = null;
        }

        // External entry point for "visit another user's space" flows
        // (search result click, dashboard, deep link, etc.). Caller must
        // resolve the spaceId — this method does NOT take a hardcoded ID.
        // Pipeline mirrors RefreshFromServer except it hits /v1/spaces/{id}
        // instead of /v1/spaces/me, then routes the response through the
        // same room-visual + bookshelf load + visit record path.
        //
        // Own-space detection: SpaceResponse.OwnerUserId == AuthSession.UserId.
        public void EnterSpaceById(long spaceId, Action<bool, string> onCompleted = null)
        {
            EnterSpaceById(new NotebookSpaceVisitRequest { SpaceId = spaceId }, onCompleted);
        }

        public void EnterSpaceById(NotebookSpaceVisitRequest request, Action<bool, string> onCompleted = null)
        {
            if (!IsConfigured || isRefreshing)
            {
                onCompleted?.Invoke(false, "책장 API 동기화가 준비되지 않았습니다.");
                return;
            }
            var spaceId = request?.SpaceId ?? 0L;
            if (spaceId <= 0)
            {
                Debug.LogWarning($"[BookshelfApiSync] EnterSpaceById ignored — invalid spaceId={spaceId}.");
                onCompleted?.Invoke(false, "공간 정보를 불러올 수 없습니다.");
                return;
            }

            var requestVersion = BeginRefresh(onCompleted, targetsOtherUserSpace: true);
            InvalidateExternalDecorationLayoutRestores();
            client.GetSpace(
                spaceId,
                space =>
                {
                    if (!IsRefreshRequestCurrent(requestVersion))
                    {
                        return;
                    }

                    if (space == null || space.Id <= 0)
                    {
                        FinishRefreshIfCurrent(requestVersion, false, "공간 정보를 불러오지 못했습니다.");
                        return;
                    }

                    var isOtherUserSpace = !IsOwnSpace(space.OwnerUserId);
                    client.GetBookshelves(
                        space.Id,
                        response =>
                        {
                            if (!IsRefreshRequestCurrent(requestVersion))
                            {
                                return;
                            }

                            currentSpaceId = space.Id;
                            SetViewingOtherUserSpace(isOtherUserSpace);
                            experienceManager?.SetNotebookScreenSuppressed(isOtherUserSpace);

                            if (isOtherUserSpace)
                            {
                                SpaceVisitorOverlayController.Instance?.SetCurrentSpace(space, request);
                                PrefetchVisibleSpaceAssets(
                                    space.Id,
                                    true,
                                    requestVersion,
                                    () =>
                                    {
                                        if (!IsRefreshRequestCurrent(requestVersion) ||
                                            !IsCurrentSpaceView(space.Id, true))
                                        {
                                            return;
                                        }

                                        ApplyRoomVisuals(space, true);
                                        LoadSpaceLayout(
                                            space.Id,
                                            true,
                                            requestVersion,
                                            () => LoadBookshelfItems(response, space.Id, true, requestVersion, space.CurrentBookshelfSkinInventoryId));
                                    });
                                RecordVisitOnce(space.Id);
                            }
                            else
                            {
                                SpaceVisitorOverlayController.Instance?.HideOverlay();
                                ClearVisibleSpaceAssetManifests();
                                ApplyRoomVisuals(space, false);
                                LoadSpaceLayout(
                                    space.Id,
                                    false,
                                    requestVersion,
                                    () => LoadBookshelfItems(response, space.Id, false, requestVersion, space.CurrentBookshelfSkinInventoryId));
                            }
                        },
                        error => FinishRefreshIfCurrent(requestVersion, false, FormatError(error)));
                },
                error =>
                {
                    if (!IsRefreshRequestCurrent(requestVersion))
                    {
                        return;
                    }

                    Debug.LogWarning(
                        $"[BookshelfApiSync] GetSpace({spaceId}) failed: code={error?.Code} message={error?.Message}");
                    FinishRefreshIfCurrent(requestVersion, false, "공간 정보를 불러오지 못했습니다.");
                });
        }

        private void SetViewingOtherUserSpace(bool value)
        {
            isViewingOtherUserSpace = value;
        }

        private bool IsCurrentSpaceView(long spaceId, bool expectedOtherUserSpace)
        {
            return currentSpaceId.HasValue &&
                   currentSpaceId.Value == spaceId &&
                   isViewingOtherUserSpace == expectedOtherUserSpace;
        }

        private void PrefetchVisibleSpaceAssets(long spaceId, bool expectedOtherUserSpace, int requestVersion, Action onCompleted = null)
        {
            ClearVisibleSpaceAssetManifests();
            if (client == null || remoteAssetCacheService == null || spaceId <= 0L)
            {
                onCompleted?.Invoke();
                return;
            }

            client.GetSpaceAssetManifests(
                spaceId,
                response =>
                {
                    if (!IsRefreshRequestCurrent(requestVersion) ||
                        !IsCurrentSpaceView(spaceId, expectedOtherUserSpace))
                    {
                        return;
                    }

                    var manifests = response?.Manifests;
                    if (manifests == null || manifests.Count == 0)
                    {
                        ClearVisibleSpaceAssetManifests();
                        onCompleted?.Invoke();
                        return;
                    }

                    var roomEditModeController = FindRoomEditModeController();
                    RegisterVisibleBookshelfSkinManifests(manifests);
                    roomEditModeController?.RegisterVisibleSpaceAssetManifests(manifests);
                    StartCoroutine(PrefetchVisibleSpaceAssetManifests(spaceId, expectedOtherUserSpace, requestVersion, manifests));
                    onCompleted?.Invoke();
                },
                error =>
                {
                    if (!IsRefreshRequestCurrent(requestVersion) ||
                        !IsCurrentSpaceView(spaceId, expectedOtherUserSpace))
                    {
                        return;
                    }

                    Debug.LogWarning(
                        $"[BookshelfApiSync] Space asset manifest prefetch skipped: spaceId={spaceId}, code={error?.Code}, message={error?.Message}");
                    onCompleted?.Invoke();
                });
        }

        private void ClearVisibleSpaceAssetManifests()
        {
            visibleBookshelfSkinAssetIdByInventoryId.Clear();
            visibleFallbackBookshelfSkinAssetId = null;

            var roomEditModeController = FindRoomEditModeController();
            roomEditModeController?.RegisterVisibleSpaceAssetManifests(Array.Empty<DownloadManifestResponseDto>());
        }

        private void RegisterVisibleBookshelfSkinManifests(IReadOnlyList<DownloadManifestResponseDto> manifests)
        {
            visibleBookshelfSkinAssetIdByInventoryId.Clear();
            visibleFallbackBookshelfSkinAssetId = null;

            if (manifests == null)
            {
                return;
            }

            for (var i = 0; i < manifests.Count; i++)
            {
                var manifest = manifests[i];
                if (!IsBookshelfSkinManifest(manifest))
                {
                    continue;
                }

                if (manifest.InventoryId.HasValue && manifest.InventoryId.Value > 0L)
                {
                    visibleBookshelfSkinAssetIdByInventoryId[manifest.InventoryId.Value] = manifest.AssetId;
                }

                visibleFallbackBookshelfSkinAssetId ??= manifest.AssetId;
            }
        }

        private static bool IsBookshelfSkinManifest(DownloadManifestResponseDto manifest)
        {
            if (manifest == null || manifest.AssetId <= 0L)
            {
                return false;
            }

            if (string.Equals(manifest.AssetType, "BOOKSHELF_SKIN", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return BookshelfSkinResolver.TryResolveThemeStyle(manifest.AssetId, out var style) &&
                   style != BookshelfThemeStyle.Default;
        }

        private long? ResolveVisibleBookshelfSkinAssetId(long? spaceBookshelfSkinInventoryId)
        {
            if (spaceBookshelfSkinInventoryId.HasValue &&
                spaceBookshelfSkinInventoryId.Value > 0L &&
                visibleBookshelfSkinAssetIdByInventoryId.TryGetValue(spaceBookshelfSkinInventoryId.Value, out var assetId) &&
                assetId > 0L)
            {
                return assetId;
            }

            return visibleFallbackBookshelfSkinAssetId;
        }

        private static void InvalidateExternalDecorationLayoutRestores()
        {
            var roomEditModeController = FindRoomEditModeController();
            roomEditModeController?.InvalidateExternalDecorationLayoutRestores();
        }

        private static RoomEditModeController FindRoomEditModeController()
        {
            return FindFirstObjectByType<RoomEditModeController>(FindObjectsInactive.Include);
        }

        private void LoadSpaceLayout(
            long spaceId,
            bool expectedOtherUserSpace,
            int requestVersion,
            Action onCompleted = null)
        {
            if (client == null || spaceId <= 0L)
            {
                if (!expectedOtherUserSpace)
                {
                    FinishRefreshIfCurrent(requestVersion, false, "Space layout request is unavailable.");
                    return;
                }

                onCompleted?.Invoke();
                return;
            }

            if (!IsRefreshRequestCurrent(requestVersion) ||
                !IsCurrentSpaceView(spaceId, expectedOtherUserSpace))
            {
                return;
            }

            client.GetSpaceLayout(
                spaceId,
                response =>
                {
                    if (!IsRefreshRequestCurrent(requestVersion) ||
                        !IsCurrentSpaceView(spaceId, expectedOtherUserSpace))
                    {
                        return;
                    }

                    var roomEditModeController = FindFirstObjectByType<RoomEditModeController>(FindObjectsInactive.Include);
                    roomEditModeController?.ApplyExternalDecorationLayoutPayload(response?.LayoutPayload, clearExisting: true);
                    if (!expectedOtherUserSpace)
                    {
                        hasResolvedInitialOwnSpaceLayout = true;
                    }
                    onCompleted?.Invoke();
                },
                error =>
                {
                    if (!IsRefreshRequestCurrent(requestVersion) ||
                        !IsCurrentSpaceView(spaceId, expectedOtherUserSpace))
                    {
                        return;
                    }

                    Debug.LogWarning(
                        $"[BookshelfApiSync] Space layout load skipped: spaceId={spaceId}, code={error?.Code}, message={error?.Message}");
                    var roomEditModeController = FindFirstObjectByType<RoomEditModeController>(FindObjectsInactive.Include);
                    roomEditModeController?.ApplyExternalDecorationLayoutPayload(null, clearExisting: true);
                    if (!expectedOtherUserSpace)
                    {
                        hasResolvedInitialOwnSpaceLayout = true;
                        onCompleted?.Invoke();
                        return;
                    }

                    onCompleted?.Invoke();
                });
        }

        private IEnumerator PrefetchVisibleSpaceAssetManifests(long spaceId, bool expectedOtherUserSpace, int requestVersion, IReadOnlyList<DownloadManifestResponseDto> manifests)
        {
            if (manifests == null || manifests.Count == 0)
            {
                yield break;
            }

            var cachedCount = 0;
            for (var i = 0; i < manifests.Count; i++)
            {
                if (!IsRefreshRequestCurrent(requestVersion) ||
                    !IsCurrentSpaceView(spaceId, expectedOtherUserSpace))
                {
                    yield break;
                }

                var manifest = manifests[i];
                if (manifest == null)
                {
                    continue;
                }

                yield return remoteAssetCacheService.EnsureManifestCached(
                    manifest,
                    paths =>
                    {
                        cachedCount += paths?.Count ?? 0;
                    },
                    error =>
                    {
                        Debug.LogWarning(
                            $"[BookshelfApiSync] Remote asset cache prefetch failed: spaceId={spaceId}, assetId={manifest.AssetId}, error={error}");
                    });
            }

            if (cachedCount > 0)
            {
                Debug.Log($"[BookshelfApiSync] Remote asset cache prefetch completed: spaceId={spaceId}, files={cachedCount}");
            }
        }

        private static void ApplyRoomVisuals(SpaceResponseDto space, bool isReadOnlyPreview)
        {
            if (space == null)
            {
                return;
            }

            var roomEditModeController = FindFirstObjectByType<RoomEditModeController>(FindObjectsInactive.Include);
            if (roomEditModeController == null)
            {
                return;
            }

            roomEditModeController.SetReadOnlySpacePreview(isReadOnlyPreview);
            if (isReadOnlyPreview)
            {
                roomEditModeController.ApplyServerSpacePreviewVisuals(space);
            }
            else
            {
                roomEditModeController.ApplyServerSpaceVisuals(space);
            }
        }

        private bool IsOwnSpace(long ownerUserId)
        {
            var myUserId = AuthSession.UserId;
            // Anonymous (pre-login) caller has myUserId == 0 from PlayerPrefs
            // default. They cannot own any BE space record, so the not-own
            // check passes — guarded callers should already short-circuit on
            // !HasAccessToken before reaching here (see RecordVisitOnce).
            return myUserId != 0 && ownerUserId == myUserId;
        }

        private void RecordVisitOnce(long spaceId)
        {
            // Visit recording requires a Bearer token: BE recordVisit calls
            // userService.requireUserId(), which 401s on anonymous calls.
            // Skipping here also keeps visitedSpaceIds untouched so a later
            // login in the same play session can still record the visit on
            // re-entry.
            if (APIManager.Instance == null || !APIManager.Instance.HasAccessToken)
            {
                return;
            }

            // Mark BEFORE the call (record-on-attempt) so a slow BE round-trip
            // cannot cause the user to navigate away and re-enter, racing two
            // visit POSTs. Worst case: BE call fails and we never retry — that
            // is preferable to inflating visit counts on a flaky network.
            if (!visitedSpaceIds.Add(spaceId))
            {
                return;
            }

            client.RecordSpaceVisit(
                spaceId,
                _ => { /* fire-and-forget on success */ },
                error =>
                {
                    Debug.LogWarning(
                        $"[BookshelfApiSync] RecordSpaceVisit({spaceId}) failed: code={error?.Code} message={error?.Message}");
                });
        }

        public bool CreateBookshelf(string name, Action onSucceeded = null, Action<string> onFailed = null)
        {
            if (!CanUseServerApi)
            {
                return false;
            }

            if (!currentSpaceId.HasValue)
            {
                client.GetMySpace(
                    space =>
                    {
                        currentSpaceId = space?.Id;
                        if (!currentSpaceId.HasValue || currentSpaceId.Value <= 0)
                        {
                            onFailed?.Invoke("책장을 추가할 공간 정보를 찾을 수 없습니다.");
                            return;
                        }

                        CreateBookshelf(name, onSucceeded, onFailed);
                    },
                    error =>
                    {
                        var message = FormatUserFacingError(error);
                        Debug.LogWarning($"Bookshelf create skipped: {FormatError(error)}");
                        onFailed?.Invoke(message);
                    });
                return true;
            }

            client.CreateBookshelf(
                new CreateBookshelfRequestDto
                {
                    SpaceId = currentSpaceId.Value,
                    Name = string.IsNullOrWhiteSpace(name) ? "새 책장" : name,
                    VisibilityScope = "PUBLIC"
                },
                _ =>
                {
                    onSucceeded?.Invoke();
                    RefreshFromServer();
                },
                error =>
                {
                    var message = FormatUserFacingError(error);
                    Debug.LogWarning($"Bookshelf create failed: {FormatError(error)}");
                    onFailed?.Invoke(message);
                });
            return true;
        }

        public bool UpdateBookshelf(int index, Action onSucceeded = null, Action<string> onFailed = null)
        {
            if (!CanUseServerApi || !TryParseId(generator.GetBookshelfServerId(index), out var bookshelfId))
            {
                return false;
            }

            var style = generator.GetBookshelfDesignStyle(index);
            var clearSkinInventory = style == BookshelfThemeStyle.Default;
            client.UpdateBookshelf(
                bookshelfId,
                new UpdateBookshelfRequestDto
                {
                    Name = generator.GetBookshelfName(index),
                    VisibilityScope = ToApiVisibility(generator.GetBookshelfVisibility(index)),
                    SkinInventoryId = clearSkinInventory ? null : BookshelfSkinResolver.ResolveInventoryId(style),
                    ClearSkinInventory = clearSkinInventory
                },
                _ => onSucceeded?.Invoke(),
                error =>
                {
                    var message = FormatUserFacingError(error);
                    Debug.LogWarning($"Bookshelf update failed: {FormatError(error)}");
                    onFailed?.Invoke(message);
                });
            return true;
        }

        public bool DeleteBookshelf(string bookshelfServerId, Action onSucceeded = null, Action<string> onFailed = null)
        {
            if (!CanUseServerApi || !TryParseId(bookshelfServerId, out var bookshelfId))
            {
                return false;
            }

            client.DeleteBookshelf(
                bookshelfId,
                _ =>
                {
                    onSucceeded?.Invoke();
                    RefreshFromServer();
                },
                error =>
                {
                    var message = FormatUserFacingError(error);
                    Debug.LogWarning($"Bookshelf delete failed: {FormatError(error)}");
                    onFailed?.Invoke(message);
                });
            return true;
        }

        public bool ReorderBookshelves(Action onSucceeded = null, Action<string> onFailed = null)
        {
            if (!CanUseServerApi)
            {
                return false;
            }

            var rawIds = generator.GetBookshelfServerIdsInDisplayOrder();
            var ids = new List<long>();
            for (var i = 0; i < rawIds.Count; i++)
            {
                if (TryParseId(rawIds[i], out var id))
                {
                    ids.Add(id);
                }
            }

            if (ids.Count <= 1 || ids.Count != rawIds.Count)
            {
                return false;
            }

            client.ReorderBookshelves(
                new ReorderBookshelvesRequestDto { BookshelfIds = ids },
                _ => onSucceeded?.Invoke(),
                error =>
                {
                    var message = FormatUserFacingError(error);
                    Debug.LogWarning($"Bookshelf reorder failed: {FormatError(error)}");
                    onFailed?.Invoke(message);
                });
            return true;
        }

        public bool SyncBookState(
            BookEntity book,
            Action<BookEntity> onSucceeded = null,
            Action<string> onFailed = null)
        {
            if (!CanUseServerApi || book == null || !TryParseId(book.userBookId, out var userBookId))
            {
                return false;
            }

            client.UpdateUserBook(
                userBookId,
                new UpdateUserBookRequestDto
                {
                    ReadingStatus = ToApiReadingStatus(book.readingStatus),
                    OwnerRating = book.ownerRating > 0f ? (decimal?)Math.Round((decimal)book.ownerRating, 1) : null
                },
                response =>
                {
                    if (!IsBookEntityAlive(book))
                    {
                        return;
                    }

                    ApplyUserBookResponse(book, response);
                    onSucceeded?.Invoke(book);
                },
                error =>
                {
                    if (!IsBookEntityAlive(book))
                    {
                        return;
                    }

                    var message = FormatUserFacingError(error);
                    Debug.LogWarning($"User book update failed: {FormatError(error)}");
                    onFailed?.Invoke(message);
                });
            return true;
        }

        public bool SyncBookLike(
            BookEntity book,
            Action<BookEntity> onSucceeded = null,
            Action<string> onFailed = null)
        {
            if (!CanUseServerApi || book == null || !TryParseId(book.userBookId, out var userBookId))
            {
                return false;
            }

            var desiredLiked = book.isLiked;
            Action<ToggleResponseDto> success = response =>
            {
                if (!IsBookEntityAlive(book))
                {
                    return;
                }

                book.isLiked = response != null ? response.Active : desiredLiked;
                book.totalLikeCount = response != null
                    ? ClampCount(response.Count)
                    : ResolveVisibleLikeCount(book.totalLikeCount, book.isLiked);
                book.hasAggregateBookStats = true;

                onSucceeded?.Invoke(book);
            };
            Action<ApiErrorResponseDto> failure = error =>
            {
                if (!IsBookEntityAlive(book))
                {
                    return;
                }

                var message = FormatUserFacingError(error);
                Debug.LogWarning($"User book like update failed: {FormatError(error)}");
                onFailed?.Invoke(message);
            };

            if (book.isLiked)
            {
                client.LikeUserBook(userBookId, success, failure);
                return true;
            }

            client.UnlikeUserBook(userBookId, success, failure);
            return true;
        }

        public bool SyncBookPlacement(
            BookEntity book,
            Action<BookEntity> onSucceeded = null,
            Action<string> onFailed = null)
        {
            if (!CanUseServerApi || book == null)
            {
                return false;
            }

            var targetBookshelfIndex = ResolveBookPlacementBookshelfIndex();
            var bookshelfIdText = ResolveBookPlacementBookshelfId(book, targetBookshelfIndex);
            var sectionIdText = ResolveBookPlacementSectionId(book, targetBookshelfIndex);

            if (!TryParseId(bookshelfIdText, out var bookshelfId) ||
                !TryParseId(sectionIdText, out var sectionId))
            {
                return false;
            }

            var hasBookshelfItemId = TryParseId(book.bookshelfItemId, out var itemId);
            var hasUserBookId = TryParseId(book.userBookId, out var userBookId);
            if (!hasBookshelfItemId && !hasUserBookId)
            {
                return false;
            }

            var previousBookshelfId = book.bookshelfId;
            var previousBookshelfSectionId = book.bookshelfSectionId;
            book.bookshelfId = bookshelfId.ToString();
            book.bookshelfSectionId = sectionId.ToString();

            var request = new BookshelfItemRequestDto
            {
                BookshelfId = bookshelfId,
                SectionId = sectionId,
                SlotNo = generator.GetBookSlotNo(book),
                FacingDirection = "FRONT",
                DisplayStyle = ToApiDisplayStyle(book.orientation)
            };

            if (hasBookshelfItemId)
            {
                client.MoveBookshelfItem(
                    itemId,
                    request,
                    response =>
                    {
                        if (!IsBookEntityAlive(book))
                        {
                            return;
                        }

                        ApplyBookshelfItemResponse(book, response);
                        onSucceeded?.Invoke(book);
                    },
                    error =>
                    {
                        if (!IsBookEntityAlive(book))
                        {
                            return;
                        }

                        RestoreBookPlacementIds(book, previousBookshelfId, previousBookshelfSectionId);
                        var message = FormatUserFacingError(error);
                        Debug.LogWarning($"Bookshelf item move failed: {FormatError(error)}");
                        onFailed?.Invoke(message);
                    });
                return true;
            }

            if (hasUserBookId)
            {
                client.MoveUserBook(
                    userBookId,
                    new MoveUserBookRequestDto
                    {
                        BookshelfId = bookshelfId,
                        SectionId = sectionId,
                        SlotNo = request.SlotNo,
                        FacingDirection = request.FacingDirection,
                        DisplayStyle = request.DisplayStyle
                    },
                    response =>
                    {
                        if (!IsBookEntityAlive(book))
                        {
                            return;
                        }

                        ApplyUserBookResponse(book, response);
                        onSucceeded?.Invoke(book);
                    },
                    error =>
                    {
                        if (!IsBookEntityAlive(book))
                        {
                            return;
                        }

                        RestoreBookPlacementIds(book, previousBookshelfId, previousBookshelfSectionId);
                        var message = FormatUserFacingError(error);
                        Debug.LogWarning($"User book move failed: {FormatError(error)}");
                        onFailed?.Invoke(message);
                    });
                return true;
            }

            return false;
        }

        private int ResolveBookPlacementBookshelfIndex()
        {
            if (generator == null || generator.BookshelfCount <= 0)
            {
                return -1;
            }

            return generator.DisplayBookshelfIndex >= 0
                ? generator.DisplayBookshelfIndex
                : generator.ActiveBookshelfIndex;
        }

        private string ResolveBookPlacementBookshelfId(BookEntity book, int bookshelfIndex)
        {
            if (generator != null && bookshelfIndex >= 0)
            {
                var currentBookshelfId = generator.GetBookshelfServerId(bookshelfIndex);
                if (!string.IsNullOrWhiteSpace(currentBookshelfId))
                {
                    return currentBookshelfId;
                }
            }

            if (!string.IsNullOrWhiteSpace(book != null ? book.bookshelfId : null))
            {
                return book.bookshelfId;
            }

            return generator != null ? generator.GetActiveBookshelfServerId() : null;
        }

        private string ResolveBookPlacementSectionId(BookEntity book, int bookshelfIndex)
        {
            var section = book != null ? book.CurrentSection : null;
            if (section == null)
            {
                return book != null ? book.bookshelfSectionId : null;
            }

            if (generator == null || bookshelfIndex < 0)
            {
                return null;
            }

            return generator.GetSectionServerId(bookshelfIndex, section.row, section.column);
        }

        public void SyncSectionAlignment(ShelfSection section)
        {
            if (!CanUseServerApi || section == null)
            {
                return;
            }

            var bookshelfIdText = generator.GetActiveBookshelfServerId();
            var sectionIdText = generator.GetSectionServerId(generator.ActiveBookshelfIndex, section.row, section.column);
            if (!TryParseId(bookshelfIdText, out var bookshelfId) ||
                !TryParseId(sectionIdText, out var sectionId))
            {
                return;
            }

            client.UpdateSectionAlignment(
                bookshelfId,
                sectionId,
                new SectionAlignmentRequestDto { Alignment = ToApiAlignment(section.alignment) },
                _ => { },
                error => Debug.LogWarning($"Bookshelf section alignment update failed: {FormatError(error)}"));
        }

        public void FetchBookDetails(BookEntity book, Action<BookEntity> onUpdated = null)
        {
            if (!CanUseServerApi || book == null || !TryParseId(book.bookId, out var bookId))
            {
                return;
            }

            client.GetBook(
                bookId,
                response =>
                {
                    if (!IsBookEntityAlive(book))
                    {
                        return;
                    }

                    ApplyBookResponse(book, response);
                    onUpdated?.Invoke(book);
                },
                error =>
                {
                    if (!IsBookEntityAlive(book))
                    {
                        return;
                    }

                    Debug.LogWarning($"Book detail load failed: {FormatError(error)}");
                });
        }

        public bool FetchReadingRecord(BookEntity book, Action<BookEntity, bool> onCompleted = null)
        {
            if (!CanUseServerApi || book == null)
            {
                return false;
            }

            if (TryParseId(book.readingRecordId, out var recordId))
            {
                client.GetReadingRecord(
                    recordId,
                    response =>
                    {
                        if (!IsBookEntityAlive(book))
                        {
                            return;
                        }

                        ApplyReadingRecordResponse(book, response);
                        onCompleted?.Invoke(book, response != null);
                    },
                    error =>
                    {
                        if (!IsBookEntityAlive(book))
                        {
                            return;
                        }

                        Debug.LogWarning($"Reading record load failed: {FormatError(error)}");
                        onCompleted?.Invoke(book, false);
                    });
                return true;
            }

            if (TryParseId(book.userBookId, out var userBookId))
            {
                client.GetUserBookReadingRecord(
                    userBookId,
                    response =>
                    {
                        if (!IsBookEntityAlive(book))
                        {
                            return;
                        }

                        ApplyReadingRecordResponse(book, response);
                        onCompleted?.Invoke(book, response != null);
                    },
                    error =>
                    {
                        if (!IsBookEntityAlive(book))
                        {
                            return;
                        }

                        onCompleted?.Invoke(book, false);
                    });
                return true;
            }

            return false;
        }

        public bool DeleteReadingRecord(BookEntity book, Action onSucceeded = null, Action<string> onFailed = null)
        {
            if (!CanUseServerApi || book == null || !TryParseId(book.readingRecordId, out var recordId))
            {
                return false;
            }

            client.DeleteReadingRecord(
                recordId,
                _ =>
                {
                    book.readingRecordId = null;
                    book.reportTitle = string.Empty;
                    book.reportBody = string.Empty;
                    book.reportUpdatedLabel = string.Empty;
                    book.memoPreview = book.description;
                    onSucceeded?.Invoke();
                },
                error =>
                {
                    var message = FormatUserFacingError(error);
                    Debug.LogWarning($"Reading record delete failed: {FormatError(error)}");
                    onFailed?.Invoke(message);
                });
            return true;
        }

        public bool ToggleReadingRecordLike(
            BookEntity book,
            bool liked,
            Action<BookEntity> onUpdated = null,
            Action<string> onFailed = null)
        {
            if (!CanUseServerApi || book == null || !TryParseId(book.readingRecordId, out var recordId))
            {
                return false;
            }

            Action<ToggleResponseDto> success = response =>
            {
                if (response != null)
                {
                    book.reportLiked = response.Active;
                    book.reportLikeCount = Mathf.Max(0, (int)response.Count);
                }
                else
                {
                    book.reportLiked = liked;
                }

                onUpdated?.Invoke(book);
            };
            Action<ApiErrorResponseDto> failure = error =>
            {
                var message = FormatUserFacingError(error);
                Debug.LogWarning($"Reading record like update failed: {FormatError(error)}");
                onFailed?.Invoke(message);
            };
            if (liked)
            {
                client.LikeReadingRecord(recordId, success, failure);
            }
            else
            {
                client.UnlikeReadingRecord(recordId, success, failure);
            }

            return true;
        }

        public bool FetchReadingRecordComments(
            BookEntity book,
            Action<IReadOnlyList<CommentResponseDto>> onLoaded = null,
            Action<string> onFailed = null)
        {
            if (!CanUseServerApi || book == null || !TryParseId(book.readingRecordId, out var recordId))
            {
                return false;
            }

            client.GetReadingRecordComments(
                recordId,
                response =>
                {
                    var comments = response?.Comments ?? new List<CommentResponseDto>();
                    book.reportCommentCount = comments.Count;
                    onLoaded?.Invoke(comments);
                },
                error =>
                {
                    var message = FormatUserFacingError(error);
                    Debug.LogWarning($"Reading record comments load failed: {FormatError(error)}");
                    onFailed?.Invoke(message);
                });
            return true;
        }

        public bool CreateReadingRecordComment(
            BookEntity book,
            string content,
            Action<CommentResponseDto> onCreated = null,
            Action<string> onFailed = null)
        {
            if (!CanUseServerApi ||
                book == null ||
                string.IsNullOrWhiteSpace(content) ||
                !TryParseId(book.readingRecordId, out var recordId))
            {
                return false;
            }

            client.CreateReadingRecordComment(
                recordId,
                new CommentRequestDto { Content = content.Trim() },
                response =>
                {
                    book.reportCommentCount = Mathf.Max(0, book.reportCommentCount + 1);
                    onCreated?.Invoke(response);
                },
                error =>
                {
                    var message = FormatUserFacingError(error);
                    Debug.LogWarning($"Reading record comment create failed: {FormatError(error)}");
                    onFailed?.Invoke(message);
                });
            return true;
        }

        public void DeleteReadingRecordComment(long commentId, Action onDeleted = null)
        {
            if (!CanUseServerApi || commentId <= 0L)
            {
                return;
            }

            client.DeleteComment(
                commentId,
                _ => onDeleted?.Invoke(),
                error => Debug.LogWarning($"Reading record comment delete failed: {FormatError(error)}"));
        }

        public void ToggleReadingRecordCommentLike(long commentId, bool liked, Action<ToggleResponseDto> onUpdated = null)
        {
            if (!CanUseServerApi || commentId <= 0L)
            {
                return;
            }

            Action<ToggleResponseDto> success = response => onUpdated?.Invoke(response);
            Action<ApiErrorResponseDto> failure = error => Debug.LogWarning($"Reading record comment like update failed: {FormatError(error)}");
            if (liked)
            {
                client.LikeComment(commentId, success, failure);
            }
            else
            {
                client.UnlikeComment(commentId, success, failure);
            }
        }

        public bool AddBookToActiveShelf(
            BookEntity book,
            Action<BookEntity> onSucceeded = null,
            Action<string> onFailed = null)
        {
            return AddBookToShelf(book, generator != null ? generator.ActiveBookshelfIndex : -1, onSucceeded, onFailed);
        }

        public bool AddBookToShelf(
            BookEntity book,
            int bookshelfIndex,
            Action<BookEntity> onSucceeded = null,
            Action<string> onFailed = null,
            Action<BookEntity, string> onPostAddStateFailed = null)
        {
            if (!CanUseServerApi || book == null)
            {
                return false;
            }

            var targetBookshelfIndex = bookshelfIndex >= 0 ? bookshelfIndex : generator.ActiveBookshelfIndex;
            var section = book.CurrentSection;
            var sectionIdText = section != null
                ? generator.GetSectionServerId(targetBookshelfIndex, section.row, section.column)
                : book.bookshelfSectionId;
            var bookshelfIdText = string.IsNullOrWhiteSpace(book.bookshelfId)
                ? generator.GetBookshelfServerId(targetBookshelfIndex)
                : book.bookshelfId;
            if (!TryParseId(bookshelfIdText, out var bookshelfId) ||
                !TryParseId(sectionIdText, out var sectionId))
            {
                return false;
            }

            var previousBookshelfId = book.bookshelfId;
            var previousBookshelfSectionId = book.bookshelfSectionId;
            book.bookshelfId = bookshelfId.ToString();
            book.bookshelfSectionId = sectionId.ToString();
            var slotNo = generator.GetBookSlotNo(book);
            var displayStyle = ToApiDisplayStyle(book.orientation);
            var facingDirection = "FRONT";
            var desiredReadingStatus = book.readingStatus;
            var desiredOwnerRating = book.ownerRating;
            if (TryParseId(book.bookId, out var bookId))
            {
                client.AddBookToShelf(
                    bookshelfId,
                    new AddBookToShelfRequestDto
                    {
                        BookId = bookId,
                        SectionId = sectionId,
                        SlotNo = slotNo,
                        FacingDirection = facingDirection,
                        DisplayStyle = displayStyle
                    },
                    response =>
                    {
                        if (!IsBookEntityAlive(book))
                        {
                            return;
                        }

                        ApplyUserBookResponse(book, response);
                        CompleteBookAddStateSync(book, desiredReadingStatus, desiredOwnerRating, onSucceeded, onFailed, onPostAddStateFailed);
                    },
                    error =>
                    {
                        if (!IsBookEntityAlive(book))
                        {
                            return;
                        }

                        RestoreBookAddPlacementIds(book, previousBookshelfId, previousBookshelfSectionId);
                        var message = FormatUserFacingError(error);
                        Debug.LogWarning($"Book add failed: {FormatError(error)}");
                        onFailed?.Invoke(message);
                    });
                return true;
            }

            client.AddKakaoBookToShelf(
                bookshelfId,
                new AddKakaoBookToShelfRequestDto
                {
                    Title = book.title,
                    Author = book.author,
                    Publisher = book.publisher,
                    Isbn13 = book.isbn13,
                    Isbn10 = book.isbn10,
                    PublishedDate = book.publishedDate,
                    Description = book.description,
                    CoverImageUrl = book.coverImageUrl,
                    SourceUrl = book.sourceUrl,
                    SectionId = sectionId,
                    SlotNo = slotNo,
                    FacingDirection = facingDirection,
                    DisplayStyle = displayStyle
                },
                response =>
                {
                    if (!IsBookEntityAlive(book))
                    {
                        return;
                    }

                    book.bookId = response?.BookId > 0 ? response.BookId.ToString() : book.bookId;
                    ApplyUserBookResponse(book, response);
                    CompleteBookAddStateSync(book, desiredReadingStatus, desiredOwnerRating, onSucceeded, onFailed, onPostAddStateFailed);
                },
                error =>
                {
                    if (!IsBookEntityAlive(book))
                    {
                        return;
                    }

                    RestoreBookAddPlacementIds(book, previousBookshelfId, previousBookshelfSectionId);
                    var message = FormatUserFacingError(error);
                    Debug.LogWarning($"Kakao/manual book add failed: {FormatError(error)}");
                    onFailed?.Invoke(message);
                });
            return true;
        }

        private void CompleteBookAddStateSync(
            BookEntity book,
            ReadingStatus desiredReadingStatus,
            float desiredOwnerRating,
            Action<BookEntity> onSucceeded,
            Action<string> onFailed,
            Action<BookEntity, string> onPostAddStateFailed)
        {
            if (book == null)
            {
                onFailed?.Invoke("추가한 책 정보를 찾을 수 없습니다.");
                return;
            }

            var responseReadingStatus = book.readingStatus;
            var responseOwnerRating = book.ownerRating;
            book.readingStatus = desiredReadingStatus;
            book.isRead = desiredReadingStatus == ReadingStatus.Read;
            book.ownerRating = desiredOwnerRating;

            var needsStateSync = responseReadingStatus != desiredReadingStatus ||
                                 !Mathf.Approximately(responseOwnerRating, desiredOwnerRating);
            if (!needsStateSync)
            {
                onSucceeded?.Invoke(book);
                return;
            }

            if (!SyncBookState(
                    book,
                    onSucceeded,
                    error => HandlePostAddStateSyncFailure(book, error, onFailed, onPostAddStateFailed)))
            {
                HandlePostAddStateSyncFailure(
                    book,
                    "추가한 책의 독서 상태를 저장하는 데 필요한 서버 식별자를 찾을 수 없습니다.",
                    onFailed,
                    onPostAddStateFailed);
            }
        }

        private static void HandlePostAddStateSyncFailure(
            BookEntity book,
            string errorMessage,
            Action<string> onFailed,
            Action<BookEntity, string> onPostAddStateFailed)
        {
            if (onPostAddStateFailed != null)
            {
                onPostAddStateFailed.Invoke(book, errorMessage);
            }
            else
            {
                onFailed?.Invoke(errorMessage);
            }
        }

        private static void RestoreBookAddPlacementIds(BookEntity book, string bookshelfId, string bookshelfSectionId)
        {
            RestoreBookPlacementIds(book, bookshelfId, bookshelfSectionId);
        }

        private static void RestoreBookPlacementIds(BookEntity book, string bookshelfId, string bookshelfSectionId)
        {
            if (book == null)
            {
                return;
            }

            book.bookshelfId = bookshelfId;
            book.bookshelfSectionId = bookshelfSectionId;
        }

        private static bool IsBookEntityAlive(BookEntity book)
        {
            return book != null && book.gameObject != null;
        }

        public bool RemoveBook(BookEntity book, Action onSucceeded = null, Action<string> onFailed = null)
        {
            if (!CanUseServerApi || book == null)
            {
                return false;
            }

            if (TryParseId(book.bookshelfItemId, out var itemId))
            {
                client.RemoveBookshelfItem(
                    itemId,
                    _ => onSucceeded?.Invoke(),
                    error =>
                    {
                        var message = FormatUserFacingError(error);
                        Debug.LogWarning($"Bookshelf item remove failed: {FormatError(error)}");
                        onFailed?.Invoke(message);
                    });
                return true;
            }

            if (TryParseId(book.userBookId, out var userBookId))
            {
                client.DeleteUserBook(
                    userBookId,
                    _ => onSucceeded?.Invoke(),
                    error =>
                    {
                        var message = FormatUserFacingError(error);
                        Debug.LogWarning($"User book delete failed: {FormatError(error)}");
                        onFailed?.Invoke(message);
                    });
                return true;
            }

            return false;
        }

        public void ArchiveBook(BookEntity book)
        {
            if (!CanUseServerApi || book == null || !TryParseId(book.userBookId, out var userBookId))
            {
                return;
            }

            client.ArchiveUserBook(
                userBookId,
                response => ApplyUserBookResponse(book, response),
                error => Debug.LogWarning($"User book archive failed: {FormatError(error)}"));
        }

        public void FetchArchivedUserBooks(Action<IReadOnlyList<UserBookResponseDto>> onLoaded = null)
        {
            if (!CanUseServerApi)
            {
                return;
            }

            client.GetArchivedUserBooks(
                response =>
                {
                    IReadOnlyList<UserBookResponseDto> books = Array.Empty<UserBookResponseDto>();
                    if (response?.Books != null)
                    {
                        books = response.Books;
                    }

                    onLoaded?.Invoke(books);
                },
                error => Debug.LogWarning($"Archived user books load failed: {FormatError(error)}"));
        }

        public void FetchUserBooks(
            string storageStatus = null,
            string readingStatus = null,
            long? bookshelfId = null,
            long? sectionId = null,
            string cursor = null,
            int limit = 50,
            Action<UserBookPlacementListResponseDto> onLoaded = null)
        {
            if (!CanUseServerApi)
            {
                return;
            }

            client.GetUserBooks(
                storageStatus,
                readingStatus,
                bookshelfId,
                sectionId,
                cursor,
                limit,
                onLoaded,
                error => Debug.LogWarning($"User books load failed: {FormatError(error)}"));
        }

        public void FetchAssetInventory(Action<InventoryResponseDto> onLoaded = null)
        {
            if (!CanUseServerApi)
            {
                return;
            }

            client.GetAssetInventory(
                inventory =>
                {
                    cachedAssetInventory = inventory;
                    onLoaded?.Invoke(inventory);
                },
                error => Debug.LogWarning($"Asset inventory load failed: {FormatError(error)}"));
        }

        public bool TryGetCachedAssetInventory(out InventoryResponseDto inventory)
        {
            inventory = cachedAssetInventory;
            return inventory != null;
        }

        public void RefreshBookshelfSkinCatalog(Action onCompleted = null)
        {
            if (!CanUseServerApi)
            {
                onCompleted?.Invoke();
                return;
            }

            if (isBookshelfSkinCatalogLoading)
            {
                StartCoroutine(CompleteAfterBookshelfSkinCatalogLoad(onCompleted));
                return;
            }

            isBookshelfSkinCatalogLoading = true;
            client.GetAssets(
                "BOOKSHELF",
                "popular",
                50,
                response =>
                {
                    var assetsResponse = response;
                    client.GetAssetInventory(
                        inventory =>
                        {
                            isBookshelfSkinCatalogLoading = false;
                            cachedAssetInventory = inventory;
                            BookshelfSkinResolver.RegisterApiAssets(assetsResponse, inventory);
                            FindRoomEditModeController()?.RegisterOwnedAssetInventory(inventory);
                            onCompleted?.Invoke();
                        },
                        error =>
                        {
                            isBookshelfSkinCatalogLoading = false;
                            BookshelfSkinResolver.RegisterApiAssets(assetsResponse, null);
                            Debug.LogWarning($"Bookshelf skin inventory load failed: {FormatError(error)}");
                            onCompleted?.Invoke();
                        });
                },
                error =>
                {
                    isBookshelfSkinCatalogLoading = false;
                    Debug.LogWarning($"Bookshelf skin catalog load failed: {FormatError(error)}");
                    onCompleted?.Invoke();
                });
        }

        private IEnumerator CompleteAfterBookshelfSkinCatalogLoad(Action onCompleted)
        {
            while (isBookshelfSkinCatalogLoading)
            {
                yield return null;
            }

            onCompleted?.Invoke();
        }

        public void AddInteriorObjectToActiveShelf(long assetInventoryId, ShelfSection section, int slotNo = 1)
        {
            if (section == null)
            {
                return;
            }

            AddInteriorObjectToActiveShelf(assetInventoryId, section.row, section.column, slotNo);
        }

        public void AddInteriorObjectToActiveShelf(long assetInventoryId, int row, int column, int slotNo = 1)
        {
            if (!CanUseServerApi || assetInventoryId <= 0L)
            {
                return;
            }

            var sectionIdText = generator.GetSectionServerId(generator.ActiveBookshelfIndex, row, column);
            if (!TryParseId(generator.GetActiveBookshelfServerId(), out var bookshelfId) ||
                !TryParseId(sectionIdText, out var sectionId))
            {
                Debug.LogWarning("Interior object add skipped: section id is required.");
                return;
            }

            client.AddInteriorObject(
                bookshelfId,
                new InteriorObjectRequestDto
                {
                    AssetInventoryId = assetInventoryId,
                    SectionId = sectionId,
                    SlotNo = Mathf.Max(1, slotNo),
                    FacingDirection = "FRONT",
                    DisplayStyle = "SPINE"
                },
                _ => RefreshFromServer(),
                error => Debug.LogWarning($"Interior object add failed: {FormatError(error)}"));
        }

        public bool UploadCurrentRoomThumbnail(
            Action<NotebookSpaceThumbnailUploadResult> onSucceeded = null,
            Action<string> onFailed = null)
        {
            if (!CanUseServerApi || notebookRuntimeUseCase == null)
            {
                return false;
            }

            if (isViewingOtherUserSpace)
            {
                onFailed?.Invoke("Room thumbnail upload is disabled while visiting another space.");
                return false;
            }

            if (isThumbnailUploading)
            {
                thumbnailUploadPending = true;
                pendingThumbnailUploadSucceeded = onSucceeded;
                pendingThumbnailUploadFailed = onFailed;
                Debug.Log("[BookshelfApiSync] Room thumbnail upload is in progress; queued latest capture.");
                return true;
            }

            StartCoroutine(CaptureAndUploadRoomThumbnail(onSucceeded, onFailed));
            return true;
        }

        private IEnumerator CaptureAndUploadRoomThumbnail(
            Action<NotebookSpaceThumbnailUploadResult> onSucceeded,
            Action<string> onFailed)
        {
            isThumbnailUploading = true;
            var nextOnSucceeded = onSucceeded;
            var nextOnFailed = onFailed;

            while (true)
            {
                thumbnailUploadPending = false;
                pendingThumbnailUploadSucceeded = null;
                pendingThumbnailUploadFailed = null;

                if (isViewingOtherUserSpace)
                {
                    nextOnFailed?.Invoke("Room thumbnail upload cancelled while visiting another space.");
                    break;
                }

                var captureReady = false;
                yield return WaitForRoomThumbnailCaptureReady(ready => captureReady = ready);

                if (!captureReady)
                {
                    nextOnFailed?.Invoke("Room thumbnail capture skipped because bookshelf refresh did not finish.");
                }
                else
                {
                    CapturedRoomThumbnail thumbnail = default;
                    var captureFailed = false;
                    try
                    {
                        thumbnail = CaptureMainViewThumbnail();
                    }
                    catch (Exception ex)
                    {
                        captureFailed = true;
                        Debug.LogWarning($"[BookshelfApiSync] Room thumbnail capture failed: {ex.Message}");
                        nextOnFailed?.Invoke("Room thumbnail capture failed.");
                    }

                    if (!captureFailed)
                    {
                        if (thumbnail.Bytes == null || thumbnail.Bytes.Length == 0)
                        {
                            nextOnFailed?.Invoke("Room thumbnail image was empty.");
                        }
                        else if (isViewingOtherUserSpace)
                        {
                            nextOnFailed?.Invoke("Room thumbnail upload cancelled while visiting another space.");
                        }
                        else
                        {
                            var completed = false;
                            UploadCapturedRoomThumbnail(
                                thumbnail,
                                nextOnSucceeded,
                                nextOnFailed,
                                () => completed = true);

                            while (!completed)
                            {
                                yield return null;
                            }
                        }
                    }
                }

                if (!thumbnailUploadPending)
                {
                    break;
                }

                nextOnSucceeded = pendingThumbnailUploadSucceeded;
                nextOnFailed = pendingThumbnailUploadFailed;
            }

            isThumbnailUploading = false;
        }

        private IEnumerator WaitForRoomThumbnailCaptureReady(Action<bool> onCompleted)
        {
            var deadline = Time.realtimeSinceStartup + RoomThumbnailRefreshWaitTimeoutSeconds;
            while (isRefreshing && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (isRefreshing)
            {
                Debug.LogWarning("[BookshelfApiSync] Room thumbnail capture skipped because bookshelf refresh did not complete within timeout.");
                onCompleted?.Invoke(false);
                yield break;
            }

            for (var frame = 0; frame < RoomThumbnailCaptureSettleFrames; frame++)
            {
                yield return null;
            }

            yield return new WaitForEndOfFrame();
            onCompleted?.Invoke(true);
        }

        private void UploadCapturedRoomThumbnail(
            CapturedRoomThumbnail thumbnail,
            Action<NotebookSpaceThumbnailUploadResult> onSucceeded,
            Action<string> onFailed,
            Action onCompleted,
            bool allowAuthRefreshRetry = true)
        {
            notebookRuntimeUseCase.UploadMySpaceThumbnail(
                thumbnail.Bytes,
                thumbnail.ContentType,
                result =>
                {
                    Debug.Log($"[BookshelfApiSync] Room thumbnail uploaded: key={result?.ThumbnailKey}, url={result?.ThumbnailUrl}");
                    onSucceeded?.Invoke(result);
                    onCompleted?.Invoke();
                },
                error =>
                {
                    if (allowAuthRefreshRetry && IsRefreshableThumbnailUploadAuthError(error))
                    {
                        Debug.LogWarning("[BookshelfApiSync] Room thumbnail upload token expired; refreshing session and retrying once.");
                        SessionRefresh.TryRefreshAndRetry(
                            () => UploadCapturedRoomThumbnail(thumbnail, onSucceeded, onFailed, onCompleted, false),
                            () => FailRoomThumbnailUpload(error, onFailed, onCompleted));
                        return;
                    }

                    FailRoomThumbnailUpload(error, onFailed, onCompleted);
                });
        }

        private static void FailRoomThumbnailUpload(
            NotebookApiError error,
            Action<string> onFailed,
            Action onCompleted)
        {
            var message = GetNotebookErrorMessage(error);
            Debug.LogWarning($"[BookshelfApiSync] Room thumbnail upload failed: code={error?.Code} message={message}");
            onFailed?.Invoke(message);
            onCompleted?.Invoke();
        }

        private static string GetNotebookErrorMessage(NotebookApiError error)
        {
            return string.IsNullOrWhiteSpace(error?.Message) ? error?.Error : error.Message;
        }

        private static bool IsRefreshableThumbnailUploadAuthError(NotebookApiError error)
        {
            var code = error?.Code?.Trim();
            return string.Equals(code, "A003", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, "TOKEN_EXPIRED", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, "UNAUTHORIZED", StringComparison.OrdinalIgnoreCase);
        }

        private CapturedRoomThumbnail CaptureMainViewThumbnail()
        {
            if (cameraController == null ||
                !cameraController.TryGetMainViewPose(out var camera, out var capturePosition, out var captureRotation, out var captureFieldOfView))
            {
                throw new InvalidOperationException("Main view capture pose was not available.");
            }

            var savedPosition = camera.transform.position;
            var savedRotation = camera.transform.rotation;
            var savedFov = camera.fieldOfView;
            var savedRect = camera.rect;
            var savedTargetTexture = camera.targetTexture;
            var previousActive = RenderTexture.active;

            var width = RoomThumbnailCaptureWidth;
            var height = RoomThumbnailCaptureHeight;
            var renderTextureFormat = camera.allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.ARGB32;
            var antiAliasing = camera.allowMSAA ? Mathf.Max(1, QualitySettings.antiAliasing) : 1;
            var renderTexture = RenderTexture.GetTemporary(
                width,
                height,
                24,
                renderTextureFormat,
                RenderTextureReadWrite.Default,
                antiAliasing);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);

            try
            {
                // Render the actual main camera into an off-screen target. We only
                // change pose/rect/targetTexture temporarily; URP camera data,
                // Volume masks, post-processing, HDR, and MSAA stay on the camera.
                var thumbnailRotation = captureRotation * Quaternion.Euler(RoomThumbnailCapturePitchDownDegrees, 0f, 0f);
                camera.transform.SetPositionAndRotation(capturePosition, thumbnailRotation);
                camera.fieldOfView = Mathf.Max(15f, captureFieldOfView * RoomThumbnailCaptureFieldOfViewScale);
                camera.rect = new Rect(0f, 0f, 1f, 1f);
                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                camera.Render();
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply(false);
                return EncodeRoomThumbnail(texture);
            }
            finally
            {
                camera.targetTexture = savedTargetTexture;
                camera.rect = savedRect;
                camera.fieldOfView = savedFov;
                camera.transform.SetPositionAndRotation(savedPosition, savedRotation);
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(renderTexture);
                Destroy(texture);
            }
        }

        private static CapturedRoomThumbnail EncodeRoomThumbnail(Texture2D texture)
        {
            var pngBytes = texture.EncodeToPNG();
            if (pngBytes != null && pngBytes.Length > 0 && pngBytes.Length <= RoomThumbnailMaxUploadBytes)
            {
                return new CapturedRoomThumbnail(pngBytes, RoomThumbnailPngContentType);
            }

            pngBytes = null;
            return EncodeRoomThumbnailJpeg(texture);
        }

        private static CapturedRoomThumbnail EncodeRoomThumbnailJpeg(Texture2D texture)
        {
            foreach (var quality in RoomThumbnailJpegQualities)
            {
                var imageBytes = texture.EncodeToJPG(quality);
                if (imageBytes != null && imageBytes.Length > 0 && imageBytes.Length <= RoomThumbnailMaxUploadBytes)
                {
                    return new CapturedRoomThumbnail(imageBytes, RoomThumbnailJpegContentType);
                }
            }

            throw new InvalidOperationException("Room thumbnail image exceeded the upload size limit.");
        }

        private readonly struct CapturedRoomThumbnail
        {
            public CapturedRoomThumbnail(byte[] bytes, string contentType)
            {
                Bytes = bytes;
                ContentType = contentType;
            }

            public byte[] Bytes { get; }
            public string ContentType { get; }
        }

        public bool SaveReadingRecord(
            BookEntity book,
            Action<BookEntity> onSucceeded = null,
            Action<string> onFailed = null)
        {
            if (!CanUseServerApi || book == null || !TryParseId(book.userBookId, out var userBookId))
            {
                return false;
            }

            var request = new ReadingRecordRequestDto
            {
                UserBookId = userBookId,
                Title = book.reportTitle,
                Content = book.reportBody,
                VisibilityScope = ToApiVisibility(book.reportVisibility)
            };

            if (TryParseId(book.readingRecordId, out var recordId))
            {
                client.UpdateReadingRecord(
                    recordId,
                    request,
                    response =>
                    {
                        ApplyReadingRecordResponse(book, response);
                        onSucceeded?.Invoke(book);
                    },
                    error =>
                    {
                        var message = FormatUserFacingError(error);
                        Debug.LogWarning($"Reading record update failed: {FormatError(error)}");
                        onFailed?.Invoke(message);
                    });
                return true;
            }

            client.CreateReadingRecord(
                request,
                response =>
                {
                    ApplyReadingRecordResponse(book, response);
                    onSucceeded?.Invoke(book);
                },
                error =>
                {
                    var message = FormatUserFacingError(error);
                    Debug.LogWarning($"Reading record create failed: {FormatError(error)}");
                    onFailed?.Invoke(message);
                });
            return true;
        }

        private IEnumerator RefreshAfterSceneSettles()
        {
            yield return null;
            RefreshFromServer();
        }

        private void LoadBookshelfItems(
            BookshelfListResponseDto response,
            long expectedSpaceId,
            bool expectedOtherUserSpace,
            int requestVersion,
            long? spaceBookshelfSkinInventoryId = null)
        {
            if (!IsRefreshRequestCurrent(requestVersion) ||
                !IsCurrentSpaceView(expectedSpaceId, expectedOtherUserSpace))
            {
                return;
            }

            itemsLoadFailureCount = 0;
            firstItemsLoadFailureMessage = null;

            var shelves = response?.Bookshelves ?? new List<BookshelfResponseDto>();
            if (shelves.Count == 0)
            {
                generator.ApplyServerBookshelves(Array.Empty<BookshelfRuntimeApiSnapshot>());
                experienceManager?.NotifyBookshelfApiSynced();
                FinishRefreshIfCurrent(requestVersion, true, null);
                return;
            }

            shelves.Sort((left, right) => left.DisplayOrder.CompareTo(right.DisplayOrder));
            var snapshots = new List<BookshelfRuntimeApiSnapshot>();
            LoadBookshelfItemAt(shelves, snapshots, 0, expectedSpaceId, expectedOtherUserSpace, requestVersion, spaceBookshelfSkinInventoryId);
        }

        private void LoadBookshelfItemAt(
            IReadOnlyList<BookshelfResponseDto> shelves,
            List<BookshelfRuntimeApiSnapshot> snapshots,
            int index,
            long expectedSpaceId,
            bool expectedOtherUserSpace,
            int requestVersion,
            long? spaceBookshelfSkinInventoryId)
        {
            if (!IsRefreshRequestCurrent(requestVersion) ||
                !IsCurrentSpaceView(expectedSpaceId, expectedOtherUserSpace))
            {
                return;
            }

            if (index >= shelves.Count)
            {
                var visibleBookshelfSkinAssetId = expectedOtherUserSpace
                    ? ResolveVisibleBookshelfSkinAssetId(spaceBookshelfSkinInventoryId)
                    : null;
                ApplySpaceBookshelfSkinOverride(snapshots, spaceBookshelfSkinInventoryId, visibleBookshelfSkinAssetId);
                generator.ApplyServerBookshelves(snapshots);
                SyncLocalPreviewBookshelfStyleFromCurrentTheme();
                experienceManager?.NotifyBookshelfApiSynced();
                FinishRefreshIfCurrent(requestVersion, true, null);
                return;
            }

            var shelf = shelves[index];

            // 진단 — items API path 의 id 자리에 spaceId 가 잘못 들어가는지 가시화. 정상 응답은 spaceId != id.
            // BE 버그 / DTO 매핑 실수 의심 케이스 (id == 0 또는 spaceId == id) 는 명시적으로 경고 + 호출 skip.
            if (shelf.Id <= 0)
            {
                Debug.LogWarning(
                    $"[Bookshelf] shelf.Id 비유효 (id={shelf.Id}, spaceId={shelf.SpaceId}) — items API skip + 빈 snapshot fallback. " +
                    "BE 응답의 'id' 필드 누락/0 값 의심.");
                snapshots.Add(MapShelfOnly(shelf));
                LoadBookshelfItemAt(shelves, snapshots, index + 1, expectedSpaceId, expectedOtherUserSpace, requestVersion, spaceBookshelfSkinInventoryId);
                return;
            }
            if (shelf.SpaceId > 0 && shelf.SpaceId == shelf.Id)
            {
                Debug.LogWarning(
                    $"[Bookshelf] shelf.SpaceId == shelf.Id ({shelf.Id}) — BE 가 같은 값을 반환 중. " +
                    "/v1/bookshelves/{id}/items 가 spaceId 로 호출되는 것처럼 보일 수 있음. BE 측 점검 필요.");
            }
            Debug.Log($"[Bookshelf] GET /v1/bookshelves/{shelf.Id}/items (spaceId={shelf.SpaceId}, displayOrder={shelf.DisplayOrder})");

            client.GetBookshelfItems(
                shelf.Id,
                render =>
                {
                    if (!IsRefreshRequestCurrent(requestVersion) ||
                        !IsCurrentSpaceView(expectedSpaceId, expectedOtherUserSpace))
                    {
                        return;
                    }

                    snapshots.Add(MapRender(render, shelf));
                    LoadBookshelfItemAt(shelves, snapshots, index + 1, expectedSpaceId, expectedOtherUserSpace, requestVersion, spaceBookshelfSkinInventoryId);
                },
                error =>
                {
                    if (!IsRefreshRequestCurrent(requestVersion) ||
                        !IsCurrentSpaceView(expectedSpaceId, expectedOtherUserSpace))
                    {
                        return;
                    }

                    TrackBookshelfItemsLoadFailure(shelf, error);
                    Debug.LogWarning(
                        $"[Bookshelf] items load failed (bookshelfId={shelf.Id}, spaceId={shelf.SpaceId}): {FormatError(error)} - preserving previous shelf snapshot when available.");
                    var fallback = MapShelfOnly(shelf);
                    fallback.ItemsLoadFailed = true;
                    snapshots.Add(fallback);
                    LoadBookshelfItemAt(shelves, snapshots, index + 1, expectedSpaceId, expectedOtherUserSpace, requestVersion, spaceBookshelfSkinInventoryId);
                });
        }

        private static void ApplySpaceBookshelfSkinOverride(
            IList<BookshelfRuntimeApiSnapshot> snapshots,
            long? spaceBookshelfSkinInventoryId,
            long? spaceBookshelfSkinAssetId = null)
        {
            if (snapshots == null ||
                snapshots.Count == 0 ||
                !TryResolveSpaceBookshelfStyle(spaceBookshelfSkinInventoryId, spaceBookshelfSkinAssetId, out var spaceStyle) ||
                spaceStyle == BookshelfThemeStyle.Default)
            {
                return;
            }

            var fallbackSkinInventoryId = spaceBookshelfSkinInventoryId.HasValue && spaceBookshelfSkinInventoryId.Value > 0L
                ? spaceBookshelfSkinInventoryId.Value.ToString()
                : spaceBookshelfSkinAssetId.GetValueOrDefault().ToString();
            for (var i = 0; i < snapshots.Count; i++)
            {
                var snapshot = snapshots[i];
                if (snapshot == null)
                {
                    continue;
                }

                snapshot.HasThemeStyle = true;
                snapshot.ThemeStyle = spaceStyle;
                snapshot.SkinInventoryId = fallbackSkinInventoryId;
            }
        }

        private static bool TryResolveSpaceBookshelfStyle(
            long? spaceBookshelfSkinInventoryId,
            long? spaceBookshelfSkinAssetId,
            out BookshelfThemeStyle spaceStyle)
        {
            if (BookshelfSkinResolver.TryResolveThemeStyle(spaceBookshelfSkinAssetId, out spaceStyle))
            {
                return true;
            }

            return BookshelfSkinResolver.TryResolveThemeStyle(spaceBookshelfSkinInventoryId, out spaceStyle);
        }

        private static BookshelfRuntimeApiSnapshot MapRender(
            BookshelfRenderResponseDto render,
            BookshelfResponseDto fallbackShelf)
        {
            var shelfDto = render?.Bookshelf ?? fallbackShelf;
            var snapshot = MapShelfOnly(shelfDto);
            var sections = render?.Sections ?? new List<SectionResponseDto>();
            for (var i = 0; i < sections.Count; i++)
            {
                snapshot.Sections.Add(new BookshelfRuntimeSectionSnapshot
                {
                    SectionId = sections[i].Id.ToString(),
                    Row = sections[i].RowNo,
                    Column = sections[i].ColumnNo,
                    Alignment = ParseAlignment(sections[i].Alignment)
                });
            }

            var sectionById = new Dictionary<long, BookshelfRuntimeSectionSnapshot>();
            for (var i = 0; i < snapshot.Sections.Count; i++)
            {
                if (long.TryParse(snapshot.Sections[i].SectionId, out var id))
                {
                    sectionById[id] = snapshot.Sections[i];
                }
            }

            var items = render?.Items ?? new List<BookshelfRenderItemResponseDto>();
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (!string.Equals(item.ItemType, "BOOK", StringComparison.OrdinalIgnoreCase) || item.Book == null)
                {
                    continue;
                }

                var row = item.SectionRow ?? 0;
                var column = item.SectionColumn ?? 0;
                if (sectionById.TryGetValue(item.SectionId, out var section))
                {
                    row = section.Row;
                    column = section.Column;
                }

                snapshot.Books.Add(new BookshelfRuntimeBookSnapshot
                {
                    BookshelfItemId = item.BookshelfItemId.ToString(),
                    UserBookId = item.UserBookId?.ToString() ?? (item.UserBook != null ? item.UserBook.Id.ToString() : null),
                    BookId = item.Book.BookId.ToString(),
                    SectionId = item.SectionId.ToString(),
                    Row = row,
                    Column = column,
                    SlotNo = item.SlotNo ?? 1,
                    Orientation = ParseDisplayStyle(item.DisplayStyle),
                    Title = item.Book.Title,
                    Author = item.Book.Author,
                    Publisher = item.Book.Publisher,
                    Description = item.Book.Description,
                    MemoPreview = item.ReadingRecord != null && !string.IsNullOrWhiteSpace(item.ReadingRecord.Content)
                        ? item.ReadingRecord.Content
                        : item.Book.Description,
                    AverageRating = ResolveAverageRating(item.Book),
                    TotalLikeCount = ResolveLikeCount(item.Book, item.UserBook?.Liked == true),
                    ReviewCount = ResolveReviewCount(item.Book),
                    HasAggregateBookStats = HasAggregateBookStats(item.Book),
                    OwnerRating = item.UserBook?.OwnerRating.HasValue == true ? (float)item.UserBook.OwnerRating.Value : 0f,
                    ReadingStatus = ParseReadingStatus(item.UserBook?.ReadingStatus),
                    IsLiked = item.UserBook?.Liked == true,
                    ReadingRecordId = item.ReadingRecord?.Id.ToString(),
                    ReportTitle = item.ReadingRecord?.Title,
                    ReportBody = item.ReadingRecord?.Content,
                    ReportUpdatedLabel = FormatApiDate(item.ReadingRecord?.UpdatedAt),
                    ReportVisibility = ParseVisibility(item.ReadingRecord?.VisibilityScope)
                });
            }

            return snapshot;
        }

        private static BookshelfRuntimeApiSnapshot MapShelfOnly(BookshelfResponseDto shelf)
        {
            var snapshot = new BookshelfRuntimeApiSnapshot
            {
                BookshelfId = shelf?.Id.ToString(),
                Name = shelf?.Name,
                Description = shelf?.Description,
                VisibilityScope = ParseVisibility(shelf?.VisibilityScope),
                DisplayOrder = shelf?.DisplayOrder ?? 0,
                BodyInventoryId = shelf?.BodyInventoryId?.ToString(),
                SkinInventoryId = shelf?.SkinInventoryId?.ToString()
            };

            if (BookshelfSkinResolver.TryResolveThemeStyle(shelf?.SkinInventoryId, out var style))
            {
                snapshot.HasThemeStyle = true;
                snapshot.ThemeStyle = style;
            }

            return snapshot;
        }

        private void SyncLocalPreviewBookshelfStyleFromCurrentTheme()
        {
            if (generator == null || isViewingOtherUserSpace)
            {
                return;
            }

            var themeApplier = FindFirstObjectByType<RoomThemeApplier>(FindObjectsInactive.Include);
            var currentTheme = themeApplier != null ? themeApplier.CurrentTheme : null;
            if (currentTheme == null || !currentTheme.IsLocalPreview)
            {
                return;
            }

            var styleIndex = currentTheme.BookshelfStyleIndex;
            if (!Enum.IsDefined(typeof(BookshelfThemeStyle), styleIndex))
            {
                return;
            }

            var desiredStyle = (BookshelfThemeStyle)styleIndex;
            if (desiredStyle == BookshelfThemeStyle.Default)
            {
                return;
            }

            var currentStyle = generator.GetBookshelfDesignStyle(generator.ActiveBookshelfIndex);
            if (currentStyle != BookshelfThemeStyle.Default)
            {
                return;
            }

            if (generator.SetActiveBookshelfDesignStyle(desiredStyle))
            {
                Debug.Log($"[BookshelfApiSync] Re-synced local preview bookshelf style to {desiredStyle} after initial server bookshelf load.");
            }
        }

        private void TrackBookshelfItemsLoadFailure(BookshelfResponseDto shelf, ApiErrorResponseDto error)
        {
            itemsLoadFailureCount++;
            if (!string.IsNullOrWhiteSpace(firstItemsLoadFailureMessage))
            {
                return;
            }

            var shelfName = string.IsNullOrWhiteSpace(shelf?.Name) ? "책장" : shelf.Name;
            firstItemsLoadFailureMessage = $"{shelfName}: {FormatUserFacingError(error)}";
        }

        private void FinishRefresh(bool success, string message)
        {
            var hasItemsLoadFailure = success && itemsLoadFailureCount > 0;
            var completionSuccess = success && !hasItemsLoadFailure;
            var completionMessage = message;
            if (hasItemsLoadFailure)
            {
                completionMessage = itemsLoadFailureCount == 1
                    ? $"책장 내용 조회에 실패했습니다. {firstItemsLoadFailureMessage}"
                    : $"{itemsLoadFailureCount}개 책장 내용 조회에 실패했습니다. {firstItemsLoadFailureMessage}";
                Debug.LogWarning($"Bookshelf API refresh completed with item load failures: {completionMessage}");
            }

            isRefreshing = false;
            activeRefreshTargetsOtherUserSpace = false;
            hasCompletedInitialRefresh = true;
            lastInitialRefreshSucceeded = completionSuccess;
            lastInitialRefreshMessage = completionMessage;
            if (!success && !string.IsNullOrWhiteSpace(message))
            {
                Debug.LogWarning($"Bookshelf API refresh skipped: {message}");
            }

            var completion = refreshCompletion;
            var queuedCompletions = queuedRefreshCompletions.Count > 0
                ? queuedRefreshCompletions.ToArray()
                : Array.Empty<Action<bool, string>>();
            queuedRefreshCompletions.Clear();
            refreshCompletion = null;
            itemsLoadFailureCount = 0;
            firstItemsLoadFailureMessage = null;
            completion?.Invoke(completionSuccess, completionMessage);
            for (var i = 0; i < queuedCompletions.Length; i++)
            {
                queuedCompletions[i]?.Invoke(completionSuccess, completionMessage);
            }
        }

        private void FinishRefreshIfCurrent(int requestVersion, bool success, string message)
        {
            if (!IsRefreshRequestCurrent(requestVersion))
            {
                return;
            }

            FinishRefresh(success, message);
        }

        private static void ApplyUserBookResponse(BookEntity book, UserBookResponseDto response)
        {
            if (book == null || response == null)
            {
                return;
            }

            book.userBookId = response.Id.ToString();
            book.readingStatus = ParseReadingStatus(response.ReadingStatus);
            book.isRead = book.readingStatus == ReadingStatus.Read;
            book.ownerRating = response.OwnerRating.HasValue ? (float)response.OwnerRating.Value : book.ownerRating;
            book.isLiked = response.Liked;
            if (response.Placement != null)
            {
                book.bookshelfItemId = response.Placement.BookshelfItemId?.ToString() ?? book.bookshelfItemId;
                book.bookshelfId = response.Placement.BookshelfId?.ToString() ?? book.bookshelfId;
                book.bookshelfSectionId = response.Placement.SectionId?.ToString() ?? book.bookshelfSectionId;
            }
        }

        private static void ApplyBookResponse(BookEntity book, BookResponseDto response)
        {
            if (book == null || response == null)
            {
                return;
            }

            book.bookId = ResolveBookId(response.BookId, response.Id, book.bookId);
            book.source = response.Source;
            book.isbn13 = response.Isbn13;
            book.isbn10 = response.Isbn10;
            book.publishedDate = response.PublishedDate;
            book.coverImageUrl = response.CoverImageUrl;
            book.sourceUrl = response.SourceUrl;
            book.title = string.IsNullOrWhiteSpace(response.Title) ? book.title : response.Title;
            book.author = string.IsNullOrWhiteSpace(response.Author) ? book.author : response.Author;
            book.publisher = string.IsNullOrWhiteSpace(response.Publisher) ? book.publisher : response.Publisher;
            book.description = string.IsNullOrWhiteSpace(response.Description) ? book.description : response.Description;
            book.memoPreview = string.IsNullOrWhiteSpace(book.memoPreview) ? book.description : book.memoPreview;
            book.isPaidCoverOwned = response.HasPaidCover;
            book.averageRating = Mathf.Clamp((float)response.AvgRating, 0f, 5f);
            book.totalLikeCount = Mathf.Max(0, response.LikeCount);
            book.reviewCount = Mathf.Max(0, response.ReviewCount);
            book.hasAggregateBookStats = true;
            book.rating = Mathf.RoundToInt(Mathf.Clamp(book.averageRating, 0f, 5f));
        }

        private static string ResolveBookId(string responseBookId, long responseId, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(responseBookId))
            {
                return responseBookId;
            }

            return responseId > 0L ? responseId.ToString() : fallback;
        }

        private static bool HasAggregateBookStats(BookSummaryResponseDto book)
        {
            return book != null &&
                   (book.AvgRating.HasValue ||
                    book.AverageRating.HasValue ||
                    book.LikeCount.HasValue ||
                    book.ReviewCount.HasValue ||
                    book.RecordCount.HasValue);
        }

        private static float ResolveAverageRating(BookSummaryResponseDto book)
        {
            if (book == null)
            {
                return 0f;
            }

            var value = book.AverageRating ?? book.AvgRating ?? 0m;
            return Mathf.Clamp((float)value, 0f, 5f);
        }

        private static int ResolveLikeCount(BookSummaryResponseDto book, bool isLiked)
        {
            var count = book == null ? 0 : Mathf.Max(0, book.LikeCount ?? 0);
            return ResolveVisibleLikeCount(count, isLiked);
        }

        private static int ResolveVisibleLikeCount(int count, bool isLiked)
        {
            count = Mathf.Max(0, count);
            return isLiked ? Mathf.Max(1, count) : count;
        }

        private static int ClampCount(long count)
        {
            if (count <= 0L)
            {
                return 0;
            }

            return count >= int.MaxValue ? int.MaxValue : (int)count;
        }

        private static int ResolveReviewCount(BookSummaryResponseDto book)
        {
            if (book == null)
            {
                return 0;
            }

            return Mathf.Max(0, book.ReviewCount ?? book.RecordCount ?? 0);
        }

        private static void ApplyBookshelfItemResponse(BookEntity book, BookshelfItemResponseDto response)
        {
            if (book == null || response == null)
            {
                return;
            }

            book.bookshelfItemId = response.Id.ToString();
            book.bookshelfId = response.BookshelfId.ToString();
            book.bookshelfSectionId = response.SectionId.ToString();
        }

        private static void ApplyReadingRecordResponse(BookEntity book, ReadingRecordResponseDto response)
        {
            if (book == null || response == null)
            {
                return;
            }

            book.readingRecordId = response.Id.ToString();
            book.reportTitle = response.Title;
            book.reportBody = response.Content;
            book.reportVisibility = ParseVisibility(response.VisibilityScope);
            book.reportUpdatedLabel = FormatApiDate(response.UpdatedAt);
            book.reportLikeCount = response.LikeCount;
            book.reportCommentCount = response.CommentCount;
            book.memoPreview = !string.IsNullOrWhiteSpace(response.Content) ? response.Content : book.memoPreview;
        }

        private static bool TryParseId(string value, out long id)
        {
            return long.TryParse(value, out id) && id > 0;
        }

        private static string ToApiReadingStatus(ReadingStatus status)
        {
            switch (status)
            {
                case ReadingStatus.Read:
                    return "READ";
                case ReadingStatus.Reading:
                    return "READING";
                default:
                    return "UNREAD";
            }
        }

        private static ReadingStatus ParseReadingStatus(string value)
        {
            if (string.Equals(value, "READ", StringComparison.OrdinalIgnoreCase))
            {
                return ReadingStatus.Read;
            }

            return string.Equals(value, "READING", StringComparison.OrdinalIgnoreCase)
                ? ReadingStatus.Reading
                : ReadingStatus.Unread;
        }

        private static string ToApiVisibility(VisibilityScope scope)
        {
            switch (scope)
            {
                case VisibilityScope.Private:
                    return "PRIVATE";
                case VisibilityScope.Followers:
                    return "FOLLOWERS";
                default:
                    return "PUBLIC";
            }
        }

        private static VisibilityScope ParseVisibility(string value)
        {
            if (string.Equals(value, "PRIVATE", StringComparison.OrdinalIgnoreCase))
            {
                return VisibilityScope.Private;
            }

            // MUTUAL = BE 정식값(맞팔). FOLLOWERS / FOLLOWERS_ONLY 는 레거시 별칭 — 함께 수용.
            if (string.Equals(value, "MUTUAL", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "FOLLOWERS", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "FOLLOWERS_ONLY", StringComparison.OrdinalIgnoreCase))
            {
                return VisibilityScope.Followers;
            }

            return VisibilityScope.Public;
        }

        private static string ToApiAlignment(ShelfSectionAlignment alignment)
        {
            switch (alignment)
            {
                case ShelfSectionAlignment.Center:
                    return "CENTER";
                case ShelfSectionAlignment.Right:
                    return "RIGHT";
                default:
                    return "LEFT";
            }
        }

        private static string ToApiDisplayStyle(BookOrientation orientation)
        {
            switch (orientation)
            {
                case BookOrientation.Front:
                    return "FRONT";
                case BookOrientation.Angled45:
                    return "ANGLED45";
                default:
                    return "SPINE";
            }
        }

        private static BookOrientation ParseDisplayStyle(string value)
        {
            if (string.Equals(value, "FRONT", StringComparison.OrdinalIgnoreCase))
            {
                return BookOrientation.Front;
            }

            return string.Equals(value, "ANGLED45", StringComparison.OrdinalIgnoreCase)
                ? BookOrientation.Angled45
                : BookOrientation.Spine;
        }

        private static ShelfSectionAlignment ParseAlignment(string value)
        {
            if (string.Equals(value, "CENTER", StringComparison.OrdinalIgnoreCase))
            {
                return ShelfSectionAlignment.Center;
            }

            return string.Equals(value, "RIGHT", StringComparison.OrdinalIgnoreCase)
                ? ShelfSectionAlignment.Right
                : ShelfSectionAlignment.Left;
        }

        private static string FormatApiDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return DateTimeOffset.TryParse(value, out var parsed)
                ? parsed.ToString("yyyy.MM.dd")
                : value;
        }

        private static string FormatError(ApiErrorResponseDto error)
        {
            if (error == null)
            {
                return "unknown error";
            }

            return string.IsNullOrWhiteSpace(error.Message)
                ? error.Error
                : error.Message;
        }

        private static string FormatUserFacingError(ApiErrorResponseDto error)
        {
            if (error == null)
            {
                return "네트워크 또는 서버 응답을 확인해 주세요.";
            }

            if (error.StatusCode == 401L || error.StatusCode == 403L)
            {
                return "권한이 없거나 로그인 정보가 만료되었습니다.";
            }

            if (error.StatusCode >= 500L)
            {
                return "서버가 요청을 처리하지 못했습니다. 잠시 후 다시 시도해 주세요.";
            }

            return "요청을 완료하지 못했습니다. 잠시 후 다시 시도해 주세요.";
        }
    }

    internal static class BookshelfSkinResolver
    {
        private const string CatalogResourcePath = "Catalogs/BookshelfSkinCatalog";
        private static ScriptableObject cachedCatalog;
        private static bool didLoadCatalog;
        private static bool didWarnMissingCatalog;
        private static bool didWarnMissingStyle;
        private static bool didLoadApiAssets;
        private static readonly Dictionary<BookshelfThemeStyle, long> apiSkinInventoryIdsByStyle = new Dictionary<BookshelfThemeStyle, long>();
        private static readonly Dictionary<long, BookshelfThemeStyle> apiStylesByInventoryId = new Dictionary<long, BookshelfThemeStyle>();
        private static readonly Dictionary<long, BookshelfThemeStyle> apiStylesByAssetId = new Dictionary<long, BookshelfThemeStyle>();

        public static bool HasLoadedApiAssets => didLoadApiAssets;

        public static void RegisterApiAssets(AssetListResponseDto response, InventoryResponseDto inventory)
        {
            didLoadApiAssets = true;
            apiSkinInventoryIdsByStyle.Clear();
            apiStylesByInventoryId.Clear();
            apiStylesByAssetId.Clear();

            var assets = response?.Assets;
            if (assets == null)
            {
                return;
            }

            var inventoryIdsByAssetId = BuildInventoryIdsByAssetId(inventory);
            var skinIndex = 0;
            for (var i = 0; i < assets.Count; i++)
            {
                var asset = assets[i];
                if (asset == null ||
                    asset.Id <= 0L ||
                    !string.Equals(asset.AssetType, "BOOKSHELF_SKIN", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var style = ResolveStyleFromKey(asset.Code, asset.Name, skinIndex);
                apiStylesByAssetId[asset.Id] = style;

                if (inventoryIdsByAssetId.TryGetValue(asset.Id, out var inventoryId) && inventoryId > 0L)
                {
                    if (!apiSkinInventoryIdsByStyle.ContainsKey(style))
                    {
                        apiSkinInventoryIdsByStyle[style] = inventoryId;
                    }

                    apiStylesByInventoryId[inventoryId] = style;
                }

                skinIndex++;
            }
        }

        public static bool HasResolvableInventoryId(BookshelfThemeStyle style)
        {
            return apiSkinInventoryIdsByStyle.TryGetValue(style, out var inventoryId) && inventoryId > 0L;
        }

        public static long? ResolveInventoryId(BookshelfThemeStyle style)
        {
            if (apiSkinInventoryIdsByStyle.TryGetValue(style, out var inventoryId) && inventoryId > 0L)
            {
                return inventoryId;
            }

            WarnMissingStyle(style);
            return null;
        }

        public static bool TryResolveThemeStyle(long? skinInventoryId, out BookshelfThemeStyle style)
        {
            style = BookshelfThemeStyle.Default;
            if (!skinInventoryId.HasValue || skinInventoryId.Value <= 0L)
            {
                return false;
            }

            if (apiStylesByInventoryId.TryGetValue(skinInventoryId.Value, out style))
            {
                return true;
            }

            // Legacy read-only compatibility: older FE builds sent Asset.id in
            // this field. Keep rendering those shelves, but never use asset ids
            // for future update requests.
            if (apiStylesByAssetId.TryGetValue(skinInventoryId.Value, out style))
            {
                return true;
            }

            var entries = GetCatalogEntries();
            if (entries == null)
            {
                return false;
            }

            var index = 0;
            foreach (var entry in entries)
            {
                if (entry != null && ResolveEntryAssetId(entry) == skinInventoryId.Value)
                {
                    style = ResolveStyleFromEntry(entry, index);
                    return true;
                }

                index++;
            }

            return false;
        }

        private static Dictionary<long, long> BuildInventoryIdsByAssetId(InventoryResponseDto inventory)
        {
            var result = new Dictionary<long, long>();
            var items = inventory?.Items;
            if (items == null)
            {
                return result;
            }

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null || item.AssetId <= 0L || item.InventoryId <= 0L)
                {
                    continue;
                }

                result[item.AssetId] = item.InventoryId;
            }

            return result;
        }

        private static object FindEntry(BookshelfThemeStyle style)
        {
            var entries = GetCatalogEntries();
            if (entries == null)
            {
                return null;
            }

            var index = 0;
            foreach (var entry in entries)
            {
                if (entry != null && ResolveStyleFromEntry(entry, index) == style)
                {
                    return entry;
                }

                index++;
            }

            return null;
        }

        private static ScriptableObject LoadCatalog()
        {
            if (didLoadCatalog)
            {
                return cachedCatalog;
            }

            didLoadCatalog = true;
            cachedCatalog = Resources.Load<ScriptableObject>(CatalogResourcePath);
            if (cachedCatalog == null && !didWarnMissingCatalog)
            {
                didWarnMissingCatalog = true;
                Debug.LogWarning($"Bookshelf skin catalog not found at Resources/{CatalogResourcePath}. SkinInventoryId sync will be skipped.");
            }

            return cachedCatalog;
        }

        private static IEnumerable GetCatalogEntries()
        {
            var catalog = LoadCatalog();
            if (catalog == null)
            {
                return null;
            }

            var field = catalog.GetType().GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic);
            return field != null ? field.GetValue(catalog) as IEnumerable : null;
        }

        private static long ResolveEntryAssetId(object entry)
        {
            if (entry == null)
            {
                return 0L;
            }

            var property = entry.GetType().GetProperty("AssetId", BindingFlags.Instance | BindingFlags.Public);
            var value = property != null ? property.GetValue(entry) : null;
            return value is long id ? id : 0L;
        }

        private static string ResolveEntryDisplayName(object entry)
        {
            if (entry == null)
            {
                return null;
            }

            var property = entry.GetType().GetProperty("DisplayName", BindingFlags.Instance | BindingFlags.Public);
            return property != null ? property.GetValue(entry) as string : null;
        }

        private static BookshelfThemeStyle ResolveStyleFromEntry(object entry, int index)
        {
            switch (ResolveEntryAssetId(entry))
            {
                case 200020L: return BookshelfThemeStyle.Vintage;
                case 300020L: return BookshelfThemeStyle.DeepSea;
                case 400020L: return BookshelfThemeStyle.Fantasy;
                case 500020L: return BookshelfThemeStyle.Space;
                case 600020L: return BookshelfThemeStyle.Spooky;
            }

            if (TryResolveStyleByKeyword(ResolveEntryDisplayName(entry), out var style))
            {
                return style;
            }

            return index switch
            {
                1 => BookshelfThemeStyle.Warm,
                2 => BookshelfThemeStyle.Dark,
                _ => BookshelfThemeStyle.Default
            };
        }

        private static BookshelfThemeStyle ResolveStyleFromKey(string code, string name, int index)
        {
            if (TryResolveStyleByKeyword($"{code} {name}", out var style))
            {
                return style;
            }

            return index switch
            {
                1 => BookshelfThemeStyle.Warm,
                2 => BookshelfThemeStyle.Dark,
                _ => BookshelfThemeStyle.Default
            };
        }

        private static bool TryResolveStyleByKeyword(string key, out BookshelfThemeStyle style)
        {
            style = BookshelfThemeStyle.Default;
            if (!string.IsNullOrWhiteSpace(key))
            {
                key = key.Trim().ToLowerInvariant();
                if (key.Contains("deepsea") || key.Contains("deep sea"))
                {
                    style = BookshelfThemeStyle.DeepSea;
                    return true;
                }

                if (key.Contains("vintage"))
                {
                    style = BookshelfThemeStyle.Vintage;
                    return true;
                }

                if (key.Contains("spooky"))
                {
                    style = BookshelfThemeStyle.Spooky;
                    return true;
                }

                if (key.Contains("fantasy"))
                {
                    style = BookshelfThemeStyle.Fantasy;
                    return true;
                }

                if (key.Contains("space"))
                {
                    style = BookshelfThemeStyle.Space;
                    return true;
                }

                if (key.Contains("goodvibes") || key.Contains("good vibes"))
                {
                    style = BookshelfThemeStyle.GoodVibes;
                    return true;
                }

                if (key.Contains("dark") || key.Contains("black") || key.Contains("night") ||
                    key.Contains("underwater") || key.Contains("shell") || key.Contains("ocean") || key.Contains("sea"))
                {
                    style = BookshelfThemeStyle.Dark;
                    return true;
                }

                if (key.Contains("warm") || key.Contains("wood") || key.Contains("brown") ||
                    key.Contains("pastel") || key.Contains("cabin"))
                {
                    style = BookshelfThemeStyle.Warm;
                    return true;
                }

                if (key.Contains("default") || key.Contains("basic") || key.Contains("ivory") || key.Contains("white"))
                {
                    style = BookshelfThemeStyle.Default;
                    return true;
                }
            }

            return false;
        }

        private static void WarnMissingStyle(BookshelfThemeStyle style)
        {
            if (didWarnMissingStyle)
            {
                return;
            }

            didWarnMissingStyle = true;
            Debug.LogWarning($"Bookshelf skin inventory id for style '{style}' could not be resolved from API asset inventory.");
        }
    }
}
