using UnityEngine;
using Training.Flow.Controllers;

namespace Training.Flow.Components
{
    // Scene entry point. Attach to a GameObject alongside a
    // TrainingSessionController and it starts the session automatically,
    // or call Run() yourself from your own bootstrap/XR-rig-ready logic.
    public class FlowRunner : MonoBehaviour
    {
        [SerializeField] private TrainingSessionController _sessionController;
        [SerializeField] private bool _startOnAwake = true;

        private void Start()
        {
            if (_startOnAwake)
                Run();
        }

        public void Run()
        {
            _sessionController.StartSession();
        }
    }
}
