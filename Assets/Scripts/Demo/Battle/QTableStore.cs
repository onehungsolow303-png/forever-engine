using System.IO;
using UnityEngine;

namespace ForeverEngine.Demo.Battle
{
    /// <summary>
    /// Static utility for persisting Q-learning tables across game sessions.
    /// Save path: Application.persistentDataPath/qtable_combat.json
    /// </summary>
    public static class QTableStore
    {
        private const string FileName = "qtable_combat.json";

        [System.Serializable]
        private class QTableData
        {
            public float[] Table;
            public int Episodes;
        }

        private static int _loadedEpisodes;

        /// <summary>How many training episodes the loaded table has accumulated.</summary>
        public static int LoadedEpisodes => _loadedEpisodes;

        private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>
        /// Load the persisted Q-table from disk.
        /// Returns the flat float array, or null if no save file exists or the file is corrupt.
        /// Also populates <see cref="LoadedEpisodes"/>.
        /// </summary>
        public static float[] Load()
        {
            string path = FilePath;
            if (!File.Exists(path))
                return null;

            try
            {
                string json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<QTableData>(json);
                if (data == null || data.Table == null || data.Table.Length == 0)
                    return null;

                _loadedEpisodes = data.Episodes;
                Debug.Log($"[QTableStore] Loaded Q-table ({data.Table.Length} values, {data.Episodes} episodes) from {path}");
                return data.Table;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[QTableStore] Failed to load Q-table from {path}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Persist the Q-table to disk.
        /// </summary>
        /// <param name="table">Flat float array returned by <c>CombatBrain.SaveQTable()</c>.</param>
        /// <param name="episodes">Total episode count to store alongside the table.</param>
        public static void Save(float[] table, int episodes)
        {
            if (table == null || table.Length == 0)
            {
                Debug.LogWarning("[QTableStore] Save called with empty table — skipping.");
                return;
            }

            var data = new QTableData { Table = table, Episodes = episodes };
            try
            {
                string json = JsonUtility.ToJson(data);
                File.WriteAllText(FilePath, json);
                _loadedEpisodes = episodes;
                Debug.Log($"[QTableStore] Saved Q-table ({table.Length} values, {episodes} episodes) to {FilePath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[QTableStore] Failed to save Q-table: {ex.Message}");
            }
        }
    }
}
