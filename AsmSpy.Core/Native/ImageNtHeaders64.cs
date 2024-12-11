using System.Runtime.InteropServices;

namespace AsmSpy.Core.Native
{
    [StructLayout(LayoutKind.Explicit)]
    internal struct ImageNtHeaders64
    {
        [FieldOffset(24)]
        public ImageOptionalHeader64 OptionalHeader;
    }
}