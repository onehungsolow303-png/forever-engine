using UnityEngine;
using UnityEditor;
using ForeverEngine.Generation;
using ForeverEngine.Generation.Data;

namespace ForeverEngine.Editor
{
    public static class GenerationTab
    {
        private static string _mapType = "dungeon";
        private static string _biome = "cave";
        private static int _width = 64, _height = 64, _seed = 42;
        private static int _partyLevel = 3, _partySize = 4;
        private static PipelineCoordinator.GenerationResult _lastResult;
        private static string _status = "Ready";

        public static void Draw()
        {
            EditorGUILayout.LabelField("Map Generation", EditorStyles.boldLabel);

            _mapType = EditorGUILayout.TextField("Map Type", _mapType);
            _biome = EditorGUILayout.TextField("Biome", _biome);
            _width = EditorGUILayout.IntSlider("Width", _width, 32, 512);
            _height = EditorGUILayout.IntSlider("Height", _height, 32, 512);
            _seed = EditorGUILayout.IntField("Seed", _seed);
            _partyLevel = EditorGUILayout.IntSlider("Party Level", _partyLevel, 1, 20);
            _partySize = EditorGUILayout.IntSlider("Party Size", _partySize, 1, 8);

            EditorGUILayout.Space();
            if (GUILayout.Button("Generate Map", GUILayout.Height(30)))
            {
                _status = "Generating...";
                var request = new MapGenerationRequest
                {
                    MapType = _mapType, Biome = _biome,
                    Width = _width, Height = _height, Seed = _seed,
                    PartyLevel = _partyLevel, PartySize = _partySize
                };
                _lastResult = PipelineCoordinator.Generate(request);
                _status = _lastResult.Success
                    ? $"Done: {_lastResult.Layout.Nodes.Count} rooms, {_lastResult.Population.Encounters.Count} encounters"
                    : $"Failed: {_lastResult.Error}";
            }

            if (GUILayout.Button("Randomize Seed")) _seed = Random.Range(1, 99999);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(_status, _lastResult.Success ? MessageType.Info : MessageType.Warning);

            if (_lastResult.Success)
            {
                EditorGUILayout.LabelField($"Rooms: {_lastResult.Layout.Nodes.Count}");
                EditorGUILayout.LabelField($"Encounters: {_lastResult.Population.Encounters.Count}");
                EditorGUILayout.LabelField($"Traps: {_lastResult.Population.Traps.Count}");
                EditorGUILayout.LabelField($"Loot: {_lastResult.Population.Loot.Count}");
                EditorGUILayout.LabelField($"Dressing: {_lastResult.Population.Dressing.Count}");
            }
        }
    }
}
