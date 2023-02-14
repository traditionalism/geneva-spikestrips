using System.Collections.Generic;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.UI;
using static CitizenFX.Core.Native.API;

namespace spikestrips.Client
{
    public class ClientMain : BaseScript
    {
        private readonly uint _spikeModel = (uint)GetHashKey("p_ld_stinger_s");
        private bool _isDeployingStrips;
        private static readonly string ResourceName = GetCurrentResourceName();
        private static readonly int DeployTime = GetConfigValue("deploy_time", 1500);
        private static readonly int RetractTime = GetConfigValue("retract_time", 1500);
        private static readonly int MinSpikes = GetConfigValue("min_spikes", 2);
        private static readonly int MaxSpikes = GetConfigValue("max_spikes", 4);
        private readonly Vector3 _minVec;
        private readonly Vector3 _maxVec;
        private readonly Vector3 size;
        private readonly float _w;
        private readonly float _l;
        private readonly float _h;

        private static readonly Dictionary<string, int> Wheels = new Dictionary<string, int>()
        {
            { "wheel_lf", 0 },
            { "wheel_rf", 1 },
            { "wheel_lm", 2 },
            { "wheel_rm", 3 },
            { "wheel_lr", 4 },
            { "wheel_rr", 5 }
        };

        public ClientMain()
        {
            GetModelDimensions(_spikeModel, ref _minVec, ref _maxVec);

            size = _maxVec - _minVec;

            _w = size.X;
            _l = size.Y;
            _h = size.Z;
        }

        private static int GetConfigValue(string key, int defaultValue)
        {
            string value = GetResourceMetadata(ResourceName, key, GetNumResourceMetadata(ResourceName, key) - 1);
            if (int.TryParse(value, out int result))
            {
                return result;
            }

            Debug.WriteLine($"failed to parse config value '{value}' for metadata key '{key}'");
            return defaultValue;
        }

        private static bool CanUseSpikestrips
        {
            get
            {
                Ped playerPed = Game.PlayerPed;
                return playerPed.IsAlive && !playerPed.IsInVehicle() && !playerPed.IsGettingIntoAVehicle &&
                       !playerPed.IsClimbing && !playerPed.IsVaulting && playerPed.IsOnFoot && !playerPed.IsRagdoll &&
                       !playerPed.IsSwimming;
            }
        }

        [Tick]
        private async Task PickupTick()
        {
            Vector3 plyPos = Game.PlayerPed.Position;
            int closestStrip = GetClosestObjectOfType(plyPos.X, plyPos.Y, plyPos.Z, 15.0f, _spikeModel, false, false, false);

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
                    await Delay(RetractTime);
                    RemoveAnimDict("amb@medic@standing@kneel@idle_a");
                    TriggerServerEvent("geneva-spikestrips:server:deleteSpikestrips");
                    await Delay(150);
                }
            }
        }

        [Tick]
        private async Task CheckForSpikedTick()
        {
            Ped playerPed = Game.PlayerPed;
            Vehicle veh = playerPed.CurrentVehicle;
            if (veh == null || veh.Driver != playerPed)
            {
                await Delay(2500);
                return;
            }

            Vector3 pedCoords = playerPed.Position;
            int closestStrip = GetClosestObjectOfType(pedCoords.X, pedCoords.Y, pedCoords.Z, 35.0f, _spikeModel, false, false, false);
            if (closestStrip == 0)
            {
                await Delay(1000);
                return;
            }

            foreach (KeyValuePair<string, int> wheel in Wheels)
            {
                if (!IsVehicleTyreBurst(veh.Handle, wheel.Value, false))
                {
                    if (TouchingSpike(GetWorldPositionOfEntityBone(veh.Handle, GetEntityBoneIndexByName(veh.Handle, wheel.Key)), closestStrip))
                    {
                        SetVehicleTyreBurst(veh.Handle, wheel.Value, false, 1.0f);
                    }
                }
            }
        }

        private bool TouchingSpike(Vector3 coords, int strip)
        {
            Vector3 offset1 = GetOffsetFromEntityInWorldCoords(strip, 0.0f, _l / 2, _h * -1);
            Vector3 offset2 = GetOffsetFromEntityInWorldCoords(strip, 0.0f, _l / 2 * -1, _h);

            return IsPointInAngledArea(coords.X, coords.Y, coords.Z, offset1.X, offset1.Y, offset1.Z, offset2.X, offset2.Y, offset2.Z, _w * 2, false, false);
        }

        [Command("spikestrips")]
        private async void SpawnSpikestrips(string[] args)
        {
            if (_isDeployingStrips) return;
            Ped playerPed = Game.PlayerPed;

            if (!CanUseSpikestrips)
            {
                TriggerEvent("chat:addMessage", new
                {
                    color = new[] { 255, 0, 0 },
                    args = new[] { "[Spikestrips]", "You can't deploy spikestrips right now!" }
                });
                return;
            }

            if (args.Length != 1 || !int.TryParse(args[0], out int numToDeploy) || numToDeploy < MinSpikes || numToDeploy > MaxSpikes)
            {
                TriggerEvent("chat:addMessage", new
                {
                    color = new[] { 255, 0, 0 },
                    args = new[] { "[Spikestrips]", "Invalid spikestrip amount argument!" }
                });
                return;
            }

            _isDeployingStrips = true;
            Screen.ShowNotification("Deploying...");

            PlayKneelAnim(true);
            await Delay(DeployTime);
            RemoveAnimDict("amb@medic@standing@kneel@idle_a");

            List<float> groundHeights = new List<float>();
            for (int i = 0; i < numToDeploy; i++)
            {
                Vector3 spawnCoords = new Vector3(playerPed.Position.X, playerPed.Position.Y, playerPed.Position.Z) + playerPed.ForwardVector * (3.4f + (4.825f * i));
                float groundHeight = World.GetGroundHeight(spawnCoords);
                groundHeights.Add(groundHeight);
            }

            TriggerServerEvent("geneva-spikestrips:server:spawnStrips", numToDeploy, playerPed.ForwardVector, groundHeights);
            Screen.ShowNotification("Deployed!", true);
            _isDeployingStrips = false;
        }

        private async void PlayKneelAnim(bool deploy)
        {
            Ped playerPed = Game.PlayerPed;
            SetCurrentPedWeapon(playerPed.Handle, (uint)WeaponHash.Unarmed, true);
            RequestAnimDict("amb@medic@standing@kneel@idle_a");
            while (!HasAnimDictLoaded("amb@medic@standing@kneel@idle_a"))
            {
                await Delay(0);
            }

            TaskPlayAnim(playerPed.Handle, "amb@medic@standing@kneel@idle_a", "idle_a", 2.5f, 2.5f, deploy ? DeployTime : RetractTime, 0, 0.0f, false, false, false);
        }
    }
}