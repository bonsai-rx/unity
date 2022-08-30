using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Bonsai.Expressions;
using System;

public class WorkflowSubscriber : MonoBehaviour
{
    public BonsaiWorkflow BonsaiWorkflow;

    private void Awake()
    {
        BonsaiWorkflow.InjectSubscription("TimerOutput", typeof(SubscribeSubjectBuilder), unit => { Debug.Log(unit); });
    }
}
