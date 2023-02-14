using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

namespace spikestrips.Server
{
    public class ServerMain : BaseScript
    {
        private readonly uint _spikeModel = (uint)GetHashKey("p_ld_stinger_s");
        private static readonly string ResourceName = GetCurrentResourceName();
        private readonly List<int> _spawnedStrips = new();
        private readonly Dictionary<int, int> _stripOwners = new Dictionary<int, int>();
        private Vector3 _spawnCoords;
        private int _numDeleting;

        [EventHandler("onResourceStop")]
        private void OnResourceStop(string resourceName)
        {
            if (resourceName != ResourceName || _spawnedStrips.Count == 0) return;
            DeleteAllStrips();
        }

        [Tick]
        private async Task Cleanup()
        {
            await Delay(30000);

            int playerCount = Players.Count();
            if (playerCount == 0 && _spawnedStrips.Count > 0)
            {
                DeleteAllStrips();
            }
        }

        private void DeleteAllStrips()
        {
            _numDeleting = _spawnedStrips.Count;
            Debug.WriteLine($"Deleting {_numDeleting} spikestrip(s).");

            for (int i = 0; i < _spawnedStrips.Count; i++)
            {
                int handle = _spawnedStrips[i];
                DeleteEntity(handle);
                _spawnedStrips.RemoveAt(i--);
            }
        }

        [EventHandler("geneva-spikestrips:server:spawnStrips")]
        private void OnSpawnStrips([FromSource] Player source, int numToDeploy, Vector3 fwdVec, List<object> groundHeights)
        {
            Vector3 plyPos = source.Character.Position;
            float heading = source.Character.Heading;

            for (int i = 0; i < numToDeploy; i++)
            {
                _spawnCoords = new Vector3(plyPos.X, plyPos.Y, plyPos.Z) + fwdVec * (3.4f + (4.825f * i));
                Entity entity = new Prop(CreateObject((int)_spikeModel, _spawnCoords.X, _spawnCoords.Y, (float)groundHeights[i], true, true, false));
                entity.Heading = heading;
                entity.IsPositionFrozen = true;

                _spawnedStrips.Add(entity.Handle);
                _stripOwners.Add(entity.Handle, int.Parse(source.Handle));
            }
        }

        [EventHandler("geneva-spikestrips:server:deletePlayerSpikestrips")]
        private void DeletePlayerSpikestrips([FromSource] Player source)
        {
            List<int> handlesToRemove = new List<int>();
            foreach (int handle in _spawnedStrips)
            {
                if (_stripOwners.TryGetValue(handle, out int stripOwner) && stripOwner == int.Parse(source.Handle))
                {
                    DeleteEntity(handle);
                    handlesToRemove.Add(handle);
                }
            }

            foreach (int handle in handlesToRemove)
            {
                _spawnedStrips.Remove(handle);
                _stripOwners.Remove(handle);
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
            }
        }
    }
}