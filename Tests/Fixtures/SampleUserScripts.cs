using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The namespace deliberately still says EditMode: these fixtures belong to the edit-mode tests and
// are only in a separate assembly because Unity refuses to AddComponent a MonoBehaviour that lives
// in an Editor-platform assembly ("Can't add script behaviour ... because it is an editor script").
// Keeping the namespace means the tests that use them need no using directive and no edit.
namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// The reference user script for the universal component system: two element references and one
    /// constrained number, which is the shape almost every gameplay UI behaviour has.
    /// </summary>
    /// <remarks>
    /// Nothing here knows about NexUI. That is the point - if this can be added, wired and saved from
    /// the Studio without a single line of Studio-side support code, so can any script in a user's
    /// project.
    /// </remarks>
    public sealed class SampleHealthBarController : MonoBehaviour
    {
        [SerializeField] private Image fill;
        [SerializeField] private TMP_Text label;
        [SerializeField, Min(0f)] private float smoothTime = 0.1f;

        public Image Fill => fill;
        public TMP_Text Label => label;
        public float SmoothTime => smoothTime;
    }

    [Serializable]
    public abstract class SampleManagedRule
    {
        public string label;
    }

    [Serializable]
    public sealed class SampleThresholdRule : SampleManagedRule
    {
        public float threshold;
    }

    /// <summary>Covers <c>[SerializeReference]</c>, which serializes differently from a plain field.</summary>
    public sealed class SampleAdvancedSerializationController : MonoBehaviour
    {
        [SerializeReference] private SampleManagedRule rule = new SampleThresholdRule
            { label = "critical", threshold = 0.25f };

        public SampleManagedRule Rule => rule;
    }
}
