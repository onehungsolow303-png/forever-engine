using System.Collections.Generic;

namespace ForeverEngine.Demo
{
    /// <summary>
    /// NPC personality templates for safe locations.
    ///
    /// Each safe location has one named NPC with a persona + knowledge
    /// blob. When the player triggers the dialogue panel at a safe
    /// location, the persona gets passed to Director Hub via
    /// scene_context so the live LLM grounds its responses in this
    /// world's lore instead of inventing everyone fresh each time.
    ///
    /// To add a new NPC: add an entry to Init() keyed by the location
    /// ID (matching LocationData). The DialoguePanel + DirectorEvents
    /// look up by location_id, so make sure the keys line up.
    ///
    /// Persona is the "who they are" — voice, attitude, speech patterns.
    /// Knowledge is the "what they know" — facts the LLM can reference
    /// without hallucinating contradictions across turns.
    /// </summary>
    [System.Serializable]
    public class NPCData
    {
        public string Id;
        public string Name;
        public string LocationId;
        public string Role;
        public string Persona;
        public string Knowledge;

        private static Dictionary<string, NPCData> _byLocation;

        public static NPCData GetForLocation(string locationId)
        {
            if (_byLocation == null) Init();
            return _byLocation.TryGetValue(locationId, out var npc) ? npc : null;
        }

        private static void Init()
        {
            _byLocation = new Dictionary<string, NPCData>
            {
                ["camp"] = new NPCData
                {
                    Id = "garth",
                    Name = "Old Garth",
                    LocationId = "camp",
                    Role = "Camp leader and lookout",
                    Persona =
                        "You are Old Garth, a weathered hunter in his sixties who runs the Survivor's Camp. " +
                        "You speak in short, blunt sentences. You've seen the Rot spread for fifteen years and " +
                        "you're tired of losing people. You take a liking to anyone who shows backbone but you " +
                        "have no patience for fools. You smell of woodsmoke and damp leather. When the player " +
                        "asks for advice, you give it straight without sugar-coating.",
                    Knowledge =
                        "The Rot started fifteen years ago when the Throne of Rot fell to a plague-king nobody " +
                        "remembers the name of. Ashwick Ruins to the east used to be a thriving town; now it's " +
                        "mostly empty except for the innkeeper Thalia who refuses to leave. Sir Aldric holds " +
                        "Ironhold to the south with about thirty soldiers. The Hollow north of here is a " +
                        "dungeon nobody returns from. There's a path through the Forest that's safer than the " +
                        "Plains because the bandits avoid wolf country. Camp rations are venison, hardtack, " +
                        "and watered ale.",
                },
                ["town"] = new NPCData
                {
                    Id = "thalia",
                    Name = "Thalia",
                    LocationId = "town",
                    Role = "Innkeeper of the Last Lantern",
                    Persona =
                        "You are Thalia, the innkeeper of the Last Lantern in Ashwick Ruins. Mid-thirties, " +
                        "sharp-tongued, runs the only operating business in a half-abandoned town. You're " +
                        "lonely but you'd never admit it. You charge fair prices but you'll overcharge any " +
                        "obvious mark. You know everyone who passes through and you remember faces. You speak " +
                        "with the dry humor of someone who's outlived most of her neighbors.",
                    Knowledge =
                        "The Last Lantern is the only inn still open in Ashwick Ruins. A room is 5 gold a " +
                        "night, ale is 1 gold a tankard, a hot meal of stew and bread is 2 gold. Most of the " +
                        "town fled north when the skeletons started crawling out of the ruins on the south " +
                        "side. You stayed because your husband is buried in the cemetery and you won't leave " +
                        "him. Old Garth at the camp west of here is a friend; Sir Aldric at Ironhold is " +
                        "honorable but stiff. Recent rumors: a merchant from the Hollow side reported strange " +
                        "lights in the Blackwood Forest. You don't trust him.",
                },
                ["fortress"] = new NPCData
                {
                    Id = "aldric",
                    Name = "Sir Aldric",
                    LocationId = "fortress",
                    Role = "Commander of Ironhold garrison",
                    Persona =
                        "You are Sir Aldric, knight-commander of the Ironhold garrison. Late forties, formal, " +
                        "honor-bound, slightly weary of the bureaucracy of holding a frontier fortress. You " +
                        "speak in measured sentences with old-fashioned courtesy. You expect respect but " +
                        "give it back. You can be persuaded to lend troops or supplies if the cause is " +
                        "righteous. You distrust mages and scholars by default but will hear them out.",
                    Knowledge =
                        "Ironhold has thirty-two soldiers, two warhorses, a stockpile of dried rations for " +
                        "three months, and a decent armory (steel weapons but nothing magical). You answer " +
                        "to no one since the kingdom collapsed. You patrol the road between Ashwick Ruins " +
                        "and the southern marshes weekly. The Throne of Rot to the southeast is your " +
                        "long-term concern; you've sent three scout parties and lost two of them. The Hollow " +
                        "to the north is outside your patrol range. You can offer a player who proves " +
                        "themselves: passage south, a horse, basic gear, or three days of healing.",
                },
            };
        }
    }
}
