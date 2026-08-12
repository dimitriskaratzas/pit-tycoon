namespace PitTycoon.Domain
{
    /// <summary>Where one crowd member stands and how it is shaped. Plain floats, not a
    /// Vector3: the Domain assembly has no UnityEngine reference.</summary>
    public readonly struct CrowdSlot
    {
        public float X { get; }
        public float Z { get; }
        public float RotationY { get; }
        public float Scale { get; }
        public float PopScale { get; }

        public CrowdSlot(float x, float z, float rotationY, float scale, float popScale)
        {
            X = x;
            Z = z;
            RotationY = rotationY;
            Scale = scale;
            PopScale = popScale;
        }
    }

    /// <summary>Layout parameters, passed as one value so the real pit and its ghost preview
    /// cannot drift apart on arguments.</summary>
    public readonly struct CrowdLayoutSettings
    {
        public int Columns { get; }
        public float Spacing { get; }
        public int StartRows { get; }
        public float PositionJitter { get; }
        public float RowSpacingFalloff { get; }
        public float RotationJitter { get; }
        public float ScaleJitter { get; }

        public CrowdLayoutSettings(int columns, float spacing, int startRows,
            float positionJitter, float rowSpacingFalloff, float rotationJitter, float scaleJitter)
        {
            Columns = columns < 1 ? 1 : columns;
            Spacing = spacing;
            StartRows = startRows < 1 ? 1 : startRows;
            PositionJitter = positionJitter;
            RowSpacingFalloff = rowSpacingFalloff;
            RotationJitter = rotationJitter;
            ScaleJitter = scaleJitter;
        }
    }

    /// <summary>
    /// Pure pit layout. Every per-member value is derived from a hash of the member's index
    /// rather than drawn randomly, which is what makes the crowd stable across rebuilds (a
    /// capacity upgrade calls Build() again) and makes a ghost preview land exactly where its
    /// real member will. No UnityEngine — fully unit-tested.
    /// </summary>
    public static class CrowdLayout
    {
        // Salts keep the derived values independent. Reusing one would lock, say, body shape
        // to outfit colour across the whole pit.
        private const int SaltJitterX = 1;
        private const int SaltJitterZ = 2;
        private const int SaltRotation = 3;
        private const int SaltScale = 4;
        private const int SaltPop = 5;
        private const int SaltVariant = 6;
        private const int SaltOutfit = 7;

        public static CrowdSlot Slot(int index, CrowdLayoutSettings s)
        {
            if (index < 0) index = 0;

            int row = index / s.Columns;
            int col = index % s.Columns;

            // Front row stays pinned near the stage, so the pit does not drift off the barrier
            // as capacity grows.
            float frontZ = (s.StartRows - 1) * s.Spacing * 0.5f;
            float offsetX = (s.Columns - 1) * s.Spacing * 0.5f;

            // Scatter grows with distance from the stage: packed at the barrier, loose at the
            // back. Expressed as a fraction of spacing so it stays proportional when tuned.
            float rowT = s.StartRows > 1 ? row / (float)(s.StartRows - 1) : 1f;
            if (rowT > 1f) rowT = 1f;
            float amount = s.PositionJitter * (0.35f + 0.65f * rowT) * s.Spacing;

            float x = col * s.Spacing - offsetX + Signed(index, SaltJitterX) * amount;
            float z = frontZ - RowDepth(row, s.Spacing, s.RowSpacingFalloff)
                      + Signed(index, SaltJitterZ) * amount;

            float rotationY = Signed(index, SaltRotation) * s.RotationJitter;
            float scale = 1f + Signed(index, SaltScale) * s.ScaleJitter;
            float popScale = 0.55f + Hash01(index, SaltPop) * 0.65f;   // 0.55 .. 1.20

            return new CrowdSlot(x, z, rotationY, scale, popScale);
        }

        /// <summary>Cumulative depth of a row from the front. The gap between consecutive rows
        /// is spacing * (1 + falloff * r), so the sum has a closed form and needs no loop.</summary>
        public static float RowDepth(int row, float spacing, float falloff)
        {
            if (row <= 0) return 0f;
            return spacing * (row + falloff * row * (row - 1) * 0.5f);
        }

        /// <summary>Height of the beat pop for a row, given how long ago the beat landed.
        /// Zero until the wave reaches that row — that guard is what makes the pop travel
        /// instead of firing across the whole pit on one frame.</summary>
        public static float PopHeight(int row, float secondsSinceBeat, float strength,
            float rowDelay, float decay)
        {
            float age = secondsSinceBeat - row * rowDelay;
            if (age < 0f) return 0f;
            float height = strength - age * decay;
            return height > 0f ? height : 0f;
        }

        /// <summary>Which body variant this member uses. Stable across rebuilds.</summary>
        public static int VariantIndex(int index, int count) => PickIndex(index, count, SaltVariant);

        /// <summary>Which outfit tint this member wears. Stable across rebuilds.</summary>
        public static int OutfitIndex(int index, int count) => PickIndex(index, count, SaltOutfit);

        private static int PickIndex(int index, int count, int salt)
        {
            if (count <= 0) return 0;
            if (index < 0) index = 0;   // match Slot's clamp so a member's slot and its outfit agree
            int picked = (int)(Hash01(index, salt) * count);
            return picked >= count ? count - 1 : picked;
        }

        /// <summary>Deterministic [0,1) hash. Must decorrelate neighbouring indices: adjacent
        /// members stand next to each other, so a weak hash reads as diagonal banding rather
        /// than scatter.</summary>
        private static float Hash01(int index, int salt)
        {
            unchecked
            {
                uint h = (uint)(index * 374761393 + salt * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFFu) / (float)0x1000000;
            }
        }

        private static float Signed(int index, int salt) => Hash01(index, salt) * 2f - 1f;
    }
}
