using UnityEngine;

namespace ForeverEngine.Bridges
{
    // DirectorClient is no longer used by the Unity client.
    // All Director Hub communication routes through ForeverEngine.Server.
    // This shell remains for compile compatibility until Phase 2/3 cleanup.
    public class DirectorClient
    {
        public string BaseUrl { get; }
        public DirectorClient(string baseUrl = "http://127.0.0.1:7802") { BaseUrl = baseUrl; }
    }
}
