using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CLASSIFIED
{
    /// <summary>
    /// Remembers which memos, notes, intel, headlines and chatter a player has already been
    /// shown, across launches, so the terminal picks something new each time and only comes back
    /// to a line once everything else in its pool has been shown (then the longest-unseen first).
    /// Lines are remembered by a hash of their text, so editing a line makes it new again.
    ///
    /// Stored in BepInEx\config\com.mybutthasarash.classified.seen.txt, one key per line,
    /// oldest first, capped. A preview picks the same way but leaves the record alone, and nothing
    /// repeats within one launch either way.
    /// </summary>
    internal static class Archive
    {
        private const int Keep = 4000;

        private static readonly Dictionary<string, int> Seen = new Dictionary<string, int>();
        private static readonly HashSet<string> ThisRun = new HashSet<string>();
        private static int _sequence;
        private static bool _loaded;
        private static bool _dirty;

        private static string FilePath =>
            Path.Combine(BepInEx.Paths.ConfigPath, ClassifiedPlugin.PluginGuid + ".seen.txt");

        /// <summary>False during a preview: picks then leave the record alone.</summary>
        internal static bool Recording = true;

        /// <summary>
        /// Picks an entry of the pool: one never seen before if there is any (at random among
        /// them), else the one seen longest ago, skipping anything already shown this launch.
        /// Records it unless this is a preview.
        /// </summary>
        internal static int Pick(string kind, IList<string> pool, Random random)
        {
            if (pool == null || pool.Count == 0)
            {
                return -1;
            }
            Load();
            var fresh = new List<int>();
            int oldest = -1;
            int oldestAge = int.MaxValue;
            for (int i = 0; i < pool.Count; i++)
            {
                string key = Key(kind, pool[i]);
                if (ThisRun.Contains(key))
                {
                    continue;
                }
                if (!Seen.TryGetValue(key, out int when))
                {
                    fresh.Add(i);
                }
                else if (when < oldestAge || (when == oldestAge && random.Next(2) == 0))
                {
                    oldestAge = when;
                    oldest = i;
                }
            }
            int pick = fresh.Count > 0 ? fresh[random.Next(fresh.Count)] : oldest;
            if (pick < 0)
            {
                // Everything was shown this launch already: any will do.
                pick = random.Next(pool.Count);
            }
            Mark(Key(kind, pool[pick]), Recording);
            return pick;
        }

        /// <summary>
        /// Picks an entry at random, but never one of the pool's most recently shown: about half
        /// the pool sits out each time, so a choice cannot come back for several launches and
        /// the order is new every time round (unlike <see cref="Pick"/>, which settles into a
        /// fixed cycle once everything has been seen). Records it unless this is a preview.
        /// </summary>
        internal static int PickShuffled(string kind, IList<string> pool, Random random)
        {
            if (pool == null || pool.Count == 0)
            {
                return -1;
            }
            Load();
            var recent = new List<KeyValuePair<int, int>>();
            for (int i = 0; i < pool.Count; i++)
            {
                if (Seen.TryGetValue(Key(kind, pool[i]), out int when))
                {
                    recent.Add(new KeyValuePair<int, int>(when, i));
                }
            }
            recent.Sort((a, b) => b.Key.CompareTo(a.Key));
            var resting = new HashSet<int>();
            for (int i = 0; i < recent.Count && i < pool.Count / 2; i++)
            {
                resting.Add(recent[i].Value);
            }
            var open = new List<int>();
            for (int i = 0; i < pool.Count; i++)
            {
                if (!resting.Contains(i))
                {
                    open.Add(i);
                }
            }
            int pick = open[random.Next(open.Count)];
            Mark(Key(kind, pool[pick]), Recording);
            return pick;
        }

        /// <summary>Picks and returns the entry itself; null for an empty pool.</summary>
        internal static string PickLine(string kind, IList<string> pool, Random random)
        {
            int i = Pick(kind, pool, random);
            return i < 0 ? null : pool[i];
        }

        /// <summary>Forgets what this launch showed, for the next run (a replayed preview, say).</summary>
        internal static void NewRun()
        {
            ThisRun.Clear();
        }

        /// <summary>Writes the record if anything new was seen. Cheap to call often.</summary>
        internal static void Save()
        {
            if (!_dirty)
            {
                return;
            }
            try
            {
                IEnumerable<string> keys = Seen.OrderBy(pair => pair.Value).Select(pair => pair.Key);
                int extra = Seen.Count - Keep;
                if (extra > 0)
                {
                    keys = keys.Skip(extra);
                }
                File.WriteAllLines(FilePath, keys.ToArray());
                _dirty = false;
            }
            catch (Exception e)
            {
                ClassifiedPlugin.Log.LogWarning($"Could not save {FilePath}: {e.Message}");
            }
        }

        private static void Mark(string key, bool remember)
        {
            ThisRun.Add(key);
            if (!remember)
            {
                return;
            }
            Seen[key] = ++_sequence;
            _dirty = true;
        }

        private static void Load()
        {
            if (_loaded)
            {
                return;
            }
            _loaded = true;
            try
            {
                if (!File.Exists(FilePath))
                {
                    return;
                }
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    string key = line.Trim();
                    if (key.Length > 0)
                    {
                        Seen[key] = ++_sequence;
                    }
                }
            }
            catch (Exception e)
            {
                ClassifiedPlugin.Log.LogWarning($"Could not read {FilePath}: {e.Message}");
            }
        }

        /// <summary>The pool's name and an FNV-1a hash of the line's text: stable across launches and versions.</summary>
        private static string Key(string kind, string text)
        {
            uint hash = 2166136261;
            foreach (char c in text)
            {
                hash = (hash ^ c) * 16777619;
            }
            return kind + ":" + hash.ToString("x8");
        }
    }
}
