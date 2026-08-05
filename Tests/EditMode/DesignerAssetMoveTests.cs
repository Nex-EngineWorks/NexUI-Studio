using emiteat.NexUI.Designer.Editor.UI.Panels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// The Assets panel's Move against a real project folder tree.
    /// </summary>
    /// <remarks>
    /// The pure rules are covered in <see cref="DesignerAssetBrowserTests"/>. What can only be checked
    /// here is that the rules and <see cref="AssetDatabase"/> agree: that a name collision produces a
    /// second file rather than a failed move, and that a refused source leaves the project exactly as
    /// it was instead of half-moving the batch.
    /// </remarks>
    public sealed class DesignerAssetMoveTests
    {
        private const string Root = "Assets/NexUIAssetMoveTests";
        private const string Source = Root + "/Source";
        private const string Target = Root + "/Target";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Root))
                AssetDatabase.CreateFolder("Assets", "NexUIAssetMoveTests");
            if (!AssetDatabase.IsValidFolder(Source)) AssetDatabase.CreateFolder(Root, "Source");
            if (!AssetDatabase.IsValidFolder(Target)) AssetDatabase.CreateFolder(Root, "Target");
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(Root);

        private static string CreateAsset(string folder, string name)
        {
            var path = folder + "/" + name + ".asset";
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<DesignerMetadataAsset>(), path);
            return path;
        }

        [Test]
        public void MovesAnAssetAndReportsIt()
        {
            var path = CreateAsset(Source, "card");

            var result = DesignerAssetBrowser.Move(new[] { path }, Target);

            Assert.AreEqual(1, result.Moved.Count, result.Summary());
            CollectionAssert.IsEmpty(result.Failed);
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<Object>(path));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Object>(Target + "/card.asset"));
        }

        /// <summary>
        /// The Project window renames rather than refusing, and so does this - a move that fails
        /// because the destination happens to hold the same name is not a useful outcome.
        /// </summary>
        [Test]
        public void ANameCollisionProducesAUniqueNameInsteadOfAFailure()
        {
            var path = CreateAsset(Source, "card");
            CreateAsset(Target, "card");

            var result = DesignerAssetBrowser.Move(new[] { path }, Target);

            Assert.AreEqual(1, result.Moved.Count, result.Summary());
            CollectionAssert.IsEmpty(result.Failed);
            Assert.AreNotEqual(Target + "/card.asset", result.Moved[0]);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Object>(Target + "/card.asset"),
                "the asset that was already there must survive");
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Object>(result.Moved[0]));
        }

        [Test]
        public void MovingIntoTheFolderItIsAlreadyInIsSkippedNotFailed()
        {
            var path = CreateAsset(Source, "card");

            var result = DesignerAssetBrowser.Move(new[] { path }, Source);

            CollectionAssert.IsEmpty(result.Moved);
            CollectionAssert.IsEmpty(result.Failed);
            Assert.AreEqual(1, result.Skipped.Count);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Object>(path));
        }

        [Test]
        public void AFolderCannotBeMovedIntoItsOwnDescendant()
        {
            var nested = Source + "/Nested";
            AssetDatabase.CreateFolder(Source, "Nested");

            var result = DesignerAssetBrowser.Move(new[] { Source }, nested);

            CollectionAssert.IsEmpty(result.Moved);
            Assert.AreEqual(1, result.Skipped.Count);
            Assert.IsTrue(AssetDatabase.IsValidFolder(nested), "the tree must be untouched");
        }

        /// <summary>Selecting a folder and its contents moves the folder once, with everything inside it.</summary>
        [Test]
        public void ANestedSelectionMovesTheFolderOnce()
        {
            var inner = Source + "/Inner";
            AssetDatabase.CreateFolder(Source, "Inner");
            var asset = CreateAsset(inner, "card");

            var result = DesignerAssetBrowser.Move(new[] { inner, asset }, Target);

            Assert.AreEqual(1, result.Moved.Count, result.Summary());
            Assert.IsTrue(AssetDatabase.IsValidFolder(Target + "/Inner"));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Object>(Target + "/Inner/card.asset"),
                "the child rides along inside the folder rather than being moved separately");
            Assert.IsFalse(AssetDatabase.IsValidFolder(inner));
        }

        [Test]
        public void AnInvalidDestinationFailsEverySourceAndMovesNothing()
        {
            var path = CreateAsset(Source, "card");

            var result = DesignerAssetBrowser.Move(new[] { path }, "Assets/DoesNotExist");

            CollectionAssert.IsEmpty(result.Moved);
            Assert.AreEqual(1, result.Failed.Count);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Object>(path));
        }
    }
}
