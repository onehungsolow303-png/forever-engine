using UnityEngine;

namespace ForeverEngine.Genres.Sandbox
{
    public class DayNightCycle : MonoBehaviour
    {
        public static DayNightCycle Instance { get; private set; }
        [SerializeField] private float _dayLengthMinutes = 10f;
        [SerializeField] private Light _sunLight;

        public float TimeOfDay { get; private set; } // 0-1 (0=midnight, 0.5=noon)
        public bool IsDay => TimeOfDay > 0.25f && TimeOfDay < 0.75f;
        public bool IsNight => !IsDay;
        public int DayCount { get; private set; }

        private void Awake() => Instance = this;

        private void Update()
        {
            float prevTime = TimeOfDay;
            TimeOfDay = (TimeOfDay + Time.deltaTime / (_dayLengthMinutes * 60f)) % 1f;
            if (TimeOfDay < prevTime) DayCount++;

            if (_sunLight != null)
            {
                _sunLight.transform.rotation = Quaternion.Euler(TimeOfDay * 360f - 90f, 170f, 0);
                float intensity = Mathf.Clamp01(Mathf.Sin(TimeOfDay * Mathf.PI));
                _sunLight.intensity = intensity;
                _sunLight.color = Color.Lerp(new Color(1f, 0.5f, 0.2f), Color.white, intensity);
            }
        }

        public void SetTime(float time01) => TimeOfDay = Mathf.Clamp01(time01);
    }
}
