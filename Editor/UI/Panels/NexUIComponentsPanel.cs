using System;
using System.Collections.Generic;
using System.Text;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Designer.Editor.Components.Definitions;
using emiteat.NexUI.Designer.Editor.Components.Preview;
using emiteat.NexUI.Designer.Editor.Localization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.UI.Panels
{
    public sealed class NexUIComponentsPanel : VisualElement
    {
        private const string FamilyFilterPrefKey = "NexUI.Designer.Components.FamilyFilter";

        private readonly NexUIDesignerContext _context;
        private readonly VisualElement _content;
        private readonly VisualElement _details;
        private readonly Label _librarySummary;
        private readonly List<Button> _cards = new();
        private readonly List<Foldout> _foldouts = new();
        private readonly List<Foldout> _familyFoldouts = new();
        private readonly List<Action> _lazyBuiltInCardBuilders = new();
        private string _filter = "";
        private FamilyFilter _familyFilter;

        private static readonly DesignerComponentFamily[] FamilyOrder =
        {
            DesignerComponentFamily.NexUI,
            DesignerComponentFamily.UGUI,
            DesignerComponentFamily.UIToolkit
        };

        /// <summary>
        /// Which component libraries the palette lists. The default shows everything; a project that
        /// only ships one backend can narrow the list without losing the other family's entries from
        /// screens that already use them.
        /// </summary>
        private enum FamilyFilter
        {
            All,
            NexUI,
            UGUI,
            UIToolkit,
            Custom,
            // Legacy persisted value from the brief standalone Built-In library. It is migrated
            // to NexUI in the constructor and is intentionally absent from the popup choices.
            BuiltIn
        }

        public NexUIComponentsPanel(NexUIDesignerContext context)
        {
            _context = context;
            AddToClassList("nexui-components-panel");

            _familyFilter = (FamilyFilter)EditorPrefs.GetInt(FamilyFilterPrefKey, (int)FamilyFilter.All);
            if (!Enum.IsDefined(typeof(FamilyFilter), _familyFilter)) _familyFilter = FamilyFilter.All;
            if (_familyFilter == FamilyFilter.BuiltIn)
            {
                _familyFilter = FamilyFilter.NexUI;
                EditorPrefs.SetInt(FamilyFilterPrefKey, (int)_familyFilter);
            }

            var header = new VisualElement();
            header.AddToClassList("nexui-component-library-header");
            var heading = new Label(DesignerLocalization.T("componentLibrary.title"));
            heading.AddToClassList("nexui-component-library-title");
            header.Add(heading);
            _librarySummary = new Label();
            _librarySummary.AddToClassList("nexui-component-library-summary");
            header.Add(_librarySummary);
            Add(header);

            var search = new ToolbarSearchField { tooltip = DesignerLocalization.T("tooltip.palette.search") };
            search.AddToClassList("nexui-component-library-search");
            search.RegisterValueChangedCallback(evt =>
            {
                _filter = evt.newValue ?? "";
                RefreshFilter();
            });
            Add(search);

            var actions = new VisualElement();
            actions.AddToClassList("nexui-component-library-actions");
            actions.Add(BuildFamilyFilter());
            actions.Add(ActionButton("componentLibrary.newFolderShort", "componentLibrary.newFolder", CreateFolder));
            actions.Add(ActionButton("componentLibrary.createShort", "componentLibrary.createFromSelection", CreateCustomFromSelection));
            actions.Add(ActionButton("componentLibrary.manageShort", "componentLibrary.manage", DesignerComponentLibraryWindow.Open));
            Add(actions);

            _details = new VisualElement();
            _details.AddToClassList("nexui-component-details");
            Add(_details);

            _content = new ScrollView();
            _content.AddToClassList("nexui-sidebar-scroll");
            Add(_content);

            Rebuild();
            ShowDetails(DesignerComponentRegistry.Get("Button"));

            RegisterCallback<AttachToPanelEvent>(_ => DesignerComponentLibrary.Changed += OnCustomLibraryChanged);
            RegisterCallback<DetachFromPanelEvent>(_ => DesignerComponentLibrary.Changed -= OnCustomLibraryChanged);
        }

        private static Button ActionButton(string textKey, string tooltipKey, Action action)
        {
            var button = new Button(action)
            {
                text = DesignerLocalization.T(textKey),
                tooltip = DesignerLocalization.T(tooltipKey)
            };
            button.AddToClassList("nexui-component-library-action");
            return button;
        }

        private void OnCustomLibraryChanged() => Rebuild();

        private VisualElement BuildFamilyFilter()
        {
            var choices = new List<FamilyFilter>
            {
                FamilyFilter.All, FamilyFilter.NexUI, FamilyFilter.UGUI, FamilyFilter.UIToolkit,
                FamilyFilter.Custom
            };
            var field = new PopupField<FamilyFilter>(DesignerLocalization.T("palette.family"), choices, _familyFilter, FamilyLabel, FamilyLabel)
            {
                tooltip = DesignerLocalization.T("tooltip.palette.family")
            };
            field.AddToClassList("nexui-palette-family");
            field.RegisterValueChangedCallback(evt =>
            {
                _familyFilter = evt.newValue;
                EditorPrefs.SetInt(FamilyFilterPrefKey, (int)_familyFilter);
                Rebuild();
            });
            return field;
        }

        private static string FamilyLabel(FamilyFilter value) => value switch
        {
            FamilyFilter.NexUI => DesignerLocalization.T("palette.family.nexui"),
            FamilyFilter.UGUI => DesignerLocalization.T("palette.family.ugui"),
            FamilyFilter.UIToolkit => DesignerLocalization.T("palette.family.uitk"),
            FamilyFilter.Custom => DesignerLocalization.T("palette.family.custom"),
            _ => DesignerLocalization.T("palette.family.all")
        };

        private void Rebuild()
        {
            _content.Clear();
            _cards.Clear();
            _foldouts.Clear();
            _familyFoldouts.Clear();
            _lazyBuiltInCardBuilders.Clear();

            var groups = DesignerComponentPalette.BuildGroups();
            var recipeCount = DesignerBuiltInComponentCatalog.All.Count;
            foreach (var family in FamilyOrder)
            {
                if (!ShowsFamily(family)) continue;

                var familyCount = CountFamilyItems(groups, family) +
                                  (family == DesignerComponentFamily.NexUI ? recipeCount : 0);
                var familyFolder = BuildFamilyFolder(family, familyCount);

                // The historical "Recent" shortcuts are NexUI entries, so they live beside the
                // NexUI categories instead of leaking into either stock Unity library.
                if (family == DesignerComponentFamily.NexUI)
                {
                    BuildRecent(familyFolder);
                    BuildBuiltInLibrary(familyFolder);
                }

                foreach (var group in groups)
                    if (group.Family == family)
                        BuildGroup(familyFolder, group);

                _content.Add(familyFolder);
            }

            var customCount = DesignerComponentLibrary.All.Count;
            if (_familyFilter == FamilyFilter.All || _familyFilter == FamilyFilter.Custom)
                BuildCustomLibrary();

            var builtInCount = 0;
            foreach (var group in groups) builtInCount += group.Items.Count;
            _librarySummary.text = string.Format(DesignerLocalization.T("componentLibrary.summary"),
                builtInCount, recipeCount, customCount);

            RefreshFilter();
        }

        private Foldout BuildFamilyFolder(DesignerComponentFamily family, int itemCount)
        {
            var prefKey = "NexUI.Designer.Components.Family." + family;
            var defaultOpen = _familyFilter != FamilyFilter.All || family == DesignerComponentFamily.NexUI;
            var foldout = new Foldout
            {
                text = $"{FamilyLabel(family)} ({itemCount})",
                value = EditorPrefs.GetBool(prefKey, defaultOpen)
            };
            foldout.AddToClassList("nexui-component-family-folder");
            foldout.AddToClassList(FamilyClass(family));
            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.target == foldout) EditorPrefs.SetBool(prefKey, evt.newValue);
            });
            _familyFoldouts.Add(foldout);
            return foldout;
        }

        private static int CountFamilyItems(IEnumerable<DesignerPaletteGroupView> groups, DesignerComponentFamily family)
        {
            var count = 0;
            foreach (var group in groups)
                if (group.Family == family)
                    count += group.Items.Count;
            return count;
        }

        private bool ShowsFamily(DesignerComponentFamily family) => _familyFilter switch
        {
            FamilyFilter.NexUI => family == DesignerComponentFamily.NexUI,
            FamilyFilter.UGUI => family == DesignerComponentFamily.UGUI,
            FamilyFilter.UIToolkit => family == DesignerComponentFamily.UIToolkit,
            FamilyFilter.BuiltIn => family == DesignerComponentFamily.NexUI,
            FamilyFilter.Custom => false,
            _ => true
        };

        private sealed class BuiltInFolderNode
        {
            public string StablePath;
            public string DisplayName;
            public readonly SortedDictionary<string, BuiltInFolderNode> Children =
                new SortedDictionary<string, BuiltInFolderNode>(StringComparer.OrdinalIgnoreCase);
            public readonly List<DesignerBuiltInComponentRecipe> Recipes = new List<DesignerBuiltInComponentRecipe>();
            public int RecursiveCount
            {
                get
                {
                    var count = Recipes.Count;
                    foreach (var child in Children.Values) count += child.RecursiveCount;
                    return count;
                }
            }
        }

        private void BuildBuiltInLibrary(VisualElement nexuiFamilyFolder)
        {
            var recipes = DesignerBuiltInComponentCatalog.All;
            var libraryFolder = new Foldout
            {
                text = $"{DesignerLocalization.T("palette.family.builtin")} ({recipes.Count})",
                value = EditorPrefs.GetBool("NexUI.Designer.Components.NexUI.BuiltIn",
                    EditorPrefs.GetBool("NexUI.Designer.Components.Family.BuiltIn", false))
            };
            libraryFolder.AddToClassList("nexui-sidebar-foldout");
            libraryFolder.AddToClassList("nexui-component-category-folder");
            libraryFolder.AddToClassList("nexui-component-builtin-library");
            libraryFolder.RegisterValueChangedCallback(evt =>
            {
                if (evt.target == libraryFolder)
                    EditorPrefs.SetBool("NexUI.Designer.Components.NexUI.BuiltIn", evt.newValue);
            });

            var root = new BuiltInFolderNode();
            foreach (var recipe in recipes)
            {
                var segments = recipe.CategoryPath.Split('/');
                var current = root;
                var stablePath = string.Empty;
                for (var i = 0; i < segments.Length; i++)
                {
                    stablePath = string.IsNullOrEmpty(stablePath) ? segments[i] : stablePath + "/" + segments[i];
                    if (!current.Children.TryGetValue(segments[i], out var child))
                    {
                        child = new BuiltInFolderNode
                        {
                            StablePath = stablePath,
                            DisplayName = i == 0 ? recipe.CategoryLabel : recipe.ArchetypeLabel
                        };
                        current.Children.Add(segments[i], child);
                    }
                    current = child;
                }
                current.Recipes.Add(recipe);
            }

            foreach (var child in root.Children.Values) BuildBuiltInFolder(libraryFolder, child, 0);
            nexuiFamilyFolder.Add(libraryFolder);
            _foldouts.Add(libraryFolder);
        }

        private void BuildBuiltInFolder(VisualElement parent, BuiltInFolderNode node, int depth)
        {
            var prefKey = "NexUI.Designer.Components.BuiltInFolder." + node.StablePath;
            var foldout = new Foldout
            {
                text = $"{node.DisplayName} ({node.RecursiveCount})",
                value = EditorPrefs.GetBool(prefKey, false)
            };
            foldout.AddToClassList("nexui-sidebar-foldout");
            foldout.AddToClassList("nexui-component-category-folder");
            foldout.AddToClassList("nexui-component-builtin-folder");
            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.target == foldout) EditorPrefs.SetBool(prefKey, evt.newValue);
            });

            if (node.Recipes.Count > 0)
            {
                foldout.AddToClassList("nexui-component-builtin-leaf");
                var grid = new VisualElement();
                grid.AddToClassList("nexui-component-grid");
                foldout.Add(grid);

                // A full package library contains hundreds of recipes. Keep collapsed folders
                // cheap, while still materializing every card when search needs to inspect it.
                var built = false;
                void EnsureCards()
                {
                    if (built) return;
                    built = true;
                    foreach (var recipe in node.Recipes) grid.Add(CreateBuiltInCard(recipe));
                }
                foldout.userData = (Action)EnsureCards;
                _lazyBuiltInCardBuilders.Add(EnsureCards);
                var foldoutToggle = foldout.Q<Toggle>();
                foldoutToggle?.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue) EnsureCards();
                });
                foldoutToggle?.RegisterCallback<PointerDownEvent>(_ => EnsureCards());
                if (foldout.value) EnsureCards();
            }
            foreach (var child in node.Children.Values) BuildBuiltInFolder(foldout, child, depth + 1);
            parent.Add(foldout);
            _foldouts.Add(foldout);
        }

        private void BuildCustomLibrary()
        {
            var definitions = DesignerComponentLibrary.All;
            var familyFolder = new Foldout
            {
                text = $"{DesignerLocalization.T("palette.family.custom")} ({definitions.Count})",
                value = EditorPrefs.GetBool("NexUI.Designer.Components.Family.Custom",
                    _familyFilter == FamilyFilter.Custom || definitions.Count > 0)
            };
            familyFolder.AddToClassList("nexui-component-family-folder");
            familyFolder.AddToClassList("family-custom");
            familyFolder.RegisterValueChangedCallback(evt =>
            {
                if (evt.target == familyFolder)
                    EditorPrefs.SetBool("NexUI.Designer.Components.Family.Custom", evt.newValue);
            });
            _familyFoldouts.Add(familyFolder);

            var root = BuildCustomFolderTree(definitions);
            foreach (var child in root.Children.Values)
                BuildCustomFolder(familyFolder, child);

            if (root.Children.Count == 0)
            {
                var empty = new Label(DesignerLocalization.T("componentLibrary.empty"));
                empty.AddToClassList("nexui-component-library-empty");
                familyFolder.Add(empty);
            }
            _content.Add(familyFolder);
        }

        private sealed class CustomFolderNode
        {
            public string Name;
            public string Path;
            public readonly SortedDictionary<string, CustomFolderNode> Children =
                new SortedDictionary<string, CustomFolderNode>(StringComparer.OrdinalIgnoreCase);
            public readonly List<DesignerComponentDefinitionAsset> Definitions = new();

            public int RecursiveCount
            {
                get
                {
                    var count = Definitions.Count;
                    foreach (var child in Children.Values) count += child.RecursiveCount;
                    return count;
                }
            }
        }

        private static CustomFolderNode BuildCustomFolderTree(IReadOnlyList<DesignerComponentDefinitionAsset> definitions)
        {
            var root = new CustomFolderNode();
            foreach (var path in DesignerComponentLibrary.Categories()) EnsureFolder(root, path);
            foreach (var definition in definitions)
                EnsureFolder(root, DesignerComponentLibrary.EffectiveFolder(definition)).Definitions.Add(definition);
            return root;
        }

        private static CustomFolderNode EnsureFolder(CustomFolderNode root, string path)
        {
            var current = root;
            var currentPath = string.Empty;
            foreach (var segment in DesignerComponentLibrary.NormalizeFolder(path).Split('/'))
            {
                currentPath = string.IsNullOrEmpty(currentPath) ? segment : currentPath + "/" + segment;
                if (!current.Children.TryGetValue(segment, out var child))
                {
                    child = new CustomFolderNode { Name = segment, Path = currentPath };
                    current.Children.Add(segment, child);
                }
                current = child;
            }
            return current;
        }

        private void BuildCustomFolder(VisualElement parent, CustomFolderNode node)
        {
            var prefKey = "NexUI.Designer.Components.CustomFolder." + node.Path;
            var foldout = new Foldout
            {
                text = $"{node.Name} ({node.RecursiveCount})",
                value = EditorPrefs.GetBool(prefKey, node.Path == DesignerComponentLibrary.DefaultFolder)
            };
            foldout.AddToClassList("nexui-sidebar-foldout");
            foldout.AddToClassList("nexui-component-category-folder");
            foldout.AddToClassList("nexui-component-custom-folder");
            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.target == foldout) EditorPrefs.SetBool(prefKey, evt.newValue);
            });
            foldout.RegisterCallback<ContextClickEvent>(evt =>
            {
                ShowCustomFolderMenu(node.Path);
                evt.StopPropagation();
            });

            if (node.Definitions.Count > 0)
            {
                var grid = new VisualElement();
                grid.AddToClassList("nexui-component-grid");
                foldout.Add(grid);
                foreach (var definition in node.Definitions)
                    grid.Add(CreateCustomCard(definition));
            }

            foreach (var child in node.Children.Values)
                BuildCustomFolder(foldout, child);

            parent.Add(foldout);
            _foldouts.Add(foldout);
        }

        private void BuildRecent(VisualElement parent)
        {
            var foldout = new Foldout { text = DesignerLocalization.T("shell.library.recent"), value = true };
            foldout.AddToClassList("nexui-sidebar-foldout");
            foldout.AddToClassList("nexui-component-category-folder");
            var grid = new VisualElement();
            grid.AddToClassList("nexui-component-grid");
            foldout.Add(grid);

            foreach (var typeId in new[] { "Panel", "Button", "Label", "Image" })
                grid.Add(CreateCard(DesignerComponentRegistry.Get(typeId)));

            parent.Add(foldout);
            _foldouts.Add(foldout);
        }

        private void BuildGroup(VisualElement parent, DesignerPaletteGroupView group)
        {
            // The pref key stays on the stable group id, not the translated title, so the
            // expanded/collapsed state survives a language switch.
            var prefKey = "NexUI.Designer.Components." + group.GroupId;
            // Unity's own control libraries start collapsed: they are long, and a project usually
            // works in one of them at a time.
            var defaultOpen = group.Family == DesignerComponentFamily.NexUI;
            var foldout = new Foldout { text = group.Title, value = EditorPrefs.GetBool(prefKey, defaultOpen) };
            foldout.AddToClassList("nexui-sidebar-foldout");
            foldout.AddToClassList("nexui-component-category-folder");
            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.target == foldout) EditorPrefs.SetBool(prefKey, evt.newValue);
            });

            var grid = new VisualElement();
            grid.AddToClassList("nexui-component-grid");
            foldout.Add(grid);

            foreach (var descriptor in group.Items)
                grid.Add(CreateCard(descriptor));

            parent.Add(foldout);
            _foldouts.Add(foldout);
        }

        private Button CreateCard(DesignerComponentDescriptor descriptor)
        {
            var label = DesignerComponentPalette.DisplayName(descriptor);
            var button = new Button(() => _context.CreateMetadataElement(descriptor.TypeId))
            {
                text = string.Empty,
                tooltip = BuildTooltip(descriptor, label)
            };
            button.AddToClassList("nexui-component-card");

            button.Add(CreatePreview(descriptor, compact: true));

            var caption = new VisualElement { pickingMode = PickingMode.Ignore };
            caption.AddToClassList("nexui-component-card-caption");

            var title = new Label(label) { pickingMode = PickingMode.Ignore };
            title.AddToClassList("nexui-component-card-title");
            caption.Add(title);

            var badge = new Label(FamilyBadge(descriptor.Family)) { pickingMode = PickingMode.Ignore };
            badge.AddToClassList("nexui-component-family-badge");
            badge.AddToClassList(FamilyClass(descriptor.Family));
            caption.Add(badge);
            button.Add(caption);

            // Both the localized label and the type id are searchable, so "UGUI.Toggle" and "토글"
            // both find the same entry.
            button.userData = label + " " + descriptor.TypeId + " " + descriptor.DisplayName;
            button.RegisterCallback<PointerEnterEvent>(_ => ShowDetails(descriptor));
            button.RegisterCallback<FocusInEvent>(_ => ShowDetails(descriptor));
            button.RegisterCallback<ContextClickEvent>(evt =>
            {
                ShowCardMenu(descriptor);
                evt.StopPropagation();
            });
            _cards.Add(button);
            return button;
        }

        private Button CreateBuiltInCard(DesignerBuiltInComponentRecipe recipe)
        {
            var definition = recipe.Definition;
            var button = new Button(() => PlaceBuiltInComponent(recipe))
            {
                text = string.Empty,
                tooltip = BuildBuiltInTooltip(recipe)
            };
            button.AddToClassList("nexui-component-card");
            button.AddToClassList("nexui-component-builtin-card");
            button.Add(CreateCustomPreview(definition, true, "family-builtin"));

            var caption = new VisualElement { pickingMode = PickingMode.Ignore };
            caption.AddToClassList("nexui-component-card-caption");
            var title = new Label(recipe.DisplayName) { pickingMode = PickingMode.Ignore };
            title.AddToClassList("nexui-component-card-title");
            caption.Add(title);
            var badge = new Label(DesignerLocalization.T("componentLibrary.builtinBadge")) { pickingMode = PickingMode.Ignore };
            badge.AddToClassList("nexui-component-family-badge");
            badge.AddToClassList("family-builtin");
            caption.Add(badge);
            button.Add(caption);

            button.userData = recipe.DisplayName + " " + definition.EffectiveDisplayName + " " +
                              recipe.CategoryPath + " " + recipe.Id + " " + recipe.Description + " " +
                              string.Join(" ", definition.tags ?? new List<string>());
            button.RegisterCallback<PointerEnterEvent>(_ => ShowBuiltInDetails(recipe));
            button.RegisterCallback<FocusInEvent>(_ => ShowBuiltInDetails(recipe));
            button.RegisterCallback<ContextClickEvent>(evt =>
            {
                ShowBuiltInCardMenu(recipe);
                evt.StopPropagation();
            });
            _cards.Add(button);
            return button;
        }

        private Button CreateCustomCard(DesignerComponentDefinitionAsset definition)
        {
            var label = definition.EffectiveDisplayName;
            var button = new Button(() => PlaceCustomComponent(definition))
            {
                text = string.Empty,
                tooltip = BuildCustomTooltip(definition)
            };
            button.AddToClassList("nexui-component-card");
            button.AddToClassList("nexui-component-custom-card");
            button.Add(CreateCustomPreview(definition, true));

            var caption = new VisualElement { pickingMode = PickingMode.Ignore };
            caption.AddToClassList("nexui-component-card-caption");
            var title = new Label(label) { pickingMode = PickingMode.Ignore };
            title.AddToClassList("nexui-component-card-title");
            caption.Add(title);
            var badge = new Label(DesignerLocalization.T("componentLibrary.customBadge")) { pickingMode = PickingMode.Ignore };
            badge.AddToClassList("nexui-component-family-badge");
            badge.AddToClassList("family-custom");
            caption.Add(badge);
            button.Add(caption);

            button.userData = label + " " + DesignerComponentLibrary.EffectiveFolder(definition) + " " +
                              definition.componentId + " " + definition.description + " " +
                              (definition.tags == null ? string.Empty : string.Join(" ", definition.tags));
            button.RegisterCallback<PointerEnterEvent>(_ => ShowCustomDetails(definition));
            button.RegisterCallback<FocusInEvent>(_ => ShowCustomDetails(definition));
            button.RegisterCallback<ContextClickEvent>(evt =>
            {
                ShowCustomCardMenu(definition);
                evt.StopPropagation();
            });
            _cards.Add(button);
            return button;
        }

        private static VisualElement CreateCustomPreview(DesignerComponentDefinitionAsset definition, bool compact,
            string familyClass = "family-custom")
        {
            var frame = new VisualElement { pickingMode = PickingMode.Ignore };
            frame.AddToClassList(compact ? "nexui-component-card-preview" : "nexui-component-detail-preview");
            frame.AddToClassList(familyClass);

            if (definition.thumbnail != null)
            {
                var image = new UnityEngine.UIElements.Image
                {
                    image = definition.thumbnail,
                    scaleMode = ScaleMode.ScaleAndCrop,
                    pickingMode = PickingMode.Ignore
                };
                image.AddToClassList("nexui-component-custom-thumbnail");
                frame.Add(image);
                return frame;
            }

            var root = definition.Root;
            var surface = new VisualElement { pickingMode = PickingMode.Ignore };
            surface.AddToClassList("nexui-component-preview-surface");
            surface.style.backgroundColor = new StyleColor(root != null ? root.tint : new Color(0.16f, 0.20f, 0.28f, 1f));
            frame.Add(surface);

            if (root != null)
            {
                BuildDefinitionPreview(surface, definition, root, compact);
            }
            if (surface.childCount == 0)
            {
                var fallback = new Label(definition.EffectiveDisplayName) { pickingMode = PickingMode.Ignore };
                fallback.AddToClassList("nexui-component-preview-fallback");
                surface.Add(fallback);
            }
            return frame;
        }

        private static void BuildDefinitionPreview(VisualElement surface, DesignerComponentDefinitionAsset definition,
            DesignerElementMetadata root, bool compact)
        {
            var elements = definition.elements;
            if (elements == null || elements.Count <= 1)
            {
                var rootContext = new DesignerPreviewContext(root, DesignerComponentState.Normal, compact ? 0.75f : 1f, false);
                DesignerComponentPreviewRegistry.Get(root.elementType).BuildPreview(surface, rootContext);
                return;
            }

            surface.AddToClassList("nexui-component-composite-preview");
            var rootRect = root.rect;
            var rootWidth = Mathf.Max(1f, rootRect.width);
            var rootHeight = Mathf.Max(1f, rootRect.height);
            var previewZoom = compact ? 0.42f : 0.72f;

            foreach (var element in elements)
            {
                if (element == null || ReferenceEquals(element, root) || element.elementId == root.elementId) continue;

                var localX = (element.rect.x - rootRect.x) / rootWidth * 100f;
                var localY = (element.rect.y - rootRect.y) / rootHeight * 100f;
                var width = element.rect.width / rootWidth * 100f;
                var height = element.rect.height / rootHeight * 100f;
                var view = new VisualElement { pickingMode = PickingMode.Ignore };
                view.AddToClassList("nexui-component-composite-part");
                view.style.left = new Length(Mathf.Clamp(localX, 0f, 100f), LengthUnit.Percent);
                view.style.top = new Length(Mathf.Clamp(localY, 0f, 100f), LengthUnit.Percent);
                view.style.width = new Length(Mathf.Clamp(width, 1.5f, 100f), LengthUnit.Percent);
                view.style.height = new Length(Mathf.Clamp(height, 2f, 100f), LengthUnit.Percent);
                view.style.backgroundColor = new StyleColor(element.tint);
                ApplyMiniatureShape(view, element.shape, compact);
                surface.Add(view);

                var context = new DesignerPreviewContext(element, DesignerComponentState.Normal, previewZoom, false);
                DesignerComponentPreviewRegistry.Get(element.elementType).BuildPreview(view, context);
                if (view.childCount == 0 && !string.IsNullOrEmpty(element.text))
                {
                    var text = new Label(element.text) { pickingMode = PickingMode.Ignore };
                    text.AddToClassList("nexui-component-composite-text");
                    text.style.color = new StyleColor(element.textColor);
                    view.Add(text);
                }
            }
        }

        private static void ApplyMiniatureShape(VisualElement view, DesignerElementShape shape, bool compact)
        {
            var radius = shape switch
            {
                DesignerElementShape.Rectangle => 0f,
                DesignerElementShape.Pill => 999f,
                DesignerElementShape.Circle => 999f,
                _ => compact ? 2f : 5f
            };
            view.style.borderTopLeftRadius = radius;
            view.style.borderTopRightRadius = radius;
            view.style.borderBottomLeftRadius = radius;
            view.style.borderBottomRightRadius = radius;
        }

        private void ShowBuiltInDetails(DesignerBuiltInComponentRecipe recipe)
        {
            if (recipe?.Definition == null || _details == null) return;
            var definition = recipe.Definition;
            _details.Clear();

            var body = new VisualElement();
            body.AddToClassList("nexui-component-details-body");
            body.Add(CreateCustomPreview(definition, false, "family-builtin"));
            var copy = new VisualElement();
            copy.AddToClassList("nexui-component-details-copy");
            var title = new Label(recipe.DisplayName);
            title.AddToClassList("nexui-component-details-title");
            copy.Add(title);
            var folder = new Label(DesignerLocalization.T("palette.family.nexui") + " / " +
                                   DesignerLocalization.T("palette.family.builtin") + " / " + recipe.CategoryLabel);
            folder.AddToClassList("nexui-component-details-type");
            copy.Add(folder);
            var description = new Label(recipe.Description);
            description.AddToClassList("nexui-component-details-description");
            copy.Add(description);
            body.Add(copy);
            _details.Add(body);

            var stats = new Label(string.Format(DesignerLocalization.T("componentLibrary.builtinStats"),
                definition.elements?.Count ?? 0, definition.exposedProperties?.Count ?? 0,
                definition.variantProperties?.Count ?? 0));
            stats.AddToClassList("nexui-component-details-capabilities");
            _details.Add(stats);
        }

        private static string BuildBuiltInTooltip(DesignerBuiltInComponentRecipe recipe)
        {
            var builder = new StringBuilder();
            builder.AppendLine(recipe.DisplayName);
            builder.Append(DesignerLocalization.T("componentLibrary.folder")).Append(": ")
                .Append(DesignerLocalization.T("palette.family.nexui")).Append(" / ")
                .Append(DesignerLocalization.T("palette.family.builtin")).Append(" / ").AppendLine(recipe.CategoryLabel);
            builder.AppendLine(recipe.Description);
            builder.AppendLine().Append(DesignerLocalization.T("componentLibrary.builtinHint"));
            return builder.ToString();
        }

        private void PlaceBuiltInComponent(DesignerBuiltInComponentRecipe recipe)
        {
            var result = DesignerComponentService.Instantiate(_context.Metadata, recipe.Definition, new Vector2(96f, 96f));
            if (!result.Success)
            {
                EditorUtility.DisplayDialog(DesignerLocalization.T("componentLibrary.title"), result.Message,
                    DesignerLocalization.T("common.ok"));
                return;
            }
            result.Element.displayName = recipe.DisplayName;
            _context.InvalidateComponentExpansion();
            _context.Validate();
            _context.Select(result.Element);
        }

        private void ShowBuiltInCardMenu(DesignerBuiltInComponentRecipe recipe)
        {
            var menu = new GenericMenu();
            if (_context.Metadata != null)
                menu.AddItem(new GUIContent(DesignerLocalization.T("ctx.library.add")), false,
                    () => PlaceBuiltInComponent(recipe));
            else
                menu.AddDisabledItem(new GUIContent(DesignerLocalization.T("ctx.library.add")));
            menu.AddSeparator(string.Empty);
            var favourite = DesignerComponentLibrary.IsFavourite(recipe.Definition);
            menu.AddItem(new GUIContent(DesignerLocalization.T("componentLibrary.favourite")), favourite,
                () => DesignerComponentLibrary.SetFavourite(recipe.Definition, !favourite));
            menu.ShowAsContext();
        }

        private void ShowCustomDetails(DesignerComponentDefinitionAsset definition)
        {
            if (definition == null || _details == null) return;
            _details.Clear();

            var body = new VisualElement();
            body.AddToClassList("nexui-component-details-body");
            body.Add(CreateCustomPreview(definition, false));
            var copy = new VisualElement();
            copy.AddToClassList("nexui-component-details-copy");
            var title = new Label(definition.EffectiveDisplayName);
            title.AddToClassList("nexui-component-details-title");
            copy.Add(title);
            var folder = new Label(DesignerComponentLibrary.EffectiveFolder(definition));
            folder.AddToClassList("nexui-component-details-type");
            copy.Add(folder);
            if (!string.IsNullOrWhiteSpace(definition.description))
            {
                var description = new Label(definition.description);
                description.AddToClassList("nexui-component-details-description");
                copy.Add(description);
            }
            body.Add(copy);
            _details.Add(body);

            var stats = new Label(string.Format(DesignerLocalization.T("componentLibrary.customStats"),
                definition.elements?.Count ?? 0, definition.slots?.Count ?? 0,
                definition.exposedProperties?.Count ?? 0, definition.variantProperties?.Count ?? 0));
            stats.AddToClassList("nexui-component-details-capabilities");
            _details.Add(stats);
        }

        private static string BuildCustomTooltip(DesignerComponentDefinitionAsset definition)
        {
            var builder = new StringBuilder();
            builder.AppendLine(definition.EffectiveDisplayName);
            builder.Append(DesignerLocalization.T("componentLibrary.folder")).Append(": ")
                .AppendLine(DesignerComponentLibrary.EffectiveFolder(definition));
            if (!string.IsNullOrWhiteSpace(definition.description)) builder.AppendLine(definition.description);
            builder.AppendLine().Append(DesignerLocalization.T("componentLibrary.customHint"));
            return builder.ToString();
        }

        private void PlaceCustomComponent(DesignerComponentDefinitionAsset definition)
        {
            var result = DesignerComponentService.Instantiate(_context.Metadata, definition, new Vector2(96f, 96f));
            if (!result.Success)
            {
                EditorUtility.DisplayDialog(DesignerLocalization.T("componentLibrary.title"), result.Message,
                    DesignerLocalization.T("common.ok"));
                return;
            }
            _context.InvalidateComponentExpansion();
            _context.Validate();
            _context.Select(result.Element);
        }

        private void CreateCustomFromSelection() => CreateCustomFromSelection(DesignerComponentLibrary.DefaultFolder);

        private void CreateCustomFromSelection(string folder)
        {
            var selected = _context.SelectedMetadata;
            if (_context.Metadata == null || selected == null)
            {
                EditorUtility.DisplayDialog(DesignerLocalization.T("componentLibrary.createFromSelection"),
                    DesignerLocalization.T("componentLibrary.selectionRequired"), DesignerLocalization.T("common.ok"));
                return;
            }
            if (selected.componentInstance != null && selected.componentInstance.IsInstance)
            {
                EditorUtility.DisplayDialog(DesignerLocalization.T("componentLibrary.createFromSelection"),
                    DesignerLocalization.T("componentLibrary.instanceSelection"), DesignerLocalization.T("common.ok"));
                return;
            }

            var defaultName = string.IsNullOrWhiteSpace(selected.displayName) ? selected.elementId : selected.displayName;
            var path = EditorUtility.SaveFilePanelInProject(
                DesignerLocalization.T("componentLibrary.createFromSelection"), defaultName, "asset",
                DesignerLocalization.T("componentLibrary.saveHint"), "Assets");
            if (string.IsNullOrEmpty(path)) return;

            EditorPrefs.SetBool("NexUI.Designer.Components.Family.Custom", true);
            var result = DesignerComponentService.CreateDefinitionFromSubtree(_context.Metadata, selected.elementId, path, defaultName);
            if (!result.Success)
            {
                EditorUtility.DisplayDialog(DesignerLocalization.T("componentLibrary.title"), result.Message,
                    DesignerLocalization.T("common.ok"));
                return;
            }
            DesignerComponentLibrary.SetFolder(result.Definition, folder);
            _context.InvalidateComponentExpansion();
            _context.Validate();
            _context.Select(result.Element);
            EditorGUIUtility.PingObject(result.Definition);
        }

        private void CreateFolder()
            => DesignerTextPromptWindow.ShowPrompt("componentLibrary.newFolder", "componentLibrary.folder", string.Empty, true,
                path =>
                {
                    EditorPrefs.SetBool("NexUI.Designer.Components.Family.Custom", true);
                    if (!DesignerComponentLibrary.CreateFolder(path))
                        EditorUtility.DisplayDialog(DesignerLocalization.T("componentLibrary.newFolder"),
                            DesignerLocalization.T("componentLibrary.folderExists"), DesignerLocalization.T("common.ok"));
                });

        private void ShowCustomFolderMenu(string folder)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(DesignerLocalization.T("componentLibrary.createHere")), false,
                () => CreateCustomFromSelection(folder));
            menu.AddItem(new GUIContent(DesignerLocalization.T("componentLibrary.newSubfolder")), false, () =>
                DesignerTextPromptWindow.ShowPrompt("componentLibrary.newSubfolder", "componentLibrary.folder",
                    folder + "/", true, path => DesignerComponentLibrary.CreateFolder(path)));
            menu.AddSeparator(string.Empty);

            if (!string.Equals(folder, DesignerComponentLibrary.DefaultFolder, StringComparison.OrdinalIgnoreCase))
            {
                menu.AddItem(new GUIContent(DesignerLocalization.T("componentLibrary.renameFolder")), false, () =>
                    DesignerTextPromptWindow.ShowPrompt("componentLibrary.renameFolder", "componentLibrary.folder",
                        folder, true, renamed => DesignerComponentLibrary.RenameFolder(folder, renamed)));
                menu.AddItem(new GUIContent(DesignerLocalization.T("componentLibrary.removeFolder")), false, () =>
                {
                    if (EditorUtility.DisplayDialog(DesignerLocalization.T("componentLibrary.removeFolder"),
                            DesignerLocalization.T("componentLibrary.removeFolderConfirm"),
                            DesignerLocalization.T("common.remove"), DesignerLocalization.T("common.cancel")))
                        DesignerComponentLibrary.RemoveFolder(folder);
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent(DesignerLocalization.T("componentLibrary.renameFolder")));
                menu.AddDisabledItem(new GUIContent(DesignerLocalization.T("componentLibrary.removeFolder")));
            }
            menu.ShowAsContext();
        }

        private void ShowCustomCardMenu(DesignerComponentDefinitionAsset definition)
        {
            var menu = new GenericMenu();
            if (_context.Metadata != null)
                menu.AddItem(new GUIContent(DesignerLocalization.T("ctx.library.add")), false, () => PlaceCustomComponent(definition));
            else
                menu.AddDisabledItem(new GUIContent(DesignerLocalization.T("ctx.library.add")));

            menu.AddSeparator(string.Empty);
            foreach (var folder in DesignerComponentLibrary.Categories())
            {
                var captured = folder;
                menu.AddItem(new GUIContent(DesignerLocalization.T("componentLibrary.moveTo") + "/" + captured),
                    string.Equals(DesignerComponentLibrary.EffectiveFolder(definition), captured, StringComparison.OrdinalIgnoreCase),
                    () => DesignerComponentLibrary.SetFolder(definition, captured));
            }
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent(DesignerLocalization.T("componentLibrary.renameComponent")), false, () =>
                DesignerTextPromptWindow.ShowPrompt("componentLibrary.renameComponent", "componentLibrary.componentName",
                    definition.EffectiveDisplayName, false, name => DesignerComponentLibrary.RenameComponent(definition, name)));
            menu.AddItem(new GUIContent(DesignerLocalization.T("componentLibrary.editAsset")), false, () => Selection.activeObject = definition);
            menu.AddItem(new GUIContent(DesignerLocalization.T("componentLibrary.pingAsset")), false, () => EditorGUIUtility.PingObject(definition));
            var favourite = DesignerComponentLibrary.IsFavourite(definition);
            menu.AddItem(new GUIContent(DesignerLocalization.T("componentLibrary.favourite")), favourite,
                () => DesignerComponentLibrary.SetFavourite(definition, !favourite));
            menu.ShowAsContext();
        }

        /// <summary>
        /// Keeps a larger, persistent preview above the scrolling library. Native Unity tooltips
        /// still carry the full text summary, while this panel makes the component understandable
        /// without waiting for the tooltip delay.
        /// </summary>
        private void ShowDetails(DesignerComponentDescriptor descriptor)
        {
            if (descriptor == null || _details == null) return;
            _details.Clear();

            var body = new VisualElement { pickingMode = PickingMode.Ignore };
            body.AddToClassList("nexui-component-details-body");
            body.Add(CreatePreview(descriptor, compact: false));

            var copy = new VisualElement { pickingMode = PickingMode.Ignore };
            copy.AddToClassList("nexui-component-details-copy");
            var label = DesignerComponentPalette.DisplayName(descriptor);
            var title = new Label(label) { pickingMode = PickingMode.Ignore };
            title.AddToClassList("nexui-component-details-title");
            copy.Add(title);
            var type = new Label(descriptor.TypeId) { pickingMode = PickingMode.Ignore };
            type.AddToClassList("nexui-component-details-type");
            copy.Add(type);
            if (!string.IsNullOrEmpty(descriptor.Description))
            {
                var description = new Label(descriptor.Description) { pickingMode = PickingMode.Ignore };
                description.AddToClassList("nexui-component-details-description");
                copy.Add(description);
            }
            body.Add(copy);
            _details.Add(body);

            var support = new VisualElement { pickingMode = PickingMode.Ignore };
            support.AddToClassList("nexui-component-support-row");
            support.Add(SupportBadge("uGUI", descriptor.UGUISupport));
            support.Add(SupportBadge("UI Toolkit", descriptor.UIToolkitSupport));
            _details.Add(support);

            var capabilities = new Label(CapabilitySummary(descriptor)) { pickingMode = PickingMode.Ignore };
            capabilities.AddToClassList("nexui-component-details-capabilities");
            _details.Add(capabilities);
        }

        private static VisualElement CreatePreview(DesignerComponentDescriptor descriptor, bool compact)
        {
            var frame = new VisualElement { pickingMode = PickingMode.Ignore };
            frame.AddToClassList(compact ? "nexui-component-card-preview" : "nexui-component-detail-preview");
            frame.AddToClassList(FamilyClass(descriptor.Family));

            var surface = new VisualElement { pickingMode = PickingMode.Ignore };
            surface.AddToClassList("nexui-component-preview-surface");
            surface.style.backgroundColor = new StyleColor(descriptor.DefaultColor);
            var radius = descriptor.DefaultShape == DesignerElementShape.Rectangle ? 2f : 6f;
            surface.style.borderTopLeftRadius = radius;
            surface.style.borderTopRightRadius = radius;
            surface.style.borderBottomLeftRadius = radius;
            surface.style.borderBottomRightRadius = radius;
            frame.Add(surface);

            var element = new DesignerElementMetadata
            {
                elementId = "palettePreview",
                displayName = descriptor.DisplayName,
                elementType = descriptor.TypeId,
                rect = new Rect(0, 0, descriptor.DefaultSize.x, descriptor.DefaultSize.y),
                text = string.IsNullOrEmpty(descriptor.DefaultText) ? descriptor.DisplayName : descriptor.DefaultText,
                tint = descriptor.DefaultColor,
                textColor = Color.white,
                fontSize = compact ? 10 : 12,
                previewValue = 60f,
                previewItemCount = compact ? 3 : 4
            };
            element.previewOptions.Add("One");
            element.previewOptions.Add("Two");
            element.previewOptions.Add("Three");

            var ctx = new DesignerPreviewContext(element, DesignerComponentState.Normal, compact ? 0.75f : 1f, false);
            DesignerComponentPreviewRegistry.Get(descriptor.TypeId).BuildPreview(surface, ctx);

            // Generic/container/text types intentionally have no virtual-parts renderer. Give those
            // a restrained label instead of leaving an indistinguishable empty color swatch.
            if (surface.childCount == 0)
            {
                var fallback = new Label(PreviewCaption(descriptor)) { pickingMode = PickingMode.Ignore };
                fallback.AddToClassList("nexui-component-preview-fallback");
                surface.Add(fallback);
            }
            return frame;
        }

        private static Label SupportBadge(string backend, DesignerBackendSupport support)
        {
            var badge = new Label(backend + " · " + SupportLabel(support)) { pickingMode = PickingMode.Ignore };
            badge.AddToClassList("nexui-component-support-badge");
            badge.AddToClassList("support-" + support.ToString().ToLowerInvariant());
            return badge;
        }

        private static string BuildTooltip(DesignerComponentDescriptor descriptor, string label)
        {
            var sb = new StringBuilder();
            sb.Append(label).Append("  [").Append(descriptor.TypeId).AppendLine("]");
            if (!string.IsNullOrEmpty(descriptor.Description)) sb.AppendLine(descriptor.Description);
            sb.AppendLine();
            sb.Append(DesignerLocalization.T("palette.tooltip.family")).Append(": ").Append(FamilyLabel(descriptor.Family)).AppendLine();
            sb.Append(DesignerLocalization.T("palette.tooltip.backends")).Append(": uGUI ").Append(SupportLabel(descriptor.UGUISupport))
              .Append(" · UI Toolkit ").Append(SupportLabel(descriptor.UIToolkitSupport)).AppendLine();
            sb.Append(DesignerLocalization.T("palette.tooltip.size")).Append(": ")
              .Append(Mathf.RoundToInt(descriptor.DefaultSize.x)).Append(" × ").Append(Mathf.RoundToInt(descriptor.DefaultSize.y)).AppendLine();
            sb.Append(DesignerLocalization.T("palette.tooltip.states")).Append(": ").Append(FlagList(descriptor.SupportedStates)).AppendLine();
            sb.Append(DesignerLocalization.T("palette.tooltip.bindings")).Append(": ").Append(FlagList(descriptor.SupportedBindings));
            if (descriptor.SupportedEvents.Count > 0)
                sb.AppendLine().Append(DesignerLocalization.T("palette.tooltip.events")).Append(": ")
                  .Append(string.Join(" · ", descriptor.SupportedEvents));
            sb.AppendLine().AppendLine().Append(DesignerLocalization.T("palette.tooltip.hint"));
            return sb.ToString();
        }

        private static string CapabilitySummary(DesignerComponentDescriptor descriptor)
        {
            var items = new List<string>();
            if (descriptor.IsContainer || descriptor.CanHaveChildren) items.Add(DesignerLocalization.T("palette.capability.children"));
            if (descriptor.IsInteractive) items.Add(DesignerLocalization.T("palette.capability.interactive"));
            if (descriptor.IsValueComponent) items.Add(DesignerLocalization.T("palette.capability.value"));
            if (descriptor.IsCollectionComponent) items.Add(DesignerLocalization.T("palette.capability.collection"));
            if (descriptor.IsOverlayComponent) items.Add(DesignerLocalization.T("palette.capability.overlay"));
            if (items.Count == 0) items.Add(DesignerLocalization.T("palette.capability.display"));
            return string.Join("  ·  ", items);
        }

        private static string FlagList(Enum value)
        {
            var text = value?.ToString();
            return string.IsNullOrEmpty(text) || text == "None"
                ? DesignerLocalization.T("palette.value.none")
                : text.Replace(", ", " · ");
        }

        private static string PreviewCaption(DesignerComponentDescriptor descriptor)
        {
            if (!string.IsNullOrEmpty(descriptor.DefaultText)) return descriptor.DefaultText;
            var name = DesignerComponentPalette.DisplayName(descriptor);
            return name.Length > 16 ? name.Substring(0, 15) + "…" : name;
        }

        private static string FamilyLabel(DesignerComponentFamily family) => family switch
        {
            DesignerComponentFamily.UGUI => DesignerLocalization.T("palette.family.ugui"),
            DesignerComponentFamily.UIToolkit => DesignerLocalization.T("palette.family.uitk"),
            _ => DesignerLocalization.T("palette.family.nexui")
        };

        private static string FamilyBadge(DesignerComponentFamily family) => family switch
        {
            DesignerComponentFamily.UGUI => "uGUI",
            DesignerComponentFamily.UIToolkit => "UITK",
            _ => "NexUI"
        };

        private static string FamilyClass(DesignerComponentFamily family) => family switch
        {
            DesignerComponentFamily.UGUI => "family-ugui",
            DesignerComponentFamily.UIToolkit => "family-uitk",
            _ => "family-nexui"
        };

        private static string SupportLabel(DesignerBackendSupport support) => support switch
        {
            DesignerBackendSupport.Full => DesignerLocalization.T("palette.support.full"),
            DesignerBackendSupport.Partial => DesignerLocalization.T("palette.support.partial"),
            DesignerBackendSupport.PreviewOnly => DesignerLocalization.T("palette.support.previewOnly"),
            _ => DesignerLocalization.T("palette.support.unsupported")
        };

        /// <summary>
        /// Right-clicking a Library entry offers where to put it, mirroring how Unity's own
        /// create menus distinguish "at the root" from "under the current selection".
        /// </summary>
        private void ShowCardMenu(DesignerComponentDescriptor descriptor)
        {
            var menu = new GenericMenu();
            var canAdd = _context.Metadata != null;
            var parent = _context.SelectedMetadata;

            if (canAdd)
                menu.AddItem(new GUIContent(DesignerLocalization.T("ctx.library.add")), false,
                    () => _context.CreateMetadataElement(descriptor.TypeId));
            else
                menu.AddDisabledItem(new GUIContent(DesignerLocalization.T("ctx.library.add")));

            if (canAdd && parent != null)
                menu.AddItem(new GUIContent(DesignerLocalization.T("ctx.library.addAsChild")), false, () =>
                    NexUIDesignerUndo.Group("Add NexUI Element As Child", () =>
                    {
                        var created = _context.CreateMetadataElement(descriptor.TypeId);
                        if (created != null) _context.ReparentElement(created, parent);
                    }));
            else
                menu.AddDisabledItem(new GUIContent(DesignerLocalization.T("ctx.library.addAsChild")));

            menu.ShowAsContext();
        }

        private void RefreshFilter()
        {
            // Searching must see cards that are normally omitted from collapsed Built-In folders.
            if (!string.IsNullOrEmpty(_filter))
                foreach (var ensureCards in _lazyBuiltInCardBuilders) ensureCards();

            foreach (var card in _cards)
            {
                var label = card.userData as string ?? "";
                card.style.display = string.IsNullOrEmpty(_filter) || label.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }

            // A search that matches a collapsed library would otherwise look like "no results".
            var searching = !string.IsNullOrEmpty(_filter);
            foreach (var foldout in _foldouts)
            {
                var anyVisible = false;
                foreach (var card in _cards)
                    if (foldout.Contains(card) && card.style.display != DisplayStyle.None) { anyVisible = true; break; }
                foldout.style.display = anyVisible || !searching ? DisplayStyle.Flex : DisplayStyle.None;
                if (searching && anyVisible) foldout.value = true;
            }

            // Category matches also keep their broader library folder visible and expanded.
            foreach (var familyFoldout in _familyFoldouts)
            {
                var anyVisible = false;
                foreach (var card in _cards)
                    if (familyFoldout.Contains(card) && card.style.display != DisplayStyle.None) { anyVisible = true; break; }
                familyFoldout.style.display = anyVisible || !searching ? DisplayStyle.Flex : DisplayStyle.None;
                if (searching && anyVisible) familyFoldout.value = true;
            }
        }

    }

    /// <summary>Small non-modal text prompt used by library folder and component rename actions.</summary>
    internal sealed class DesignerTextPromptWindow : EditorWindow
    {
        private string _labelKey;
        private string _value;
        private Action<string> _confirm;
        private bool _allowPath;
        private bool _focusPending = true;

        public static void ShowPrompt(string titleKey, string labelKey, string initialValue, bool allowPath, Action<string> confirm)
        {
            var window = CreateInstance<DesignerTextPromptWindow>();
            window.titleContent = new GUIContent(DesignerLocalization.T(titleKey));
            window._labelKey = labelKey;
            window._value = initialValue ?? string.Empty;
            window._allowPath = allowPath;
            window._confirm = confirm;
            window.minSize = window.maxSize = new Vector2(360f, 118f);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            GUI.SetNextControlName("NexUITextPrompt");
            _value = EditorGUILayout.TextField(DesignerLocalization.T(_labelKey), _value);
            if (_focusPending)
            {
                _focusPending = false;
                EditorGUI.FocusTextInControl("NexUITextPrompt");
            }

            var raw = (_value ?? string.Empty).Trim();
            var normalized = _allowPath && !string.IsNullOrEmpty(raw)
                ? DesignerComponentLibrary.NormalizeFolder(raw)
                : raw;
            if (_allowPath)
                EditorGUILayout.LabelField(DesignerLocalization.T("componentLibrary.folderPathHint"), EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(DesignerLocalization.T("common.cancel"), GUILayout.Width(82f))) Close();
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(normalized)))
            {
                if (GUILayout.Button(DesignerLocalization.T("common.apply"), GUILayout.Width(82f)))
                {
                    var callback = _confirm;
                    Close();
                    callback?.Invoke(normalized);
                }
            }
            EditorGUILayout.EndHorizontal();

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Event.current.Use();
                Close();
            }
        }
    }
}
