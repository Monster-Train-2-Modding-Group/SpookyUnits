using HarmonyLib;
using ShinyShoe;
using Spine;
using Spine.Unity;
using System.Reflection;
using UnityEngine;

namespace SpookyUnits.Plugin.patches
{
    [HarmonyPatch(typeof(CharacterUIMeshSpine), nameof(CharacterUIMeshSpine.Setup))]
    public static class Mod_CharacterUI_Setup
    {
        private readonly static List<string> CharactersThatDontDisplayWell =
        [
            "Character_HornbreakerPrince_ClassSelect",
            "Character_ShardtailQueen_ClassSelect",
            "Character_TheSentient_ClassSelect",
            "Character_RootsOfWyldenten_ClassSelect",
            "Character_AcksoLotal_ClassSelect",
            "Character_SolgardTheMartyr_ClassSelect",
            "Character_Penumbra_ClassSelect",
            "Character_ShadowPrimordium_ClassSelect",
            "Character_RfrFlicker_ClassSelect",
            "Character_LilFade_ClassSelect",
            "Character_SpineChief_ClassSelect",
            "Character_Echowright_ClassSelect"
        ];

        private static readonly MethodInfo CreateBoneFollower_Function = AccessTools.Method(typeof(CharacterUIMeshSpine), "CreateBoneFollower");
     
        private static void Postfix(CharacterUIMeshSpine __instance, string debugName, float facingDir)
        {
            // Cult of the Lamb upgrade
            if (__instance.name == "RedCrown") return;

            // Remove [#] from name.
            int index = debugName.IndexOf(" [");
            index = index == -1 ? debugName.Length : index;
            string name = debugName[..index];

            var screenManager = AllGameManagers.Instance!.GetScreenManager();
            int seed;
            bool noLog = false;
            // Apparently this can leak the seed before the run starts. Use the out of run seed.
            if (!(screenManager.GetScreenActive(ScreenName.RunSetup) || screenManager.GetScreenActive(ScreenName.SoulSaviorRunSetup)))
            {
                seed = AllGameManagers.Instance!.GetSaveManager().GetStartingConditions().Seed; /* 0 if out of run */

            }
            else
            {
                /*facingDir *= -1;
                seed = Plugin.Seed;
                noLog = true;*/
                return;
            }

            if (screenManager.GetScreenActive(ScreenName.Compendium))
            {
                // Out of run seeds have the default set to the startup game seed.
                seed = (seed == 0) ? Plugin.Seed : seed;
                // Bugfix for setting enemies as monsters in the enemy compendium.
                facingDir = -1;
                noLog = true;
            }
            else if (screenManager.GetScreenActive(ScreenName.BattleIntro))
            {
                facingDir = -1;
            }


            var hash = name.GetMurmurHash();

            var gameObject = Plugin.GetOverlayImage((uint)seed, (uint)hash);

            if (gameObject == null) return;

            var overlayImage = gameObject.GetComponentInChildren<SpriteRenderer>();
            overlayImage.sortingLayerID = __instance.GetSortingLayer().LayerID();

            if (!noLog)
            {
                var characterState = __instance.transform.parent.parent.parent.GetComponent<CharacterState>();
                Plugin.LogPumpkin(overlayImage.name, characterState);
            }

            AttachToBone(__instance, name, gameObject.transform, VfxAtLoc.Location.BoneStatusEffectSlot1);

            var child = gameObject.transform.GetChild(0);
            var scale = child.localScale;
            child.localScale = new Vector3(facingDir * scale.x, scale.y, scale.z);
        }

        public static void AttachToBone(CharacterUIMeshSpine mesh, string name, Transform transform, VfxAtLoc.Location location)
        {
            if (!CharactersThatDontDisplayWell.Contains(name))
            {
                mesh.AttachToBone(transform, location);
            }
            else
            {
                // Somehow the pumpkins don't display correctly on the ClassSelect for MT1 champions
                // they have the same skeleton with same bones/slots...

                /*var boneFollower = CreateBoneFollower_Function.Invoke(mesh, []) as BoneFollower;
                List<Bone> list;
                using (GenericPools.GetList(out list))
                {
                    list.Clear();
                    string text = "Head";
                    if (text != null && mesh.SkeletonAnimation != null)
                    {
                        mesh.SkeletonAnimation.skeleton.FindBonesWithPrefix(list, text);
                    }
                    foreach (Bone item in list)
                    {
                        boneFollower!.SetBone(item.Data.Name);
                        transform.SetParent(boneFollower.transform, worldPositionStays: false);
                        transform.localPosition = new Vector3(0f, 0f, -0.2f);
                    }
                }*/
                return;
            }
        }
    }

    // Handles save metadata. In run save data handled in Awake.
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.Save))]
    public class SaveManager_Save_Patch
    {
        public static void Prefix(SaveManager __instance, bool writeToFile, bool saveReplayOnly)
        {
            if (__instance.PreviewMode || saveReplayOnly)
                return;

            if (__instance.IsInUndoMode)
                return;

            if (writeToFile)
                Plugin.WriteSaveData();
        }
    }
}