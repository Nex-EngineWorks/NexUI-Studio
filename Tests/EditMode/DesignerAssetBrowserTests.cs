using emiteat.NexUI.Designer.Editor.UI.Panels;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// Pure rules behind the Designer's in-window asset browser: how a path is classified, how the
    /// search/kind filter behaves, how folder navigation resolves, and what dropping an asset on the
    /// canvas is allowed to do. No AssetDatabase, no window.
    /// </summary>
    public sealed class DesignerAssetBrowserTests
    {
        // ---- Classification ---------------------------------------------------------------

        [TestCase("Assets/UI/icon.png", DesignerAssetKind.Image)]
        [TestCase("Assets/UI/icon.PNG", DesignerAssetKind.Image)]
        [TestCase("Assets/UI/art.psd", DesignerAssetKind.Image)]
        [TestCase("Assets/Fonts/Roboto.ttf", DesignerAssetKind.Font)]
        [TestCase("Assets/M/Glass.mat", DesignerAssetKind.Material)]
        [TestCase("Assets/P/Button.prefab", DesignerAssetKind.Prefab)]
        [TestCase("Assets/UI/Screen.uxml", DesignerAssetKind.Uxml)]
        [TestCase("Assets/UI/Screen.uss", DesignerAssetKind.Uss)]
        [TestCase("Assets/Data/Card.asset", DesignerAssetKind.ScriptableObject)]
        [TestCase("Assets/Scenes/Main.unity", DesignerAssetKind.Scene)]
        [TestCase("Assets/README.md", DesignerAssetKind.Other)]
        [TestCase("Assets/NoExtension", DesignerAssetKind.Other)]
        [TestCase("", DesignerAssetKind.Other)]
        [TestCase(null, DesignerAssetKind.Other)]
        public void KindOf_ClassifiesByExtensionCaseInsensitively(string path, DesignerAssetKind expected)
            => Assert.AreEqual(expected, DesignerAssetBrowser.KindOf(path));

        // ---- Filtering --------------------------------------------------------------------

        private static DesignerAssetEntry Asset(string name, DesignerAssetKind kind)
            => new DesignerAssetEntry { Name = name, Path = "Assets/" + name, Kind = kind };

        private static DesignerAssetEntry Folder(string name)
            => new DesignerAssetEntry { Name = name, Path = "Assets/" + name, IsFolder = true, Kind = DesignerAssetKind.Folder };

        [Test]
        public void Matches_AllFilterAcceptsEveryKind()
        {
            Assert.IsTrue(DesignerAssetBrowser.Matches(Asset("a.png", DesignerAssetKind.Image), null, DesignerAssetKind.Other));
            Assert.IsTrue(DesignerAssetBrowser.Matches(Asset("b.mat", DesignerAssetKind.Material), null, DesignerAssetKind.Other));
        }

        [Test]
        public void Matches_KindFilterExcludesOtherKindsButNeverFolders()
        {
            Assert.IsTrue(DesignerAssetBrowser.Matches(Asset("a.png", DesignerAssetKind.Image), null, DesignerAssetKind.Image));
            Assert.IsFalse(DesignerAssetBrowser.Matches(Asset("b.mat", DesignerAssetKind.Material), null, DesignerAssetKind.Image));
            Assert.IsTrue(DesignerAssetBrowser.Matches(Folder("Icons"), null, DesignerAssetKind.Image),
                "Folders must stay visible or the assets inside them become unreachable.");
        }

        [Test]
        public void Matches_SearchIsCaseInsensitiveAndAppliesToFoldersToo()
        {
            Assert.IsTrue(DesignerAssetBrowser.Matches(Asset("HeroIcon.png", DesignerAssetKind.Image), "heroicon", DesignerAssetKind.Other));
            Assert.IsTrue(DesignerAssetBrowser.Matches(Asset("HeroIcon.png", DesignerAssetKind.Image), "  Icon ", DesignerAssetKind.Other));
            Assert.IsFalse(DesignerAssetBrowser.Matches(Asset("HeroIcon.png", DesignerAssetKind.Image), "zzz", DesignerAssetKind.Other));
            Assert.IsFalse(DesignerAssetBrowser.Matches(Folder("Icons"), "zzz", DesignerAssetKind.Other));
        }

        [Test]
        public void Matches_NullEntryIsRejected()
            => Assert.IsFalse(DesignerAssetBrowser.Matches(null, null, DesignerAssetKind.Other));

        // ---- Navigation -------------------------------------------------------------------

        [TestCase("Assets/UI/Icons", "Assets/UI")]
        [TestCase("Assets/UI", "Assets")]
        [TestCase("Assets", "Assets")]
        [TestCase("", "Assets")]
        [TestCase(null, "Assets")]
        public void ParentFolder_ClampsAtAssetsRoot(string folder, string expected)
            => Assert.AreEqual(expected, DesignerAssetBrowser.ParentFolder(folder));

        [Test]
        public void ParentFolder_NormalizesSeparatorsAndTrailingSlash()
        {
            Assert.AreEqual("Assets/UI", DesignerAssetBrowser.ParentFolder("Assets\\UI\\Icons"));
            Assert.AreEqual("Assets/UI", DesignerAssetBrowser.ParentFolder("Assets/UI/Icons/"));
        }

        [Test]
        public void Breadcrumbs_AreCumulativePaths()
        {
            var crumbs = DesignerAssetBrowser.Breadcrumbs("Assets/UI/Icons");
            Assert.AreEqual(3, crumbs.Count);
            Assert.AreEqual("Assets", crumbs[0]);
            Assert.AreEqual("Assets/UI", crumbs[1]);
            Assert.AreEqual("Assets/UI/Icons", crumbs[2]);
        }

        [Test]
        public void Breadcrumbs_EmptyPathYieldsNothing()
            => Assert.AreEqual(0, DesignerAssetBrowser.Breadcrumbs("").Count);

        [TestCase("Assets/UI/Icons", "Icons")]
        [TestCase("Assets", "Assets")]
        [TestCase("", "")]
        public void LeafName_ReturnsLastSegment(string path, string expected)
            => Assert.AreEqual(expected, DesignerAssetBrowser.LeafName(path));

        [Test]
        public void FilterKinds_StartWithTheAllSlot()
        {
            Assert.AreEqual(DesignerAssetKind.Other, DesignerAssetBrowser.FilterKinds[0]);
            Assert.AreEqual("All", DesignerAssetBrowser.FilterLabel(DesignerAssetKind.Other));
        }

        // ---- Drop resolution --------------------------------------------------------------

        private static DesignerElementMetadata Element(string id = "panel")
            => new DesignerElementMetadata { elementId = id };

        [Test]
        public void Drop_SpriteOnElementSetsSprite_OnEmptyCanvasCreatesImage()
        {
            var sprite = MakeSprite();
            try
            {
                Assert.AreEqual(DesignerAssetDropAction.SetSprite, DesignerAssetDropResolver.Resolve(sprite, Element()));
                Assert.AreEqual(DesignerAssetDropAction.CreateImage, DesignerAssetDropResolver.Resolve(sprite, null));
            }
            finally { Cleanup(sprite); }
        }

        [Test]
        public void Drop_MaterialNeedsATargetElement()
        {
            var material = new Material(Shader.Find("UI/Default"));
            try
            {
                Assert.AreEqual(DesignerAssetDropAction.SetMaterial, DesignerAssetDropResolver.Resolve(material, Element()));
                Assert.AreEqual(DesignerAssetDropAction.None, DesignerAssetDropResolver.Resolve(material, null),
                    "A material has no meaning dropped on empty canvas, so the drop is rejected rather than guessed.");
            }
            finally { Object.DestroyImmediate(material); }
        }

        [Test]
        public void Drop_ComponentDefinitionAlwaysPlacesAnInstance()
        {
            var definition = ScriptableObject.CreateInstance<DesignerComponentDefinitionAsset>();
            try
            {
                Assert.AreEqual(DesignerAssetDropAction.PlaceComponent, DesignerAssetDropResolver.Resolve(definition, null));
                Assert.AreEqual(DesignerAssetDropAction.PlaceComponent, DesignerAssetDropResolver.Resolve(definition, Element()),
                    "A definition describes a whole sub-tree; assigning it to the hovered element would be meaningless.");
            }
            finally { Object.DestroyImmediate(definition); }
        }

        [Test]
        public void Drop_UnknownPayloadIsRejected()
        {
            var unrelated = ScriptableObject.CreateInstance<ScriptableObject>();
            try
            {
                Assert.AreEqual(DesignerAssetDropAction.None, DesignerAssetDropResolver.Resolve(unrelated, Element()));
                Assert.AreEqual(DesignerAssetDropAction.None, DesignerAssetDropResolver.Resolve(null, Element()));
            }
            finally { Object.DestroyImmediate(unrelated); }
        }

        [Test]
        public void Drop_DescribeExplainsTheActionAndNamesTheTarget()
        {
            var sprite = MakeSprite();
            try
            {
                var text = DesignerAssetDropResolver.Describe(DesignerAssetDropAction.SetSprite, sprite, Element("heroIcon"));
                StringAssert.Contains("heroIcon", text);
                Assert.IsNull(DesignerAssetDropResolver.Describe(DesignerAssetDropAction.None, sprite, null));
            }
            finally { Cleanup(sprite); }
        }

        // ---- Move rules -------------------------------------------------------------------

        [TestCase("Assets/UI/icon.png", "Assets/UI", true)]
        [TestCase("Assets/UI/Icons/icon.png", "Assets/UI", true)]
        [TestCase("Assets/UI", "Assets/UI", true)]
        [TestCase("Assets/UIKit/icon.png", "Assets/UI", false)]
        [TestCase("Assets/Art/icon.png", "Assets/UI", false)]
        [TestCase("Assets/UI/icon.png", "", false)]
        public void IsUnder_TreatsOnlyWholeSegmentsAsContainment(string path, string folder, bool expected)
            => Assert.AreEqual(expected, DesignerAssetBrowser.IsUnder(path, folder));

        [Test]
        public void MoveBlockedReason_RefusesAFolderIntoItselfOrItsOwnChild()
        {
            Assert.IsNotNull(DesignerAssetBrowser.MoveBlockedReason("Assets/UI", "Assets/UI"));
            Assert.IsNotNull(DesignerAssetBrowser.MoveBlockedReason("Assets/UI", "Assets/UI/Icons"),
                "moving a folder into its own descendant is what would corrupt the tree");
        }

        [Test]
        public void MoveBlockedReason_RefusesAMoveThatWouldChangeNothing()
            => Assert.IsNotNull(DesignerAssetBrowser.MoveBlockedReason("Assets/UI/icon.png", "Assets/UI"));

        [Test]
        public void MoveBlockedReason_AllowsARealMove()
        {
            Assert.IsNull(DesignerAssetBrowser.MoveBlockedReason("Assets/UI/icon.png", "Assets/Art"));
            Assert.IsNull(DesignerAssetBrowser.MoveBlockedReason("Assets/UI", "Assets/Art"));
            Assert.IsNull(DesignerAssetBrowser.MoveBlockedReason("Assets/UI", "Assets/UIKit"),
                "a sibling with a shared name prefix is not a descendant");
        }

        /// <summary>
        /// Rubber-band selecting a folder and something inside it is easy. Moving both would move the
        /// child out of the folder that was being moved as a whole.
        /// </summary>
        [Test]
        public void WithoutNestedSources_KeepsOnlyTheOutermostSelection()
        {
            var kept = DesignerAssetBrowser.WithoutNestedSources(new[]
            {
                "Assets/UI", "Assets/UI/Icons", "Assets/UI/Icons/a.png", "Assets/Art/b.png"
            });

            CollectionAssert.AreEquivalent(new[] { "Assets/UI", "Assets/Art/b.png" }, kept);
        }

        [Test]
        public void WithoutNestedSources_KeepsSiblingsAndDeduplicates()
        {
            var kept = DesignerAssetBrowser.WithoutNestedSources(new[]
            {
                "Assets/UI/a.png", "Assets/UI/a.png", "Assets/UI/b.png"
            });

            CollectionAssert.AreEqual(new[] { "Assets/UI/a.png", "Assets/UI/b.png" }, kept);
        }

        private static Sprite MakeSprite()
        {
            var texture = new Texture2D(4, 4);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f));
            sprite.name = "heroSprite";
            return sprite;
        }

        private static void Cleanup(Sprite sprite)
        {
            if (sprite == null) return;
            var texture = sprite.texture;
            Object.DestroyImmediate(sprite);
            if (texture != null) Object.DestroyImmediate(texture);
        }
    }
}
