using NekoLib.Extensions;
using UnityEngine;

namespace NekoSignal
{
    /// <summary>
    /// Allow only subscribers whose owner GameObject is on the specified layer.
    /// </summary>
    public sealed class InLayer : ISignalFilter
    {
        private readonly LayerMask _layer;

        public InLayer(LayerMask layer) { _layer = layer; }

        public bool Evaluate(MonoBehaviour owner) => owner != null && owner.gameObject.IsInLayer(_layer);
    }
}
