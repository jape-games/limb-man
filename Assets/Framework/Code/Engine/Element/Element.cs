﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

#pragma warning disable IDE0079
#pragma warning disable CS0109

namespace Jape
{
    public abstract partial class Element : Mono
    {
        protected const char KeySplitChar = '_';

        public virtual bool Saved => false;
        protected virtual IReceiver[] Receivers { get; set; }

        private Key cachedKey;

        public new virtual Key Key => cachedKey ??= GenerateKey();

        protected virtual Key GenerateKey() => new(GetType(), 
                                                       gameObject.Identifier(), 
                                                       gameObject.HasId() ? Key.IdentifierEncoding.Hex : Key.IdentifierEncoding.Ascii);

        internal List<Job> jobs = new();
        internal List<Activity> activities = new();
        internal List<ModifierInstance> modifiers = new();

        internal void AddModifier(ModifierInstance modifier) { modifiers.Add(modifier); }
        internal void RemoveModifier(ModifierInstance modifier) { modifiers.Remove(modifier); }
        public bool HasModifier(ModifierInstance modifier) { return modifiers.Contains(modifier); }

        protected virtual Status CreateStatus() => new()
        {
            Key = Key.ToString()
        };

        private bool CanSave()
        {
            if (string.IsNullOrEmpty(gameObject.Identifier())) { return false; }
            return Saved && gameObject.Properties().CanSaveElement(this);
        }

        /// <summary>
        /// Called before status is saved
        /// Used to set status values from element
        /// </summary>
        protected virtual void StatusSave(Status status) {}

        /// <summary>
        /// Called after status is loaded, called after Init() when first initialized
        /// Used to set element values from status
        /// </summary>
        protected virtual void StatusLoad(Status status) {}

        /// <summary>
        /// Called before status is loaded, called before StatusLoad()
        /// </summary>
        protected virtual void StatusPreload() {}

        /// <summary>
        /// Called when status is streaming
        /// </summary>
        protected virtual void StatusStream(DataStream stream) {}

        public void Save()
        {
            if (!CanSave()) { return; }

            Status status = CreateStatus();

            SaveAttributes(status);
            SaveStream(status);
            StatusSave(status);

            Jape.Status.Save(status);
        }

        public void Load()
        {
            if (!CanSave()) { return; }

            Status status = Jape.Status.Load<Status>(Key.ToString());

            if (status == null) { return; }

            StatusPreload();
            LoadAttributes(status);
            LoadStream(status);
            StatusLoad(status);
        }

        private void SaveStream(Status status)
        {
            status.StreamWrite(StatusStream);
        }

        private void LoadStream(Status status)
        {
            status.StreamRead(StatusStream);
        }

        private void SaveAttributes(Status status)
        {
            foreach (var (field, attribute) in GetAttributeMembers<SaveAttribute>(true))
            {
                string key = attribute.Key ?? field.Name;
                if (status.AttributeData.ContainsKey(key))
                {
                    status.AttributeData[key] = field.GetValue(this);
                } 
                else { status.AttributeData.Add(key, field.GetValue(this)); }
            }
        }

        private void LoadAttributes(Status status)
        {
            foreach (var (field, attribute) in GetAttributeMembers<SaveAttribute>(true))
            {
                string key = attribute.Key ?? field.Name;
                if (status.AttributeData.ContainsKey(key)) { field.SetValue(this, status.AttributeData[key]); } 
            }
        }

        internal override void Awake()
        {
            base.Awake(); // First //

            if (Game.IsRunning)
            {
                EngineManager.Instance.runtimeElements.Add(this);
                SaveManager.Instance.OnSaveRequest += Save;
                SaveManager.Instance.OnLoadResponse += Load;

                Load();
            }
        }

        internal override void OnDestroy()
        {
            base.OnDestroy(); // First //

            if (Game.IsRunning)
            {
                if (EngineManager.Instance != null) { EngineManager.Instance.runtimeElements.Remove(this); }
                if (SaveManager.Instance != null)
                {
                    SaveManager.Instance.OnSaveRequest -= Save;
                    SaveManager.Instance.OnLoadResponse -= Load;
                }

                DestroyJobs();
                DestroyActivities();
                DestroyModifiers();
            }
        }

        internal void Send(IReceivable receivable)
        {
            if (receivable == null) { return; }
            if (Receivers == null) { return; }

            IEnumerable<IReceiver> targetReceivers = Receivers.Where(receiver => receiver.Type.IsBaseOrSubclassOf(receivable.GetType()));

            foreach (IReceiver receiver in targetReceivers)
            {
                receiver.Receive(receivable);
            }
        }

        private void DestroyModifiers()
        {
            for (int i = modifiers.Count - 1; i >= 0; i--)
            {
                ModifierInstance modifier = modifiers[i];
                modifier.Destroy();
            }
        }

        protected Job RunJob(IEnumerable routine)
        {
            Job job = CreateJob().Set(routine).Start();
            JobManager.QueueAction(job, QueueDestroy);
            return job;

            void QueueDestroy() { DestroyJob(job); }
        }

        protected Job RunJob(Action routine)
        {
            Job job = CreateJob().Set(routine).Start();
            JobManager.QueueAction(job, QueueDestroy);
            return job;

            void QueueDestroy() { DestroyJob(job); }
        }

        protected Job CreateJob()
        {
            Job job = new ElementJob(this);
            jobs.Add(job);
            return job;
        }

        protected void DestroyJob(Job job) 
        {
            job?.Destroy();
            jobs.Remove(job);
        }

        private void DestroyJobs()
        {
            for (int i = jobs.Count - 1; i >= 0; i--)
            {
                DestroyJob(jobs[i]);
            }
        }

        protected Activity CreateActivity()
        {
            Activity activity = new ElementActivity(this);
            activities.Add(activity);
            return activity;
        }

        protected void DestroyActivity(Activity activity) 
        {
            activity?.Destroy();
            activities.Remove(activity);
        }

        private void DestroyActivities()
        {
            for (int i = activities.Count - 1; i >= 0; i--)
            {
                DestroyActivity(activities[i]);
            }
        }

        protected JobQueue CreateJobQueue() { return CreateDrivenModule<JobQueue>(); }

        protected Timer CreateTimer() { return CreateDrivenModule<Timer>(); }
        protected void Delay(float time, Time.Counter counter, Action action) { Timer.Delay(CreateTimer(), time, counter, action); }

        protected Condition CreateCondition() { return CreateDrivenModule<Condition>(); }
        protected Flash CreateFlash() { return CreateDrivenModule<Flash>(); }

        public T CreateDrivenModule<T>() where T : JobDriven<T>
        {
            JobDriven<T>.LogOff();
            T module = (T)Activator.CreateInstance(typeof(T), true);
            module.SetJob(CreateJob());
            JobDriven<T>.LogOn();
            return module;
        }

        protected static Receiver<T> Receive<T>(Action<T> action) where T : IReceivable { return new Receiver<T>(action); }

        public static IEnumerable<Type> Subclass(bool includeEntities = false, bool includeManagers = false)
        {
            IEnumerable<Type> classes = typeof(Element).GetSubclass();

            if (!includeEntities) { classes = classes.Where(t => !typeof(Entity).IsAssignableFrom(t)); }
            if (!includeManagers) { classes = classes.Where(t => !t.IsGenericSubclassOf(typeof(Manager<>))); }

            return classes;
        }

        [Pure] public new static IEnumerable<T> FindAll<T>() where T : Element { return FindAll(typeof(T)).Cast<T>(); }
        [Pure] public new static IEnumerable<Element> FindAll(Type type) { return FindAll().Where(e => e.GetType().IsBaseOrSubclassOf(type)); }
        [Pure] public new static IEnumerable<Element> FindAll() { return EngineManager.Instance.runtimeElements.Where(e => e.gameObject.activeInHierarchy); }
    }
}