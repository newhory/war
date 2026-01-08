using Unity.Entities;
using UnityEngine;

namespace War.Game.Authoring
{
    public class TeamAuthoring : MonoBehaviour
    {
        [SerializeField] private TeamColor color;
        

        private class Baker : Baker<TeamAuthoring>
        {
            public override void Bake(TeamAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new Team { Color = authoring.color });
            }
        }
    }
}