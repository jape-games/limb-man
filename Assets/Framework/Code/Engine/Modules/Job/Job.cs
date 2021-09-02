﻿using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Jape
{
    public partial class Job : Module<Job>, ICloneable
    {
        protected bool debug;

        public enum Mode { Single, Loop };
        protected Mode mode = Mode.Single;

        protected Routine routine;
        protected Coroutine coroutine;
        protected IEnumerator enumerator;

        public Event<object> onReturn = new Event<object>();

        internal Job() { Init(this); }

        public virtual object Clone()
        {
            Job clone = Copy();
            return CloneFill(clone);
        }

        protected virtual Job Copy()
        {
            if (ModuleManager.Instance.GetModules().Contains(this)) { return Create(); }
            if (ModuleManager.Instance.GetModulesGlobal().Contains(this)) { return CreateGlobal(); }
            this.Log().Warning("Copy job created without module manager");
            return new Job().Set(routine);
        }

        protected Job CloneFill(Job job)
        {
            if (debug) { job.ToggleDebug(); }
            job.mode = mode;
            return job;
        }

        public override Job ForceStart()
        {
            if (routine == null) { this.Log().Response("Cant start because the routine is not set"); return this; }
            return base.ForceStart();
        }

        public Job Set(IEnumerable routine) { Set(new Routine(routine)); return this; }
        public Job Set(Action routine) { Set(new Routine(routine)); return this; }
        public Job Set(Routine routine)
        {
            if (IsProcessing()) { this.Log().Response("Cant set routine while job is processing"); return this; }
            this.routine = routine; 
            return this;
        }

        protected override void StartAction() { routine.Launch(Dispatch, Action); }
        protected override void StopAction() { Recall(); }
        protected override void PauseAction() { Recall(); }
        protected override void ResumeAction() { Dispatch(); }

        public Job ChangeMode(Mode mode) { this.mode = mode; return this; } 
        
        public Job ToggleDebug()
        {
            debug = !debug;

            if (debug) { onReturn.Handler += DebugReturn; }
            else { onReturn.Handler -= DebugReturn; }

            return this;
        }

        protected virtual void SetEnumerator() { enumerator = routine.SetEnumerator(); }
        protected virtual void Dispatch() { coroutine = DispatchManager.Instance?.Dispatch(Enumeration()); }
        protected virtual void Recall() { DispatchManager.Instance?.Recall(coroutine); }

        public bool IsAction() { return routine.Type() == typeof(Action); }
        public bool IsEnumeration() { return routine.Type() == typeof(IEnumerable); }

        /// <summary>
        /// Wait until yield matches the value
        /// </summary>
        public IEnumerator WaitReturnValue(object returnValue)
        {
            Trigger trigger = new Trigger();
            onReturn.Handler += Trigger;
            yield return trigger.Wait();
            onReturn.Handler -= Trigger;

            void Trigger(object sender, object value)
            {
                if (!Equals(returnValue, value)) { return; }
                trigger.Invoke(this);
            }
        }

        /// <summary>
        /// Wait until yield matches the condition
        /// </summary>
        public IEnumerator WaitReturnValue(Func<object, bool> predicate)
        {
            Trigger trigger = new Trigger();
            onReturn.Handler += Trigger;
            yield return trigger.Wait();
            onReturn.Handler -= Trigger;

            void Trigger(object sender, object value)
            {
                if (!predicate(value)) { return; }
                trigger.Invoke(this);
            }
        }

        /// <summary>
        /// Wait until yield matches the condition and send yield value to action
        /// </summary>
        public IEnumerator GetReturnValue(Func<object, bool> predicate, Action<object> action)
        {
            Trigger trigger = new Trigger();
            onReturn.Handler += Trigger;
            yield return trigger.Wait();
            onReturn.Handler -= Trigger;

            void Trigger(object sender, object value)
            {
                if (!predicate(value)) { return; }
                action?.Invoke(value);
                trigger.Invoke(this);
            }
        }

        protected void Action()
        {
            Action action = (Action)routine.Get();
            action.Invoke();
            
            Iteration();
            Complete();
            Processed();
        }

        protected IEnumerator Enumeration()
        {
            SetEnumerator();
            while (!complete)
            {
                if (enumerator.MoveNext())
                {
                    if (!Skip(enumerator.Current))
                    {
                        onReturn.Trigger(this, enumerator.Current);
                        yield return enumerator.Current;
                    }
                }
                else
                {
                    switch (mode)
                    {
                        case Mode.Single: Iteration(); Complete(); Processed(); break;
                        case Mode.Loop: Iteration(); SetEnumerator(); break;
                    }
                }
            }

            bool Skip(object current)
            {
                if (current == null) { return false; }
                return current.GetType() == typeof(Wait.Skip);
            }
        }

        protected void DebugReturn(object sender, object value)
        {
            if (!debug) { return; }
            this.Log().Value("Return", value);
        }

        public override string ToString() { return $"Job ({routine.Get()})"; }
    }
}