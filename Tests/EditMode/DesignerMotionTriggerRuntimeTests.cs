using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.MotionClip;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    public sealed class DesignerMotionTriggerRuntimeTests
    {
        [Test]
        public void ClickAndNotify_PlayExpectedClips_AndDisposeUnsubscribes()
        {
            var clickClip = ScriptableObject.CreateInstance<UIMotionClip>();
            var commandClip = ScriptableObject.CreateInstance<UIMotionClip>();
            var reducedClip = ScriptableObject.CreateInstance<UIMotionClip>();
            try
            {
                var click = new ClickCapability();
                var surface = new Surface(new Handle("button", click));
                var motion = new DesignerScreenMotionMetadata();
                motion.bindings.Add(new DesignerMotionBinding { targetElementId = "button", trigger = DesignerMotionTrigger.Click, clip = clickClip, reducedMotionClip = reducedClip });
                motion.bindings.Add(new DesignerMotionBinding { trigger = DesignerMotionTrigger.CommandCompleted, commandId = "equip", clip = commandClip });
                var played = new List<UIMotionClip>();

                using (var runtime = new DesignerMotionTriggerRuntime(surface, motion,
                           () => new RecordingPlayer(played), () => true))
                {
                    click.Raise();
                    runtime.Notify(DesignerMotionTrigger.CommandCompleted, commandId: "equip");
                    CollectionAssert.AreEqual(new[] { reducedClip, commandClip }, played);
                }

                click.Raise();
                Assert.AreEqual(2, played.Count, "disposed runtime must not retain capability callbacks");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clickClip);
                UnityEngine.Object.DestroyImmediate(commandClip);
                UnityEngine.Object.DestroyImmediate(reducedClip);
            }
        }

        private sealed class RecordingPlayer : IUIMotionClipPlayer
        {
            private readonly List<UIMotionClip> _played;
            public RecordingPlayer(List<UIMotionClip> played) => _played = played;
            public Task PlayAsync(IUISurface surface, UIMotionClip clip, CancellationToken cancellationToken = default)
            { _played.Add(clip); return Task.CompletedTask; }
            public void Stop() { }
            public void Evaluate(IUISurface surface, UIMotionClip clip, float time) { }
        }

        private sealed class ClickCapability : IUIClickCapability
        {
            public event Action Clicked;
            public void Raise() => Clicked?.Invoke();
        }

        private sealed class Handle : IUIElementHandle
        {
            private readonly object _capability;
            public Handle(string id, object capability) { Id = id; _capability = capability; }
            public string Id { get; }
            public UIRenderBackend Backend => UIRenderBackend.UGUI;
            public object Native => null;
            public bool Has<TCapability>() where TCapability : class => _capability is TCapability;
            public TCapability As<TCapability>() where TCapability : class => _capability as TCapability;
        }

        private sealed class Surface : IUISurface
        {
            private readonly IUIElementHandle _handle;
            public Surface(IUIElementHandle handle) => _handle = handle;
            public string ScreenId => "test";
            public UIRenderBackend Backend => UIRenderBackend.UGUI;
            public object NativeRoot => null;
            public IUIElementHandle RootHandle => _handle;
            public IUIElementHandle TryFind(string elementId) => _handle.Id == elementId ? _handle : null;
            public IUIElementHandle FindRequired(string elementId) => TryFind(elementId) ?? throw new UIElementNotFoundException(elementId);
            public void SetActive(bool active) { }
            public void SetSortingOrder(int order) { }
            public void SetInputBlocking(bool blocking) { }
            public void Destroy() { }
        }
    }
}
