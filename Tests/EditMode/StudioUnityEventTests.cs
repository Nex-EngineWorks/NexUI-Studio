using System.Collections.Generic;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Core;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Designer.Editor.Components.Serialization;
using emiteat.NexUI.Designer.Editor.Serialization;
using emiteat.NexUI.Designer.Editor.Validation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// UnityEvent authoring: persistent calls that target another element, survive a prefab round
    /// trip, and are reported when the method they name disappears.
    /// </summary>
    public sealed class StudioUnityEventTests
    {
        private const string TempFolder = "Assets/NexUIStudioEventTests";
        private const string PrefabPath = TempFolder + "/Events.prefab";

        /// <summary>A target with one method of each shape the persistent-call format supports.</summary>
        public sealed class SampleEventTarget : MonoBehaviour
        {
            public int LastInt;
            public bool LastBool;

            public void Ping() { }
            public void SetAmount(int value) => LastInt = value;
            public void SetFlag(bool value) => LastBool = value;
            public int NotVoid() => 0;
            public void TooManyArguments(int a, int b) { }
        }

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.CreateFolder("Assets", "NexUIStudioEventTests");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TempFolder);
            Undo.ClearAll();
            StudioScratchComponentHost.Dispose();
        }

        // ---- Method discovery ----------------------------------------------------------------------

        [Test]
        public void OnlyMethodsUnityCanInvokeAreOffered()
        {
            var names = new List<string>();
            foreach (var method in StudioUnityEventModel.InvokableMethods(typeof(SampleEventTarget)))
                names.Add(method.Name);

            CollectionAssert.Contains(names, nameof(SampleEventTarget.Ping));
            CollectionAssert.Contains(names, nameof(SampleEventTarget.SetAmount));
            CollectionAssert.Contains(names, nameof(SampleEventTarget.SetFlag));
            CollectionAssert.DoesNotContain(names, nameof(SampleEventTarget.NotVoid));
            CollectionAssert.DoesNotContain(names, nameof(SampleEventTarget.TooManyArguments));
        }

        [Test]
        public void TheListenerModeFollowsTheMethodSignature()
        {
            var methods = StudioUnityEventModel.InvokableMethods(typeof(SampleEventTarget));

            Assert.AreEqual(StudioUnityEventModel.ListenerMode.Void,
                StudioUnityEventModel.ModeOf(methods.Find(m => m.Name == nameof(SampleEventTarget.Ping))));
            Assert.AreEqual(StudioUnityEventModel.ListenerMode.Int,
                StudioUnityEventModel.ModeOf(methods.Find(m => m.Name == nameof(SampleEventTarget.SetAmount))));
            Assert.AreEqual(StudioUnityEventModel.ListenerMode.Bool,
                StudioUnityEventModel.ModeOf(methods.Find(m => m.Name == nameof(SampleEventTarget.SetFlag))));
        }

        // ---- Model round trip -----------------------------------------------------------------------

        [Test]
        public void CallsRoundTripThroughThePropertyBag()
        {
            var component = new DesignerElementComponent("UGUI.Button");
            var calls = new List<StudioUnityEventModel.Call>
            {
                new StudioUnityEventModel.Call
                {
                    Target = new DesignerObjectReference
                    {
                        kind = DesignerReferenceKind.Element,
                        stableElementId = "target-stable",
                        componentTypeName = StudioComponentTypeIndex.Identity(typeof(SampleEventTarget))
                    },
                    MethodName = nameof(SampleEventTarget.SetAmount),
                    Mode = StudioUnityEventModel.ListenerMode.Int,
                    IntArgument = 7,
                    CallState = UnityEventCallState.EditorAndRuntime
                }
            };

            StudioUnityEventModel.Write(component, "m_OnClick", calls);
            var read = StudioUnityEventModel.Read(component, "m_OnClick");

            Assert.AreEqual(1, read.Count);
            Assert.AreEqual("target-stable", read[0].Target.stableElementId);
            Assert.AreEqual(nameof(SampleEventTarget.SetAmount), read[0].MethodName);
            Assert.AreEqual(StudioUnityEventModel.ListenerMode.Int, read[0].Mode);
            Assert.AreEqual(7, read[0].IntArgument);
            Assert.AreEqual(UnityEventCallState.EditorAndRuntime, read[0].CallState);
        }

        [Test]
        public void RewritingTheListShrinksItInsteadOfLeavingStaleEntries()
        {
            var component = new DesignerElementComponent("UGUI.Button");
            StudioUnityEventModel.Write(component, "m_OnClick", new List<StudioUnityEventModel.Call>
            {
                new StudioUnityEventModel.Call { MethodName = "A" },
                new StudioUnityEventModel.Call { MethodName = "B" }
            });

            StudioUnityEventModel.Write(component, "m_OnClick", new List<StudioUnityEventModel.Call>
            {
                new StudioUnityEventModel.Call { MethodName = "A" }
            });

            var read = StudioUnityEventModel.Read(component, "m_OnClick");
            Assert.AreEqual(1, read.Count);
            foreach (var entry in component.properties)
                StringAssert.DoesNotContain("data[1]", entry.key,
                    "A removed listener must leave no entries behind, or the array size and its data disagree.");
        }

        // ---- Prefab apply ------------------------------------------------------------------------------

        [Test]
        public void AListenerTargetingAnotherElementIsWiredOnThePrefab()
        {
            var root = new GameObject("Screen", typeof(RectTransform));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            metadata.schemaVersion = DesignerMetadataAsset.CurrentSchemaVersion;
            metadata.screenId = "events";

            var handler = new DesignerElementMetadata
            {
                stableId = "handler-stable", elementId = "Handler", elementType = "Panel"
            };
            DesignerElementComponentAccess.AttachProject(handler, typeof(SampleEventTarget));
            metadata.elements.Add(handler);

            var button = new DesignerElementMetadata
            {
                stableId = "button-stable", elementId = "Button", elementType = "Button"
            };
            var buttonComponent = DesignerElementComponentAccess.AttachProject(button, typeof(Button));
            StudioUnityEventModel.Write(buttonComponent, "m_OnClick", new List<StudioUnityEventModel.Call>
            {
                new StudioUnityEventModel.Call
                {
                    Target = new DesignerObjectReference
                    {
                        kind = DesignerReferenceKind.Element,
                        stableElementId = "handler-stable",
                        componentTypeName = StudioComponentTypeIndex.Identity(typeof(SampleEventTarget))
                    },
                    MethodName = nameof(SampleEventTarget.SetAmount),
                    Mode = StudioUnityEventModel.ListenerMode.Int,
                    IntArgument = 42,
                    CallState = UnityEventCallState.RuntimeOnly
                }
            });
            metadata.elements.Add(button);

            var screen = ScriptableObject.CreateInstance<UIScreenDefinition>();
            screen.identity = new UIScreenIdentity { screenId = "events" };
            screen.backendAsset = new UIScreenBackendAsset { backend = UIRenderBackend.UGUI, asset = prefab };

            var report = new UGUIAssetSerializer().Save(screen, metadata);
            Assert.IsFalse(report.HasErrors, report.Details());

            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var saved = contents.transform.Find("Button").GetComponent<Button>();
                Assert.AreEqual(1, saved.onClick.GetPersistentEventCount(),
                    "The listener has to reach the real UnityEvent, not just the metadata.");
                Assert.AreEqual(nameof(SampleEventTarget.SetAmount), saved.onClick.GetPersistentMethodName(0));

                var target = saved.onClick.GetPersistentTarget(0) as SampleEventTarget;
                Assert.IsNotNull(target, "The element reference must resolve to the real component.");
                Assert.AreEqual("Handler", target.gameObject.name);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
                Object.DestroyImmediate(metadata);
                Object.DestroyImmediate(screen);
            }
        }

        [Test]
        public void DrawingTheInspectorDoesNotEraseListeners()
        {
            var element = new DesignerElementMetadata { stableId = "b-stable", elementId = "Button" };
            var component = DesignerElementComponentAccess.AttachProject(element, typeof(Button));
            StudioUnityEventModel.Write(component, "m_OnClick", new List<StudioUnityEventModel.Call>
            {
                new StudioUnityEventModel.Call { MethodName = nameof(SampleEventTarget.Ping) }
            });

            // Loading into the scratch object and capturing straight back is what happens the moment
            // the card is drawn. The scratch Button's own event list is empty, so a naive capture
            // would decide the user's listener was a removal.
            var serializedObject = StudioSerializedComponentBridge.Load(component, typeof(Button));
            StudioSerializedComponentBridge.Capture(serializedObject, typeof(Button), component);

            Assert.AreEqual(1, StudioUnityEventModel.Read(component, "m_OnClick").Count,
                "Selecting the element must not delete its listeners.");
        }

        // ---- Validation --------------------------------------------------------------------------------

        [Test]
        public void AListenerNamingAMissingMethodIsReported()
        {
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            metadata.screenId = "events";
            var handler = new DesignerElementMetadata { stableId = "handler-stable", elementId = "Handler" };
            DesignerElementComponentAccess.AttachProject(handler, typeof(SampleEventTarget));
            metadata.elements.Add(handler);

            var button = new DesignerElementMetadata { stableId = "button-stable", elementId = "Button" };
            var component = DesignerElementComponentAccess.AttachProject(button, typeof(Button));
            StudioUnityEventModel.Write(component, "m_OnClick", new List<StudioUnityEventModel.Call>
            {
                new StudioUnityEventModel.Call
                {
                    Target = new DesignerObjectReference
                    {
                        kind = DesignerReferenceKind.Element,
                        stableElementId = "handler-stable",
                        componentTypeName = StudioComponentTypeIndex.Identity(typeof(SampleEventTarget))
                    },
                    MethodName = "MethodThatWasRenamed"
                }
            });
            metadata.elements.Add(button);

            var issues = new List<DesignerValidationIssue>();
            try
            {
                DesignerElementComponentValidation.Validate(
                    metadata, "events", DesignerUIComponentFamily.UGUI, issues);

                Assert.IsNotNull(issues.Find(i => i.Code == "NEXUI-EVENT-MISSING-METHOD"),
                    "A renamed handler leaves a button that silently does nothing; it has to be reported.");
            }
            finally
            {
                Object.DestroyImmediate(metadata);
            }
        }

        [Test]
        public void AReferenceToADeletedElementIsReported()
        {
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            metadata.screenId = "events";
            var bar = new DesignerElementMetadata { stableId = "bar-stable", elementId = "HealthBar" };
            var component = DesignerElementComponentAccess.AttachProject(bar, typeof(SampleHealthBarController));
            DesignerComponentPropertyBag.Set(component.properties, "fill", new DesignerPropertyValue
            {
                type = DesignerPropertyValueType.ElementReference,
                reference = new DesignerObjectReference
                {
                    kind = DesignerReferenceKind.Element, stableElementId = "deleted-stable"
                }
            });
            metadata.elements.Add(bar);

            var issues = new List<DesignerValidationIssue>();
            try
            {
                DesignerElementComponentValidation.Validate(
                    metadata, "events", DesignerUIComponentFamily.UGUI, issues);

                Assert.IsNotNull(issues.Find(i => i.Code == "NEXUI-REFERENCE-MISSING"));
            }
            finally
            {
                Object.DestroyImmediate(metadata);
            }
        }
    }
}
