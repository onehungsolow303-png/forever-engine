using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace ForeverEngine.Genres.Adventure
{
    [System.Serializable]
    public class CinematicStep { public enum StepType { MoveTo, LookAt, Wait, Dialogue, FadeIn, FadeOut } public StepType Type; public Vector3 Target; public float Duration = 1f; public string DialogueText; }

    public class CinematicSystem : MonoBehaviour
    {
        public static CinematicSystem Instance { get; private set; }
        public bool IsPlaying { get; private set; }

        private void Awake() => Instance = this;

        public void Play(List<CinematicStep> steps, Camera cam)
        {
            if (IsPlaying) return;
            StartCoroutine(PlaySequence(steps, cam));
        }

        public void Skip() { StopAllCoroutines(); IsPlaying = false; }

        private IEnumerator PlaySequence(List<CinematicStep> steps, Camera cam)
        {
            IsPlaying = true;
            foreach (var step in steps)
            {
                switch (step.Type)
                {
                    case CinematicStep.StepType.MoveTo:
                        float t = 0;
                        Vector3 start = cam.transform.position;
                        while (t < step.Duration) { t += Time.deltaTime; cam.transform.position = Vector3.Lerp(start, step.Target, t / step.Duration); yield return null; }
                        break;
                    case CinematicStep.StepType.LookAt:
                        cam.transform.LookAt(step.Target);
                        break;
                    case CinematicStep.StepType.Wait:
                        yield return new WaitForSeconds(step.Duration);
                        break;
                    case CinematicStep.StepType.Dialogue:
                        Debug.Log($"[Cinematic] {step.DialogueText}");
                        yield return new WaitForSeconds(step.Duration);
                        break;
                }
            }
            IsPlaying = false;
        }
    }
}
