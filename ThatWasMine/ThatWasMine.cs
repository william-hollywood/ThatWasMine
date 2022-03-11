using BepInEx;
using R2API;
using R2API.Utils;
using RoR2;
using UnityEngine;

namespace ThatWasMine
{
    [BepInDependency(R2API.R2API.PluginGUID)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [R2APISubmoduleDependency(nameof(ItemAPI), nameof(LanguageAPI))]
    public class ThatWasMine : BaseUnityPlugin
	{
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "WDH";
        public const string PluginName = "ThatWasMine";
        public const string PluginVersion = "1.0.1";

        private static readonly Xoroshiro128Plus treasureRng = new Xoroshiro128Plus(0UL);
        private static readonly PickupDropTable dropTable = LegacyResourcesAPI.Load<PickupDropTable>("DropTables/dtSacrificeArtifact");

        //The Awake() method is run at the very start when the game is initialized.
        public void Awake()
        {
            On.RoR2.Artifacts.SacrificeArtifactManager.OnServerCharacterDeath += (orig, damageReport) => 
            {
                DoDeath(damageReport);
            };

            On.RoR2.Artifacts.SacrificeArtifactManager.OnServerStageBegin += (orig, stage) =>
            {
                treasureRng.ResetSeed(Run.instance.treasureRng.nextUlong);
            };

            // This line of log will appear in the bepinex console when the Awake method is done.
            Logger.LogInfo(nameof(Awake) + " done.");
        }

        /// <summary>
        /// Do the calculations for an item drop whenever a character dies on the server side.
        /// Ripped from decompiled source ror2 code. only change is the message sending
        /// </summary>
        /// <param name="damageReport">damage report of the killing hit</param>
        public void DoDeath(DamageReport damageReport)
        {
            if (!damageReport.victimMaster)
            {
                return;
            }
            if (damageReport.attackerTeamIndex == damageReport.victimTeamIndex && damageReport.victimMaster.minionOwnership.ownerMaster)
            {
                return;
            }
            float expAdjustedDropChancePercent = Util.GetExpAdjustedDropChancePercent(5f, damageReport.victim.gameObject);
            Debug.Log($"Drop chance from {damageReport.victimBody}: {expAdjustedDropChancePercent}");
            if (Util.CheckRoll(expAdjustedDropChancePercent, 0f, null))
            {
                PickupIndex pickupIndex = dropTable.GenerateDrop(treasureRng);
                if (pickupIndex != PickupIndex.none)
                {
                    CreateMessage(damageReport, pickupIndex);
                    PickupDropletController.CreatePickupDroplet(pickupIndex, damageReport.victimBody.corePosition, Vector3.up * 20f);
                }
            }
        }
        /// <summary>
        /// Create and send a message containing who killed the enemy and what item dropped
        /// </summary>
        /// <param name="damageReport">damage report of the killing hit</param>
        /// <param name="pickupIndex">item generated for a drop</param>
        private static void CreateMessage(DamageReport damageReport, PickupIndex pickupIndex)
        {
            // TODO (WDH): use the networking api to send something to the player who killed the monster and send a chat message from them saying: "Mine!" or something similar, or use localized steam nicknames of the client
            // Currently does all names on the server side
            PickupDef pickupDef = PickupCatalog.GetPickupDef(pickupIndex);
            CharacterBody attackerBody = damageReport.attackerOwnerMaster ? damageReport.attackerOwnerMaster.GetBody() : damageReport.attackerBody;
            string msg = $"{attackerBody.GetUserName()} <style=cEvent>got dropped</style> {Util.GenerateColoredString(Language.GetString(pickupDef.nameToken), pickupDef.baseColor)}</color>";
            Chat.SendBroadcastChat(new Chat.SimpleChatMessage
            {
                baseToken = "{0}",
                paramTokens = new[] { msg }
            });
        }
    }
}
