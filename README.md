# MdfTools

A MDF 4.x Reader (for now)

3 flavours:
- .NET Core/.NET 5
- .NET 4
- MATLAB (proxy using .NET 4)

## Goals

* Reading MDF 4.x
* Reading MDF 4.x fast
* Reading partial sets (e.g. sample 0 - 100 or 1e15 - 1e16) without loading everything into memory
* The ability to fully load `your common measurements`™ into memory (some 1-digit GB-sized files should just work™)
* Minimal dependencies

## Non-Goals

* Writing (for now, until I have an idea how the API should look)
  * Reading and writing MDF files is so fundamentally different that it makes sense to implement it separately
* Anything but "Numeric-Channels" (read: what is convertable to double)
  * Helpers to get Enum-Strings etc. might be added
* Reading unsorted files

## Design choices

* Sample buffers for numeric channels in unmanaged memory
  * Easier to interop with other stuff
  * Less pressure on the GC
* 64bit only, no async-IO
  * Memory-Mapped-Files are easier to work with for fragmented files like these. 
* https://github.com/ebiggers/libdeflate as inflate library as the .NET DeflateStream is just slower.

## Status

* Reading the files I have access to works, others might not.
* This is a very early stage, API and internals might change.
* ~ Half of the blocks are not yet implemented<sup>1</sup> (properly)
* Only linear conversion is implemented<sup>1</sup>
* Only host-byte order is implemented<sup>1</sup>

<sup>1</sup>: mostly because I don't have access to validation data/example files

### Missing:

* ~~FH Block - File History Block~~
* SD Block - Signal Data Block
* DV Block - Data Values Block
* LD Block - List Data Block
* AT Block - Attachment Block
* CH Block - Channel Hierarchy Block
* EV Block - Event Block
* CA Block - Channel Array Block

* DI Block + everything else "invalidation"

### Blocks that likely will never make it in this implementation

* SR, RD, RV, RI Blocks
  * Only relevant for plotting.
  * Not avaliable in all measurements so a fallback has to be implemented anyway.
  * Can be calculated on the fly if neccessary.
  
# Roadmap

* [x] Validate some example files
* [ ] Add option to disable/limit/set threading
* [ ] Maybe do an alternative stream interface for remote files?
* [ ] Implement missing blocks
* [ ] Add time based sample access
* [ ] Port the LOD Buffer to .NET?
* [ ] Support MDF 3.x

# How To use

* Clone this repo, reference the project in your solution.

```csharp

  static void Main(string[] args)
  {
      using var mf4 = Mdf4File.Open("cool_measurements.mf4");
      var example = mf4
                    .Channels
                    .FirstOrDefault(k => k.Name.ToLower().Contains("rpm_channel"));


      var samples = Mdf4Sampler.LoadFull(example, example.Master);

      var data = samples[0].GetSpan<double>();
      var time = samples[1].GetSpan<double>();

      // use the samples for something.
  }

```

* Use The `Mdf4File.Bench("foo.mf4");` to get a little bench/stats output

```

-- File Info........
# Groups in file   : 112
# Groups loaded    : 112
# Channels in file : 3777
# Channels loaded  : 3777
-- Data.............
Raw-bytes loaded   : 2,614 GB
Zip-bytes loaded   : 0 B
Samples loaded     : 22,809 Msamples
Read speed         : 1,158 GBps
Allocations        : 8,828 GB
-- Times............
Full load time     : 2,3s
Time opening       : CPU:3ms RT: 11066ms-11069ms
Block creation     : CPU:59ms RT: 11066ms-11160ms
BLI construction   : CPU:55ms RT: 11069ms-11124ms
Raw copies         : CPU:2638ms RT: 11161ms-13321ms
Inflate/Transpose  : CPU:0ms RT: -1ms--1ms
SampleReading      : CPU:10169ms RT: 11161ms-13321ms
Allocations        : CPU:114ms RT: 11161ms-13320ms
-- Parser stuff.....
Format version 4.0
Block MdfBlockCA: 0 (zipped: 0)
Block MdfBlockCC: 637 (zipped: 0)
Block MdfBlockHD: 1 (zipped: 0)
Block MdfBlockLD: 0 (zipped: 0)
Block MdfBlockMD: 3780 (zipped: 0)
Block MdfBlockRD: 0 (zipped: 0)
Block MdfBlockSD: 0 (zipped: 0)
Block MdfBlockCG: 112 (zipped: 0)
Block MdfBlockDG: 112 (zipped: 0)
Block MdfBlockCH: 0 (zipped: 0)
Block MdfBlockFH: 2 (zipped: 0)
Block MdfBlockDI: 0 (zipped: 0)
Block MdfBlockRI: 0 (zipped: 0)
Block MdfBlockSI: 3889 (zipped: 0)
Block MdfBlockDL: 154 (zipped: 0)
Block MdfBlockHL: 0 (zipped: 0)
Block MdfBlockCN: 3777 (zipped: 0)
Block MdfBlockSR: 0 (zipped: 0)
Block MdfBlockAT: 0 (zipped: 0)
Block MdfBlockDT: 19480 (zipped: 0)
Block MdfBlockDV: 0 (zipped: 0)
Block MdfBlockEV: 1 (zipped: 0)
Block MdfBlockRV: 0 (zipped: 0)
Block MdfBlockTX: 21625 (zipped: 0)
Block MdfBlockDZ: 0 (zipped: 0)

```

* To use with MATLAB:

```matlab

%% Add Net assembly
NET.addAssembly("C:\FULL\PATH\TO\MDFTOOLS\MdfToolsMatlab.dll");

%% open a random file
file = MdfToolsMatlab.MdfApi.Open("sample.mf4");

%% create a sampler object
smp = file.CreateSampler()

%% populate the sampler object

% returns the number of added channels
smp.AddByName("engrpm")

%% print all names in the current sampler

% .NET lists are 0 based!
for k = 0:smp.Channels.Count-1
    cname = smp.Channels.Item(k).Name.string;
    gname = smp.Channels.Item(k).Group.Name.string;
    disp([cname gname])
end

%% Load the sample data

% returns the buffers (and keeps a internal reference)
smp.Load()

% second load does only return the data
% the list contains all buffers including time channels etc.
buffers = smp.Load()

%% get the data

signal = smp.FindBuffer("engrpm");
rpm = signal.Data.double';
time = signal.MasterData.double';

%% do something with the data
plot(time, rpm)

%% clear the list to free the memory
smp.Clear()

%% dispose the file
file.Close()

```

