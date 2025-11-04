using UnityEngine;

namespace Belen.Rendering
{
    // Helper to set OffAxisCamera screen size using common presets or custom diagonal/aspect.
    [ExecuteAlways]
    public class OffAxisScreenPreset : MonoBehaviour
    {
        public enum Preset
        {
            Inch24_16x9,
            Inch27_16x9,
            Inch55_16x9,
            Custom
        }

        public OffAxisCamera target;
        public Preset preset = Preset.Inch27_16x9;
        public bool autoApply = true;

        [Header("Custom Diagonal + Aspect")]
        public float diagonalInches = 27f;
        public int aspectX = 16;
        public int aspectY = 9;

        private const float InchToMeters = 0.0254f;

        private void OnEnable()
        {
            if (autoApply) ApplyToTarget();
        }

        private void OnValidate()
        {
            aspectX = Mathf.Max(1, aspectX);
            aspectY = Mathf.Max(1, aspectY);
            diagonalInches = Mathf.Max(1f, diagonalInches);
            if (autoApply) ApplyToTarget();
        }

        [ContextMenu("Apply Preset To Target")]
        public void ApplyToTarget()
        {
            if (target == null) return;

            float diag = diagonalInches;
            int ax = aspectX, ay = aspectY;

            switch (preset)
            {
                case Preset.Inch24_16x9: diag = 24f; ax = 16; ay = 9; break;
                case Preset.Inch27_16x9: diag = 27f; ax = 16; ay = 9; break;
                case Preset.Inch55_16x9: diag = 55f; ax = 16; ay = 9; break;
                case Preset.Custom: default: break;
            }

            float diagMeters = diag * InchToMeters;
            float norm = Mathf.Sqrt(ax * ax + ay * ay);
            float width = diagMeters * (ax / norm);
            float height = diagMeters * (ay / norm);

            target.screenWidth = width;
            target.screenHeight = height;
        }
    }
}

