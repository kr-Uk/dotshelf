using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UiButton = UnityEngine.UI.Button;
using UiDropdown = UnityEngine.UI.Dropdown;
using UiInputField = UnityEngine.UI.InputField;
using UiScrollbar = UnityEngine.UI.Scrollbar;
using UiSlider = UnityEngine.UI.Slider;
using UiToggle = UnityEngine.UI.Toggle;
using UitButton = UnityEngine.UIElements.Button;
using UitScroller = UnityEngine.UIElements.Scroller;
using UitSlider = UnityEngine.UIElements.Slider;
using UitToggle = UnityEngine.UIElements.Toggle;

namespace App.Bootstrap.Cursors
{
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class GlobalMouseCursorController : MonoBehaviour
    {
        private const string ResourceRoot = "Cursors/";
        private static readonly object DefaultOverrideOwner = new object();

        private static GlobalMouseCursorController instance;
        private static long nextOverrideOrder;

        private readonly Dictionary<MouseCursorKind, CursorDefinition> cursors = new Dictionary<MouseCursorKind, CursorDefinition>();
        private readonly Dictionary<object, CursorOverride> overrides = new Dictionary<object, CursorOverride>();
        private readonly List<object> staleOverrideOwners = new List<object>();
        private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

        private PointerEventData pointerEventData;
        private EventSystem pointerEventSystem;
        private bool hasAppliedCursor;
        private MouseCursorKind appliedCursor;

        private struct CursorDefinition
        {
            public CursorDefinition(Texture2D texture, Vector2 hotspot)
            {
                Texture = texture;
                Hotspot = hotspot;
            }

            public Texture2D Texture { get; }
            public Vector2 Hotspot { get; }
        }

        private struct CursorOverride
        {
            public CursorOverride(MouseCursorKind cursor, int priority, long order)
            {
                Cursor = cursor;
                Priority = priority;
                Order = order;
            }

            public MouseCursorKind Cursor { get; }
            public int Priority { get; }
            public long Order { get; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoBootstrap()
        {
            EnsureInstance();
        }

        public static void SetOverride(object owner, MouseCursorKind cursor, int priority)
        {
            var controller = EnsureInstance();
            controller.overrides[owner ?? DefaultOverrideOwner] = new CursorOverride(cursor, priority, ++nextOverrideOrder);
        }

        public static void ClearOverride(object owner)
        {
            var controller = instance;
            if (controller == null)
            {
                return;
            }

            controller.overrides.Remove(owner ?? DefaultOverrideOwner);
        }

        private static GlobalMouseCursorController EnsureInstance()
        {
            if (instance != null)
            {
                return instance;
            }

            var existing = UnityEngine.Object.FindFirstObjectByType<GlobalMouseCursorController>(FindObjectsInactive.Include);
            if (existing != null)
            {
                instance = existing;
                return existing;
            }

            var host = new GameObject(nameof(GlobalMouseCursorController));
            DontDestroyOnLoad(host);
            return host.AddComponent<GlobalMouseCursorController>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            LoadCursors();
            ApplyCursor(MouseCursorKind.Default);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                hasAppliedCursor = false;
            }
        }

        private void Update()
        {
            if (!TryReadPointerPosition(out var pointer))
            {
                ApplyCursor(MouseCursorKind.Default);
                return;
            }

            ApplyCursor(ResolveCursor(pointer));
        }

        private void LoadCursors()
        {
            cursors.Clear();
            AddCursor(MouseCursorKind.Default, "cursor_default", new Vector2(14f, 4f));
            AddCursor(MouseCursorKind.Pointer, "cursor_pointer", new Vector2(31f, 7f));
            AddCursor(MouseCursorKind.Text, "cursor_text", new Vector2(32f, 32f));
            AddCursor(MouseCursorKind.Move, "cursor_move", new Vector2(32f, 32f));
            AddCursor(MouseCursorKind.Grabbing, "cursor_grabbing", new Vector2(32f, 32f));
            AddCursor(MouseCursorKind.Loading, "cursor_loading", new Vector2(32f, 32f));
            AddCursor(MouseCursorKind.NotAllowed, "cursor_default", new Vector2(14f, 4f));
            AddCursor(MouseCursorKind.ResizeHorizontal, "cursor_resize_horizontal", new Vector2(32f, 32f));
            AddCursor(MouseCursorKind.ResizeVertical, "cursor_resize_vertical", new Vector2(32f, 32f));
            AddCursor(MouseCursorKind.Zoom, "cursor_zoom", new Vector2(26f, 26f));
        }

        private void AddCursor(MouseCursorKind kind, string resourceName, Vector2 hotspot)
        {
            var texture = Resources.Load<Texture2D>(ResourceRoot + resourceName);
            if (texture == null)
            {
                Debug.LogWarning($"[MouseCursor] Missing cursor texture Resources/{ResourceRoot}{resourceName}.png");
            }

            cursors[kind] = new CursorDefinition(texture, hotspot);
        }

        private MouseCursorKind ResolveCursor(Vector2 pointer)
        {
            if (TryResolveOverride(out var cursor) ||
                TryResolveUiToolkitCursor(pointer, out cursor) ||
                TryResolveEventSystemCursor(pointer, out cursor) ||
                TryResolveWorldCursor(pointer, out cursor))
            {
                return cursor;
            }

            return MouseCursorKind.Default;
        }

        private bool TryResolveOverride(out MouseCursorKind cursor)
        {
            cursor = MouseCursorKind.Default;
            var found = false;
            var bestPriority = int.MinValue;
            var bestOrder = long.MinValue;

            staleOverrideOwners.Clear();
            foreach (var pair in overrides)
            {
                if (IsDeadOwner(pair.Key))
                {
                    staleOverrideOwners.Add(pair.Key);
                    continue;
                }

                var value = pair.Value;
                if (found && (value.Priority < bestPriority || value.Priority == bestPriority && value.Order <= bestOrder))
                {
                    continue;
                }

                cursor = value.Cursor;
                bestPriority = value.Priority;
                bestOrder = value.Order;
                found = true;
            }

            for (var i = 0; i < staleOverrideOwners.Count; i++)
            {
                overrides.Remove(staleOverrideOwners[i]);
            }

            return found;
        }

        private static bool IsDeadOwner(object owner)
        {
            return owner is UnityEngine.Object unityObject && unityObject == null;
        }

        private bool TryResolveUiToolkitCursor(Vector2 pointer, out MouseCursorKind cursor)
        {
            cursor = MouseCursorKind.Default;
            var documents = UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (var i = 0; i < documents.Length; i++)
            {
                var root = documents[i].rootVisualElement;
                if (root == null || root.panel == null)
                {
                    continue;
                }

                var panelPosition = RuntimePanelUtils.ScreenToPanel(root.panel, pointer);
                var picked = root.panel.Pick(panelPosition);
                if (picked == null)
                {
                    continue;
                }

                if (TryResolveVisualElementCursor(picked, out cursor))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveVisualElementCursor(VisualElement element, out MouseCursorKind cursor)
        {
            cursor = MouseCursorKind.Default;

            for (var current = element; current != null; current = current.parent)
            {
                if (TryResolveCursorClass(current, out cursor))
                {
                    return true;
                }

                if (!current.enabledInHierarchy)
                {
                    cursor = MouseCursorKind.NotAllowed;
                    return true;
                }

                if (IsTextInputElement(current))
                {
                    cursor = MouseCursorKind.Text;
                    return true;
                }

                if (IsPointerElement(current))
                {
                    cursor = MouseCursorKind.Pointer;
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveCursorClass(VisualElement element, out MouseCursorKind cursor)
        {
            if (HasCursorClass(element, "default"))
            {
                cursor = MouseCursorKind.Default;
                return true;
            }

            if (HasCursorClass(element, "pointer"))
            {
                cursor = MouseCursorKind.Pointer;
                return true;
            }

            if (HasCursorClass(element, "text"))
            {
                cursor = MouseCursorKind.Text;
                return true;
            }

            if (HasCursorClass(element, "move"))
            {
                cursor = MouseCursorKind.Move;
                return true;
            }

            if (HasCursorClass(element, "grabbing"))
            {
                cursor = MouseCursorKind.Grabbing;
                return true;
            }

            if (HasCursorClass(element, "loading"))
            {
                cursor = MouseCursorKind.Loading;
                return true;
            }

            if (HasCursorClass(element, "not-allowed") ||
                HasCursorClass(element, "not_allowed") ||
                HasCursorClass(element, "disabled"))
            {
                cursor = MouseCursorKind.NotAllowed;
                return true;
            }

            if (HasCursorClass(element, "resize-horizontal") ||
                HasCursorClass(element, "resize_h") ||
                HasCursorClass(element, "resize-x"))
            {
                cursor = MouseCursorKind.ResizeHorizontal;
                return true;
            }

            if (HasCursorClass(element, "resize-vertical") ||
                HasCursorClass(element, "resize_v") ||
                HasCursorClass(element, "resize-y"))
            {
                cursor = MouseCursorKind.ResizeVertical;
                return true;
            }

            if (HasCursorClass(element, "zoom"))
            {
                cursor = MouseCursorKind.Zoom;
                return true;
            }

            cursor = MouseCursorKind.Default;
            return false;
        }

        private static bool HasCursorClass(VisualElement element, string suffix)
        {
            return element.ClassListContains("cursor-" + suffix) ||
                   element.ClassListContains("mouse-cursor-" + suffix);
        }

        private static bool IsTextInputElement(VisualElement element)
        {
            var typeName = element.GetType().Name;
            return typeName.IndexOf("TextField", StringComparison.Ordinal) >= 0 ||
                   typeName.IndexOf("TextInput", StringComparison.Ordinal) >= 0 ||
                   typeName.IndexOf("SearchField", StringComparison.Ordinal) >= 0 ||
                   element.ClassListContains("unity-text-input") ||
                   element.ClassListContains("unity-base-text-field__input");
        }

        private static bool IsPointerElement(VisualElement element)
        {
            if (element is UitButton ||
                element is UitToggle ||
                element is UitSlider ||
                element is UitScroller ||
                element is MinMaxSlider)
            {
                return true;
            }

            var typeName = element.GetType().Name;
            return typeName.IndexOf("Button", StringComparison.Ordinal) >= 0 ||
                   typeName.IndexOf("Toggle", StringComparison.Ordinal) >= 0 ||
                   typeName.IndexOf("Dropdown", StringComparison.Ordinal) >= 0 ||
                   typeName.IndexOf("Popup", StringComparison.Ordinal) >= 0 ||
                   typeName.IndexOf("RadioButton", StringComparison.Ordinal) >= 0 ||
                   typeName.IndexOf("Scroller", StringComparison.Ordinal) >= 0 ||
                   typeName.IndexOf("Slider", StringComparison.Ordinal) >= 0 ||
                   typeName.IndexOf("Foldout", StringComparison.Ordinal) >= 0 ||
                   typeName.IndexOf("Tab", StringComparison.Ordinal) >= 0;
        }

        private bool TryResolveEventSystemCursor(Vector2 pointer, out MouseCursorKind cursor)
        {
            cursor = MouseCursorKind.Default;
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            if (pointerEventData == null || pointerEventSystem != eventSystem)
            {
                pointerEventData = new PointerEventData(eventSystem);
                pointerEventSystem = eventSystem;
            }

            pointerEventData.Reset();
            pointerEventData.position = pointer;
            raycastResults.Clear();
            eventSystem.RaycastAll(pointerEventData, raycastResults);

            for (var i = 0; i < raycastResults.Count; i++)
            {
                var hitObject = raycastResults[i].gameObject;
                if (hitObject == null)
                {
                    continue;
                }

                if (TryResolveHint(hitObject, out cursor))
                {
                    return true;
                }

                var selectable = hitObject.GetComponentInParent<Selectable>(true);
                if (selectable != null && (!selectable.enabled || !selectable.IsInteractable()))
                {
                    cursor = MouseCursorKind.NotAllowed;
                    return true;
                }

                if (hitObject.GetComponentInParent<UiInputField>(true) != null ||
                    hitObject.GetComponentInParent<TMP_InputField>(true) != null)
                {
                    cursor = MouseCursorKind.Text;
                    return true;
                }

                var scrollbar = hitObject.GetComponentInParent<UiScrollbar>(true);
                if (scrollbar != null)
                {
                    cursor = IsHorizontalScrollbar(scrollbar) ? MouseCursorKind.ResizeHorizontal : MouseCursorKind.ResizeVertical;
                    return true;
                }

                var scrollRect = hitObject.GetComponentInParent<ScrollRect>(true);
                if (scrollRect != null && IsPrimaryPointerPressed())
                {
                    cursor = MouseCursorKind.Grabbing;
                    return true;
                }

                if (hitObject.GetComponentInParent<UiButton>(true) != null ||
                    hitObject.GetComponentInParent<UiToggle>(true) != null ||
                    hitObject.GetComponentInParent<UiSlider>(true) != null ||
                    hitObject.GetComponentInParent<UiDropdown>(true) != null ||
                    hitObject.GetComponentInParent<TMP_Dropdown>(true) != null)
                {
                    cursor = MouseCursorKind.Pointer;
                    return true;
                }
            }

            return false;
        }

        private static bool IsHorizontalScrollbar(UiScrollbar scrollbar)
        {
            return scrollbar.direction == UiScrollbar.Direction.LeftToRight ||
                   scrollbar.direction == UiScrollbar.Direction.RightToLeft;
        }

        private bool TryResolveWorldCursor(Vector2 pointer, out MouseCursorKind cursor)
        {
            cursor = MouseCursorKind.Default;
            var camera = Camera.main;
            if (camera == null)
            {
                return false;
            }

            var ray = camera.ScreenPointToRay(pointer);
            var hits = Physics.RaycastAll(ray, 500f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            if (hits.Length == 0)
            {
                return false;
            }

            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            var editModeActive = IsRoomEditModeActive();
            for (var i = 0; i < hits.Length; i++)
            {
                var collider = hits[i].collider;
                if (collider == null || !collider.enabled)
                {
                    continue;
                }

                var hitObject = collider.gameObject;
                if (TryResolveHint(hitObject, out cursor))
                {
                    return true;
                }

                if (editModeActive && HasComponentInParentsNamed(hitObject.transform, "DecorationItemMarker"))
                {
                    cursor = IsPrimaryPointerPressed() ? MouseCursorKind.Grabbing : MouseCursorKind.Move;
                    return true;
                }

                if (TryResolveWorldMarker(hitObject.transform, out cursor))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveHint(GameObject source, out MouseCursorKind cursor)
        {
            cursor = MouseCursorKind.Default;
            var hint = source.GetComponentInParent<MouseCursorHint>(true);
            if (hint == null || !hint.isActiveAndEnabled)
            {
                return false;
            }

            cursor = hint.UsePressedCursor && IsPrimaryPointerPressed() ? hint.PressedCursor : hint.Cursor;
            return true;
        }

        private static bool HasComponentInParentsNamed(Transform start, string typeName)
        {
            for (var current = start; current != null; current = current.parent)
            {
                var components = current.GetComponents<MonoBehaviour>();
                for (var i = 0; i < components.Length; i++)
                {
                    var component = components[i];
                    if (component != null && component.GetType().Name == typeName)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryResolveWorldMarker(Transform start, out MouseCursorKind cursor)
        {
            cursor = MouseCursorKind.Default;

            for (var current = start; current != null; current = current.parent)
            {
                var components = current.GetComponents<MonoBehaviour>();
                for (var i = 0; i < components.Length; i++)
                {
                    var component = components[i];
                    if (component == null)
                    {
                        continue;
                    }

                    var typeName = component.GetType().Name;
                    if (typeName == "BookshelfFocusHitArea")
                    {
                        cursor = MouseCursorKind.Zoom;
                        return true;
                    }

                    if (typeName == "BookInteractionHitArea" ||
                        typeName == "NotebookHitArea" ||
                        typeName == "GuestbookBoardHitArea" ||
                        typeName == "BookView" ||
                        typeName == "ShelfSectionView" ||
                        typeName.EndsWith("HitArea", StringComparison.Ordinal))
                    {
                        cursor = MouseCursorKind.Pointer;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsRoomEditModeActive()
        {
            var controller = UnityEngine.Object.FindFirstObjectByType<RoomEditModeController>(FindObjectsInactive.Include);
            return controller != null && controller.IsEditModeActive;
        }

        private void ApplyCursor(MouseCursorKind cursor)
        {
            if (hasAppliedCursor && appliedCursor == cursor)
            {
                return;
            }

            if (!cursors.TryGetValue(cursor, out var definition) || definition.Texture == null)
            {
                if (!cursors.TryGetValue(MouseCursorKind.Default, out definition) || definition.Texture == null)
                {
                    UnityEngine.Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                    hasAppliedCursor = true;
                    appliedCursor = cursor;
                    return;
                }
            }

            UnityEngine.Cursor.SetCursor(definition.Texture, definition.Hotspot, CursorMode.Auto);
            hasAppliedCursor = true;
            appliedCursor = cursor;
        }

        private static bool TryReadPointerPosition(out Vector2 position)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                position = Mouse.current.position.ReadValue();
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            position = Input.mousePosition;
            return true;
#else
            position = default;
            return false;
#endif
        }

        private static bool IsPrimaryPointerPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.leftButton.isPressed;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(0);
#else
            return false;
#endif
        }
    }
}
