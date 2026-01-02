using System.Threading;
using System.Collections.Generic;
using UnityEngine;
using ZLinq;
using Cysharp.Threading.Tasks;


namespace War.VAT
{
    public class VertexAnimationBehaviour : MonoBehaviour
    {
        [SerializeField] private VatData vatData;
        [SerializeField] private string defaultClip = "idle";


#pragma warning disable UDR0001
        private static int s_frameIndex = -1;
#pragma warning restore UDR0001

        private List<MeshRenderer> _meshRenderers;
        private Dictionary<string, VatClipData> _vatClips;

        private CancellationTokenSource _ctsAfterStart;

        private VatClipData _vatClipData;
        private string _currentClip;
        private MaterialPropertyBlock _materialPropertyBlock;
        private float _animTime;
        private bool _isPlaying;


        private void Awake()
        {
            if (s_frameIndex < 0)
            {
                s_frameIndex = Shader.PropertyToID("_FrameIndex");
            }
        }

        private void Start()
        {
            _materialPropertyBlock = new MaterialPropertyBlock();

            _meshRenderers = new List<MeshRenderer>();

            Transform trans = transform;

            foreach (VatMeshData meshData in vatData.meshData)
            {
                GameObject go = new(meshData.mesh.name);

                MeshFilter meshFilter = go.AddComponent<MeshFilter>();
                meshFilter.mesh = meshData.mesh;

                MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
                meshRenderer.material = meshData.material;

                _meshRenderers.Add(meshRenderer);

                Transform vatTransform = go.transform;
                vatTransform.SetParent(trans);
                vatTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                vatTransform.localScale = Vector3.one;
            }

            _vatClips = vatData.clipData.AsValueEnumerable().ToDictionary(clipData => clipData.keyword);

            if (!string.IsNullOrEmpty(defaultClip))
            {
                PlayClip(defaultClip);
            }
        }

        private void OnEnable()
        {
            VertexAnimationBehaviourSystem.RegisterBehaviour(this);
        }

        private void OnDisable()
        {
            _ctsAfterStart?.Cancel();
            _ctsAfterStart?.Dispose();

            VertexAnimationBehaviourSystem.UnregisterBehaviour(this);

            _currentClip = string.Empty;
            _vatClipData = null;
        }

        public void OnUpdate()
        {
            if (!_isPlaying || _vatClipData is null)
            {
                return;
            }

            // 시간 기반 애니메이션 진행
            _animTime += Time.deltaTime * _vatClipData.fps;

            // 현재 프레임과 다음 프레임 계산
            int currentFrame = Mathf.FloorToInt(_animTime);
            int frameIndex = _vatClipData.startFrame;

            // 루프 여부에 따라 처리
            if (_vatClipData.loop)
            {
                currentFrame %= _vatClipData.FrameCount;

                frameIndex += currentFrame;
            }
            else
            {
                frameIndex += currentFrame;

                if (frameIndex >= _vatClipData.endFrame)
                {
                    frameIndex = _vatClipData.endFrame;

                    _isPlaying = false;
                }
            }

            // 보간 값 (0~1)
            //float blend = animTime - Mathf.Floor(animTime);

            // PropertyBlock에 값 설정
            _materialPropertyBlock.SetFloat(s_frameIndex, frameIndex);
            //_materialPropertyBlock.SetFloat(frameBlendID, blend);

            foreach (MeshRenderer meshRenderer in _meshRenderers)
            {
                meshRenderer.SetPropertyBlock(_materialPropertyBlock);
            }
        }

        public void PlayClip(string clipName, bool resetTime = false)
        {
            if (_isPlaying && !resetTime && _currentClip == clipName)
            {
                return;
            }

            if (!_vatClips.TryGetValue(clipName, out VatClipData vatClipData))
            {
                return;
            }

            _currentClip = clipName;
            _vatClipData = vatClipData;

            if (resetTime)
            {
                _animTime = 0f;
            }

            _isPlaying = true;
        }

        public void Stop()
        {
            _isPlaying = false;
        }
    }
}