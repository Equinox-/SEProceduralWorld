using Equinox.Utils.Stream;

namespace Equinox.ProceduralWorld.Voxels.VoxelBuilder
{
    public interface IStorageDataProviderBuilder
    {
        int ProviderTypeId { get; }
        int SerializedSize { get; }

        void WriteTo(MemoryStream stream);
    }
}
