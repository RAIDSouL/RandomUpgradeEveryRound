using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;
using Steamworks;
using BepInEx.Configuration;
using System.Linq;

namespace UpgradeEveryRound;

[BepInPlugin(modGUID, modName, modVersion), BepInDependency("nickklmao.menulib", "2.1.3")]
public class Plugin : BaseUnityPlugin
{
    public const string modGUID = "dev.redfops.repo.upgradeeveryround";
    public const string modName = "Upgrade Every Round";
    public const string modVersion = "1.1.0";

    public static ConfigEntry<int> upgradesPerRound;

    internal static new ManualLogSource Logger;
    private readonly Harmony harmony = new Harmony(modGUID);

    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;

        upgradesPerRound = Config.Bind("Upgrades", "Upgrades Per Round", 1, new ConfigDescription("Number of upgrades per round", new AcceptableValueRange<int>(0, 10)));

        harmony.PatchAll(typeof(PlayerSpawnPatch));
        harmony.PatchAll(typeof(StatsManagerPatch));
        harmony.PatchAll(typeof(UpgradeMapPlayerCountPatch));
        harmony.PatchAll(typeof(UpgradePlayerEnergyPatch));
        harmony.PatchAll(typeof(UpgradePlayerExtraJumpPatch));
        harmony.PatchAll(typeof(UpgradePlayerGrabRangePatch));
        harmony.PatchAll(typeof(UpgradePlayerGrabStrengthPatch));
        harmony.PatchAll(typeof(UpgradePlayerHealthPatch));
        harmony.PatchAll(typeof(UpgradePlayerSprintSpeedPatch));
        harmony.PatchAll(typeof(UpgradePlayerTumbleLaunchPatch));


        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }

    public static void ApplyUpgrade(string _steamID)
    {
        //Update UI to reflect upgrade
        StatsUI.instance.Fetch();
        StatsUI.instance.ShowStats();
        CameraGlitch.Instance.PlayUpgrade();

        int value = ++StatsManager.instance.dictionaryOfDictionaries["playerUpgradesUsed"][_steamID];
        if (GameManager.Multiplayer())
        {
            //Broadcast that we used an upgrade
            PhotonView _photonView = PunManager.instance.GetComponent<PhotonView>();
            _photonView.RPC("UpdateStatRPC", RpcTarget.Others, "playerUpgradesUsed", _steamID, value);
        }

    }
}

[HarmonyPatch(typeof(PlayerAvatar))]
[HarmonyPatch("SpawnRPC")]
public static class PlayerSpawnPatch
{
    static void Prefix(PhotonView ___photonView)
    {
        Level[] bannedLevels = [RunManager.instance.levelMainMenu, RunManager.instance.levelLobbyMenu, RunManager.instance.levelTutorial];
        if (bannedLevels.Contains(RunManager.instance.levelCurrent)) return;

        List<PlayerAvatar> _allPlayer = SemiFunc.PlayerGetList();
        List<string> _PlayerSteamID = new List<string>();
        for (int i = 0; i < _allPlayer.Count; i++)
        {
            string _playerSteamID = SemiFunc.PlayerGetSteamID(SemiFunc.PlayerAvatarGetFromPhotonID(_allPlayer[i].GetComponent<PhotonView>().ViewID));
            Debug.Log(_playerSteamID);
            _PlayerSteamID.Add(_playerSteamID);
        }


        if (GameManager.Multiplayer() && !___photonView.IsMine) return;

        MenuManager.instance.PageCloseAll(); //Just in case somehow other menus were opened previously.
        while (!PunManager.instance)
        {
            Debug.Log("Waiting for PUN");
        }
        for (int i = 0; i < _PlayerSteamID.Count; i++)
        {
            switch (Random.Range(0, 7))
            {
                case 0:
                    Debug.Log("0");
                    PunManager.instance.UpgradePlayerEnergy(_PlayerSteamID[i]);
                    break;
                case 1:
                    Debug.Log("1");
                    PunManager.instance.UpgradePlayerExtraJump(_PlayerSteamID[i]);
                    break;
                case 2:
                    Debug.Log("2");
                    PunManager.instance.UpgradePlayerGrabRange(_PlayerSteamID[i]);
                    break;
                case 3:
                    Debug.Log("3");
                    PunManager.instance.UpgradePlayerGrabStrength(_PlayerSteamID[i]);
                    break;
                case 4:
                    Debug.Log("4");
                    PunManager.instance.UpgradePlayerHealth(_PlayerSteamID[i]);
                    break;
                case 5:
                    Debug.Log("5");
                    PunManager.instance.UpgradePlayerSprintSpeed(_PlayerSteamID[i]);
                    break;
                case 6:
                    Debug.Log("6");
                    PunManager.instance.UpgradePlayerTumbleLaunch(_PlayerSteamID[i]);
                    break;
                case 7:
                    Debug.Log("7");
                    PunManager.instance.UpgradeMapPlayerCount(_PlayerSteamID[i]);
                    break;
            }
            int value = ++StatsManager.instance.dictionaryOfDictionaries["playerUpgradesUsed"][_PlayerSteamID[i]];
            if (GameManager.Multiplayer())
            {
                PhotonView _photonView = PunManager.instance.GetComponent<PhotonView>();
                _photonView.RPC("UpdateStatRPC", RpcTarget.Others, "playerUpgradesUsed", _PlayerSteamID[i], value);
            }
        }
    }
}

//Our custom save data handling
[HarmonyPatch(typeof(StatsManager))]
[HarmonyPatch("Start")]
public static class StatsManagerPatch
{
    static void Prefix(StatsManager __instance)
    {
        __instance.dictionaryOfDictionaries.Add("playerUpgradesUsed", []);
    }
}

//Yippee networking and boilerplate!

//Yippee networking and boilerplate!

[HarmonyPatch(typeof(PunManager))]
[HarmonyPatch(nameof(PunManager.UpgradeMapPlayerCount))]
public static class UpgradeMapPlayerCountPatch
{
    static void Postfix(string _steamID, PhotonView ___photonView, StatsManager ___statsManager)
    {
        if (!SemiFunc.IsMasterClient() && GameManager.Multiplayer())
        {
            ___photonView.RPC("UpgradeMapPlayerCountRPC", RpcTarget.Others, _steamID, ___statsManager.playerUpgradeMapPlayerCount[_steamID]);
        }
    }
}

[HarmonyPatch(typeof(PunManager))]
[HarmonyPatch(nameof(PunManager.UpgradePlayerEnergy))]
public static class UpgradePlayerEnergyPatch
{
    static void Postfix(string _steamID, PhotonView ___photonView, StatsManager ___statsManager)
    {
        if (!SemiFunc.IsMasterClient() && GameManager.Multiplayer())
        {
            ___photonView.RPC("UpgradePlayerEnergyCountRPC", RpcTarget.Others, _steamID, ___statsManager.playerUpgradeStamina[_steamID]);
        }
    }
}

[HarmonyPatch(typeof(PunManager))]
[HarmonyPatch(nameof(PunManager.UpgradePlayerExtraJump))]
public static class UpgradePlayerExtraJumpPatch
{
    static void Postfix(string _steamID, PhotonView ___photonView, StatsManager ___statsManager)
    {
        if (!SemiFunc.IsMasterClient() && GameManager.Multiplayer())
        {
            ___photonView.RPC("UpgradePlayerExtraJumpRPC", RpcTarget.Others, _steamID, ___statsManager.playerUpgradeExtraJump[_steamID]);
        }
    }
}

[HarmonyPatch(typeof(PunManager))]
[HarmonyPatch(nameof(PunManager.UpgradePlayerGrabRange))]
public static class UpgradePlayerGrabRangePatch
{
    static void Postfix(string _steamID, PhotonView ___photonView, StatsManager ___statsManager)
    {
        if (!SemiFunc.IsMasterClient() && GameManager.Multiplayer())
        {
            ___photonView.RPC("UpgradePlayerGrabRangeRPC", RpcTarget.Others, _steamID, ___statsManager.playerUpgradeRange[_steamID]);
        }
    }
}

[HarmonyPatch(typeof(PunManager))]
[HarmonyPatch(nameof(PunManager.UpgradePlayerGrabStrength))]
public static class UpgradePlayerGrabStrengthPatch
{
    static void Postfix(string _steamID, PhotonView ___photonView, StatsManager ___statsManager)
    {
        if (!SemiFunc.IsMasterClient() && GameManager.Multiplayer())
        {
            ___photonView.RPC("UpgradePlayerGrabStrengthRPC", RpcTarget.Others, _steamID, ___statsManager.playerUpgradeStrength[_steamID]);
        }
    }
}

[HarmonyPatch(typeof(PunManager))]
[HarmonyPatch(nameof(PunManager.UpgradePlayerHealth))]
public static class UpgradePlayerHealthPatch
{
    static void Postfix(string playerName, PhotonView ___photonView, StatsManager ___statsManager)
    {
        if (!SemiFunc.IsMasterClient() && GameManager.Multiplayer())
        {
            ___photonView.RPC("UpgradePlayerHealthRPC", RpcTarget.Others, playerName, ___statsManager.playerUpgradeHealth[playerName]);
        }
    }
}

[HarmonyPatch(typeof(PunManager))]
[HarmonyPatch(nameof(PunManager.UpgradePlayerSprintSpeed))]
public static class UpgradePlayerSprintSpeedPatch
{
    static void Postfix(string _steamID, PhotonView ___photonView, StatsManager ___statsManager)
    {
        if (!SemiFunc.IsMasterClient() && GameManager.Multiplayer())
        {
            ___photonView.RPC("UpgradePlayerSprintSpeedRPC", RpcTarget.Others, _steamID, ___statsManager.playerUpgradeSpeed[_steamID]);
        }
    }
}

[HarmonyPatch(typeof(PunManager))]
[HarmonyPatch(nameof(PunManager.UpgradePlayerTumbleLaunch))]
public static class UpgradePlayerTumbleLaunchPatch
{
    static void Postfix(string _steamID, PhotonView ___photonView, StatsManager ___statsManager)
    {
        if (!SemiFunc.IsMasterClient() && GameManager.Multiplayer())
        {
            ___photonView.RPC("UpgradePlayerTumbleLaunchRPC", RpcTarget.Others, _steamID, ___statsManager.playerUpgradeLaunch[_steamID]);
        }
    }
}
