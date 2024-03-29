using UnityEngine;

namespace Jape
{
    [AddComponentMenu("")]
    public class EntFuncTransform : EntFunc
    {
        protected override Texture2D Icon => GetIcon("IconTransform");

        private Timer moveTimer;
        private Timer rotateTimer;
        private Timer resizeTimer;

        protected override void Activated()
        {
            moveTimer = CreateTimer();
            rotateTimer = CreateTimer();
            resizeTimer = CreateTimer();
        }

        [Route]
        public void SetPosition(float x, float y, float z) { transform.localPosition = new Vector3(x, y, z); }

        [Route]
        public void SetRotation(float x, float y, float z) { transform.localRotation = Quaternion.Euler(x, y, z); }

        [Route]
        public void SetScale(float x, float y, float z) { transform.localScale = new Vector3(x, y, z); }

        [Route]
        public void Move(float seconds, float x, float y, float z)
        {
            if (moveTimer.IsProcessing()) { this.Log().Response("Cannot move when already moving"); }
            Vector3 position = transform.localPosition;
            moveTimer.Set(seconds).IntervalAction(Transform).Start();
            void Transform(Timer timer) { transform.localPosition = Vector3.Lerp(position, position + new Vector3(x, y, z), timer.Progress()); }
        }

        [Route]
        public void Rotate(float seconds, float x, float y, float z)
        {
            if (rotateTimer.IsProcessing()) { this.Log().Response("Cannot rotate when already rotating"); }
            Quaternion rotation = transform.localRotation;
            rotateTimer.Set(seconds).IntervalAction(Transform).Start();
            void Transform(Timer timer) { transform.localRotation = Quaternion.Lerp(rotation, rotation * Quaternion.Euler(x, y, z), timer.Progress()); }
        }

        [Route]
        public void Scale(float seconds, float x, float y, float z)
        {
            if (resizeTimer.IsProcessing()) { this.Log().Response("Cannot scale when already scaling"); }
            Vector3 scale = transform.localScale;
            resizeTimer.Set(seconds).IntervalAction(Transform).Start();
            void Transform(Timer timer) { transform.localScale = Vector3.Lerp(scale, scale + new Vector3(x, y, z), timer.Progress()); }
        }
    }
}