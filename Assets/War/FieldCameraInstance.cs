using Unity.Cinemachine;
using UnityEngine;


namespace War
{
    [RequireComponent(typeof(CinemachineCamera))]
    public class FieldCameraInstance : MonoBehaviour
    {
        public static CinemachineCamera FieldCamera { get; private set; }


        private void Awake()
        {
            if (!FieldCamera)
            {
                FieldCamera = GetComponent<CinemachineCamera>();
            }
        }
    }
}