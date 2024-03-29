using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Jape
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
	public partial class Properties : Mono
    {
        internal static List<string> runtimeIds = new();

        public new string Key => $"{GetType().FullName}_{gameObject.Identifier()}";

        [OnInspectorInit(nameof(InitInspectorEditor))]

        [PropertySpace(4)]
        [TabGroup("Tabs", "General")]

        [PropertyOrder(-1)]
        [SerializeField]
        [EnableIf(nameof(IsPrefabEditor))]
        [HideLabel]
        internal string alias;
        
        [PropertySpace(2)]
        [HorizontalGroup("Tabs/General/Id")]

        [PropertyOrder(-1)]
        [SerializeField]
        [HideLabel, ReadOnly]
        private string id;
        internal string Id
        {
            get => id;
            set
            {
                if (Game.IsRunning && id != null)
                {
                    if (runtimeIds.Contains(id))
                    {
                        runtimeIds.Remove(id);
                    }
                } 
                id = value;
                if (Game.IsRunning && id != null) { runtimeIds.Add(id); }
            }
        }

        private bool idActive;

        [PropertySpace(4)]
        [HorizontalGroup("Tabs/General/Id", MaxWidth = 42)]

        [PropertyOrder(-1)]
        [ShowInInspector]
        [DisableIf(nameof(IsPrefabEditor))]
        [Button(ButtonHeight = 20)]
        private void Copy() { Id.Copy(); }

        [PropertySpace(8)]
        [TabGroup("Tabs", "General")]

        [SerializeField]
        internal List<Tag> tags = new();

        [PropertySpace(8)]
        [TabGroup("Tabs", "General")]

        [SerializeField]
        internal int player;

        [TabGroup("Tabs", "General")]

        [ShowInInspector, LabelText(" "), HidePicker]
        [HideIf(nameof(HideTeams))]
        [ListDrawerSettings(Expanded = true, IsReadOnly = true)]
        internal Team[] Teams => Team.FindPlayer(player);
        private bool HideTeams => Teams.Length <= 0;

        [TabGroup("Tabs", "Save")]

        [SerializeField]
        internal bool save;

        [TabGroup("Tabs", "Save")]

        [SerializeField]
        [ShowIf(nameof(save))]
        internal bool savePosition = true;

        [PropertySpace(4)]
        [TabGroup("Tabs", "Save")]

        [SerializeField]
        [ShowIf(nameof(save))]
        [OnInspectorGUI(nameof(SetSavedElements))]
        [ListDrawerSettings(IsReadOnly = true, Expanded = true)]
        [LabelText("Elements")]
        internal List<SavedElement> savedElements = new();

        [HideInInspector]
        public Action<GameObject> OnDisabled = delegate {};

        internal Rigidbody Rigidbody => GetComponent<Rigidbody>();
        internal Rigidbody2D Rigidbody2D => GetComponent<Rigidbody2D>();

        internal Collider[] Colliders => GetComponents<Collider>();
        internal Collider2D[] Colliders2D => GetComponents<Collider2D>();

        internal static Properties Create(GameObject gameObject)
        {
            if (gameObject.TryGetComponent(out Properties properties))
            {
                return properties;
            }

            #if UNITY_EDITOR

            if (UnityEditor.PrefabUtility.IsPartOfImmutablePrefab(gameObject))
            {
                Log.Warning("Cannot add properties to immutable prefab");
                return null;
            }

            #endif

            properties = gameObject.AddComponent<Properties>();

            if (properties == null)
            {
                Log.Warning("Error Creating Properties");
            }

            return properties;
        }

        private void SetSavedElements()
        {
            ValidateSaveElements();
            Element[] elements = GetComponents<Element>().Where(e => e.Saved).ToArray();
            savedElements.RemoveAll(e => e.element == null);
            foreach (Element element in elements.Where(element => savedElements.All(e => e.element != element)))
            {
                savedElements.Add(new SavedElement
                {
                    element = element,
                    save = true
                });
            }
        }

        internal void ApplyForce(Vector3 force, ForceMode mode, bool useMass = true)
        {
            if (Rigidbody != null)
            {
                switch (mode)
                {
                    case ForceMode.Default:
                        Rigidbody.AddForce(force, useMass ? UnityEngine.ForceMode.Force : UnityEngine.ForceMode.Acceleration);
                        break;

                    case ForceMode.Instant:
                        Rigidbody.AddForce(force, useMass ? UnityEngine.ForceMode.Impulse : UnityEngine.ForceMode.VelocityChange);
                        break;
                }
                return;
            }

            if (Rigidbody2D != null)
            {
                switch (mode)
                {
                    case ForceMode.Default:
                        Rigidbody2D.AddForce(force, ForceMode2D.Force);
                        break;

                    case ForceMode.Instant:
                        Rigidbody2D.AddForce(force, ForceMode2D.Impulse);
                        break;
                }
            }
        }

        internal bool HasRigidbody()
        {
            return Rigidbody != null || 
                   Rigidbody2D != null;
        }

        internal int ColliderCount()
        {
            return Colliders.Length + Colliders2D.Length;
        }

        private bool NoPlayer() { return player == 0; }

        internal void AddTag(Tag tag) { tags.Add(tag); }
        internal void RemoveTag(Tag tag) { tags.Remove(tag); }
        internal bool HasTag(Tag tag) { return tags.Contains(tag); }

        internal void Send(Element.IReceivable receivable) { foreach (Element element in GetComponents<Element>()) { element.Send(receivable); }}

        private bool CanSave()
        {
            if (string.IsNullOrEmpty(gameObject.Identifier())) { return false; }
            return save;
        }

        internal bool CanSaveElement(Element element)
        {
            if (!save) { return false; }
            if (savedElements.All(e => e.element != element)) { return false; }
            return savedElements.First(e => e.element == element).save;
        }

        internal void SaveAll()
        {
            if (!CanSave()) { Log.Warning($"Unable to SaveAll() on {gameObject.name}"); }
            foreach (SavedElement element in savedElements) { element.element.Save(); }
            Save();
        }

        public void Save()
        {
            if (!CanSave()) { return; }

            Status status = new()
                { Key = Key };

            if (gameObject.Properties().savePosition) { status.position = transform.position; }

            Jape.Status.Save(status);
        }

        public void Load()
        {
            if (!CanSave()) { return; }

            Status status = Jape.Status.Load<Status>(Key);

            if (status == null) { return; }

            if (gameObject.Properties().savePosition) { transform.position = status.position; }
        }

        private void ValidateSaveElements()
        {
            foreach (SavedElement element in savedElements)
            {
                SavedElement[] matches = savedElements.Where
                (
                    e => element.save 
                    && e.save 
                    && e.element.GetType() == element.element.GetType()
                ).ToArray();

                if (matches.Length <= 1) { continue; } 

                Log.Warning($"Cannot have multiple saved {element.element.GetType()} elements", "Deactivated saving for both elements");

                foreach (SavedElement match in matches)
                {
                    match.save = false;
                }
            }
        }

        internal override void Awake()
        {
            if (Game.IsRunning)
            {
                SaveManager.Instance.OnSaveRequest += Save;
                SaveManager.Instance.OnLoadResponse += Load;
                Load();
            }

            base.Awake(); // Last //
        }

        internal override void OnDestroy()
        {
            base.OnDestroy(); // First //

            if (Game.IsRunning)
            {
                if (SaveManager.Instance != null)
                {
                    SaveManager.Instance.OnSaveRequest -= Save;
                    SaveManager.Instance.OnLoadResponse -= Load;
                }
            }
        }

        internal override void OnDisable()
        {
            base.OnDisable(); // First //
            if (!Game.IsRunning) { return; }
            if (!gameObject.activeSelf)
            {
                OnDisabled?.Invoke(gameObject);
            }
        }

        public void InitInspectorEditor()
        {
            #if UNITY_EDITOR
            if (!IsPrefabEditor()) { return; }
            Id = null;
            #endif
        }

        protected override void FrameEditor()
        {
            #if UNITY_EDITOR
            OrderEditor();
            #endif
        }

        private void OrderEditor()
        {
            #if UNITY_EDITOR
            List<Component> components = gameObject.GetComponents<Component>().ToList();
            int index = components.FindIndex(c => c == this);
            Enumeration.Repeat(index - 1, () => UnityEditorInternal.ComponentUtility.MoveComponentUp(this));
            #endif
        }

        internal bool IsPrefabEditor()
        {
            #if UNITY_EDITOR
            return UnityEditor.PrefabUtility.GetPrefabAssetType(gameObject) != UnityEditor.PrefabAssetType.NotAPrefab
                   && UnityEditor.PrefabUtility.GetPrefabInstanceStatus(gameObject) == UnityEditor.PrefabInstanceStatus.NotAPrefab
                   || UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject) != null;
            #else
            return false;
            #endif
        }

        public void SetDirtyEditor()
        {
            if (Game.IsRunning) { return; }

            #if UNITY_EDITOR

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(Game.ActiveScene());
            UnityEditor.EditorUtility.SetDirty(this);

            #endif
        }
        
        /// <summary>
        /// Generate and assign a unique id
        /// </summary>
        public void GenerateId() { Id = Ids.Generate(); }

        protected override void EnabledEditor()
        {
            #if UNITY_EDITOR

            if (IsPrefabEditor()) { return; }
            idActive = true;
            RegisterEditor();
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaved += SceneSave;

            #endif
        }


        protected override void DestroyedEditor()
        {
            #if UNITY_EDITOR

            if (IsPrefabEditor()) { return; }
            idActive = false;
            UnregisterEditor();
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaved -= SceneSave;

            #endif
        }

        protected override void Cloned()
        {
            #if UNITY_EDITOR

            if (Ids.Has(Id))
            {
                Id = null;
                RegisterEditor();
            }

            #endif
        }

        protected override void Validated()
        {
            #if UNITY_EDITOR

            RefreshEditor();

            #endif
        }

        private void RegisterEditor()
        {
            #if UNITY_EDITOR

            if (!string.IsNullOrEmpty(Id))
            {
                if (Ids.Has(Id))
                {
                    if (Ids.Get(Id).Map.IsSame(Map.GetActive()))
                    {
                        RefreshEditor();
                    } 
                    else
                    {
                        GenerateId();
                        Ids.Add(this);
                        UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += OnSceneOpenEditor;
                    }
                }
                else
                {
                    Ids.Add(this);
                }
            }
            else
            {
                GenerateId();
                Ids.Add(this);
            }

            void OnSceneOpenEditor(Scene scene, UnityEditor.SceneManagement.OpenSceneMode mode)
            {
                UnityEditor.SceneManagement.EditorSceneManager.sceneOpened -= OnSceneOpenEditor;
                SetDirtyEditor();
            }

            #endif
        }

        private void UnregisterEditor()
        {
            #if UNITY_EDITOR

            if (!gameObject.scene.isLoaded) { return; } 
            Ids.Remove(this);

            #endif
        }

        private void RefreshEditor()
        {
            #if UNITY_EDITOR

            if (!idActive) { return; }
            if (string.IsNullOrEmpty(Id)) { return; }
            if (!Ids.Has(this)) { return; }
            Ids.Refresh(this);

            #endif
        }

        private void SceneSave(Scene scene) { RefreshEditor(); }
    }
}