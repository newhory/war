using Unity.Entities;
using UnityEngine;


namespace War.Dots.Component.Authoring
{
    public class ArrowSpawnerAuthoring : MonoBehaviour
    {
        [SerializeField] private GameObject arrowPrefab;
        [SerializeField] private GameObject arrowPrefabForHybrid;
        [SerializeField] private float arrowScale = 1f;


        private class Baker : Baker<ArrowSpawnerAuthoring>
        {
            public override void Bake(ArrowSpawnerAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);

                AddComponent(entity, new ArrowSpawner
                {
                    ArrowProtoType = GetEntity(
#if HYBRID_ARROW
                        authoring.arrowPrefabForHybrid
#else
                        authoring.arrowPrefab
#endif
                        , TransformUsageFlags.Dynamic),
                    ArrowScale = authoring.arrowScale,
                });
            }
        }
    }
}