using System;
using System.Runtime.CompilerServices;


namespace War
{
    public static class StructInterfaceExtension
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DisposeRef<T>(this ref T disposable)
            where T : struct, IDisposable
        {
            // 여기가 constrained call로 컴파일되어 boxing 없이 호출됩니다.
            disposable.Dispose();
        }
    }
}