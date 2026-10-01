using System;

namespace Training.Core
{
    public interface IFlowController
    {
        event Action OnFlowCompleted;
        void StartFlow();
        void StopFlow();
    }
}
