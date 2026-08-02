using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEditor.VersionControl;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Serialization
{
    public readonly struct GeneratedAssetFile
    {
        public readonly string Path;
        public readonly string Content;
        public GeneratedAssetFile(string path, string content) { Path = path; Content = content; }
    }

    public sealed class GeneratedAssetWriteResult
    {
        public bool Success;
        public bool DryRun;
        public readonly List<string> ChangedPaths = new List<string>();
        public readonly List<string> UnchangedPaths = new List<string>();
        public readonly List<string> Errors = new List<string>();
    }

    public interface IGeneratedAssetWriter
    {
        GeneratedAssetWriteResult Write(IReadOnlyList<GeneratedAssetFile> files, bool dryRun = false);
    }

    /// <summary>Validates and transactionally replaces a related set of generated text assets.</summary>
    public sealed class GeneratedAssetWriter : IGeneratedAssetWriter
    {
        public const string GeneratedMarker = "NEXUI:GENERATED";

        public GeneratedAssetWriteResult Write(IReadOnlyList<GeneratedAssetFile> files, bool dryRun = false)
        {
            var result = new GeneratedAssetWriteResult { DryRun = dryRun };
            if (files == null || files.Count == 0) { result.Success = true; return result; }
            var pending = new List<GeneratedAssetFile>();
            try
            {
                var paths = new HashSet<string>(DesignerPlatformUtility.FileSystemComparer);
                foreach (var file in files)
                {
                    var normalizedFile = new GeneratedAssetFile(
                        DesignerPlatformUtility.NormalizeAssetPath(file.Path), file.Content);
                    ValidatePath(normalizedFile.Path);
                    if (!paths.Add(normalizedFile.Path))
                        throw new InvalidOperationException("Duplicate generated path: " + normalizedFile.Path);
                    ValidateContent(normalizedFile);
                    if (File.Exists(normalizedFile.Path))
                    {
                        var existing = File.ReadAllText(normalizedFile.Path);
                        if (!existing.Contains(GeneratedMarker))
                            throw new InvalidOperationException("Refusing to overwrite a file without the generated marker: " + normalizedFile.Path);
                        if (Normalize(existing) == Normalize(normalizedFile.Content))
                        {
                            result.UnchangedPaths.Add(normalizedFile.Path);
                            continue;
                        }
                    }
                    pending.Add(normalizedFile);
                    result.ChangedPaths.Add(normalizedFile.Path);
                }

                if (dryRun || pending.Count == 0) { result.Success = true; return result; }
                StageAndCommit(pending);
                // Import dependencies before UXML documents. A newly-created UXML may reference a
                // sibling USS from the same transaction; importing the UXML first records an invalid
                // zero-GUID dependency in Unity 6 and turns an otherwise successful write into an
                // editor error.
                foreach (var file in pending)
                    if (!string.Equals(Path.GetExtension(file.Path), ".uxml", StringComparison.OrdinalIgnoreCase))
                        AssetDatabase.ImportAsset(file.Path, ImportAssetOptions.ForceUpdate);
                foreach (var file in pending)
                    if (string.Equals(Path.GetExtension(file.Path), ".uxml", StringComparison.OrdinalIgnoreCase))
                        AssetDatabase.ImportAsset(file.Path, ImportAssetOptions.ForceUpdate);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Errors.Add(ex.Message);
                // Expected validation failures are returned to the caller as structured errors.
                // Logging them as Unity errors makes tests and batch CI fail a second time even
                // when the caller intentionally exercises the refusal path.
            }
            return result;
        }

        private static void StageAndCommit(List<GeneratedAssetFile> files)
        {
            var staged = new List<string>();
            var backups = new Dictionary<string, string>(DesignerPlatformUtility.FileSystemComparer);
            var created = new HashSet<string>(DesignerPlatformUtility.FileSystemComparer);
            try
            {
                foreach (var file in files)
                {
                    var directory = Path.GetDirectoryName(file.Path);
                    if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                    var temp = file.Path + ".nexui.tmp." + Guid.NewGuid().ToString("N");
                    File.WriteAllText(temp, file.Content ?? string.Empty, new UTF8Encoding(false));
                    staged.Add(temp);
                }

                TryCheckout(files);
                for (var i = 0; i < files.Count; i++)
                {
                    var path = files[i].Path;
                    if (File.Exists(path))
                    {
                        var backup = path + ".nexui.bak." + Guid.NewGuid().ToString("N");
                        File.Copy(path, backup, true);
                        backups[path] = backup;
                    }
                    else created.Add(path);
                    File.Copy(staged[i], path, true);
                }
            }
            catch
            {
                foreach (var pair in backups)
                    if (File.Exists(pair.Value)) File.Copy(pair.Value, pair.Key, true);
                foreach (var path in created)
                    if (File.Exists(path)) File.Delete(path);
                throw;
            }
            finally
            {
                foreach (var temp in staged) if (File.Exists(temp)) File.Delete(temp);
                foreach (var backup in backups.Values) if (File.Exists(backup)) File.Delete(backup);
            }
        }

        private static void TryCheckout(List<GeneratedAssetFile> files)
        {
            if (!Provider.isActive) return;
            var paths = new List<string>();
            foreach (var file in files) if (File.Exists(file.Path)) paths.Add(file.Path);
            if (paths.Count == 0) return;
            var task = Provider.Checkout(paths.ToArray(), CheckoutMode.Asset);
            task.Wait();
            if (!task.success) throw new IOException("Version Control checkout failed for generated assets.");
        }

        private static void ValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Generated asset path is empty.");
            var normalized = path.Replace('\\', '/');
            if (Path.IsPathRooted(path))
                throw new UnauthorizedAccessException("Generated asset paths must be project-relative: " + path);
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal) && normalized != "Assets")
                throw new UnauthorizedAccessException("Generated assets must be written under the project Assets folder: " + path);
            var segments = normalized.Split('/');
            foreach (var segment in segments)
                if (segment == "." || segment == "..")
                    throw new UnauthorizedAccessException("Generated asset paths cannot traverse directories: " + path);
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            var assetsRoot = Path.GetFullPath(Application.dataPath);
            var fullPath = Path.GetFullPath(Path.Combine(projectRoot ?? string.Empty, normalized));
            if (!DesignerPlatformUtility.IsSameOrChildPath(assetsRoot, fullPath,
                    DesignerPlatformUtility.FileSystemComparison))
                throw new UnauthorizedAccessException("Generated asset resolved outside the project Assets folder: " + path);
            if (Path.GetFileName(path).IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("Generated asset has an invalid file name: " + path);
        }

        private static void ValidateContent(GeneratedAssetFile file)
        {
            if (string.IsNullOrEmpty(file.Content) || !file.Content.Contains(GeneratedMarker))
                throw new InvalidDataException("Generated marker is missing: " + file.Path);
            var extension = Path.GetExtension(file.Path).ToLowerInvariant();
            if (extension == ".uxml")
            {
                var document = new XmlDocument();
                document.LoadXml(file.Content);
            }
            else if (extension == ".uss")
            {
                var depth = 0;
                foreach (var c in file.Content)
                {
                    if (c == '{') depth++;
                    else if (c == '}' && --depth < 0) throw new InvalidDataException("USS contains an unmatched closing brace: " + file.Path);
                }
                if (depth != 0) throw new InvalidDataException("USS contains unmatched braces: " + file.Path);
            }
        }

        private static string Normalize(string value) => (value ?? string.Empty).Replace("\r\n", "\n");
    }

    /// <summary>Filesystem rules shared by Studio features that accept native or Unity asset paths.</summary>
    public static class DesignerPlatformUtility
    {
        public static StringComparison FileSystemComparison
            => Application.platform == RuntimePlatform.WindowsEditor
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

        public static StringComparer FileSystemComparer
            => FileSystemComparison == StringComparison.OrdinalIgnoreCase
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;

        public static string NormalizeAssetPath(string path) => (path ?? string.Empty).Replace('\\', '/');

        public static bool IsSameOrChildPath(string root, string candidate, StringComparison comparison)
        {
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(candidate)) return false;
            var normalizedRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedCandidate = Path.GetFullPath(candidate)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(normalizedRoot, normalizedCandidate, comparison)) return true;
            return normalizedCandidate.Length > normalizedRoot.Length &&
                   normalizedCandidate.StartsWith(normalizedRoot, comparison) &&
                   (normalizedCandidate[normalizedRoot.Length] == Path.DirectorySeparatorChar ||
                    normalizedCandidate[normalizedRoot.Length] == Path.AltDirectorySeparatorChar);
        }

        public static bool TryAbsoluteToAssetPath(string absolutePath, out string assetPath)
        {
            assetPath = null;
            if (string.IsNullOrWhiteSpace(absolutePath)) return false;
            var assetsRoot = Path.GetFullPath(Application.dataPath);
            var candidate = Path.GetFullPath(absolutePath);
            if (!IsSameOrChildPath(assetsRoot, candidate, FileSystemComparison)) return false;
            if (string.Equals(assetsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    candidate.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    FileSystemComparison))
            {
                assetPath = "Assets";
                return true;
            }
            var relative = candidate.Substring(assetsRoot.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            assetPath = "Assets/" + NormalizeAssetPath(relative);
            return true;
        }
    }
}
