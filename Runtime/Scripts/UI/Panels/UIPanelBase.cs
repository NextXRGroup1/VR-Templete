using UnityEngine;
using Training.Core;

namespace Training.UI.Panels
{
    // Base for every content/question panel. Presents content and reports
    // interaction only — it never decides training progression (Section 22).
    public abstract class UIPanelBase : MonoBehaviour, IUIPanel
    {
        public virtual void Show()
        {
            gameObject.SetActive(true);
        }

        public virtual void Hide()
        {
            gameObject.SetActive(false);
        }

    }
}
