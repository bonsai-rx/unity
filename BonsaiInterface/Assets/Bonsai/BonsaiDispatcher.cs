using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class BonsaiDispatcher : MonoBehaviour
{
    static BonsaiDispatcher Instance;
    static volatile bool Queued = false;
    static List<Action> Backlog = new List<Action>(8);
    static List<Action> Actions = new List<Action>(8);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (Instance == null)
        {
            Instance = new GameObject("Dispatcher").AddComponent<BonsaiDispatcher>();
            DontDestroyOnLoad(Instance.gameObject);
        }
    }

    public static void RunAsync(Action<object> action, object state)
    {
        ThreadPool.QueueUserWorkItem(o => action(o), state);
    }

    public static void RunOnMainThread(Action action)
    {
        lock (Backlog)
        {
            Backlog.Add(action);
            Queued = true;
        }
    }

    private void Update()
    {
        if (Queued)
        {
            lock (Backlog)
            {
                var tmp = Actions;
                Actions = Backlog;
                Backlog = tmp;
                Queued = false;
            }

            foreach (var action in Actions)
                action();

            Actions.Clear();
        }
    }
}
