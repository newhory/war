using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;


namespace War.Dots.Component
{
    [System.Serializable]
    public struct SoldierData
    {
        public Entity BlueTeamProtoType;
        public Entity RedTeamProtoType;
        
        [Header("Status")]
        public float health;
        
        [Header("Movement")]
        public float moveSpeed;
        public float moveAcceleration;
        
        [Header("State")]
        public float searchTargetRange;
        public float checkTargetInterval;
        
        [Header("Attack")]
        public float attackPower;
        public float attackDuration;
        public float attackHitTime;
        public float attackDelay;
        [Tooltip("유효 사거리. 피해를 입힐 수 있는 최대 거리")] public float attackRange;
        public SoldierWeaponType weapon;

        [Header("NavMeshAgent")]
        public float radius;
        
        [Header("Formation")]
        public float radiusInFormation;
    }
    
    public struct SpawnSoldierData : IBufferElementData
    {
        public Entity ProtoType;
        
        public TeamColor TeamColor;
        public SoldierType SoldierType;

        public float3 Position;
        public quaternion Rotation;

        public int Count;
    }
    
    public struct SoldierSpawner : IComponentData
    {
        public Entity SoldierProtoType;
        public Entity TroopProtoType;
        
        public SoldierData SpearSoldierData;
        public SoldierData ArcherSoldierData;
        public SoldierData ShieldSoldierData;
        public SoldierData CavalrySoldierData;
    }
}