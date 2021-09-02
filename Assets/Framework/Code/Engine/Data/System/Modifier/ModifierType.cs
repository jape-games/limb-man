using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Jape
{
    public class ModifierType : BehaviourType
    {
        protected override Mode GetMode() => Mode.Modifier;

        protected new static string Path => "System/Resources/ModifierTypes";

        protected override string TemplatePath => "System/Resources/Templates/Modifiers";

        protected override string DefaultTemplate => "DefaultModifier";

        protected override Type InstanceType => typeof(ModifierInstance<>);
        protected override Type ScriptType => typeof(Modifier);

        [ShowInInspector]
        [PropertyOrder(-1)]
        [DisableIf(nameof(IsScriptSet))]
        [ValueDropdown(nameof(GetElementTypes))]
        public Type TargetType
        {
            get => targetType;
            set
            {
                SetTarget(value);
                targetType = value;
            }
        }

        [SerializeField, HideInInspector]
        private Type targetType;

        private IEnumerable<Type> GetElementTypes() { return Element.Subclass(); }

        private void SetTarget(Type targetType)
        {
            #if UNITY_EDITOR
            UnityEditor.AssetDatabase.RenameAsset(UnityEditor.AssetDatabase.GetAssetPath(this), targetType.CleanName());
            script.SetName(name);
            #endif
        }

        public bool IsScriptSet() { return script.IsSet(); }
    }
}