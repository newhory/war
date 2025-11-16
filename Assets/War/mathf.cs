using Unity.Mathematics;


namespace War
{
    public class mathf
    {
        public static bool Approximately(float a, float b, float epsilon = 1e-6f) => math.abs(a - b) < epsilon;
        public static bool Approximately(float2 a, float2 b, float epsilon = 1e-6f) => math.all(math.abs(a - b) < epsilon);
        public static bool Approximately(float3 a, float3 b, float epsilon = 1e-6f) => math.all(math.abs(a - b) < epsilon);
    }
}