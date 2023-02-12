using System;
using System.Collections.Generic;
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
        private static string ourResourceName = GetCurrentResourceName();
        private int deployTime = (int)ParseConfigValue<int>("deploy_time", 2500);
        private int retractTime = (int)ParseConfigValue<int>("retract_time", 2000);
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
                await Delay(3500);
                return;
            }

            float dist = Vector3.DistanceSquared(plyPos, GetEntityCoords(closestStrip, false));

            if (dist >= 6.0f)
            {
                await Delay(1500);
                return;
            }

            if (CanUseSpikestrips && dist <= 2.8f)
            {
                Screen.DisplayHelpTextThisFrame("Press ~INPUT_DETONATE~ to retract the spikestrips");

                if (IsControlJustPressed(0, 47))
                {
                    Vector3 spikePos = GetEntityCoords(closestStrip, false);
                    float heading = GetHeadingFromVector_2d(spikePos.X - plyPos.X, spikePos.X - plyPos.Y);
                    SetEntityHeading(Game.PlayerPed.Handle, heading);

                    PlayKneelAnim(false);
                    await Delay(retractTime);
                    TriggerServerEvent("geneva-spikestrips:server:deleteSpikestrips");
                    await Delay(150);
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

            PlayKneelAnim(true);
            await Delay(deployTime);
            RemoveAnimDict("amb@medic@standing@kneel@idle_a");
            List<float> groundHeights = new List<float>();
            for (int i = 0; i < numToDeploy; i++)
            {
                Vector3 spawnCoords = new Vector3(player.Position.X, player.Position.Y, player.Position.Z) + player.ForwardVector * (3.4f + (4.825f * i));
                float groundHeight = World.GetGroundHeight(spawnCoords);
                groundHeights.Add(groundHeight);
            }
            TriggerServerEvent("geneva-spikestrips:server:spawnStrips", numToDeploy, player.ForwardVector, groundHeights);
            Screen.ShowNotification("Deployed!", true);
            isDeployingStrips = false;
        }

        private async void PlayKneelAnim(bool deploy)
        {
            Ped player = Game.PlayerPed;
            SetCurrentPedWeapon(player.Handle, (uint)WeaponHash.Unarmed, true);
            RequestAnimDict("amb@medic@standing@kneel@idle_a");
            while (!HasAnimDictLoaded("amb@medic@standing@kneel@idle_a"))
            {
                await Delay(0);
            }
            TaskPlayAnim(player.Handle, "amb@medic@standing@kneel@idle_a", "idle_a", 2.5f, 2.5f, deploy ? deployTime : retractTime, 0, 0.0f, false, false, false);
        }
    }
}