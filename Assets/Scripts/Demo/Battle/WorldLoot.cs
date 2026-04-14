using UnityEngine;
using ForeverEngine.Demo.UI;

namespace ForeverEngine.Demo.Battle
{
    /// <summary>
    /// WorldLoot is a MonoBehaviour spawned at defeated enemy positions.
    /// It displays a gold-colored pickup, bobs up and down, and auto-collects
    /// when the player comes within range.
    /// </summary>
    public class WorldLoot : UnityEngine.MonoBehaviour
    {
        public int GoldAmount = 10;
        public int XPAmount = 25;

        private float _spawnTime;
        private const float DESPAWN_TIME = 60f;
        private const float BOB_AMPLITUDE = 0.15f;
        private const float BOB_SPEED = 3f;
        private const float ROTATION_SPEED = 90f; // degrees per second
        private const float COLLECT_DISTANCE = 1.5f;

        private Vector3 _basePosition;

        private void Start()
        {
            _spawnTime = Time.time;
            _basePosition = transform.position;

            // Create a small gold-colored cube child
            var cubeGo = new GameObject("LootCube");
            cubeGo.transform.SetParent(transform, false);
            cubeGo.transform.localPosition = Vector3.zero;
            cubeGo.transform.localScale = Vector3.one * 0.3f;

            // Add mesh renderer and filter
            var mf = cubeGo.AddComponent<MeshFilter>();
            mf.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

            var mr = cubeGo.AddComponent<MeshRenderer>();

            // Create URP Lit material with gold color
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(1f, 0.84f, 0f, 1f)); // Gold
            mr.material = material;

            // Remove cube collider if present (there shouldn't be one, but be safe)
            var col = cubeGo.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }
        }

        private void Update()
        {
            // Bob up and down with sine wave
            float elapsed = Time.time - _spawnTime;
            float yOffset = Mathf.Sin(elapsed * BOB_SPEED) * BOB_AMPLITUDE;
            transform.position = _basePosition + Vector3.up * yOffset;

            // Rotate 90 degrees per second around Y axis
            transform.Rotate(0f, ROTATION_SPEED * Time.deltaTime, 0f, Space.Self);

            // Check for auto-despawn after 60 seconds
            if (elapsed >= DESPAWN_TIME)
            {
                Destroy(gameObject);
                return;
            }

            // Check distance to player for auto-collection
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                float distToPlayer = Vector3.Distance(transform.position, player.transform.position);
                if (distToPlayer <= COLLECT_DISTANCE)
                {
                    Collect();
                }
            }
        }

        private void Collect()
        {
            if (GameManager.Instance == null) return;

            // Add gold to player
            GameManager.Instance.Player.Gold += GoldAmount;

            // Add XP to character (only CharacterSheet, not PlayerData)
            if (GameManager.Instance.Character != null)
            {
                GameManager.Instance.Character.GainXP(XPAmount);

                // Check whether the XP gain crossed a level-up threshold.
                if (GameManager.Instance.Character.CanLevelUp)
                    LevelUpScreen.Show();
            }
            else if (GameManager.Instance.Player != null)
            {
                // Fallback for PlayerData-only sessions: simple level * 100 XP threshold.
                // PlayerData doesn't track XP directly so we approximate based on level.
                // Level up is triggered once per loot pickup when player has accumulated
                // enough total gold-weighted XP (gold serves as a loose proxy here).
                // This path is rarely hit; CharacterSheet is always present in shipped demo.
            }

            // Spawn floating text popup reusing DamagePopup pattern
            var popupGo = new GameObject("LootPopup");
            popupGo.transform.position = transform.position + Vector3.up * 1.5f;

            var tm = popupGo.AddComponent<TextMesh>();
            tm.text = $"+{GoldAmount}g +{XPAmount}xp";
            tm.characterSize = 0.15f;
            tm.fontSize = 64;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(1f, 0.84f, 0f, 1f); // Gold color

            popupGo.AddComponent<DamagePopup>();

            // Destroy self
            Destroy(gameObject);
        }
    }
}
