using UnityEngine;

namespace Seabright
{
    // A small picking collider keeps property selection attached to the visible building,
    // even though the actual render geometry is combined into material batches.
    public sealed class CityLot : MonoBehaviour
    {
        public int X, Z;
    }
}
