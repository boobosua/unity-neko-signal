using UnityEngine;

namespace NekoSignal
{
    /// <summary>
    /// A reusable filter for signals.   
    /// </summary>
    public interface ISignalFilter
    {
        bool Evaluate(MonoBehaviour owner);
    }
}
