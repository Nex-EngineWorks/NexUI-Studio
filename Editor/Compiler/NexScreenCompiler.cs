using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Diagnostics;
using emiteat.NexUI.Designer.Editor.Components;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Compiler
{
    /// <summary>
    /// Lowers an authoring screen into a compiled program the runtime can execute.
    /// </summary>
    /// <remarks>
    /// Four passes, in order, each one allowed to add diagnostics and none allowed to throw:
    /// <list type="number">
    /// <item><b>Normalize</b> - put the elements into a deterministic parents-first order.</item>
    /// <item><b>Validate</b> - every structural rule the runtime is then allowed to assume.</item>
    /// <item><b>Lower</b> - authoring types become the four node kinds, plus source map and features.</item>
    /// <item><b>Hash</b> - content hash over the canonical form, for caching and determinism tests.</item>
    /// </list>
    ///
    /// The order matters: lowering never re-checks what validation established, which is why the
    /// runtime builder can be a straight forward pass with no defensive branches. If validation
    /// found an error the program is still built, because a build report and an inspector are
    /// more useful with a partial program than with nothing - but it is never published.
    ///
    /// Everything here is pure with respect to the project: no asset is written, no importer is
    /// triggered, nothing is registered. That is what lets the same call run inside a background
    /// job, inside a test, and inside the incremental preview without three implementations.
    /// </remarks>
    public static class NexScreenCompiler
    {
        public static NexCompileResult Compile(DesignerMetadataAsset metadata)
            => Compile(metadata, NexCompileOptions.Default);

        public static NexCompileResult Compile(DesignerMetadataAsset metadata, NexCompileOptions options)
        {
            var stopwatch = Stopwatch.StartNew();
            var diagnostics = new NexDiagnosticBag();

            if (metadata == null)
            {
                diagnostics.Add(NexDiagnosticCodes.NoDocument);
                return new NexCompileResult(null, diagnostics, stopwatch.Elapsed.TotalMilliseconds);
            }

            var screenId = metadata.screenId ?? string.Empty;

            // One scope around the whole compile: every diagnostic below is attributed to the
            // Compile feature and to this run, without each pass having to say so.
            using (diagnostics.Scope(NexDiagnosticFeatures.Compile, nameof(NexScreenCompiler),
                       operationId: NewOperationId()))
            {
                if (string.IsNullOrEmpty(screenId))
                    diagnostics.Add(NexDiagnosticCodes.ScreenIdMissing, new NexSourceLocation(string.Empty),
                        detail: "Asset: " + metadata.name);

                var ordered = Normalize(metadata, screenId, diagnostics);
                var program = Lower(screenId, ordered, options, diagnostics);

                stopwatch.Stop();
                return new NexCompileResult(program, diagnostics, stopwatch.Elapsed.TotalMilliseconds);
            }
        }

        /// <summary>
        /// A short id grouping everything one compile produced.
        /// </summary>
        /// <remarks>
        /// Short on purpose: it is read off a console row and typed into a search box, not parsed.
        /// Uniqueness only has to hold within a session's log, which a handful of hex digits covers.
        /// </remarks>
        private static string NewOperationId()
            => "op-" + Guid.NewGuid().ToString("N").Substring(0, 8);

        // ---- pass 1: normalize ----------------------------------------------

        /// <summary>
        /// Depth-first, parents before children, siblings by <c>siblingIndex</c> then element id.
        /// </summary>
        /// <remarks>
        /// The element id tiebreak is not cosmetic. Two elements with the same sibling index are
        /// legal in the authoring model, and without a total order the compiler would emit a
        /// different node array depending on list order - which breaks the content hash, the
        /// compile cache and every screenshot test downstream of them.
        ///
        /// Elements that fail structural validation are dropped here rather than carried through
        /// as half-valid nodes, so later passes never have to ask whether a parent exists.
        /// </remarks>
        private static List<DesignerElementMetadata> Normalize(DesignerMetadataAsset metadata,
            string screenId, NexDiagnosticBag diagnostics)
        {
            var source = metadata.elements ?? new List<DesignerElementMetadata>();
            var byId = new Dictionary<string, DesignerElementMetadata>(source.Count, StringComparer.Ordinal);
            var valid = new List<DesignerElementMetadata>(source.Count);

            for (int i = 0; i < source.Count; i++)
            {
                var element = source[i];
                if (element == null) continue;

                if (string.IsNullOrEmpty(element.elementId))
                {
                    diagnostics.Add(NexDiagnosticCodes.ElementIdMissing,
                        new NexSourceLocation(screenId, element.stableId, element.displayName),
                        detail: "Type: " + element.elementType);
                    continue;
                }

                if (byId.ContainsKey(element.elementId))
                {
                    diagnostics.Add(NexDiagnosticCodes.DuplicateElementId,
                        new NexSourceLocation(screenId, element.stableId, element.elementId),
                        "Element id '" + element.elementId + "' is used more than once on this screen.");
                    continue;
                }

                byId[element.elementId] = element;
                valid.Add(element);
            }

            var children = new Dictionary<string, List<DesignerElementMetadata>>(StringComparer.Ordinal);
            var roots = new List<DesignerElementMetadata>();

            for (int i = 0; i < valid.Count; i++)
            {
                var element = valid[i];
                var parentId = element.parentId;

                if (string.IsNullOrEmpty(parentId))
                {
                    roots.Add(element);
                    continue;
                }

                if (!byId.ContainsKey(parentId))
                {
                    diagnostics.Add(NexDiagnosticCodes.ParentNotFound,
                        new NexSourceLocation(screenId, element.stableId, element.elementId, "parentId"),
                        "'" + element.elementId + "' is parented to '" + parentId + "', which is not on this screen.");
                    continue;
                }

                if (!children.TryGetValue(parentId, out var list))
                {
                    list = new List<DesignerElementMetadata>();
                    children[parentId] = list;
                }
                list.Add(element);
            }

            Comparison<DesignerElementMetadata> order = (a, b) =>
            {
                var bySibling = a.siblingIndex.CompareTo(b.siblingIndex);
                return bySibling != 0 ? bySibling : string.CompareOrdinal(a.elementId, b.elementId);
            };

            roots.Sort(order);
            foreach (var list in children.Values) list.Sort(order);

            var result = new List<DesignerElementMetadata>(valid.Count);
            var visiting = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < roots.Count; i++)
                Emit(roots[i], children, result, visiting, screenId, diagnostics);

            ReportUnreachable(valid, byId, result, screenId, diagnostics);

            if (result.Count == 0 && diagnostics.MaxSeverity < NexSeverity.Error)
                diagnostics.Add(NexDiagnosticCodes.EmptyScreen, new NexSourceLocation(screenId));

            return result;
        }

        /// <summary>
        /// Reports elements that survived id and parent checks but were never reached from a root.
        /// </summary>
        /// <remarks>
        /// With parent pointers, a cycle is unreachable from any root by construction - so the
        /// depth-first guard inside <see cref="Emit"/> never fires for one, and without this pass
        /// a screen whose elements form a ring would compile to nothing and be reported only as
        /// "empty". Naming the elements that vanished is the difference between a fixable error
        /// and a mystery.
        /// </remarks>
        private static void ReportUnreachable(List<DesignerElementMetadata> valid,
            Dictionary<string, DesignerElementMetadata> byId, List<DesignerElementMetadata> emitted,
            string screenId, NexDiagnosticBag diagnostics)
        {
            if (emitted.Count == valid.Count) return;

            var reached = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < emitted.Count; i++) reached.Add(emitted[i].elementId);

            for (int i = 0; i < valid.Count; i++)
            {
                var element = valid[i];
                if (reached.Contains(element.elementId)) continue;

                // A missing parent was already reported as NEX-DOC-1004; anything else that is
                // unreachable is in a parent ring.
                if (string.IsNullOrEmpty(element.parentId) || !byId.ContainsKey(element.parentId)) continue;

                diagnostics.Add(NexDiagnosticCodes.ParentCycle,
                    new NexSourceLocation(screenId, element.stableId, element.elementId, "parentId"),
                    "'" + element.elementId + "' is inside a parent cycle and cannot be placed on the screen.");
            }
        }

        /// <summary>
        /// Emits one element then its children. <paramref name="visiting"/> is the cycle guard:
        /// an element already on the current path is reported once and its subtree abandoned,
        /// rather than recursing until the stack gives out.
        /// </summary>
        private static void Emit(DesignerElementMetadata element,
            Dictionary<string, List<DesignerElementMetadata>> children,
            List<DesignerElementMetadata> result, HashSet<string> visiting,
            string screenId, NexDiagnosticBag diagnostics)
        {
            if (!visiting.Add(element.elementId))
            {
                diagnostics.Add(NexDiagnosticCodes.ParentCycle,
                    new NexSourceLocation(screenId, element.stableId, element.elementId, "parentId"),
                    "'" + element.elementId + "' is its own ancestor.");
                return;
            }

            result.Add(element);

            if (children.TryGetValue(element.elementId, out var list))
                for (int i = 0; i < list.Count; i++)
                    Emit(list[i], children, result, visiting, screenId, diagnostics);

            visiting.Remove(element.elementId);
        }

        // ---- pass 2 + 3: validate and lower ---------------------------------

        private static NexScreenProgram Lower(string screenId, List<DesignerElementMetadata> ordered,
            NexCompileOptions options, NexDiagnosticBag diagnostics)
        {
            var nodes = new NexNodeProgram[ordered.Count];
            var sourceMap = new NexSourceMap();
            var features = new NexFeatureManifest();

            var indexById = new Dictionary<string, int>(ordered.Count, StringComparer.Ordinal);
            var pathById = new Dictionary<string, string>(ordered.Count, StringComparer.Ordinal);

            // Automation ids must be unique or a test's lookup is a coin flip. Checked here rather
            // than in a per-element rule because uniqueness is a property of the whole screen.
            var automationIds = new Dictionary<string, string>(StringComparer.Ordinal);

            for (int i = 0; i < ordered.Count; i++)
                indexById[ordered[i].elementId] = i;

            for (int i = 0; i < ordered.Count; i++)
            {
                var element = ordered[i];
                var location = new NexSourceLocation(screenId, element.stableId, element.elementId);

                var path = element.elementId;
                if (!string.IsNullOrEmpty(element.parentId) && pathById.TryGetValue(element.parentId, out var parentPath))
                    path = parentPath + "/" + element.elementId;
                pathById[element.elementId] = path;

                var kind = ResolveKind(element, location, diagnostics);
                var binding = element.binding;

                nodes[i] = new NexNodeProgram
                {
                    NodeId = element.stableId,
                    Name = element.elementId,
                    ParentIndex = !string.IsNullOrEmpty(element.parentId) && indexById.TryGetValue(element.parentId, out var pi)
                        ? pi
                        : -1,
                    Kind = kind,
                    Rect = element.rect,
                    Anchor = (NexAnchor)element.anchorPreset,
                    Tint = element.tint,
                    TextColor = element.textColor,
                    FontSize = element.fontSize,
                    Text = element.text ?? string.Empty,
                    Visible = element.runtimeVisible,
                    TextBindingKey = LowerTextBinding(element, kind, location, diagnostics),
                    CommandId = LowerCommandBinding(element, kind, location, diagnostics),
                    ValueBindingKey = binding?.valueKey ?? string.Empty,
                    VisibilityBindingKey = binding?.visibilityKey ?? string.Empty,
                    InteractableBindingKey = binding?.interactableKey ?? string.Empty,
                    ClassBindingKey = binding?.classKey ?? string.Empty,
                    TextBindingMode = binding?.textMode ?? State.UIBindingMode.OneWay,
                    ValueBindingMode = binding?.valueMode ?? State.UIBindingMode.OneWay,
                    TextConverterKey = binding?.textConverterKey ?? string.Empty,
                    ValueConverterKey = binding?.valueConverterKey ?? string.Empty,
                    Capabilities = ResolveCapabilities(element, kind),
                    ControlId = ControlIdOf(element) ?? string.Empty,
                    ValueMin = ValueMinOf(element),
                    ValueMax = ValueMaxOf(element),
                    ControlProperties = CollectProperties(element),
                    // Cloned, and only when authored: the program is a separate asset, and handing
                    // it the element's own instance would make a later pen edit silently rewrite
                    // already-published geometry without changing the content hash.
                    Shape = element.hasShape ? element.vectorShape?.Clone() : null,
                    AutomationId = element.automationId ?? string.Empty,
                    Role = element.accessibilityRole,
                    AccessibilityLabel = element.accessibilityLabel ?? string.Empty,
                    FocusOrder = -1
                };

                CheckBindings(nodes[i], location, diagnostics);

                // Nested scope: inherits the Compile operation id, reports under Accessibility so
                // a missing label is filed with the other accessibility findings rather than
                // buried among structural compile errors.
                using (diagnostics.Scope(NexDiagnosticFeatures.Accessibility, handler: element.elementId))
                    CheckAccessibleName(nodes[i], location, diagnostics);

                if (!string.IsNullOrEmpty(element.automationId))
                {
                    if (automationIds.TryGetValue(element.automationId, out var owner))
                        diagnostics.Add(NexDiagnosticCodes.DuplicateAutomationId,
                            location.WithMember("automationId"),
                            "Automation id '" + element.automationId + "' is used by both '" + owner +
                            "' and '" + element.elementId + "'.");
                    else
                        automationIds[element.automationId] = element.elementId;
                }

                sourceMap.Add(element.stableId, element.elementId, i, path);
                RequireFeatures(features, nodes[i], path);
            }

            AssignFocusOrder(nodes);

            NexInteractionProgram interactions;
            using (diagnostics.Scope(NexDiagnosticFeatures.Interaction))
                interactions = LowerInteractions(ordered, nodes, indexById, pathById, screenId, features, diagnostics);

            var program = ScriptableObject.CreateInstance<NexScreenProgram>();
            program.name = string.IsNullOrEmpty(screenId) ? "NexScreenProgram" : screenId;
            program.Initialize(screenId, nodes, sourceMap, features, options.ResolvedReferenceResolution,
                string.Empty, interactions);

            // The hash covers the finished program, so it changes when and only when something
            // that affects runtime behaviour changed - including compiler version, which is what
            // forces a rebuild after the compiler itself is updated.
            program.Initialize(screenId, nodes, sourceMap, features, options.ResolvedReferenceResolution,
                ComputeHash(program.ToCanonicalString()), interactions);

            return program;
        }

        // ---- interaction lowering -------------------------------------------

        /// <summary>
        /// Turns authored trigger / condition / action rules into a resolved interaction program.
        /// </summary>
        /// <remarks>
        /// Everything that could fail at runtime is decided here instead: element ids become node
        /// indices, values are parsed once, and a rule that cannot possibly run is reported and
        /// dropped rather than compiled into a screen that quietly does nothing. That is what lets
        /// <c>NexInteractionRuntime</c> be a scan and a switch with no lookups and no parsing on
        /// the click path.
        ///
        /// A rule is dropped as a unit. Compiling three of its four actions would produce a screen
        /// that half-works, which is harder to diagnose than one that visibly does not.
        /// </remarks>
        private static NexInteractionProgram LowerInteractions(
            List<DesignerElementMetadata> ordered,
            NexNodeProgram[] nodes,
            Dictionary<string, int> indexById,
            Dictionary<string, string> pathById,
            string screenId,
            NexFeatureManifest features,
            NexDiagnosticBag diagnostics)
        {
            var program = new NexInteractionProgram();

            // Which elements have descendants, so a Capture/Bubble rule can be told at compile time
            // that nothing will ever reach it.
            var hasDescendants = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < ordered.Count; i++)
                if (!string.IsNullOrEmpty(ordered[i].parentId)) hasDescendants.Add(ordered[i].parentId);

            for (int i = 0; i < ordered.Count; i++)
            {
                var element = ordered[i];
                if (element.interactions == null || element.interactions.Count == 0) continue;

                var path = pathById.TryGetValue(element.elementId, out var p) ? p : element.elementId;
                var location = new NexSourceLocation(screenId, element.stableId, path, "interaction");

                for (int r = 0; r < element.interactions.Count; r++)
                {
                    var rule = element.interactions[r];
                    if (rule == null || !rule.enabled) continue;

                    if (rule.phase != DesignerInteractionPhase.Target)
                    {
                        // Capture and Bubble deliver an event raised *below* this element, so the
                        // element itself need not be clickable - but something must be under it,
                        // and the trigger must be one that travels.
                        if (!Propagates(rule.trigger) || !hasDescendants.Contains(element.elementId))
                        {
                            diagnostics.Add(NexDiagnosticCodes.InteractionPhaseUnreachable, location,
                                "'" + element.elementId + "' listens on " + rule.phase + " for " + rule.trigger +
                                ", which can never reach it; the rule was dropped.",
                                !Propagates(rule.trigger)
                                    ? rule.trigger + " does not propagate."
                                    : "The element has no children.");
                            continue;
                        }
                    }
                    else if (!CanRaise(nodes[i], rule.trigger))
                    {
                        diagnostics.Add(NexDiagnosticCodes.TriggerNotRaisableByNode, location,
                            "'" + element.elementId + "' cannot raise " + rule.trigger + "; the rule was dropped.",
                            "Node kind: " + nodes[i].Kind);
                        continue;
                    }

                    if (rule.actions == null || rule.actions.Count == 0)
                    {
                        diagnostics.Add(NexDiagnosticCodes.InteractionHasNoActions, location,
                            "Rule '" + RuleName(rule) + "' on '" + element.elementId + "' has no actions.");
                        continue;
                    }

                    var lowered = new List<NexInteractionAction>(rule.actions.Count);
                    var ok = true;

                    for (int a = 0; a < rule.actions.Count && ok; a++)
                        ok = TryLowerAction(rule.actions[a], rule, element, indexById, nodes,
                            location, diagnostics, lowered);

                    if (!ok) continue;

                    ParseValue(rule.conditionValue, out var conditionNumber, out var conditionIsNumeric);

                    program.Rules.Add(new NexInteractionRule
                    {
                        RuleId = rule.ruleId,
                        NodeIndex = i,
                        Trigger = (NexTrigger)rule.trigger,
                        Phase = (NexPhase)rule.phase,
                        StopsPropagation = rule.stopPropagation,
                        HasCondition = rule.HasCondition,
                        ConditionKey = rule.conditionKey ?? string.Empty,
                        Comparison = (NexComparison)rule.comparison,
                        ConditionString = rule.conditionValue ?? string.Empty,
                        ConditionNumber = conditionNumber,
                        ConditionIsNumeric = conditionIsNumeric,
                        ActionStart = program.Actions.Count,
                        ActionCount = lowered.Count
                    });

                    program.Actions.AddRange(lowered);

                    features.Require(Compiled.NexFeatures.Interaction, element.stableId,
                        path + " runs a rule on " + rule.trigger + ".");
                }
            }

            return program;
        }

        private static bool TryLowerAction(
            DesignerInteractionAction action,
            DesignerInteractionRule rule,
            DesignerElementMetadata owner,
            Dictionary<string, int> indexById,
            NexNodeProgram[] nodes,
            NexSourceLocation location,
            NexDiagnosticBag diagnostics,
            List<NexInteractionAction> output)
        {
            if (action == null)
            {
                diagnostics.Add(NexDiagnosticCodes.InteractionActionIncomplete, location,
                    "Rule '" + RuleName(rule) + "' on '" + owner.elementId + "' has an empty action.");
                return false;
            }

            ParseValue(action.value, out var number, out var isNumeric);

            var lowered = new NexInteractionAction
            {
                Kind = (NexActionKind)action.kind,
                CommandId = action.commandId ?? string.Empty,
                StateKey = action.stateKey ?? string.Empty,
                StringValue = action.value ?? string.Empty,
                NumberValue = number,
                IsNumeric = isNumeric,
                BoolValue = action.boolValue,
                TargetNodeIndex = -1,
                Seconds = action.seconds
            };

            switch (lowered.Kind)
            {
                case NexActionKind.ExecuteCommand:
                    if (string.IsNullOrEmpty(lowered.CommandId))
                        return Incomplete(diagnostics, location, rule, owner, "a command id");
                    break;

                case NexActionKind.SetState:
                    if (string.IsNullOrEmpty(lowered.StateKey))
                        return Incomplete(diagnostics, location, rule, owner, "a state key");
                    break;

                case NexActionKind.Delay:
                    // A delay that ends the rule pauses and then does nothing, which is always a
                    // mistake - the author meant to put something after it.
                    if (rule.actions[rule.actions.Count - 1] == action)
                        diagnostics.Add(NexDiagnosticCodes.InteractionHasNoActions, location,
                            "Rule '" + RuleName(rule) + "' on '" + owner.elementId +
                            "' ends with a delay, so the wait leads to nothing.");
                    break;

                case NexActionKind.SetVisible:
                case NexActionKind.SetText:
                {
                    if (string.IsNullOrEmpty(action.targetElementId))
                        return Incomplete(diagnostics, location, rule, owner, "a target element");

                    if (!indexById.TryGetValue(action.targetElementId, out var targetIndex))
                    {
                        diagnostics.Add(NexDiagnosticCodes.InteractionTargetNotFound, location,
                            "Rule '" + RuleName(rule) + "' targets '" + action.targetElementId +
                            "', which is not on this screen.");
                        return false;
                    }

                    lowered.TargetNodeIndex = targetIndex;

                    // Text aimed at an element that draws none is the same authoring mistake a
                    // text binding on a Panel is, so it reuses that code rather than inventing a
                    // near-duplicate one. It is a warning: the rest of the rule still works.
                    if (lowered.Kind == NexActionKind.SetText && !nodes[targetIndex].HasText)
                        diagnostics.Add(NexDiagnosticCodes.TextBindingOnNonTextNode, location,
                            "Rule '" + RuleName(rule) + "' sets text on '" + action.targetElementId +
                            "', which draws no text.");
                    break;
                }
            }

            output.Add(lowered);
            return true;
        }

        private static bool Incomplete(NexDiagnosticBag diagnostics, NexSourceLocation location,
            DesignerInteractionRule rule, DesignerElementMetadata owner, string missing)
        {
            diagnostics.Add(NexDiagnosticCodes.InteractionActionIncomplete, location,
                "Rule '" + RuleName(rule) + "' on '" + owner.elementId + "' has an action missing " + missing + ".");
            return false;
        }

        /// <summary>
        /// Whether a trigger travels up and down the element tree.
        /// </summary>
        /// <remarks>
        /// Everything a node raises propagates; only the screen lifecycle does not. Show and hide
        /// are delivered to every node already, so a Bubble rule for them would fire on the same
        /// event the Target rule did - not propagation, just a duplicate.
        /// </remarks>
        private static bool Propagates(DesignerInteractionTrigger trigger)
            => trigger != DesignerInteractionTrigger.OnShow && trigger != DesignerInteractionTrigger.OnHide;

        /// <summary>Which nodes can actually raise which trigger.</summary>
        /// <remarks>
        /// Show and hide belong to the screen's lifecycle so any node can be told about them.
        /// A click has to come from something clickable, and authoring one on a Label produces a
        /// rule that would never fire - caught here rather than left as a mystery at runtime.
        ///
        /// Submit and cancel need more than clickability: they are delivered to whatever currently
        /// holds focus, so a node that cannot be focused can never receive them. Pointer and drag
        /// triggers ask for less - anything the raycaster can hit will do - so they are not
        /// restricted here, because whether a node has a raycast target depends on styling the
        /// compiler does not own.
        ///
        /// A close request needs something that closes, which is the overlay capability.
        /// </remarks>
        private static bool CanRaise(in NexNodeProgram node, DesignerInteractionTrigger trigger)
        {
            switch (trigger)
            {
                case DesignerInteractionTrigger.OnClick:
                case DesignerInteractionTrigger.OnSubmit:
                case DesignerInteractionTrigger.OnCancel:
                    return node.Kind == NexNodeKind.Button;

                case DesignerInteractionTrigger.OnCloseRequested:
                    // Only something that closes can be asked to. On anything else the rule would
                    // sit there looking authored and never run.
                    return node.IsOverlay;

                default:
                    return true;
            }
        }

        private static string RuleName(DesignerInteractionRule rule)
            => !string.IsNullOrEmpty(rule.displayName) ? rule.displayName : rule.trigger.ToString();

        /// <summary>
        /// Parses an authored value once, at compile time. Invariant culture so a screen authored
        /// on a machine with a comma decimal separator compiles to the same program everywhere.
        /// </summary>
        private static void ParseValue(string text, out double number, out bool isNumeric)
        {
            if (!string.IsNullOrEmpty(text) &&
                double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
            {
                isNumeric = true;
                return;
            }

            number = 0d;
            isNumeric = false;
        }

        /// <summary>
        /// Maps an authoring type onto one of the four node kinds via the component registry's
        /// uGUI control name.
        /// </summary>
        /// <remarks>
        /// Going through <c>UGUIControl</c> rather than the type id directly means a new authoring
        /// component that declares itself a Button compiles correctly with no change here. An
        /// unrecognised control is a warning, not an error, and lowers to a plain panel: the
        /// screen still opens, the element is still positioned and still in the hierarchy, and the
        /// build report says exactly what was lost. Failing the whole screen instead would make
        /// one unsupported decoration block a release.
        /// </remarks>
        // ---- bindings --------------------------------------------------------

        /// <summary>
        /// Reports bindings that were authored but cannot do what they say.
        /// </summary>
        /// <remarks>
        /// All of these are warnings, and all of them keep the binding in the compiled program.
        /// Dropping a binding the author wrote is how a screen ends up silently doing nothing while
        /// the Inspector still shows the key - the failure mode this whole pass exists to replace.
        /// The program carries the intent; the diagnostics say what part of it the backend cannot
        /// honour yet.
        /// </remarks>
        private static void CheckBindings(NexNodeProgram node, NexSourceLocation location,
            NexDiagnosticBag diagnostics)
        {
            using (diagnostics.Scope(NexDiagnosticFeatures.Binding, handler: node.Name))
            {
                // Only a node with no value capability at all. A slider or a toggle carries one,
                // so binding a value to it is exactly right and says nothing.
                if (!string.IsNullOrEmpty(node.ValueBindingKey) && !node.HasValue)
                    diagnostics.Add(NexDiagnosticCodes.ValueBindingHasNoBackendTarget,
                        location.WithMember("binding.valueKey"),
                        "'" + node.Name + "' binds a value to '" + node.ValueBindingKey +
                        "' but holds no value; it compiles to a " + node.Kind + " with no control.");

                // Write-back needs something the user can operate.
                if (node.TextWritesBack && !node.IsUserEditable)
                    diagnostics.Add(NexDiagnosticCodes.TwoWayBindingOnReadOnlyNode,
                        location.WithMember("binding.textMode"),
                        "'" + node.Name + "' binds text two-way, but nothing on it lets the user edit text.");

                if (node.ValueWritesBack && !node.IsUserEditable)
                    diagnostics.Add(NexDiagnosticCodes.TwoWayBindingOnReadOnlyNode,
                        location.WithMember("binding.valueMode"),
                        "'" + node.Name + "' binds a value two-way, but nothing on it lets the user change it.");

                if (!string.IsNullOrEmpty(node.TextConverterKey) && string.IsNullOrEmpty(node.TextBindingKey))
                    diagnostics.Add(NexDiagnosticCodes.ConverterKeyWithoutBinding,
                        location.WithMember("binding.textConverterKey"),
                        "Text converter '" + node.TextConverterKey + "' is set, but no text is bound.");

                if (!string.IsNullOrEmpty(node.ValueConverterKey) && string.IsNullOrEmpty(node.ValueBindingKey))
                    diagnostics.Add(NexDiagnosticCodes.ConverterKeyWithoutBinding,
                        location.WithMember("binding.valueConverterKey"),
                        "Value converter '" + node.ValueConverterKey + "' is set, but no value is bound.");
            }
        }

        // ---- accessibility ---------------------------------------------------

        /// <summary>
        /// Reports nodes that assistive technology would reach but could not describe.
        /// </summary>
        /// <remarks>
        /// A warning rather than an error. An unnamed button is a real defect - it is the icon-only
        /// close button a screen reader announces as "button", with nothing to say which one - but
        /// failing the compile would block a screen that is still being laid out, and the fastest
        /// way to teach someone to disable a check is to have it stop their work.
        /// </remarks>
        private static void CheckAccessibleName(NexNodeProgram node, NexSourceLocation location,
            NexDiagnosticBag diagnostics)
        {
            if (!string.IsNullOrEmpty(node.AccessibleName)) return;

            if (node.IsClickable || IsInteractiveRole(node.Role))
            {
                diagnostics.Add(NexDiagnosticCodes.InteractiveNodeHasNoAccessibleName,
                    location.WithMember("accessibilityLabel"),
                    "'" + node.Name + "' can be operated but announces nothing.");
                return;
            }

            // Role.Image is the author saying the picture carries meaning. Decorative art is
            // Role.None, and stays silent without a complaint.
            if (node.Role == emiteat.NexUI.Accessibility.AccessibilityRole.Image)
                diagnostics.Add(NexDiagnosticCodes.ImageRoleWithoutLabel,
                    location.WithMember("accessibilityLabel"),
                    "'" + node.Name + "' is marked as a meaningful image but has no label.");
        }

        private static bool IsInteractiveRole(emiteat.NexUI.Accessibility.AccessibilityRole role)
            => role == emiteat.NexUI.Accessibility.AccessibilityRole.Button
               || role == emiteat.NexUI.Accessibility.AccessibilityRole.Toggle
               || role == emiteat.NexUI.Accessibility.AccessibilityRole.Slider
               || role == emiteat.NexUI.Accessibility.AccessibilityRole.TextField;

        /// <summary>
        /// Numbers the focusable nodes in document order.
        /// </summary>
        /// <remarks>
        /// Document order is the reading order the author already sees in the hierarchy panel, so
        /// the announcement sequence matches what they arranged rather than the order nodes happen
        /// to sit in a serialized list. Nodes are numbered after lowering because focusability
        /// depends on the resolved kind, not on the authoring type.
        ///
        /// Non-focusable nodes keep -1: a panel that exists only to group children should not be
        /// stopped on by Tab, and should not be announced as a thing of its own.
        /// </remarks>
        private static void AssignFocusOrder(NexNodeProgram[] nodes)
        {
            var order = 0;
            for (var i = 0; i < nodes.Length; i++)
                nodes[i].FocusOrder = TakesFocus(nodes[i]) ? order++ : -1;
        }

        private static bool TakesFocus(NexNodeProgram node)
            => node.IsClickable
               || IsInteractiveRole(node.Role)
               || (node.Role != emiteat.NexUI.Accessibility.AccessibilityRole.None
                   && node.Role != emiteat.NexUI.Accessibility.AccessibilityRole.Container
                   && !string.IsNullOrEmpty(node.AccessibleName));

        private static NexNodeKind ResolveKind(DesignerElementMetadata element,
            NexSourceLocation location, NexDiagnosticBag diagnostics)
        {
            if (!DesignerComponentRegistry.IsRegistered(element.elementType))
            {
                diagnostics.Add(NexDiagnosticCodes.UnknownElementType, location,
                    "Element type '" + element.elementType + "' is not registered.");
                return NexNodeKind.Panel;
            }

            var control = DesignerComponentRegistry.Get(element.elementType).UGUIControl;

            switch (control)
            {
                case "Button":
                case "ButtonTMP":
                    return NexNodeKind.Button;

                case "Text":
                case "TextTMP":
                    return NexNodeKind.Label;

                case "Image":
                case "RawImage":
                    return NexNodeKind.Image;

                case null:
                case "":
                case "Panel":
                case "Mask":
                    return NexNodeKind.Panel;

                default:
                    // Controls that are not one of the four visual kinds still compile: the kind
                    // describes what is drawn, and ResolveCapabilities says what it can do. Only
                    // a control nothing knows how to build reaches the diagnostic below.
                    if (ControlIdOf(element) != null) return NexNodeKind.Panel;

                    diagnostics.Add(NexDiagnosticCodes.BackendUnsupportedNode, location,
                        "'" + element.elementType + "' has no compiled representation yet; it becomes a panel.",
                        "uGUI control: " + control);
                    return NexNodeKind.Panel;
            }
        }

        /// <summary>
        /// Collects the control settings the author actually changed.
        /// </summary>
        /// <remarks>
        /// Only overridden properties are emitted. A schema default belongs to the control, and
        /// writing it into the program would make the compiled asset churn whenever a default
        /// changes, defeat the content hash, and let this compiler's idea of a default override
        /// the backend's.
        ///
        /// Asset, element-reference and serialized properties are skipped: the compiled program is
        /// a value type with no object graph, which is what lets it load without patching up
        /// references. Those stay on the prefab path until the program grows an asset table.
        /// </remarks>
        /// <summary>
        /// Resolves how a value component fills, so the runtime does not have to.
        /// </summary>
        /// <remarks>
        /// The direction lives in two places - the property bag and the older <c>fill</c> record -
        /// and the prefab writer picks between them with the bag winning. Resolving it here rather
        /// than at runtime means that precedence is decided once, in the one place that can see
        /// both, instead of being reimplemented by every backend.
        ///
        /// Emitted as ordinary properties because the bag already reaches the runtime and already
        /// counts toward the content hash. These are appended after the authored bag, and lookup
        /// returns the first match, so an authored entry of the same key still wins.
        /// </remarks>
        private static List<NexNodeProperty> FillProperties(DesignerElementMetadata element)
        {
            var properties = new List<NexNodeProperty>();
            if (element?.fill == null) return properties;
            if (!DesignerComponentRegistry.IsRegistered(element.elementType)) return properties;
            if (!DesignerComponentRegistry.Get(element.elementType).IsValueComponent) return properties;

            properties.Add(NexNodeProperty.OfText("value.direction", element.fill.direction.ToString()));
            properties.Add(NexNodeProperty.OfFlag("value.clockwise", element.fill.clockwise));
            return properties;
        }

        private static NexNodeProperty[] CollectProperties(DesignerElementMetadata element)
        {
            var stored = element?.componentProperties;
            var fill = FillProperties(element);

            if ((stored == null || stored.Count == 0) && fill.Count == 0) return Array.Empty<NexNodeProperty>();

            var collected = new List<NexNodeProperty>((stored?.Count ?? 0) + fill.Count);
            if (stored == null) return fill.ToArray();

            // The stored bag, not the schema. Walking the schema misses anything the author set
            // that the current schema does not declare - a property from a newer Studio, or one a
            // component contributes outside the palette descriptor - and the authoring model
            // exists precisely so those survive a round trip rather than being silently dropped.
            // The stored value carries its own type, so no schema lookup is needed to read it.
            for (var i = 0; i < stored.Count; i++)
            {
                var entry = stored[i];
                if (entry == null || string.IsNullOrEmpty(entry.key) || entry.value == null) continue;

                var key = entry.key;
                var value = entry.value;

                switch (value.type)
                {
                    case DesignerPropertyValueType.Float:
                        collected.Add(NexNodeProperty.OfNumber(key, value.floatValue));
                        break;

                    case DesignerPropertyValueType.Integer:
                        collected.Add(NexNodeProperty.OfNumber(key, value.intValue));
                        break;

                    case DesignerPropertyValueType.Boolean:
                        collected.Add(NexNodeProperty.OfFlag(key, value.boolValue));
                        break;

                    case DesignerPropertyValueType.String:
                        collected.Add(NexNodeProperty.OfText(key, value.stringValue));
                        break;

                    case DesignerPropertyValueType.Enum:
                        // By name, not by index: an index means something else the moment a member
                        // is inserted, and the runtime maps names for the same reason.
                        collected.Add(NexNodeProperty.OfText(key, value.stringValue));
                        break;

                    case DesignerPropertyValueType.Color:
                        collected.Add(NexNodeProperty.OfColor(key, value.colorValue));
                        break;

                    case DesignerPropertyValueType.Vector2:
                        collected.Add(NexNodeProperty.OfVector(key, value.vector2Value));
                        break;

                    // Asset, element reference and serialized values are skipped: the compiled
                    // program is a value type with no object graph, which is what lets it load
                    // without patching references. Those stay on the prefab path.
                }
            }

            // Appended last, and lookup returns the first match - so an authored entry of the same
            // key sits earlier and wins. That is the precedence the prefab writer already uses.
            collected.AddRange(fill);

            return collected.Count == 0 ? Array.Empty<NexNodeProperty>() : collected.ToArray();
        }

        /// <summary>
        /// The control key a node carries, or null when it is only a visual.
        /// </summary>
        /// <remarks>
        /// Read from the palette descriptor rather than from a fixed list here, so a control added
        /// to the registry becomes compilable without editing the compiler. The key travels into
        /// the program as <see cref="NexNodeProgram.ControlId"/>; a backend maps it to its own type.
        /// </remarks>
        /// <summary>
        /// The value range a control operates over.
        /// </summary>
        /// <remarks>
        /// Read from the authored data rather than hardcoded. These were fixed at 0 and 1, which
        /// meant a slider authored to run 0-100 arrived at the runtime as 0-1 and a progress bar
        /// had no range at all - the prefab writer honoured both and the compiled screen did not.
        ///
        /// The property bag wins over the fill record, matching <c>ApplyValueFill</c>: an authored
        /// <c>value.min</c> is an explicit decision and the fill record is the older default.
        /// </remarks>
        private static float ValueMinOf(DesignerElementMetadata element)
            => DesignerComponentPropertyAccess.GetFloat(element, "value.min", element.fill?.minValue ?? 0f);

        private static float ValueMaxOf(DesignerElementMetadata element)
        {
            var minimum = ValueMinOf(element);
            var maximum = DesignerComponentPropertyAccess.GetFloat(
                element, "value.max", element.fill?.maxValue ?? 1f);

            // A range that does not increase would make every value normalise to the same point,
            // so the runtime would show a control that never moves.
            return maximum > minimum ? maximum : minimum + 1f;
        }

        private static string ControlIdOf(DesignerElementMetadata element)
        {
            if (!DesignerComponentRegistry.IsRegistered(element.elementType)) return null;

            var descriptor = DesignerComponentRegistry.Get(element.elementType);
            var control = descriptor.UGUIControl;

            if (string.IsNullOrEmpty(control))
            {
                // A value or overlay component with no Unity control behind it - a progress bar is
                // a filled Image, not a Slider, and a modal is a panel that opens. The type id is
                // the control id in those cases, which lets the backend build the right thing while
                // the program stays backend-neutral.
                return descriptor.IsValueComponent || descriptor.IsOverlayComponent
                    ? element.elementType
                    : null;
            }

            switch (control)
            {
                case "Slider":
                case "Scrollbar":
                case "Toggle":
                case "Dropdown":
                case "DropdownTMP":
                case "InputField":
                case "InputFieldTMP":
                    return control;
                default:
                    return null;
            }
        }

        /// <summary>
        /// What the node can do, from the control it carries plus the visual kind.
        /// </summary>
        /// <remarks>
        /// Capabilities rather than more node kinds. A slider and a scrollbar differ in
        /// appearance and not at all in what a binding does with them, so they report the same
        /// capabilities and the binding code has one path instead of two.
        /// </remarks>
        private static NexNodeCapabilities ResolveCapabilities(DesignerElementMetadata element, NexNodeKind kind)
        {
            var capabilities = NexNodeCapabilities.None;

            if (kind == NexNodeKind.Label || kind == NexNodeKind.Button) capabilities |= NexNodeCapabilities.Text;
            if (kind == NexNodeKind.Button) capabilities |= NexNodeCapabilities.Click;

            // A drawn path replaces the node's rect fill rather than adding to it. The capability
            // is what the backend switches on; the kind still says what else the node does, so a
            // button can carry a custom shape and stay a button.
            if (element.hasShape && element.vectorShape != null && !element.vectorShape.IsEmpty)
                capabilities |= NexNodeCapabilities.Vector;

            // An overlay keeps whatever else it is - a modal is still a panel that can carry a
            // binding - so this is added to the capabilities rather than replacing them.
            if (DesignerComponentRegistry.IsRegistered(element.elementType) &&
                DesignerComponentRegistry.Get(element.elementType).IsOverlayComponent)
            {
                capabilities |= NexNodeCapabilities.Overlay;
            }

            switch (ControlIdOf(element))
            {
                case "Slider":
                case "Scrollbar":
                    capabilities |= NexNodeCapabilities.Value | NexNodeCapabilities.UserEditable;
                    break;

                case "Toggle":
                    capabilities |= NexNodeCapabilities.Value | NexNodeCapabilities.BooleanValue
                                    | NexNodeCapabilities.UserEditable | NexNodeCapabilities.Click;
                    break;

                case "Dropdown":
                case "DropdownTMP":
                    capabilities |= NexNodeCapabilities.Value | NexNodeCapabilities.UserEditable;
                    break;

                case "InputField":
                case "InputFieldTMP":
                    capabilities |= NexNodeCapabilities.Text | NexNodeCapabilities.UserEditable;
                    break;

                case "ProgressBar":
                case "StatBar":
                case "RadialFill":
                    // Value but not UserEditable: these display a number and never report one back.
                    // Marking them editable would let a two-way binding write from a control the
                    // user cannot touch, which is a loop with no author behind it.
                    capabilities |= NexNodeCapabilities.Value;
                    break;
            }

            return capabilities;
        }

        private static string LowerTextBinding(DesignerElementMetadata element, NexNodeKind kind,
            NexSourceLocation location, NexDiagnosticBag diagnostics)
        {
            var key = element.binding != null ? element.binding.textKey : null;
            if (string.IsNullOrEmpty(key)) return string.Empty;

            if (kind == NexNodeKind.Label || kind == NexNodeKind.Button) return key;

            diagnostics.Add(NexDiagnosticCodes.TextBindingOnNonTextNode, location.WithMember("binding.textKey"),
                "'" + element.elementId + "' binds text to '" + key + "' but draws no text.");
            return string.Empty;
        }

        private static string LowerCommandBinding(DesignerElementMetadata element, NexNodeKind kind,
            NexSourceLocation location, NexDiagnosticBag diagnostics)
        {
            var commandId = element.binding != null ? element.binding.commandKey : null;
            if (string.IsNullOrEmpty(commandId)) return string.Empty;

            if (kind == NexNodeKind.Button) return commandId;

            diagnostics.Add(NexDiagnosticCodes.CommandOnNonClickableNode, location.WithMember("binding.commandKey"),
                "'" + element.elementId + "' dispatches '" + commandId + "' but cannot be clicked.");
            return string.Empty;
        }

        private static void RequireFeatures(NexFeatureManifest features, NexNodeProgram node, string path)
        {
            switch (node.Kind)
            {
                case NexNodeKind.Image:
                    features.Require(Compiled.NexFeatures.Image, node.NodeId, path + " is an image.");
                    break;
                case NexNodeKind.Label:
                    features.Require(Compiled.NexFeatures.Text, node.NodeId, path + " draws text.");
                    break;
                case NexNodeKind.Button:
                    features.Require(Compiled.NexFeatures.Button, node.NodeId, path + " is a button.");
                    features.Require(Compiled.NexFeatures.Text, node.NodeId, path + " has a button label.");
                    break;
            }

            if (!string.IsNullOrEmpty(node.TextBindingKey))
                features.Require(Compiled.NexFeatures.TextBinding, node.NodeId,
                    path + " binds text to '" + node.TextBindingKey + "'.");

            if (!string.IsNullOrEmpty(node.CommandId))
                features.Require(Compiled.NexFeatures.CommandBinding, node.NodeId,
                    path + " dispatches '" + node.CommandId + "'.");
        }

        // ---- pass 4: hash ---------------------------------------------------

        /// <summary>
        /// SHA-1 over the canonical form, hex, lower case. Chosen for stability across runtimes
        /// rather than for security - <c>string.GetHashCode</c> is explicitly not guaranteed to be
        /// stable between processes, which would make a persisted cache key meaningless.
        /// </summary>
        private static string ComputeHash(string canonical)
        {
            using (var sha = SHA1.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical ?? string.Empty));
                var sb = new StringBuilder(bytes.Length * 2);
                for (int i = 0; i < bytes.Length; i++) sb.Append(bytes[i].ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
