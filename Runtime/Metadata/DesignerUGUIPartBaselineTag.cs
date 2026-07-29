using UnityEngine;

namespace emiteat.NexUI.Designer
{
    /// <summary>
    /// Hidden baseline captured on a generated uGUI internal part. It makes sparse Designer part
    /// offsets idempotent across repeated prefab saves and lets Reset restore the original Unity
    /// control transform instead of accumulating deltas.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class DesignerUGUIPartBaselineTag : MonoBehaviour
    {
        [HideInInspector] public string ownerStableId;
        [HideInInspector] public string partId;
        [HideInInspector] public Vector2 anchoredPosition;
        [HideInInspector] public Vector2 sizeDelta;
        [HideInInspector] public Vector3 localEulerAngles;
        [HideInInspector] public Vector3 localScale = Vector3.one;
        [HideInInspector] public bool activeSelf = true;

        public void Capture(RectTransform target, string ownerId, string id)
        {
            ownerStableId = ownerId;
            partId = id;
            anchoredPosition = target.anchoredPosition;
            sizeDelta = target.sizeDelta;
            localEulerAngles = target.localEulerAngles;
            localScale = target.localScale;
            activeSelf = target.gameObject.activeSelf;
        }

        public void Restore(RectTransform target)
        {
            target.anchoredPosition = anchoredPosition;
            target.sizeDelta = sizeDelta;
            target.localEulerAngles = localEulerAngles;
            target.localScale = localScale;
            target.gameObject.SetActive(activeSelf);
        }
    }
}
