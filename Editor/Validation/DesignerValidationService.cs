using System.Collections.Generic;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Components;
using emiteat.NexUI.Core;
using emiteat.NexUI.Designer.Editor.Backend;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Designer.Editor.Serialization;
using emiteat.NexUI.Integrations.UGUI;
using emiteat.NexUI.Designer.Editor.Properties;
using emiteat.NexUI.MotionClip;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Validation
{
    /// <summary>
    /// Produces structured, actionable validation issues for a screen + metadata pair.
    /// Cheap metadata / screen rules run unconditionally; backend-asset cross-checks read
    /// the backend asset directly (VisualTreeAsset clone or prefab-asset transform walk)
    /// without instantiating a live surface.
    /// </summary>
    public static class DesignerValidationService
    {
        /// <param name="variantContext">
        /// Canvas resolution / input mode, so component variant rules conditioned on the environment
        /// validate against what the canvas is actually showing.
        /// </param>
        /// <param name="elementCache">
        /// Optional. When supplied, the element-scoped rules are reused for elements that have not
        /// changed. Null - the default - recomputes everything, which is what every non-interactive
        /// caller wants and what the behaviour was before caching existed.
        /// </param>
        public static List<DesignerValidationIssue> Validate(UIScreenDefinition screen, DesignerMetadataAsset metadata,
            Components.Definitions.DesignerComponentVariantContext variantContext = default,
            IDesignerElementIssueCache elementCache = null)
        {
            var issues = new List<DesignerValidationIssue>();
            var screenId = screen != null ? screen.ScreenId : null;

            if (screen == null)
            {
                issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Info, "no-screen",
                    "No screen is open.", "Assign a UIScreenDefinition in the toolbar Screen field."));
                return issues;
            }

            ValidateScreen(screen, screenId, issues);

            var backendNames = CollectBackendElementNames(screen);

            if (metadata == null)
            {
                issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Info, "no-metadata",
                    "No Designer metadata asset is assigned.",
                    "Assign or create a DesignerMetadataAsset with the 'New' button.", screenId));
                return issues;
            }

            if (!string.IsNullOrEmpty(metadata.screenId) && !string.IsNullOrEmpty(screenId) && metadata.screenId != screenId)
                issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning, "metadata-screen-mismatch",
                    $"Metadata screenId '{metadata.screenId}' differs from screen '{screenId}'.",
                    "Set the metadata screenId to match the screen, or open the correct screen.", screenId));

            ValidateElements(screen, metadata, screenId, backendNames, issues, elementCache);
            ValidateHierarchy(metadata, screenId, issues);
            ValidateOrphans(metadata, screenId, backendNames, issues);
            ValidateReferences(metadata, screenId, issues);
            ValidateCollections(metadata, screenId, issues);
            ValidateMotion(metadata, screenId, issues);
            ValidatePrefabComponents(screen, metadata, screenId, issues);
            DesignerComponentValidation.Validate(metadata, screenId, issues, variantContext);
            // The screen's backend decides which attached components can run at all, so this check
            // belongs here rather than at attach time: switching the backend afterwards is exactly the
            // case attach-time rules cannot catch.
            DesignerElementComponentValidation.Validate(metadata, screenId, ComponentBackendOf(screen), issues);

            return issues;
        }

        /// <summary>The component family a screen's backend can actually run.</summary>
        private static Components.DesignerUIComponentFamily ComponentBackendOf(UIScreenDefinition screen)
            => screen != null && screen.backendAsset.backend == UIRenderBackend.UIToolkit
                ? Components.DesignerUIComponentFamily.UIToolkit
                : Components.DesignerUIComponentFamily.UGUI;

        private static void ValidateScreen(UIScreenDefinition screen, string screenId, List<DesignerValidationIssue> issues)
        {
            if (string.IsNullOrEmpty(screenId))
                issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Error, "empty-screen-id",
                    "Screen has an empty screenId.", "Set identity.screenId on the UIScreenDefinition."));

            var backend = screen.backendAsset.backend;
            var asset = screen.backendAsset.asset;

            if (!DesignerBackendRegistry.TryGet(backend, out _))
                issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Error, "unsupported-backend",
                    $"No designer backend is registered for '{backend}'.",
                    "Use a supported backend (UIToolkit or UGUI).", screenId));

            if (asset == null)
            {
                issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning, "backend-asset-missing",
                    "The screen has no backend asset assigned.",
                    backend == UIRenderBackend.UIToolkit
                        ? "Assign a UXML VisualTreeAsset to backendAsset.asset."
                        : "Assign a uGUI prefab to backendAsset.asset.", screenId));
            }
            else if (backend == UIRenderBackend.UIToolkit && !(asset is VisualTreeAsset))
            {
                issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Error, "backend-type-mismatch",
                    $"UI Toolkit backend requires a VisualTreeAsset but '{asset.name}' is {asset.GetType().Name}.",
                    "Assign a UXML VisualTreeAsset.", screenId));
            }
            else if (backend == UIRenderBackend.UGUI && !(asset is GameObject))
            {
                issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Error, "backend-type-mismatch",
                    $"uGUI backend requires a GameObject prefab but '{asset.name}' is {asset.GetType().Name}.",
                    "Assign a uGUI prefab.", screenId));
            }
        }

        private static void ValidateElements(UIScreenDefinition screen, DesignerMetadataAsset metadata, string screenId,
            HashSet<string> backendNames, List<DesignerValidationIssue> issues,
            IDesignerElementIssueCache elementCache = null)
        {
            var backend = screen.backendAsset.backend;
            elementCache?.BeginPass(backend, screenId, backendNames);

            var ids = new HashSet<string>();
            var stableIds = new HashSet<string>();

            // Cross-element, so it stays in this loop rather than moving into ValidateElement:
            // uniqueness is a property of the whole screen and cannot be answered per element.
            var automationIds = new Dictionary<string, string>();
            foreach (var element in metadata.elements)
            {
                if (element == null) continue;
                var id = element.elementId;

                if (string.IsNullOrEmpty(id))
                {
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Error, "empty-element-id",
                        "An element has an empty id.", "Give every element a unique elementId.", screenId));
                    continue;
                }

                if (!ids.Add(id))
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Error, "duplicate-element-id",
                        $"Element id '{id}' is used more than once.", "Rename one of the duplicates.", screenId, id));

                if (string.IsNullOrEmpty(element.stableId))
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Error, "missing-stable-id",
                        $"Element '{id}' has no stable identity.", "Run the metadata migration or recreate the element.", screenId, id));
                else if (!stableIds.Add(element.stableId))
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Error, "duplicate-stable-id",
                        $"Element '{id}' shares stableId '{element.stableId}' with another element.",
                        "Assign a new stable identity before saving.", screenId, id));

                if (!string.IsNullOrEmpty(element.automationId))
                {
                    if (automationIds.TryGetValue(element.automationId, out var owner))
                        issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Error, "duplicate-automation-id",
                            $"Automation id '{element.automationId}' is used by both '{owner}' and '{id}'.",
                            "Rename one of them; a test looking it up would get whichever compiled first.",
                            screenId, id));
                    else
                        automationIds[element.automationId] = id;
                }

                // Spliced in at exactly the position the inline call used to occupy, so a cached
                // pass and a full pass produce the same list in the same order.
                if (elementCache == null)
                {
                    ValidateElement(element, backend, screenId, backendNames, issues);
                    continue;
                }

                var reused = elementCache.TryReuse(element);
                if (reused != null)
                {
                    issues.AddRange(reused);
                    continue;
                }

                var produced = new List<DesignerValidationIssue>();
                ValidateElement(element, backend, screenId, backendNames, produced);
                issues.AddRange(produced);
                elementCache.Store(element, produced);
            }
        }

        /// <summary>
        /// Every rule that is a function of one element on its own.
        /// </summary>
        /// <remarks>
        /// Split out from the element loop so validation can eventually be narrowed to the
        /// elements that actually changed. These rules read nothing but the element, the target
        /// backend and the backend asset's element names, which is what makes narrowing sound:
        /// re-running them for one element can never change another element's issues.
        ///
        /// The rules that are <em>not</em> here are the cross-element ones - duplicate element ids
        /// and duplicate stable ids - because their answer depends on the whole document. Those
        /// stay in the loop above and must be re-run whenever anything is added, removed or
        /// renamed.
        ///
        /// Call order is deliberately identical to what the single loop did before, so the issue
        /// list this produces is byte-for-byte what it used to be.
        /// </remarks>
        public static void ValidateElement(DesignerElementMetadata element, UIRenderBackend backend,
            string screenId, HashSet<string> backendNames, List<DesignerValidationIssue> issues)
        {
            if (element == null || issues == null) return;

            var id = element.elementId;
            if (string.IsNullOrEmpty(id)) return;

            if (!DesignerMetadataUtility.IsValidElementId(id))
                issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning, "invalid-element-id",
                    $"Element id '{id}' is not a safe identifier.",
                    "Use letters, digits, '_' or '-' and start with a letter/underscore.", screenId, id));

            if (backendNames != null && !backendNames.Contains(id) &&
                (string.IsNullOrEmpty(element.stableId) || !backendNames.Contains("$stable:" + element.stableId)))
                issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning, "missing-backend-element",
                    $"No element named '{id}' exists in the backend asset.",
                    backend == UIRenderBackend.UIToolkit
                        ? "Add name=\"" + id + "\" in UI Builder, or save to create it (uGUI)."
                        : "Save the screen to create the GameObject, or rename to match.", screenId, id));

            ValidateElementDetails(element, screenId, issues);
            ValidateComponentProperties(element, backend, screenId, issues);
            ValidateComponentParts(element, backend, screenId, issues);
            ValidatePropertyParity(element, backend, screenId, issues);
        }

        private static void ValidateComponentParts(DesignerElementMetadata element, UIRenderBackend backend,
            string screenId, List<DesignerValidationIssue> issues)
        {
            if (element.componentPartOverrides == null || element.componentPartOverrides.Count == 0) return;
            var descriptor = DesignerComponentRegistry.Get(element.elementType);
            var seen = new HashSet<string>();
            foreach (var value in element.componentPartOverrides)
            {
                if (value == null || string.IsNullOrWhiteSpace(value.partId))
                {
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning,
                        "component-part-empty-id", $"'{element.elementId}' contains an internal part override with no id.",
                        "Reset the malformed part override.", screenId, element.elementId));
                    continue;
                }
                if (!seen.Add(value.partId))
                {
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Error,
                        "component-part-duplicate", $"'{element.elementId}' stores part '{value.partId}' more than once.",
                        "Keep one override for this internal part.", screenId, element.elementId));
                    continue;
                }
                var part = descriptor.GetPart(value.partId);
                if (part == null)
                {
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Info,
                        "component-part-unknown", $"'{element.elementId}' preserves unknown part '{value.partId}'.",
                        "Install the component library that declares this part, or leave it for forward compatibility.",
                        screenId, element.elementId));
                    continue;
                }
                if (value.hasScale && (Mathf.Abs(value.scale.x) < 0.001f || Mathf.Abs(value.scale.y) < 0.001f))
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning,
                        "component-part-zero-scale", $"'{element.elementId}/{part.DisplayName}' has a near-zero scale.",
                        "Use Visible to hide the part, or restore a non-zero scale.", screenId, element.elementId));

                var supported = backend == UIRenderBackend.UGUI
                    ? !part.PreviewOnly && part.UGUIPath != null
                    : !part.PreviewOnly && !string.IsNullOrEmpty(part.UIToolkitSelector);
                if (!supported && value.HasAnyOverride)
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Info,
                        "component-part-backend-preview-only",
                        $"'{element.elementId}/{part.DisplayName}' transform is preview/metadata-only on {backend}.",
                        "Use a component part with a backend mapping or provide a custom adapter.", screenId, element.elementId));
                else if (backend == UIRenderBackend.UIToolkit && value.hasSizeDelta)
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Info,
                        "component-part-size-preview-only",
                        $"'{element.elementId}/{part.DisplayName}' Size Delta cannot be emitted safely for UI Toolkit internals.",
                        "Position, Rotation, Scale and Visibility are still emitted to generated USS.", screenId, element.elementId));
            }
        }

        private static void ValidateComponentProperties(DesignerElementMetadata element, UIRenderBackend backend,
            string screenId, List<DesignerValidationIssue> issues)
        {
            if (element.componentProperties == null || element.componentProperties.Count == 0) return;
            var component = DesignerComponentRegistry.Get(element.elementType);
            var seen = new HashSet<string>();

            foreach (var entry in element.componentProperties)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                {
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning,
                        "component-property-empty-key", $"'{element.elementId}' contains a component property with no key.",
                        "Reset the malformed property entry.", screenId, element.elementId));
                    continue;
                }
                if (!seen.Add(entry.key))
                {
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Error,
                        "component-property-duplicate", $"'{element.elementId}' stores '{entry.key}' more than once.",
                        "Keep one value for the property.", screenId, element.elementId));
                    continue;
                }

                var property = DesignerComponentPropertyAccess.Find(element, entry.key);
                if (property == null) continue; // forward-compatible/custom values are intentionally preserved
                if (entry.value == null || entry.value.type != property.Type)
                {
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning,
                        "component-property-type-mismatch",
                        $"'{element.elementId}' property '{entry.key}' does not match schema type {property.Type}.",
                        "Reset the property and author it again in the Component Properties inspector.", screenId, element.elementId));
                    continue;
                }

                if (property.Type == DesignerPropertyValueType.Enum &&
                    (property.EnumOptions == null || entry.value.intValue < 0 || entry.value.intValue >= property.EnumOptions.Length))
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning,
                        "component-property-enum-range", $"'{element.elementId}' property '{entry.key}' has an invalid option index.",
                        "Pick one of the listed options.", screenId, element.elementId));
                else if (property.HasRange)
                {
                    var numeric = property.Type == DesignerPropertyValueType.Integer
                        ? entry.value.intValue
                        : entry.value.floatValue;
                    if (numeric < property.Min || numeric > property.Max)
                        issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning,
                            "component-property-out-of-range",
                            $"'{element.elementId}' property '{entry.key}' is {numeric:0.###}; expected {property.Min:0.###}..{property.Max:0.###}.",
                            "Clamp the value in the Component Properties inspector.", screenId, element.elementId));
                }

                var support = backend == UIRenderBackend.UGUI
                    ? DesignerComponentPropertySupport.UGUI(component, property)
                    : DesignerComponentPropertySupport.UIToolkit(component, property);
                if (support == DesignerBackendSupport.Unsupported)
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning,
                        "component-property-backend-unsupported",
                        $"'{element.elementId}' property '{entry.key}' is unsupported on {backend}.",
                        "Remove it or provide a custom backend adapter.", screenId, element.elementId));
                else if (support == DesignerBackendSupport.PreviewOnly)
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Info,
                        "component-property-backend-preview-only",
                        $"'{element.elementId}' property '{entry.key}' is preview/metadata-only on {backend}.",
                        "Add runtime behavior if this property must affect the built screen.", screenId, element.elementId));
            }

            var min = DesignerComponentPropertyAccess.GetFloat(element, "value.min", 0f);
            var max = DesignerComponentPropertyAccess.GetFloat(element, "value.max", 0f);
            if ((seen.Contains("value.min") || seen.Contains("value.max")) && max <= min)
                issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Error,
                    "component-property-invalid-range",
                    $"'{element.elementId}' maximum value ({max:0.###}) must be greater than minimum ({min:0.###}).",
                    "Increase Max Value or decrease Min Value.", screenId, element.elementId));

            if (seen.Contains("range.low") || seen.Contains("range.high"))
            {
                var low = DesignerComponentPropertyAccess.GetFloat(element, "range.low");
                var high = DesignerComponentPropertyAccess.GetFloat(element, "range.high");
                if (low > high)
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Error,
                        "component-property-invalid-range",
                        $"'{element.elementId}' low value ({low:0.###}) exceeds high value ({high:0.###}).",
                        "Lower the Low Value or increase the High Value.", screenId, element.elementId));
            }
        }

        private static void ValidateElementDetails(DesignerElementMetadata element, string screenId, List<DesignerValidationIssue> issues)
        {
            var id = element.elementId;
            var type = element.elementType ?? "Panel";
            bool isButton = Is(type, "Button") || Is(type, "IconButton");

            if (isButton && (element.binding == null || string.IsNullOrEmpty(element.binding.commandKey)))
                issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning, "button-without-command",
                    $"{type} '{id}' has no command key.", "Set a Command Key in the Binding inspector.", screenId, id));

            if (isButton && string.IsNullOrEmpty(element.text) &&
                (element.binding == null || string.IsNullOrEmpty(element.binding.textKey)))
                issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Info, "button-without-text",
                    $"{type} '{id}' has neither text nor a text binding.", "Set text or a Text Key.", screenId, id));

            if (element.rect.width < 32f || element.rect.height < 32f)
                issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning, "small-touch-target",
                    $"'{id}' is {element.rect.width:0}x{element.rect.height:0}; below the 32x32 minimum touch target.",
                    "Increase width/height to at least 32x32.", screenId, id));

            if (element.rect.width <= 0f || element.rect.height <= 0f)
                issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Error, "zero-size-element",
                    $"'{id}' has a non-positive size ({element.rect.width:0.#}x{element.rect.height:0.#}).",
                    "Set a positive width and height.", screenId, id));

            var reference = new Rect(0f, 0f, 1920f, 1080f);
            if (!reference.Overlaps(element.rect))
                issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning, "outside-canvas",
                    $"'{id}' is completely outside the 1920x1080 reference canvas.",
                    "Move the element back inside the canvas.", screenId, id));

            if (element.hiddenInDesigner && element.binding != null &&
                (!string.IsNullOrEmpty(element.binding.commandKey) || !string.IsNullOrEmpty(element.binding.interactableKey)))
                issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Info, "hidden-but-interactive",
                    $"'{id}' is hidden in designer yet declares interactive bindings.",
                    "Unhide it, or remove the command/interactable binding.", screenId, id));
        }

        private static void ValidatePropertyParity(DesignerElementMetadata element, UIRenderBackend backend,
            string screenId, List<DesignerValidationIssue> issues)
        {
            var layout = DesignerPropertyAdapter.Layout(element);
            var visual = DesignerPropertyAdapter.Visual(element);
            var typography = DesignerPropertyAdapter.Typography(element);

            if (layout.hasOverrides)
            {
                if (layout.maxSize.x > 0f) CheckProperty(DesignerPropertyId.MaxWidth, backend, element, screenId, issues);
                if (layout.maxSize.y > 0f) CheckProperty(DesignerPropertyId.MaxHeight, backend, element, screenId, issues);
                if (layout.aspectRatio > 0f) CheckProperty(DesignerPropertyId.AspectRatio, backend, element, screenId, issues);
                if (layout.wrap == DesignerLayoutWrap.Wrap) CheckProperty(DesignerPropertyId.Wrap, backend, element, screenId, issues);
                if (layout.justify == DesignerJustifyContent.SpaceAround || layout.justify == DesignerJustifyContent.SpaceBetween)
                    CheckProperty(DesignerPropertyId.Justify, backend, element, screenId, issues);
            }
            if (visual.hasOverrides)
            {
                if (visual.gradient != null) CheckProperty(DesignerPropertyId.Gradient, backend, element, screenId, issues);
                if (visual.borderWidth > 0f) CheckProperty(DesignerPropertyId.BorderWidth, backend, element, screenId, issues);
                if (visual.cornerRadius > 0f) CheckProperty(DesignerPropertyId.CornerRadius, backend, element, screenId, issues);
                if (visual.dropShadow) CheckProperty(DesignerPropertyId.DropShadow, backend, element, screenId, issues);
                if (visual.innerShadow) CheckProperty(DesignerPropertyId.InnerShadow, backend, element, screenId, issues);
                if (visual.blur > 0f) CheckProperty(DesignerPropertyId.Blur, backend, element, screenId, issues);
                if (visual.material != null) CheckProperty(DesignerPropertyId.Material, backend, element, screenId, issues);
            }
            if (typography.hasOverrides)
            {
                if (typography.fontAsset != null) CheckProperty(DesignerPropertyId.FontAsset, backend, element, screenId, issues);
                if (typography.autoSize) CheckProperty(DesignerPropertyId.AutoFontSize, backend, element, screenId, issues);
                if (typography.paragraphSpacing != 0f) CheckProperty(DesignerPropertyId.ParagraphSpacing, backend, element, screenId, issues);
                if (typography.rightToLeft) CheckProperty(DesignerPropertyId.RightToLeft, backend, element, screenId, issues);
                if (typography.outlineWidth > 0f) CheckProperty(DesignerPropertyId.TextOutline, backend, element, screenId, issues);
            }
        }

        private static void CheckProperty(DesignerPropertyId propertyId, UIRenderBackend backend,
            DesignerElementMetadata element, string screenId, List<DesignerValidationIssue> issues)
        {
            var descriptor = DesignerPropertyRegistry.Get(propertyId);
            if (descriptor == null) return;
            var support = backend == UIRenderBackend.UGUI ? descriptor.UGUI : descriptor.UIToolkit;
            if (support == DesignerPropertyBackendSupport.Supported) return;
            var fallback = backend == UIRenderBackend.UGUI ? descriptor.UGUIFallback : descriptor.UIToolkitFallback;
            var severity = support == DesignerPropertyBackendSupport.Unsupported
                ? DesignerValidationSeverity.Warning : DesignerValidationSeverity.Info;
            issues.Add(new DesignerValidationIssue(severity, "property-backend-" + support.ToString().ToLowerInvariant(),
                $"'{element.elementId}' property {descriptor.DisplayName} is {support} on {backend}.",
                string.IsNullOrEmpty(fallback) ? "Remove the property or provide a custom backend adapter." : fallback,
                screenId, element.elementId));
        }

        /// <summary>
        /// Parent/child hierarchy integrity: missing/self/cyclic parents, leaf types holding
        /// children, and excessive nesting depth. The source of truth is parentId + siblingIndex.
        /// </summary>
        private static void ValidateHierarchy(DesignerMetadataAsset metadata, string screenId, List<DesignerValidationIssue> issues)
        {
            const int MaxDepth = 20;
            foreach (var element in metadata.elements)
            {
                if (element == null || string.IsNullOrEmpty(element.elementId)) continue;
                var id = element.elementId;

                if (!string.IsNullOrEmpty(element.parentId))
                {
                    if (element.parentId == id)
                    {
                        issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Error, "self-parent",
                            $"'{id}' is its own parent.", "Move it to root or set a different parent.", screenId, id));
                    }
                    else if (metadata.Find(element.parentId) == null)
                    {
                        issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Error, "missing-parent",
                            $"'{id}' references parent '{element.parentId}' which does not exist.",
                            "Move it to root or repoint it at an existing element.", screenId, id));
                    }
                    else if (DesignerHierarchyUtility.IsDescendant(metadata, element.parentId, id))
                    {
                        issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Error, "circular-parent",
                            $"'{id}' is part of a circular parent chain via '{element.parentId}'.",
                            "Move one node in the cycle to root.", screenId, id));
                        continue; // depth walk below would loop
                    }
                }

                // Leaf-type element holding children ⇒ warn (allowed, but usually unintended).
                if (!DesignerComponentRegistry.CanHaveChildren(element.elementType) &&
                    DesignerHierarchyUtility.CountChildren(metadata, element) > 0)
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning, "leaf-with-children",
                        $"'{id}' is a {element.elementType} (a leaf type) but has children.",
                        "Wrap the children in a Panel/Container, or change this element's type.", screenId, id));

                // Binding key set on a channel the component doesn't support ⇒ warn (never deleted;
                // shown so the user can move it to the Advanced/Legacy area or remove it).
                ValidateBindingSupport(element, screenId, issues);

                if (DesignerHierarchyUtility.GetDepth(metadata, element) > MaxDepth)
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning, "excessive-depth",
                        $"'{id}' is nested deeper than {MaxDepth} levels.",
                        "Flatten the hierarchy to keep layout predictable.", screenId, id));

                // Slot integrity: a non-default parentSlotId must name a real slot on the parent's
                // descriptor, and template slots hold at most one child.
                if (!string.IsNullOrEmpty(element.parentId) && !string.IsNullOrEmpty(element.parentSlotId) &&
                    element.parentSlotId != DesignerComponentSlot.Content)
                {
                    var parent = metadata.Find(element.parentId);
                    if (parent != null)
                    {
                        var parentDesc = DesignerComponentRegistry.Get(parent.elementType);
                        if (!parentDesc.IsGeneric && parentDesc.GetSlot(element.parentSlotId) == null)
                            issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Error, "invalid-slot",
                                $"'{id}' targets slot '{element.parentSlotId}' which '{parent.elementId}' ({parentDesc.DisplayName}) does not have.",
                                "Move it to a valid slot or the content slot.", screenId, id));
                    }
                }
            }

            // Template slots must not contain more than one authored child.
            foreach (var parent in metadata.elements)
            {
                if (parent == null || string.IsNullOrEmpty(parent.elementId)) continue;
                var desc = DesignerComponentRegistry.Get(parent.elementType);
                foreach (var slot in desc.Slots)
                {
                    if (!slot.IsTemplateSlot) continue;
                    var count = 0;
                    foreach (var child in metadata.elements)
                        if (child != null && child.parentId == parent.elementId &&
                            (child.parentSlotId ?? DesignerComponentSlot.Content) == slot.SlotId)
                            count++;
                    if (count > 1)
                        issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning, "template-slot-multiple",
                            $"'{parent.elementId}' template slot '{slot.SlotId}' has {count} children; only the first is used as the item template.",
                            "Keep a single element in the template slot.", screenId, parent.elementId));
                }
            }
        }

        /// <summary>
        /// Rules for the CollectionView system and every preset of it.
        /// </summary>
        /// <remarks>
        /// A collection fails in ways that look like "the UI is broken" at runtime rather than at
        /// author time: no template means no rows, no source key means an eternally empty list, and
        /// an unsupported option combination means the layout quietly does something else. Each of
        /// these is cheap to detect from metadata alone, so none of them should reach a build.
        /// </remarks>
        private static void ValidateCollections(DesignerMetadataAsset metadata, string screenId,
            List<DesignerValidationIssue> issues)
        {
            foreach (var element in metadata.elements)
            {
                if (element == null || string.IsNullOrEmpty(element.elementId)) continue;
                if (!DesignerCollectionOptions.IsCollection(element)) continue;

                var options = DesignerCollectionOptions.Read(element);

                if (DesignerCollectionOptions.FindTemplate(metadata, element) == null)
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Error, "collection-template-missing",
                        $"'{element.elementId}' has no item template, so it will show no items at runtime.",
                        "Add a child element and place it in the collection's template slot.", screenId, element.elementId));

                if (string.IsNullOrEmpty(DesignerCollectionOptions.SourceKey(element))
                    && string.IsNullOrEmpty(element.binding?.valueKey))
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning, "collection-source-missing",
                        $"'{element.elementId}' has no Items Source Key and no value binding, so nothing will populate it.",
                        "Set Items Source Key, or bind the Value channel to a runtime state key.", screenId, element.elementId));

                foreach (var problem in DesignerCollectionOptions.Problems(element))
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning, "collection-options-conflict",
                        $"'{element.elementId}': {problem}",
                        "Adjust the collection options so the combination can be honoured.", screenId, element.elementId));

                if (DesignerCollectionOptions.ShowsEmptyState(element)
                    && DesignerCollectionOptions.FindStateChild(metadata, element, "empty") == null)
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Info, "collection-empty-state-missing",
                        $"'{element.elementId}' shows an empty state but has no element in the Empty State slot.",
                        "Add an element to the Empty State slot, or turn Show Empty State off.", screenId, element.elementId));

                if (options.Selection == NXSelectionMode.None
                    && (options.Interactions & NXCollectionInteractions.Reorder) != 0)
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning, "collection-selection-conflict",
                        $"'{element.elementId}' is reorderable but its Selection Mode is None, so the user cannot pick what to move.",
                        "Set Selection Mode to Single or Multiple.", screenId, element.elementId));

                if (options.Paging == NXPagingMode.Infinite
                    && options.Virtualization == NXVirtualizationMode.None)
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning, "collection-virtualization-conflict",
                        $"'{element.elementId}' loads pages forever with virtualization off, so every loaded item stays realized.",
                        "Set Virtualization to Fixed Size or Dynamic Size.", screenId, element.elementId));
            }
        }

        /// <summary>
        /// Flags binding keys set on channels the element's component descriptor does not support.
        /// Reported as info (not error) and never mutated - the value stays in the data so it can be
        /// surfaced in the Inspector's Legacy/Unsupported area. Unknown/Generic types support all
        /// channels, so they never trip this.
        /// </summary>
        private static void ValidateBindingSupport(DesignerElementMetadata element, string screenId, List<DesignerValidationIssue> issues)
        {
            var b = element.binding;
            if (b == null) return;
            var d = DesignerComponentRegistry.Get(element.elementType);
            var id = element.elementId;

            void Check(string key, DesignerBindingChannel channel, string label)
            {
                if (!string.IsNullOrEmpty(key) && !d.SupportsBinding(channel))
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Info, "unsupported-binding",
                        $"'{id}' ({d.DisplayName}) sets a {label} binding, which this component does not use.",
                        "Remove it, or move it to the Advanced/Legacy bindings area.", screenId, id));
            }

            Check(b.textKey, DesignerBindingChannel.Text, "text");
            Check(b.valueKey, DesignerBindingChannel.Value, "value");
            Check(b.commandKey, DesignerBindingChannel.Command, "command");
            Check(b.interactableKey, DesignerBindingChannel.Interactable, "interactable");
        }

        private static void ValidateOrphans(DesignerMetadataAsset metadata, string screenId,
            HashSet<string> backendNames, List<DesignerValidationIssue> issues)
        {
            if (backendNames == null) return;
            var metaIds = new HashSet<string>();
            foreach (var e in metadata.elements)
                if (e != null && !string.IsNullOrEmpty(e.elementId)) metaIds.Add(e.elementId);

            foreach (var name in backendNames)
                if (!metaIds.Contains(name))
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Info, "orphan-backend-element",
                        $"Backend element '{name}' has no Designer metadata.",
                        "Use 'Sync Metadata From Backend' or ignore if it is decorative.", screenId, name));
        }

        private static void ValidateReferences(DesignerMetadataAsset metadata, string screenId, List<DesignerValidationIssue> issues)
        {
            var ids = new HashSet<string>();
            foreach (var e in metadata.elements)
                if (e != null && !string.IsNullOrEmpty(e.elementId)) ids.Add(e.elementId);

            if (metadata.localization != null)
                foreach (var link in metadata.localization.links)
                {
                    if (link == null) continue;
                    if (!string.IsNullOrEmpty(link.elementId) && !ids.Contains(link.elementId))
                        issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning, "localization-target-missing",
                            $"Localization link targets missing element '{link.elementId}'.",
                            "Point the link at an existing element or remove it.", screenId, link.elementId));
                    if (!string.IsNullOrEmpty(link.elementId) && string.IsNullOrEmpty(link.localizationKey))
                        issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Info, "localization-key-missing",
                            $"Element '{link.elementId}' has a localization link with no key.",
                            "Set a localization key or remove the link.", screenId, link.elementId));
                }

            if (metadata.variants != null)
                foreach (var v in metadata.variants)
                {
                    if (v == null) continue;
                    foreach (var ov in v.overrides)
                    {
                        if (ov == null) continue;
                        if (!string.IsNullOrEmpty(ov.targetElementId) && !ids.Contains(ov.targetElementId))
                            issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning, "variant-target-missing",
                                $"Variant '{v.variantId}' overrides missing element '{ov.targetElementId}'.",
                                "Fix the target elementId or remove the override.", screenId, ov.targetElementId));
                        ValidateOverride(ov.propertyId, ov.typedValue, ov.propertyPath, ov.value,
                            $"Variant '{v.variantId}'", screenId, ov.targetElementId, issues);
                    }
                }

            if (metadata.responsiveRules != null)
                foreach (var r in metadata.responsiveRules)
                {
                    if (r == null) continue;
                    foreach (var ov in r.overrides)
                    {
                        if (ov == null) continue;
                        if (!string.IsNullOrEmpty(ov.elementId) && !ids.Contains(ov.elementId))
                            issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning, "responsive-target-missing",
                                $"Responsive rule '{r.ruleId}' overrides missing element '{ov.elementId}'.",
                                "Fix the target elementId or remove the override.", screenId, ov.elementId));
                        ValidateOverride(ov.propertyId, ov.typedValue, ov.propertyPath, ov.value,
                            $"Responsive rule '{r.ruleId}'", screenId, ov.elementId, issues);
                    }
                }
        }

        private static void ValidateOverride(DesignerPropertyId propertyId, DesignerPropertyValue typedValue,
            string legacyPath, string legacyValue, string owner, string screenId, string elementId,
            List<DesignerValidationIssue> issues)
        {
            var resolved = propertyId != DesignerPropertyId.None ? propertyId : DesignerPropertyRegistry.ResolveLegacyPath(legacyPath);
            var descriptor = DesignerPropertyRegistry.Get(resolved);
            if (descriptor == null)
            {
                issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning, "property-path-legacy-unknown",
                    $"{owner} uses unknown legacy property '{legacyPath}'.",
                    "Choose a typed property; the legacy value is preserved but cannot be capability-checked.", screenId, elementId));
                return;
            }
            if ((descriptor.Usage & DesignerPropertyUsage.Override) == 0)
                issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Error, "property-override-not-allowed",
                    $"{owner} cannot override '{descriptor.Path}'.", "Choose a property that supports overrides.", screenId, elementId));

            if (propertyId != DesignerPropertyId.None)
            {
                if (typedValue == null || typedValue.type != descriptor.ValueType)
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Error, "property-value-type-mismatch",
                        $"{owner} value for '{descriptor.Path}' is not {descriptor.ValueType}.",
                        "Re-enter the value using the typed property editor.", screenId, elementId));
            }
            else if (descriptor.ValueType != DesignerPropertyValueType.AssetReference &&
                     !DesignerPropertyRegistry.TryParse(resolved, legacyValue, out _, out var error))
                issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Error, "property-value-invalid",
                    $"{owner}: {error}", "Enter a value compatible with the selected property type.", screenId, elementId));
        }

        private static void ValidateMotion(DesignerMetadataAsset metadata, string screenId, List<DesignerValidationIssue> issues)
        {
            var motion = metadata.screenMotion;
            if (motion == null) return;
            var ids = new HashSet<string>();
            foreach (var element in metadata.elements)
                if (element != null && !string.IsNullOrEmpty(element.elementId)) ids.Add(element.elementId);

            var validatedClips = new HashSet<UIMotionClip>();
            if (motion.entryClip != null && motion.exitClip == null)
                issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning, "motion-close-missing",
                    "The screen has an Open transition but no Close transition.",
                    "Generate a reversed Close transition from the Open clip.", screenId));
            ValidateClip(motion.entryClip, screenId, issues, validatedClips);
            ValidateClip(motion.exitClip, screenId, issues, validatedClips);
            foreach (var binding in motion.bindings ?? new List<DesignerMotionBinding>())
            {
                if (binding == null) continue;
                var isScreenTrigger = binding.trigger == DesignerMotionTrigger.ScreenEnter || binding.trigger == DesignerMotionTrigger.ScreenExit;
                if (!isScreenTrigger && (string.IsNullOrEmpty(binding.targetElementId) || !ids.Contains(binding.targetElementId)))
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Error, "motion-target-missing",
                        $"Motion binding '{binding.bindingId}' targets missing element '{binding.targetElementId}'.",
                        "Choose an existing element or remove the binding.", screenId, binding.targetElementId));
                if (isScreenTrigger && !string.IsNullOrEmpty(binding.targetElementId))
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning, "screen-motion-has-target",
                        $"Screen trigger '{binding.trigger}' is connected to element '{binding.targetElementId}'.",
                        "Clear the target or use an element trigger.", screenId, binding.targetElementId));
                if (binding.clip == null)
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Error, "motion-clip-missing",
                        $"Motion binding '{binding.bindingId}' has no clip or its asset is missing.",
                        "Assign an existing UIMotionClip asset.", screenId, binding.targetElementId));
                if ((binding.trigger == DesignerMotionTrigger.StateEnter || binding.trigger == DesignerMotionTrigger.StateExit) && string.IsNullOrEmpty(binding.stateId))
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Error, "motion-state-id-missing",
                        $"Motion binding '{binding.bindingId}' requires a state id.", "Set a valid State Id.", screenId, binding.targetElementId));
                if ((binding.trigger == DesignerMotionTrigger.CommandStarted || binding.trigger == DesignerMotionTrigger.CommandCompleted || binding.trigger == DesignerMotionTrigger.CommandFailed) && string.IsNullOrEmpty(binding.commandId))
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning, "motion-command-id-missing",
                        $"Motion binding '{binding.bindingId}' requires a command id.", "Set a valid Command Id.", screenId, binding.targetElementId));
                ValidateClip(binding.clip, screenId, issues, validatedClips);
                ValidateClip(binding.reducedMotionClip, screenId, issues, validatedClips);
            }
        }

        private static void ValidateClip(UIMotionClip clip, string screenId, List<DesignerValidationIssue> issues, HashSet<UIMotionClip> validated)
        {
            if (clip == null || !validated.Add(clip)) return;
            var targets = new HashSet<string>();
            foreach (var track in clip.tracks ?? System.Array.Empty<UIMotionClipTrack>())
            {
                if (track == null) continue;
                if (!targets.Add(track.targetElementId ?? string.Empty))
                    AddClipIssue(DesignerValidationSeverity.Error, "motion-duplicate-track-target",
                        $"Clip '{clip.name}' contains duplicate track target '{track.targetElementId}'.",
                        "Merge properties into one target track.", clip, screenId, issues);
                foreach (var propertyTrack in track.propertyTracks ?? System.Array.Empty<UIMotionClipPropertyTrack>())
                {
                    if (propertyTrack?.keyframes == null) continue;
                    var previous = float.NegativeInfinity;
                    var hasStart = false;
                    var hasEnd = false;
                    var times = new HashSet<float>();
                    foreach (var keyframe in propertyTrack.keyframes)
                    {
                        if (!times.Add(keyframe.time))
                            AddClipIssue(DesignerValidationSeverity.Error, "motion-duplicate-keyframe-time",
                                $"Clip '{clip.name}' has duplicate keyframes at {keyframe.time:0.###} in {propertyTrack.propertyType}.",
                                "Merge or move one of the duplicate keyframes.", clip, screenId, issues);
                        hasStart |= Mathf.Approximately(keyframe.time, 0f);
                        hasEnd |= Mathf.Approximately(keyframe.time, clip.duration);
                        if (keyframe.time < 0f)
                            AddClipIssue(DesignerValidationSeverity.Error, "motion-negative-keyframe",
                                $"Clip '{clip.name}' has a keyframe at negative time {keyframe.time:0.###}.", "Move it to time 0 or later.", clip, screenId, issues);
                        if (keyframe.time > clip.duration)
                            AddClipIssue(DesignerValidationSeverity.Error, "motion-keyframe-after-duration",
                                $"Clip '{clip.name}' has a keyframe at {keyframe.time:0.###}, after duration {clip.duration:0.###}.", "Extend duration or move the keyframe.", clip, screenId, issues);
                        if (keyframe.time < previous)
                            AddClipIssue(DesignerValidationSeverity.Error, "motion-keyframes-unsorted",
                                $"Clip '{clip.name}' has unsorted keyframes in {propertyTrack.propertyType}.", "Sort keyframes by time.", clip, screenId, issues);
                        previous = keyframe.time;
                    }
                    if (propertyTrack.keyframes.Length > 0 && !hasStart)
                        AddClipIssue(DesignerValidationSeverity.Warning, "motion-start-keyframe-missing",
                            $"Clip '{clip.name}' has no keyframe at time 0 in {propertyTrack.propertyType}.",
                            "Add a start keyframe at time 0.", clip, screenId, issues);
                    if (propertyTrack.keyframes.Length > 0 && !hasEnd)
                        AddClipIssue(DesignerValidationSeverity.Warning, "motion-end-keyframe-missing",
                            $"Clip '{clip.name}' has no keyframe at duration {clip.duration:0.###} in {propertyTrack.propertyType}.",
                            "Add an end keyframe at the clip duration.", clip, screenId, issues);
                }
            }
        }

        private static void AddClipIssue(DesignerValidationSeverity severity, string code, string message, string fix,
            UIMotionClip clip, string screenId, List<DesignerValidationIssue> issues)
        {
            var issue = new DesignerValidationIssue(severity, code, message, fix, screenId) { Asset = clip };
            issues.Add(issue);
        }

        // ---- Backend-asset inspection ------------------------------------------------

        private static HashSet<string> CollectBackendElementNames(UIScreenDefinition screen)
        {
            var asset = screen.backendAsset.asset;
            switch (screen.backendAsset.backend)
            {
                case UIRenderBackend.UIToolkit:
                    return asset is VisualTreeAsset vta ? UIToolkitAssetSerializer.CollectElementNames(vta) : null;
                case UIRenderBackend.UGUI:
                    return asset is GameObject go ? CollectPrefabNames(go) : null;
                default:
                    return null;
            }
        }

        private static HashSet<string> CollectPrefabNames(GameObject prefab)
        {
            var names = new HashSet<string>();
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
            {
                names.Add(t.name);
                var tag = t.GetComponent<NxUGuiBindingTag>();
                if (tag == null) continue;
                if (!string.IsNullOrEmpty(tag.elementId)) names.Add(tag.elementId);
                if (!string.IsNullOrEmpty(tag.stableId)) names.Add("$stable:" + tag.stableId);
            }
            return names;
        }

        private static void ValidatePrefabComponents(UIScreenDefinition screen, DesignerMetadataAsset metadata,
            string screenId, List<DesignerValidationIssue> issues)
        {
            if (screen.backendAsset.backend != UIRenderBackend.UGUI) return;
            if (!(screen.backendAsset.asset is GameObject prefab)) return;

            // Duplicate GameObject names make name-based prefab matching unpredictable.
            var seen = new HashSet<string>();
            var dupes = new HashSet<string>();
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
                if (!seen.Add(t.name)) dupes.Add(t.name);
            foreach (var name in dupes)
                issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning, "duplicate-gameobject-name",
                    $"Prefab contains multiple GameObjects named '{name}'.",
                    "Rename duplicates so element matching stays reliable.", screenId, name));

            var stableIds = new HashSet<string>();
            var duplicateStableIds = new HashSet<string>();
            foreach (var tag in prefab.GetComponentsInChildren<NxUGuiBindingTag>(true))
                if (!string.IsNullOrEmpty(tag.stableId) && !stableIds.Add(tag.stableId))
                    duplicateStableIds.Add(tag.stableId);
            foreach (var stableId in duplicateStableIds)
                issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Error, "duplicate-prefab-stable-id",
                    $"Prefab contains multiple binding tags with stableId '{stableId}'.",
                    "Give every NxUGuiBindingTag a unique stableId before saving.", screenId));

            foreach (var element in metadata.elements)
            {
                if (element == null || string.IsNullOrEmpty(element.elementId)) continue;
                var child = FindChild(prefab.transform, element);
                if (child == null) continue; // missing-backend-element already reported.

                var type = element.elementType ?? "Panel";
                var go = child.gameObject;

                var graphic = go.GetComponent<Graphic>();
                if (graphic != null && graphic.raycastTarget && !Is(type, "Button") && !Is(type, "IconButton"))
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning, "ugui-decorative-raycast",
                        $"Decorative '{element.elementId}' has Raycast Target enabled and can block controls behind it.",
                        "Turn off Graphic.raycastTarget.", screenId, element.elementId));

                var canvasGroup = go.GetComponent<CanvasGroup>();
                if (canvasGroup != null && canvasGroup.alpha <= 0.001f && (canvasGroup.interactable || canvasGroup.blocksRaycasts))
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Error, "ugui-invisible-canvasgroup-blocks-input",
                        $"'{element.elementId}' has CanvasGroup alpha 0 but still receives or blocks input.",
                        "Disable Interactable and Blocks Raycasts.", screenId, element.elementId));

                var button = go.GetComponent<UnityEngine.UI.Button>();
                if (button != null && button.targetGraphic == null)
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning, "ugui-button-target-graphic-missing",
                        $"Button '{element.elementId}' has no Target Graphic.",
                        "Assign a Graphic on the same object as Target Graphic.", screenId, element.elementId));

                if ((Is(type, "Button") || Is(type, "IconButton")) && go.GetComponent<UnityEngine.UI.Button>() == null)
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning, "ugui-missing-button",
                        $"'{element.elementId}' is a {type} but has no Button component.",
                        "Save the screen to add one, or add Button manually.", screenId, element.elementId));

                if ((Is(type, "Label") || Is(type, "Toast") || Is(type, "Tooltip")) &&
                    go.GetComponentInChildren<TMP_Text>(true) == null && go.GetComponentInChildren<Text>(true) == null)
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning, "ugui-missing-text",
                        $"'{element.elementId}' is a {type} but has no TMP_Text/Text component.",
                        "Save the screen to add text, or add a text component manually.", screenId, element.elementId));

                if (Is(type, "Image") && go.GetComponent<Graphic>() == null)
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning, "ugui-missing-graphic",
                        $"'{element.elementId}' is an Image but has no Graphic/Image component.",
                        "Save the screen to add an Image, or add one manually.", screenId, element.elementId));

                if (Is(type, "Modal") && go.GetComponent<CanvasGroup>() == null)
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Info, "ugui-modal-without-canvasgroup",
                        $"Modal '{element.elementId}' has no CanvasGroup.",
                        "Add a CanvasGroup so the modal can fade / block input.", screenId, element.elementId));
            }
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindChild(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private static Transform FindChild(Transform root, DesignerElementMetadata element)
        {
            if (!string.IsNullOrEmpty(element.stableId))
                foreach (var tag in root.GetComponentsInChildren<NxUGuiBindingTag>(true))
                    if (tag.stableId == element.stableId) return tag.transform;
            return FindChild(root, element.elementId);
        }

        private static bool Is(string type, string other)
            => string.Equals(type, other, System.StringComparison.OrdinalIgnoreCase);
    }
}
