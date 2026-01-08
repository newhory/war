using Unity.Entities;

namespace War.Game
{
    public struct ArrowSpawner : IComponentData
    {
        public Entity ArrowProtoType;
        public float ArrowScale;
    }
    
    public struct EffectSpawner : IComponentData
    {
        public Entity HitEffectProtoType;
        public float HitEffectDuration;
    }
}