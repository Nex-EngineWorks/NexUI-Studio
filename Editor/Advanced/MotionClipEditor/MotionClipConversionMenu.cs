using emiteat.NexUI.MotionClip;
using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.MotionClipEditor
{
    /// <summary>Asset menu entry points for standard AnimationClip interoperability.</summary>
    public static class MotionClipConversionMenu
    {
        [MenuItem("Assets/NexUI/Convert AnimationClip To Motion Clip", priority = 2010)]
        public static void ImportSelected()
        {
            var source = Selection.activeObject as AnimationClip;
            if (source == null) return;
            var result = UIMotionClipImporter.Import(source);
            var sourcePath = AssetDatabase.GetAssetPath(source);
            var path = AssetDatabase.GenerateUniqueAssetPath(System.IO.Path.ChangeExtension(sourcePath, null) + ".Motion.asset");
            AssetDatabase.CreateAsset(result, path);
            Undo.RegisterCreatedObjectUndo(result, "Import Animation Clip as Motion Clip");
            AssetDatabase.SaveAssetIfDirty(result);
            Selection.activeObject = result;
            EditorGUIUtility.PingObject(result);
        }

        [MenuItem("Assets/NexUI/Convert AnimationClip To Motion Clip", true)]
        private static bool CanImportSelected() => Selection.activeObject is AnimationClip;

        [MenuItem("Assets/NexUI/Export Motion Clip To AnimationClip", priority = 2011)]
        public static void ExportSelected()
        {
            var source = Selection.activeObject as UIMotionClip;
            if (source == null) return;
            Export(source);
        }

        [MenuItem("Assets/NexUI/Export Motion Clip To AnimationClip", true)]
        private static bool CanExportSelected() => Selection.activeObject is UIMotionClip;

        public static AnimationClip Export(UIMotionClip source)
        {
            var result = UIMotionClipExporter.Export(source);
            var sourcePath = AssetDatabase.GetAssetPath(source);
            var path = AssetDatabase.GenerateUniqueAssetPath(System.IO.Path.ChangeExtension(sourcePath, null) + ".anim");
            AssetDatabase.CreateAsset(result, path);
            Undo.RegisterCreatedObjectUndo(result, "Export Motion Clip as Animation Clip");
            AssetDatabase.SaveAssetIfDirty(result);
            Selection.activeObject = result;
            EditorGUIUtility.PingObject(result);
            return result;
        }
    }
}
