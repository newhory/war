using Unity.Entities;

namespace War.Game
{
    public struct Attack : IComponentData
    {
        public enum Step
        {
            NotYet = 0,
            Attacked,
            End,
            Delay,
        }

        public Step AttackStep;
        public double AttackTime;
    }

    public struct AttackPower : IComponentData
    {
        public float Value;
    }

    public struct AttackRange : IComponentData
    {
        public float Value;
    }

    public struct AttackData : IComponentData
    {
        public float Duration;
        public float HitTime;
        public float Delay;
    }
}