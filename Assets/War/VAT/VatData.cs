using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace War.VAT
{
    [Serializable]
    public class VatMeshData : IComponentData
    {
        public Mesh mesh;
        public Material material;
    }

    [Serializable]
    public class VatClipData
    {
        /// <summary>
        /// Animation Clip Name
        /// </summary>
        public string keyword;

        /// <summary>
        /// Start Frame in VAT
        /// </summary>
        public int startFrame;

        /// <summary>
        /// End Frame in VAT
        /// </summary>
        public int endFrame;

        /// <summary>
        /// FPS of animation clip
        /// </summary>
        public float fps;
        
        public bool loop;
        
        public int FrameCount => endFrame - startFrame + 1;
    }

    public class VatData : ScriptableObject
    {
        public List<VatMeshData> meshData;
        public List<VatClipData> clipData;
    }
}