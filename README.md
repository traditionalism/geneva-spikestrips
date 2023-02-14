# geneva-spikestrips

To edit it, open `spikestrips.sln` in Visual Studio (or an equivalent).

To build it, run `build.cmd`. To run it, run the following commands to make a symbolic link in your server data directory:

```dos
cd /d [PATH TO THIS RESOURCE]
mklink /d X:\cfx-server-data\resources\[local]\spikestrips dist
```

Afterwards, you can use `ensure spikestrips` in your server.cfg or server console to start the resource.

## Features
* Server-side spawning of spikestrips (See: [Server-side entity persistence](https://docs.fivem.net/docs/scripting-reference/onesync/#i-want-persistent-entities-how-do-i-do-it) & [Entity lockdown mode](https://docs.fivem.net/docs/scripting-reference/onesync/#entity-lockdown)).
* `deleteallspikes` command for staff restricted using FiveM's built-in access control system.
* Automatic cleanup of leftover spikestrips (whenever the server empties and someone forgets to delete their spikestrips).
* Reasonable performance when you're not near any spikestrips.
### Planned
* Blips for spikestrips you spawn.
* Maybe a better simulation of IRL spikestrips(?)

## Configuration
geneva-spikestrips is completely confirgued using the `fxmanifest.lua` file, the manifest metadata entries at the bottom are currently the only configurable options. Although, I plan to add more in the near future.