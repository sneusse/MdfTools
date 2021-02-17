using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MdfTools.V4;

namespace MdfTools.Shared.Data.Base
{
    public class LodBuffer
    {
        public readonly SampleBuffer<double> Original;
        public readonly LodLayer[] LodLayers;

        private LodBuffer(SampleBuffer<double> original, List<LodLayer> layers)
        {
            Original = original;
            LodLayers = layers.ToArray();
        }


        public static LodBuffer Create(NumericBufferBase original, int minSamples = 256)
        {
            var size = Unsafe.SizeOf<double>();

            // allocate LOD buffer
            var ptr = Marshal.AllocHGlobal((IntPtr) (original.Span.Length * size));
            var sampleCount = original.Span.Length;
            var layers = new List<LodLayer>();

            // Lod 0 = original Data
            layers.Add(new LodLayer(original.HeapArray, sampleCount));
            IntPtr current = ptr;
            while (sampleCount > minSamples)
            {
                sampleCount = sampleCount >> 1;
                layers.Add(new LodLayer(current, sampleCount));
                current += sampleCount * size;
            }

            var lod = new LodBuffer(original, layers);

            bool isMaster = (original.Decodable is Mdf4Channel c && c.Master == c);
            if (isMaster)
            {
                lod.CalculateAvg();
            }
            else
            {
                lod.CalculateErr();
            }

            return lod;
        }

        private unsafe void CalculateAvg()
        {
            var avail = Original.Span.Length;

            // we always look 1 sample back -> start with 1
            for (int i = 1; i < avail; i += 2)
            {
                var prevIdx = i;

                // skip layer 0 which is the original data
                for (int layerIdx = 1; layerIdx < LodLayers.Length; layerIdx++)
                {
                    var prevLayer = LodLayers[layerIdx-1].Data;
                    var layer = LodLayers[layerIdx].Data;
                    
                    var t0 = prevLayer[prevIdx-1];
                    var t1 = prevLayer[prevIdx];
                    var idx = prevIdx >> 1;
                    layer[idx] = (t1 + t0) / 2;

                    // alignment lost
                    if ((idx & 1) == 1)
                        break;

                    prevIdx = idx;
                }
            }
        }

        private unsafe void CalculateErr()
        {
            var avail = Original.Span.Length;

            // we always look 1 sample back -> start with 1
            for (int i = 1; i < avail; i += 2)
            {
                var prevIdx = i;

                // skip layer 0 which is the original data
                for (int layerIdx = 1; layerIdx < LodLayers.Length; layerIdx++)
                {
                    var prevLayer = LodLayers[layerIdx - 1].Data;
                    ref var layer = ref LodLayers[layerIdx];

                    var v0 = prevLayer[prevIdx - 1];
                    var v1 = prevLayer[prevIdx];
                    var idx = prevIdx >> 1;

                    var last = layer.Last;
                    var e0 = Math.Abs(last - v0);
                    var e1 = Math.Abs(last - v1);

                    var next = e0 > e1 ? v0 : v1;

                    layer.Data[idx] = next;
                    layer.Last = next;

                    // alignment lost
                    if ((idx & 1) == 1)
                        break;

                    prevIdx = idx;
                }
            }
        }

        public struct LodLayer
        {
            public readonly IntPtr Ptr;
            public readonly int Length;
            internal double Last;
            public unsafe Span<double> Span => new Span<double>(Ptr.ToPointer(), Length);

            internal unsafe double* Data => (double*) Ptr;

            public LodLayer(in IntPtr current, in int sampls)
            {
                Ptr = current;
                Length = sampls;
                Last = 0;
            }
        }
    }
}
