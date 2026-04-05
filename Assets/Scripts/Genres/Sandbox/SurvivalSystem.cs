using UnityEngine;

namespace ForeverEngine.Genres.Sandbox
{
    public class SurvivalSystem : UnityEngine.MonoBehaviour
    {
        [SerializeField] private float _maxHunger = 100, _maxThirst = 100, _maxStamina = 100;
        [SerializeField] private float _hungerRate = 0.5f, _thirstRate = 0.8f, _staminaRegen = 5f;

        public float Hunger { get; private set; }
        public float Thirst { get; private set; }
        public float Stamina { get; private set; }
        public float Temperature { get; set; } = 20f;
        public bool IsExhausted => Stamina <= 0;
        public bool IsStarving => Hunger >= _maxHunger;
        public bool IsDehydrated => Thirst >= _maxThirst;

        private void Start() { Stamina = _maxStamina; }

        private void Update()
        {
            Hunger = Mathf.Min(_maxHunger, Hunger + _hungerRate * Time.deltaTime);
            Thirst = Mathf.Min(_maxThirst, Thirst + _thirstRate * Time.deltaTime);
            Stamina = Mathf.Min(_maxStamina, Stamina + _staminaRegen * Time.deltaTime);
        }

        public void Eat(float amount) => Hunger = Mathf.Max(0, Hunger - amount);
        public void Drink(float amount) => Thirst = Mathf.Max(0, Thirst - amount);
        public bool UseStamina(float amount) { if (Stamina < amount) return false; Stamina -= amount; return true; }
    }
}
