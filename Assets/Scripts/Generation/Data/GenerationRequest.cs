namespace ForeverEngine.Generation.Data
{
    public enum MapSize { SmallEncounter = 256, MediumEncounter = 512, LargeEncounter = 768, Standard = 512, Large = 1024, Region = 1024, OpenWorld = 1536 }

    public class GenerationRequest
    {
        public string MapType = "dungeon";
        public string Biome = "cave";
        public int Width = 512;
        public int Height = 512;
        public int Seed = 42;
        public int PartyLevel = 3;
        public int PartySize = 4;

        public bool Validate(out string error)
        {
            if (Width < 64 || Width > 2048) { error = "Width must be 64-2048"; return false; }
            if (Height < 64 || Height > 2048) { error = "Height must be 64-2048"; return false; }
            if (PartyLevel < 1 || PartyLevel > 20) { error = "Party level must be 1-20"; return false; }
            if (PartySize < 1 || PartySize > 8) { error = "Party size must be 1-8"; return false; }
            if (string.IsNullOrEmpty(MapType)) { error = "MapType required"; return false; }
            error = null; return true;
        }
    }
}
