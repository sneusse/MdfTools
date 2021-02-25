using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MdfTools.Shared.Data.Base;
using MdfTools.V4;

namespace MdfToolsMatlab
{
    public static class Extensions
    {
        public static bool OrderedFuzzyMatch(this string s, string toFind)
        {
            var idx = 0;
            foreach (var item in toFind)
            {
                idx = s.IndexOf(item, idx);
                if (idx == -1)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public class MdfChannelAdapter
    {
        public readonly Mdf4Channel Org;

        public string Name { get; }
        public string Comment { get; }
        public MdfChannelGroupAdapter Group { get; }

        public MdfChannelAdapter(MdfChannelGroupAdapter groupAdapter, Mdf4Channel org)
        {
            Org = org;
            Name = org.Name;
            Comment = org.Comment;
            Group = groupAdapter;
        }

        public override string ToString()
        {
            return Org.ToString();
        }
    }

    public class MdfChannelGroupAdapter
    {
        public readonly Mdf4ChannelGroup Org;

        public MdfChannelAdapter[] Channels { get; }
        public string Name { get; }
        public string Source { get; }

        public MdfChannelGroupAdapter(Mdf4ChannelGroup org)
        {
            Org = org;
            Name = org.Name;
            Source = org.Source;
            Channels = Org.Channels.Select(k => new MdfChannelAdapter(this, k)).ToArray();
        }

        public override string ToString()
        {
            return Org.ToString();
        }
    }

    public class MdfFileAdapter
    {
        private readonly Mdf4File _file;
        internal Dictionary<Mdf4Channel, MdfChannelAdapter> Mapping = new Dictionary<Mdf4Channel, MdfChannelAdapter>();
        internal List<ChannelList> _loadedLists = new List<ChannelList>();

        public MdfChannelGroupAdapter[] Groups { get; }
        public MdfChannelAdapter[] Channels { get; }


        public MdfFileAdapter(Mdf4File file)
        {
            _file = file;
            Groups = _file.ChannelGroups.Select(k => new MdfChannelGroupAdapter(k)).ToArray();
            Channels = Groups.SelectMany(k => k.Channels).ToArray();

            foreach (var mdfChannelAdapter in Channels)
            {
                Mapping[mdfChannelAdapter.Org] = mdfChannelAdapter;
            }
        }

        public ChannelList CreateSampler()
        {
            return new ChannelList(this);
        }

        public void Close()
        {
            foreach (var channelList in _loadedLists)
            {
                channelList.Clear();
            }

            _loadedLists.Clear();
            _file.Dispose();
        }

        public override string ToString()
        {
            return _file.ToString();
        }
    }


    public class MdfBufferAdapter : IDisposable
    {
        private readonly BufferView<Mdf4Channel> _channelSamples;
        private readonly BufferView<Mdf4Channel> _masterSamples;
        public MdfChannelAdapter Channel { get; }
        public MdfChannelAdapter Master { get; }

        public double[] Data => _channelSamples.GetData<double>();
        public double[] MasterData => _masterSamples.GetData<double>();

        public string GetDisplayValueOfSample(long index) =>
            Channel.Org.GetDisplayValue(Data[index]);

        public MdfBufferAdapter(MdfChannelAdapter masterChannel,
            MdfChannelAdapter channel,
            BufferView<Mdf4Channel> channelSamples, BufferView<Mdf4Channel> masterSamples)
        {
            _masterSamples = masterSamples;
            _channelSamples = channelSamples;
            Channel = channel;
            Master = masterChannel;
        }

        public override string ToString()
        {
            return $"{Channel}: {Data.Length}";
        }

        public void Dispose()
        {
            _channelSamples?.Dispose();
            _masterSamples?.Dispose();
        }
    }

    public class ChannelList
    {
        private readonly MdfFileAdapter _file;
        private MdfBufferAdapter[] _adapters = null;


        internal ChannelList(MdfFileAdapter file)
        {
            _file = file;
        }

        public int Count => Channels.Count;

        public List<MdfChannelAdapter> Channels { get; } = new List<MdfChannelAdapter>();

        public MdfBufferAdapter FindBuffer(string name)
        {
            return _adapters?.FirstOrDefault(k => k.Channel.Name == name);
        }

        private int AddHelper(IEnumerable<MdfChannelAdapter> toAdd)
        {
            if (_adapters != null)
            {
                return -1;
            }

            var chan = toAdd.Where(k => k != null).ToArray();
            if (chan.Length > 0)
            {
                Channels.AddRange(chan);
                return chan.Length;
            }

            return 0;
        }

        public int AddByName(string name)
        {
            var chan = _file.Channels.FirstOrDefault(k => k.Name == name);
            return AddHelper(new[] {chan});
        }

        public int AddByNameFuzzy(string name)
        {
            var chan = _file.Channels.Where(k => k.Name.ToLower().OrderedFuzzyMatch(name.ToLower())).ToArray();
            return AddHelper(chan);
        }

        public int AddByNameContained(string name)
        {
            var chan = _file.Channels.Where(k => k.Name.ToLower().Contains(name.ToLower())).ToArray();
            return AddHelper(chan);
        }

        public MdfBufferAdapter[] Load()
        {
            if (_adapters != null)
                return _adapters;

            var masters = Channels.Select(k => k.Group.Org.MasterChannel).ToArray();
            var chans = Channels.Select(k => k.Org).ToArray();
            var toLoad = masters.Concat(chans).Distinct().ToArray();

            var smp = Mdf4Sampler.LoadFull(toLoad);
            var grouped = smp.ToDictionary(k => k.Channel, k => k);

            MdfBufferAdapter[] adapters = new MdfBufferAdapter[smp.Length];

            for (int i = 0; i < smp.Length; i++)
            {
                var buf = smp[i];
                var chn = smp[i].Channel;
                var master = smp[i].Channel.Master;

                adapters[i] = new MdfBufferAdapter(
                    _file.Mapping[master], _file.Mapping[chn], buf, grouped[master]
                );
            }

            _adapters = adapters;

            return adapters;
        }

        public void Clear()
        {
            if (_adapters != null)
            {
                for (int i = 0; i < _adapters.Length; i++)
                {
                    _adapters[i].Dispose();
                }

                _adapters = null;
            }

            Channels.Clear();
        }
    }

    public class MdfApi
    {
        public static MdfFileAdapter Open(string filename)
            => new MdfFileAdapter(Mdf4File.Open(filename));
    }
}
