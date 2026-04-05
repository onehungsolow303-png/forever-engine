using UnityEngine;

namespace ForeverEngine.MonoBehaviour.SaveLoad
{
    [System.Serializable]
    public class SaveData
    {
        public string MapPath;
        public int PlayerX, PlayerY, PlayerZ;
        public int PlayerHP, PlayerMaxHP;
        public int Gold;
        public string[] ActiveQuests;
        public string[] CompletedQuests;
        public string SaveTimestamp;

        public static string ToJson(SaveData data) { data.SaveTimestamp = System.DateTime.UtcNow.ToString("o"); return JsonUtility.ToJson(data, true); }
        public static SaveData FromJson(string json) => JsonUtility.FromJson<SaveData>(json);
    }
}
