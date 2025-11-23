using Unity.Entities;
using Unity.Mathematics;


namespace War.Dots.Component
{
    public struct SpawnArrow : IBufferElementData
    {
        public Entity Shooter;
        public TeamColor ShooterTeamColor;
        public Entity Target;
        public float Damage;
        public float MinSpeed;
        public float MaxPoiDeviation; // Max Point of impact deviation
        public float3 StartPosition;
        public float3 EndPosition;
    }

    public struct SpawnHitEffect : ICleanupBufferElementData
    {
        public float3 Position;
    }

    public struct Damaged : ICleanupBufferElementData
    {
        public Entity Hitter;
        public float HitDamage;
    }
}