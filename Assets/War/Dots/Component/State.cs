using Unity.Entities;


namespace War.Dots.Component
{
    public struct TroopStateMoveToDestination : IComponentData, IEnableableComponent
    {
    }

    public struct TroopStateMoveToTarget : IComponentData, IEnableableComponent
    {
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
}