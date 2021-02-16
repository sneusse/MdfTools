# MdfTools

A MDF 4.x Reader (for now)

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

* FH Block - File History Block
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
  * Can calculated on the fly if neccessary.
  
# Roadmap

* [ ] Validate some example files
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


