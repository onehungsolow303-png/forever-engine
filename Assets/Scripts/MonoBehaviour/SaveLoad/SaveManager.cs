using UnityEngine;
using Unity.Entities;
using System.IO;
using ForeverEngine.ECS.Components;
using ForeverEngine.ECS.Data;

namespace ForeverEngine.MonoBehaviour.SaveLoad
{
    public class SaveManager : UnityEngine.MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }
        private string SaveDirectory => Path.Combine(Application.persistentDataPath, "saves");

        private void Awake() { Instance = this; Directory.CreateDirectory(SaveDirectory); }

        public void Save(string slotName = "quicksave")
        {
            var data = CaptureState();
            string json = SaveData.ToJson(data);
            File.WriteAllText(Path.Combine(SaveDirectory, $"{slotName}.json"), json);
            Debug.Log($"[SaveManager] Saved to {slotName}");
        }

        public bool Load(string slotName = "quicksave")
        {
            string path = Path.Combine(SaveDirectory, $"{slotName}.json");
            if (!File.Exists(path)) return false;
            var data = SaveData.FromJson(File.ReadAllText(path));
            RestoreState(data);
            Debug.Log($"[SaveManager] Loaded {slotName}");
            return true;
        }

        public string[] GetSaveSlots()
        {
            var files = Directory.GetFiles(SaveDirectory, "*.json");
            var names = new string[files.Length];
            for (int i = 0; i < files.Length; i++) names[i] = Path.GetFileNameWithoutExtension(files[i]);
            return names;
        }

        private SaveData CaptureState()
        {
            var data = new SaveData();
            var em = World.DefaultGameObjectInjectionWorld?.EntityManager;
            if (em == null) return data;
            var query = em.Value.CreateEntityQuery(typeof(PlayerTag), typeof(PositionComponent), typeof(StatsComponent));
            if (query.CalculateEntityCount() > 0)
            {
                var pos = query.GetSingleton<PositionComponent>();
                var stats = query.GetSingleton<StatsComponent>();
                data.PlayerX = pos.X; data.PlayerY = pos.Y; data.PlayerZ = pos.Z;
                data.PlayerHP = stats.HP; data.PlayerMaxHP = stats.MaxHP;
            }
            return data;
        }

        private void RestoreState(SaveData data)
        {
            var em = World.DefaultGameObjectInjectionWorld?.EntityManager;
            if (em == null) return;
            var query = em.Value.CreateEntityQuery(typeof(PlayerTag), typeof(PositionComponent), typeof(StatsComponent));
            if (query.CalculateEntityCount() > 0)
            {
                var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
                em.Value.SetComponentData(entities[0], new PositionComponent { X = data.PlayerX, Y = data.PlayerY, Z = data.PlayerZ });
                var stats = em.Value.GetComponentData<StatsComponent>(entities[0]);
                stats.HP = data.PlayerHP; stats.MaxHP = data.PlayerMaxHP;
                em.Value.SetComponentData(entities[0], stats);
                entities.Dispose();
            }
        }
    }
}
