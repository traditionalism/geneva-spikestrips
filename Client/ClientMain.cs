using System;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.UI;
using static CitizenFX.Core.Native.API;

namespace spikestrips.Client
{
    public class ClientMain : BaseScript
    {
        private readonly uint spikeModel = (uint)GetHashKey("p_ld_stinger_s");
        private int numToDeploy = 2;
        private bool isDeployingStrips = false;
        private float groundHeight;
        private static string ourResourceName = GetCurrentResourceName();
        private int deployTime = (int)ParseConfigValue<int>("deploy_time", 4) * 1000;
        private int minSpikes = (int)ParseConfigValue<int>("min_spikes", 2);
        private int maxSpikes = (int)ParseConfigValue<int>("max_spikes", 4);

        private static object ParseConfigValue<T>(string key, T defaultValue)
        {
            string value = GetConfigValue(key);
            if (typeof(T) == typeof(int))
            {
                if (int.TryParse(value, out int result))
                {
                    return result;
                }
            }
            else if (typeof(T) == typeof(string))
            {
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }

            Debug.WriteLine($"~r~Error parsing config value '{value}' for metadata key '{key}'~s~");
            return defaultValue;
        }

        private static string GetConfigValue(string key)
        {
            try
            {
                return GetResourceMetadata(ourResourceName, key, GetNumResourceMetadata(ourResourceName, key) - 1);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"~r~Error getting config value for metadata key '{key}': {ex.Message}~s~");
                return "";
            }
        }

        private bool CanUseSpikestrips
        {
            get
            {
                Ped player = Game.PlayerPed;
                return player.IsAlive && !player.IsInVehicle() && !player.IsGettingIntoAVehicle && !player.IsClimbing && !player.IsVaulting && player.IsOnFoot && !player.IsRagdoll && !player.IsSwimming;
            }
        }

        [Tick]
        private async Task PickupTick()
        {
            Vector3 plyPos = Game.PlayerPed.Position;
            int closestStrip = GetClosestObjectOfType(plyPos.X, plyPos.Y, plyPos.Z, 15.0f, spikeModel, false, false, false);

            if (closestStrip == 0 || NetworkGetEntityOwner(closestStrip) != Game.Player.Handle)
            {
                await Delay(4000);
                return;
            }

            float dist = Vector3.DistanceSquared(plyPos, GetEntityCoords(closestStrip, false));

            if (dist >= 5.0f)
            {
                await Delay(1500);
                return;
            }

            if (dist <= 2.3f)
            {
                Screen.DisplayHelpTextThisFrame("Press ~INPUT_DETONATE~ to retract the spikestrips");

                if (IsControlJustPressed(0, 47))
                {
                    TriggerServerEvent("geneva-spikestrips:server:deleteSpikestrips");
                    await Delay(500);
                }
            }
        }

        [Command("spikestrips")]
        private async void SpawnSpikestrips(string[] args)
        {
            if (isDeployingStrips) return;
            Ped player = Game.PlayerPed;

            if (!CanUseSpikestrips)
            {
                TriggerEvent("chat:addMessage", new
                {
                    color = new[] {255, 0, 0},
                    args = new[] {"[Spikestrips]", "You can't deploy spikestrips right now!"}
                });
                return;
            }

            if (!int.TryParse(args[0], out int numToDeploy) || numToDeploy < minSpikes || numToDeploy > maxSpikes)
            {
                TriggerEvent("chat:addMessage", new
                {
                    color = new[] {255, 0, 0},
                    args = new[] {"[Spikestrips]", "Invalid spikestrip amount argument!"}
                });
                return;
            }

            isDeployingStrips = true;
            Screen.ShowNotification("Deploying...");

            SetCurrentPedWeapon(player.Handle, (uint)WeaponHash.Unarmed, true);
            RequestAnimDict("amb@medic@standing@kneel@idle_a");
            while (!HasAnimDictLoaded("amb@medic@standing@kneel@idle_a"))
            {
                await Delay(0);
            }
            TaskPlayAnim(player.Handle, "amb@medic@standing@kneel@idle_a", "idle_a", 2.5f, 2.5f, deployTime, 0, 0.0f, false, false, false);
            await Delay(deployTime);
            TriggerServerEvent("geneva-spikestrips:server:spawnStrips", numToDeploy, player.ForwardVector);
            Screen.ShowNotification("Deployed!", true);
            RemoveAnimDict("amb@medic@standing@kneel@idle_a");
            isDeployingStrips = false;
        }

        [EventHandler("geneva-spikestrips:client:getGroundHeight")]
        private void GetGroundHeight(Vector3 pos)
        {
            groundHeight = World.GetGroundHeight(pos);
            TriggerServerEvent("geneva-spikestrips:server:getGroundHeight", groundHeight);
        }
    }
}