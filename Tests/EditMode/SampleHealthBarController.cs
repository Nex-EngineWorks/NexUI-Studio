using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    public sealed class SampleAdvancedSerializationController : MonoBehaviour
    {
        [SerializeReference] private SampleManagedRule rule = new SampleThresholdRule
            { label = "critical", threshold = 0.25f };

        public SampleManagedRule Rule => rule;
    }
}
