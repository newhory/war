using Unity.Mathematics;
using Unity.Entities;
using UnityEngine;
using UnityEngine.AI;


namespace War.Navigation.Systems
{
    [UpdateInGroup(typeof(Group.NavigationSystemGroup), OrderFirst = true)]
    public partial struct NavigationGridSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NavigationGrid>();
            state.RequireForUpdate<UpdateNavigationGrid>();
        }

        public void OnUpdate(ref SystemState state)
        {
            Entity navigationGridEntity = SystemAPI.GetSingletonEntity<NavigationGrid>();
            NavigationGrid navigationGrid = state.EntityManager.GetComponentData<NavigationGrid>(navigationGridEntity);

            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            if (triangulation.vertices is not null && triangulation.vertices.Length > 0)
            {
                Vector3 min = triangulation.vertices[0];
                Vector3 max = triangulation.vertices[0];

                foreach (Vector3 v in triangulation.vertices)
                {
                    min = Vector3.Min(min, v);
                    max = Vector3.Max(max, v);
                }

                navigationGrid.Extents = (max - min) * 0.5f;
                navigationGrid.Center = (float3)min + navigationGrid.Extents;

                navigationGrid.NeighborHashGridSize =
                    new int2(
                        (int)math.ceil(navigationGrid.Size.x / navigationGrid.NeighborHashCellSize),
                        (int)math.ceil(navigationGrid.Size.y / navigationGrid.NeighborHashCellSize));

                navigationGrid.FlowFieldGridSize =
                    new int2(
                        (int)math.ceil(navigationGrid.Size.x / navigationGrid.FlowFieldCellSize),
                        (int)math.ceil(navigationGrid.Size.y / navigationGrid.FlowFieldCellSize));

                state.EntityManager.SetComponentData(navigationGridEntity, navigationGrid);
            }

            state.EntityManager.RemoveComponent<UpdateNavigationGrid>(navigationGridEntity);
        }

        public void OnDestroy(ref SystemState state)
        {
        }
    }
}