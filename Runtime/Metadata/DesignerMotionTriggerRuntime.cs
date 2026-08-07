using System;
using System.Collections.Generic;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.MotionClip;

namespace emiteat.NexUI.Designer
{
    /// <summary>
    /// Connects persisted Designer motion bindings to backend-neutral runtime capabilities.
    /// Pointer, click and focus events are subscribed automatically; screen/state/command and
    /// enabled/selected state owners call <see cref="Notify"/> at their existing lifecycle boundary.
    /// </summary>
    public sealed class DesignerMotionTriggerRuntime : IDisposable
    {
        private readonly IUISurface _surface;
        private readonly DesignerScreenMotionMetadata _motion;
        private readonly Func<IUIMotionClipPlayer> _playerFactory;
        private readonly Func<bool> _reducedMotion;
        private readonly List<Action> _unsubscribe = new List<Action>();
        private readonly List<IUIMotionClipPlayer> _players = new List<IUIMotionClipPlayer>();
        private bool _disposed;

        /// <summary>Creates and immediately attaches a trigger runtime to a mounted surface.</summary>
        public DesignerMotionTriggerRuntime(IUISurface surface, DesignerScreenMotionMetadata motion,
            Func<IUIMotionClipPlayer> playerFactory = null, Func<bool> reducedMotion = null)
        {
            _surface = surface ?? throw new ArgumentNullException(nameof(surface));
            _motion = motion ?? throw new ArgumentNullException(nameof(motion));
            _playerFactory = playerFactory ?? (() => new UIMotionClipPlayer());
            _reducedMotion = reducedMotion ?? (() => false);
            AttachCapabilities();
        }

        /// <summary>Plays the screen entry clip and all ScreenEnter bindings.</summary>
        public void Enter()
        {
            Play(_motion.entryClip);
            Notify(DesignerMotionTrigger.ScreenEnter);
        }

        /// <summary>Plays all ScreenExit bindings followed by the screen exit clip.</summary>
        public void Exit()
        {
            Notify(DesignerMotionTrigger.ScreenExit);
            Play(_motion.exitClip);
        }

        /// <summary>
        /// Dispatches a lifecycle trigger that is owned outside element capabilities. Null filter
        /// values match every binding for that trigger.
        /// </summary>
        public void Notify(DesignerMotionTrigger trigger, string targetElementId = null,
            string stateId = null, string commandId = null)
        {
            if (_disposed || _motion.bindings == null) return;
            foreach (var binding in _motion.bindings)
            {
                if (binding == null || binding.trigger != trigger) continue;
                if (targetElementId != null && binding.targetElementId != targetElementId) continue;
                if (stateId != null && binding.stateId != stateId) continue;
                if (commandId != null && binding.commandId != commandId) continue;
                Play(BindingClip(binding));
            }
        }

        /// <summary>Unsubscribes every capability event and stops motion started by this runtime.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            for (var i = _unsubscribe.Count - 1; i >= 0; i--) _unsubscribe[i]();
            _unsubscribe.Clear();
            foreach (var player in _players) player.Stop();
            _players.Clear();
        }

        private void AttachCapabilities()
        {
            if (_motion.bindings == null) return;
            foreach (var binding in _motion.bindings)
            {
                if (binding == null || string.IsNullOrEmpty(binding.targetElementId)) continue;
                var handle = _surface.TryFind(binding.targetElementId);
                if (handle == null) continue;
                var captured = binding;
                switch (binding.trigger)
                {
                    case DesignerMotionTrigger.Click:
                    {
                        var capability = handle.As<IUIClickCapability>();
                        if (capability == null) break;
                        Action handler = () => Play(BindingClip(captured));
                        capability.Clicked += handler;
                        _unsubscribe.Add(() => capability.Clicked -= handler);
                        break;
                    }
                    case DesignerMotionTrigger.HoverEnter:
                    case DesignerMotionTrigger.HoverExit:
                    case DesignerMotionTrigger.PointerDown:
                    case DesignerMotionTrigger.PointerUp:
                    {
                        var capability = handle.As<IUIPointerCapability>();
                        if (capability == null) break;
                        Action handler = () => Play(BindingClip(captured));
                        if (binding.trigger == DesignerMotionTrigger.HoverEnter) capability.PointerEntered += handler;
                        else if (binding.trigger == DesignerMotionTrigger.HoverExit) capability.PointerExited += handler;
                        else if (binding.trigger == DesignerMotionTrigger.PointerDown) capability.PointerDown += handler;
                        else capability.PointerUp += handler;
                        _unsubscribe.Add(() =>
                        {
                            if (captured.trigger == DesignerMotionTrigger.HoverEnter) capability.PointerEntered -= handler;
                            else if (captured.trigger == DesignerMotionTrigger.HoverExit) capability.PointerExited -= handler;
                            else if (captured.trigger == DesignerMotionTrigger.PointerDown) capability.PointerDown -= handler;
                            else capability.PointerUp -= handler;
                        });
                        break;
                    }
                    case DesignerMotionTrigger.Selected:
                    case DesignerMotionTrigger.Deselected:
                    {
                        var capability = handle.As<IUIFocusCapability>();
                        if (capability == null) break;
                        Action handler = () => Play(BindingClip(captured));
                        if (binding.trigger == DesignerMotionTrigger.Selected) capability.Focused += handler;
                        else capability.Blurred += handler;
                        _unsubscribe.Add(() =>
                        {
                            if (captured.trigger == DesignerMotionTrigger.Selected) capability.Focused -= handler;
                            else capability.Blurred -= handler;
                        });
                        break;
                    }
                }
            }
        }

        private UIMotionClip BindingClip(DesignerMotionBinding binding)
            => _reducedMotion() && binding.reducedMotionClip != null ? binding.reducedMotionClip : binding.clip;

        private void Play(UIMotionClip clip)
        {
            if (_disposed || clip == null) return;
            var player = _playerFactory();
            if (player == null) return;
            _players.Add(player);
            player.PlayAsync(_surface, clip);
        }
    }
}
