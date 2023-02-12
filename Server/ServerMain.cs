using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

namespace spikestrips.Server
{
    public class ServerMain : BaseScript
    {
        private uint spikeModel = (uint)GetHashKey("p_ld_stinger_s");
        private string ourResourceName = GetCurrentResourceName();
        private List<int> spawnedStrips = new();
        private Vector3 spawnCoords;
        private int numDeleting = 0;

        [EventHandler("onResourceStop")]
        private void OnResourceStop(string resourceName)
        {
            if (resourceName != ourResourceName || spawnedStrips.Count == 0) return;
            DeleteAllStrips();
        }

        [Tick]
        private async Task Cleanup()
        {
            await Delay(30000);

            int playerCount = Players.Count();
            if (playerCount == 0 && spawnedStrips.Count > 0)
            {
                DeleteAllStrips();
            }
        }

        private void DeleteAllStrips()
        {
            numDeleting = spawnedStrips.Count;
            Debug.WriteLine($"Deleting {numDeleting} spikestrip(s).");
            List<int> handlesToRemove = new List<int>();
            foreach (int handle in spawnedStrips)
            {
                DeleteEntity(handle);
                handlesToRemove.Add(handle);
            }

            foreach (int handle in handlesToRemove)
            {
                spawnedStrips.Remove(handle);
            }
        }

        [EventHandler("geneva-spikestrips:server:spawnStrips")]
        private async void OnSpawnStrips([FromSource] Player source, int numToDeploy, Vector3 fwdVec, List<object> groundHeights)
        {
            Vector3 plyPos = source.Character.Position;
            float heading = source.Character.Heading;

            for (int i = 0; i < numToDeploy; i++)
            {
                spawnCoords = new Vector3(plyPos.X, plyPos.Y, plyPos.Z) + fwdVec * (3.4f + (4.825f * i));
                TriggerClientEvent(source, "geneva-spikestrips:client:getGroundHeight", spawnCoords);
                await Delay(150);
                Entity entity = new Prop(CreateObject((int)spikeModel, spawnCoords.X, spawnCoords.Y, (float)groundHeights[i], true, true, false));
                entity.Heading = heading;
                entity.IsPositionFrozen = true;

                spawnedStrips.Add(entity.Handle);
            }
        }

        [EventHandler("geneva-spikestrips:server:deleteSpikestrips")]
        private void DeleteSpikestrips([FromSource] Player source)
        {
            int playerHandle = int.Parse(source.Handle);
            List<int> handlesToRemove = new List<int>();
            foreach (int handle in spawnedStrips)
            {
                DeleteEntity(handle);
                handlesToRemove.Add(handle);
            }

            foreach (int handle in handlesToRemove)
            {
                spawnedStrips.Remove(handle);
            }
        }

        [Command("deleteallspikes")]
        private void DeleteAllSpikesCmd([FromSource] Player source)
        {
            if (IsPlayerAceAllowed(source.Handle, "spikestrips.deleteAll"))
            {
                Debug.WriteLine($"{source.Name} executed the 'deleteallspikes' command!");
                DeleteAllStrips();
            }
            else
            {
                source.TriggerEvent("chat:addMessage", new
                {
                    color = new[] {255, 0, 0},
                    args = new[] {"[Spikestrips]", "You can't execute this!"}
                });
                return;
            }
        }
    }
}