using System;
using System.Collections.Generic;
using System.IO;
using emiteat.NexUI.Accessibility;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Motion;
using emiteat.NexUI.MotionClip;
using emiteat.NexUI.MotionGraph;
using emiteat.NexUI.Theme;
using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Serialization
{
    /// <summary>
    /// Companion git-friendly JSON export/import for <see cref="DesignerMetadataAsset"/> (B8).
    /// The <c>.asset</c> stays Unity's own YAML - this only adds a <c>.json</c> file written
    /// next to it that mirrors the same data through DTOs with a fixed, declaration-order field
    /// layout (so JsonUtility produces the same byte-for-byte output for the same data every
    /// time - a real diff/merge tool, unlike Unity's YAML). <see cref="UnityEngine.Object"/>
    /// references (motion preset, theme) are written as persistent asset GUIDs, never raw
    /// instance IDs, so the JSON stays valid across sessions and machines.
    ///
    /// Treat the <c>.json</c> file as the thing to diff/review in a PR; use
    /// <see cref="Import"/> ("Sync from JSON" in the Designer) after resolving a merge conflict
    /// in the JSON to push the merged result back into the <c>.asset</c>.
    /// </summary>
    public static class DesignerMetadataJsonSerializer
    {
        public static string CompanionPathFor(DesignerMetadataAsset asset)
        {
            var assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath)) return null;
            return Path.ChangeExtension(assetPath, null) + ".json";
        }

        /// <summary>Writes the companion JSON next to the asset. Returns the written path, or null if the asset isn't saved to disk yet.</summary>
        public static string Export(DesignerMetadataAsset asset)
        {
            if (asset == null) return null;
            var path = CompanionPathFor(asset);
            if (string.IsNullOrEmpty(path)) return null;
            if (!path.Replace('\\', '/').StartsWith("Assets/", StringComparison.Ordinal))
            {
                Debug.LogError($"[NexUI] Companion JSON is read-only outside Assets: '{path}'.");
                return null;
            }

            var temp = path + ".nexui.tmp";
            try
            {
                var dto = ToDto(asset);
                var json = JsonUtility.ToJson(dto, true) + "\n";
                if (File.Exists(path) && File.ReadAllText(path).Replace("\r\n", "\n") == json.Replace("\r\n", "\n"))
                    return path;
                File.WriteAllText(temp, json, new System.Text.UTF8Encoding(false));
                File.Copy(temp, path, true);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                return path;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NexUI] Failed to export companion JSON '{path}': {ex}");
                return null;
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        /// <summary>Applies a companion JSON file's contents onto <paramref name="asset"/> (Undo-tracked). Returns false if the file is missing or fails to parse.</summary>
        public static bool Import(DesignerMetadataAsset asset)
        {
            if (asset == null) return false;
            var path = CompanionPathFor(asset);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;

            MetadataFileDto dto;
            try { dto = JsonUtility.FromJson<MetadataFileDto>(File.ReadAllText(path)); }
            catch (Exception ex)
            {
                Debug.LogError($"[NexUI] Failed to parse '{path}': {ex.Message}");
                return false;
            }
            if (dto == null) return false;

            Undo.RecordObject(asset, "Sync NexUI Metadata From JSON");
            ApplyDto(dto, asset);
            EditorUtility.SetDirty(asset);
            return true;
        }

        // ---- DTOs: fixed field order, no UnityEngine.Object references -----------------------

        [Serializable]
        private sealed class MetadataFileDto
        {
            public int formatVersion;
            public int schemaVersion;
            public string screenId;
            public List<ElementDto> elements = new();
            public ScreenMotionDto screenMotion = new();
            public List<VariantDto> variants = new();
            public List<ResponsiveDto> responsiveRules = new();
            public DesignerContractMetadata contract = new();
            public DesignerSnapshotMetadata snapshots = new();
            public DesignerLocalizationMetadata localization = new();
            public DesignerPromptMetadata prompts = new();
            public List<DesignerRecipeMetadata> recipes = new();
        }

        [Serializable]
        private sealed class ScreenMotionDto
        {
            public string entryClipGuid = "";
            public string exitClipGuid = "";
            public string stateMachineGuid = "";
            public string motionGraphGuid = "";
            public List<MotionBindingDto> bindings = new();
        }

        [Serializable]
        private sealed class MotionBindingDto
        {
            public string bindingId;
            public string targetElementId;
            public string trigger;
            public string stateId;
            public string commandId;
            public string clipGuid = "";
            public string reducedMotionClipGuid = "";
        }

        [Serializable]
        private sealed class ElementDto
        {
            public string stableId;
            public string elementId;
            public string parentId;
            public int siblingIndex;
            public string parentSlotId;
            public string displayName;
            public string elementType;
            public RectDto rect = new();
            public string anchorPreset;
            public string shape;
            public float previewValue;
            public int previewItemCount;
            public List<string> previewOptions = new();
            public List<DesignerAttachedComponentMetadata> attachedComponents = new();
            public List<ComponentPropertyDto> componentProperties = new();
            public List<ComponentPartOverrideDto> componentPartOverrides = new();
            public DesignerFillMetadata fill = new();
            public string previewImageGuid = "";
            public long previewImageLocalId;
            public string text;
            public ColorDto tint = new();
            public ColorDto textColor = new();
            public int fontSize;
            public List<string> classes = new();
            public BindingDto binding = new();
            public MotionDto motion = new();
            public ThemeDto theme = new();
            public DesignerAutoLayoutMetadata autoLayout = new();
            public DesignerConstraintMetadata constraint = new();
            public DesignerFocusMetadata focus = new();
            public bool locked;
            public bool hiddenInDesigner;
            public bool runtimeVisible;
            public bool clipChildren;
            public RectOffsetDto contentPadding = new();
            public string accessibilityLabel;
            public string accessibilityRole;
            public DesignerLayoutStyleMetadata layoutStyle = new();
            public DesignerVisualStyleMetadata visualStyle = new();
            public string visualMaterialGuid = "";
            public DesignerTypographyMetadata typography = new();
            public string fontAssetGuid = "";
            public long fontAssetLocalId;
            public string fontFallbackGuid = "";
            public long fontFallbackLocalId;
        }

        [Serializable]
        private sealed class PropertyValueDto
        {
            public DesignerPropertyValue value = new();
            public string assetGuid = "";
            public long assetLocalId;
        }

        [Serializable]
        private sealed class OverrideDto
        {
            public string elementId;
            public string targetElementId;
            public string propertyId;
            public PropertyValueDto typedValue = new();
            public string propertyPath;
            public string value;
        }

        [Serializable]
        private sealed class VariantDto
        {
            public string variantId;
            public string displayName;
            public bool isDefault;
            public List<OverrideDto> overrides = new();
        }

        [Serializable]
        private sealed class ResponsiveDto
        {
            public string ruleId;
            public Vector2Int minResolution;
            public Vector2Int maxResolution;
            public UIInputMode inputMode;
            public bool constrainInputMode;
            public List<OverrideDto> overrides = new();
        }

        [Serializable]
        private sealed class RectDto
        {
            public float x, y, width, height;
        }

        [Serializable]
        private sealed class ColorDto
        {
            public float r, g, b, a;
        }

        /// <summary>
        /// One component-property value. Asset references are written as persistent GUIDs, like every
        /// other asset reference in this file, so the JSON stays valid across machines.
        /// </summary>
        [Serializable]
        private sealed class ComponentPropertyDto
        {
            public string key;
            public string type;
            public float floatValue;
            public int intValue;
            public bool boolValue;
            public string stringValue = "";
            public ColorDto colorValue = new();
            public Vector2 vector2Value;
            public string assetGuid = "";
            public long assetLocalId;
        }

        [Serializable]
        private sealed class ComponentPartOverrideDto
        {
            public string partId;
            public bool hasPosition;
            public Vector2 position;
            public bool hasSizeDelta;
            public Vector2 sizeDelta;
            public bool hasRotation;
            public float rotation;
            public bool hasScale;
            public Vector2 scale = Vector2.one;
            public bool hasVisibility;
            public bool visible = true;
        }

        [Serializable]
        private sealed class RectOffsetDto
        {
            public int left, right, top, bottom;
            public bool hasValue;
        }

        [Serializable]
        private sealed class BindingDto
        {
            public string textKey, valueKey, visibilityKey, classKey, commandKey, interactableKey;
        }

        [Serializable]
        private sealed class MotionDto
        {
            /// <summary>Asset GUID of the motion preset, or empty when none - never a raw instance/file ID.</summary>
            public string motionPresetGuid = "";
            public string motionId;
            public string initialVariant;
            public string animateVariant;
            public string exitVariant;
            public string hoverVariant;
            public string pressedVariant;
            public string focusVariant;
        }

        [Serializable]
        private sealed class ThemeDto
        {
            /// <summary>Asset GUID of the theme, or empty when none.</summary>
            public string themeRefGuid = "";
            public string themeId;
            public List<string> classes = new();
            public List<TokenOverrideDto> tokenOverrides = new();
        }

        [Serializable]
        private sealed class TokenOverrideDto
        {
            public string key, value;
        }

        // ---- Mapping ---------------------------------------------------------------------------

        private static MetadataFileDto ToDto(DesignerMetadataAsset asset)
        {
            var screenMotion = asset.screenMotion ?? new DesignerScreenMotionMetadata();
            var dto = new MetadataFileDto
            {
                formatVersion = 6,
                schemaVersion = asset.schemaVersion,
                screenId = asset.screenId,
                variants = ToVariantDtos(asset.variants),
                responsiveRules = ToResponsiveDtos(asset.responsiveRules),
                contract = asset.contract ?? new DesignerContractMetadata(),
                snapshots = asset.snapshots ?? new DesignerSnapshotMetadata(),
                localization = asset.localization ?? new DesignerLocalizationMetadata(),
                prompts = asset.prompts ?? new DesignerPromptMetadata(),
                recipes = asset.recipes ?? new List<DesignerRecipeMetadata>(),
                screenMotion = new ScreenMotionDto
                {
                    entryClipGuid = AssetGuid(screenMotion.entryClip),
                    exitClipGuid = AssetGuid(screenMotion.exitClip),
                    stateMachineGuid = AssetGuid(screenMotion.stateMachine),
                    motionGraphGuid = AssetGuid(screenMotion.motionGraph),
                }
            };
            foreach (var binding in screenMotion.bindings)
            {
                if (binding == null) continue;
                dto.screenMotion.bindings.Add(new MotionBindingDto
                {
                    bindingId = binding.bindingId,
                    targetElementId = binding.targetElementId,
                    trigger = binding.trigger.ToString(),
                    stateId = binding.stateId,
                    commandId = binding.commandId,
                    clipGuid = AssetGuid(binding.clip),
                    reducedMotionClipGuid = AssetGuid(binding.reducedMotionClip),
                });
            }
            foreach (var e in asset.elements)
            {
                if (e == null) continue;
                dto.elements.Add(new ElementDto
                {
                    stableId = e.stableId,
                    elementId = e.elementId,
                    parentId = e.parentId,
                    siblingIndex = e.siblingIndex,
                    parentSlotId = e.parentSlotId,
                    displayName = e.displayName,
                    elementType = e.elementType,
                    rect = new RectDto { x = e.rect.x, y = e.rect.y, width = e.rect.width, height = e.rect.height },
                    anchorPreset = e.anchorPreset.ToString(),
                    shape = e.shape.ToString(),
                    previewValue = e.previewValue,
                    previewItemCount = e.previewItemCount,
                    previewOptions = e.previewOptions != null ? new List<string>(e.previewOptions) : new List<string>(),
                    attachedComponents = CloneAttachedComponents(e.attachedComponents),
                    componentProperties = ToComponentPropertyDtos(e.componentProperties),
                    componentPartOverrides = ToComponentPartOverrideDtos(e.componentPartOverrides),
                    fill = e.fill ?? new DesignerFillMetadata(),
                    previewImageGuid = AssetGuid(e.previewImage),
                    previewImageLocalId = AssetLocalId(e.previewImage),
                    text = e.text,
                    tint = new ColorDto { r = e.tint.r, g = e.tint.g, b = e.tint.b, a = e.tint.a },
                    textColor = new ColorDto { r = e.textColor.r, g = e.textColor.g, b = e.textColor.b, a = e.textColor.a },
                    fontSize = e.fontSize,
                    classes = new List<string>(e.classes),
                    binding = new BindingDto
                    {
                        textKey = e.binding?.textKey, valueKey = e.binding?.valueKey,
                        visibilityKey = e.binding?.visibilityKey, classKey = e.binding?.classKey,
                        commandKey = e.binding?.commandKey, interactableKey = e.binding?.interactableKey,
                    },
                    motion = new MotionDto
                    {
                        motionPresetGuid = AssetGuid(e.motion?.motionPreset),
                        motionId = e.motion?.motionId, initialVariant = e.motion?.initialVariant,
                        animateVariant = e.motion?.animateVariant,
                        exitVariant = e.motion?.exitVariant, hoverVariant = e.motion?.hoverVariant,
                        pressedVariant = e.motion?.pressedVariant, focusVariant = e.motion?.focusVariant,
                    },
                    theme = new ThemeDto
                    {
                        themeRefGuid = AssetGuid(e.theme?.themeRef),
                        themeId = e.theme?.themeId,
                        classes = e.theme != null ? new List<string>(e.theme.classes) : new List<string>(),
                        tokenOverrides = ToTokenDtos(e.theme?.tokenOverrides),
                    },
                    autoLayout = e.autoLayout ?? new DesignerAutoLayoutMetadata(),
                    constraint = e.constraint ?? new DesignerConstraintMetadata(),
                    focus = e.focus ?? new DesignerFocusMetadata(),
                    locked = e.locked,
                    hiddenInDesigner = e.hiddenInDesigner,
                    runtimeVisible = e.runtimeVisible,
                    clipChildren = e.clipChildren,
                    contentPadding = ToRectOffsetDto(e.contentPadding),
                    accessibilityLabel = e.accessibilityLabel,
                    accessibilityRole = e.accessibilityRole.ToString(),
                    layoutStyle = Clone(e.layoutStyle) ?? new DesignerLayoutStyleMetadata(),
                    visualStyle = VisualWithoutObject(e.visualStyle),
                    visualMaterialGuid = AssetGuid(e.visualStyle?.material),
                    typography = TypographyWithoutObjects(e.typography),
                    fontAssetGuid = AssetGuid(e.typography?.fontAsset),
                    fontAssetLocalId = AssetLocalId(e.typography?.fontAsset),
                    fontFallbackGuid = AssetGuid(e.typography?.fontFallback),
                    fontFallbackLocalId = AssetLocalId(e.typography?.fontFallback),
                });
            }
            return dto;
        }

        private static void ApplyDto(MetadataFileDto dto, DesignerMetadataAsset asset)
        {
            var hasFullSchema = dto.formatVersion >= 2;
            var hasTypedSchema = dto.formatVersion >= 3;
            var hasAttachedComponentSchema = dto.formatVersion >= 4;
            // Older files predate component properties; importing their (absent) list would wipe
            // values the asset already holds, so only a file that can carry them may overwrite them.
            var hasComponentPropertySchema = dto.formatVersion >= 5;
            var hasComponentPartSchema = dto.formatVersion >= 6;
            if (hasFullSchema)
                asset.schemaVersion = dto.schemaVersion;
            asset.screenId = dto.screenId;
            if (hasFullSchema)
            {
                asset.variants = FromVariantDtos(dto.variants);
                asset.responsiveRules = FromResponsiveDtos(dto.responsiveRules);
                asset.contract = dto.contract ?? new DesignerContractMetadata();
                asset.snapshots = dto.snapshots ?? new DesignerSnapshotMetadata();
                asset.localization = dto.localization ?? new DesignerLocalizationMetadata();
                asset.prompts = dto.prompts ?? new DesignerPromptMetadata();
                asset.recipes = dto.recipes ?? new List<DesignerRecipeMetadata>();
            }
            asset.screenMotion = new DesignerScreenMotionMetadata();
            if (dto.screenMotion != null)
            {
                asset.screenMotion.entryClip = ResolveAsset<UIMotionClip>(dto.screenMotion.entryClipGuid);
                asset.screenMotion.exitClip = ResolveAsset<UIMotionClip>(dto.screenMotion.exitClipGuid);
                asset.screenMotion.stateMachine = ResolveAsset<UIMotionStateMachine>(dto.screenMotion.stateMachineGuid);
                asset.screenMotion.motionGraph = ResolveAsset<UIMotionGraphAsset>(dto.screenMotion.motionGraphGuid);
                foreach (var d in dto.screenMotion.bindings ?? new List<MotionBindingDto>())
                {
                    asset.screenMotion.bindings.Add(new DesignerMotionBinding
                    {
                        bindingId = d.bindingId,
                        targetElementId = d.targetElementId,
                        trigger = ParseEnum(d.trigger, DesignerMotionTrigger.Click),
                        stateId = d.stateId,
                        commandId = d.commandId,
                        clip = ResolveAsset<UIMotionClip>(d.clipGuid),
                        reducedMotionClip = ResolveAsset<UIMotionClip>(d.reducedMotionClipGuid),
                    });
                }
            }
            var previousElements = new Dictionary<string, DesignerElementMetadata>();
            if (!hasFullSchema)
                foreach (var existing in asset.elements)
                    if (existing != null && !string.IsNullOrEmpty(existing.elementId) && !previousElements.ContainsKey(existing.elementId))
                        previousElements.Add(existing.elementId, existing);

            var importedElements = new List<DesignerElementMetadata>();
            foreach (var d in dto.elements ?? new List<ElementDto>())
            {
                var e = !hasFullSchema && !string.IsNullOrEmpty(d.elementId) && previousElements.TryGetValue(d.elementId, out var existing)
                    ? existing
                    : new DesignerElementMetadata();
                if (hasFullSchema) e.stableId = d.stableId;
                e.elementId = d.elementId;
                e.parentId = d.parentId;
                e.displayName = d.displayName;
                e.elementType = d.elementType;
                e.rect = new Rect(d.rect.x, d.rect.y, d.rect.width, d.rect.height);
                e.anchorPreset = ParseEnum(d.anchorPreset, DesignerAnchorPreset.TopLeft);
                e.text = d.text;
                e.tint = new Color(d.tint.r, d.tint.g, d.tint.b, d.tint.a);
                e.textColor = new Color(d.textColor.r, d.textColor.g, d.textColor.b, d.textColor.a);
                e.fontSize = d.fontSize;
                e.locked = d.locked;
                e.hiddenInDesigner = d.hiddenInDesigner;
                if (hasFullSchema) e.runtimeVisible = d.runtimeVisible;
                e.accessibilityLabel = d.accessibilityLabel;
                e.accessibilityRole = ParseEnum(d.accessibilityRole, AccessibilityRole.None);
                if (hasFullSchema)
                {
                    e.siblingIndex = d.siblingIndex;
                    e.parentSlotId = d.parentSlotId;
                    e.shape = ParseEnum(d.shape, DesignerElementShape.Rounded);
                    e.previewValue = d.previewValue;
                    e.previewItemCount = d.previewItemCount;
                    e.previewOptions.AddRange(d.previewOptions ?? new List<string>());
                    if (hasAttachedComponentSchema)
                        e.attachedComponents = CloneAttachedComponents(d.attachedComponents);
                    if (hasComponentPropertySchema)
                        e.componentProperties = FromComponentPropertyDtos(d.componentProperties);
                    if (hasComponentPartSchema)
                        e.componentPartOverrides = FromComponentPartOverrideDtos(d.componentPartOverrides);
                    e.fill = d.fill ?? new DesignerFillMetadata();
                    e.previewImage = ResolveAsset<Sprite>(d.previewImageGuid, d.previewImageLocalId);
                    e.autoLayout = d.autoLayout ?? new DesignerAutoLayoutMetadata();
                    e.constraint = d.constraint ?? new DesignerConstraintMetadata();
                    e.focus = d.focus ?? new DesignerFocusMetadata();
                    e.clipChildren = d.clipChildren;
                    e.contentPadding = FromRectOffsetDto(d.contentPadding);
                }
                if (hasTypedSchema)
                {
                    e.layoutStyle = d.layoutStyle ?? new DesignerLayoutStyleMetadata();
                    e.visualStyle = d.visualStyle ?? new DesignerVisualStyleMetadata();
                    e.visualStyle.material = ResolveAsset<Material>(d.visualMaterialGuid);
                    e.typography = d.typography ?? new DesignerTypographyMetadata();
                    e.typography.fontAsset = ResolveAsset<UnityEngine.Object>(d.fontAssetGuid, d.fontAssetLocalId);
                    e.typography.fontFallback = ResolveAsset<UnityEngine.Object>(d.fontFallbackGuid, d.fontFallbackLocalId);
                }
                e.classes ??= new List<string>();
                e.classes.Clear();
                e.classes.AddRange(d.classes ?? new List<string>());

                if (d.binding != null)
                {
                    e.binding ??= new DesignerBindingMetadata();
                    e.binding.textKey = d.binding.textKey;
                    e.binding.valueKey = d.binding.valueKey;
                    e.binding.visibilityKey = d.binding.visibilityKey;
                    e.binding.classKey = d.binding.classKey;
                    e.binding.commandKey = d.binding.commandKey;
                    e.binding.interactableKey = d.binding.interactableKey;
                }
                if (d.motion != null)
                {
                    e.motion ??= new DesignerMotionMetadata();
                    e.motion.motionPreset = ResolveAsset<UIMotionPreset>(d.motion.motionPresetGuid);
                    e.motion.motionId = d.motion.motionId;
                    e.motion.initialVariant = d.motion.initialVariant;
                    e.motion.animateVariant = d.motion.animateVariant;
                    e.motion.exitVariant = d.motion.exitVariant;
                    e.motion.hoverVariant = d.motion.hoverVariant;
                    e.motion.pressedVariant = d.motion.pressedVariant;
                    e.motion.focusVariant = d.motion.focusVariant;
                }
                if (d.theme != null)
                {
                    e.theme ??= new DesignerThemeMetadata();
                    e.theme.themeRef = ResolveAsset<UITheme>(d.theme.themeRefGuid);
                    e.theme.themeId = d.theme.themeId;
                    e.theme.classes ??= new List<string>();
                    e.theme.classes.Clear();
                    e.theme.classes.AddRange(d.theme.classes ?? new List<string>());
                    e.theme.tokenOverrides ??= new List<DesignerTokenOverride>();
                    e.theme.tokenOverrides.Clear();
                    foreach (var t in d.theme.tokenOverrides ?? new List<TokenOverrideDto>())
                        e.theme.tokenOverrides.Add(new DesignerTokenOverride { key = t.key, value = t.value });
                }

                importedElements.Add(e);
            }
            asset.elements = importedElements;
            DesignerHierarchyMigration.Migrate(asset, recordUndo: false);
        }

        private static List<ComponentPropertyDto> ToComponentPropertyDtos(List<DesignerComponentPropertyEntry> source)
        {
            var result = new List<ComponentPropertyDto>();
            if (source == null) return result;
            foreach (var entry in source)
            {
                if (entry == null || string.IsNullOrEmpty(entry.key) || entry.value == null) continue;
                var value = entry.value;
                result.Add(new ComponentPropertyDto
                {
                    key = entry.key,
                    type = value.type.ToString(),
                    floatValue = value.floatValue,
                    intValue = value.intValue,
                    boolValue = value.boolValue,
                    stringValue = value.stringValue ?? "",
                    colorValue = new ColorDto { r = value.colorValue.r, g = value.colorValue.g, b = value.colorValue.b, a = value.colorValue.a },
                    vector2Value = value.vector2Value,
                    assetGuid = AssetGuid(value.assetValue),
                    assetLocalId = AssetLocalId(value.assetValue)
                });
            }
            return result;
        }

        private static List<ComponentPartOverrideDto> ToComponentPartOverrideDtos(
            List<DesignerComponentPartOverrideMetadata> source)
        {
            var result = new List<ComponentPartOverrideDto>();
            if (source == null) return result;
            foreach (var item in source)
            {
                if (item == null || string.IsNullOrEmpty(item.partId) || !item.HasAnyOverride) continue;
                result.Add(new ComponentPartOverrideDto
                {
                    partId = item.partId,
                    hasPosition = item.hasPosition,
                    position = item.position,
                    hasSizeDelta = item.hasSizeDelta,
                    sizeDelta = item.sizeDelta,
                    hasRotation = item.hasRotation,
                    rotation = item.rotation,
                    hasScale = item.hasScale,
                    scale = item.scale,
                    hasVisibility = item.hasVisibility,
                    visible = item.visible
                });
            }
            return result;
        }

        private static List<DesignerComponentPartOverrideMetadata> FromComponentPartOverrideDtos(
            List<ComponentPartOverrideDto> source)
        {
            var result = new List<DesignerComponentPartOverrideMetadata>();
            if (source == null) return result;
            foreach (var item in source)
            {
                if (item == null || string.IsNullOrEmpty(item.partId)) continue;
                result.Add(new DesignerComponentPartOverrideMetadata
                {
                    partId = item.partId,
                    hasPosition = item.hasPosition,
                    position = item.position,
                    hasSizeDelta = item.hasSizeDelta,
                    sizeDelta = item.sizeDelta,
                    hasRotation = item.hasRotation,
                    rotation = item.rotation,
                    hasScale = item.hasScale,
                    scale = item.scale,
                    hasVisibility = item.hasVisibility,
                    visible = item.visible
                });
            }
            return result;
        }

        private static List<DesignerComponentPropertyEntry> FromComponentPropertyDtos(List<ComponentPropertyDto> source)
        {
            var result = new List<DesignerComponentPropertyEntry>();
            if (source == null) return result;
            foreach (var dto in source)
            {
                if (dto == null || string.IsNullOrEmpty(dto.key)) continue;
                var value = new DesignerPropertyValue
                {
                    type = ParseEnum(dto.type, DesignerPropertyValueType.None),
                    floatValue = dto.floatValue,
                    intValue = dto.intValue,
                    boolValue = dto.boolValue,
                    stringValue = dto.stringValue,
                    colorValue = dto.colorValue != null
                        ? new Color(dto.colorValue.r, dto.colorValue.g, dto.colorValue.b, dto.colorValue.a)
                        : Color.white,
                    vector2Value = dto.vector2Value,
                    assetValue = ResolveAsset<UnityEngine.Object>(dto.assetGuid, dto.assetLocalId)
                };
                result.Add(new DesignerComponentPropertyEntry(dto.key, value));
            }
            return result;
        }

        private static List<DesignerAttachedComponentMetadata> CloneAttachedComponents(
            List<DesignerAttachedComponentMetadata> source)
        {
            var result = new List<DesignerAttachedComponentMetadata>();
            if (source == null) return result;
            foreach (var item in source)
                if (item != null)
                    result.Add(new DesignerAttachedComponentMetadata { typeName = item.typeName });
            return result;
        }

        private static List<TokenOverrideDto> ToTokenDtos(List<DesignerTokenOverride> overrides)
        {
            var list = new List<TokenOverrideDto>();
            if (overrides == null) return list;
            foreach (var o in overrides)
                list.Add(new TokenOverrideDto { key = o.key, value = o.value });
            return list;
        }

        private static T Clone<T>(T value) where T : class
            => value == null ? null : JsonUtility.FromJson<T>(JsonUtility.ToJson(value));

        private static DesignerVisualStyleMetadata VisualWithoutObject(DesignerVisualStyleMetadata source)
        {
            var copy = Clone(source) ?? new DesignerVisualStyleMetadata();
            copy.material = null;
            return copy;
        }

        private static DesignerTypographyMetadata TypographyWithoutObjects(DesignerTypographyMetadata source)
        {
            var copy = Clone(source) ?? new DesignerTypographyMetadata();
            copy.fontAsset = null;
            copy.fontFallback = null;
            return copy;
        }

        private static PropertyValueDto ToPropertyValueDto(DesignerPropertyValue source)
        {
            var copy = source?.Clone() ?? new DesignerPropertyValue();
            copy.assetValue = null;
            return new PropertyValueDto
            {
                value = copy,
                assetGuid = AssetGuid(source?.assetValue),
                assetLocalId = AssetLocalId(source?.assetValue)
            };
        }

        private static DesignerPropertyValue FromPropertyValueDto(PropertyValueDto source)
        {
            var value = Clone(source?.value) ?? new DesignerPropertyValue();
            if (source != null)
                value.assetValue = ResolveAsset<UnityEngine.Object>(source.assetGuid, source.assetLocalId);
            return value;
        }

        private static List<VariantDto> ToVariantDtos(List<DesignerVariantMetadata> variants)
        {
            var result = new List<VariantDto>();
            if (variants == null) return result;
            foreach (var source in variants)
            {
                if (source == null) continue;
                var dto = new VariantDto { variantId = source.variantId, displayName = source.displayName, isDefault = source.isDefault };
                foreach (var item in source.overrides ?? new List<DesignerVariantOverrideMetadata>())
                    if (item != null) dto.overrides.Add(new OverrideDto
                    {
                        targetElementId = item.targetElementId,
                        propertyId = item.propertyId.ToString(),
                        typedValue = ToPropertyValueDto(item.typedValue),
                        propertyPath = item.propertyPath,
                        value = item.value
                    });
                result.Add(dto);
            }
            return result;
        }

        private static List<DesignerVariantMetadata> FromVariantDtos(List<VariantDto> variants)
        {
            var result = new List<DesignerVariantMetadata>();
            foreach (var source in variants ?? new List<VariantDto>())
            {
                if (source == null) continue;
                var item = new DesignerVariantMetadata { variantId = source.variantId, displayName = source.displayName, isDefault = source.isDefault };
                foreach (var value in source.overrides ?? new List<OverrideDto>())
                    if (value != null) item.overrides.Add(new DesignerVariantOverrideMetadata
                    {
                        targetElementId = value.targetElementId,
                        propertyId = ParseEnum(value.propertyId, DesignerPropertyId.None),
                        typedValue = FromPropertyValueDto(value.typedValue),
                        propertyPath = value.propertyPath,
                        value = value.value
                    });
                result.Add(item);
            }
            return result;
        }

        private static List<ResponsiveDto> ToResponsiveDtos(List<DesignerResponsiveMetadata> rules)
        {
            var result = new List<ResponsiveDto>();
            if (rules == null) return result;
            foreach (var source in rules)
            {
                if (source == null) continue;
                var dto = new ResponsiveDto
                {
                    ruleId = source.ruleId, minResolution = source.minResolution, maxResolution = source.maxResolution,
                    inputMode = source.inputMode, constrainInputMode = source.constrainInputMode
                };
                foreach (var item in source.overrides ?? new List<DesignerResponsiveOverrideMetadata>())
                    if (item != null) dto.overrides.Add(new OverrideDto
                    {
                        elementId = item.elementId,
                        propertyId = item.propertyId.ToString(),
                        typedValue = ToPropertyValueDto(item.typedValue),
                        propertyPath = item.propertyPath,
                        value = item.value
                    });
                result.Add(dto);
            }
            return result;
        }

        private static List<DesignerResponsiveMetadata> FromResponsiveDtos(List<ResponsiveDto> rules)
        {
            var result = new List<DesignerResponsiveMetadata>();
            foreach (var source in rules ?? new List<ResponsiveDto>())
            {
                if (source == null) continue;
                var item = new DesignerResponsiveMetadata
                {
                    ruleId = source.ruleId, minResolution = source.minResolution, maxResolution = source.maxResolution,
                    inputMode = source.inputMode, constrainInputMode = source.constrainInputMode
                };
                foreach (var value in source.overrides ?? new List<OverrideDto>())
                    if (value != null) item.overrides.Add(new DesignerResponsiveOverrideMetadata
                    {
                        elementId = value.elementId,
                        propertyId = ParseEnum(value.propertyId, DesignerPropertyId.None),
                        typedValue = FromPropertyValueDto(value.typedValue),
                        propertyPath = value.propertyPath,
                        value = value.value
                    });
                result.Add(item);
            }
            return result;
        }

        private static RectOffsetDto ToRectOffsetDto(RectOffset value)
            => value == null
                ? new RectOffsetDto()
                : new RectOffsetDto
                {
                    hasValue = true,
                    left = value.left,
                    right = value.right,
                    top = value.top,
                    bottom = value.bottom
                };

        private static RectOffset FromRectOffsetDto(RectOffsetDto value)
            => value == null || !value.hasValue
                ? null
                : new RectOffset(value.left, value.right, value.top, value.bottom);

        private static string AssetGuid(UnityEngine.Object obj)
        {
            if (obj == null) return "";
            var path = AssetDatabase.GetAssetPath(obj);
            return string.IsNullOrEmpty(path) ? "" : AssetDatabase.AssetPathToGUID(path);
        }

        private static long AssetLocalId(UnityEngine.Object obj)
            => obj != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out _, out long localId)
                ? localId
                : 0L;

        private static T ResolveAsset<T>(string guid) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(guid)) return null;
            var path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static T ResolveAsset<T>(string guid, long localId) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(guid)) return null;
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return null;
            if (localId != 0L)
            {
                foreach (var candidate in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (candidate is T typed &&
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(candidate, out _, out long candidateLocalId) &&
                        candidateLocalId == localId)
                        return typed;
            }
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static TEnum ParseEnum<TEnum>(string value, TEnum fallback) where TEnum : struct
            => !string.IsNullOrEmpty(value) && Enum.TryParse<TEnum>(value, out var parsed) ? parsed : fallback;
    }
}
