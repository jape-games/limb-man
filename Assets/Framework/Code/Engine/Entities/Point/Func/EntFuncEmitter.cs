using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Jape
{
    [AddComponentMenu("")]
    public class EntFuncEmitter : EntFunc
    {
        protected override Texture Icon => GetIcon("IconEmitter");

        [SerializeField] [AssetsOnly] private GameObject prefab = null;

        [Space(8)]

        [SerializeField] public bool autoStart = false;
        [SerializeField] private bool retrigger = true;

        [Space(8)]

        [SerializeField] 
        [ShowIf(nameof(retrigger))] 
        [HideLabel] 
        private Time.Interval interval = new Time.Interval(Time.Counter.Seconds, 0);

        [Space(8)]

        [SerializeField] private int amount = 0;

        [Space(8)]

        [SerializeField] private bool parent = false;

        private Timer timer;

        protected override void Activated() { timer = CreateTimer(); }
        protected override void Init() { if (autoStart) { EmitStart(); }}

        [Route]
        public void EmitStart()
        {
            if (timer.IsProcessing()) { this.Log().Response("Cannot start when already emitting"); return; }
            if (retrigger)
            {
                Spawn(amount);
                timer.Set(interval.Value(), interval.Counter).
                      ChangeMode(Timer.Mode.Loop).
                      IterationAction(() => Spawn(amount)).
                      Start();
            } 
            else { Spawn(amount); }
        }

        [Route]
        public void Burst(int amount) { Spawn(amount); }

        [Route]
        public void EmitStop() { timer.Stop(); }

        private void Spawn(int amount)
        {
            if (prefab == null) { return; }
            Transform parent = this.parent ? transform : null;
            Enumeration.Repeat(amount, () => Game.CloneGameObject(prefab, transform.position, transform.rotation, parent));
        }
    }
}