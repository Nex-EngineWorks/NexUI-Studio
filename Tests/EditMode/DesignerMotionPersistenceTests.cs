using System.Linq;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Core;
using emiteat.NexUI.Designer.Editor.Validation;
using emiteat.NexUI.Designer.Editor;
using emiteat.NexUI.Designer.Editor.Serialization;
using emiteat.NexUI.MotionClip;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    public sealed class DesignerMotionPersistenceTests
    {
        private const string TempFolder = "Assets/NexUIDesignerMotionTests";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder)) AssetDatabase.CreateFolder("Assets", "NexUIDesignerMotionTests");
            Undo.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TempFolder);
            Undo.ClearAll();
        }

        [Test]
        public void MotionBinding_SurvivesAssetSaveAndReload()
        {
            var clip = ScriptableObject.CreateInstance<UIMotionClip>();
            AssetDatabase.CreateAsset(clip, TempFolder + "/Hover.asset");
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            metadata.screenId = "inventory";
            metadata.elements.Add(new DesignerElementMetadata { elementId = "slot1" });
            metadata.screenMotion.entryClip = clip;
            metadata.screenMotion.exitClip = clip;
            metadata.screenMotion.bindings.Add(new DesignerMotionBinding
            {
                bindingId = "hover-slot1", targetElementId = "slot1",
                trigger = DesignerMotionTrigger.HoverEnter, clip = clip, reducedMotionClip = clip
            });
            var path = TempFolder + "/Inventory.Metadata.asset";
            AssetDatabase.CreateAsset(metadata, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var loaded = AssetDatabase.LoadAssetAtPath<DesignerMetadataAsset>(path);
            Assert.That(loaded.screenMotion.entryClip, Is.SameAs(clip));
            Assert.That(loaded.screenMotion.exitClip, Is.SameAs(clip));
            Assert.That(loaded.screenMotion.bindings.Single().targetElementId, Is.EqualTo("slot1"));
            Assert.That(loaded.screenMotion.bindings.Single().reducedMotionClip, Is.SameAs(clip));
        }

        [Test]
        public void RenameElement_UpdatesMotionTarget_AndUndoRestoresBoth()
        {
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            var element = new DesignerElementMetadata { elementId = "oldId" };
            metadata.elements.Add(element);
            metadata.screenMotion.bindings.Add(new DesignerMotionBinding { bindingId = "b", targetElementId = "oldId" });
            var context = new NexUIDesignerContext();
            context.SetMetadata(metadata);
            context.RenameElementId(element, "newId");
            Assert.That(element.elementId, Is.EqualTo("newId"));
            Assert.That(metadata.screenMotion.bindings[0].targetElementId, Is.EqualTo("newId"));
            Undo.PerformUndo();
            Assert.That(element.elementId, Is.EqualTo("oldId"));
            Assert.That(metadata.screenMotion.bindings[0].targetElementId, Is.EqualTo("oldId"));
            context.Dispose();
            Object.DestroyImmediate(metadata);
        }

        [Test]
        public void DeletedTargetAndMissingClip_AreValidationErrors()
        {
            var screen = ScriptableObject.CreateInstance<UIScreenDefinition>();
            screen.identity = new UIScreenIdentity { screenId = "inventory" };
            screen.backendAsset = new UIScreenBackendAsset { backend = UIRenderBackend.UIToolkit };
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            metadata.screenId = "inventory";
            metadata.screenMotion.bindings.Add(new DesignerMotionBinding
            {
                bindingId = "broken", targetElementId = "deletedSlot", trigger = DesignerMotionTrigger.Click
            });
            var issues = DesignerValidationService.Validate(screen, metadata);
            Assert.That(issues.Any(i => i.Code == "motion-target-missing"), Is.True);
            Assert.That(issues.Any(i => i.Code == "motion-clip-missing"), Is.True);
            Object.DestroyImmediate(metadata);
            Object.DestroyImmediate(screen);
        }

        [Test]
        public void CompanionJson_RoundTripsFullMetadataSchema()
        {
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            metadata.schemaVersion = DesignerMetadataAsset.CurrentSchemaVersion;
            metadata.screenId = "inventory";
            metadata.elements.Add(new DesignerElementMetadata
            {
                stableId = "stable-slot-1",
                elementId = "slot1",
                parentId = "grid",
                siblingIndex = 3,
                parentSlotId = "content",
                shape = DesignerElementShape.Circle,
                previewValue = 42f,
                previewItemCount = 7,
                previewOptions = { "Sword", "Shield" },
                fill = new DesignerFillMetadata { minValue = 10f, maxValue = 90f, direction = DesignerFillDirection.RightToLeft },
                autoLayout = new DesignerAutoLayoutMetadata { enabled = true, spacing = 12f, gridColumns = 4 },
                constraint = new DesignerConstraintMetadata { horizontal = DesignerConstraintMode.Scale, vertical = DesignerConstraintMode.End },
                focus = new DesignerFocusMetadata { leftElementId = "slot0", isDefaultFocus = true },
                clipChildren = true,
                contentPadding = new RectOffset(1, 2, 3, 4),
                layoutStyle = new DesignerLayoutStyleMetadata
                {
                    hasOverrides = true, minSize = new Vector2(64, 32), maxSize = new Vector2(256, 128),
                    rotation = 15f, scale = new Vector2(1.1f, .9f), marginLeft = 5f
                },
                visualStyle = new DesignerVisualStyleMetadata
                {
                    hasOverrides = true, backgroundColor = Color.magenta, opacity = .7f,
                    borderWidth = 2f, cornerRadius = 12f, blur = 3f
                },
                typography = new DesignerTypographyMetadata
                {
                    hasOverrides = true, fontSize = 26f, color = Color.cyan,
                    fontWeight = DesignerFontWeight.Bold, rightToLeft = true, lineHeight = 1.5f
                }
            });
            var variant = new DesignerVariantMetadata { variantId = "compact", displayName = "Compact" };
            variant.overrides.Add(new DesignerVariantOverrideMetadata
            {
                targetElementId = "slot1", propertyId = DesignerPropertyId.Opacity,
                typedValue = new DesignerPropertyValue { type = DesignerPropertyValueType.Float, floatValue = .5f }
            });
            metadata.variants.Add(variant);
            var responsive = new DesignerResponsiveMetadata { ruleId = "small", minResolution = new Vector2Int(320, 180) };
            responsive.overrides.Add(new DesignerResponsiveOverrideMetadata
            {
                elementId = "slot1", propertyId = DesignerPropertyId.Scale,
                typedValue = new DesignerPropertyValue { type = DesignerPropertyValueType.Vector2, vector2Value = new Vector2(.8f, .8f) }
            });
            metadata.responsiveRules.Add(responsive);
            metadata.contract = new DesignerContractMetadata { contractId = "inventory-contract", screenId = "inventory" };
            metadata.localization = new DesignerLocalizationMetadata { screenId = "inventory" };
            metadata.recipes.Add(new DesignerRecipeMetadata { recipeId = "inventory-grid", idPrefix = "inv" });
            AssetDatabase.CreateAsset(metadata, TempFolder + "/Full.Metadata.asset");
            AssetDatabase.SaveAssets();

            Assert.That(DesignerMetadataJsonSerializer.Export(metadata), Is.Not.Null);
            metadata.elements.Clear();
            metadata.variants.Clear();
            metadata.responsiveRules.Clear();
            metadata.contract.contractId = "changed";
            metadata.localization.screenId = "changed";
            metadata.recipes.Clear();

            Assert.That(DesignerMetadataJsonSerializer.Import(metadata), Is.True);
            var element = metadata.elements.Single();
            Assert.That(element.siblingIndex, Is.EqualTo(3));
            Assert.That(element.stableId, Is.EqualTo("stable-slot-1"));
            Assert.That(element.parentSlotId, Is.EqualTo("content"));
            Assert.That(element.shape, Is.EqualTo(DesignerElementShape.Circle));
            Assert.That(element.previewOptions, Is.EqualTo(new[] { "Sword", "Shield" }));
            Assert.That(element.fill.direction, Is.EqualTo(DesignerFillDirection.RightToLeft));
            Assert.That(element.autoLayout.enabled, Is.True);
            Assert.That(element.constraint.horizontal, Is.EqualTo(DesignerConstraintMode.Scale));
            Assert.That(element.focus.isDefaultFocus, Is.True);
            Assert.That(element.clipChildren, Is.True);
            Assert.That(element.contentPadding.left, Is.EqualTo(1));
            Assert.That(element.contentPadding.bottom, Is.EqualTo(4));
            Assert.That(element.layoutStyle.maxSize.x, Is.EqualTo(256f));
            Assert.That(element.layoutStyle.rotation, Is.EqualTo(15f));
            Assert.That(element.visualStyle.opacity, Is.EqualTo(.7f));
            Assert.That(element.visualStyle.blur, Is.EqualTo(3f));
            Assert.That(element.typography.fontWeight, Is.EqualTo(DesignerFontWeight.Bold));
            Assert.That(element.typography.rightToLeft, Is.True);
            Assert.That(metadata.variants.Single().variantId, Is.EqualTo("compact"));
            Assert.That(metadata.variants.Single().overrides.Single().propertyId, Is.EqualTo(DesignerPropertyId.Opacity));
            Assert.That(metadata.variants.Single().overrides.Single().typedValue.floatValue, Is.EqualTo(.5f));
            Assert.That(metadata.responsiveRules.Single().ruleId, Is.EqualTo("small"));
            Assert.That(metadata.responsiveRules.Single().overrides.Single().propertyId, Is.EqualTo(DesignerPropertyId.Scale));
            Assert.That(metadata.responsiveRules.Single().overrides.Single().typedValue.vector2Value, Is.EqualTo(new Vector2(.8f, .8f)));
            Assert.That(metadata.contract.contractId, Is.EqualTo("inventory-contract"));
            Assert.That(metadata.localization.screenId, Is.EqualTo("inventory"));
            Assert.That(metadata.recipes.Single().recipeId, Is.EqualTo("inventory-grid"));
        }

        [Test]
        public void UguiSave_UsesStableIdentityAcrossRenameAndAppliesRuntimeVisibility()
        {
            var root = new GameObject("Screen", typeof(RectTransform));
            var child = new GameObject("oldName", typeof(RectTransform));
            child.transform.SetParent(root.transform, false);
            AddBindingTag(child, "stable-button", "oldName", "DesignerOwned");
            var prefabPath = TempFolder + "/Stable.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            var screen = ScriptableObject.CreateInstance<UIScreenDefinition>();
            screen.identity = new UIScreenIdentity { screenId = "stable-screen" };
            screen.backendAsset = new UIScreenBackendAsset { backend = UIRenderBackend.UGUI, asset = prefab };
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            metadata.screenId = "stable-screen";
            metadata.elements.Add(new DesignerElementMetadata
            {
                stableId = "stable-button", elementId = "renamedButton", runtimeVisible = false
            });

            var report = new UGUIAssetSerializer().Save(screen, metadata);
            var saved = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var savedTags = GetBindingTags(saved);
                Assert.That(report.HasErrors, Is.False, report.Details());
                Assert.That(savedTags.Length, Is.EqualTo(1), "rename must reuse the same GameObject");
                Assert.That(GetTagField(savedTags[0], "stableId"), Is.EqualTo("stable-button"));
                Assert.That(GetTagField(savedTags[0], "elementId"), Is.EqualTo("renamedButton"));
                Assert.That(savedTags[0].gameObject.name, Is.EqualTo("renamedButton"));
                Assert.That(savedTags[0].gameObject.activeSelf, Is.False);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(saved);
                Object.DestroyImmediate(metadata);
                Object.DestroyImmediate(screen);
            }
        }

        [Test]
        public void UguiSave_ReportsDuplicateStableIdsAndPreservesOrphans()
        {
            var root = new GameObject("Screen", typeof(RectTransform));
            for (var i = 0; i < 2; i++)
            {
                var child = new GameObject("duplicate" + i, typeof(RectTransform));
                child.transform.SetParent(root.transform, false);
                AddBindingTag(child, "duplicate-stable", child.name, "DesignerOwned");
            }
            var orphan = new GameObject("orphan", typeof(RectTransform));
            orphan.transform.SetParent(root.transform, false);
            AddBindingTag(orphan, "orphan-stable", "orphan", "DesignerOwned");
            var prefabPath = TempFolder + "/Duplicate.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            var screen = ScriptableObject.CreateInstance<UIScreenDefinition>();
            screen.identity = new UIScreenIdentity { screenId = "duplicate-screen" };
            screen.backendAsset = new UIScreenBackendAsset { backend = UIRenderBackend.UGUI, asset = prefab };
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            metadata.screenId = "duplicate-screen";
            metadata.elements.Add(new DesignerElementMetadata { stableId = "duplicate-stable", elementId = "target" });

            var report = new UGUIAssetSerializer().Save(screen, metadata);
            var saved = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Assert.That(report.HasErrors, Is.True);
                Assert.That(report.Errors.Any(e => e.Contains("Duplicate prefab stableId")), Is.True);
                Assert.That(report.Warnings.Any(e => e.Contains("Orphaned Designer-owned object")), Is.True);
                Assert.That(GetBindingTags(saved).Length, Is.EqualTo(3));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(saved);
                Object.DestroyImmediate(metadata);
                Object.DestroyImmediate(screen);
            }
        }

        [Test]
        public void UguiSave_AppliesTypedLayoutVisualAndTypography()
        {
            var root = new GameObject("Screen", typeof(RectTransform));
            var prefabPath = TempFolder + "/Typed.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            var screen = ScriptableObject.CreateInstance<UIScreenDefinition>();
            screen.identity = new UIScreenIdentity { screenId = "typed-screen" };
            screen.backendAsset = new UIScreenBackendAsset { backend = UIRenderBackend.UGUI, asset = prefab };
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            metadata.schemaVersion = DesignerMetadataAsset.CurrentSchemaVersion;
            metadata.screenId = "typed-screen";
            metadata.elements.Add(new DesignerElementMetadata
            {
                stableId = "typed-button", elementId = "play", elementType = "Button", text = "Play",
                layoutStyle = new DesignerLayoutStyleMetadata
                {
                    hasOverrides = true, minSize = new Vector2(80, 30), pivot = new Vector2(.5f, .5f),
                    rotation = 10f, scale = new Vector2(1.2f, .8f), aspectRatio = 2f,
                    overflow = DesignerOverflowMode.Hidden
                },
                visualStyle = new DesignerVisualStyleMetadata
                {
                    hasOverrides = true, backgroundColor = Color.red, opacity = .6f,
                    borderWidth = 2f, borderColor = Color.yellow, dropShadow = true
                },
                typography = new DesignerTypographyMetadata
                {
                    hasOverrides = true, fontSize = 28f, color = Color.cyan,
                    fontStyle = DesignerFontStyle.Bold | DesignerFontStyle.Italic,
                    alignment = DesignerTextAlignment.UpperRight, wrapping = false, ellipsis = true,
                    letterSpacing = 3f, outlineWidth = 1f
                }
            });

            var report = new UGUIAssetSerializer().Save(screen, metadata);
            var saved = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var button = saved.transform.Find("play").gameObject;
                Assert.That(report.HasErrors, Is.False, report.Details());
                Assert.That(button.GetComponent<LayoutElement>().minWidth, Is.EqualTo(80f));
                Assert.That(button.GetComponent<AspectRatioFitter>().aspectRatio, Is.EqualTo(2f));
                Assert.That(button.GetComponent<RectMask2D>(), Is.Not.Null);
                Assert.That(button.GetComponent<CanvasGroup>().alpha, Is.EqualTo(.6f));
                Assert.That(button.GetComponent<Image>().color, Is.EqualTo(Color.red));
                Assert.That(button.GetComponent<UnityEngine.UI.Outline>().effectDistance, Is.EqualTo(new Vector2(2f, -2f)));
                Assert.That(button.GetComponents<UnityEngine.UI.Shadow>().Length, Is.GreaterThanOrEqualTo(2),
                    "border Outline and drop Shadow must be distinct effects");
                Assert.That(button.GetComponent<RectTransform>().localScale.x, Is.EqualTo(1.2f));
                var tmpType = System.Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro", true);
                var tmp = button.GetComponentInChildren(tmpType, true);
                Assert.That((float)tmpType.GetProperty("fontSize").GetValue(tmp), Is.EqualTo(28f));
                Assert.That((Color)tmpType.GetProperty("color").GetValue(tmp), Is.EqualTo(Color.cyan));
                Assert.That(tmpType.GetProperty("textWrappingMode").GetValue(tmp).ToString(), Is.EqualTo("NoWrap"));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(saved);
                Object.DestroyImmediate(metadata);
                Object.DestroyImmediate(screen);
            }
        }

        [Test]
        public void UguiSave_CreatesStockControlHierarchiesAndSynchronizesAttachedComponents()
        {
            var root = new GameObject("Screen", typeof(RectTransform));
            var prefabPath = TempFolder + "/StockControls.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            var screen = ScriptableObject.CreateInstance<UIScreenDefinition>();
            screen.identity = new UIScreenIdentity { screenId = "stock-controls" };
            screen.backendAsset = new UIScreenBackendAsset { backend = UIRenderBackend.UGUI, asset = prefab };
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            metadata.schemaVersion = DesignerMetadataAsset.CurrentSchemaVersion;
            metadata.screenId = "stock-controls";
            metadata.elements.Add(new DesignerElementMetadata
            {
                stableId = "toggle-stable", elementId = "sound", elementType = "UGUI.Toggle", text = "Sound",
                previewValue = 1f, rect = new Rect(10, 10, 160, 20),
                attachedComponents = new System.Collections.Generic.List<DesignerAttachedComponentMetadata>
                {
                    new DesignerAttachedComponentMetadata
                    {
                        typeName = typeof(CanvasGroup).FullName + ", " + typeof(CanvasGroup).Assembly.GetName().Name
                    }
                }
            });
            metadata.elements.Add(new DesignerElementMetadata
            {
                stableId = "slider-stable", elementId = "volume", elementType = "UGUI.Slider",
                previewValue = 75f, rect = new Rect(10, 40, 160, 20)
            });
            metadata.elements.Add(new DesignerElementMetadata
            {
                stableId = "scroll-stable", elementId = "items", elementType = "UGUI.ScrollView",
                rect = new Rect(10, 70, 240, 200)
            });

            var report = new UGUIAssetSerializer().Save(screen, metadata);
            var saved = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Assert.That(report.HasErrors, Is.False, report.Details());
                var toggle = saved.transform.Find("sound");
                Assert.That(toggle.GetComponent<Toggle>(), Is.Not.Null);
                Assert.That(toggle.Find("Background/Checkmark"), Is.Not.Null);
                Assert.That(toggle.GetComponent<Toggle>().isOn, Is.True);
                Assert.That(toggle.GetComponent<CanvasGroup>(), Is.Not.Null);
                Assert.That(toggle.GetComponent<DesignerAttachedComponentTracker>(), Is.Not.Null);

                var slider = saved.transform.Find("volume");
                Assert.That(slider.GetComponent<Slider>(), Is.Not.Null);
                Assert.That(slider.Find("Fill Area/Fill"), Is.Not.Null);
                Assert.That(slider.GetComponent<Slider>().value, Is.EqualTo(75f));

                var scroll = saved.transform.Find("items");
                Assert.That(scroll.GetComponent<ScrollRect>(), Is.Not.Null);
                Assert.That(scroll.Find("Viewport/Content"), Is.Not.Null);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(saved);
                Object.DestroyImmediate(metadata);
                Object.DestroyImmediate(screen);
            }
        }

        [Test]
        public void SavePreview_IsReadOnlyAndCategorizesCreateUnsupportedAndFallback()
        {
            var root = new GameObject("Screen", typeof(RectTransform));
            var prefabPath = TempFolder + "/Preview.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            var screen = ScriptableObject.CreateInstance<UIScreenDefinition>();
            screen.identity = new UIScreenIdentity { screenId = "preview-screen" };
            screen.backendAsset = new UIScreenBackendAsset { backend = UIRenderBackend.UGUI, asset = prefab };
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            metadata.screenId = "preview-screen";
            metadata.elements.Add(new DesignerElementMetadata
            {
                stableId = "new-element", elementId = "new", elementType = "Panel",
                layoutStyle = new DesignerLayoutStyleMetadata { hasOverrides = true, maxSize = new Vector2(200, 100) },
                visualStyle = new DesignerVisualStyleMetadata { hasOverrides = true, blur = 4f, borderWidth = 2f }
            });

            var before = GetBindingTags(prefab).Length;
            var report = DesignerSavePreviewService.Preview(screen, metadata);
            var after = GetBindingTags(prefab).Length;
            Assert.That(report.IsPreview, Is.True);
            Assert.That(report.Count(DesignerSaveImpactKind.Created), Is.EqualTo(1));
            Assert.That(report.Count(DesignerSaveImpactKind.Unsupported), Is.GreaterThanOrEqualTo(2));
            Assert.That(report.Count(DesignerSaveImpactKind.UserImpact), Is.GreaterThanOrEqualTo(1));
            Assert.That(after, Is.EqualTo(before), "preview must not mutate the prefab asset");
            Object.DestroyImmediate(metadata);
            Object.DestroyImmediate(screen);
        }

        [Test]
        public void DiscardUnsavedChanges_RestoresMetadataObject()
        {
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            metadata.schemaVersion = DesignerMetadataAsset.CurrentSchemaVersion;
            metadata.screenId = "inventory";
            metadata.elements.Add(new DesignerElementMetadata { elementId = "title", text = "Before" });
            var context = new NexUIDesignerContext();
            context.SetMetadata(metadata);

            context.UpdateElement(metadata.elements[0], e => e.text = "After", "Change title");
            Assert.That(metadata.elements[0].text, Is.EqualTo("After"));

            context.DiscardUnsavedChanges();
            Assert.That(metadata.elements.Single().text, Is.EqualTo("Before"));
            Assert.That(context.HasUnsavedChanges, Is.False);
            context.Dispose();
            Object.DestroyImmediate(metadata);
        }

        [Test]
        public void DiscardUnsavedChanges_RestoresScreenAndMetadataThroughOnePath()
        {
            var screen = ScriptableObject.CreateInstance<UIScreenDefinition>();
            screen.identity = new UIScreenIdentity { screenId = "before-screen" };
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            metadata.schemaVersion = DesignerMetadataAsset.CurrentSchemaVersion;
            metadata.screenId = "before-screen";
            metadata.elements.Add(new DesignerElementMetadata { elementId = "title", text = "Before" });
            var context = new NexUIDesignerContext();
            context.Open(screen);
            context.SetMetadata(metadata);

            context.UpdateScreen(s => s.identity = new UIScreenIdentity { screenId = "after-screen" }, "Change screen id");
            context.UpdateElement(metadata.elements[0], e => e.text = "After", "Change title");

            Assert.That(context.DiscardUnsavedChanges(), Is.True);
            Assert.That(screen.ScreenId, Is.EqualTo("before-screen"));
            Assert.That(metadata.elements.Single().text, Is.EqualTo("Before"));
            Assert.That(context.HasUnsavedChanges, Is.False);
            context.Dispose();
            Object.DestroyImmediate(metadata);
            Object.DestroyImmediate(screen);
        }

        [Test]
        public void CompanionJson_LegacyImport_PreservesFieldsThatLegacyFormatDidNotContain()
        {
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            metadata.screenId = "legacy";
            metadata.elements.Add(new DesignerElementMetadata
            {
                elementId = "title",
                text = "From JSON",
                shape = DesignerElementShape.Circle,
                previewItemCount = 5
            });
            metadata.variants.Add(new DesignerVariantMetadata { variantId = "saved" });
            AssetDatabase.CreateAsset(metadata, TempFolder + "/Legacy.Metadata.asset");
            AssetDatabase.SaveAssets();
            var path = DesignerMetadataJsonSerializer.Export(metadata);
            var json = System.IO.File.ReadAllText(path).Replace("\"formatVersion\": 3", "\"formatVersion\": 0");
            System.IO.File.WriteAllText(path, json);

            metadata.elements[0].text = "Local";
            metadata.elements[0].shape = DesignerElementShape.Pill;
            metadata.elements[0].previewItemCount = 9;
            metadata.variants[0].variantId = "local";

            Assert.That(DesignerMetadataJsonSerializer.Import(metadata), Is.True);
            Assert.That(metadata.elements.Single().text, Is.EqualTo("From JSON"));
            Assert.That(metadata.elements.Single().shape, Is.EqualTo(DesignerElementShape.Pill));
            Assert.That(metadata.elements.Single().previewItemCount, Is.EqualTo(9));
            Assert.That(metadata.variants.Single().variantId, Is.EqualTo("local"));
        }

        private static System.Type BindingTagType
            => System.Type.GetType("emiteat.NexUI.Integrations.UGUI.NxUGuiBindingTag, emiteat.NexUI.Integrations.UGUI", true);

        private static Component AddBindingTag(GameObject gameObject, string stableId, string elementId, string ownership)
        {
            var tag = gameObject.AddComponent(BindingTagType);
            BindingTagType.GetField("stableId").SetValue(tag, stableId);
            BindingTagType.GetField("elementId").SetValue(tag, elementId);
            var ownershipField = BindingTagType.GetField("ownership");
            ownershipField.SetValue(tag, System.Enum.Parse(ownershipField.FieldType, ownership));
            return tag;
        }

        private static Component[] GetBindingTags(GameObject root)
            => root.GetComponentsInChildren(BindingTagType, true).Cast<Component>().ToArray();

        private static string GetTagField(Component tag, string field)
            => (string)BindingTagType.GetField(field).GetValue(tag);
    }
}
