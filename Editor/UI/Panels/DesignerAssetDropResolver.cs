using UnityEngine;
using Object = UnityEngine.Object;

namespace emiteat.NexUI.Designer.Editor.UI.Panels
{
    /// <summary>What dropping an asset on the Designer canvas should do.</summary>
    public enum DesignerAssetDropAction
    {
        /// <summary>The payload means nothing here; the drop is rejected rather than guessed at.</summary>
        None,
        /// <summary>Assign the sprite to the element under the cursor.</summary>
        SetSprite,
        /// <summary>Assign the font to the element under the cursor.</summary>
        SetFont,
        /// <summary>Assign the material to the element under the cursor.</summary>
        SetMaterial,
        /// <summary>Create a new Image element at the drop point.</summary>
        CreateImage,
        /// <summary>Place an instance of a reusable component definition at the drop point.</summary>
        PlaceComponent
    }

    /// <summary>
    /// Decides what a dragged asset does when it lands on the canvas, given what is under the cursor.
    ///
    /// Split out from the viewport and kept free of UI/Undo/AssetDatabase so the rules are
    /// unit-testable and stated in exactly one place. The viewport asks this, then performs the
    /// action; nothing here mutates anything.
    /// </summary>
    public static class DesignerAssetDropResolver
    {
        /// <summary>
        /// Resolves the action for <paramref name="payload"/> dropped on <paramref name="target"/>
        /// (null ⇒ empty canvas). Returns <see cref="DesignerAssetDropAction.None"/> for anything the
        /// Designer has no defined behaviour for - a rejected drop is better than a surprising edit.
        /// </summary>
        public static DesignerAssetDropAction Resolve(Object payload, DesignerElementMetadata target)
        {
            if (payload == null) return DesignerAssetDropAction.None;

            // A component definition always places an instance - it describes a whole sub-tree, so
            // "assign it to the hovered element" would have no meaning.
            if (payload is DesignerComponentDefinitionAsset)
                return DesignerAssetDropAction.PlaceComponent;

            if (payload is Sprite)
                return target != null ? DesignerAssetDropAction.SetSprite : DesignerAssetDropAction.CreateImage;

            // A Texture2D dropped from the browser is the importer's main asset; the Designer stores
            // Sprites, so this only works when the texture actually has a sprite sub-asset. The
            // viewport resolves that, and falls back to None when it does not.
            if (payload is Texture2D)
                return target != null ? DesignerAssetDropAction.SetSprite : DesignerAssetDropAction.CreateImage;

            if (payload is Material)
                return target != null ? DesignerAssetDropAction.SetMaterial : DesignerAssetDropAction.None;

            if (IsFont(payload))
                return target != null ? DesignerAssetDropAction.SetFont : DesignerAssetDropAction.None;

            return DesignerAssetDropAction.None;
        }

        /// <summary>
        /// True for a Unity <see cref="Font"/> or a TextMeshPro font asset. TMP is referenced by name
        /// so this file does not need a hard dependency on the TMP assembly.
        /// </summary>
        public static bool IsFont(Object payload)
        {
            if (payload is Font) return true;
            if (payload == null) return false;
            var typeName = payload.GetType().FullName;
            return typeName == "TMPro.TMP_FontAsset";
        }

        /// <summary>Short, human sentence describing what the drop will do. Shown as canvas feedback.</summary>
        public static string Describe(DesignerAssetDropAction action, Object payload, DesignerElementMetadata target)
        {
            var name = payload != null ? payload.name : "asset";
            switch (action)
            {
                case DesignerAssetDropAction.SetSprite:   return $"Set '{name}' as {Target(target)} image";
                case DesignerAssetDropAction.SetFont:     return $"Set '{name}' as {Target(target)} font";
                case DesignerAssetDropAction.SetMaterial: return $"Set '{name}' as {Target(target)} material";
                case DesignerAssetDropAction.CreateImage: return $"Create an Image from '{name}'";
                case DesignerAssetDropAction.PlaceComponent: return $"Place a '{name}' component instance";
                default: return null;
            }
        }

        private static string Target(DesignerElementMetadata target)
            => target != null ? "'" + target.elementId + "'" : "the element";
    }
}
