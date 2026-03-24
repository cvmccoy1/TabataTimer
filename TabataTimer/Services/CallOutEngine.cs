using TabataTimer.Models;

namespace TabataTimer.Services
{
    /// <summary>
    /// Manages call-out exercise selection for Follow, Repeat, and Random modes.
    /// Call Reset() when a workout starts. Call Next() at the start of each Work phase.
    /// </summary>
    public class CallOutEngine
    {
        private readonly TabataSequence _sequence;
        private readonly Random _rng = new();

        // Follow / Repeat tracking
        private int _followIndex = 0;

        // Random tracking — exercises already used this cycle
        private readonly List<string> _randomPool = new();
        private readonly List<string> _randomUsed = new();

        public CallOutEngine(TabataSequence sequence)
        {
            _sequence = sequence;
        }

        /// <summary>Reset state at workout start.</summary>
        public void Reset()
        {
            _followIndex = 0;
            _randomPool.Clear();
            _randomUsed.Clear();

            if (_sequence.CallOutMode == CallOutMode.Random)
                BuildRandomPool();
        }

        /// <summary>
        /// Returns the exercise text to speak for the next Work phase,
        /// or null if nothing should be spoken.
        /// </summary>
        public string? Next()
        {
            if (_sequence.CallOutMode == CallOutMode.Off) return null;
            if (_sequence.CallOutList == null || _sequence.CallOutList.Count == 0) return null;

            return _sequence.CallOutMode switch
            {
                CallOutMode.Follow  => NextFollow(),
                CallOutMode.Repeat  => NextRepeat(),
                CallOutMode.Random  => NextRandom(),
                _                   => null
            };
        }

        // ── Follow ────────────────────────────────────────────────────────────
        // Advance through list exactly once, one slot per Work phase.
        private string? NextFollow()
        {
            if (_followIndex >= _sequence.CallOutList.Count)
                return null;

            var slot = _sequence.CallOutList[_followIndex];
            _followIndex++;
            return PickFromSlot(slot);
        }

        // ── Repeat ────────────────────────────────────────────────────────────
        // Same as Follow but wraps back to start when list is exhausted.
        private string? NextRepeat()
        {
            if (_sequence.CallOutList.Count == 0) return null;

            var slot = _sequence.CallOutList[_followIndex % _sequence.CallOutList.Count];
            _followIndex++;
            return PickFromSlot(slot);
        }

        // ── Random ────────────────────────────────────────────────────────────
        // Pick from the flat pool of all exercises across all slots.
        // Once all have been used, refill the pool.
        private string? NextRandom()
        {
            if (_randomPool.Count == 0 && _randomUsed.Count == 0)
                return null;

            // Refill when pool is exhausted
            if (_randomPool.Count == 0)
            {
                _randomPool.AddRange(_randomUsed);
                _randomUsed.Clear();
            }

            int idx = _rng.Next(_randomPool.Count);
            string exercise = _randomPool[idx];
            _randomPool.RemoveAt(idx);
            _randomUsed.Add(exercise);
            return exercise;
        }

        // ── Slot helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// A slot may be blank, a single exercise, or comma-separated exercises.
        /// If multiple, pick one at random — preferring one not recently used.
        /// </summary>
        private string? PickFromSlot(string? slot)
        {
            if (string.IsNullOrWhiteSpace(slot)) return null;

            var parts = slot.Split(',')
                            .Select(p => p.Trim())
                            .Where(p => !string.IsNullOrWhiteSpace(p))
                            .ToList();

            if (parts.Count == 0) return null;
            if (parts.Count == 1) return parts[0];

            // Try to pick one not in the used set
            var unused = parts.Except(_randomUsed, StringComparer.OrdinalIgnoreCase).ToList();
            var candidates = unused.Count > 0 ? unused : parts;
            var chosen = candidates[_rng.Next(candidates.Count)];

            // Track it so we avoid repeating it next call for this slot type
            if (!_randomUsed.Contains(chosen, StringComparer.OrdinalIgnoreCase))
                _randomUsed.Add(chosen);

            return chosen;
        }

        private void BuildRandomPool()
        {
            _randomPool.Clear();
            foreach (var slot in _sequence.CallOutList)
            {
                if (string.IsNullOrWhiteSpace(slot)) continue;
                var parts = slot.Split(',')
                                .Select(p => p.Trim())
                                .Where(p => !string.IsNullOrWhiteSpace(p));
                _randomPool.AddRange(parts);
            }
        }
    }
}
