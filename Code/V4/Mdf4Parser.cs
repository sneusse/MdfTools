using System;
using System.Collections.Generic;
using MdfTools.Shared;
using MdfTools.Utils;

namespace MdfTools.V4
{
    internal sealed class Mdf4Parser : IDisposable
    {
        private readonly List<Mdf4Block> _allBlocks = new List<Mdf4Block>();
        private readonly Dictionary<long, Mdf4Block> _blockCache = new Dictionary<long, Mdf4Block>();

        internal readonly Mdf4File Mdf4File;

        public FastBinaryReader Reader { get; }


        public Mdf4Parser(string filename)
        {
            Reader = new FastBinaryReader(filename);
            Mdf4File = new Mdf4File(this, filename);
        }

        public void Dispose()
        {
            Reader?.Dispose();
        }

        public Mdf4File Open()
        {
            using var time = Mdf4File.Metrics.TimeOpening.Measure();

            // do something with id stuff which is quite useless.
            // maybe branch to mdf3?<
            ref var rawIdBlock = ref Reader.GetRaw<IDBlockRaw>(0);

            var header = GetBlock<Mdf4HDBlock>(64);

            foreach (var dgBlock in header.DGBlocks)
            foreach (var cgBlock in dgBlock.CGBlocks)
            {
                var group = new Mdf4ChannelGroup(Mdf4File, cgBlock, dgBlock);

                foreach (var cnBlock in cgBlock.CNBlocks)
                {
                    var channel = new Mdf4Channel(group, cnBlock);
                    group.Add(channel);
                }

                Mdf4File.ChannelGroupsInternal.Add(group);
            }

            return Mdf4File;
        }


        public void DumpInfo()
        {
            ref var rawIdBlock = ref Reader.GetRaw<IDBlockRaw>(0);

            Console.WriteLine($"Format version {rawIdBlock.IdVer / 100}.{rawIdBlock.IdVer % 100}");

            var stats = new Dictionary<BlockId, long>();
            var zstats = new Dictionary<BlockId, long>();
            var seen = new HashSet<long>();
            var blockStack = new Stack<long>();
            var lateList = new List<Mdf4Block>();
            ulong rawData = 0;
            ulong zipData = 0;

            foreach (BlockId blockId in Enum.GetValues(typeof(BlockId)))
            {
                stats[blockId] = 0;
                zstats[blockId] = 0;
            }

            blockStack.Push(64);
            while (blockStack.Count > 0)
            {
                var pos = blockStack.Pop();
                ref var rawHeader = ref Reader.GetRaw<BlockHeader>(pos);

                if (!stats.ContainsKey(rawHeader.Id))
                {
                    Console.WriteLine($"-- Unknown Block @ {pos}");
                    break;
                }

                var block = GetBlock(pos);
                seen.Add(block.Offset);
                lateList.Add(block);
                stats[rawHeader.Id]++;

                if (rawHeader.Id == BlockId.MdfBlockDZ)
                {
                    var dz = Mdf4Block.Create(this, pos, _blockCache) as Mdf4DZBlock;
                    zipData += dz.Data.CompressedDataLength;
                    rawData += dz.Data.UncompressedDataLength;
                    var zid = Reader.GetRaw<BlockId>(block.BlockDataOffset);
                    zstats[zid]++;
                }

                foreach (var blockLink in block.Links)
                {
                    var link = blockLink;
                    if (link != 0 && !seen.Contains(link))
                        blockStack.Push(link);
                }
            }

            foreach (BlockId blockId in Enum.GetValues(typeof(BlockId)))
                Console.WriteLine($"Block {blockId}: {stats[blockId]} (zipped: {zstats[blockId]})");

            Console.WriteLine($"Raw data: ${FormatUtils.GetBytesReadable((long)rawData)}");
            Console.WriteLine($"Zip data: ${FormatUtils.GetBytesReadable((long)zipData)}");
        }


        public Mdf4Block GetBlock(long link)
        {
            using var time = Mdf4File.Metrics.BlockCreation.Measure();

            if (link == 0)
                return null;
            if (_blockCache.TryGetValue(link, out var block))
                return block;

            var blk = Mdf4Block.Create(this, link, _blockCache);

            return blk;
        }

        public T GetBlock<T>(long link) where T : Mdf4Block
        {
            return (T) GetBlock(link);
        }
    }
}
