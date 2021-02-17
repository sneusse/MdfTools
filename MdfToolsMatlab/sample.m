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


