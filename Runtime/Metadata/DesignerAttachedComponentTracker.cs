using System.Collections.Generic;
using UnityEngine;

namespace emiteat.NexUI.Designer
{
    /// <summary>
    /// Tracks only MonoBehaviours created by the Designer's Add Component flow. This lets a later
    /// save remove a Designer-managed attachment without deleting a same-type component authored
    /// manually by the user on the prefab.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class DesignerAttachedComponentTracker : MonoBehaviour
    {
        [SerializeField, HideInInspector]
        public List<Component> managedComponents = new List<Component>();
    }
}
