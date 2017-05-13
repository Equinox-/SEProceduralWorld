using VRageMath;

namespace ProcBuild.Library
{
    public struct MyReservedSpace
    {
        public BoundingBox Box;
        public bool IsShared;
        public bool IsOptional;

        public MyReservedSpace(MyObjectBuilder_ReservedSpace r)
        {
            Box = new BoundingBox(r.Min, r.Max);
            IsShared = r.IsShared;
            IsOptional = r.IsOptional;
        }

        public MyObjectBuilder_ReservedSpace GetObjectBuilder()
        {
            return new MyObjectBuilder_ReservedSpace()
            {
                Min = Box.Min,
                Max = Box.Max,
                IsShared = IsShared,
                IsOptional = IsOptional
            };
        }
    }
}