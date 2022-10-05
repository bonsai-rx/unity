using Bonsai;
using Bonsai.Expressions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using UnityEngine;

public class InputInjector : MonoBehaviour
{
    public BonsaiWorkflow Workflow;

    private IntegerSource Source;
    private DataDevice Device;

    private int counter = 0;

    // Start is called before the first frame update
    private void Start()
    {
        Device = new DataDevice();
        Source = new IntegerSource { Device = this.Device };

        Workflow.InjectInput(Source, "IntInput");

        Workflow.InjectSubscription("IntValue", typeof(PublishSubjectBuilder), val => { Debug.Log(val); });
    }

    private void Update()
    {
        Device.handler.Invoke(this, new DeviceEventArgs { number = counter });
        counter++;
    }

    class IntegerSource : Source<int>
    {
        public DataDevice Device;

        public override IObservable<int> Generate()
        {
            return Observable.Create<int>(observer =>
            {
                Device.handler += (sender, e) => {
                    observer.OnNext(e.number);
                };

                return Disposable.Create(() => { });
            });
        }
    }

    class DataDevice
    {
        public delegate void EventHandler(object sender, DeviceEventArgs e);
        public EventHandler handler = (sender, e) => { };
    }

    class DeviceEventArgs : EventArgs
    {
        public int number;
    }
}
