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

public class BonsaiWorkflow : MonoBehaviour
{
    public BonsaiWorkflowAsset BonsaiWorkflowAsset;

    private WorkflowBuilder WorkflowBuilder;
    private IObservable<System.Reactive.Unit> WorkflowRuntime;
    private IDisposable WorkflowDisposable;

    private List<SubscriptionInjection> SubscriptionInjections = new List<SubscriptionInjection>();

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

    private struct SubscriptionInjection
    {
        public string TargetName;
        public Type TargetType;
        public Action<object> Action;
    }
}
