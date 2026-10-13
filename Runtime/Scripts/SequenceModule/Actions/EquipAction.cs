using UnityEngine;

namespace Training.Sequence
{
    /// <summary>
    /// Equip action – completes only when the correct item is placed
    /// into the correct socket / attach point.
    /// Call NotifyEquipped(item, socket) from your XR grab/socket logic.
    /// </summary>
    public class EquipAction : SequenceActionBase, ISequenceTarget, ISequenceValidatable
    {
        [Tooltip("The item that must be equipped.")]
        [SerializeField] private GameObject item;

        [Tooltip("Required socket / attach point. Leave null to skip socket validation.")]
        [SerializeField] private Transform socket;

        public GameObject Target => item;

        public bool Validate(out string error)
        {
            if (item == null)
            {
                error = "EquipAction requires an Item.";
                return false;
            }

            // Socket is intentionally optional
            error = null;
            return true;
        }

        protected override void OnStartAction()
        {
            // Waiting for external equip notification.
        }

        protected override void OnCancelAction()
        {
            // Nothing to clean up by default.
        }

        /// <summary>
        /// Call this when an item has been placed into a socket.
        /// Only completes if both item and socket match the required values.
        /// </summary>
        public void NotifyEquipped(GameObject equippedItem, Transform usedSocket)
        {
            if (IsCompleted) return;
            if (item == null || equippedItem != item) return;
            if (socket != null && usedSocket != socket) return;

            Complete();
        }
    }
}
