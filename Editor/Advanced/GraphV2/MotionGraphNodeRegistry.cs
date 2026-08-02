using System.Collections.Generic;
using emiteat.NexUI.MotionGraph;

namespace emiteat.NexUI.Designer.Editor.GraphV2
{
    /// <summary>One named data input port a node type exposes.</summary>
    public sealed class GraphNodeDataInput
    {
        public string PortName;
        public UIGraphValueType ValueType;

        public GraphNodeDataInput(string portName, UIGraphValueType valueType)
        {
            PortName = portName;
            ValueType = valueType;
        }
    }

    /// <summary>
    /// Describes one node type's ports for the editor - single source of truth so
    /// <see cref="MotionGraphV2NodeView"/> builds ports generically instead of a per-type switch
    /// (same rationale as <c>DesignerComponentRegistry</c>). Unknown node types (e.g. from a newer
    /// Designer version, or a custom executor the project registered itself) resolve to a safe
    /// generic descriptor with just a single "Next" flow output, matching the Component Registry's
    /// forward-compat convention.
    /// </summary>
    public sealed class GraphNodeDescriptor
    {
        public string NodeType;
        public string DisplayName;
        public bool HasFlowInput = true;

        /// <summary>Fixed flow output names (e.g. "Next", or "True"/"False"). Empty when <see cref="SupportsDynamicFlowOutputs"/> is true.</summary>
        public string[] FlowOutputs = System.Array.Empty<string>();

        /// <summary>Sequence/Parallel/Race: flow outputs are a user-editable ordered list ("Step0", "Step1", ...) instead of a fixed set.</summary>
        public bool SupportsDynamicFlowOutputs;

        /// <summary>Fixed outputs shown alongside the dynamic step list (e.g. Race's "Completed"). Ignored unless <see cref="SupportsDynamicFlowOutputs"/> is true.</summary>
        public string[] FixedFlowOutputsWithDynamic = System.Array.Empty<string>();

        public GraphNodeDataInput[] DataInputs = System.Array.Empty<GraphNodeDataInput>();
    }

    public static class MotionGraphNodeRegistry
    {
        private static readonly Dictionary<string, GraphNodeDescriptor> Descriptors = Build();

        public static IReadOnlyDictionary<string, GraphNodeDescriptor> All => Descriptors;

        public static GraphNodeDescriptor Get(string nodeType)
            => Descriptors.TryGetValue(nodeType, out var descriptor) ? descriptor : GenericFallback(nodeType);

        private static GraphNodeDescriptor GenericFallback(string nodeType) => new GraphNodeDescriptor
        {
            NodeType = nodeType,
            DisplayName = string.IsNullOrEmpty(nodeType) ? "Unknown Node" : nodeType,
            FlowOutputs = new[] { "Next" }
        };

        private static Dictionary<string, GraphNodeDescriptor> Build()
        {
            var descriptors = new[]
            {
                new GraphNodeDescriptor
                {
                    NodeType = "Event",
                    DisplayName = "Event",
                    HasFlowInput = false,
                    FlowOutputs = new[] { "Next" }
                },
                new GraphNodeDescriptor
                {
                    NodeType = "Flow.Sequence",
                    DisplayName = "Sequence",
                    SupportsDynamicFlowOutputs = true
                },
                new GraphNodeDescriptor
                {
                    NodeType = "Flow.Parallel",
                    DisplayName = "Parallel",
                    SupportsDynamicFlowOutputs = true,
                    DataInputs = new[] { new GraphNodeDataInput("Completion Policy", UIGraphValueType.String) }
                },
                new GraphNodeDescriptor
                {
                    NodeType = "Flow.Delay",
                    DisplayName = "Delay",
                    FlowOutputs = new[] { "Next" },
                    DataInputs = new[] { new GraphNodeDataInput("Duration", UIGraphValueType.Float) }
                },
                new GraphNodeDescriptor
                {
                    NodeType = "Flow.Branch",
                    DisplayName = "Branch",
                    FlowOutputs = new[] { "True", "False" },
                    DataInputs = new[] { new GraphNodeDataInput("Condition", UIGraphValueType.Bool) }
                },
                new GraphNodeDescriptor
                {
                    NodeType = "Motion.PlayClip",
                    DisplayName = "Play Motion Clip",
                    FlowOutputs = new[] { "Completed" },
                    DataInputs = new[] { new GraphNodeDataInput("Clip", UIGraphValueType.MotionClip) }
                },
                new GraphNodeDescriptor
                {
                    NodeType = "Data.Expression",
                    DisplayName = "Expression",
                    FlowOutputs = new[] { "Next" },
                    DataInputs = new[]
                    {
                        new GraphNodeDataInput("Operation", UIGraphValueType.String),
                        new GraphNodeDataInput("A", UIGraphValueType.Float),
                        new GraphNodeDataInput("B", UIGraphValueType.Float)
                    }
                },
                new GraphNodeDescriptor
                {
                    NodeType = "Data.SetFloatVariable",
                    DisplayName = "Set Float Variable",
                    FlowOutputs = new[] { "Next" },
                    DataInputs = new[]
                    {
                        new GraphNodeDataInput("Name", UIGraphValueType.String),
                        new GraphNodeDataInput("Value", UIGraphValueType.Float)
                    }
                },
                new GraphNodeDescriptor
                {
                    NodeType = "Data.SetBoolVariable",
                    DisplayName = "Set Bool Variable",
                    FlowOutputs = new[] { "Next" },
                    DataInputs = new[]
                    {
                        new GraphNodeDataInput("Name", UIGraphValueType.String),
                        new GraphNodeDataInput("Value", UIGraphValueType.Bool)
                    }
                },
                new GraphNodeDescriptor
                {
                    NodeType = "Command.Dispatch",
                    DisplayName = "Dispatch Command",
                    FlowOutputs = new[] { "Success", "Failed" },
                    DataInputs = new[] { new GraphNodeDataInput("CommandId", UIGraphValueType.String) }
                },
                new GraphNodeDescriptor
                {
                    NodeType = "Flow.Repeat",
                    DisplayName = "Repeat",
                    FlowOutputs = new[] { "Body", "Completed" },
                    DataInputs = new[] { new GraphNodeDataInput("Count", UIGraphValueType.Int) }
                },
                new GraphNodeDescriptor
                {
                    NodeType = "Flow.Timeout",
                    DisplayName = "Timeout",
                    FlowOutputs = new[] { "Body", "Completed", "TimedOut" },
                    DataInputs = new[] { new GraphNodeDataInput("Duration", UIGraphValueType.Float) }
                },
                new GraphNodeDescriptor
                {
                    NodeType = "Flow.Race",
                    DisplayName = "Race",
                    SupportsDynamicFlowOutputs = true,
                    FixedFlowOutputsWithDynamic = new[] { "Completed" }
                },
                new GraphNodeDescriptor
                {
                    NodeType = "Graph.RunSubgraph",
                    DisplayName = "Run Subgraph",
                    FlowOutputs = new[] { "Completed" },
                    DataInputs = new[]
                    {
                        new GraphNodeDataInput("Graph", UIGraphValueType.MotionGraph),
                        new GraphNodeDataInput("EventName", UIGraphValueType.String)
                    }
                }
            };

            var dict = new Dictionary<string, GraphNodeDescriptor>();
            foreach (var descriptor in descriptors) dict[descriptor.NodeType] = descriptor;
            return dict;
        }
    }
}
