using UnityEngine;
using Unity.Entities;
using ECSWorld = Unity.Entities.World;
using System.IO;
using ForeverEngine.ECS.Components;
using ForeverEngine.ECS.Systems;
using ForeverEngine.MonoBehaviour.Bootstrap;

namespace ForeverEngine.MonoBehaviour.ContentLoader
{
    /// <summary>
    /// Receives completed generation responses and feeds them into the ECS world.
    ///
    /// Dungeon responses  → pass map_data.json path to MapImporter for full entity spawn.
    /// Encounter responses → spawn ECS encounter entities at runtime.
    /// NPC responses       → create NPC entities with NPCPersonalityComponent.
    /// Treasure responses  → create loot container entities with LootTableComponent.
    ///
    /// Also polls for EncounterActivatedTag each frame and handles monster spawning.
    /// </summary>
    public class HotContentLoader : UnityEngine.MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MapImporter mapImporter;

        // ── Public entry point (called by ContentRequestQueue) ─────────────

        public void HandleGenerationResponse(GenerationRequest request, string responseJson)
        {
            var response = JsonUtility.FromJson<GenerationResponse>(responseJson);
            if (response == null || !response.success)
            {
                Debug.LogWarning($"[HotContentLoader] Response parse failed for request '{request?.id}'. " +
                                 $"Error: {response?.errorMessage}");
                return;
            }

            switch (response.type)
            {
                case "dungeon":
                case "village":
                case "town":
                case "city":
                case "wilderness":
                case "castle":
                case "cave":
                case "fort":
                case "camp":
                case "temple":
                    LoadDungeon(response);
                    break;
                case "encounter":
                    LoadEncounter(response);
                    break;
                case "npc":
                    LoadNPC(response);
                    break;
                case "treasure":
                    LoadTreasure(response);
                    break;
                default:
                    Debug.LogWarning($"[HotContentLoader] Unknown response type: '{response.type}'");
                    break;
            }
        }

        /// <summary>
        /// Directly loads a map_data.json file — useful for initial load and testing.
        /// </summary>
        public void LoadContent(string mapDataPath)
        {
            if (!File.Exists(mapDataPath))
            {
                Debug.LogError($"[HotContentLoader] File not found: {mapDataPath}");
                return;
            }

            if (mapImporter == null)
            {
                Debug.LogError("[HotContentLoader] MapImporter reference is null. Assign in Inspector.");
                return;
            }

            var em = ECSWorld.DefaultGameObjectInjectionWorld.EntityManager;
            mapImporter.Import(mapDataPath, em);
        }

        // ── Unity lifecycle ────────────────────────────────────────────────

        private void Update()
        {
            // Poll for EncounterActivatedTag and spawn monsters
            ProcessActivatedEncounters();
        }

        // ── Internal handlers ──────────────────────────────────────────────

        private void LoadDungeon(GenerationResponse response)
        {
            if (string.IsNullOrEmpty(response.dataPath))
            {
                Debug.LogWarning("[HotContentLoader] Dungeon response has no dataPath.");
                return;
            }
            LoadContent(response.dataPath);
        }

        private void LoadEncounter(GenerationResponse response)
        {
            if (string.IsNullOrEmpty(response.encounterJson)) return;

            // TODO: Parse encounterJson (from GM module EncounterData schema)
            // and create an EncounterComponent entity at the specified position.
            // Full implementation when GM module JSON schema is finalised in Plan 5.
            Debug.Log($"[HotContentLoader] Encounter response received (inline JSON). " +
                      $"Parsing deferred to Plan 5 schema integration.");
        }

        private void LoadNPC(GenerationResponse response)
        {
            if (string.IsNullOrEmpty(response.npcJson)) return;

            // TODO: Parse npcJson and create an entity with NPCPersonalityComponent.
            Debug.Log($"[HotContentLoader] NPC response received. Parsing deferred to Plan 5.");
        }

        private void LoadTreasure(GenerationResponse response)
        {
            if (string.IsNullOrEmpty(response.treasureJson)) return;

            // TODO: Parse treasureJson and update existing LootTableComponent entities.
            Debug.Log($"[HotContentLoader] Treasure response received. Parsing deferred to Plan 5.");
        }

        private void ProcessActivatedEncounters()
        {
            var world = ECSWorld.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var em = world.EntityManager;

            // Find encounter entities tagged as activated (one-frame tag from EncounterTriggerSystem)
            using var query = em.CreateEntityQuery(
                ComponentType.ReadWrite<EncounterComponent>(),
                ComponentType.ReadOnly<EncounterActivatedTag>());

            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);

            foreach (var entity in entities)
            {
                var encounter = em.GetComponentData<EncounterComponent>(entity);
                SpawnEncounterMonsters(em, encounter, entity);
                em.RemoveComponent<EncounterActivatedTag>(entity);
            }
        }

        private void SpawnEncounterMonsters(EntityManager em, EncounterComponent encounter, Entity encounterEntity)
        {
            // TODO: Look up MonsterDatabase (passed as singleton or injected) for MonsterTemplateId.
            // For now: log intent and place placeholder. Full spawn in Plan 5.
            Debug.Log($"[HotContentLoader] Spawning {encounter.MonsterCount} '{encounter.MonsterTemplateId}' " +
                      $"for encounter '{encounter.EncounterId}'.");

            // The actual monster entity creation mirrors MapImporter.SpawnCreature().
            // It will be implemented in Plan 5 once MonsterDatabase is wired as a singleton
            // accessible from MonoBehaviour context.
        }
    }
}
