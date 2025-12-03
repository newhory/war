using UnityEngine;
using TMPro;

namespace War
{
    [RequireComponent(typeof(TMP_Text))]
    public class FPSDisplay : MonoBehaviour
    {
        private TMP_Text _fpsText;

        private float _deltaTime;


        private void Awake()
        {
            _fpsText = GetComponent<TMP_Text>();
        }

        private void Update()
        {
            // 지수 이동 평균으로 프레임 시간 안정화
            _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;

            float ms = _deltaTime * 1000.0f; // 프레임당 ms
            float fps = 1.0f / _deltaTime; // FPS

            // "F0"는 정수, "F1"은 소수점 1자리
            _fpsText.text = $"{fps:F0} FPS ({ms:F1} ms)";
        }
    }
}