using UnityEngine;

namespace NekoSignal
{
    /// <summary>A reusable filter evaluated against a subscriber's owner.</summary>
    public interface ISignalFilter
    {
        bool Evaluate(MonoBehaviour owner);
    }
}
