using System.Runtime.InteropServices;

namespace AsmSpy.Core.Native
{
    [StructLayout(LayoutKind.Explicit)]
    internal struct ImageOptionalHeader64
    {
        [FieldOffset(0)]
        public ushort Magic;
        [FieldOffset(108)]
        public ushort NumberOfRvaAndSizes;
        [FieldOffset(224)]
        public ImageDataDirectory ComHeaderDirectory;
    }
}