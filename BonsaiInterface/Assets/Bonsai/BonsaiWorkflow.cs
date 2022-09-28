using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Bonsai; // Requires dll for Bonsai.Core is exported with package
using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using Bonsai.Expressions; // Requires dll for Bonsai.Expressions is exported with package
using System.Linq;
using System.Reactive.Linq;
using Bonsai.Dag;

public class BonsaiWorkflow : MonoBehaviour
{
    public BonsaiWorkflowAsset BonsaiWorkflowAsset;

    private WorkflowBuilder WorkflowBuilder;
    private IObservable<System.Reactive.Unit> WorkflowRuntime;
    private IDisposable WorkflowDisposable;

    // The set of Bonsai-->Unity subscriptions we must build when starting the workflow
    private List<SubscriptionInjection> SubscriptionInjections = new List<SubscriptionInjection>();

    // The set of Unity-->Bonsai input sources we must build when starting the workflow
    private List<InputInjection> InputInjections = new List<InputInjection>();

    private void Awake()
    {
        ParseWorkflow();
    }

    private void Start()
    {
        StartWorkflow();
    }

    private void StartWorkflow()
    {
        if (WorkflowDisposable != null)
        {
            Debug.LogError("Workflow already started.");
            return;
        }

        // Apply input injections. TODO - what if user tries multiple inputs to the same node
        foreach (var input in InputInjections)
        {
            // Convert externalized mapping to property mapping
            var propertyMappingBuilder = new PropertyMappingBuilder();
            var externalizedMappingBuilder = ExpressionBuilder.Unwrap(input.To.Value) as ExternalizedMappingBuilder; // TODO - 'To' should always be an externalizedmapping or propertymapping
            foreach (var mapping in externalizedMappingBuilder.ExternalizedProperties)
            {
                propertyMappingBuilder.PropertyMappings.Add(new PropertyMapping(mapping.Name, null));
            }

            // replace externalized mapping with property mapping
            var replaceNode = WorkflowBuilder.Workflow.Add(propertyMappingBuilder);
            WorkflowBuilder.Workflow.AddEdge(replaceNode, input.To.Successors[0].Target, new ExpressionBuilderArgument());
            WorkflowBuilder.Workflow.Remove(input.To);

            // Add the input injection and edge
            var fromNode = WorkflowBuilder.Workflow.Add(input.From);
            WorkflowBuilder.Workflow.AddEdge(fromNode, replaceNode, new ExpressionBuilderArgument());
        }
        WorkflowBuilder = new WorkflowBuilder(WorkflowBuilder.Workflow.ToInspectableGraph());

        // Build workflow
        WorkflowBuilder = new WorkflowBuilder(WorkflowBuilder.Workflow.ToInspectableGraph());
        WorkflowBuilder.Workflow.Build();
        WorkflowRuntime = WorkflowBuilder.Workflow.BuildObservable();

        // Apply subscription injections
        foreach (var subscription in SubscriptionInjections)
        {
            var selectedNode = WorkflowBuilder.Workflow.Where(x => ExpressionBuilder.GetElementDisplayName(x.Value) == subscription.TargetName
                && ExpressionBuilder.Unwrap(x.Value).GetType() == subscription.TargetType).FirstOrDefault();

            if (selectedNode != null)
            {
                var inspect = (InspectBuilder)selectedNode.Value;
                inspect.Output.Merge().Subscribe(unit => { BonsaiDispatcher.RunOnMainThread(() => { subscription.Action(unit); }); });
            }
            else
            {
                Debug.LogWarning($"Target node {subscription.TargetName} for subscription injection not found.");
            }
        }

        // Create workflow subscription to begin, cache the disposable for ending the session, log errors to the main thread
        WorkflowDisposable = WorkflowRuntime.Subscribe(
            unit => { },
            er => { BonsaiDispatcher.RunOnMainThread(() => { Debug.Log(er); }); },
            () => { Debug.Log("Completed"); }
        );
    }

    private void OnDestroy()
    {
        WorkflowDisposable.Dispose();
    }

    private void ParseWorkflow()
    {
        using (var stringReader = new StringReader(BonsaiWorkflowAsset.WorkflowBuilderData))
        {
            using (var reader = XmlReader.Create(stringReader))
            {
                reader.MoveToContent();
                var serializer = new XmlSerializer(typeof(WorkflowBuilder), reader.NamespaceURI);
                WorkflowBuilder = (WorkflowBuilder)serializer.Deserialize(reader);
            }
        }
    }

    public void InjectSubscription(string targetNode, Type targetType, Action<object> action)
    {
        SubscriptionInjections.Add(new SubscriptionInjection { TargetName = targetNode, TargetType = targetType, Action = action });
    }

    // TODO - we should only be able to inject onto externalizedmapping or propertymapping (or in future e.g. UnitySubject)
    public void InjectInput<T>(Source<T> source, string targetName)
    {
        var externalizedMappings = WorkflowBuilder.Workflow.Where(x => ExpressionBuilder.Unwrap(x.Value).GetType() == typeof(ExternalizedMappingBuilder));
        var targetNode = externalizedMappings.Where(x => ExpressionBuilder.GetElementDisplayName(x.Value) == targetName).FirstOrDefault();

        if (targetNode != null)
        {
            var sourceNodeBuilder = new CombinatorBuilder { Combinator = source };
            InputInjections.Add(new InputInjection { From = sourceNodeBuilder, To = targetNode });
        }
        else
        {
            Debug.LogWarning($"Target node {targetName} for input injection not found.");
        }
    }

    private struct SubscriptionInjection
    {
        public string TargetName;
        public Type TargetType;
        public Action<object> Action;
    }

    private struct InputInjection
    {
        public ExpressionBuilder From;
        public Node<ExpressionBuilder, ExpressionBuilderArgument> To;
    }
}
