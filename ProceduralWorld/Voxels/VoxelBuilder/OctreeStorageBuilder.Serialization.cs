using System;
using Equinox.Utils.Stream;
using Sandbox.Definitions;

namespace Equinox.ProceduralWorld.Voxels.VoxelBuilder
{
    public partial class OctreeStorageBuilder
    {
        private void WriteStorageMetaData(MemoryStream stream)
        {
            new ChunkHeader()
            {
                ChunkType = ChunkTypeEnum.StorageMetaData,
                Version = 1,
                Size = sizeof(int) * 4 + 1,
            }.WriteTo(stream);

            stream.Write(LeafLodCount);
            stream.Write(Size.X);
            stream.Write(Size.Y);
            stream.Write(Size.Z);
            stream.Write(m_defaultMaterial);
        }

        private const int VERSION_OCTREE_NODES_32BIT_KEY = 1;
        private const int CURRENT_VERSION_OCTREE_NODES = 2;

        protected byte m_defaultMaterial = MyDefinitionManager.Static.GetDefaultVoxelMaterialDefinition().Index;

        // Write an empty node list.
        private static void WriteOctreeNodes(MemoryStream stream, ChunkTypeEnum type)
        {
            new ChunkHeader()
            {
                ChunkType = type,
                Version = CURRENT_VERSION_OCTREE_NODES,
                Size = 0
            }.WriteTo(stream);
        }

        private const int VERSION_OCTREE_LEAVES_32BIT_KEY = 2; // also version 1

        private static void WriteMaterialTable(MemoryStream stream)
        {
            var materials = MyDefinitionManager.Static.GetVoxelMaterialDefinitions();
            using (var ms = MemoryStream.CreateEmptyStream(1024))
            {
                ms.Write(materials.Count);
                foreach (var material in materials)
                {
                    ms.Write7BitEncodedInt(material.Index);
                    ms.Write(material.Id.SubtypeName);
                }

                new ChunkHeader()
                {
                    ChunkType = ChunkTypeEnum.MaterialIndexTable,
                    Version = 1,
                    Size = ms.WriteHead,
                }.WriteTo(stream);

                stream.Write(ms.Backing, 0, ms.WriteHead);
            }
        }

        private const int CURRENT_VERSION_OCTREE_LEAVES = 3;

        private static void WriteEmptyProviderLeaf(MemoryStream stream, UInt64 key, ChunkTypeEnum type)
        {
            var header = new ChunkHeader()
            {
                ChunkType = type,
                // ReSharper disable once BuiltInTypeReferenceStyle
                Size = 0 + sizeof(UInt64), // increase chunk size by the size of key (which is inserted before it)
                Version = CURRENT_VERSION_OCTREE_LEAVES,
            };
            header.WriteTo(stream);

            stream.Write(key);
        }

        private static void WriteDefaultMicroOctreeLeaf(MemoryStream stream, UInt64 key, ChunkTypeEnum type, byte val)
        {
            var header = new ChunkHeader()
            {
                ChunkType = type,
                // ReSharper disable once BuiltInTypeReferenceStyle
                Size =
                    sizeof(int) + sizeof(byte) +
                    sizeof(UInt64), // increase chunk size by the size of key (which is inserted before it)
                Version = CURRENT_VERSION_OCTREE_LEAVES,
            };
            header.WriteTo(stream);

            stream.Write(4); // micro octree height
            stream.Write(val); // micro octree default value

            // don't write any nodes.  (this *should* be okay)
        }

        private static void WriteDataProvider(MemoryStream stream, IStorageDataProviderBuilder provider)
        {
            if (provider == null)
                return;

            ChunkHeader header = new ChunkHeader()
            {
                ChunkType = ChunkTypeEnum.DataProvider,
                Version = 2,
                Size = provider.SerializedSize + sizeof(Int32),
            };
            header.WriteTo(stream);
            stream.Write(provider.ProviderTypeId);
            provider.WriteTo(stream);
        }
    }
}