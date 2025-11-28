using Unity.Collections;
using Unity.Entities;


namespace War.Dots.Component
{
    public enum SoldierType
    {
        Spear,
        Archer,
        Shield,
        Cavalry
    }

    public enum SoldierWeaponType
    {
        Melee,
        Arrow,
    }

    public struct Soldier : IComponentData
    {
        public int Id;
        public SoldierType Type;
    }

    public struct SoldierAttachedTroop : IComponentData
    {
        public int TroopId;
        public Entity TroopEntity;
    }

    public struct SoldierWeapon : IComponentData
    {
        public SoldierWeaponType Type;
    }

    public struct SoldierTargetForAttack : IComponentData
    {
        public Entity TargetSoldier;
    }

    public struct SoldierStateMoveInFormation : IComponentData, IEnableableComponent
    {
    }

    public struct SoldierStateMoveToTarget : IComponentData, IEnableableComponent
    {
    }

    public struct SoldierStateAttackTarget : IComponentData, IEnableableComponent
    {
    }

    public struct SoldierAnimation : IComponentData
    {
        public enum State
        {
            None,

            Default,
            Attack,
            Hit,
            Dead
        }

        public State Current;
        public State Next;
    }

    public struct SoldierSpatialHashMap : IComponentData
    {
        public NativeParallelMultiHashMap<int, Entity> SpatialHashMap;
    }
}