using TabataTimer.Models;
using TabataTimer.Services.Interfaces;

namespace TabataTimer.Services
{
    /// <summary>
    /// Manages call-out exercise selection for Follow, Repeat, and Random modes.
    /// Call Reset() when a workout starts. Call Next() at the start of each Work phase.
    /// </summary>
    public class CallOutEngine : ICallOutEngine
    {
        private readonly Random _rng = new();

        // Follow / Repeat tracking
        private int _followIndex = 0;

        // Per-slot exercise tracking (for cycling through comma-separated exercises)
        private readonly Dictionary<int, int> _slotExerciseIndices = [];

        // Random tracking — exercises already used this cycle
        private readonly List<string> _randomPool = [];
        private readonly List<string> _randomUsed = [];

        /// <summary>The exercise text for the current (most recently selected) Work phase.</summary>
        public string? CurrentExercise { get; private set; }

        /// <summary>True if the current exercise should trigger a mid-Work beep.</summary>
        public bool CurrentExerciseNeedsMidWorkBeep { get; private set; }

        public CallOutEngine(TabataSequence sequence)
        {
            _sequence = sequence;
        }

        private readonly TabataSequence _sequence;

        /// <summary>Reset state at workout start.</summary>
        public void Reset()
        {
            _followIndex = 0;
            _slotExerciseIndices.Clear();
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

            string? result = _sequence.CallOutMode switch
            {
                CallOutMode.Follow  => NextFollow(),
                CallOutMode.Repeat  => NextRepeat(),
                CallOutMode.Random  => NextRandom(),
                _                   => null
            };
            CurrentExercise = result;
            CurrentExerciseNeedsMidWorkBeep = result?.StartsWith('*') ?? false;
            return result;
        }

        // ── Follow ────────────────────────────────────────────────────────────
        // Advance through list exactly once, one slot per Work phase.
        private string? NextFollow()
        {
            if (_followIndex >= _sequence.CallOutList.Count)
                return null;

            var slot = _sequence.CallOutList[_followIndex];
            int slotIndex = _followIndex;
            _followIndex++;
            return PickFromSlot(slotIndex, slot);
        }

        // ── Repeat ────────────────────────────────────────────────────────────
        // Same as Follow but wraps back to start when list is exhausted.
        private string? NextRepeat()
        {
            if (_sequence.CallOutList.Count == 0) return null;

            int slotIndex = _followIndex % _sequence.CallOutList.Count;
            var slot = _sequence.CallOutList[slotIndex];
            _followIndex++;
            return PickFromSlot(slotIndex, slot);
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
        /// If multiple, cycles through them sequentially, wrapping around when exhausted.
        /// </summary>
        private string? PickFromSlot(int slotIndex, string? slot)
        {
            if (string.IsNullOrWhiteSpace(slot)) return null;

            var parts = slot.Split(',')
                            .Select(p => p.Trim())
                            .Where(p => !string.IsNullOrWhiteSpace(p))
                            .ToList();

            if (parts.Count == 0) return null;
            if (parts.Count == 1) return parts[0];

            // Get current index for this slot, default to 0
            if (!_slotExerciseIndices.TryGetValue(slotIndex, out int currentIndex))
                currentIndex = 0;

            // Cycle through exercises sequentially
            string exercise = parts[currentIndex];
            _slotExerciseIndices[slotIndex] = (currentIndex + 1) % parts.Count;

            return exercise;
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
