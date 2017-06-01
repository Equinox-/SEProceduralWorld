using System;
using Equinox.Utils.DotNet;
using Sandbox.Definitions;

namespace Equinox.ProceduralWorld.Voxels.VoxelBuilder
{
    public partial class MyOctreeStorageBuilder
    {
        private void WriteStorageMetaData(MyMemoryStream stream)
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
        private static void WriteOctreeNodes(MyMemoryStream stream, ChunkTypeEnum type)
        {
            new ChunkHeader()
            {
                ChunkType = type,
                Version = CURRENT_VERSION_OCTREE_NODES,
                Size = 0
            }.WriteTo(stream);
        }

        private const int VERSION_OCTREE_LEAVES_32BIT_KEY = 2; // also version 1

        private static void WriteMaterialTable(MyMemoryStream stream)
        {
            var materials = MyDefinitionManager.Static.GetVoxelMaterialDefinitions();
            using (var ms = MyMemoryStream.CreateEmptyStream(1024))
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

        private static void WriteEmptyProviderLeaf(MyMemoryStream stream, UInt64 key, ChunkTypeEnum type)
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

        private static void WriteDataProvider(MyMemoryStream stream, IMyStorageDataProviderBuilder provider)
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