using UnityEngine;

namespace NekoSignal
{
    /// <summary>
    /// Allow only subscribers whose owner GameObject has the given Unity tag.
    /// </summary>
    public sealed class WithTag : ISignalFilter
    {
        private readonly string _tag;

        public WithTag(string tag) { _tag = tag; }

        public bool Evaluate(MonoBehaviour owner) => owner != null && owner.CompareTag(_tag);
    }
}
