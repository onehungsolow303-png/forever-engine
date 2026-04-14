#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ForeverEngine.MonoBehaviour.Audio;

/// <summary>
/// Editor utility that discovers AudioClip assets from installed packs and
/// populates the AudioConfig ScriptableObject at Assets/Resources/AudioConfig.asset.
/// Run from: Forever Engine → Populate Audio Config.
/// </summary>
public static class AudioPopulator
{
    // ── Field mapping rules ───────────────────────────────────────────────
    // Each rule: (name pattern, target field)
    // Arrays (HitSounds, MissSounds, etc.) accumulate; singles take first match.

    private enum FieldTarget
    {
        HitSounds,
        MissSounds,
        FootstepSounds,
        DeathSounds,
        UIClickSounds,
        DoorOpenSound,
        LevelUpSound,
        QuestCompleteSound,
        ExplorationMusic,
        CombatMusic,
        MenuMusic,
        DungeonAmbient,
        CaveAmbient,
        ForestAmbient,
    }

    private static readonly (string pattern, FieldTarget target)[] Rules =
    {
        // Hit
        ("_hit",            FieldTarget.HitSounds),
        ("hit_",            FieldTarget.HitSounds),
        ("Hit_",            FieldTarget.HitSounds),
        ("_Hit",            FieldTarget.HitSounds),
        ("impact",          FieldTarget.HitSounds),
        ("Impact",          FieldTarget.HitSounds),
        ("sword_swing",     FieldTarget.HitSounds),
        ("SwordSwing",      FieldTarget.HitSounds),
        ("attack",          FieldTarget.HitSounds),
        ("Attack",          FieldTarget.HitSounds),

        // Miss
        ("miss",            FieldTarget.MissSounds),
        ("Miss",            FieldTarget.MissSounds),
        ("whoosh",          FieldTarget.MissSounds),
        ("Whoosh",          FieldTarget.MissSounds),
        ("swipe",           FieldTarget.MissSounds),

        // Footstep
        ("footstep",        FieldTarget.FootstepSounds),
        ("Footstep",        FieldTarget.FootstepSounds),
        ("step",            FieldTarget.FootstepSounds),
        ("Step",            FieldTarget.FootstepSounds),
        ("walk",            FieldTarget.FootstepSounds),
        ("Walk",            FieldTarget.FootstepSounds),

        // Death
        ("death",           FieldTarget.DeathSounds),
        ("Death",           FieldTarget.DeathSounds),
        ("die",             FieldTarget.DeathSounds),
        ("Die",             FieldTarget.DeathSounds),

        // UI Click
        ("click",           FieldTarget.UIClickSounds),
        ("Click",           FieldTarget.UIClickSounds),
        ("button",          FieldTarget.UIClickSounds),
        ("Button",          FieldTarget.UIClickSounds),
        ("ui_",             FieldTarget.UIClickSounds),
        ("UI_",             FieldTarget.UIClickSounds),

        // Door
        ("door",            FieldTarget.DoorOpenSound),
        ("Door",            FieldTarget.DoorOpenSound),
        ("gate",            FieldTarget.DoorOpenSound),
        ("Gate",            FieldTarget.DoorOpenSound),

        // Level Up
        ("levelup",         FieldTarget.LevelUpSound),
        ("level_up",        FieldTarget.LevelUpSound),
        ("LevelUp",         FieldTarget.LevelUpSound),
        ("level up",        FieldTarget.LevelUpSound),

        // Quest Complete
        ("quest",           FieldTarget.QuestCompleteSound),
        ("Quest",           FieldTarget.QuestCompleteSound),
        ("complete",        FieldTarget.QuestCompleteSound),
        ("Complete",        FieldTarget.QuestCompleteSound),
        ("fanfare",         FieldTarget.QuestCompleteSound),
        ("Fanfare",         FieldTarget.QuestCompleteSound),

        // Exploration Music
        ("explore",         FieldTarget.ExplorationMusic),
        ("Explore",         FieldTarget.ExplorationMusic),
        ("exploration",     FieldTarget.ExplorationMusic),
        ("Exploration",     FieldTarget.ExplorationMusic),
        ("overworld",       FieldTarget.ExplorationMusic),
        ("Overworld",       FieldTarget.ExplorationMusic),
        ("ambient_music",   FieldTarget.ExplorationMusic),

        // Combat Music
        ("combat",          FieldTarget.CombatMusic),
        ("Combat",          FieldTarget.CombatMusic),
        ("battle",          FieldTarget.CombatMusic),
        ("Battle",          FieldTarget.CombatMusic),
        ("fight",           FieldTarget.CombatMusic),
        ("Fight",           FieldTarget.CombatMusic),

        // Menu Music
        ("menu",            FieldTarget.MenuMusic),
        ("Menu",            FieldTarget.MenuMusic),
        ("title",           FieldTarget.MenuMusic),
        ("Title",           FieldTarget.MenuMusic),
        ("main_theme",      FieldTarget.MenuMusic),
        ("MainTheme",       FieldTarget.MenuMusic),

        // Dungeon Ambient
        ("dungeon",         FieldTarget.DungeonAmbient),
        ("Dungeon",         FieldTarget.DungeonAmbient),
        ("underground",     FieldTarget.DungeonAmbient),
        ("Underground",     FieldTarget.DungeonAmbient),

        // Cave Ambient
        ("cave",            FieldTarget.CaveAmbient),
        ("Cave",            FieldTarget.CaveAmbient),
        ("drip",            FieldTarget.CaveAmbient),
        ("Drip",            FieldTarget.CaveAmbient),

        // Forest Ambient
        ("forest",          FieldTarget.ForestAmbient),
        ("Forest",          FieldTarget.ForestAmbient),
        ("nature",          FieldTarget.ForestAmbient),
        ("Nature",          FieldTarget.ForestAmbient),
        ("birds",           FieldTarget.ForestAmbient),
        ("Birds",           FieldTarget.ForestAmbient),
    };

    private static readonly HashSet<FieldTarget> ArrayFields = new HashSet<FieldTarget>
    {
        FieldTarget.HitSounds,
        FieldTarget.MissSounds,
        FieldTarget.FootstepSounds,
        FieldTarget.DeathSounds,
        FieldTarget.UIClickSounds,
    };

    // ── Menu entry ────────────────────────────────────────────────────────

    [MenuItem("Forever Engine/Populate Audio Config")]
    public static void Populate()
    {
        const string assetPath = "Assets/Resources/AudioConfig.asset";

        var config = AssetDatabase.LoadAssetAtPath<AudioConfig>(assetPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<AudioConfig>();
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            AssetDatabase.CreateAsset(config, assetPath);
            Debug.Log("[AudioPopulator] Created new AudioConfig.asset");
        }

        // Accumulate array candidates and single-slot candidates
        var arrayBuckets = new Dictionary<FieldTarget, List<AudioClip>>();
        var singleSlots  = new Dictionary<FieldTarget, AudioClip>();

        foreach (FieldTarget ft in System.Enum.GetValues(typeof(FieldTarget)))
            if (ArrayFields.Contains(ft))
                arrayBuckets[ft] = new List<AudioClip>();

        string[] guids = AssetDatabase.FindAssets("t:AudioClip");
        int scanned = 0;
        int assigned = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) continue;

            scanned++;
            string name = clip.name;

            foreach (var (pattern, target) in Rules)
            {
                if (!name.Contains(pattern)) continue;

                if (ArrayFields.Contains(target))
                {
                    arrayBuckets[target].Add(clip);
                    assigned++;
                }
                else if (!singleSlots.ContainsKey(target))
                {
                    singleSlots[target] = clip;
                    assigned++;
                }
                break; // first matching rule wins
            }
        }

        // Write array fields
        config.HitSounds      = arrayBuckets[FieldTarget.HitSounds     ].ToArray();
        config.MissSounds     = arrayBuckets[FieldTarget.MissSounds    ].ToArray();
        config.FootstepSounds = arrayBuckets[FieldTarget.FootstepSounds].ToArray();
        config.DeathSounds    = arrayBuckets[FieldTarget.DeathSounds   ].ToArray();
        config.UIClickSounds  = arrayBuckets[FieldTarget.UIClickSounds ].ToArray();

        // Write single-slot fields (only if a clip was found — never null-out existing)
        if (singleSlots.TryGetValue(FieldTarget.DoorOpenSound,       out var c)) config.DoorOpenSound       = c;
        if (singleSlots.TryGetValue(FieldTarget.LevelUpSound,        out c))     config.LevelUpSound        = c;
        if (singleSlots.TryGetValue(FieldTarget.QuestCompleteSound,  out c))     config.QuestCompleteSound  = c;
        if (singleSlots.TryGetValue(FieldTarget.ExplorationMusic,    out c))     config.ExplorationMusic    = c;
        if (singleSlots.TryGetValue(FieldTarget.CombatMusic,         out c))     config.CombatMusic         = c;
        if (singleSlots.TryGetValue(FieldTarget.MenuMusic,           out c))     config.MenuMusic           = c;
        if (singleSlots.TryGetValue(FieldTarget.DungeonAmbient,      out c))     config.DungeonAmbient      = c;
        if (singleSlots.TryGetValue(FieldTarget.CaveAmbient,         out c))     config.CaveAmbient         = c;
        if (singleSlots.TryGetValue(FieldTarget.ForestAmbient,       out c))     config.ForestAmbient       = c;

        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();

        Debug.Log($"[AudioPopulator] Scanned {scanned} clips, assigned {assigned} to AudioConfig " +
                  $"(Hit:{config.HitSounds?.Length ?? 0} Miss:{config.MissSounds?.Length ?? 0} " +
                  $"Footstep:{config.FootstepSounds?.Length ?? 0} Death:{config.DeathSounds?.Length ?? 0} " +
                  $"UIClick:{config.UIClickSounds?.Length ?? 0})");
    }
}
#endif
