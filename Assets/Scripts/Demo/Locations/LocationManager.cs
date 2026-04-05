using UnityEngine;
using ForeverEngine.Generation;
using ForeverEngine.Generation.Data;

namespace ForeverEngine.Demo.Locations
{
    public class LocationManager : UnityEngine.MonoBehaviour
    {
        public static LocationManager Instance { get; private set; }

        private void Awake() => Instance = this;

        public void EnterLocation(string locationId)
        {
            var loc = LocationData.Get(locationId);
            if (loc == null) return;

            var gm = GameManager.Instance;
            gm.Player.DiscoveredLocations.Add(locationId);

            if (loc.IsSafe)
            {
                gm.Player.LastSafeLocation = locationId;
                if (loc.Type == "camp") { gm.Player.FullRest(); Debug.Log("[Location] Rested at camp"); }
                else if (loc.Type == "fortress") { gm.Player.FullRest(); gm.Player.AC += 0; Debug.Log("[Location] Rested at Ironhold"); }
            }

            if (loc.Type == "dungeon" || loc.Type == "castle")
            {
                // Generate interior and enter battle scene
                gm.PendingLocationId = locationId;
                string encounterId = locationId == "castle" ? "castle_boss" : "dungeon_boss";
                gm.EnterBattle(encounterId);
            }

            Debug.Log($"[Location] Entered {loc.Name} ({loc.Type})");
        }

        public static PipelineCoordinator.GenerationResult GenerateInterior(LocationData loc)
        {
            var request = new GenerationRequest
            {
                MapType = loc.MapType ?? "dungeon",
                Width = loc.InteriorSize,
                Height = loc.InteriorSize,
                Seed = loc.Id.GetHashCode(),
                PartyLevel = GameManager.Instance?.Player?.Level ?? 3,
                PartySize = 1
            };
            return PipelineCoordinator.Generate(request);
        }
    }
}
