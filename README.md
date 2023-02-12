# geneva-spikestrips

To edit it, open `spikestrips.sln` in Visual Studio (or an equivalent).

To build it, run `build.cmd`. To run it, run the following commands to make a symbolic link in your server data directory:

```dos
cd /d [PATH TO THIS RESOURCE]
mklink /d X:\cfx-server-data\resources\[local]\spikestrips dist
```

Afterwards, you can use `ensure spikestrips` in your server.cfg or server console to start the resource.

## Configuration

This resource is completely confirgued using the `fxmanifest.lua` file, the manifest metadata entries at the bottom are currently the only configurable options. Although, I plan to add more in the near future.