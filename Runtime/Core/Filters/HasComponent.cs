using UnityEngine;

namespace NekoSignal
{
    /// <summary>
    /// Allow only subscribers whose owner has component T.
    /// </summary>
    public sealed class HasComponent<T> : ISignalFilter where T : Component
    {
        public bool Evaluate(MonoBehaviour owner) => owner != null && owner.GetComponent<T>() != null;
    }
}
