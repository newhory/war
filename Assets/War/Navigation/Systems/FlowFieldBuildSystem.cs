using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;


namespace War.Navigation.Systems
{
    [UpdateInGroup(typeof(Group.NavigationSystemGroup))]
    [UpdateAfter(typeof(StandingToggleSystem))]
    [BurstCompile]
    public partial struct FlowFieldBuildSystem : ISystem, ISystemStartStop
    {
        private uint _frameCount;
        private uint _throttleFrames; // 갱신 주기


        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NavigationGrid>();

            _throttleFrames = 10; // 10프레임마다 재계산(대규모 변화 시에만 더 자주)
        }

        public void OnDestroy(ref SystemState state) => FlowFieldProvider.Dispose();

        public void OnUpdate(ref SystemState state)
        {
            // 스로틀: 지정한 프레임 간격으로만 업데이트
            if (_frameCount++ % _throttleFrames != 0)
            {
                return;
            }

            NavigationGrid navigationGrid = SystemAPI.GetSingleton<NavigationGrid>();

            FlowFieldProvider.Init(navigationGrid);
        }

        public void OnStartRunning(ref SystemState state) => _frameCount = 0;

        public void OnStopRunning(ref SystemState state)
        {
        }
    }
}