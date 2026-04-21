using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BookshelfPorting.Runtime
{
    public class BookshelfGenerator : MonoBehaviour
    {
        private const int BookshelfVariantCount = 3;
        private const float BookshelfAnchorZ = 1.285f;
        private const float BookshelfFadeDuration = 0.22f;
        private const string HamhamAssetPath = "Assets/Meshy_AI_Hamham_0416082404_texture.glb";
        private const string QABooksBaseTexturePath = "Assets/QA_Books/Textures/BooksA_bc.tga";
        private const string QABooksNormalTexturePath = "Assets/QA_Books/Textures/BooksA_n.tga";
        private const string QABooksMetallicTexturePath = "Assets/QA_Books/Textures/BooksA_m_ao_g.tga";
        private const string QABooksAltBaseTexturePath = "Assets/QA_Books/Textures/Books_bc.tga";
        private const string QABooksAltNormalTexturePath = "Assets/QA_Books/Textures/Books_n.tga";
        private const string QABooksAltMetallicTexturePath = "Assets/QA_Books/Textures/Books_m_ao_g.tga";
        private static readonly Vector3 MainSceneNotebookStationLocalPosition = new Vector3(0.72f, -1.18f, 0f);
        private static readonly Vector3 FallbackNotebookStationLocalPosition = new Vector3(-1.275f, 0f, 0.12f);

        [Header("References")]
        [SerializeField] private BookshelfState state = null;
        [SerializeField] private MaterialFactory materialFactory = null;
        [SerializeField] private Transform roomRoot = null;
        [SerializeField] private Transform bookshelfRoot = null;
        [SerializeField] private Transform booksRoot = null;
        [SerializeField] private GameObject hamhamModelAsset = null;
        [SerializeField] private Texture2D qaBooksBaseMap = null;
        [SerializeField] private Texture2D qaBooksNormalMap = null;
        [SerializeField] private Texture2D qaBooksMaskMap = null;
        [SerializeField] private Texture2D qaBooksAltBaseMap = null;
        [SerializeField] private Texture2D qaBooksAltNormalMap = null;
        [SerializeField] private Texture2D qaBooksAltMaskMap = null;

        [Header("Bookshelf Dimensions")]
        [SerializeField] private float width = 1.60f;
        [SerializeField] private float height = 1.60f;
        [SerializeField] private float depth = 0.25f;
        [SerializeField] private int rows = 4;
        [SerializeField] private int columns = 4;
        [SerializeField] private float boardThickness = 0.025f;
        [SerializeField] private float dividerThickness = 0.02f;
        [SerializeField] private float backPanelThickness = 0.012f;
        [SerializeField] private float booksForwardZ = 0.12f;

        [Header("Defaults")]
        [SerializeField] private int booksPerSection = 3;
        [SerializeField] private Vector2 thicknessRange = new Vector2(0.025f, 0.045f);
        [SerializeField] private Vector2 heightRange = new Vector2(0.20f, 0.27f);
        [SerializeField] private float bookDepth = 0.16f;
        [SerializeField] private int randomSeed = 7;

        private readonly List<GameObject> generatedGeometry = new List<GameObject>();
        private readonly List<ShelfSection> bookshelfSections = new List<ShelfSection>();
        private readonly List<GameObject> qaBookPrefabs = new List<GameObject>();
        private Material qaBooksMaterial;
        private Material qaBooksAltMaterial;
        private Coroutine bookshelfSwitchCoroutine;
        private int activeBookshelfIndex;

        public Transform BooksRoot => booksRoot;
        public bool IsBookshelfSwitching => bookshelfSwitchCoroutine != null;
        public int BookshelfCount => BookshelfVariantCount;
        public int ActiveBookshelfIndex => activeBookshelfIndex;
        public event System.Action BookshelfSwitchStateChanged;

        public void Configure(BookshelfState bookshelfState, MaterialFactory factory)
        {
            state = bookshelfState;
            materialFactory = factory;
        }

        private void Start()
        {
            if (state != null && state.Sections.Count > 0 && booksRoot != null && booksRoot.childCount > 0)
            {
                return;
            }

            Generate();
        }

        [ContextMenu("Generate")]
        public void Generate()
        {
            EnsureRoots();
            ClearGenerated();
            BuildRoom();
            BuildBookshelf();
            NormalizeNotebookStationPlacement();
        }

        public void NormalizeNotebookStationPlacement()
        {
            var resolvedRoomRoot = ResolveRoomRoot();
            if (resolvedRoomRoot == null)
            {
                return;
            }

            var guestbook = resolvedRoomRoot.Find("GuestbookBoard");
            if (guestbook == null)
            {
                return;
            }

            var notebookStation = FindPrimaryNotebookStation(resolvedRoomRoot);
            if (notebookStation == null)
            {
                return;
            }

            notebookStation.SetParent(guestbook, false);
            notebookStation.localPosition = MainSceneNotebookStationLocalPosition;
            notebookStation.localRotation = Quaternion.identity;
            notebookStation.localScale = Vector3.one;
        }

        private void EnsureRoots()
        {
            if (roomRoot == null)
            {
                roomRoot = new GameObject("RoomRoot").transform;
                roomRoot.SetParent(transform, false);
            }

            if (bookshelfRoot == null)
            {
                bookshelfRoot = new GameObject("BookshelfRoot").transform;
                bookshelfRoot.SetParent(transform, false);
            }

            if (booksRoot == null)
            {
                booksRoot = new GameObject("BooksRoot").transform;
                booksRoot.SetParent(bookshelfRoot, false);
            }
        }

        private void ClearGenerated()
        {
            if (bookshelfSwitchCoroutine != null)
            {
                StopCoroutine(bookshelfSwitchCoroutine);
                bookshelfSwitchCoroutine = null;
            }

            for (var i = generatedGeometry.Count - 1; i >= 0; i--)
            {
                if (generatedGeometry[i] != null)
                {
                    DestroyImmediate(generatedGeometry[i]);
                }
            }

            generatedGeometry.Clear();
            bookshelfSections.Clear();
            activeBookshelfIndex = 0;

            ClearRoomChildren();
            ClearBookshelfShellChildren();
            DestroyNamedChild(transform, "HamhamDisplay");

            DestroyBookEntitiesUnderBooksRoot();
        }

        private void ClearRoomChildren()
        {
            if (roomRoot == null)
            {
                return;
            }

            ClearChildren(roomRoot, null);
        }

        private void ClearBookshelfShellChildren()
        {
            if (bookshelfRoot == null)
            {
                return;
            }

            ClearChildren(bookshelfRoot, booksRoot);
        }

        private static void ClearChildren(Transform parent, Transform except)
        {
            var children = new List<GameObject>();
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (except != null && child == except)
                {
                    continue;
                }

                children.Add(child.gameObject);
            }

            for (var i = 0; i < children.Count; i++)
            {
                DestroySpawnedObject(children[i]);
            }
        }

        private Transform ResolveRoomRoot()
        {
            if (roomRoot != null)
            {
                return roomRoot;
            }

            var directRoot = transform.Find("RoomRoot");
            if (directRoot != null)
            {
                roomRoot = directRoot;
                return roomRoot;
            }

            var sceneRoot = GameObject.Find("RoomRoot");
            if (sceneRoot != null)
            {
                roomRoot = sceneRoot.transform;
                return roomRoot;
            }

            return null;
        }

        private static void DestroyNamedChild(Transform parent, string childName)
        {
            if (parent == null)
            {
                return;
            }

            var child = parent.Find(childName);
            if (child != null)
            {
                DestroySpawnedObject(child.gameObject);
            }
        }

        private void BuildRoom()
        {
            CreateBox("Floor", roomRoot, new Vector3(0f, -0.05f, 0f), new Vector3(4.2f, 0.1f, 4.2f), materialFactory.GetFloorMaterial());
            CreateBox("BackWall", roomRoot, new Vector3(0f, 1.4f, 1.45f), new Vector3(4.2f, 2.8f, 0.08f), materialFactory.GetWallMaterial());
            CreateBox("LeftWall", roomRoot, new Vector3(-2.05f, 1.4f, 0f), new Vector3(0.08f, 2.8f, 4.2f), materialFactory.GetWallMaterial());
            CreateBox("RightWall", roomRoot, new Vector3(2.05f, 1.4f, 0f), new Vector3(0.08f, 2.8f, 4.2f), materialFactory.GetWallMaterial());

            BuildGuestbookBoard();
            BuildNotebookStation();
        }

        private void BuildBookshelf()
        {
            bookshelfRoot.localPosition = new Vector3(0f, 0f, BookshelfAnchorZ);
            bookshelfRoot.localRotation = Quaternion.identity;
            var wood = materialFactory.GetBookshelfWoodMaterial();

            CreateBox("LeftPanel", bookshelfRoot, new Vector3(-width * 0.5f + boardThickness * 0.5f, height * 0.5f, 0f), new Vector3(boardThickness, height, depth), wood);
            CreateBox("RightPanel", bookshelfRoot, new Vector3(width * 0.5f - boardThickness * 0.5f, height * 0.5f, 0f), new Vector3(boardThickness, height, depth), wood);
            CreateBox("BottomBoard", bookshelfRoot, new Vector3(0f, boardThickness * 0.5f, 0f), new Vector3(width, boardThickness, depth), wood);
            CreateBox("TopBoard", bookshelfRoot, new Vector3(0f, height - boardThickness * 0.5f, 0f), new Vector3(width, boardThickness, depth), wood);
            CreateBox("BackPanel", bookshelfRoot, new Vector3(0f, height * 0.5f, depth * 0.5f - backPanelThickness * 0.5f), new Vector3(width, height, backPanelThickness), wood);
            CreateBookshelfFocusHitArea();

            var usableWidth = width - boardThickness * 2f;
            var usableHeight = height - boardThickness * 2f;
            var sectionWidth = (usableWidth - dividerThickness * (columns - 1)) / columns;
            var sectionHeight = (usableHeight - boardThickness * (rows - 1)) / rows;

            for (var row = 1; row < rows; row++)
            {
                var y = boardThickness + row * sectionHeight + (row - 0.5f) * boardThickness;
                CreateBox($"Shelf_{row}", bookshelfRoot, new Vector3(0f, y, 0f), new Vector3(width - boardThickness * 2f, boardThickness, depth), wood);
            }

            for (var column = 1; column < columns; column++)
            {
                var x = -usableWidth * 0.5f + column * sectionWidth + (column - 0.5f) * dividerThickness;
                CreateBox($"Divider_{column}", bookshelfRoot, new Vector3(x, height * 0.5f, 0f), new Vector3(dividerThickness, height - boardThickness * 2f, depth), wood);
            }

            bookshelfSections.Clear();
            for (var row = 0; row < rows; row++)
            {
                var bottomY = boardThickness + row * (sectionHeight + boardThickness);
                var topY = bottomY + sectionHeight;
                for (var column = 0; column < columns; column++)
                {
                    var left = -usableWidth * 0.5f + column * (sectionWidth + dividerThickness);
                    var right = left + sectionWidth;
                    bookshelfSections.Add(new ShelfSection(row, column, left, right, bottomY, topY - bottomY, booksForwardZ));
                }
            }

            state.ResetSections(bookshelfSections);
            PopulateBookshelfVariant(activeBookshelfIndex);
            SetBookshelfAlpha(1f);
            BuildHamhamDisplay();
        }

        private void PopulateBookshelfVariant(int variantIndex)
        {
            ClearBookshelfBooks();
            SpawnBooks(bookshelfSections, variantIndex);
            state.LayoutAll(state, false);
        }

        private void SpawnBooks(List<ShelfSection> sections, int variantIndex)
        {
            Random.InitState(randomSeed + variantIndex * 137);
            var prefabs = GetQaBookPrefabs();

            for (var i = 0; i < sections.Count; i++)
            {
                var section = sections[i];
                for (var j = 0; j < booksPerSection; j++)
                {
                    BookEntity book;
                    if (prefabs.Count > 0)
                    {
                        var prefabIndex = (variantIndex * 11 + i * booksPerSection + j) % prefabs.Count;
                        book = CreatePrefabBook(prefabs[prefabIndex], variantIndex, i, j);
                    }
                    else
                    {
                        book = CreateProceduralBook(variantIndex, i, j);
                    }

                    state.AddBookToSection(book, section);
                }
            }
        }

        private BookEntity CreateProceduralBook(int variantIndex, int sectionIndex, int bookIndex)
        {
            var bookObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bookObject.name = $"Shelf{variantIndex}_Book_{sectionIndex}_{bookIndex}";
            bookObject.transform.SetParent(booksRoot, false);

            var thickness = Random.Range(thicknessRange.x, thicknessRange.y);
            var bookHeight = Random.Range(heightRange.x, heightRange.y);
            bookObject.transform.localScale = new Vector3(thickness, bookHeight, bookDepth);

            var renderer = bookObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = GetBookVisualMaterial();

            var entity = bookObject.AddComponent<BookEntity>();
            entity.tintRenderers = new[] { renderer };
            entity.Initialize(bookObject.name, thickness, bookHeight, bookDepth, Color.white);
            return entity;
        }

        private Material GetBookVisualMaterial()
        {
            if (qaBooksMaterial != null)
            {
                return qaBooksMaterial;
            }

#if UNITY_EDITOR
            if (qaBooksBaseMap == null)
            {
                qaBooksBaseMap = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(QABooksBaseTexturePath);
            }

            if (qaBooksNormalMap == null)
            {
                qaBooksNormalMap = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(QABooksNormalTexturePath);
            }

            if (qaBooksMaskMap == null)
            {
                qaBooksMaskMap = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(QABooksMetallicTexturePath);
            }
#endif

            if (qaBooksBaseMap != null)
            {
                qaBooksMaterial = materialFactory.CreateBookMaterial(qaBooksBaseMap, qaBooksNormalMap, Color.white, 0.28f);
                if (qaBooksMaskMap != null)
                {
                    qaBooksMaterial.EnableKeyword("_METALLICSPECGLOSSMAP");
                    qaBooksMaterial.SetTexture("_MetallicGlossMap", qaBooksMaskMap);
                    qaBooksMaterial.SetTexture("_OcclusionMap", qaBooksMaskMap);
                }
                return qaBooksMaterial;
            }

            return materialFactory.CreateBookMaterial(Random.ColorHSV(0f, 1f, 0.45f, 0.9f, 0.45f, 0.95f));
        }

        private Material GetAlternateBookVisualMaterial()
        {
            if (qaBooksAltMaterial != null)
            {
                return qaBooksAltMaterial;
            }

#if UNITY_EDITOR
            if (qaBooksAltBaseMap == null)
            {
                qaBooksAltBaseMap = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(QABooksAltBaseTexturePath);
            }

            if (qaBooksAltNormalMap == null)
            {
                qaBooksAltNormalMap = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(QABooksAltNormalTexturePath);
            }

            if (qaBooksAltMaskMap == null)
            {
                qaBooksAltMaskMap = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(QABooksAltMetallicTexturePath);
            }
#endif

            if (qaBooksAltBaseMap != null)
            {
                qaBooksAltMaterial = materialFactory.CreateBookMaterial(qaBooksAltBaseMap, qaBooksAltNormalMap, Color.white, 0.28f);
                if (qaBooksAltMaskMap != null)
                {
                    qaBooksAltMaterial.EnableKeyword("_METALLICSPECGLOSSMAP");
                    qaBooksAltMaterial.SetTexture("_MetallicGlossMap", qaBooksAltMaskMap);
                    qaBooksAltMaterial.SetTexture("_OcclusionMap", qaBooksAltMaskMap);
                }
                return qaBooksAltMaterial;
            }

            return GetBookVisualMaterial();
        }

        private List<GameObject> GetQaBookPrefabs()
        {
            if (qaBookPrefabs.Count > 0)
            {
                return qaBookPrefabs;
            }

#if UNITY_EDITOR
            var prefabGuids = UnityEditor.AssetDatabase.FindAssets("t:Prefab Book_", new[] { "Assets/QA_Books/Prefabs" });
            for (var i = 0; i < prefabGuids.Length; i++)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (!path.Contains("/Book_"))
                {
                    continue;
                }

                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    qaBookPrefabs.Add(prefab);
                }
            }
#endif

            return qaBookPrefabs;
        }

        private BookEntity CreatePrefabBook(GameObject prefab, int variantIndex, int sectionIndex, int bookIndex)
        {
            var root = new GameObject($"Shelf{variantIndex}_{prefab.name}_{sectionIndex}_{bookIndex}");
            root.transform.SetParent(booksRoot, false);

            var visual = Instantiate(prefab, root.transform);
            visual.name = prefab.name;
            SetDynamicHierarchy(visual.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            visual.transform.localScale = Vector3.one;

            RemoveChildColliders(visual.transform);

            var renderers = root.GetComponentsInChildren<MeshRenderer>();
            ApplyQaBookMaterials(renderers);
            CenterChildVisual(root.transform);

            var localBounds = CalculateLocalBounds(root.transform, root.GetComponentsInChildren<Renderer>());
            var collider = root.AddComponent<BoxCollider>();
            collider.center = localBounds.center;
            collider.size = localBounds.size;

            var entity = root.AddComponent<BookEntity>();
            entity.tintRenderers = renderers;
            entity.Initialize(root.name, localBounds.size.x, localBounds.size.y, localBounds.size.z, Color.white);
            return entity;
        }

        private static void SetDynamicHierarchy(Transform root)
        {
            if (root == null)
            {
                return;
            }

            root.gameObject.isStatic = false;
            for (var i = 0; i < root.childCount; i++)
            {
                SetDynamicHierarchy(root.GetChild(i));
            }
        }

        private void ApplyQaBookMaterials(MeshRenderer[] renderers)
        {
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || renderers[i].sharedMaterial == null)
                {
                    continue;
                }

                var sourceName = renderers[i].sharedMaterial.name;
                renderers[i].sharedMaterial = sourceName.Contains("BooksA") ? GetBookVisualMaterial() : GetAlternateBookVisualMaterial();
            }
        }

        private static void RemoveChildColliders(Transform root)
        {
            var colliders = root.GetComponentsInChildren<Collider>();
            for (var i = 0; i < colliders.Length; i++)
            {
                DestroyImmediate(colliders[i]);
            }
        }

        private static void CenterChildVisual(Transform root)
        {
            var bounds = CalculateLocalBounds(root, root.GetComponentsInChildren<Renderer>());
            for (var i = 0; i < root.childCount; i++)
            {
                root.GetChild(i).localPosition -= bounds.center;
            }
        }

        private static Bounds CalculateLocalBounds(Transform root, Renderer[] renderers)
        {
            var hasBounds = false;
            var bounds = new Bounds(Vector3.zero, Vector3.zero);

            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    continue;
                }

                var rendererBounds = renderers[i].bounds;
                var min = root.InverseTransformPoint(rendererBounds.min);
                var max = root.InverseTransformPoint(rendererBounds.max);

                if (!hasBounds)
                {
                    bounds = new Bounds((min + max) * 0.5f, max - min);
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(min);
                bounds.Encapsulate(max);
            }

            if (!hasBounds)
            {
                bounds = new Bounds(Vector3.zero, new Vector3(0.04f, 0.22f, 0.16f));
            }

            return bounds;
        }

        private void CreateBox(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = localScale;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
            DestroyImmediate(box.GetComponent<Collider>());
            generatedGeometry.Add(box);
        }

        private void BuildHamhamDisplay()
        {
            var hamhamAsset = ResolveHamhamAsset();
            if (hamhamAsset == null)
            {
                return;
            }

            var hamhamRoot = new GameObject("HamhamDisplay");
            hamhamRoot.transform.SetParent(transform, false);
            hamhamRoot.transform.localPosition = bookshelfRoot.localPosition + new Vector3(width * 0.5f + 0.42f, 0f, -0.05f);
            hamhamRoot.transform.localRotation = Quaternion.Euler(0f, 152f, 0f);
            hamhamRoot.transform.localScale = Vector3.one * 0.42f;
            generatedGeometry.Add(hamhamRoot);

            var instance = Instantiate(hamhamAsset, hamhamRoot.transform);
            instance.name = hamhamAsset.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            AlignModelToFloor(hamhamRoot.transform, instance.transform);
        }

        private GameObject ResolveHamhamAsset()
        {
            if (hamhamModelAsset != null)
            {
                return hamhamModelAsset;
            }

#if UNITY_EDITOR
            hamhamModelAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(HamhamAssetPath);
#endif

            return hamhamModelAsset;
        }

        private static void AlignModelToFloor(Transform root, Transform modelRoot)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
            {
                return;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            var minLocal = root.InverseTransformPoint(bounds.min);
            modelRoot.localPosition -= new Vector3(0f, minLocal.y, 0f);
        }

        public bool ShowPreviousBookshelf()
        {
            return BeginBookshelfSwitch(-1);
        }

        public bool ShowNextBookshelf()
        {
            return BeginBookshelfSwitch(1);
        }

        private bool BeginBookshelfSwitch(int direction)
        {
            if (direction == 0 ||
                IsBookshelfSwitching ||
                booksRoot == null ||
                bookshelfSections.Count == 0 ||
                BookshelfVariantCount <= 1)
            {
                return false;
            }

            var targetIndex = GetWrappedBookshelfIndex(activeBookshelfIndex + direction);
            if (targetIndex == activeBookshelfIndex)
            {
                return false;
            }

            bookshelfSwitchCoroutine = StartCoroutine(AnimateBookshelfSwitch(targetIndex));
            BookshelfSwitchStateChanged?.Invoke();
            return true;
        }

        private IEnumerator AnimateBookshelfSwitch(int targetIndex)
        {
            yield return AnimateBookshelfFade(1f, 0f);

            ClearBookshelfBooks();
            yield return null;

            activeBookshelfIndex = targetIndex;
            PopulateBookshelfVariant(activeBookshelfIndex);
            SetBookshelfAlpha(0f);

            yield return AnimateBookshelfFade(0f, 1f);

            bookshelfSwitchCoroutine = null;
            BookshelfSwitchStateChanged?.Invoke();
        }

        private IEnumerator AnimateBookshelfFade(float startAlpha, float endAlpha)
        {
            var elapsed = 0f;

            while (elapsed < BookshelfFadeDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, elapsed / BookshelfFadeDuration);
                SetBookshelfAlpha(Mathf.Lerp(startAlpha, endAlpha, t));
                yield return null;
            }

            SetBookshelfAlpha(endAlpha);
        }

        private void ClearBookshelfBooks()
        {
            if (state != null)
            {
                for (var i = 0; i < state.Sections.Count; i++)
                {
                    state.Sections[i].books.Clear();
                }
            }

            DestroyBookEntitiesUnderBooksRoot();
        }

        private void DestroyBookEntitiesUnderBooksRoot()
        {
            if (booksRoot == null)
            {
                return;
            }

            var bookChildren = new List<GameObject>();
            for (var i = 0; i < booksRoot.childCount; i++)
            {
                var child = booksRoot.GetChild(i);
                if (child.GetComponent<BookEntity>() != null)
                {
                    bookChildren.Add(child.gameObject);
                }
            }

            for (var i = 0; i < bookChildren.Count; i++)
            {
                DestroySpawnedObject(bookChildren[i]);
            }
        }

        private static void DestroySpawnedObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            Object.DestroyImmediate(target);
        }

        private static int GetWrappedBookshelfIndex(int index)
        {
            var wrapped = index % BookshelfVariantCount;
            return wrapped < 0 ? wrapped + BookshelfVariantCount : wrapped;
        }

        private void SetBookshelfAlpha(float alpha)
        {
            if (bookshelfRoot == null)
            {
                return;
            }

            var renderers = bookshelfRoot.GetComponentsInChildren<Renderer>(true);
            var processedMaterials = new HashSet<Material>();

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                var materials = renderer.sharedMaterials;
                for (var j = 0; j < materials.Length; j++)
                {
                    var material = materials[j];
                    if (material == null ||
                        material == materialFactory.GetGhostPreviewMaterial() ||
                        !processedMaterials.Add(material))
                    {
                        continue;
                    }

                    if (alpha < 0.999f)
                    {
                        ConfigureMaterialForFade(material);
                    }
                    else
                    {
                        RestoreMaterialFromFade(material);
                    }

                    var color = material.HasProperty("_BaseColor")
                        ? material.GetColor("_BaseColor")
                        : material.color;
                    color.a = alpha < 0.999f ? alpha : 1f;

                    if (material.HasProperty("_BaseColor"))
                    {
                        material.SetColor("_BaseColor", color);
                    }

                    material.color = color;
                }
            }
        }

        private static void ConfigureMaterialForFade(Material material)
        {
            if (material == null)
            {
                return;
            }

            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private static void RestoreMaterialFromFade(Material material)
        {
            if (material == null)
            {
                return;
            }

            material.SetFloat("_Surface", 0f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            material.SetFloat("_ZWrite", 1f);
            material.renderQueue = -1;
        }

        private void BuildGuestbookBoard()
        {
            var guestbook = new GameObject("GuestbookBoard");
            guestbook.transform.SetParent(roomRoot, false);
            guestbook.transform.localPosition = new Vector3(-1.995f, 1.18f, 0.12f);
            guestbook.transform.localRotation = Quaternion.identity;
            generatedGeometry.Add(guestbook);

            CreateBox("Frame", guestbook.transform, Vector3.zero, new Vector3(0.04f, 1.08f, 1.52f), materialFactory.GetWhiteboardFrameMaterial());
            CreateBox("Surface", guestbook.transform, new Vector3(0.012f, 0f, 0f), new Vector3(0.012f, 0.96f, 1.38f), materialFactory.GetWhiteboardSurfaceMaterial());

            var hitArea = new GameObject("GuestbookHitArea");
            hitArea.transform.SetParent(guestbook.transform, false);
            hitArea.transform.localPosition = new Vector3(0.08f, 0f, 0f);
            var collider = hitArea.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.2f, 1.0f, 1.4f);
            hitArea.AddComponent<GuestbookBoardHitArea>();
            generatedGeometry.Add(hitArea);
        }

        private void CreateBookshelfFocusHitArea()
        {
            var hitArea = new GameObject("BookshelfFocusHitArea");
            hitArea.transform.SetParent(bookshelfRoot, false);
            hitArea.transform.localPosition = new Vector3(0f, height * 0.5f, -depth * 0.5f - 0.05f);
            var collider = hitArea.AddComponent<BoxCollider>();
            collider.size = new Vector3(width + 0.10f, height + 0.10f, 0.18f);
            hitArea.AddComponent<BookshelfFocusHitArea>();
            generatedGeometry.Add(hitArea);
        }

        private void BuildNotebookStation()
        {
            var guestbook = roomRoot != null ? roomRoot.Find("GuestbookBoard") : null;
            var stationParent = guestbook != null ? guestbook : roomRoot;
            var notebookStation = new GameObject("NotebookStation");
            notebookStation.transform.SetParent(stationParent, false);
            notebookStation.transform.localPosition = guestbook != null
                ? MainSceneNotebookStationLocalPosition
                : FallbackNotebookStationLocalPosition;
            notebookStation.transform.localRotation = Quaternion.identity;
            generatedGeometry.Add(notebookStation);

            var wood = materialFactory.GetFurnitureWoodMaterial();
            var aluminum = materialFactory.CreateBookMaterial(new Color(0.72f, 0.73f, 0.71f));
            var darkGlass = materialFactory.CreateBookMaterial(new Color(0.015f, 0.018f, 0.022f));
            var screenGlow = materialFactory.CreateBookMaterial(new Color(0.10f, 0.16f, 0.24f));
            var keyboardTone = materialFactory.CreateBookMaterial(new Color(0.035f, 0.038f, 0.043f));
            var keyLetterTone = materialFactory.CreateBookMaterial(new Color(0.12f, 0.125f, 0.13f));
            var trackpadTone = materialFactory.CreateBookMaterial(new Color(0.60f, 0.61f, 0.59f));

            const float deskHeight = 0.38f;
            const float deskThickness = 0.06f;
            const float deskDepth = 0.54f;
            const float deskWidth = 0.86f;
            const float legThickness = 0.06f;

            var desk = new GameObject("Desk");
            desk.transform.SetParent(notebookStation.transform, false);
            desk.transform.localPosition = Vector3.zero;
            desk.transform.localRotation = Quaternion.identity;
            generatedGeometry.Add(desk);

            CreateBox("Desktop", desk.transform, new Vector3(0f, deskHeight, 0f), new Vector3(deskDepth, deskThickness, deskWidth), wood);
            CreateBox("DeskLegA", desk.transform, new Vector3(-(deskDepth * 0.5f - 0.11f), deskHeight * 0.5f, -(deskWidth * 0.5f - 0.10f)), new Vector3(legThickness, deskHeight, legThickness), wood);
            CreateBox("DeskLegB", desk.transform, new Vector3(deskDepth * 0.5f - 0.11f, deskHeight * 0.5f, -(deskWidth * 0.5f - 0.10f)), new Vector3(legThickness, deskHeight, legThickness), wood);
            CreateBox("DeskLegC", desk.transform, new Vector3(-(deskDepth * 0.5f - 0.11f), deskHeight * 0.5f, deskWidth * 0.5f - 0.10f), new Vector3(legThickness, deskHeight, legThickness), wood);
            CreateBox("DeskLegD", desk.transform, new Vector3(deskDepth * 0.5f - 0.11f, deskHeight * 0.5f, deskWidth * 0.5f - 0.10f), new Vector3(legThickness, deskHeight, legThickness), wood);

            var notebook = new GameObject("Notebook");
            notebook.transform.SetParent(desk.transform, false);
            notebook.transform.localPosition = new Vector3(0.10f, deskHeight + deskThickness * 0.5f + 0.009f, 0f);
            notebook.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            generatedGeometry.Add(notebook);

            CreateBox("UnibodyBase", notebook.transform, new Vector3(0f, 0f, 0f), new Vector3(0.50f, 0.018f, 0.32f), aluminum);
            CreateBox("FrontChamfer", notebook.transform, new Vector3(0f, 0.006f, 0.165f), new Vector3(0.45f, 0.008f, 0.012f), materialFactory.CreateBookMaterial(new Color(0.82f, 0.83f, 0.80f)));
            CreateBox("KeyboardWell", notebook.transform, new Vector3(0f, 0.010f, -0.045f), new Vector3(0.36f, 0.006f, 0.145f), keyboardTone);
            BuildKeyboardKeys(notebook.transform, keyLetterTone);
            CreateBox("Trackpad", notebook.transform, new Vector3(0f, 0.012f, 0.095f), new Vector3(0.15f, 0.004f, 0.085f), trackpadTone);
            CreateBox("TrackpadInset", notebook.transform, new Vector3(0f, 0.0145f, 0.095f), new Vector3(0.13f, 0.002f, 0.067f), materialFactory.CreateBookMaterial(new Color(0.68f, 0.69f, 0.67f)));
            CreateBox("HingeBar", notebook.transform, new Vector3(0f, 0.021f, -0.154f), new Vector3(0.43f, 0.020f, 0.026f), aluminum);
            CreateBox("HingeShadowGap", notebook.transform, new Vector3(0f, 0.020f, -0.139f), new Vector3(0.39f, 0.006f, 0.008f), keyboardTone);

            var screen = new GameObject("Screen");
            screen.transform.SetParent(notebook.transform, false);
            screen.transform.localPosition = new Vector3(0f, 0.114f, -0.154f);
            screen.transform.localRotation = Quaternion.Euler(-99.5f, 0f, 0f);
            generatedGeometry.Add(screen);

            CreateBox("ScreenLid", screen.transform, Vector3.zero, new Vector3(0.50f, 0.018f, 0.31f), aluminum);
            CreateBox("LowerBezelLip", screen.transform, new Vector3(0f, -0.0095f, 0.143f), new Vector3(0.478f, 0.009f, 0.015f), aluminum);
            CreateBox("BlackBezel", screen.transform, new Vector3(0f, -0.0105f, 0f), new Vector3(0.474f, 0.008f, 0.270f), darkGlass);
            CreateBox("DisplayPanel", screen.transform, new Vector3(0f, -0.0135f, 0f), new Vector3(0.464f, 0.006f, 0.260f), screenGlow);
            CreateBox("CameraDot", screen.transform, new Vector3(0f, -0.0185f, -0.127f), new Vector3(0.012f, 0.004f, 0.012f), materialFactory.CreateBookMaterial(new Color(0.02f, 0.025f, 0.03f)));
            CreateReferenceTransform(screen.transform, "ScreenCenter", new Vector3(0f, 0.068f, 0.014f));
            CreateReferenceTransform(screen.transform, "CameraFocusPoint", new Vector3(0f, -0.58f, 0.024f));

            var hitArea = new GameObject("NotebookHitArea");
            hitArea.transform.SetParent(notebook.transform, false);
            hitArea.transform.localPosition = new Vector3(0f, 0.13f, -0.03f);
            hitArea.transform.localRotation = Quaternion.identity;
            var collider = hitArea.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.58f, 0.30f, 0.36f);
            hitArea.AddComponent<NotebookHitArea>();
            generatedGeometry.Add(hitArea);

            notebook.AddComponent<NotebookScreenController>();
        }

        private static Transform FindPrimaryNotebookStation(Transform root)
        {
            var candidates = new List<Transform>();
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == "NotebookStation")
                {
                    candidates.Add(transforms[i]);
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            var primary = candidates[0];
            for (var i = 0; i < candidates.Count; i++)
            {
                var parent = candidates[i].parent;
                if (parent != null && parent.name == "GuestbookBoard")
                {
                    primary = candidates[i];
                    break;
                }
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                if (candidates[i] != primary)
                {
                    DestroySpawnedObject(candidates[i].gameObject);
                }
            }

            return primary;
        }

        private static void CreateReferenceTransform(Transform parent, string name, Vector3 localPosition)
        {
            var reference = new GameObject(name).transform;
            reference.SetParent(parent, false);
            reference.localPosition = localPosition;
            reference.localRotation = Quaternion.identity;
            reference.localScale = Vector3.one;
        }

        private void BuildKeyboardKeys(Transform notebook, Material keyMaterial)
        {
            const int columns = 10;
            const int rows = 4;
            const float keyWidth = 0.026f;
            const float keyDepth = 0.018f;
            const float spacingX = 0.032f;
            const float spacingZ = 0.026f;
            var startX = -spacingX * (columns - 1) * 0.5f;
            var startZ = -0.105f;

            for (var row = 0; row < rows; row++)
            {
                var rowOffset = row == 1 ? 0.008f : row == 2 ? 0.015f : 0f;
                var keysInRow = row == 3 ? 7 : columns;
                var rowStartX = startX + rowOffset + (columns - keysInRow) * spacingX * 0.5f;

                for (var column = 0; column < keysInRow; column++)
                {
                    var keyScale = new Vector3(keyWidth, 0.004f, keyDepth);
                    if (row == 3 && column == 3)
                    {
                        keyScale.x = 0.075f;
                    }

                    var keyPosition = new Vector3(rowStartX + column * spacingX, 0.017f, startZ + row * spacingZ);
                    CreateBox($"Key{row}_{column}", notebook, keyPosition, keyScale, keyMaterial);
                }
            }
        }
    }
}
