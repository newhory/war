using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Pool;


namespace War.Dots.Component
{
    public struct Alive : IComponentData, IEnableableComponent
    {
    }

    public struct Movable : IComponentData, IEnableableComponent
    {
    }

    public struct Rotatable : IComponentData, IEnableableComponent
    {
    }

    public struct UseDefaultMaxSpeed : IComponentData, IEnableableComponent
    {
    }

    public struct Velocity : IComponentData
    {
        public float3 Value;
    }

    public struct Acceleration : IComponentData
    {
        public float3 Value;
        public float Max;
    }

    public struct MoveSpeed : IComponentData
    {
        public float Current;
        public float Max;
        public float CurrentMax;
    }

    public struct Forward : IComponentData
    {
        public float3 Value;
    }

    public struct Destination : IComponentData
    {
        public float3 Position;
    }

    public struct SearchTargetRange : IComponentData
    {
        public float Value;
    }

    public struct Health : IComponentData
    {
        public float Value;
    }

    public struct Damaged : ICleanupBufferElementData
    {
        public Entity Hitter;
        public float HitDamage;
    }

    public struct DestroyOn : IComponentData
    {
        public double DestroyTime;
    }

    public struct SpawnHitEffect : ICleanupBufferElementData
    {
        public float3 Position;
    }

    public struct NavMeshAgentData : IComponentData
    {
        public float Radius;
    }

    public class PooledGameObject : IComponentData
    {
        public PooledObject<GameObject> PooledObject;
    }

    public struct UnityTransform : IComponentData
    {
        public UnityObjectRef<Transform> Transform;
    }

    public struct UnityAnimator : IComponentData
    {
        public UnityObjectRef<Animator> Animator;
    }

    public struct UnityNavMeshAgent : IComponentData
    {
        public UnityObjectRef<NavMeshAgent> Agent;
    }

    public struct UnityNavMeshObstacle : IComponentData
    {
        public UnityObjectRef<NavMeshObstacle> Obstacle;
    }
}