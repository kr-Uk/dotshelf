using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using App.Application.Notebook;
using App.Features.Notebook.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Dotshelf.EditorTools
{
    [InitializeOnLoad]
    public static class NotebookDashboardLayoutValidationUtility
    {
        private const string ActiveKey = "Dotshelf.NotebookDashboardLayoutValidation.Active";
        private const string OutputDirKey = "Dotshelf.NotebookDashboardLayoutValidation.OutputDir";
        private const string StartedAtKey = "Dotshelf.NotebookDashboardLayoutValidation.StartedAt";
        private const double TimeoutSeconds = 90d;
        private const int WaitFramesPerState = 60;
        private const int ScreenshotWaitFrames = 8;
        private const double MinimumSecondsPerState = 0.55d;
        private static readonly BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly ValidationCase[] Cases =
        {
            new("book-market-1920x1080", 1920, 1080, controller => ActivateHeaderTab(controller, "책 시장")),
            new("book-search-1600x900", 1600, 900, controller =>
            {
                ActivateHeaderTab(controller, "책 시장");
                SetField(controller, "query", "완벽하지 않은");
                Invoke(controller, "SubmitSearch");
            }),
            new("book-detail-1366x768", 1366, 768, controller =>
            {
                ActivateHeaderTab(controller, "책 시장");
                var book = Invoke(controller, "GetSelectedBook");
                Invoke(controller, "OpenBookDetailModal", book);
            }),
            new("other-spaces-1280x720", 1280, 720, controller => ActivateHeaderTab(controller, "다른 공간")),
            new("other-spaces-search-1366x768", 1366, 768, controller =>
            {
                ActivateHeaderTab(controller, "다른 공간");
                Invoke(controller, "RequestSpaces", "창가", null);
            }),
            new("followers-panel-1280x720", 1280, 720, controller =>
            {
                ActivateHeaderTab(controller, "다른 공간");
                Invoke(controller, "RequestSpaces", string.Empty, null);
                var space = GetFirstArrayItem(controller, "apiSpaces");
                Invoke(controller, "OpenSocialListPanel", space, "followers");
            }),
            new("followings-panel-1600x900", 1600, 900, controller =>
            {
                ActivateHeaderTab(controller, "다른 공간");
                Invoke(controller, "RequestSpaces", string.Empty, null);
                var space = GetFirstArrayItem(controller, "apiSpaces");
                Invoke(controller, "OpenSocialListPanel", space, "followings");
            }),
            new("object-market-1366x768", 1366, 768, controller => ActivateHeaderTab(controller, "꾸미기 시장")),
            new("wallet-panel-1280x720", 1280, 720, controller => Invoke(controller, "SetWalletPanelOpen", true, false)),
        };

        private static NotebookToolkitController controller;
        private static UIDocument document;
        private static int caseIndex;
        private static int frameInState;
        private static int screenshotWait;
        private static string pendingScreenshotPath;
        private static int pendingScreenshotWidth;
        private static int pendingScreenshotHeight;
        private static double startedAt;
        private static double casePreparedAt;
        private static readonly List<string> failures = new();
        private static string outputDir;
        private static PanelSettings panelSettings;
        private static RenderTexture panelRenderTexture;

        static NotebookDashboardLayoutValidationUtility()
        {
            if (SessionState.GetBool(ActiveKey, false))
            {
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
            }
        }

        [MenuItem("Dotshelf/Notebook/Validate Dashboard Layouts")]
        public static void ValidateDashboardLayouts()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Cannot start notebook dashboard layout validation while play mode is already active.");
            }

            outputDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/NotebookDashboardLayoutValidation"));
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, true);
            }

            Directory.CreateDirectory(outputDir);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString(OutputDirKey, outputDir);
            failures.Clear();
            caseIndex = 0;
            frameInState = 0;
            screenshotWait = 0;
            pendingScreenshotPath = null;
            startedAt = EditorApplication.timeSinceStartup;
            SessionState.SetFloat(StartedAtKey, (float)startedAt);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false))
            {
                EditorApplication.update -= Tick;
                return;
            }

            if (startedAt <= 0d)
            {
                startedAt = SessionState.GetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
            }

            if (EditorApplication.timeSinceStartup - startedAt > TimeoutSeconds)
            {
                Finish(1, $"Notebook dashboard layout validation timed out after {TimeoutSeconds:0}s.");
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                return;
            }

            outputDir ??= SessionState.GetString(OutputDirKey, Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/NotebookDashboardLayoutValidation")));
            EnsureHarness();

            if (!string.IsNullOrEmpty(pendingScreenshotPath))
            {
                screenshotWait++;
                if (screenshotWait < ScreenshotWaitFrames)
                {
                    return;
                }

                CapturePanelScreenshot(pendingScreenshotPath, pendingScreenshotWidth, pendingScreenshotHeight);
                if (!File.Exists(pendingScreenshotPath) || new FileInfo(pendingScreenshotPath).Length <= 1024L)
                {
                    failures.Add($"Screenshot was not created or is too small: {pendingScreenshotPath}");
                }

                pendingScreenshotPath = null;
                screenshotWait = 0;
                caseIndex++;
                frameInState = 0;
                return;
            }

            if (caseIndex >= Cases.Length)
            {
                Finish(failures.Count == 0 ? 0 : 1, null);
                return;
            }

            var current = Cases[caseIndex];
            if (frameInState == 0)
            {
                Screen.SetResolution(current.Width, current.Height, false);
                ConfigurePanelTarget(current.Width, current.Height);
                ResetDashboardState(controller);
                current.Prepare(controller);
                controller.ShowDashboard(true);
                casePreparedAt = EditorApplication.timeSinceStartup;
            }

            frameInState++;
            if (frameInState < WaitFramesPerState ||
                EditorApplication.timeSinceStartup - casePreparedAt < MinimumSecondsPerState)
            {
                return;
            }

            ValidateCase(current);
            pendingScreenshotPath = Path.Combine(outputDir, $"{caseIndex + 1:00}-{current.Name}.png");
            pendingScreenshotWidth = current.Width;
            pendingScreenshotHeight = current.Height;
            Debug.Log($"Notebook dashboard validation captured {current.Name}: {pendingScreenshotPath}");
        }

        private static void EnsureHarness()
        {
            if (controller != null && document != null)
            {
                return;
            }

            var host = new GameObject("NotebookDashboardLayoutValidationHost");
            UnityEngine.Object.DontDestroyOnLoad(host);
            document = host.AddComponent<UIDocument>();
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "NotebookDashboardLayoutValidationPanelSettings";
            document.panelSettings = panelSettings;
            controller = host.AddComponent<NotebookToolkitController>();

            var bookRepo = new ValidationNotebookBookRepository();
            var runtimeRepo = new ValidationNotebookRuntimeRepository();
            controller.Configure(
                null,
                new SearchNotebookBooksUseCase(bookRepo),
                new LoadNotebookMarketBooksUseCase(bookRepo),
                new AddNotebookBookToShelfUseCase(bookRepo),
                new NotebookRuntimeUseCase(runtimeRepo));
            controller.ShowDashboard(true);
        }

        private static void ValidateCase(ValidationCase current)
        {
            var root = document != null ? document.rootVisualElement?.Q<VisualElement>("dotshelf-notebook-dashboard") : null;
            if (root == null)
            {
                failures.Add($"{current.Name}: root dashboard not found.");
                return;
            }

            var rootBound = root.worldBound;
            if (!IsValidRect(rootBound))
            {
                failures.Add($"{current.Name}: root dashboard has invalid layout {rootBound}.");
                return;
            }

            if (rootBound.width < 600f || rootBound.height < 400f)
            {
                failures.Add($"{current.Name}: root dashboard is unexpectedly small {rootBound.size}.");
            }

            ValidateNamedElement(current.Name, root, "header", rootBound, true);
            ValidateNamedElement(current.Name, root, "hero", rootBound, true);
            ValidateNamedElement(current.Name, root, "main", rootBound, false);
            ValidateNamedElement(current.Name, root, "book-market-content", rootBound, false);
            ValidateNamedElement(current.Name, root, "search-results-panel", rootBound, false);
            ValidateNamedElement(current.Name, root, "book-detail-modal", rootBound, true);
            ValidateNamedElement(current.Name, root, "book-review-panel", rootBound, true);
            ValidateNamedElement(current.Name, root, "other-spaces-search-panel", rootBound, false);
            ValidateNamedElement(current.Name, root, "social-list-panel", rootBound, true);
            ValidateNamedElement(current.Name, root, "object-controls", rootBound, false);
            ValidateNamedElement(current.Name, root, "wallet-detail-panel", rootBound, true);

            var visibleButtons = new List<Button>();
            root.Query<Button>().ForEach(button =>
            {
                if (IsVisible(button))
                {
                    visibleButtons.Add(button);
                }
            });

            foreach (var button in visibleButtons)
            {
                var bounds = button.worldBound;
                if (!IsValidRect(bounds))
                {
                    failures.Add($"{current.Name}: button '{button.text}' has invalid bounds {bounds}.");
                    continue;
                }

                if (bounds.width < 24f || bounds.height < 20f)
                {
                    failures.Add($"{current.Name}: button '{button.text}' is too small {bounds.size}.");
                }

                if (bounds.xMin < rootBound.xMin - 2f || bounds.xMax > rootBound.xMax + 2f)
                {
                    if (!IsInsideScrollView(button))
                    {
                        failures.Add($"{current.Name}: button '{button.text}' escapes horizontally. bounds={bounds}, root={rootBound}");
                    }
                }
            }
        }

        private static void ValidateNamedElement(string caseName, VisualElement root, string name, Rect rootBound, bool requireViewportBounds)
        {
            var element = root.Q<VisualElement>(name);
            if (element == null || !IsVisible(element))
            {
                return;
            }

            var bounds = element.worldBound;
            if (!IsValidRect(bounds))
            {
                failures.Add($"{caseName}: element '{name}' has invalid bounds {bounds}.");
                return;
            }

            if (bounds.xMin < rootBound.xMin - 2f || bounds.xMax > rootBound.xMax + 2f)
            {
                failures.Add($"{caseName}: element '{name}' escapes horizontally. bounds={bounds}, root={rootBound}");
            }

            if (requireViewportBounds && (bounds.yMin < rootBound.yMin - 2f || bounds.yMax > rootBound.yMax + 2f))
            {
                failures.Add($"{caseName}: element '{name}' escapes viewport vertically. bounds={bounds}, root={rootBound}");
            }
        }

        private static bool IsVisible(VisualElement element)
        {
            for (var current = element; current != null; current = current.parent)
            {
                if (current.resolvedStyle.display == DisplayStyle.None ||
                    current.resolvedStyle.visibility == Visibility.Hidden ||
                    current.resolvedStyle.opacity <= 0.01f)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsInsideScrollView(VisualElement element)
        {
            for (var current = element.parent; current != null; current = current.parent)
            {
                if (current is ScrollView)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsValidRect(Rect rect)
        {
            return rect.width > 0.5f &&
                   rect.height > 0.5f &&
                   !float.IsNaN(rect.x) &&
                   !float.IsNaN(rect.y) &&
                   !float.IsInfinity(rect.x) &&
                   !float.IsInfinity(rect.y);
        }

        private static void Finish(int exitCode, string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                Debug.LogError(message);
                failures.Add(message);
            }

            if (failures.Count == 0)
            {
                Debug.Log($"Notebook dashboard layout validation passed. screenshots={outputDir}");
            }
            else
            {
                Debug.LogError("Notebook dashboard layout validation failed:\n" + string.Join("\n", failures));
            }

            SessionState.SetBool(ActiveKey, false);
            EditorApplication.update -= Tick;
            ReleasePanelTarget();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.ExitPlaymode();
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        private static void ConfigurePanelTarget(int width, int height)
        {
            if (panelSettings == null)
            {
                return;
            }

            if (panelRenderTexture != null &&
                panelRenderTexture.width == width &&
                panelRenderTexture.height == height)
            {
                panelSettings.targetTexture = panelRenderTexture;
                return;
            }

            ReleasePanelTarget();
            panelRenderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = $"NotebookDashboardValidationRT-{width}x{height}",
                antiAliasing = 1
            };
            panelRenderTexture.Create();
            panelSettings.targetTexture = panelRenderTexture;
        }

        private static void CapturePanelScreenshot(string path, int width, int height)
        {
            if (panelRenderTexture == null)
            {
                return;
            }

            var previous = RenderTexture.active;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = panelRenderTexture;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply(false, false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void ReleasePanelTarget()
        {
            if (panelSettings != null)
            {
                panelSettings.targetTexture = null;
            }

            if (panelRenderTexture == null)
            {
                return;
            }

            panelRenderTexture.Release();
            UnityEngine.Object.DestroyImmediate(panelRenderTexture);
            panelRenderTexture = null;
        }

        private static void ActivateHeaderTab(NotebookToolkitController target, string tab)
        {
            Invoke(target, "ActivateHeaderTab", tab);
        }

        private static void ResetDashboardState(NotebookToolkitController target)
        {
            InvokeIfExists(target, "CloseBookDetail");
            InvokeIfExists(target, "CloseSocialListPanel");
            InvokeIfExists(target, "SetWalletPanelOpen", false, false);
            SetFieldIfExists(target, "searchPageOpen", false);
            SetFieldIfExists(target, "query", string.Empty);
            SetFieldIfExists(target, "appliedSearchQuery", string.Empty);
            SetFieldIfExists(target, "activeSearchSuggestion", string.Empty);
            SetFieldIfExists(target, "selectedBookOverride", null);
            SetFieldIfExists(target, "selectedBookIndex", 0);
            InvokeIfExists(target, "RefreshAll");
        }

        private static object Invoke(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, InstancePrivate);
            if (method == null)
            {
                throw new MissingMethodException(target.GetType().Name, methodName);
            }

            return method.Invoke(target, args);
        }

        private static void InvokeIfExists(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, InstancePrivate);
            method?.Invoke(target, args);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, InstancePrivate);
            if (field == null)
            {
                throw new MissingFieldException(target.GetType().Name, fieldName);
            }

            field.SetValue(target, value);
        }

        private static void SetFieldIfExists(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, InstancePrivate);
            field?.SetValue(target, value);
        }

        private static object GetFirstArrayItem(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, InstancePrivate);
            if (field == null)
            {
                throw new MissingFieldException(target.GetType().Name, fieldName);
            }

            var array = field.GetValue(target) as Array;
            if (array == null || array.Length == 0)
            {
                throw new InvalidOperationException($"Field '{fieldName}' does not contain any items.");
            }

            return array.GetValue(0);
        }

        private sealed class ValidationCase
        {
            public ValidationCase(string name, int width, int height, Action<NotebookToolkitController> prepare)
            {
                Name = name;
                Width = width;
                Height = height;
                Prepare = prepare;
            }

            public string Name { get; }
            public int Width { get; }
            public int Height { get; }
            public Action<NotebookToolkitController> Prepare { get; }
        }

        private sealed class ValidationNotebookBookRepository : INotebookBookRepository
        {
            private readonly List<NotebookBookSearchItem> books = BuildBooks();

            public void SearchBooks(string keyword, int page, int size, Action<NotebookBookSearchResult> onSuccess, Action<NotebookApiError> onError)
            {
                var normalized = string.IsNullOrWhiteSpace(keyword) ? string.Empty : keyword.Trim();
                var results = books
                    .Where(book => string.IsNullOrEmpty(normalized) ||
                                   Contains(book.Title, normalized) ||
                                   Contains(book.Author, normalized) ||
                                   Contains(book.Publisher, normalized))
                    .Take(Math.Max(1, size))
                    .ToArray();
                onSuccess?.Invoke(new NotebookBookSearchResult(results));
            }

            public void LoadMarketBooks(string category, int limit, Action<NotebookBookSearchResult> onSuccess, Action<NotebookApiError> onError)
            {
                var source = string.Equals(category, "RECOMMENDED", StringComparison.OrdinalIgnoreCase)
                    ? books.OrderByDescending(book => book.AvgRating).ThenByDescending(book => book.ReviewCount)
                    : books.OrderBy(book => book.Rank);
                onSuccess?.Invoke(new NotebookBookSearchResult(source.Take(Math.Max(1, limit)).ToArray()));
            }

            public void AddBookToShelf(long bookshelfId, AddNotebookBookToShelfCommand command, NotebookShelfPlacement placement, Action<NotebookBookAddResult> onSuccess, Action<NotebookApiError> onError)
            {
                onSuccess?.Invoke(new NotebookBookAddResult { Id = 9001, BookId = command?.BookId ?? 1 });
            }

            private static List<NotebookBookSearchItem> BuildBooks()
            {
                var titles = new[]
                {
                    "완벽하지 않은 여름",
                    "숲으로 간 편지",
                    "문장 수집가",
                    "관계의 지도",
                    "조용한 심장",
                    "달라구트 꿈 백화점",
                    "일요일의 경제학",
                    "끝없이 이어지는 방",
                    "햇빛 인사법",
                    "돈을 읽는 습관",
                    "작은 습관의 물리학",
                    "사소한 용기에 대하여"
                };

                return titles.Select((title, index) => new NotebookBookSearchItem
                {
                    Id = index + 1,
                    Source = "LOCAL",
                    BookId = (index + 1).ToString(),
                    Title = title,
                    Author = index % 2 == 0 ? "서은재" : "박이도",
                    Publisher = index % 2 == 0 ? "백지출판" : "초록서재",
                    Description = "긴 제목과 설명이 들어와도 카드 내부에서 줄바꿈과 스크롤이 안정적으로 동작해야 합니다.",
                    AvgRating = 4.9m - (index * 0.03m),
                    ReviewCount = 900 - (index * 24),
                    ViewCount = 18420 - (index * 310),
                    PricePages = index % 3 == 0 ? 0 : 1200 + (index * 100),
                    Rank = index + 1,
                    Owned = index == 1,
                    UserBookId = index == 1 ? 4100 + index : null,
                    LikeCount = 1200 - index * 33,
                    PurchaseCount = 800 - index * 21,
                    RecordCount = 500 - index * 17,
                    PlacementCount = 300 - index * 9,
                    HasPaidCover = index % 3 != 0
                }).ToList();
            }
        }

        private sealed class ValidationNotebookRuntimeRepository : INotebookRuntimeRepository
        {
            private readonly HashSet<long> followed = new() { 201, 203 };
            private readonly HashSet<long> likedSpaces = new() { 3001 };
            private readonly List<NotebookSpaceItem> spaces = BuildSpaces();
            private readonly List<NotebookAssetItem> assets = BuildAssets();

            public void LoadWallet(Action<NotebookWalletResult> onSuccess, Action<NotebookApiError> onError) => onSuccess?.Invoke(new NotebookWalletResult { BalancePages = 4820 });
            public void ChargePages(int pagesAmount, Action<NotebookWalletResult> onSuccess, Action<NotebookApiError> onError) => onSuccess?.Invoke(new NotebookWalletResult { BalancePages = 4820 + pagesAmount });
            public void LoadPurchaseHistory(Action<NotebookPurchaseHistoryResult> onSuccess, Action<NotebookApiError> onError) => onSuccess?.Invoke(new NotebookPurchaseHistoryResult(new[] { new NotebookPurchaseHistoryItem { Id = 1, AssetId = 7001, PricePages = 900, Status = "COMPLETED" } }));
            public void LoadPaymentMethods(Action<NotebookPaymentMethodResult> onSuccess, Action<NotebookApiError> onError) => onSuccess?.Invoke(new NotebookPaymentMethodResult(new[] { "Dotshelf Pay" }));
            public void LoadAssets(string category, string sort, int limit, Action<NotebookAssetListResult> onSuccess, Action<NotebookApiError> onError) => onSuccess?.Invoke(new NotebookAssetListResult(assets.Take(Math.Max(1, limit)).ToArray()));
            public void LoadAssetInventory(Action<NotebookAssetInventoryResult> onSuccess, Action<NotebookApiError> onError) => onSuccess?.Invoke(new NotebookAssetInventoryResult(assets.Where(asset => asset.Owned).ToArray()));
            public void LoadAssetDownloadManifest(long assetId, Action<NotebookAssetDownloadManifestResult> onSuccess, Action<NotebookApiError> onError) => onSuccess?.Invoke(new NotebookAssetDownloadManifestResult { AssetId = assetId });
            public void LoadSpaceAssetDownloadManifests(long spaceId, Action<NotebookSpaceAssetManifestListResult> onSuccess, Action<NotebookApiError> onError) => onSuccess?.Invoke(new NotebookSpaceAssetManifestListResult(Array.Empty<NotebookAssetDownloadManifestResult>()) { SpaceId = spaceId });
            public void WishAsset(long assetId, bool wished, Action<NotebookToggleResult> onSuccess, Action<NotebookApiError> onError) => onSuccess?.Invoke(new NotebookToggleResult { Active = wished });
            public void PurchaseAsset(long assetId, Action<NotebookAssetPurchaseResult> onSuccess, Action<NotebookApiError> onError) => onSuccess?.Invoke(new NotebookAssetPurchaseResult { AssetId = assetId, InventoryId = assetId + 1000, PricePages = 900 });
            public void LoadSpaces(int limit, Action<NotebookSpaceListResult> onSuccess, Action<NotebookApiError> onError) => LoadSpaces(null, null, limit, onSuccess, onError);

            public void LoadSpaces(string keyword, string cursor, int limit, Action<NotebookSpaceListResult> onSuccess, Action<NotebookApiError> onError)
            {
                var normalized = string.IsNullOrWhiteSpace(keyword) ? string.Empty : keyword.Trim();
                var result = spaces
                    .Where(space => string.IsNullOrEmpty(normalized) || Contains(space.OwnerName, normalized))
                    .Take(Math.Max(1, limit))
                    .Select(CloneSpace)
                    .ToArray();
                foreach (var space in result)
                {
                    space.Following = followed.Contains(space.OwnerUserId);
                    space.Liked = likedSpaces.Contains(space.Id);
                }

                onSuccess?.Invoke(new NotebookSpaceListResult(result));
            }

            public void UploadMySpaceThumbnail(byte[] imageBytes, string contentType, Action<NotebookSpaceThumbnailUploadResult> onSuccess, Action<NotebookApiError> onError) => onSuccess?.Invoke(new NotebookSpaceThumbnailUploadResult { SpaceId = 3004, ThumbnailKey = "spaces/3004/thumbnail/mock.jpg", LayoutVersion = 1 });

            public void LoadDefaultShelfTarget(Action<NotebookShelfTarget> onSuccess, Action<NotebookApiError> onError) => onSuccess?.Invoke(new NotebookShelfTarget { BookshelfId = 1, SectionId = 1, SlotNo = 1 });
            public void LikeSpace(long spaceId, bool liked, Action<NotebookToggleResult> onSuccess, Action<NotebookApiError> onError)
            {
                if (liked)
                {
                    likedSpaces.Add(spaceId);
                }
                else
                {
                    likedSpaces.Remove(spaceId);
                }

                onSuccess?.Invoke(new NotebookToggleResult { Active = liked, Count = 100 });
            }

            public void FollowUser(long userId, bool following, Action<NotebookToggleResult> onSuccess, Action<NotebookApiError> onError)
            {
                if (following)
                {
                    followed.Add(userId);
                }
                else
                {
                    followed.Remove(userId);
                }

                onSuccess?.Invoke(new NotebookToggleResult { Active = following, Count = following ? 1281 : 1280 });
            }

            public void LoadUserFollowers(long userId, string cursor, int limit, Action<NotebookUserListResult> onSuccess, Action<NotebookApiError> onError) => onSuccess?.Invoke(new NotebookUserListResult(BuildUsers(userId, "팔로워", limit), null));
            public void LoadUserFollowings(long userId, string cursor, int limit, Action<NotebookUserListResult> onSuccess, Action<NotebookApiError> onError) => onSuccess?.Invoke(new NotebookUserListResult(BuildUsers(userId, "팔로잉", limit), null));
            public void LoadBookDetail(long bookId, Action<NotebookBookSearchItem> onSuccess, Action<NotebookApiError> onError) => onSuccess?.Invoke(new NotebookBookSearchItem
            {
                Id = bookId,
                Source = "LOCAL",
                BookId = bookId.ToString(),
                Title = $"Validation Book {bookId}",
                Author = "Validation Author",
                Publisher = "Validation Publisher",
                Description = "Validation book detail fixture.",
                AvgRating = 4.5m,
                ReviewCount = 12,
                ViewCount = 120,
                PricePages = 0,
                Rank = 1,
                Owned = true,
                LikeCount = 8,
                PurchaseCount = 3,
                RecordCount = 5,
                PlacementCount = 1
            });
            public void LoadBookReviews(long bookId, Action<NotebookReviewListResult> onSuccess, Action<NotebookApiError> onError) => onSuccess?.Invoke(new NotebookReviewListResult(new[] { new NotebookReviewItem { Id = 1, Author = "reader", Rating = 4.8f, Content = "리뷰 영역은 짧은 내용과 긴 내용을 모두 안정적으로 표시해야 합니다." } }));
            public void CreateBookReview(long bookId, float rating, string content, Action<NotebookReviewItem> onSuccess, Action<NotebookApiError> onError) => onSuccess?.Invoke(new NotebookReviewItem { Id = 99, Author = "me", Rating = rating, Content = content });
            public void SetUserBookFavorite(long userBookId, bool favorite, Action<NotebookToggleResult> onSuccess, Action<NotebookApiError> onError) => onSuccess?.Invoke(new NotebookToggleResult { Active = favorite });

            private static List<NotebookSpaceItem> BuildSpaces()
            {
                return new List<NotebookSpaceItem>
                {
                    new() { Id = 3001, OwnerUserId = 201, OwnerName = "하린", SpaceName = "햇살이 머무는 창가의 책방", Mood = "WARM LIBRARY", Description = "큰 창과 조명, 식물을 함께 배치한 공간입니다.", FollowerCount = 1240, LikeCount = 842, VisitCount = 5820, Following = true },
                    new() { Id = 3002, OwnerUserId = 202, OwnerName = "도윤", SpaceName = "밤의 기록실", Mood = "FOCUS ROOM", Description = "긴 독서 시간을 위한 낮은 조도와 정돈된 책상.", FollowerCount = 980, LikeCount = 790, VisitCount = 4310 },
                    new() { Id = 3003, OwnerUserId = 203, OwnerName = "서아", SpaceName = "창가의 북라운지", Mood = "BRIGHT REST", Description = "낮은 소파와 책장을 중심으로 만든 휴식형 공간.", FollowerCount = 1534, LikeCount = 1020, VisitCount = 7244, Following = true },
                    new() { Id = 3004, OwnerUserId = 1, OwnerName = "나", SpaceName = "나의 공간", Mood = "MY SPACE", Description = "자기 공간 팔로우 버튼 숨김 검증용.", FollowerCount = 32, LikeCount = 12, VisitCount = 160, IsOwnSpace = true }
                };
            }

            private static List<NotebookAssetItem> BuildAssets()
            {
                return Enumerable.Range(1, 12).Select(index => new NotebookAssetItem
                {
                    Id = 7000 + index,
                    Code = $"asset-{index}",
                    Name = index % 2 == 0 ? "푸른 조명 세트" : "원목 책상 오브젝트",
                    Category = index % 2 == 0 ? "조명" : "가구",
                    CategoryCode = index % 2 == 0 ? "LIGHT" : "FURNITURE",
                    Tier = index % 3 == 0 ? "PREMIUM" : "BASIC",
                    AssetType = "DECOR",
                    PricePages = index % 4 == 0 ? 0 : 600 + index * 80,
                    Popularity = 1200 - index * 20,
                    Owned = index == 1,
                    Wished = index == 2
                }).ToList();
            }

            private static IReadOnlyList<NotebookUserItem> BuildUsers(long ownerId, string prefix, int limit)
            {
                return Enumerable.Range(1, Math.Min(Math.Max(1, limit), 8)).Select(index => new NotebookUserItem
                {
                    Id = ownerId * 100 + index,
                    Nickname = $"{prefix}{index}_긴닉네임검증",
                    Mutual = index % 3 == 0,
                    Following = index % 2 == 0,
                    Blocked = false
                }).ToArray();
            }

            private static NotebookSpaceItem CloneSpace(NotebookSpaceItem source)
            {
                return new NotebookSpaceItem
                {
                    Id = source.Id,
                    OwnerUserId = source.OwnerUserId,
                    OwnerName = source.OwnerName,
                    OwnerProfileImageUrl = source.OwnerProfileImageUrl,
                    SpaceName = source.SpaceName,
                    Mood = source.Mood,
                    Description = source.Description,
                    ThumbnailUrl = source.ThumbnailUrl,
                    FollowerCount = source.FollowerCount,
                    LikeCount = source.LikeCount,
                    VisitCount = source.VisitCount,
                    Following = source.Following,
                    Liked = source.Liked,
                    IsOwnSpace = source.IsOwnSpace
                };
            }
        }

        private static bool Contains(string source, string value)
        {
            return !string.IsNullOrWhiteSpace(source) &&
                   !string.IsNullOrWhiteSpace(value) &&
                   source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
