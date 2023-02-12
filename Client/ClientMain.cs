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
        private uint spikeModel = (uint)GetHashKey("p_ld_stinger_s");
        private bool isDeployingStrips = false;
        private static string ourResourceName = GetCurrentResourceName();
        private int deployTime = (int)ParseConfigValue<int>("deploy_time", 1500);
        private int retractTime = (int)ParseConfigValue<int>("retract_time", 1500);
        private int minSpikes = (int)ParseConfigValue<int>("min_spikes", 2);
        private int maxSpikes = (int)ParseConfigValue<int>("max_spikes", 4);
        private static readonly Dictionary<string, int> Wheels = new Dictionary<string, int>()
        {
            {"wheel_lf", 0},
            {"wheel_rf", 1},
            {"wheel_lm", 2},
            {"wheel_rm", 3},
            {"wheel_lr", 4},
            {"wheel_rr", 5}
        };

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
                await Delay(3000);
                return;
            }

            float dist = Vector3.DistanceSquared(plyPos, GetEntityCoords(closestStrip, false));

            if (dist >= 6.0f)
            {
                await Delay(1500);
                return;
            }

            if (CanUseSpikestrips && dist <= 4.3f)
            {
                Screen.DisplayHelpTextThisFrame("Press ~INPUT_CHARACTER_WHEEL~ + ~INPUT_CONTEXT~ to retract the spikestrips");

                if (IsControlPressed(0, 19) && IsControlPressed(0, 51))
                {
                    Vector3 spikePos = GetEntityCoords(closestStrip, false);
                    float heading = GetHeadingFromVector_2d(spikePos.X - plyPos.X, spikePos.X - plyPos.Y);
                    SetEntityHeading(Game.PlayerPed.Handle, heading);

                    PlayKneelAnim(false);
                    await Delay(retractTime);
                    TriggerServerEvent("geneva-spikestrips:server:deleteSpikestrips");
                    await Delay(150);
                    return;
                }
            }
        }

        [Tick]
        private async Task CheckForSpikedTick()
        {
            Ped player = Game.PlayerPed;
            int veh = GetVehiclePedIsUsing(player.Handle);
            if (veh == 0 || GetPedInVehicleSeat(veh, -1) != player.Handle)
            {
                await Delay(2500);
                return;
            }

            Vector3 pedCoords = player.Position;
            int closestStrip = GetClosestObjectOfType(pedCoords.X, pedCoords.Y, pedCoords.Z, 30.0f, spikeModel, false, false, false);
            if (closestStrip == 0)
            {
                await Delay(1500);
                return;
            }

            if (!IsEntityTouchingEntity(veh, closestStrip)) return;

            foreach (KeyValuePair<string, int> wheel in Wheels)
            {
                if (!IsVehicleTyreBurst(veh, wheel.Value, false))
                {
                    if (TouchingSpike(GetWorldPositionOfEntityBone(veh, GetEntityBoneIndexByName(veh, wheel.Key)), closestStrip))
                    {
                        SetVehicleTyreBurst(veh, wheel.Value, true, 1000.0f);
                    }
                }
            }
        }

        private bool TouchingSpike(Vector3 coords, int strip)
        {
            Vector3 minVec = new Vector3();
            Vector3 maxVec = new Vector3();
            GetModelDimensions((uint)GetEntityModel(strip), ref minVec, ref maxVec);

            Vector3 minResult = minVec;
            Vector3 maxResult = maxVec;
            Vector3 size = maxResult - minResult;

            float w = size.X;
            float l = size.Y;
            float h = size.Z;

            Vector3 offset1 = GetOffsetFromEntityInWorldCoords(strip, 0.0f, l / 2, h * -1);
            Vector3 offset2 = GetOffsetFromEntityInWorldCoords(strip, 0.0f, l / 2 * -1, h);

            return IsPointInAngledArea(coords.X, coords.Y, coords.Z, offset1.X, offset1.Y, offset1.Z, offset2.X, offset2.Y, offset2.Z, w * 4, false, false);
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

            int numToDeploy = int.TryParse(args[0], out int n) && n >= minSpikes && n <= maxSpikes ? n : -1;
            if (numToDeploy == -1)
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