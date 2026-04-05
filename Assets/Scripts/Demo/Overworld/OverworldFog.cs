using System.Collections.Generic;

namespace ForeverEngine.Demo.Overworld
{
    public class OverworldFog
    {
        private HashSet<string> _explored = new();
        private HashSet<string> _visible = new();
        private int _revealRadius;

        public OverworldFog(int revealRadius = 2) => _revealRadius = revealRadius;

        public void Reveal(int q, int r, int radius = -1)
        {
            int rad = radius >= 0 ? radius : _revealRadius;
            for (int dq = -rad; dq <= rad; dq++)
                for (int dr = -rad; dr <= rad; dr++)
                    if (System.Math.Abs(dq + dr) <= rad)
                    {
                        string key = $"{q+dq},{r+dr}";
                        _explored.Add(key);
                    }
            UpdateVisible(q, r);
        }

        private void UpdateVisible(int q, int r)
        {
            _visible.Clear();
            int rad = _revealRadius;
            for (int dq = -rad; dq <= rad; dq++)
                for (int dr = -rad; dr <= rad; dr++)
                    if (System.Math.Abs(dq + dr) <= rad)
                        _visible.Add($"{q+dq},{r+dr}");
        }

        public bool IsExplored(int q, int r) => _explored.Contains($"{q},{r}");
        public bool IsVisible(int q, int r) => _visible.Contains($"{q},{r}");
        public int ExploredCount => _explored.Count;

        public void SetRevealRadius(int radius) => _revealRadius = radius;
        public HashSet<string> GetExploredSet() => new(_explored);
        public void LoadExplored(HashSet<string> set) { _explored = new HashSet<string>(set); }
    }
}
