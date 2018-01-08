using System;
using Equinox.Utils.Stream;
using VRage;
using VRage.Voxels;
using VRageMath;

namespace Equinox.ProceduralWorld.Voxels.VoxelBuilder
{
    // This class lets you write a .vx2 file for a procedural voxel map.
    // Most of this code is Keen's, pulled from the GitHub
    public partial class OctreeStorageBuilder
    {
        public const int LeafLodCount = 4;
        public const int LeafSizeInVoxels = 1 << LeafLodCount;

        public IStorageDataProviderBuilder DataProvider;

        private Vector3I m_size;
        public Vector3I Size
        {
            get { return m_size; }
            set
            {
                m_size = value;
                InitTreeHeight();
            }
        }

        private int m_treeHeight;
        private void InitTreeHeight()
        {
            var sizeLeaves = Size >> LeafLodCount;
            m_treeHeight = -1;
            var lodSize = sizeLeaves;
            while (lodSize != Vector3I.Zero)
            {
                lodSize >>= 1;
                ++m_treeHeight;
            }

            if (m_treeHeight < 0) m_treeHeight = 1;
        }

        public OctreeStorageBuilder(IStorageDataProviderBuilder dataProvider, Vector3I size)
        {
            {
                var tmp = MathHelper.Max(size.X, size.Y, size.Z);
                Size = new Vector3I(MathHelper.GetNearestBiggerPowerOfTwo(tmp));
            }
            DataProvider = dataProvider;
            InitTreeHeight();
        }

        public enum ChunkTypeEnum : ushort
        { // Changing values will break backwards compatibility!
            StorageMetaData = 1,
            MaterialIndexTable = 2,
            MacroContentNodes = 3,
            MacroMaterialNodes = 4,
            ContentLeafProvider = 5,
            ContentLeafOctree = 6,
            MaterialLeafProvider = 7,
            MaterialLeafOctree = 8,
            DataProvider = 9,

            EndOfFile = ushort.MaxValue,
        }

        public struct ChunkHeader
        {
            public ChunkTypeEnum ChunkType;
            public int Version;
            public int Size;

            public void WriteTo(MemoryStream stream)
            {
                stream.Write7BitEncodedInt((ushort)ChunkType);
                stream.Write7BitEncodedInt(Version);
                stream.Write7BitEncodedInt(Size);
            }
        }

        protected void SaveInternal(MemoryStream stream)
        {
            WriteStorageMetaData(stream);
            WriteMaterialTable(stream);
            WriteDataProvider(stream, DataProvider);
            WriteOctreeNodes(stream, ChunkTypeEnum.MacroContentNodes);
            WriteOctreeNodes(stream, ChunkTypeEnum.MacroMaterialNodes);

            var cellCoord = new MyCellCoord(m_treeHeight, ref Vector3I.Zero);
            var leafId = cellCoord.PackId64();
            cellCoord.Lod += LeafLodCount;

            WriteEmptyProviderLeaf(stream, leafId, ChunkTypeEnum.ContentLeafProvider);
            WriteEmptyProviderLeaf(stream, leafId, ChunkTypeEnum.MaterialLeafProvider);

            new ChunkHeader()
            {
                ChunkType = ChunkTypeEnum.EndOfFile,
            }.WriteTo(stream);
        }

        protected const string STORAGE_TYPE_NAME_OCTREE = "Octree";
        protected const int STORAGE_TYPE_VERSION_OCTREE = 1;

        public byte[] GetCompressedData()
        {
            MemoryStream ms;
            byte[] copy;
            using (ms = MemoryStream.CreateEmptyStream(0x4000))
            {
                ms.Write(STORAGE_TYPE_NAME_OCTREE);
                ms.Write7BitEncodedInt(STORAGE_TYPE_VERSION_OCTREE);
                SaveInternal(ms);
                copy = new byte[ms.WriteHead];
                Array.Copy(ms.Backing, 0, copy, 0, ms.WriteHead);
            }
            var lenPData = MyCompression.Compress(copy);
            // Compress adds an int for some reason.
            var output = new byte[lenPData.Length - 4];
            Array.Copy(lenPData, 4, output, 0, lenPData.Length - 4);
            return output;
        }
    }
}
