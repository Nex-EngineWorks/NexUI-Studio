using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Properties;
using emiteat.NexUI.Vector;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor
{
    /// <summary>
    /// The vector shapes offered as a starting point when creating an element.
    /// </summary>
    /// <remarks>
    /// A fixed list rather than a parameterised dialog. Every one of these becomes an ordinary
    /// editable path the moment it exists, so the pen tool - not a properties form - is where a
    /// six-pointed star turns into a seven-pointed one. That keeps one way to change a shape
    /// instead of two that can disagree.
    /// </remarks>
    public enum NexShapePreset
    {
        Rectangle,
        RoundedRectangle,
        Ellipse,
        Triangle,
        Pentagon,
        Hexagon,
        Star,
        SixPointStar,
        Ring,
        HalfCircle,
        Arc
    }

    public sealed partial class NexUIDesignerContext
    {
        /// <summary>
        /// Combines the selected elements into one shape and removes the operands it consumed.
        /// </summary>
        /// <remarks>
        /// The first selected element is the subject: it survives, keeps its id, bindings,
        /// interactions and component stack, and receives the combined path. That matters more
        /// than it sounds - a boolean operation that produced a brand-new element would silently
        /// drop everything authored on the originals, and "my button stopped working after I
        /// subtracted a notch from it" is a bad way to find out.
        ///
        /// Elements with no drawn path contribute their silhouette instead, so a circle can be
        /// subtracted from a plain panel without drawing the panel's outline by hand first.
        /// </remarks>
        /// <returns>The surviving element, or null if there was nothing to combine.</returns>
        public DesignerElementMetadata CombineSelection(NexBooleanOperation operation)
        {
            if (Metadata == null || _selection.Count < 2) return null;

            var operands = new List<DesignerElementMetadata>(_selection);
            var subject = operands[0];

            var shapes = new List<NexVectorShape>(operands.Count);
            foreach (var element in operands)
            {
                var shape = CanvasSpaceShape(element);
                if (shape != null) shapes.Add(shape);
            }

            if (shapes.Count < 2) return null;

            var combined = NexVectorBoolean.Combine(shapes, operation);

            NexUIDesignerUndo.Group(UndoNameFor(operation), () =>
            {
                RecordMetadata(UndoNameFor(operation));

                subject.hasShape = true;
                subject.vectorShape = combined;
                subject.rect = DesignerVectorSpace.RectFor(combined, subject.rect);

                // Consumed operands go the same way a delete does, so their descendants are not
                // left pointing at a parent that no longer exists.
                for (var i = 1; i < operands.Count; i++)
                {
                    var element = operands[i];
                    if (element == null || !Metadata.elements.Contains(element)) continue;

                    foreach (var descendant in DesignerHierarchyUtility.GetDescendants(Metadata, element))
                        Metadata.elements.Remove(descendant);
                    Metadata.elements.Remove(element);
                }

                DesignerHierarchyUtility.NormalizeSiblingIndices(Metadata);
                MarkMetadataDirty();
            });

            SelectMetadata(subject);
            Validate();
            return subject;
        }

        /// <summary>Whether <see cref="CombineSelection"/> has something to do right now.</summary>
        public bool CanCombineSelection => Metadata != null && _selection.Count >= 2;

        /// <summary>The default box a new shape is created in, before it is moved or resized.</summary>
        private static readonly Rect DefaultShapeRect = new Rect(0f, 0f, 160f, 160f);

        /// <summary>
        /// Creates an element that draws <paramref name="preset"/>.
        /// </summary>
        /// <param name="preset">Which shape to start from.</param>
        /// <param name="canvasPoint">Where to put it, or null for the default position.</param>
        /// <remarks>
        /// The result is a normal element carrying a normal path - not a special "shape element".
        /// It can be bound, given interactions, combined, and re-pointed with the pen exactly like
        /// one drawn by hand, because a preset is only a faster way to place the first anchors.
        /// </remarks>
        public DesignerElementMetadata CreateShapeElement(NexShapePreset preset, Vector2? canvasPoint = null)
        {
            if (Metadata == null) return null;

            var element = CreateEmptyMetadataElement();
            if (element == null) return null;

            var rect = DefaultShapeRect;
            if (canvasPoint.HasValue) rect.position = canvasPoint.Value;

            var shape = Build(preset, rect);

            UpdateElement(element, target =>
            {
                target.displayName = DisplayNameFor(preset);
                target.hasShape = true;
                target.vectorShape = shape;
                target.rect = DesignerVectorSpace.RectFor(shape, rect);

                // An arc is a stroked open path, so it has no fill to tint; everything else takes
                // the element's own tint so a new shape is visible immediately rather than white
                // on white.
                if (shape.Filled) shape.FillColor = DesignerPropertyAdapter.BackgroundColor(target);
            }, "Create NexUI Shape");

            Validate();
            return element;
        }

        private static NexVectorShape Build(NexShapePreset preset, Rect rect)
        {
            switch (preset)
            {
                case NexShapePreset.RoundedRectangle:
                    return NexShapeFactory.RoundedRectangle(rect, Mathf.Min(rect.width, rect.height) * 0.15f);
                case NexShapePreset.Ellipse: return NexShapeFactory.Ellipse(rect);
                case NexShapePreset.Triangle: return NexShapeFactory.Polygon(rect, 3);
                case NexShapePreset.Pentagon: return NexShapeFactory.Polygon(rect, 5);
                case NexShapePreset.Hexagon: return NexShapeFactory.Polygon(rect, 6);
                case NexShapePreset.Star: return NexShapeFactory.Star(rect, 5, 0.4f);
                case NexShapePreset.SixPointStar: return NexShapeFactory.Star(rect, 6, 0.55f);
                case NexShapePreset.Ring:
                    return NexShapeFactory.Ring(rect, Mathf.Min(rect.width, rect.height) * 0.25f);
                case NexShapePreset.HalfCircle: return NexShapeFactory.Pie(rect, 180f, 180f);
                case NexShapePreset.Arc:
                    return NexShapeFactory.Arc(rect, 180f, 180f, Mathf.Min(rect.width, rect.height) * 0.08f);
                default: return NexShapeFactory.Rectangle(rect);
            }
        }

        private static string DisplayNameFor(NexShapePreset preset)
        {
            switch (preset)
            {
                case NexShapePreset.RoundedRectangle: return "Rounded Rectangle";
                case NexShapePreset.SixPointStar: return "Six-Point Star";
                case NexShapePreset.HalfCircle: return "Half Circle";
                default: return preset.ToString();
            }
        }

        /// <summary>
        /// Imports an SVG file as one element per shape it contains.
        /// </summary>
        /// <param name="path">Absolute path to the .svg file.</param>
        /// <param name="canvasPoint">Where the artwork's top-left should land, or null for a default.</param>
        /// <param name="error">Why nothing was imported, when the result is empty.</param>
        /// <remarks>
        /// One element per shape rather than one element for the file, because an icon's shapes
        /// usually carry different fills and merging them would throw the colours away. They are
        /// placed under a shared parent so the icon still moves as a unit.
        ///
        /// The document's own coordinates are preserved relative to each other and only translated
        /// as a group, so an imported icon keeps its proportions instead of each piece being
        /// separately normalised into its own box.
        /// </remarks>
        public IReadOnlyList<DesignerElementMetadata> ImportSvg(string path, Vector2? canvasPoint, out string error)
        {
            error = null;
            var created = new List<DesignerElementMetadata>();

            if (Metadata == null)
            {
                error = "Open a screen before importing.";
                return created;
            }

            var import = NexSvgImporter.ImportFile(path);
            if (!import.Succeeded)
            {
                error = import.Error;
                return created;
            }

            var origin = canvasPoint ?? new Vector2(96f, 96f);
            var offset = origin - import.Bounds.min;
            var name = System.IO.Path.GetFileNameWithoutExtension(path);

            NexUIDesignerUndo.Group("Import SVG", () =>
            {
                // A single-shape file needs no wrapper; making one anyway would leave every simple
                // icon nested one level deeper than the author drew it.
                var parent = import.Shapes.Count > 1 ? CreateSvgGroup(name, import.Bounds, offset) : null;

                foreach (var shape in import.Shapes)
                {
                    var bounds = shape.Bounds();
                    if (bounds.width <= 0f && bounds.height <= 0f) continue;

                    Translate(shape, offset);

                    var element = CreateEmptyMetadataElement();
                    if (element == null) continue;

                    UpdateElement(element, target =>
                    {
                        target.displayName = name;
                        target.hasShape = true;
                        target.vectorShape = shape;
                        target.rect = DesignerVectorSpace.RectFor(shape, new Rect(bounds.position + offset,
                            new Vector2(Mathf.Max(1f, bounds.width), Mathf.Max(1f, bounds.height))));
                        if (parent != null) target.parentId = parent.elementId;
                    }, "Import SVG Shape");

                    created.Add(element);
                }

                DesignerHierarchyUtility.NormalizeSiblingIndices(Metadata);
                MarkMetadataDirty();
            });

            if (created.Count == 0) error = "The SVG contained no shapes NexUI can import.";
            else SelectMany(created);

            Validate();
            return created;
        }

        private DesignerElementMetadata CreateSvgGroup(string name, Rect bounds, Vector2 offset)
        {
            var group = CreateEmptyMetadataElement();
            if (group == null) return null;

            UpdateElement(group, target =>
            {
                target.displayName = name;
                target.rect = new Rect(bounds.min + offset, bounds.size);
                target.tint = new Color(0f, 0f, 0f, 0f);
            }, "Import SVG");

            DesignerPropertyAdapter.SetBackgroundColor(group, group.tint);
            return group;
        }

        /// <summary>Moves a whole path, leaving its handles alone - they are already relative.</summary>
        private static void Translate(NexVectorShape shape, Vector2 offset)
        {
            if (shape == null || offset == Vector2.zero) return;

            foreach (var contour in shape.Contours)
            {
                if (contour == null) continue;
                var anchors = contour.Anchors;
                for (var i = 0; i < anchors.Count; i++)
                {
                    var anchor = anchors[i];
                    anchor.Position += offset;
                    anchors[i] = anchor;
                }
            }
        }

        /// <summary>
        /// The element's path in canvas coordinates, or its silhouette when it has none.
        /// </summary>
        /// <remarks>
        /// A clone every time. The operands are about to be fed to a clipper and the subject is
        /// about to be overwritten with the answer; sharing a contour between those two would let
        /// the result change its own input midway.
        /// </remarks>
        private static NexVectorShape CanvasSpaceShape(DesignerElementMetadata element)
        {
            if (element == null) return null;

            if (element.hasShape && element.vectorShape != null && !element.vectorShape.IsEmpty)
            {
                var drawn = element.vectorShape.Clone();
                DesignerVectorSpace.Bake(drawn, element.rect);
                return drawn;
            }

            var rect = element.rect;
            if (rect.width <= 0f || rect.height <= 0f) return null;

            // The silhouette the canvas already draws, so combining matches what was on screen.
            switch (element.shape)
            {
                case DesignerElementShape.Circle:
                    return NexShapeFactory.Ellipse(rect);

                case DesignerElementShape.Pill:
                    return NexShapeFactory.RoundedRectangle(rect, Mathf.Min(rect.width, rect.height) * 0.5f);

                case DesignerElementShape.Rounded:
                    var radius = Mathf.Min(DesignerPropertyAdapter.CornerRadius(element),
                        Mathf.Min(rect.width, rect.height) * 0.5f);
                    return radius > 0.01f
                        ? NexShapeFactory.RoundedRectangle(rect, radius)
                        : NexShapeFactory.Rectangle(rect);

                default:
                    return NexShapeFactory.Rectangle(rect);
            }
        }

        private static string UndoNameFor(NexBooleanOperation operation)
        {
            switch (operation)
            {
                case NexBooleanOperation.Intersect: return "Intersect NexUI Shapes";
                case NexBooleanOperation.Subtract: return "Subtract NexUI Shapes";
                case NexBooleanOperation.Exclude: return "Exclude NexUI Shapes";
                default: return "Unite NexUI Shapes";
            }
        }
    }
}
