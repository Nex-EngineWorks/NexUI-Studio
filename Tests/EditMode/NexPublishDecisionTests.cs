using System.Collections.Generic;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Designer.Editor.Compiler;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// Covers when a compile is allowed to skip writing to disk.
    /// </summary>
    /// <remarks>
    /// This is the one place where being wrong is expensive in a way tests can catch: skipping a
    /// write that was needed ships stale UI, and the mistake is invisible until someone wonders
    /// why their edit did nothing. The decision is a pure function of two programs precisely so
    /// it can be asserted here without an AssetDatabase, a project folder or an importer.
    /// </remarks>
    public sealed class NexPublishDecisionTests
    {
        private readonly List<NexScreenProgram> _created = new List<NexScreenProgram>();

        [TearDown]
        public void TearDown()
        {
            foreach (var program in _created)
                if (program != null) Object.DestroyImmediate(program);
            _created.Clear();
        }

        private NexScreenProgram Program(string hash, string screenId = "TestScreen")
        {
            var program = ScriptableObject.CreateInstance<NexScreenProgram>();
            program.Initialize(screenId, new NexNodeProgram[0], new NexSourceMap(),
                new NexFeatureManifest(), new Vector2(1920f, 1080f), hash);
            _created.Add(program);
            return program;
        }

        [Test]
        public void Decide_WritesWhenNothingIsPublishedYet()
        {
            var decision = NexScreenPublisher.Decide(null, Program("abc123"));

            Assert.IsTrue(decision.ShouldWrite);
            StringAssert.Contains("no previously published", decision.Reason);
        }

        [Test]
        public void Decide_SkipsWhenTheContentHashIsUnchanged()
        {
            var decision = NexScreenPublisher.Decide(Program("abc123"), Program("abc123"));

            Assert.IsFalse(decision.ShouldWrite);
            StringAssert.Contains("unchanged", decision.Reason);
        }

        [Test]
        public void Decide_WritesWhenTheContentHashDiffers()
        {
            var decision = NexScreenPublisher.Decide(Program("abc123"), Program("def456"));

            Assert.IsTrue(decision.ShouldWrite);
            StringAssert.Contains("content changed", decision.Reason);
        }

        [Test]
        public void Decide_WritesWhenThePublishedScreenHasNoHash()
        {
            // A program from before hashing existed, or one an importer half-wrote. Trusting an
            // empty hash as "matching" would strand that screen permanently out of date.
            var decision = NexScreenPublisher.Decide(Program(string.Empty), Program(string.Empty));

            Assert.IsTrue(decision.ShouldWrite);
        }

        [Test]
        public void Decide_WritesWhenTheCompilerVersionChanged()
        {
            var existing = Program("abc123");
            SetCompilerVersion(existing, NexScreenProgram.CurrentCompilerVersion - 1);

            var decision = NexScreenPublisher.Decide(existing, Program("abc123"));

            Assert.IsTrue(decision.ShouldWrite,
                "The same hash from a different compiler is not the same output.");
            StringAssert.Contains("compiler version", decision.Reason);
        }

        [Test]
        public void Decide_WritesWhenThereIsNothingToCompareAgainst()
        {
            Assert.IsTrue(NexScreenPublisher.Decide(Program("abc123"), null).ShouldWrite);
        }

        private static void SetCompilerVersion(NexScreenProgram program, int version)
        {
            typeof(NexScreenProgram)
                .GetField("_compilerVersion", System.Reflection.BindingFlags.NonPublic |
                                              System.Reflection.BindingFlags.Instance)
                .SetValue(program, version);
        }
    }
}
