namespace ForeverEngine.MonoBehaviour.Animation
{
    public class AnimClip
    {
        public string Name; public int FrameCount; public float FrameDuration; public bool Loop;
        public AnimClip(string name, int frameCount, float frameDuration, bool loop) { Name = name; FrameCount = frameCount; FrameDuration = frameDuration; Loop = loop; }
    }

    public class AnimState
    {
        public AnimClip Clip { get; }
        public int CurrentFrame { get; private set; }
        public bool Finished { get; private set; }
        public float SpeedMultiplier { get; set; } = 1f;
        private float _timer;

        public AnimState(AnimClip clip) { Clip = clip; }

        public void Advance(float deltaTime)
        {
            if (Finished) return;
            _timer += deltaTime * SpeedMultiplier;
            while (_timer >= Clip.FrameDuration)
            {
                _timer -= Clip.FrameDuration;
                CurrentFrame++;
                if (CurrentFrame >= Clip.FrameCount)
                {
                    if (Clip.Loop) CurrentFrame = 0;
                    else { CurrentFrame = Clip.FrameCount - 1; Finished = true; return; }
                }
            }
        }

        public void Reset() { CurrentFrame = 0; _timer = 0f; Finished = false; }
    }
}
