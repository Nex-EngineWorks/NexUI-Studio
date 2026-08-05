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
            if (string.IsNullOrEmpty(screenId))
                diagnostics.Add(NexDiagnosticCodes.ScreenIdMissing, new NexSourceLocation(string.Empty),
                    detail: "Asset: " + metadata.name);

            var ordered = Normalize(metadata, screenId, diagnostics);
            var program = Lower(screenId, ordered, options, diagnostics);

            stopwatch.Stop();
            return new NexCompileResult(program, diagnostics, stopwatch.Elapsed.TotalMilliseconds);
        }

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
                    AutomationId = element.automationId ?? string.Empty,
                    Role = element.accessibilityRole
                };

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

            var interactions = LowerInteractions(ordered, nodes, indexById, pathById, screenId, features, diagnostics);

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
                        if (rule.trigger != DesignerInteractionTrigger.OnClick ||
                            !hasDescendants.Contains(element.elementId))
                        {
                            diagnostics.Add(NexDiagnosticCodes.InteractionPhaseUnreachable, location,
                                "'" + element.elementId + "' listens on " + rule.phase + " for " + rule.trigger +
                                ", which can never reach it; the rule was dropped.",
                                rule.trigger != DesignerInteractionTrigger.OnClick
                                    ? rule.trigger + " does not propagate."
                                    : "The element has no children.");
                            continue;
                        }
                    }
                    else if (!CanRaise(nodes[i].Kind, rule.trigger))
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

                    features.Require(NexFeatures.Interaction, element.stableId,
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

        /// <summary>Which node kinds can actually raise which trigger.</summary>
        /// <remarks>
        /// Show and hide belong to the screen's lifecycle so any node can be told about them.
        /// A click has to come from something clickable, and authoring one on a Label produces a
        /// rule that would never fire - caught here rather than left as a mystery at runtime.
        /// </remarks>
        private static bool CanRaise(NexNodeKind kind, DesignerInteractionTrigger trigger)
            => trigger != DesignerInteractionTrigger.OnClick || kind == NexNodeKind.Button;

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
                    diagnostics.Add(NexDiagnosticCodes.BackendUnsupportedNode, location,
                        "'" + element.elementType + "' has no compiled representation yet; it becomes a panel.",
                        "uGUI control: " + control);
                    return NexNodeKind.Panel;
            }
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
                    features.Require(NexFeatures.Image, node.NodeId, path + " is an image.");
                    break;
                case NexNodeKind.Label:
                    features.Require(NexFeatures.Text, node.NodeId, path + " draws text.");
                    break;
                case NexNodeKind.Button:
                    features.Require(NexFeatures.Button, node.NodeId, path + " is a button.");
                    features.Require(NexFeatures.Text, node.NodeId, path + " has a button label.");
                    break;
            }

            if (!string.IsNullOrEmpty(node.TextBindingKey))
                features.Require(NexFeatures.TextBinding, node.NodeId,
                    path + " binds text to '" + node.TextBindingKey + "'.");

            if (!string.IsNullOrEmpty(node.CommandId))
                features.Require(NexFeatures.CommandBinding, node.NodeId,
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
