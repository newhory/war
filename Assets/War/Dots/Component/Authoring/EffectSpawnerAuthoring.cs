using Unity.Entities;
using UnityEngine;


namespace War.Dots.Component.Authoring
{
    public class EffectSpawnerAuthoring : MonoBehaviour
    {
        [SerializeField] private GameObject hitEffectPrefab;
        [SerializeField] private float hitEffectDuration;


        private class Baker : Baker<EffectSpawnerAuthoring>
        {
            public override void Bake(EffectSpawnerAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);

                AddComponent(entity, new EffectSpawner
                {
                    HitEffectProtoType = GetEntity(authoring.hitEffectPrefab, TransformUsageFlags.Dynamic),
                    HitEffectDuration = authoring.hitEffectDuration
                });
            }
        }
    }
}