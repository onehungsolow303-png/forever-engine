using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ForeverEngine.AI.GameMaster
{
    [System.Serializable]
    public class JournalEntry
    {
        public int    day;
        public string text;
    }

    /// <summary>
    /// Rolling narrative log of in-game events.
    /// Older entries are auto-compressed into a prose summary to stay within
    /// the MAX_WORDS budget that gets injected into LLM prompts.
    /// </summary>
    public class NarrativeJournal : UnityEngine.MonoBehaviour
    {
        private List<JournalEntry> _entries  = new();
        private string             _summary  = "";
        private string             _savePath;

        private const int MAX_WORDS = 2000;

        // ── Lifecycle ─────────────────────────────────────────────────────

        public void Initialize(string sessionId)
        {
            _savePath = Path.Combine(
                Application.persistentDataPath, "game_state", sessionId, "journal.md");
            Directory.CreateDirectory(Path.GetDirectoryName(_savePath));
        }

        // ── Public API ────────────────────────────────────────────────────

        public void AddEntry(int day, string text)
        {
            _entries.Add(new JournalEntry { day = day, text = text });
            if (GetWordCount() > MAX_WORDS) Summarize();
        }

        /// <summary>Returns recent entries prepended with the compressed summary.</summary>
        public string GetRecentExcerpt(int maxWords = 500)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(_summary))
                sb.AppendLine($"[Summary of earlier events: {_summary}]\n");

            int wordCount = 0;
            for (int i = _entries.Count - 1; i >= 0 && wordCount < maxWords; i--)
            {
                string entry = $"Day {_entries[i].day}: {_entries[i].text}";
                wordCount += entry.Split(' ').Length;
                sb.Insert(string.IsNullOrEmpty(_summary) ? 0 : _summary.Length + 40, entry + "\n");
            }
            return sb.ToString();
        }

        public string GetFullJournal()
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(_summary))
                sb.AppendLine($"[Earlier summary]: {_summary}\n");
            foreach (var e in _entries)
                sb.AppendLine($"Day {e.day}: {e.text}");
            return sb.ToString();
        }

        // ── Persistence ───────────────────────────────────────────────────

        public void Save()
        {
            var sb = new StringBuilder();
            sb.AppendLine("---");
            sb.AppendLine($"summary: {_summary}");
            sb.AppendLine("---");
            foreach (var e in _entries)
                sb.AppendLine($"Day {e.day}: {e.text}");
            File.WriteAllText(_savePath, sb.ToString());
        }

        public void Load()
        {
            if (!File.Exists(_savePath)) return;

            string content = File.ReadAllText(_savePath);
            string[] lines = content.Split('\n');
            bool inFrontmatter = false;
            bool frontmatterDone = false;
            _entries.Clear();

            foreach (string raw in lines)
            {
                string line = raw.TrimEnd();
                if (line == "---")
                {
                    if (!frontmatterDone) { inFrontmatter = !inFrontmatter; if (!inFrontmatter) frontmatterDone = true; }
                    continue;
                }
                if (inFrontmatter && line.StartsWith("summary: "))
                {
                    _summary = line.Substring("summary: ".Length);
                    continue;
                }
                if (frontmatterDone && line.StartsWith("Day "))
                {
                    int colon = line.IndexOf(':');
                    if (colon > 0 && int.TryParse(line.Substring(4, colon - 4).Trim(), out int day))
                        _entries.Add(new JournalEntry { day = day, text = line.Substring(colon + 1).Trim() });
                }
            }
        }

        // ── Private helpers ───────────────────────────────────────────────

        private void Summarize()
        {
            int keepCount = Mathf.Min(5, _entries.Count);
            int compressCount = _entries.Count - keepCount;
            if (compressCount <= 0) return;

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(_summary)) sb.Append(_summary + " ");
            for (int i = 0; i < compressCount; i++)
                sb.Append($"Day {_entries[i].day}: {_entries[i].text} ");

            _summary = sb.ToString().Trim();
            _entries = _entries.GetRange(compressCount, keepCount);
        }

        private int GetWordCount()
        {
            int count = string.IsNullOrEmpty(_summary) ? 0 : _summary.Split(' ').Length;
            foreach (var e in _entries)
                count += string.IsNullOrEmpty(e.text) ? 0 : e.text.Split(' ').Length;
            return count;
        }
    }
}
