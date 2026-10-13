using UnityEngine;
using Training.Sequence;

namespace Training.Integration
{
    /// <summary>
    /// Mouse-click helper for sequence steps in Play Mode (no XR required).
    /// </summary>
    public class CombinedSequenceInput : MonoBehaviour
    {
        public SelectAction SelectHelmet;
        public EquipAction EquipGloves;
        public SelectAction SelectGlasses;
        public GameObject Helmet, Gloves, Glasses;
        public Transform Socket;

        private void Update()
        {
            if (!Input.GetMouseButtonDown(0) || Camera.main == null) return;
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 100f)) return;
            var go = hit.collider.gameObject;
            if (go == Helmet) SelectHelmet?.NotifySelected(Helmet);
            else if (go == Gloves) EquipGloves?.NotifyEquipped(Gloves, Socket);
            else if (go == Glasses) SelectGlasses?.NotifySelected(Glasses);
        }
    }
}
