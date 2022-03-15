using BepInEx;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using R2API.Utils;
using RoR2;
using System.Linq;

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
        public const string PluginVersion = "1.1.2";

        public const bool DEBUG = false;

        public static ConfigEntry<bool> PromptOnPlayerOnly { get; set; }

        //The Awake() method is run at the very start when the game is initialized, where i add my own hook
        public void Awake()
        {
            PromptOnPlayerOnly = Config.Bind(
                "Message Configuration",
                "PromptOnPlayerOnly",
                true,
                "Prompt item drops only on player kills"
                );

            IL.RoR2.Artifacts.SacrificeArtifactManager.OnServerCharacterDeath += (il) =>
            {
                ILCursor c = new(il);
                // goto where i want to insert my code with the cursor
                // i want to insert it directly before the PickupDroplet is created
                // using dnSpy i found a unique OpCode signature that starts the PickupDroplet creation
                c.GotoNext(
                    x => x.Match(OpCodes.Ldloc_1), // load the pickup index (used later in the method call)
                    x => x.Match(OpCodes.Ldarg_0) // loading the damage report
                    );

                // my edit works by loading the 2 items i need onto the stack and calling the CreateMessage (via EmitDelegate, which is just the call OpCode and the CreateMessage method)
                c.Emit(OpCodes.Ldarg_0); // load damage report
                c.Emit(OpCodes.Ldloc_1); // load pickup index
                c.EmitDelegate(CreateMessage); // create a message
            };

            // This line of log will appear in the bepinex console when the Awake method is done.
            Logger.LogInfo(nameof(Awake) + " done.");
        }

        /// <summary>
        /// Create and send a message containing who killed the enemy and what item dropped
        /// </summary>
        /// <param name="damageReport">damage report of the killing hit</param>
        /// <param name="pickupIndex">item generated for a drop</param>
        private void CreateMessage(DamageReport damageReport, PickupIndex pickupIndex)
        {
            // Dont send a message if you're the only one playing
            if (NetworkUser.readOnlyInstancesList.Count == 1 && !DEBUG)
                return;

            // Get the owner of the damaging entity (if any)
            CharacterBody attackerBody = damageReport.attackerOwnerMaster ? damageReport.attackerOwnerMaster.GetBody() : damageReport.attackerBody;
            
            // check the config for player only prompts and if it wasnt a player, dont continue.
            if (PromptOnPlayerOnly.Value && !NetworkUser.readOnlyInstancesList.Select(u => u.userName).Contains(attackerBody.GetUserName()))
                return;

            PickupDef pickupDef = PickupCatalog.GetPickupDef(pickupIndex);

            // TODO (WDH): use the networking api to send something to the player who killed the monster and send a local chat message so that localized steam nicknames of the client can be used
            // Currently does all names on the server side
            string msg = $"{attackerBody.GetUserName()} <style=cEvent>got dropped</style> {Util.GenerateColoredString(Language.GetString(pickupDef.nameToken), pickupDef.baseColor)}</color>";

            Chat.SendBroadcastChat(new Chat.SimpleChatMessage
            {
                baseToken = "{0}",
                paramTokens = new[] { msg }
            });
        }
    }
}
