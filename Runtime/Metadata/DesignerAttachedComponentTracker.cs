using System.Collections.Generic;
using UnityEngine;

namespace emiteat.NexUI.Designer
{
    /// <summary>
    /// Tracks components created by the Designer. Separate lists keep explicit Add Component
    /// attachments distinct from optional serializer helpers such as Outline or LayoutGroup, so
    /// a later save never removes a same-type component authored manually by the user.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class DesignerAttachedComponentTracker : MonoBehaviour
    {
        [SerializeField, HideInInspector]
        public List<Component> managedComponents = new List<Component>();

        [SerializeField, HideInInspector]
        public List<Component> managedGeneratedComponents = new List<Component>();
    }
}
