using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using static HarmonyLib.AccessTools;
using System.Text.RegularExpressions;
using ADOFAI;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace RecentLevelName
{
    public static class Main
    {
        public static UnityModManager.ModEntry.ModLogger Logger { get; private set; }

        private static Harmony _harmony;

        public static bool Initialize(UnityModManager.ModEntry mod)
        {
            _harmony = new Harmony(mod.Info.Id + "." + DateTime.Now.Ticks);

            Logger = mod.Logger;

            mod.OnToggle = OnToggle;

            return true;
        }

        private static bool OnToggle(UnityModManager.ModEntry mod, bool value)
        {
            if (value)
            {
                _harmony.PatchAll();
            }
            else
            {
                PlayerPrefs.SetString("lastOpenedLevelName", string.Empty);
                _harmony.UnpatchAll();
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(scnEditor), nameof(scnEditor.ShowFileActionsPanel))]
    public class scnEditorShowFileActionsPanelPatch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var newInstructions = new List<CodeInstruction>(instructions);
            var idx = newInstructions.FindIndex(instruction =>
                instruction.Calls(Method(typeof(Path), nameof(Path.GetFileNameWithoutExtension), new []{ typeof(string) }))) - 2;

            newInstructions.RemoveRange(idx, 5);
            newInstructions.InsertRange(idx, new []
            {
                new CodeInstruction(OpCodes.Ldloc_2),
                new CodeInstruction(OpCodes.Call, Method(typeof(scnEditorShowFileActionsPanelPatch), nameof(GetLastOpenButtonText))),
            });

            foreach (var instruction in newInstructions)
            {
                yield return instruction;
            }
        }

        public static string GetLastOpenButtonText(string levelPath)
        {
            var levelName = PlayerPrefs.GetString("lastOpenedLevelName", string.Empty);
            if (string.IsNullOrEmpty(levelName))
            {
                return "<color=#6495ED>" + Path.GetFileNameWithoutExtension(levelPath) + "</color>";
            }

            return $"<color=#6495ED>{levelName}</color> <color=#6e6e6e>({Path.GetFileNameWithoutExtension(levelPath)})</color>";
        }
    }

    [HarmonyPatch(typeof(Persistence), nameof(Persistence.UpdateLastOpenedLevel))]
    public class PersistenceUpdateLastOpenedLevelPatch
    {
        public static Regex RemoveTags { get; } = new Regex(@"<[^>]+>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static void Postfix()
        {
            scnEditor.instance.StartCoroutine(PostfixCo());
        }

        private static IEnumerator PostfixCo()
        {
            while (LevelDataLoadLevelPatch.LevelName == null)
            {
                yield return null;
            }

            var levelName = LevelDataLoadLevelPatch.LevelName;

            if (string.IsNullOrEmpty(levelName))
            {
                PlayerPrefs.SetString("lastOpenedLevelName", string.Empty);
                yield break;
            }

            levelName = RemoveTags.Replace(levelName, string.Empty).Trim();

            if (string.IsNullOrEmpty(levelName))
            {
                PlayerPrefs.SetString("lastOpenedLevelName", string.Empty);
                yield break;
            }

            PlayerPrefs.SetString("lastOpenedLevelName", levelName);
        }
    }

    [HarmonyPatch(typeof(LevelData), nameof(LevelData.LoadLevel))]
    public class LevelDataLoadLevelPatch
    {
        public static string LevelName
        {
            get
            {
                var levelName = _levelName;
                _levelName = null;
                return levelName;
            }
        }

        private static string _levelName;

        public static void Postfix(LevelData __instance)
        {
            _levelName = __instance.song;
        }
    }
}