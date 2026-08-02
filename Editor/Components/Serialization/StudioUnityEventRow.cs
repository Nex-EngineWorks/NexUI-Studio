using System;
using System.Collections.Generic;
using System.Reflection;
using emiteat.NexUI.Designer.Editor.Localization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Components.Serialization
{
    /// <summary>
    /// Editor for one UnityEvent field: the persistent call list, with targets that can be another
    /// element on this screen.
    /// </summary>
    /// <remarks>
    /// Unity's own UnityEvent drawer cannot be reused here. It enumerates methods from the object
    /// currently assigned as the target, and at authoring time the target element has no GameObject
    /// yet - so the method dropdown would always be empty. This row resolves the target's component
    /// <i>type</i> from the reference instead, which is all the method list actually needs.
    /// </remarks>
    internal sealed class StudioUnityEventRow : VisualElement
    {
        private readonly NexUIDesignerContext _context;
        private readonly DesignerElementMetadata _element;
        private readonly string _instanceId;
        private readonly string _key;
        private readonly VisualElement _list;

        public StudioUnityEventRow(NexUIDesignerContext context, DesignerElementMetadata element,
            string instanceId, string key, string displayName, string tooltip)
        {
            _context = context;
            _element = element;
            _instanceId = instanceId;
            _key = key;

            AddToClassList("nexui-unityevent");

            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var label = new Label(displayName) { tooltip = tooltip };
            label.AddToClassList("nexui-unityevent-title");
            label.style.flexGrow = 1;
            header.Add(label);

            var add = new Button(AddCall)
            {
                text = "+",
                tooltip = DesignerLocalization.T("inspector.unityEvent.addTooltip")
            };
            add.AddToClassList("nexui-unityevent-add");
            header.Add(add);
            Add(header);

            _list = new VisualElement();
            _list.AddToClassList("nexui-unityevent-list");
            Add(_list);

            Refresh();
        }

        private DesignerElementComponent Component => DesignerElementComponentAccess.Find(_element, _instanceId);

        private List<StudioUnityEventModel.Call> Calls => StudioUnityEventModel.Read(Component, _key);

        private void Commit(List<StudioUnityEventModel.Call> calls, string undoName)
        {
            _context.UpdateElement(_element, e =>
            {
                var component = DesignerElementComponentAccess.Find(e, _instanceId);
                if (component != null) StudioUnityEventModel.Write(component, _key, calls);
            }, undoName);
            Refresh();
        }

        private void AddCall()
        {
            var calls = Calls;
            calls.Add(new StudioUnityEventModel.Call());
            Commit(calls, "Add Event Listener");
        }

        private void Refresh()
        {
            _list.Clear();
            var calls = Calls;
            if (calls.Count == 0)
            {
                var empty = new Label(DesignerLocalization.T("inspector.unityEvent.empty"))
                {
                    style = { opacity = 0.6f, fontSize = 11, marginLeft = 12 }
                };
                _list.Add(empty);
                return;
            }

            for (var i = 0; i < calls.Count; i++) _list.Add(CallRow(calls, i));
        }

        private VisualElement CallRow(List<StudioUnityEventModel.Call> calls, int index)
        {
            var call = calls[index];
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            row.AddToClassList("nexui-unityevent-call");

            var state = new EnumField(call.CallState) { tooltip = DesignerLocalization.T("inspector.unityEvent.callStateTooltip") };
            state.style.width = 96;
            state.RegisterValueChangedCallback(evt =>
            {
                calls[index].CallState = (UnityEventCallState)evt.newValue;
                Commit(calls, "Set Listener Call State");
            });
            row.Add(state);

            var target = new Button(() => ShowTargetMenu(calls, index))
            {
                text = StudioReferenceUtility.Describe(call.Target, _context?.Metadata),
                tooltip = DesignerLocalization.T("inspector.unityEvent.targetTooltip")
            };
            target.AddToClassList("nexui-unityevent-target");
            target.style.flexGrow = 1;
            row.Add(target);

            var targetType = TargetTypeOf(call);
            var methods = StudioUnityEventModel.InvokableMethods(targetType);
            row.Add(MethodControl(calls, index, methods, targetType));

            var argument = ArgumentControl(calls, index, methods);
            if (argument != null) row.Add(argument);

            var remove = new Button(() =>
            {
                calls.RemoveAt(index);
                Commit(calls, "Remove Event Listener");
            })
            {
                text = "−",
                tooltip = DesignerLocalization.T("inspector.unityEvent.removeTooltip")
            };
            remove.AddToClassList("nexui-unityevent-remove");
            row.Add(remove);

            return row;
        }

        private VisualElement MethodControl(List<StudioUnityEventModel.Call> calls, int index,
            List<MethodInfo> methods, Type targetType)
        {
            if (targetType == null || methods.Count == 0)
            {
                var none = new Label(targetType == null
                    ? DesignerLocalization.T("inspector.unityEvent.noTarget")
                    : DesignerLocalization.T("inspector.unityEvent.noMethods"))
                {
                    style = { width = 160, opacity = 0.6f, fontSize = 11 }
                };
                return none;
            }

            var labels = new List<string>();
            foreach (var method in methods) labels.Add(StudioUnityEventModel.Label(method));

            var current = methods.FindIndex(m => m.Name == calls[index].MethodName);

            // A method that no longer exists is shown as such instead of silently snapping to the
            // first entry - a renamed handler is a bug the user needs to see, not one to paper over.
            if (current < 0 && !string.IsNullOrEmpty(calls[index].MethodName))
            {
                labels.Insert(0, string.Format(
                    DesignerLocalization.T("inspector.unityEvent.missingMethod"), calls[index].MethodName));
                current = 0;
            }
            else if (current < 0) current = 0;

            var popup = new PopupField<string>(labels, Mathf.Clamp(current, 0, labels.Count - 1));
            popup.style.width = 160;
            popup.RegisterValueChangedCallback(evt =>
            {
                var chosen = labels.IndexOf(evt.newValue);
                var offset = labels.Count - methods.Count; // 1 when the missing-method entry is present
                var methodIndex = chosen - offset;
                if (methodIndex < 0 || methodIndex >= methods.Count) return;

                var method = methods[methodIndex];
                calls[index].MethodName = method.Name;
                calls[index].Mode = StudioUnityEventModel.ModeOf(method);
                Commit(calls, "Set Listener Method");
            });
            return popup;
        }

        private VisualElement ArgumentControl(List<StudioUnityEventModel.Call> calls, int index,
            List<MethodInfo> methods)
        {
            var call = calls[index];
            switch (call.Mode)
            {
                case StudioUnityEventModel.ListenerMode.Int:
                {
                    var field = new IntegerField { value = call.IntArgument, style = { width = 80 } };
                    field.RegisterValueChangedCallback(evt =>
                    {
                        calls[index].IntArgument = evt.newValue;
                        Commit(calls, "Set Listener Argument");
                    });
                    return field;
                }
                case StudioUnityEventModel.ListenerMode.Float:
                {
                    var field = new FloatField { value = call.FloatArgument, style = { width = 80 } };
                    field.RegisterValueChangedCallback(evt =>
                    {
                        calls[index].FloatArgument = evt.newValue;
                        Commit(calls, "Set Listener Argument");
                    });
                    return field;
                }
                case StudioUnityEventModel.ListenerMode.String:
                {
                    var field = new TextField { value = call.StringArgument, style = { width = 100 } };
                    field.RegisterValueChangedCallback(evt =>
                    {
                        calls[index].StringArgument = evt.newValue;
                        Commit(calls, "Set Listener Argument");
                    });
                    return field;
                }
                case StudioUnityEventModel.ListenerMode.Bool:
                {
                    var field = new Toggle { value = call.BoolArgument };
                    field.RegisterValueChangedCallback(evt =>
                    {
                        calls[index].BoolArgument = evt.newValue;
                        Commit(calls, "Set Listener Argument");
                    });
                    return field;
                }
                case StudioUnityEventModel.ListenerMode.Object:
                {
                    var button = new Button(() => ShowArgumentMenu(calls, index))
                    {
                        text = StudioReferenceUtility.Describe(call.ObjectArgument, _context?.Metadata),
                        style = { width = 120 }
                    };
                    return button;
                }
                default:
                    return null;
            }
        }

        // ---- Target and argument pickers ------------------------------------------------------------

        private void ShowTargetMenu(List<StudioUnityEventModel.Call> calls, int index)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(DesignerLocalization.T("inspector.reference.none")), false, () =>
            {
                calls[index].Target = new DesignerObjectReference();
                calls[index].MethodName = string.Empty;
                Commit(calls, "Clear Listener Target");
            });
            menu.AddSeparator("");

            // Every element and each of its components is offered: a call can target the GameObject
            // itself (SetActive) or any behaviour on it.
            foreach (var element in _context?.Metadata?.elements ?? new List<DesignerElementMetadata>())
            {
                if (element == null) continue;
                var name = string.IsNullOrWhiteSpace(element.displayName) ? element.elementId : element.displayName;
                var captured = element;

                menu.AddItem(new GUIContent($"{name}/GameObject"), false, () =>
                {
                    // Recorded as GameObject rather than left blank: the writer resolves the target
                    // from this type, and a UnityEvent target field is only typed UnityEngine.Object.
                    calls[index].Target = StudioReferenceUtility.ToElement(captured, typeof(GameObject));
                    calls[index].MethodName = string.Empty;
                    Commit(calls, "Set Listener Target");
                });

                foreach (var component in element.components ?? new List<DesignerElementComponent>())
                {
                    var type = StudioReferenceUtility.ResolveComponentType(component);
                    if (type == null) continue;
                    var capturedType = type;
                    menu.AddItem(new GUIContent($"{name}/{type.Name}"), false, () =>
                    {
                        calls[index].Target = StudioReferenceUtility.ToElement(captured, capturedType);
                        calls[index].MethodName = string.Empty;
                        Commit(calls, "Set Listener Target");
                    });
                }
            }
            menu.ShowAsContext();
        }

        private void ShowArgumentMenu(List<StudioUnityEventModel.Call> calls, int index)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(DesignerLocalization.T("inspector.reference.none")), false, () =>
            {
                calls[index].ObjectArgument = new DesignerObjectReference();
                Commit(calls, "Clear Listener Argument");
            });
            menu.AddItem(new GUIContent(DesignerLocalization.T("inspector.reference.asset")), false,
                () => StudioAssetPickerBridge.Pick(typeof(UnityEngine.Object), asset =>
                {
                    calls[index].ObjectArgument = StudioReferenceUtility.FromAsset(asset);
                    Commit(calls, "Set Listener Argument");
                }));
            menu.ShowAsContext();
        }

        /// <summary>
        /// The type whose methods the dropdown lists. A component reference names it outright; a
        /// reference to the element itself means the GameObject.
        /// </summary>
        private static Type TargetTypeOf(StudioUnityEventModel.Call call)
        {
            if (call?.Target == null || !call.Target.IsAssigned) return null;
            if (call.Target.kind == DesignerReferenceKind.Asset)
                return StudioReferenceUtility.ResolveAsset(call.Target)?.GetType();
            return string.IsNullOrEmpty(call.Target.componentTypeName)
                ? typeof(GameObject)
                : StudioComponentTypeIndex.Resolve(call.Target.componentTypeName);
        }
    }
}
