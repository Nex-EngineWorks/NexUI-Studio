using System;
using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Localization;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Components.Definitions
{
    /// <summary>Localized presentation plus the runtime-safe definition used to place one package recipe.</summary>
    public sealed class DesignerBuiltInComponentRecipe
    {
        internal readonly string ArchetypeKey;
        internal readonly string ThemeKey;
        internal readonly string CategoryKey;

        public DesignerComponentDefinitionAsset Definition { get; internal set; }
        public string Id => Definition?.componentId;
        public string CategoryPath { get; internal set; }
        public string DisplayName => DesignerLocalization.T(ArchetypeKey) + " · " + DesignerLocalization.T(ThemeKey);
        public string ArchetypeLabel => DesignerLocalization.T(ArchetypeKey);
        public string Description => DesignerLocalization.T("builtinRecipe.description",
            DesignerLocalization.T(ArchetypeKey), DesignerLocalization.T(ThemeKey));
        public string CategoryLabel => DesignerLocalization.T(CategoryKey);

        internal DesignerBuiltInComponentRecipe(string archetypeKey, string themeKey, string categoryKey)
        {
            ArchetypeKey = archetypeKey;
            ThemeKey = themeKey;
            CategoryKey = categoryKey;
        }
    }

    /// <summary>
    /// Package-owned composite UI recipes. The definitions are deterministic in-memory assets:
    /// instances serialize a stable <c>builtin:</c> identity and resolve on every machine without
    /// copying hundreds of ScriptableObject files into the package or the user's Assets folder.
    /// </summary>
    public static class DesignerBuiltInComponentCatalog
    {
        public const string GuidPrefix = "builtin:";
        public const int ExpectedRecipeCount = 300;

        private enum RecipeKind { Header, Navigation, Metric, Card, Empty, Form, Toolbar, Row, Grid, Overlay }

        private sealed class Archetype
        {
            public string Slug, Name, Key, Category, CategoryKey, Title, Subtitle, Action;
            public string[] Tags;
            public RecipeKind Kind;
            public Vector2 Size;
        }

        private sealed class Theme
        {
            public string Slug, Name, Key;
            public Color Surface, Raised, Accent, Text, Muted;
            public float Density;
        }

        private static readonly List<DesignerBuiltInComponentRecipe> Recipes = new List<DesignerBuiltInComponentRecipe>();
        private static readonly Dictionary<string, DesignerBuiltInComponentRecipe> ById =
            new Dictionary<string, DesignerBuiltInComponentRecipe>(StringComparer.Ordinal);
        private static bool _built;

        public static IReadOnlyList<DesignerBuiltInComponentRecipe> All
        {
            get { EnsureBuilt(); return Recipes; }
        }

        public static DesignerBuiltInComponentRecipe ResolveRecipe(string definitionGuid, string definitionId)
        {
            EnsureBuilt();
            var id = definitionId;
            if (string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(definitionGuid) &&
                definitionGuid.StartsWith(GuidPrefix, StringComparison.Ordinal))
                id = definitionGuid.Substring(GuidPrefix.Length);
            return !string.IsNullOrEmpty(id) && ById.TryGetValue(id, out var recipe) ? recipe : null;
        }

        public static DesignerComponentDefinitionAsset Resolve(string definitionGuid, string definitionId)
            => ResolveRecipe(definitionGuid, definitionId)?.Definition;

        public static bool IsBuiltIn(DesignerComponentDefinitionAsset definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.componentId)) return false;
            EnsureBuilt();
            return ById.TryGetValue(definition.componentId, out var recipe) && ReferenceEquals(recipe.Definition, definition);
        }

        public static string SyntheticGuid(DesignerComponentDefinitionAsset definition)
            => IsBuiltIn(definition) ? GuidPrefix + definition.componentId : null;

        private static void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            Recipes.Clear();
            ById.Clear();

            foreach (var archetype in Archetypes())
            foreach (var theme in Themes())
            {
                var id = "builtin.recipe." + archetype.Slug + "." + theme.Slug;
                var recipe = new DesignerBuiltInComponentRecipe(archetype.Key, theme.Key, archetype.CategoryKey)
                {
                    CategoryPath = archetype.Category + "/" + archetype.Name,
                    Definition = BuildDefinition(id, archetype, theme)
                };
                Recipes.Add(recipe);
                ById.Add(id, recipe);
            }

            Recipes.Sort((a, b) => string.Compare(a.Id, b.Id, StringComparison.Ordinal));
        }

        private static DesignerComponentDefinitionAsset BuildDefinition(string id, Archetype a, Theme t)
        {
            var definition = ScriptableObject.CreateInstance<DesignerComponentDefinitionAsset>();
            definition.hideFlags = HideFlags.HideAndDontSave;
            definition.name = id;
            definition.componentId = id;
            definition.version = 1;
            definition.displayName = a.Name + " · " + t.Name;
            definition.category = "NexUI/Built-In/" + a.Category;
            definition.description = "Package built-in " + a.Name + " recipe in the " + t.Name + " visual style.";
            definition.tags = new List<string> { "nexui", "built-in", "recipe", a.Slug, t.Slug, a.Category.ToLowerInvariant() };
            if (a.Tags != null) definition.tags.AddRange(a.Tags);
            definition.defaultSize = a.Size;
            definition.rootElementId = "root";

            var root = Element(id, "root", "Panel", new Rect(Vector2.zero, a.Size), string.Empty, t.Surface, t.Text, 14);
            root.displayName = a.Name;
            root.clipChildren = true;
            root.classes.Add("nexui-built-in-recipe");
            root.classes.Add("recipe-" + a.Slug);
            root.classes.Add("theme-" + t.Slug);
            root.visualStyle.hasOverrides = true;
            root.visualStyle.backgroundColor = t.Surface;
            root.visualStyle.cornerRadius = a.Kind == RecipeKind.Row || a.Kind == RecipeKind.Toolbar ? 10f : 14f;
            root.visualStyle.borderWidth = 1f;
            root.visualStyle.borderColor = Color.Lerp(t.Raised, t.Accent, 0.25f);
            root.visualStyle.dropShadow = a.Kind == RecipeKind.Card || a.Kind == RecipeKind.Overlay;
            root.visualStyle.shadowColor = new Color(0f, 0f, 0f, 0.28f);
            definition.elements.Add(root);

            switch (a.Kind)
            {
                case RecipeKind.Header: BuildHeader(definition, id, a, t); break;
                case RecipeKind.Navigation: BuildNavigation(definition, id, a, t); break;
                case RecipeKind.Metric: BuildMetric(definition, id, a, t); break;
                case RecipeKind.Card: BuildCard(definition, id, a, t); break;
                case RecipeKind.Empty: BuildEmpty(definition, id, a, t); break;
                case RecipeKind.Form: BuildForm(definition, id, a, t); break;
                case RecipeKind.Toolbar: BuildToolbar(definition, id, a, t); break;
                case RecipeKind.Row: BuildRow(definition, id, a, t); break;
                case RecipeKind.Grid: BuildGrid(definition, id, a, t); break;
                default: BuildOverlay(definition, id, a, t); break;
            }

            AddExposedText(definition, "title", "Title", "title", a.Title);
            if (definition.Find("subtitle") != null) AddExposedText(definition, "subtitle", "Subtitle", "subtitle", a.Subtitle);
            if (definition.Find("action") != null) AddExposedText(definition, "actionLabel", "Action Label", "action", a.Action);
            definition.slots.Add(new DesignerComponentSlotDefinition
            {
                slotId = DesignerComponentSlotDefinition.Content,
                displayName = "Content",
                hostElementId = "root",
                allowReorder = true
            });
            definition.variantProperties.Add(new DesignerComponentVariantProperty
            {
                propertyName = "state", displayName = "State", defaultValue = "Normal",
                options = new List<string> { "Normal", "Disabled" }
            });
            definition.variantRules.Add(new DesignerComponentVariantRule
            {
                propertyName = "state", equalsValue = "Disabled",
                overrides = new List<DesignerComponentPropertyOverride>
                {
                    new DesignerComponentPropertyOverride
                    {
                        targetElementId = "root", propertyId = DesignerPropertyId.Opacity,
                        value = new DesignerPropertyValue { type = DesignerPropertyValueType.Float, floatValue = 0.46f }
                    }
                }
            });
            return definition;
        }

        private static void BuildHeader(DesignerComponentDefinitionAsset d, string id, Archetype a, Theme t)
        {
            var h = a.Size.y;
            Add(d, id, "title", "Label", new Rect(18, 13, a.Size.x * 0.5f, 25), a.Title, Color.clear, t.Text, 18);
            Add(d, id, "subtitle", "Label", new Rect(18, 39, a.Size.x * 0.58f, 20), a.Subtitle, Color.clear, t.Muted, 11);
            Add(d, id, "action", "Button", new Rect(a.Size.x - 112, (h - 36) * 0.5f, 94, 36), a.Action, t.Accent, Color.white, 12);
        }

        private static void BuildNavigation(DesignerComponentDefinitionAsset d, string id, Archetype a, Theme t)
        {
            Add(d, id, "title", "Label", new Rect(14, 10, a.Size.x - 28, 24), a.Title, Color.clear, t.Text, 15);
            var vertical = a.Size.y > a.Size.x * 0.55f;
            for (var i = 0; i < 4; i++)
            {
                var rect = vertical
                    ? new Rect(12, 46 + i * 44 * t.Density, a.Size.x - 24, 36)
                    : new Rect(12 + i * ((a.Size.x - 24) / 4f), a.Size.y - 46, (a.Size.x - 32) / 4f, 34);
                Add(d, id, "nav" + i, "Button", rect, new[] { "Home", "Explore", "Activity", "Profile" }[i],
                    i == 0 ? t.Accent : t.Raised, i == 0 ? Color.white : t.Text, 11);
            }
        }

        private static void BuildMetric(DesignerComponentDefinitionAsset d, string id, Archetype a, Theme t)
        {
            Add(d, id, "title", "Label", new Rect(16, 14, a.Size.x - 32, 20), a.Title, Color.clear, t.Muted, 11);
            Add(d, id, "value", "Label", new Rect(16, 40, a.Size.x - 32, 38), "72%", Color.clear, t.Text, 28);
            Add(d, id, "subtitle", "Label", new Rect(16, 81, a.Size.x - 32, 18), a.Subtitle, Color.clear, t.Muted, 10);
            var progress = Add(d, id, "progress", "ProgressBar", new Rect(16, a.Size.y - 28, a.Size.x - 32, 12), string.Empty, t.Raised, t.Accent, 10);
            progress.previewValue = 72f;
        }

        private static void BuildCard(DesignerComponentDefinitionAsset d, string id, Archetype a, Theme t)
        {
            Add(d, id, "image", "Image", new Rect(14, 14, a.Size.x - 28, a.Size.y * 0.38f), string.Empty, t.Raised, t.Accent, 12);
            Add(d, id, "title", "Label", new Rect(16, a.Size.y * 0.45f, a.Size.x - 32, 26), a.Title, Color.clear, t.Text, 17);
            Add(d, id, "subtitle", "Label", new Rect(16, a.Size.y * 0.45f + 29, a.Size.x - 32, 38), a.Subtitle, Color.clear, t.Muted, 11);
            Add(d, id, "action", "Button", new Rect(16, a.Size.y - 48, a.Size.x - 32, 34), a.Action, t.Accent, Color.white, 12);
        }

        private static void BuildEmpty(DesignerComponentDefinitionAsset d, string id, Archetype a, Theme t)
        {
            var cx = a.Size.x * 0.5f;
            var icon = Add(d, id, "image", "Image", new Rect(cx - 28, 22, 56, 56), string.Empty, t.Raised, t.Accent, 12);
            icon.shape = DesignerElementShape.Circle;
            Add(d, id, "title", "Label", new Rect(20, 88, a.Size.x - 40, 25), a.Title, Color.clear, t.Text, 17);
            Add(d, id, "subtitle", "Label", new Rect(20, 116, a.Size.x - 40, 34), a.Subtitle, Color.clear, t.Muted, 11);
            Add(d, id, "action", "Button", new Rect(cx - 62, a.Size.y - 48, 124, 34), a.Action, t.Accent, Color.white, 12);
        }

        private static void BuildForm(DesignerComponentDefinitionAsset d, string id, Archetype a, Theme t)
        {
            Add(d, id, "title", "Label", new Rect(18, 14, a.Size.x - 36, 27), a.Title, Color.clear, t.Text, 19);
            Add(d, id, "subtitle", "Label", new Rect(18, 43, a.Size.x - 36, 24), a.Subtitle, Color.clear, t.Muted, 11);
            Add(d, id, "field1", "TextField", new Rect(18, 78, a.Size.x - 36, 38), "Primary value", t.Raised, t.Text, 11);
            Add(d, id, "field2", "TextField", new Rect(18, 124, a.Size.x - 36, 38), "Secondary value", t.Raised, t.Text, 11);
            Add(d, id, "action", "Button", new Rect(18, a.Size.y - 50, a.Size.x - 36, 36), a.Action, t.Accent, Color.white, 12);
        }

        private static void BuildToolbar(DesignerComponentDefinitionAsset d, string id, Archetype a, Theme t)
        {
            Add(d, id, "title", "Label", new Rect(14, 12, 90, 24), a.Title, Color.clear, t.Text, 14);
            Add(d, id, "search", "TextField", new Rect(108, 9, a.Size.x - 236, 32), a.Subtitle, t.Raised, t.Text, 10);
            Add(d, id, "toggle", "Toggle", new Rect(a.Size.x - 120, 10, 54, 30), "", t.Raised, t.Accent, 10);
            Add(d, id, "action", "Button", new Rect(a.Size.x - 62, 9, 50, 32), a.Action, t.Accent, Color.white, 10);
        }

        private static void BuildRow(DesignerComponentDefinitionAsset d, string id, Archetype a, Theme t)
        {
            var image = Add(d, id, "image", "Image", new Rect(12, 12, 50, 50), string.Empty, t.Raised, t.Accent, 12);
            image.shape = DesignerElementShape.Circle;
            Add(d, id, "title", "Label", new Rect(74, 13, a.Size.x - 178, 22), a.Title, Color.clear, t.Text, 14);
            Add(d, id, "subtitle", "Label", new Rect(74, 37, a.Size.x - 178, 20), a.Subtitle, Color.clear, t.Muted, 10);
            Add(d, id, "action", "Button", new Rect(a.Size.x - 94, 20, 78, 34), a.Action, t.Accent, Color.white, 10);
        }

        private static void BuildGrid(DesignerComponentDefinitionAsset d, string id, Archetype a, Theme t)
        {
            Add(d, id, "title", "Label", new Rect(14, 12, a.Size.x - 28, 25), a.Title, Color.clear, t.Text, 17);
            Add(d, id, "subtitle", "Label", new Rect(14, 38, a.Size.x - 28, 20), a.Subtitle, Color.clear, t.Muted, 10);
            var gap = 10f;
            var cellW = (a.Size.x - 28 - gap) * 0.5f;
            var cellH = (a.Size.y - 78 - gap) * 0.5f;
            for (var i = 0; i < 4; i++)
            {
                var x = 14 + (i % 2) * (cellW + gap);
                var y = 66 + (i / 2) * (cellH + gap);
                Add(d, id, "item" + i, "Panel", new Rect(x, y, cellW, cellH), (i + 1).ToString(),
                    i == 0 ? Color.Lerp(t.Raised, t.Accent, 0.35f) : t.Raised, t.Text, 11);
            }
        }

        private static void BuildOverlay(DesignerComponentDefinitionAsset d, string id, Archetype a, Theme t)
        {
            Add(d, id, "title", "Label", new Rect(20, 18, a.Size.x - 40, 28), a.Title, Color.clear, t.Text, 20);
            Add(d, id, "subtitle", "Label", new Rect(20, 54, a.Size.x - 40, 48), a.Subtitle, Color.clear, t.Muted, 12);
            if (a.Name.Contains("Loading", StringComparison.Ordinal))
            {
                var progress = Add(d, id, "progress", "ProgressBar", new Rect(20, a.Size.y - 62, a.Size.x - 40, 14), string.Empty, t.Raised, t.Accent, 10);
                progress.previewValue = 58f;
                Add(d, id, "action", "Button", new Rect(a.Size.x - 104, a.Size.y - 38, 84, 28), a.Action, t.Raised, t.Text, 10);
            }
            else
            {
                Add(d, id, "secondary", "Button", new Rect(20, a.Size.y - 52, 100, 34), "Cancel", t.Raised, t.Text, 11);
                Add(d, id, "action", "Button", new Rect(a.Size.x - 120, a.Size.y - 52, 100, 34), a.Action, t.Accent, Color.white, 11);
            }
        }

        private static DesignerElementMetadata Add(DesignerComponentDefinitionAsset definition, string recipeId, string localId,
            string type, Rect rect, string text, Color tint, Color textColor, int fontSize)
        {
            var element = Element(recipeId, localId, type, rect, text, tint, textColor, fontSize);
            element.parentId = "root";
            element.siblingIndex = definition.elements.Count;
            definition.elements.Add(element);
            return element;
        }

        private static DesignerElementMetadata Element(string recipeId, string localId, string type, Rect rect,
            string text, Color tint, Color textColor, int fontSize)
            => new DesignerElementMetadata
            {
                stableId = DesignerComponentExpander.DeterministicStableId(recipeId, localId),
                elementId = localId,
                displayName = localId,
                elementType = type,
                rect = rect,
                text = text,
                tint = tint,
                textColor = textColor,
                fontSize = fontSize,
                shape = type == "Button" || type == "TextField" ? DesignerElementShape.Rounded : DesignerElementShape.Rectangle
            };

        private static void AddExposedText(DesignerComponentDefinitionAsset definition, string propertyName,
            string displayName, string targetId, string value)
            => definition.exposedProperties.Add(new DesignerComponentExposedProperty
            {
                propertyName = propertyName,
                displayName = displayName,
                targetElementId = targetId,
                propertyId = DesignerPropertyId.Text,
                defaultValue = new DesignerPropertyValue { type = DesignerPropertyValueType.String, stringValue = value }
            });

        private static IEnumerable<Archetype> Archetypes()
        {
            yield return A("app-header", "App Header", "navigation", RecipeKind.Header, 520, 82, "Workspace", "Project overview and primary actions", "Create", "header", "topbar");
            yield return A("bottom-navigation", "Bottom Navigation", "navigation", RecipeKind.Navigation, 420, 94, "Navigation", "Primary destinations", "Open", "mobile", "tabs");
            yield return A("sidebar-navigation", "Sidebar Navigation", "navigation", RecipeKind.Navigation, 220, 270, "Workspace", "Main navigation", "Open", "desktop", "menu");
            yield return A("tab-strip", "Tab Strip", "navigation", RecipeKind.Navigation, 440, 92, "Sections", "Switch between related views", "Open", "tabs", "segmented");
            yield return A("breadcrumb-bar", "Breadcrumb Bar", "navigation", RecipeKind.Header, 500, 74, "Library / Components", "Current navigation path", "Back", "breadcrumb", "path");

            yield return A("metric-card", "Metric Card", "content", RecipeKind.Metric, 260, 148, "Completion", "Up 12% this week", "Details", "dashboard", "analytics");
            yield return A("feature-card", "Feature Card", "content", RecipeKind.Card, 286, 240, "Featured Content", "A concise description for the highlighted feature.", "Explore", "marketing", "feature");
            yield return A("profile-card", "Profile Card", "content", RecipeKind.Card, 286, 240, "Player Profile", "Status, role, and recent activity.", "View Profile", "profile", "identity");
            yield return A("article-card", "Article Card", "content", RecipeKind.Card, 300, 250, "Design Systems", "Build consistent interfaces with reusable foundations.", "Read More", "article", "news");
            yield return A("empty-state", "Empty State", "content", RecipeKind.Empty, 320, 210, "Nothing here yet", "Create the first item to get started.", "Create Item", "empty", "placeholder");

            yield return A("login-form", "Login Form", "forms", RecipeKind.Form, 340, 230, "Welcome Back", "Sign in to continue", "Sign In", "auth", "account");
            yield return A("search-panel", "Search Panel", "forms", RecipeKind.Form, 380, 230, "Advanced Search", "Narrow results with multiple fields", "Search", "query", "filter");
            yield return A("settings-row", "Settings Row", "forms", RecipeKind.Row, 430, 76, "Notifications", "Receive important product updates", "Edit", "settings", "preference");
            yield return A("filter-toolbar", "Filter Toolbar", "forms", RecipeKind.Toolbar, 540, 52, "Filters", "Search items", "Apply", "toolbar", "filter");
            yield return A("feedback-form", "Feedback Form", "forms", RecipeKind.Form, 360, 250, "Send Feedback", "Tell us what could be improved", "Submit", "feedback", "survey");

            yield return A("product-card", "Product Card", "commerce", RecipeKind.Card, 286, 250, "Premium Pack", "Everything needed to accelerate your workflow.", "Add to Cart", "shop", "product");
            yield return A("cart-summary", "Cart Summary", "commerce", RecipeKind.Metric, 300, 160, "Order Total", "3 items · taxes calculated", "Checkout", "cart", "checkout");
            yield return A("pricing-card", "Pricing Card", "commerce", RecipeKind.Card, 300, 260, "Pro Plan", "Advanced controls for production teams.", "Choose Plan", "pricing", "subscription");

            yield return A("inventory-panel", "Inventory Panel", "game", RecipeKind.Grid, 360, 300, "Inventory", "4 of 24 slots shown", "Manage", "inventory", "items");
            yield return A("quest-tracker", "Quest Tracker", "game", RecipeKind.Metric, 300, 168, "Into the Wild", "Objectives completed", "Open Journal", "quest", "objective");
            yield return A("party-member", "Party Member Row", "game", RecipeKind.Row, 400, 78, "Ranger", "Level 24 · Ready", "Inspect", "party", "character");
            yield return A("achievement-card", "Achievement Card", "game", RecipeKind.Card, 300, 230, "Pathfinder", "Discover every region on the world map.", "Claim", "achievement", "reward");

            yield return A("notification-toast", "Notification Toast", "feedback", RecipeKind.Row, 420, 78, "Changes Saved", "Your updates are now live.", "Undo", "toast", "notification");
            yield return A("modal-dialog", "Modal Dialog", "feedback", RecipeKind.Overlay, 360, 210, "Confirm Action", "Review the details before continuing.", "Confirm", "modal", "dialog");
            yield return A("loading-panel", "Loading Panel", "feedback", RecipeKind.Overlay, 360, 190, "Loading Content", "Preparing the next view…", "Cancel", "loading", "progress");
        }

        private static Archetype A(string slug, string name, string category, RecipeKind kind, float width, float height,
            string title, string subtitle, string action, params string[] tags)
            => new Archetype
            {
                Slug = slug, Name = name, Key = "builtinRecipe.archetype." + SlugKey(slug),
                Category = char.ToUpperInvariant(category[0]) + category.Substring(1),
                CategoryKey = "builtinRecipe.category." + category,
                Kind = kind, Size = new Vector2(width, height), Title = title, Subtitle = subtitle, Action = action, Tags = tags
            };

        private static IEnumerable<Theme> Themes()
        {
            yield return T("midnight", "Midnight", "171A24", "242938", "7C6FF2", "F1F3FA", "9DA5B8", .94f);
            yield return T("slate", "Slate", "20252D", "303844", "7193C8", "EEF2F7", "AAB4C2", 1f);
            yield return T("ocean", "Ocean", "102A3A", "173D52", "2AA9E0", "E9F7FD", "91BDCF", 1f);
            yield return T("forest", "Forest", "172D25", "234337", "3DBA78", "EDFAF4", "9AC6B0", 1.02f);
            yield return T("ember", "Ember", "382119", "512E22", "F07843", "FFF1EB", "D6A995", .96f);
            yield return T("violet", "Violet", "281D3B", "392752", "9B72F2", "F7F0FF", "BAA5D2", 1.04f);
            yield return T("rose", "Rose", "3A202D", "512D3F", "E96A9D", "FFF0F6", "D2A1B5", 1f);
            yield return T("gold", "Gold", "302817", "463A20", "D6A83E", "FFF8E5", "C8B681", 1.06f);
            yield return T("mono-dark", "Mono Dark", "191919", "2B2B2B", "AFAFAF", "F4F4F4", "AAAAAA", .90f);
            yield return T("mono-light", "Mono Light", "E9EBEF", "F8F9FB", "4F647C", "202630", "697483", 1f);
            yield return T("glass", "Glass", "20293A", "344057", "63C8FF", "F3FAFF", "A9C1D2", 1.08f);
            yield return T("contrast", "High Contrast", "080A0E", "171B22", "F4C542", "FFFFFF", "D5D8DE", 1f);
        }

        private static Theme T(string slug, string name, string surface, string raised, string accent,
            string text, string muted, float density)
            => new Theme
            {
                Slug = slug, Name = name, Key = "builtinRecipe.theme." + SlugKey(slug),
                Surface = Hex(surface), Raised = Hex(raised), Accent = Hex(accent), Text = Hex(text), Muted = Hex(muted), Density = density
            };

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var color);
            return color;
        }

        private static string SlugKey(string slug)
        {
            var parts = slug.Split('-');
            var result = parts[0];
            for (var i = 1; i < parts.Length; i++)
                result += char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            return result;
        }
    }
}
