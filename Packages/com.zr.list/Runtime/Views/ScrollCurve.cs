// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System;
using UnityEngine;

namespace ZRList
{
    public enum ScrollCurveMode
    {
        Arc,
        Custom
    }

    /// <summary>Maps a viewport-axis distance to a cross-axis offset and tangent angle.</summary>
    [Serializable]
    public sealed class ScrollCurve
    {
        public ScrollCurveMode Mode;
        [Min(1f)] public float ArcRadius = 900f;
        public bool Invert;
        [Min(0f)] public float Amplitude = 110f;
        [Tooltip("Time 0..1 spans the viewport. Value is multiplied by Amplitude.")]
        public AnimationCurve CustomCurve = AnimationCurve.EaseInOut(0f, -1f, 1f, 1f);

        private const int SampleCount = 257;
        private const float ArcLimitSin = 0.984807753f; // 80 degrees: finite slope near the circle's sides.
        private const float ArcLimitCos = 0.173648178f;
        [NonSerialized] private Vector2[] m_samples;
        [NonSerialized] private AnimationCurve m_cachedCurve;

        /// <summary>Call after editing keys in place. Storage is reused on subsequent rebuilds.</summary>
        public void RebuildCache()
        {
            if (m_samples == null) {
                m_samples = new Vector2[SampleCount];
            }

            m_cachedCurve = CustomCurve;
            float step = 1f / (SampleCount - 1);
            for (var index = 0; index < SampleCount; ++index) {
                float time = index * step;
                float left = Mathf.Max(0f, time - step);
                float right = Mathf.Min(1f, time + step);
                float value = CustomCurve == null ? 0f : CustomCurve.Evaluate(time);
                float slope = CustomCurve == null ? 0f : (CustomCurve.Evaluate(right) - CustomCurve.Evaluate(left)) / (right - left);
                m_samples[index] = new Vector2(FiniteOrZero(value), FiniteOrZero(slope));
            }
        }

        /// <summary>Positive distance follows the scroll order (right or down). Units are viewport-local UI units.</summary>
        public void Evaluate(float distance, float halfExtent, out float offset, out float angle)
        {
            distance = FiniteOrZero(distance);
            float slope;
            if (Mode == ScrollCurveMode.Arc) {
                float radius = Mathf.Max(1f, FiniteOrZero(ArcRadius));
                float limit = radius * ArcLimitSin;
                float x = Mathf.Clamp(distance, -limit, limit);
                float ratio = x / radius;
                float heightRatio = Mathf.Sqrt(Mathf.Max(1f - ratio * ratio, ArcLimitCos * ArcLimitCos));
                slope = ratio / heightRatio;
                // Rationalized sagitta avoids cancellation for large radii. Continue along
                // the tangent past 80 degrees instead of producing NaN or stacking items.
                offset = (x / (1f + heightRatio)) * ratio + (distance - x) * slope;
            }
            else {
                if (m_samples == null || !ReferenceEquals(m_cachedCurve, CustomCurve)) {
                    RebuildCache();
                }

                float extent = Mathf.Max(1f, FiniteOrZero(halfExtent));
                float time = 0.5f + distance / (2f * extent);
                float clamped = Mathf.Clamp01(time);
                float sampleIndex = clamped * (SampleCount - 1);
                int first = Mathf.Min((int)sampleIndex, SampleCount - 2);
                Vector2 sample = Vector2.LerpUnclamped(m_samples[first], m_samples[first + 1], sampleIndex - first);
                float amplitude = Mathf.Max(0f, FiniteOrZero(Amplitude));
                offset = (sample.x + (time - clamped) * sample.y) * amplitude;
                slope = sample.y * amplitude / (2f * extent);
            }

            if (Invert) {
                offset = -offset;
                slope = -slope;
            }

            angle = Mathf.Atan(slope) * Mathf.Rad2Deg;
        }

        private static float FiniteOrZero(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }
    }
}
