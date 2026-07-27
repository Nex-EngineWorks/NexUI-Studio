using System.Collections.Generic;
using emiteat.NexUI.Abstractions;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Backend
{
    internal sealed class DesignerSurface : IUISurface
    {
        private readonly Dictionary<string, DesignerElementHandle> _handles = new Dictionary<string, DesignerElementHandle>();

        public string ScreenId { get; private set; }
        public UIRenderBackend Backend { get; private set; }
        public object NativeRoot { get; private set; }
        public IUIElementHandle RootHandle { get; private set; }

        public DesignerSurface(string screenId, UIRenderBackend backend, object nativeRoot)
        {
            ScreenId = screenId;
            Backend = backend;
            NativeRoot = nativeRoot;
            RootHandle = AddHandle("root", nativeRoot);
        }

        public IReadOnlyList<IUIElementHandle> Handles
        {
            get
            {
                var list = new List<IUIElementHandle>();
                foreach (var pair in _handles)
                    list.Add(pair.Value);
                return list;
            }
        }

        public DesignerElementHandle AddHandle(string id, object native)
        {
            var handle = new DesignerElementHandle(id, Backend, native);
            _handles[id] = handle;
            return handle;
        }

        public IUIElementHandle TryFind(string elementId)
            => _handles.TryGetValue(elementId, out var h) ? h : null;

        public IUIElementHandle FindRequired(string elementId)
            => TryFind(elementId) ?? throw new UIElementNotFoundException(elementId);

        public void SetActive(bool active)
        {
            if (NativeRoot is VisualElement ve)
                ve.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            if (NativeRoot is GameObject go)
                go.SetActive(active);
        }

        public void SetSortingOrder(int order) { }
        public void SetInputBlocking(bool blocking) { }

        public void Destroy()
        {
            if (NativeRoot is VisualElement ve)
                ve.RemoveFromHierarchy();
            if (NativeRoot is GameObject go)
                Object.DestroyImmediate(go);
            _handles.Clear();
        }
    }

    internal sealed class DesignerElementHandle : IUIElementHandle, IUITransformCapability, IUIVisibilityCapability, IUIStyleCapability, IUITextCapability, IUIValueCapability, IUIColorCapability, IUITypographyCapability
    {
        private readonly HashSet<string> _classes = new HashSet<string>();
        public string Id { get; private set; }
        public DesignerBindingMetadata Binding { get; set; } = new DesignerBindingMetadata();
        public UIRenderBackend Backend { get; private set; }
        public object Native { get; private set; }
        public float Opacity { get; set; }
        public Vector2 Position { get; set; }
        public Vector3 Scale { get; set; }
        public float Rotation { get; set; }
        public bool Visible { get; set; }
        public string Text { get; set; }
        public float Value { get; set; }
        public float Min { get; set; }
        public float Max { get; set; }
        public Color BackgroundColor { get; set; }
        public Color TextColor { get; set; }
        public float FontSize { get; set; }

        public DesignerElementHandle(string id, UIRenderBackend backend, object native)
        {
            Id = id;
            Backend = backend;
            Native = native;
            Visible = true;
            Scale = Vector3.one;
            Max = 1f;
        }

        public bool Has<TCapability>() where TCapability : class => As<TCapability>() != null;
        public TCapability As<TCapability>() where TCapability : class => this as TCapability;
        public void Rename(string id) => Id = id;
        public void SetClass(string className, bool on)
        {
            if (on) _classes.Add(className);
            else _classes.Remove(className);
        }
        public void ApplyToken(string tokenKey, string value) { }
    }
}
