using Unity.Entities;


namespace War.Dots.Component
{
    public struct StateMoveInFormation : IComponentData, IEnableableComponent
    {
    }

    public struct StateMoveToTarget : IComponentData, IEnableableComponent
    {
    }

    public struct StateAttackTarget : IComponentData, IEnableableComponent
    {
    }
}