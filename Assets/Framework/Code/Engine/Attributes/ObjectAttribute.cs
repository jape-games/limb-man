using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jape
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class ObjectAttribute : Attribute
    {
        internal List<string> MethodNames = new List<string>();
        internal bool HidePicker = false;
        internal int PickerMode = 0;
    }
}