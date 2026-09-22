using HarmonyLib;
using UnityEngine;

namespace QuantumObliterator.Util
{
    /// <summary>
    ///     Spawns the Obliterator's lightning effect with every damaging behaviour stripped out,
    ///     so shipping cargo never destroys the tag sign standing next to the piece.
    /// </summary>
    /// <remarks>
    ///     Why a Harmony prefix rather than editing the instance afterwards: <c>Aoe.Awake()</c>
    ///     bakes its collision ray mask from the <c>m_hit*</c> booleans, and Awake runs
    ///     synchronously inside <c>Object.Instantiate</c>. By the time Instantiate returns the mask
    ///     already includes structures, and <c>Aoe.ShouldHit()</c> only re-reads the *character*
    ///     flags, so nothing downstream filters build pieces back out. A prefix on Awake is the one
    ///     window where the flags can still be changed.
    ///
    ///     <see cref="Arming"/> is set only across our own Instantiate call, which is synchronous
    ///     and single-threaded, so the prefix neuters exactly the Aoe components of the clone we
    ///     asked for -- root and children alike -- and nothing else. Vanilla obliteration keeps its
    ///     damaging lightning.
    ///
    ///     Everything else is left strictly vanilla: the same Instantiate overload, the same
    ///     position and rotation, no staging parent and no deferred activation. An earlier version
    ///     staged the clone under a deactivated GameObject; reparenting out of an inactive parent
    ///     activates the object immediately, so Awake ran at the stage's position (the world
    ///     origin) and the effect's ZNetView registered a ZDO in the wrong sector, which ZNetScene
    ///     then culled a frame later -- clipped thunder, no bolt.
    ///
    ///     Damage is also zeroed as a second line of defence, and every visual field
    ///     (m_hitEffects, m_initiateEffect, m_chainObj, m_chainEffects, particles, renderers) is
    ///     left untouched, so the effect looks exactly like vanilla's.
    /// </remarks>
    internal static class HarmlessLightning
    {
        /// <summary>True only while our own Object.Instantiate call is running.</summary>
        internal static bool Arming;

        /// <summary>Aoe components the prefix neutered during the current Spawn.</summary>
        private static int _armedCount;

        private static bool _loggedComponentCount;

        internal static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            GameObject instance;

            _armedCount = 0;
            Arming = true;
            try
            {
                // Vanilla's exact call. Awake runs inside it, and the prefix below neuters each
                // Aoe just before Awake reads the flags it bakes into the ray mask.
                instance = Object.Instantiate(prefab, position, rotation);
            }
            finally
            {
                Arming = false;
            }

            QOLog.Debug($"Transfer lightning spawned at {position}; {_armedCount} Aoe component(s) neutered.");

            if (!_loggedComponentCount)
            {
                _loggedComponentCount = true;
                if (_armedCount == 0)
                {
                    QOLog.Warn(
                        "The Obliterator lightning prefab carries no Aoe components, so transfers " +
                        "may still damage nearby structures. The damage source is something else " +
                        "and QuantumObliterator cannot disarm it.");
                }
                else
                {
                    QOLog.Info($"Transfer lightning disarmed: {_armedCount} Aoe component(s) neutered.");
                }
            }

            return instance;
        }

        internal static void Neuter(Aoe aoe)
        {
            _armedCount++;

            // These decide the ray mask in Awake. With all of them off the AOE can touch nothing.
            aoe.m_hitCharacters = false;
            aoe.m_hitProps = false;
            aoe.m_hitTerrain = false;
            aoe.m_hitFriendly = false;
            aoe.m_hitEnemy = false;
            aoe.m_hitParent = false;
            aoe.m_hitOwner = false;
            aoe.m_launchCharacters = false;

            // Belt and braces: even if something does get hit, the hit carries nothing.
            aoe.m_damage = new HitData.DamageTypes();
            aoe.m_damagePerLevel = new HitData.DamageTypes();
            aoe.m_toolTier = 0;
            aoe.m_attackForce = 0f;
            aoe.m_knockBackForce = 0f;
            aoe.m_damageSelf = 0f;
            aoe.m_hitNoise = 0f;
            aoe.m_statusEffect = string.Empty;
            aoe.m_statusEffectIfBoss = string.Empty;
            aoe.m_statusEffectIfPlayer = string.Empty;

            // Terrain deformation is a separate path from damage.
            aoe.m_spawnOnHitTerrain = null;
        }
    }

    /// <summary>
    ///     Disarms the Aoe components of the clone <see cref="HarmlessLightning.Spawn"/> is
    ///     creating, in the only window where the ray mask has not been baked yet.
    /// </summary>
    /// <remarks>
    ///     Fires for every Aoe in the game, so the guard has to be the first thing it does:
    ///     vanilla obliteration, and every other AOE in Valheim, must keep its damage.
    /// </remarks>
    [HarmonyPatch(typeof(Aoe), "Awake")]
    internal static class Aoe_Awake_Patch
    {
        [HarmonyPrefix]
        private static void Prefix(Aoe __instance)
        {
            if (!HarmlessLightning.Arming) return;
            HarmlessLightning.Neuter(__instance);
        }
    }
}
