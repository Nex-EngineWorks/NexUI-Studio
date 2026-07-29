using System;
using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor;
using emiteat.NexUI.Designer.Editor.Backend;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Designer.Editor.Components.Definitions;
using emiteat.NexUI.Designer.Editor.Inspectors;
using emiteat.NexUI.Designer.Editor.Localization;
using emiteat.NexUI.Designer.Editor.UI.Panels;
using emiteat.NexUI.Designer.Editor.Viewport;
using emiteat.NexUI.MotionClip;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// Registry-completeness tests (spec §34): every runtime component type has a valid descriptor,
    /// ids are unique, defaults are sane, and unknown types resolve to a safe Generic descriptor.
    /// </summary>
    public sealed class DesignerComponentRegistryTests
    {
        [AddComponentMenu("NexUI Tests/Annotated Behaviour")]
        [RequireComponent(typeof(CanvasGroup))]
        [DisallowMultipleComponent]
        [System.ComponentModel.Description("Keeps a test UI element synchronized with gameplay state.")]
        public sealed class AnnotatedAttachedBehaviour : MonoBehaviour { }

        [Test]
        public void EveryElementTypeHasADescriptor()
        {
            foreach (DesignerElementType type in Enum.GetValues(typeof(DesignerElementType)))
            {
                var d = DesignerComponentRegistry.Get(type);
                Assert.IsNotNull(d, $"No descriptor for {type}");
                Assert.AreEqual(type.ToString(), d.TypeId, $"Descriptor TypeId mismatch for {type}");
                Assert.IsFalse(string.IsNullOrEmpty(d.DisplayName), $"{type} has no DisplayName");
                Assert.IsFalse(string.IsNullOrEmpty(d.LocalizationKey), $"{type} has no LocalizationKey");
            }
        }

        [Test]
        public void NoDuplicateTypeIds()
        {
            var seen = new HashSet<string>();
            foreach (var d in DesignerComponentRegistry.All)
                Assert.IsTrue(seen.Add(d.TypeId), $"Duplicate descriptor TypeId '{d.TypeId}'");
        }

        [Test]
        public void DefaultSizesAreValidAndAtLeastMinimum()
        {
            foreach (var d in DesignerComponentRegistry.All)
            {
                Assert.Greater(d.DefaultSize.x, 0f, $"{d.TypeId} default width");
                Assert.Greater(d.DefaultSize.y, 0f, $"{d.TypeId} default height");
                Assert.GreaterOrEqual(d.DefaultSize.x, d.MinimumSize.x, $"{d.TypeId} width < min");
                Assert.GreaterOrEqual(d.DefaultSize.y, d.MinimumSize.y, $"{d.TypeId} height < min");
            }
        }

        [Test]
        public void ContainersDeclareAtLeastOneSlot()
        {
            foreach (var d in DesignerComponentRegistry.All)
                if (d.CanHaveChildren)
                    Assert.Greater(d.Slots.Count, 0, $"{d.TypeId} can have children but declares no slots");
        }

        [Test]
        public void UnknownTypeResolvesToGenericKeepingItsId()
        {
            var d = DesignerComponentRegistry.Get("MyCustomWidget");
            Assert.IsTrue(d.IsGeneric);
            Assert.AreEqual("MyCustomWidget", d.TypeId);
            Assert.IsTrue(d.CanHaveChildren, "Generic must be permissive so custom screens aren't blocked");
        }

        [Test]
        public void LeafTypesCannotHaveChildren()
        {
            Assert.IsFalse(DesignerComponentRegistry.CanHaveChildren("Label"));
            Assert.IsFalse(DesignerComponentRegistry.CanHaveChildren("Image"));
            Assert.IsFalse(DesignerComponentRegistry.CanHaveChildren("ProgressBar"));
            Assert.IsTrue(DesignerComponentRegistry.CanHaveChildren("Panel"));
            Assert.IsTrue(DesignerComponentRegistry.CanHaveChildren("Button"), "Button has icon/content slots");
        }

        [Test]
        public void ContainerFlagMatchesExpectations()
        {
            Assert.IsTrue(DesignerComponentRegistry.IsContainer("Panel"));
            Assert.IsTrue(DesignerComponentRegistry.IsContainer("Modal"));
            Assert.IsFalse(DesignerComponentRegistry.IsContainer("Label"));
            Assert.IsFalse(DesignerComponentRegistry.IsContainer("Button"), "Button holds slot children but is not a layout container");
        }

        [Test]
        public void SupportedBindingsAreDeclaredPerType()
        {
            var button = DesignerComponentRegistry.Get("Button");
            Assert.IsTrue(button.SupportsBinding(DesignerBindingChannel.Command));
            Assert.IsTrue(button.SupportsBinding(DesignerBindingChannel.Text));

            var progress = DesignerComponentRegistry.Get("ProgressBar");
            Assert.IsTrue(progress.SupportsBinding(DesignerBindingChannel.Value));
            Assert.IsFalse(progress.SupportsBinding(DesignerBindingChannel.Command), "ProgressBar is not command-driven");
        }

        [Test]
        public void TemplateSlotsAreMarkedOnCollectionTypes()
        {
            foreach (var typeId in new[] { "List", "Grid", "ChoiceList", "Hotbar" })
            {
                var d = DesignerComponentRegistry.Get(typeId);
                var hasTemplate = false;
                foreach (var s in d.Slots) if (s.IsTemplateSlot) hasTemplate = true;
                Assert.IsTrue(hasTemplate, $"{typeId} should declare a template slot");
            }
        }

        [Test]
        public void ChannelForKeyName_MapsSerializedKeys()
        {
            Assert.AreEqual(DesignerBindingChannel.Command, DesignerComponentDescriptor.ChannelForKeyName("commandKey"));
            Assert.AreEqual(DesignerBindingChannel.Value, DesignerComponentDescriptor.ChannelForKeyName("valueKey"));
            Assert.AreEqual(DesignerBindingChannel.None, DesignerComponentDescriptor.ChannelForKeyName("nope"));
        }

        [Test]
        public void DefaultSlotIdPrefersContent()
        {
            Assert.AreEqual("content", DesignerComponentRegistry.Get("Panel").DefaultSlotId);
            Assert.AreEqual("content", DesignerComponentRegistry.Get("Modal").DefaultSlotId);
        }

        [Test]
        public void StockCatalogsDeclareNativeBackendFactories()
        {
            var uguiCount = 0;
            var toolkitCount = 0;
            foreach (var descriptor in DesignerComponentRegistry.All)
            {
                if (descriptor.Family == DesignerComponentFamily.UGUI)
                {
                    uguiCount++;
                    Assert.IsFalse(string.IsNullOrEmpty(descriptor.UGUIControl), descriptor.TypeId);
                    Assert.AreEqual(DesignerBackendSupport.Full, descriptor.UGUISupport, descriptor.TypeId);
                }
                else if (descriptor.Family == DesignerComponentFamily.UIToolkit)
                {
                    toolkitCount++;
                    Assert.IsFalse(string.IsNullOrEmpty(descriptor.UxmlTag), descriptor.TypeId);
                }
            }
            Assert.GreaterOrEqual(uguiCount, 22, "uGUI catalog should cover the stock GameObject/UI controls and layouts");
            Assert.GreaterOrEqual(toolkitCount, 50, "UI Toolkit catalog should cover Unity 6's creatable runtime controls");
        }

        /// <summary>
        /// The NexUI library is the reason a screen can be assembled instead of drawn. These are the
        /// invariants that keep it usable as it grows: enough breadth to cover a real product, every
        /// entry reachable from the palette, and - because the registry keys by TypeId and a later
        /// catalog silently overwrites an earlier one - no id claimed twice.
        /// </summary>
        [Test]
        public void NexUILibraryIsLargeReachableAndFreeOfIdCollisions()
        {
            var catalogIds = new List<string>();
            foreach (var d in NexUIComponentCatalog.Build()) catalogIds.Add(d.TypeId);
            foreach (var d in NexUILibraryCatalog.Build()) catalogIds.Add(d.TypeId);
            foreach (var d in NexUIGameCatalog.Build()) catalogIds.Add(d.TypeId);

            var seen = new HashSet<string>();
            foreach (var id in catalogIds)
                Assert.IsTrue(seen.Add(id), $"'{id}' is declared by more than one NexUI catalog and would be overwritten");

            var registered = 0;
            foreach (var descriptor in DesignerComponentRegistry.All)
            {
                if (descriptor.Family != DesignerComponentFamily.NexUI) continue;
                registered++;
                if (descriptor.TypeId == "ComponentInstance" || descriptor.TypeId == "Custom") continue;
                Assert.IsFalse(string.IsNullOrEmpty(descriptor.PaletteGroup),
                    $"{descriptor.TypeId} is not reachable from the palette");
                Assert.IsFalse(string.IsNullOrEmpty(descriptor.Description), descriptor.TypeId);
            }

            Assert.GreaterOrEqual(registered, 440,
                "the shipped NexUI component library should stay in the hundreds, not shrink back to a starter set");
            foreach (var id in catalogIds)
                Assert.AreSame(DesignerComponentRegistry.Get(id), DesignerComponentRegistry.Get(id),
                    $"'{id}' does not resolve from the registry");
        }

        /// <summary>
        /// Game UI is the reason most of this package exists, so the shelves it ships with are part
        /// of the contract: HUD, world/map, items, progression, menus and multiplayer must each stay
        /// populated rather than collapsing back into one unusable "Game" folder.
        /// </summary>
        [Test]
        public void GameCatalogCoversEveryGameShelf()
        {
            var perGroup = new Dictionary<string, int>();
            foreach (var descriptor in NexUIGameCatalog.Build())
            {
                Assert.IsFalse(string.IsNullOrEmpty(descriptor.PaletteGroup), descriptor.TypeId);
                perGroup.TryGetValue(descriptor.PaletteGroup, out var count);
                perGroup[descriptor.PaletteGroup] = count + 1;
            }

            foreach (var group in new[]
                     {
                         DesignerPaletteGroup.Game, DesignerPaletteGroup.GameWorld,
                         DesignerPaletteGroup.GameItems, DesignerPaletteGroup.GameProgression,
                         DesignerPaletteGroup.GameMenu, DesignerPaletteGroup.GameMultiplayer
                     })
            {
                Assert.IsTrue(perGroup.TryGetValue(group, out var count), $"{group} has no components");
                Assert.GreaterOrEqual(count, 15, group);
            }
        }

        /// <summary>
        /// Every palette component must be named in both shipped languages. The palette falls back to
        /// the English DisplayName when a key is missing, so an untranslated component does not fail
        /// loudly - it just quietly leaves English entries in a Korean palette. This is the check that
        /// makes that visible when a catalog grows.
        /// </summary>
        [Test]
        public void EveryPaletteComponentIsNamedInBothLanguages()
        {
            const string root = "Packages/com.emiteat.nexui.designer/Localization/";
            var korean = System.IO.File.ReadAllText(root + "ko-KR.json");
            var english = System.IO.File.ReadAllText(root + "en-US.json");

            var untranslated = new List<string>();
            foreach (var descriptor in DesignerComponentRegistry.All)
            {
                if (string.IsNullOrEmpty(descriptor.PaletteGroup)) continue; // not palette-creatable
                var token = "\"" + descriptor.LocalizationKey + "\":";
                if (!korean.Contains(token) || !english.Contains(token))
                    untranslated.Add(descriptor.TypeId + " (" + descriptor.LocalizationKey + ")");
            }

            CollectionAssert.IsEmpty(untranslated,
                "these components have no localized name: " + string.Join(", ", untranslated));
        }

        /// <summary>Every palette folder must have a real title in both shipped languages, or the
        /// palette shows raw keys like "palette.group.charts" to the user.</summary>
        [Test]
        public void EveryPaletteGroupHasATranslatedTitle()
        {
            foreach (var group in DesignerComponentPalette.BuildGroups())
            {
                var title = DesignerLocalization.T(group.GroupId);
                Assert.AreNotEqual(group.GroupId, title, $"{group.GroupId} has no translation");
                Assert.IsFalse(string.IsNullOrWhiteSpace(title), group.GroupId);
            }
        }

        [Test]
        public void PaletteContainsAllThreeFamilies()
        {
            var families = new HashSet<DesignerComponentFamily>();
            foreach (var group in DesignerComponentPalette.BuildGroups())
            {
                families.Add(group.Family);
                Assert.Greater(group.Items.Count, 0, group.GroupId);
            }
            CollectionAssert.AreEquivalent(new[]
            {
                DesignerComponentFamily.NexUI,
                DesignerComponentFamily.UGUI,
                DesignerComponentFamily.UIToolkit
            }, families);
        }

        [Test]
        public void BuiltInRecipeCatalogContainsThreeHundredStableCompositeDefinitions()
        {
            var recipes = DesignerBuiltInComponentCatalog.All;
            Assert.AreEqual(DesignerBuiltInComponentCatalog.ExpectedRecipeCount, recipes.Count);
            Assert.AreEqual(300, recipes.Count, "the package contract is 25 archetypes across 12 themes");

            var ids = new HashSet<string>();
            var archetypeFolders = new HashSet<string>();
            foreach (var recipe in recipes)
            {
                Assert.IsTrue(ids.Add(recipe.Id), recipe.Id);
                archetypeFolders.Add(recipe.CategoryPath);
                Assert.Greater(recipe.Definition.elements.Count, 1, recipe.Id);
                Assert.Greater(recipe.Definition.exposedProperties.Count, 0, recipe.Id);
                Assert.AreEqual(1, recipe.Definition.slots.Count, recipe.Id);
                Assert.AreEqual(1, recipe.Definition.variantProperties.Count, recipe.Id);
                Assert.AreEqual(1, recipe.Definition.variantRules.Count, recipe.Id);

                var syntheticGuid = DesignerComponentLibrary.GuidOf(recipe.Definition);
                StringAssert.StartsWith(DesignerBuiltInComponentCatalog.GuidPrefix, syntheticGuid, recipe.Id);
                Assert.AreSame(recipe.Definition,
                    DesignerComponentLibrary.Resolve(syntheticGuid, recipe.Id), recipe.Id);
            }
            Assert.AreEqual(25, archetypeFolders.Count);
        }

        [Test]
        public void BuiltInRecipeInstantiatesAndExpandsWithoutProjectAssets()
        {
            var screen = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            try
            {
                var recipe = DesignerBuiltInComponentCatalog.All[0];
                var result = DesignerComponentService.Instantiate(screen, recipe.Definition, new Vector2(32f, 48f));
                Assert.IsTrue(result.Success, result.Message);
                StringAssert.StartsWith(DesignerBuiltInComponentCatalog.GuidPrefix,
                    result.Element.componentInstance.definitionGuid);

                var expansion = DesignerComponentExpander.Expand(screen, DesignerComponentLibrary.Resolver);
                try
                {
                    Assert.IsEmpty(expansion.Issues);
                    Assert.AreEqual(recipe.Definition.elements.Count, expansion.Expanded.elements.Count);
                    Assert.AreEqual(new Vector2(32f, 48f),
                        expansion.Expanded.Find(result.Element.elementId).rect.position);
                }
                finally
                {
                    expansion.Dispose();
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(screen);
            }
        }

        [Test]
        public void BuiltInExpandedChildrenPassPointerInputToAuthoredInstanceRoot()
        {
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            var context = new NexUIDesignerContext();
            try
            {
                var recipe = DesignerBuiltInComponentCatalog.All[0];
                var result = DesignerComponentService.Instantiate(metadata, recipe.Definition, new Vector2(48f, 64f));
                Assert.IsTrue(result.Success, result.Message);
                context.SetMetadata(metadata);

                var viewport = new NexUIDesignerViewport(context);
                var views = viewport.Query<VisualElement>(className: "nexui-design-element").ToList();
                Assert.AreEqual(recipe.Definition.elements.Count, views.Count);
                foreach (var previewElement in context.PreviewElements)
                    Assert.AreSame(result.Element, context.ResolveAuthoredOwner(previewElement),
                        previewElement.elementId);

                var editableViews = views.FindAll(view => view.pickingMode == PickingMode.Position);
                Assert.AreEqual(1, editableViews.Count,
                    "only the authored component instance root may accept move/resize input");
                Assert.That(editableViews[0].style.left.value.value, Is.EqualTo(48f).Within(0.01f));
                Assert.That(editableViews[0].style.top.value.value, Is.EqualTo(64f).Within(0.01f));

                foreach (var generated in views)
                {
                    if (ReferenceEquals(generated, editableViews[0])) continue;
                    Assert.AreEqual(PickingMode.Ignore, generated.pickingMode);
                    foreach (var descendant in generated.Query<VisualElement>().ToList())
                        Assert.AreEqual(PickingMode.Ignore, descendant.pickingMode);
                }
            }
            finally
            {
                context.Dispose();
                UnityEngine.Object.DestroyImmediate(metadata);
            }
        }

        [Test]
        public void BuiltInRecipePaletteUsesFoldersLazyCardsAndCompositePreviews()
        {
            const string familyFilterKey = "NexUI.Designer.Components.FamilyFilter";
            var hadFamilyFilter = EditorPrefs.HasKey(familyFilterKey);
            var previousFamilyFilter = EditorPrefs.GetInt(familyFilterKey, 0);
            EditorPrefs.SetInt(familyFilterKey, 5);
            var folderPreferences = new Dictionary<string, Tuple<bool, bool>>();
            foreach (var recipe in DesignerBuiltInComponentCatalog.All)
            {
                var slash = recipe.CategoryPath.IndexOf('/');
                var category = slash > 0 ? recipe.CategoryPath.Substring(0, slash) : recipe.CategoryPath;
                foreach (var path in new[] { category, recipe.CategoryPath })
                {
                    var key = "NexUI.Designer.Components.BuiltInFolder." + path;
                    if (folderPreferences.ContainsKey(key)) continue;
                    folderPreferences[key] = Tuple.Create(EditorPrefs.HasKey(key), EditorPrefs.GetBool(key, false));
                    EditorPrefs.DeleteKey(key);
                }
            }
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            var context = new NexUIDesignerContext();
            context.SetMetadata(metadata);
            try
            {
                var panel = new NexUIComponentsPanel(context);
                Assert.AreEqual(1, EditorPrefs.GetInt(familyFilterKey),
                    "the retired standalone Built-In filter must migrate to NexUI");
                var nexuiFamily = panel.Q<Foldout>(className: "family-nexui");
                var builtInLibrary = panel.Q<Foldout>(className: "nexui-component-builtin-library");
                Assert.IsNotNull(nexuiFamily);
                Assert.IsNotNull(builtInLibrary);
                Assert.IsTrue(nexuiFamily.Contains(builtInLibrary),
                    "package recipes belong inside the NexUI library, not beside it");

                var folders = panel.Query<Foldout>(className: "nexui-component-builtin-folder").ToList();
                Assert.AreEqual(31, folders.Count, "six categories and twenty-five archetype folders");
                Assert.AreEqual(0, panel.Query<Button>(className: "nexui-component-builtin-card").ToList().Count,
                    "collapsed archetypes must not allocate all 300 cards");

                var leaf = folders.Find(folder => folder.text.EndsWith("(12)"));
                Assert.IsNotNull(leaf);
                leaf.value = true;
                var cards = panel.Query<Button>(className: "nexui-component-builtin-card").ToList();
                Assert.AreEqual(12, cards.Count);
                Assert.IsNotNull(cards[0].Q<VisualElement>(className: "nexui-component-composite-preview"));
                Assert.Greater(cards[0].Query<VisualElement>(className: "nexui-component-composite-part").ToList().Count, 1);
                Assert.IsFalse(string.IsNullOrWhiteSpace(cards[0].tooltip));
            }
            finally
            {
                context.Dispose();
                UnityEngine.Object.DestroyImmediate(metadata);
                if (hadFamilyFilter) EditorPrefs.SetInt(familyFilterKey, previousFamilyFilter);
                else EditorPrefs.DeleteKey(familyFilterKey);
                foreach (var preference in folderPreferences)
                {
                    if (preference.Value.Item1) EditorPrefs.SetBool(preference.Key, preference.Value.Item2);
                    else EditorPrefs.DeleteKey(preference.Key);
                }
            }
        }

        [Test]
        public void ComponentLibraryCardsExposePreviewAndDetailedTooltip()
        {
            const string familyFilterKey = "NexUI.Designer.Components.FamilyFilter";
            var hadFamilyFilter = EditorPrefs.HasKey(familyFilterKey);
            var previousFamilyFilter = EditorPrefs.GetInt(familyFilterKey, 0);
            EditorPrefs.SetInt(familyFilterKey, 0);
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            var context = new NexUIDesignerContext();
            context.SetMetadata(metadata);
            try
            {
                var panel = new NexUIComponentsPanel(context);
                var cards = panel.Query<Button>(className: "nexui-component-card").ToList();
                Assert.GreaterOrEqual(cards.Count, 130, "the library should visualize the full atomic catalog");
                foreach (var card in cards)
                {
                    Assert.IsNotNull(card.Q<VisualElement>(className: "nexui-component-card-preview"), card.userData as string);
                    if (!card.ClassListContains("nexui-component-custom-card") &&
                        !card.ClassListContains("nexui-component-builtin-card"))
                    {
                        StringAssert.Contains("uGUI", card.tooltip, card.userData as string);
                        StringAssert.Contains("UI Toolkit", card.tooltip, card.userData as string);
                    }
                }
                Assert.IsNotNull(panel.Q<VisualElement>(className: "nexui-component-details"));
                Assert.IsNotNull(panel.Q<VisualElement>(className: "nexui-component-detail-preview"));

                var familyFolders = panel.Query<Foldout>(className: "nexui-component-family-folder").ToList();
                var categoryFolders = panel.Query<Foldout>(className: "nexui-component-category-folder").ToList();
                Assert.AreEqual(4, familyFolders.Count,
                    "NexUI (including Built-In recipes), uGUI, UI Toolkit and Custom need top-level folders");
                var nexuiFamily = familyFolders.Find(folder => folder.ClassListContains("family-nexui"));
                Assert.IsNotNull(nexuiFamily);
                Assert.IsTrue(nexuiFamily.Contains(
                    panel.Q<Foldout>(className: "nexui-component-builtin-library")));
                Assert.GreaterOrEqual(categoryFolders.Count, DesignerPaletteGroup.Order.Length,
                    "the existing property/category folders must remain nested under a library folder");
                foreach (var familyFolder in familyFolders)
                    Assert.IsTrue(categoryFolders.Exists(familyFolder.Contains), familyFolder.text);
            }
            finally
            {
                context.Dispose();
                UnityEngine.Object.DestroyImmediate(metadata);
                if (hadFamilyFilter) EditorPrefs.SetInt(familyFilterKey, previousFamilyFilter);
                else EditorPrefs.DeleteKey(familyFilterKey);
            }
        }

        [TestCase(" Gameplay \\ HUD / Combat ", "Gameplay/HUD/Combat")]
        [TestCase("Gameplay//HUD", "Gameplay/HUD")]
        [TestCase("../", "Custom")]
        [TestCase("", "Custom")]
        public void CustomComponentFolderPathsAreSafeAndSupportNesting(string input, string expected)
        {
            Assert.AreEqual(expected, DesignerComponentLibrary.NormalizeFolder(input));
        }

        [Test]
        public void AttachedComponentInspectorVisualizesTypeDescriptionAndRuntimeMetadata()
        {
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            var element = new DesignerElementMetadata
            {
                elementId = "health", displayName = "Health Bar", elementType = "ProgressBar",
                attachedComponents = new List<DesignerAttachedComponentMetadata>
                {
                    new DesignerAttachedComponentMetadata
                    {
                        typeName = typeof(AnnotatedAttachedBehaviour).FullName + ", " +
                                   typeof(AnnotatedAttachedBehaviour).Assembly.GetName().Name
                    }
                }
            };
            metadata.elements.Add(element);
            var context = new NexUIDesignerContext();
            context.SetMetadata(metadata);
            context.Select(element);
            try
            {
                var inspector = new AttachedComponentsInspector(context);
                Assert.IsNotNull(inspector.Q<VisualElement>(className: "nexui-attached-overview"));
                var card = inspector.Q<VisualElement>(className: "nexui-attached-card");
                Assert.IsNotNull(card);
                StringAssert.Contains("Annotated Behaviour", card.Q<Label>(className: "nexui-attached-card-title").text);
                StringAssert.Contains("gameplay state", card.Q<Label>(className: "nexui-attached-card-description").text);
                StringAssert.Contains("Canvas Group", card.Q<Label>(className: "nexui-attached-card-requires").text);
                StringAssert.Contains("uGUI", card.tooltip);
                Assert.IsNotNull(inspector.Q<Button>(className: "nexui-attached-add"));
            }
            finally
            {
                context.Dispose();
                UnityEngine.Object.DestroyImmediate(metadata);
            }
        }

        [Test]
        public void ComponentLibraryShowsProjectCustomComponentsInsideNestedFolders()
        {
            const string path = "Assets/__NexUIDesignerCustomPaletteTest.asset";
            const string familyFilterKey = "NexUI.Designer.Components.FamilyFilter";
            var hadFamilyFilter = EditorPrefs.HasKey(familyFilterKey);
            var previousFamilyFilter = EditorPrefs.GetInt(familyFilterKey, 0);
            EditorPrefs.SetInt(familyFilterKey, 0);
            AssetDatabase.DeleteAsset(path);
            var definition = ScriptableObject.CreateInstance<DesignerComponentDefinitionAsset>();
            definition.displayName = "Inventory Slot";
            definition.category = "Gameplay/HUD";
            definition.rootElementId = "slot";
            definition.elements.Add(new DesignerElementMetadata
            {
                elementId = "slot", elementType = "Panel", rect = new Rect(0, 0, 96, 96), text = "Slot"
            });
            AssetDatabase.CreateAsset(definition, path);
            AssetDatabase.SaveAssets();
            DesignerComponentLibrary.Invalidate();

            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            var context = new NexUIDesignerContext();
            context.SetMetadata(metadata);
            try
            {
                var panel = new NexUIComponentsPanel(context);
                var customCards = panel.Query<Button>(className: "nexui-component-custom-card").ToList();
                Assert.IsTrue(customCards.Exists(card => (card.userData as string)?.Contains("Inventory Slot") == true));

                var folders = panel.Query<Foldout>(className: "nexui-component-custom-folder").ToList();
                Assert.IsTrue(folders.Exists(folder => folder.text.StartsWith("Gameplay")));
                Assert.IsTrue(folders.Exists(folder => folder.text.StartsWith("HUD")));
            }
            finally
            {
                context.Dispose();
                UnityEngine.Object.DestroyImmediate(metadata);
                AssetDatabase.DeleteAsset(path);
                DesignerComponentLibrary.Invalidate();
                if (hadFamilyFilter) EditorPrefs.SetInt(familyFilterKey, previousFamilyFilter);
                else EditorPrefs.DeleteKey(familyFilterKey);
            }
        }

        [Test]
        public void LocalizationLoadsCommaFirstUtf8SectionsAndKeepsLanguageParity()
        {
            var previous = DesignerLocalization.CurrentLanguage;
            try
            {
                DesignerLocalization.SetLanguage(DesignerLanguage.Korean);
                Assert.AreEqual("라이브러리", DesignerLocalization.T("palette.tooltip.family"));
                Assert.AreEqual("새 스크린", DesignerLocalization.T("productivity.newScreen"));
                Assert.AreEqual("재생", DesignerLocalization.T("motionClip.toolbar.play"));
                Assert.AreEqual("실시간 모션 미리보기", DesignerLocalization.T("motionInspector.previewTitle"));

                DesignerLocalization.SetLanguage(DesignerLanguage.English);
                Assert.AreEqual("Library", DesignerLocalization.T("palette.tooltip.family"));
                Assert.AreEqual("New Screen", DesignerLocalization.T("productivity.newScreen"));
                Assert.AreEqual("Play", DesignerLocalization.T("motionClip.toolbar.play"));
                Assert.AreEqual("Live Motion Preview", DesignerLocalization.T("motionInspector.previewTitle"));
            }
            finally
            {
                DesignerLocalization.SetLanguage(previous);
            }
        }

        [Test]
        public void MotionInspectorShowsLivePathPreviewAndDocumentedExamples()
        {
            const string motionSectionPreferenceKey = "NexUI.Designer.MotionInspector.ActiveSection";
            var hadMotionSectionPreference = EditorPrefs.HasKey(motionSectionPreferenceKey);
            var previousMotionSection = EditorPrefs.GetInt(motionSectionPreferenceKey, 0);
            EditorPrefs.DeleteKey(motionSectionPreferenceKey);
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            var element = new DesignerElementMetadata
            {
                stableId = "motion-inspector-stable", elementId = "dialog", displayName = "Dialog",
                elementType = "Panel", rect = new Rect(0, 0, 320, 180)
            };
            metadata.elements.Add(element);
            var context = new NexUIDesignerContext();
            context.SetMetadata(metadata);
            context.Select(element);
            try
            {
                var inspector = new MotionInspector(context);
                Assert.IsNotNull(inspector.Q<VisualElement>(className: "nexui-motion-overview"));
                Assert.IsNotNull(inspector.Q<VisualElement>(className: "nexui-motion-live-preview"));
                Assert.IsNotNull(inspector.Q<VisualElement>(className: "nexui-motion-example-ghost"));
                Assert.IsNotNull(inspector.Q<VisualElement>(className: "nexui-motion-example-path"));
                Assert.IsNotNull(inspector.Q<VisualElement>(className: "nexui-motion-example-target"));
                Assert.IsNotNull(inspector.Q<VisualElement>(className: "nexui-motion-example-object"));

                var motionTabs = inspector.Query<Button>(className: "nexui-motion-section-tab").ToList();
                Assert.AreEqual(3, motionTabs.Count);
                Assert.AreEqual(1, motionTabs.FindAll(tab => tab.ClassListContains("is-selected")).Count);
                var motionPages = inspector.Query<VisualElement>(className: "nexui-motion-section-page").ToList();
                Assert.AreEqual(3, motionPages.Count);
                Assert.AreEqual(2, motionPages.FindAll(page => page.style.display.value == DisplayStyle.None).Count);

                var description = inspector.Q<Label>(className: "nexui-motion-preview-description");
                Assert.IsFalse(string.IsNullOrWhiteSpace(description?.text));
                var examples = inspector.Query<Button>(className: "nexui-motion-example-card").ToList();
                Assert.AreEqual(4, examples.Count);
                Assert.IsTrue(examples.Exists(card => card.ClassListContains("is-selected")));
                foreach (var example in examples)
                    Assert.IsFalse(string.IsNullOrWhiteSpace(example.tooltip));
                Assert.IsFalse(string.IsNullOrWhiteSpace(
                    inspector.Q<Button>(className: "nexui-motion-use-example")?.tooltip));
            }
            finally
            {
                context.Dispose();
                UnityEngine.Object.DestroyImmediate(metadata);
                if (hadMotionSectionPreference) EditorPrefs.SetInt(motionSectionPreferenceKey, previousMotionSection);
                else EditorPrefs.DeleteKey(motionSectionPreferenceKey);
            }
        }

        [Test]
        public void MotionPreviewMovesVisibleElementAndKeepsStartGhost()
        {
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            metadata.elements.Add(new DesignerElementMetadata
            {
                stableId = "motion-stable", elementId = "moving", elementType = "Button",
                rect = new Rect(10, 20, 100, 40), text = "Move"
            });
            var clip = ScriptableObject.CreateInstance<UIMotionClip>();
            clip.duration = 1f;
            clip.tracks = new[]
            {
                new UIMotionClipTrack
                {
                    targetElementId = "moving",
                    propertyTracks = new[]
                    {
                        new UIMotionClipPropertyTrack
                        {
                            propertyType = UIMotionClipPropertyType.AnchoredPosition,
                            keyframes = new[]
                            {
                                new UIMotionClipKeyframe(0f, UIMotionClipValue.FromVector2(new Vector2(10, 20))),
                                new UIMotionClipKeyframe(1f, UIMotionClipValue.FromVector2(new Vector2(210, 20)))
                            }
                        }
                    }
                }
            };
            var context = new NexUIDesignerContext();
            context.SetMetadata(metadata);
            try
            {
                var viewport = new NexUIDesignerViewport(context);
                context.SetActiveMotionClip(clip, 0.5f);

                var views = viewport.Query<VisualElement>(className: "nexui-design-element").ToList();
                var actual = views.Find(view => !view.ClassListContains("nexui-motion-start-ghost"));
                var ghost = views.Find(view => view.ClassListContains("nexui-motion-start-ghost"));
                Assert.IsNotNull(actual);
                Assert.IsNotNull(ghost);
                Assert.That(actual.style.left.value.value, Is.EqualTo(110f).Within(0.01f));
                Assert.That(ghost.style.left.value.value, Is.EqualTo(10f).Within(0.01f));
                Assert.That(ghost.style.opacity.value, Is.EqualTo(0.28f).Within(0.01f));
            }
            finally
            {
                context.Dispose();
                UnityEngine.Object.DestroyImmediate(clip);
                UnityEngine.Object.DestroyImmediate(metadata);
            }
        }

        [Test]
        public void RightPointerUpOverElementBubblesForCanvasContextMenu()
        {
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            metadata.elements.Add(new DesignerElementMetadata
            {
                stableId = "context-stable", elementId = "context-target", elementType = "Button",
                rect = new Rect(10, 20, 100, 40), text = "Context"
            });
            var context = new NexUIDesignerContext();
            context.SetMetadata(metadata);
            try
            {
                var viewport = new NexUIDesignerViewport(context);
                var elementView = viewport.Q<VisualElement>(className: "nexui-design-element");
                Assert.IsNotNull(elementView);

                var bubbled = false;
                viewport.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (evt.button == 1) bubbled = true;
                });

                var systemEvent = new Event
                {
                    type = EventType.MouseUp,
                    button = 1,
                    mousePosition = new Vector2(20f, 30f)
                };
                var pointerUp = PointerUpEvent.GetPooled(systemEvent);
                elementView.SendEvent(pointerUp);
                pointerUp.Dispose();

                Assert.IsTrue(bubbled,
                    "right PointerUp must reach the canvas so UI Toolkit can produce ContextClickEvent");
            }
            finally
            {
                context.Dispose();
                UnityEngine.Object.DestroyImmediate(metadata);
            }
        }
    }
}
