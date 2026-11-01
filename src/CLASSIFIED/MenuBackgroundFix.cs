using System;
using System.Reflection;
using EFT;
using EFT.UI;
using HarmonyLib;

namespace CLASSIFIED
{
    /// <summary>
    /// Keeps the load from failing on a Random main-menu background. The last thing the game
    /// loads before the menu is its background, and the game settles "Random" inside
    /// EnvironmentUI.SetEnvironmentAsync, after every patch on it has already seen "Random".
    /// Background mods that load their files when their own background is asked for (WTT
    /// Content Backport's, amos's Server Room and Halloween, Unlock All Backgrounds' Server Room)
    /// then never load them, the scene fails, and the load stops with "Object reference not set to
    /// an instance of an object". This picks the background first, the way the game would, so
    /// every later patch sees the one actually chosen. "Random" is itself one of the unlocked
    /// backgrounds the game picks from, so it draws again until it gets a real one.
    /// </summary>
    internal static class MenuBackgroundFix
    {
        internal static void Apply(Harmony harmony)
        {
            MethodInfo target = AccessTools.Method(typeof(EnvironmentUI), nameof(EnvironmentUI.SetEnvironmentAsync));
            if (target == null)
            {
                ClassifiedPlugin.Log.LogWarning("EnvironmentUI.SetEnvironmentAsync not found; Random menu backgrounds are left to the game.");
                return;
            }
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(MenuBackgroundFix), nameof(SettleRandom)) { priority = Priority.First });
        }

        private static void SettleRandom(EnvironmentUI __instance, ref EEnvironmentUIType environmentUiType)
        {
            if (environmentUiType != EEnvironmentUIType.RandomEnvironmentUiType)
            {
                return;
            }
            try
            {
                EEnvironmentUIType picked = EEnvironmentUIType.RandomEnvironmentUiType;
                for (int i = 0; i < 20 && picked == EEnvironmentUIType.RandomEnvironmentUiType; i++)
                {
                    picked = __instance.GetRandomEnvironment();
                }
                if (picked == EEnvironmentUIType.RandomEnvironmentUiType)
                {
                    return;
                }
                environmentUiType = picked;
                ClassifiedPlugin.Log.LogInfo("Random menu background settled early: " + picked);
            }
            catch (Exception e)
            {
                // Nothing to pick from: leave it to the game.
                ClassifiedPlugin.Log.LogWarning("Random menu background not settled early: " + e.Message);
            }
        }
    }
}
