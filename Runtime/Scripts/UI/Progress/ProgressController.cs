using System;
using UnityEngine;

namespace Training.UI.Progress
{
    public class ProgressController : MonoBehaviour
    {
        [SerializeField] private string label = "Progress";
        [SerializeField] private float minValue = 0f;
        [SerializeField] private float maxValue = 100f;
        [SerializeField] private float currentValue;
        [SerializeField] private bool completeWhenMax = true;
        [SerializeField] private bool manualAdvanceOnly;

        public event Action<float, float> OnChanged;
        public event Action OnCompleted;
        public event Action OnReset;

        private bool _completedFired;

        public string Label => label;
        public float Min => minValue;
        public float Max => maxValue;
        public float Value => currentValue;
        public float Normalized =>
            Mathf.Approximately(maxValue, minValue)
                ? 0f
                : Mathf.Clamp01((currentValue - minValue) / (maxValue - minValue));
        public bool IsComplete => currentValue >= maxValue - 0.0001f;
        public bool ManualAdvanceOnly => manualAdvanceOnly;

        public void Setup(string progressLabel, float min, float max,
            bool completeAtMax = true, bool manualOnly = false)
        {
            label = progressLabel;
            minValue = min;
            maxValue = max;
            completeWhenMax = completeAtMax;
            manualAdvanceOnly = manualOnly;
            ResetProgress();
        }

        public void Add(float amount) => Set(currentValue + amount);

        public void Set(float value)
        {
            currentValue = Mathf.Clamp(value, minValue, maxValue);
            OnChanged?.Invoke(currentValue, Normalized);
            TryComplete();
        }

        public void ResetProgress()
        {
            currentValue = minValue;
            _completedFired = false;
            OnReset?.Invoke();
            OnChanged?.Invoke(currentValue, Normalized);
        }

        private void TryComplete()
        {
            if (!completeWhenMax || _completedFired || !IsComplete) return;
            _completedFired = true;
            OnCompleted?.Invoke();
        }
    }
}
