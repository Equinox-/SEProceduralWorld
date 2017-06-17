using Equinox.Utils.Stream;

namespace Equinox.ProceduralWorld.Voxels.VoxelBuilder
{
    public interface IMyStorageDataProviderBuilder
    {
        int ProviderTypeId { get; }
        int SerializedSize { get; }

        void WriteTo(MyMemoryStream stream);
    }
}
