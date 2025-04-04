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

        var dict = StatsManager.instance.dictionaryOfDictionaries["playerUpgradesUsed"];
        int value = dict.ContainsKey(_steamID) ? ++dict[_steamID] : (dict[_steamID] = 1);

        if (GameManager.Multiplayer())
        {
            //Broadcast that we used an upgrade
            PhotonView _photonView = PunManager.instance.GetComponent<PhotonView>();
            _photonView.RPC("UpdateStatRPC", RpcTarget.Others, "playerUpgradesUsed", _steamID, value);
        }

    }
}

public static class DictionaryExtensions
{
    public static int GetOrDefault(this Dictionary<string, int> dict, string key)
    {
        if (dict == null) return 0;
        if (!dict.ContainsKey(key)) return 0;
        return dict[key];
    }
}

[HarmonyPatch(typeof(StatsManager))]
[HarmonyPatch("Start")]
public static class StatsManagerPatch
{
    static void Prefix(StatsManager __instance)
    {
        __instance.dictionaryOfDictionaries.Add("playerUpgradesUsed", []);

        // Initialize all player upgrade stat dictionaries to prevent null refs
        __instance.playerUpgradeLaunch = new Dictionary<string, int>();
        __instance.playerUpgradeStamina = new Dictionary<string, int>();
        __instance.playerUpgradeExtraJump = new Dictionary<string, int>();
        __instance.playerUpgradeRange = new Dictionary<string, int>();
        __instance.playerUpgradeStrength = new Dictionary<string, int>();
        __instance.playerUpgradeHealth = new Dictionary<string, int>();
        __instance.playerUpgradeSpeed = new Dictionary<string, int>();
        __instance.playerUpgradeMapPlayerCount = new Dictionary<string, int>();
    }
}

[HarmonyPatch(typeof(PlayerAvatar))]
[HarmonyPatch("SpawnRPC")]
public static class PlayerSpawnPatch
{
    static void Prefix(PhotonView ___photonView)
    {
        if (RunManager.instance.levelCurrent != SemiFunc.RunIsLevel())
        {
            //Plugin.Logger.LogInfo("[UpgradeEveryRound] Skipping upgrade logic — not in Shop.");
            return;
        }

        if (!PhotonNetwork.IsMasterClient || !___photonView.IsMine)
        {
            //Plugin.Logger.LogInfo("[UpgradeEveryRound] Skipped upgrade logic — not master client.");
            return;
        }

        Debug.Log("Call Coroutine");
        CoroutineRunner.Instance.StartCoroutine(WaitForPlayersAndApplyUpgrades(___photonView));

    }
    private static System.Collections.IEnumerator WaitForPlayersAndApplyUpgrades(PhotonView ___photonView)
    {
        List<PlayerAvatar> players = SemiFunc.PlayerGetAll();
        if (players == null || players.Count == 0)
        {
            Plugin.Logger.LogWarning("[UpgradeEveryRound] No player avatars found!");
            yield break;
        }

        Level[] bannedLevels = [RunManager.instance.levelMainMenu, RunManager.instance.levelLobbyMenu, RunManager.instance.levelTutorial];
        if (bannedLevels.Contains(RunManager.instance.levelCurrent)) yield break;

        foreach (var avatar in players)
        {
            string steamID = SemiFunc.PlayerGetSteamID(avatar);

            if (string.IsNullOrEmpty(steamID)) continue;

            for (int i = 0; i < Plugin.upgradesPerRound.Value; i++)
            {
                ApplyRandomUpgrade(steamID);
            }
        }
    }

    public static void ApplyRandomUpgrade(string steamID)
    {

        switch (Random.Range(0, 8))
        {
            case 0: PunManager.instance.UpgradePlayerEnergy(steamID); break;
            case 1: PunManager.instance.UpgradePlayerExtraJump(steamID); break;
            case 2: PunManager.instance.UpgradePlayerGrabRange(steamID); break;
            case 3: PunManager.instance.UpgradePlayerGrabStrength(steamID); break;
            case 4: PunManager.instance.UpgradePlayerSprintSpeed(steamID); break;
            case 5: PunManager.instance.UpgradePlayerHealth(steamID); break;
            case 6:
                try
                {
                    PunManager.instance.UpgradePlayerTumbleLaunch(steamID);
                }
                catch (System.NullReferenceException ex)
                {
                    Plugin.Logger.LogWarning($"Caught NullRef in TumbleLaunch for {steamID}: {ex.Message}");
                    ApplyRandomUpgrade(steamID);
                }
                break;
            case 7:
                if (StatsManager.instance.playerUpgradeMapPlayerCount.ContainsKey(steamID) && StatsManager.instance.playerUpgradeMapPlayerCount[steamID] > 0)
                {
                    Plugin.Logger.LogInfo($"[UpgradeEveryRound] Rerolling upgrade for {steamID} — already has MapPlayerCount");
                    ApplyRandomUpgrade(steamID);
                    break;
                }
                else
                {
                    PunManager.instance.UpgradeMapPlayerCount(steamID);
                }
                break;
        }
        Plugin.ApplyUpgrade(steamID);
    }
}

public static class PatchHelper
{
    public static void SendStatRPC(Dictionary<string, int> dict, string steamID, string rpcName, PhotonView photonView)
    {
        if (dict == null)
        {
            Debug.LogWarning("Stat dictionary is null");
            return;
        }
        if (photonView == null)
        {
            Debug.LogWarning("PhotonView is null");
            return;
        }
        if (string.IsNullOrEmpty(steamID))
        {
            Debug.LogWarning("SteamID is null or empty");
            return;
        }
        if (!dict.ContainsKey(steamID))
        {
            Debug.LogWarning($"Stat dictionary does not contain key: {steamID}");
            return;
        }

        Debug.Log($"Sending RPC '{rpcName}' for {steamID} with value {dict[steamID]}");
        photonView.RPC(rpcName, RpcTarget.Others, steamID, dict[steamID]);
    }
}

[HarmonyPatch(typeof(PunManager))]
[HarmonyPatch(nameof(PunManager.UpgradeMapPlayerCount))]
public static class UpgradeMapPlayerCountPatch
{
    static void Postfix(string _steamID, PhotonView ___photonView, StatsManager ___statsManager)
        => PatchHelper.SendStatRPC(___statsManager.playerUpgradeMapPlayerCount, _steamID, "UpgradeMapPlayerCountRPC", ___photonView);
}

[HarmonyPatch(typeof(PunManager))]
[HarmonyPatch(nameof(PunManager.UpgradePlayerEnergy))]
public static class UpgradePlayerEnergyPatch
{
    static void Postfix(string _steamID, PhotonView ___photonView, StatsManager ___statsManager)
        => PatchHelper.SendStatRPC(___statsManager.playerUpgradeStamina, _steamID, "UpgradePlayerEnergyCountRPC", ___photonView);
}

[HarmonyPatch(typeof(PunManager))]
[HarmonyPatch(nameof(PunManager.UpgradePlayerExtraJump))]
public static class UpgradePlayerExtraJumpPatch
{
    static void Postfix(string _steamID, PhotonView ___photonView, StatsManager ___statsManager)
        => PatchHelper.SendStatRPC(___statsManager.playerUpgradeExtraJump, _steamID, "UpgradePlayerExtraJumpRPC", ___photonView);
}

[HarmonyPatch(typeof(PunManager))]
[HarmonyPatch(nameof(PunManager.UpgradePlayerGrabRange))]
public static class UpgradePlayerGrabRangePatch
{
    static void Postfix(string _steamID, PhotonView ___photonView, StatsManager ___statsManager)
        => PatchHelper.SendStatRPC(___statsManager.playerUpgradeRange, _steamID, "UpgradePlayerGrabRangeRPC", ___photonView);
}

[HarmonyPatch(typeof(PunManager))]
[HarmonyPatch(nameof(PunManager.UpgradePlayerGrabStrength))]
public static class UpgradePlayerGrabStrengthPatch
{
    static void Postfix(string _steamID, PhotonView ___photonView, StatsManager ___statsManager)
        => PatchHelper.SendStatRPC(___statsManager.playerUpgradeStrength, _steamID, "UpgradePlayerGrabStrengthRPC", ___photonView);
}

[HarmonyPatch(typeof(PunManager))]
[HarmonyPatch(nameof(PunManager.UpgradePlayerHealth))]
public static class UpgradePlayerHealthPatch
{
    static void Postfix(string playerName, PhotonView ___photonView, StatsManager ___statsManager)
        => PatchHelper.SendStatRPC(___statsManager.playerUpgradeHealth, playerName, "UpgradePlayerHealthRPC", ___photonView);
}

[HarmonyPatch(typeof(PunManager))]
[HarmonyPatch(nameof(PunManager.UpgradePlayerSprintSpeed))]
public static class UpgradePlayerSprintSpeedPatch
{
    static void Postfix(string _steamID, PhotonView ___photonView, StatsManager ___statsManager)
        => PatchHelper.SendStatRPC(___statsManager.playerUpgradeSpeed, _steamID, "UpgradePlayerSprintSpeedRPC", ___photonView);
}

[HarmonyPatch(typeof(PunManager))]
[HarmonyPatch(nameof(PunManager.UpgradePlayerTumbleLaunch))]
public static class UpgradePlayerTumbleLaunchPatch
{
    static void Postfix(string _steamID, PhotonView ___photonView, StatsManager ___statsManager)
        => PatchHelper.SendStatRPC(___statsManager.playerUpgradeLaunch, _steamID, "UpgradePlayerTumbleLaunchRPC", ___photonView);
}

public class CoroutineRunner : MonoBehaviour
{
    private static CoroutineRunner _instance;

    public static CoroutineRunner Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("CoroutineRunner");
                _instance = go.AddComponent<CoroutineRunner>();
                UnityEngine.Object.DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1) && PhotonNetwork.IsMasterClient)
        {
            Debug.Log("[UpgradeEveryRound] F1 key detected — manual upgrade triggered.");

            List<PlayerAvatar> players = SemiFunc.PlayerGetAll();
            if (players == null || players.Count == 0)
            {
                Plugin.Logger.LogWarning("[UpgradeEveryRound] No player avatars found!");
                return;
            }

            Level[] bannedLevels = [RunManager.instance.levelMainMenu, RunManager.instance.levelLobbyMenu, RunManager.instance.levelTutorial];
            if (bannedLevels.Contains(RunManager.instance.levelCurrent)) return;

            foreach (var avatar in players)
            {
                string steamID = SemiFunc.PlayerGetSteamID(avatar);
                if (string.IsNullOrEmpty(steamID)) continue;

                for (int i = 0; i < Plugin.upgradesPerRound.Value; i++)
                {
                    PlayerSpawnPatch.ApplyRandomUpgrade(steamID);
                }
            }
        }
    }
}
