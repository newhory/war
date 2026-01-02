using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;


namespace War.VAT
{
    public partial class VertexAnimationBehaviourSystem : SystemBase
    {
        private static HashSet<VertexAnimationBehaviour> behaviours;
        

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init() => behaviours = new HashSet<VertexAnimationBehaviour>();

        public static void RegisterBehaviour(VertexAnimationBehaviour behaviour) => behaviours.Add(behaviour);
        public static void UnregisterBehaviour(VertexAnimationBehaviour behaviour) => behaviours.Remove(behaviour);

        protected override void OnUpdate()
        {
            foreach (VertexAnimationBehaviour behaviour in behaviours)
            {
                behaviour.OnUpdate();
            }
        }
    }
}