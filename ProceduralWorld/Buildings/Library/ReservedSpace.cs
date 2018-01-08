using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Library
{
    public struct ReservedSpace
    {
        public BoundingBox Box;
        public bool IsShared;
        public bool IsOptional;

        public ReservedSpace(Ob_Part.ReservedSpace r)
        {
            Box = new BoundingBox(r.Min, r.Max);
            IsShared = r.IsShared;
            IsOptional = r.IsOptional;
        }

        public Ob_Part.ReservedSpace GetObjectBuilder()
        {
            return new Ob_Part.ReservedSpace()
            {
                Min = Box.Min,
                Max = Box.Max,
                IsShared = IsShared,
                IsOptional = IsOptional
            };
        }
    }
}