using System.Runtime.InteropServices;

namespace AsmSpy.Core.Native
{
    [StructLayout(LayoutKind.Explicit)]
    internal struct ImageNtHeaders32
    {
        [FieldOffset(24)]
        public ImageOptionalHeader32 OptionalHeader;
    }
}