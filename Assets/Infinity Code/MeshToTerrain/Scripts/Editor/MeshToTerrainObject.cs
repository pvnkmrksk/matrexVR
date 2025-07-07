/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using UnityEngine;

namespace InfinityCode.MeshToTerrain
{
    public class MeshToTerrainObject
    {
        public GameObject gameobject;
        public int layer;
        public MeshCollider tempCollider;
        public Transform originalParent;
        public bool temporary;
        public bool changedParent;

        public MeshToTerrainObject(GameObject gameObject)
        {
            gameobject = gameObject;
            layer = gameObject.layer;
        }
    }
}