using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using RocketPod.Ballistics;
using Shared.Ballistics;
using UnityEngine;

namespace RocketPod
{

    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    [BepInProcess("NuclearOption.exe")]

    [BepInDependency(BlueprinterGUID)]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal const string BlueprinterGUID = "com.nikkorap.blueprinter";

        public static Plugin? Instance { get; private set; }
        public static ManualLogSource Log { get; private set; } = null!;

        public readonly struct Settled<T>
        {
            public T Value { get; }
            public Settled(T value) { Value = value; }
        }

        private static Settled<T> Fixed<T>(T value) => new Settled<T>(value);

        public static ConfigEntry<bool> HudEnabled { get; private set; } = null!;
        public static ConfigEntry<KeyCode> HudModeKey { get; private set; } = null!;
        public static ConfigEntry<KeyCode> HudDesignateKey { get; private set; } = null!;
        public static ConfigEntry<Hud.HudAnchor> HudPanelAnchor { get; private set; } = null!;
        public static ConfigEntry<bool> HudMaxRangeArc { get; private set; } = null!;
        public static ConfigEntry<bool> HudMagnifier { get; private set; } = null!;
        public static ConfigEntry<bool> HudMapMarker { get; private set; } = null!;
        public static ConfigEntry<float> HudNoseAimBelowKph { get; private set; } = null!;
        public static ConfigEntry<bool> HudTerrainCheck { get; private set; } = null!;
        public static ConfigEntry<bool> HudHideWithGear { get; private set; } = null!;
        public static ConfigEntry<bool> HudCockpitOnly { get; private set; } = null!;

        public static ConfigEntry<bool> ReleaseAssist { get; private set; } = null!;
        public static ConfigEntry<float> AiLoadoutChance { get; private set; } = null!;
        public static ConfigEntry<bool> AiForceLoadout { get; private set; } = null!;
        public static ConfigEntry<KeyCode> ReleaseAssistKey { get; private set; } = null!;
        public static ConfigEntry<bool> TiltAssist { get; private set; } = null!;
        public static ConfigEntry<KeyCode> TiltAssistKey { get; private set; } = null!;
        public static ConfigEntry<float> TiltAssistAuthority { get; private set; } = null!;

        public static ConfigEntry<bool> SillyEffectsEnabled { get; private set; } = null!;

        public static ConfigEntry<float> StrikeFinHold { get; private set; } = null!;
        public static ConfigEntry<float> StrikeFinSweep { get; private set; } = null!;

        public static ConfigEntry<bool> WaterBackstop { get; private set; } = null!;

        public static ConfigEntry<bool> DumpTuningReadout { get; private set; } = null!;
        public static ConfigEntry<bool> DumpPrefabRenderers { get; private set; } = null!;
        public static ConfigEntry<bool> DumpFlightModels { get; private set; } = null!;
        public static ConfigEntry<bool> CheckBallistics { get; private set; } = null!;
        public static ConfigEntry<string> CheckOnlyWeapon { get; private set; } = null!;
        public static ConfigEntry<string> CheckFileName { get; private set; } = null!;

        public static ConfigEntry<bool> LaunchTrace { get; private set; } = null!;

        public static ConfigEntry<bool> AiShotAudit { get; private set; } = null!;

        public static ConfigEntry<int> AiShotAuditCount { get; private set; } = null!;

        public static ConfigEntry<bool> AiReport { get; private set; } = null!;

        public static ConfigEntry<float> AiReportSeconds { get; private set; } = null!;

        public static Settled<float> HudStepScale { get; } = Fixed(6f);

        public static Settled<float> HudReleaseToleranceMeters { get; } = Fixed(0f);

        public static Settled<float> HudReachArcDegrees { get; } = Fixed(14f);

        public static Settled<float> HudFootprintMinPixels { get; } = Fixed(9f);

        public static Settled<float> HudMagnifierPixels { get; } = Fixed(190f);

        public static Settled<bool> HudDirector { get; } = Fixed(true);

        public static Settled<float> HudDirectorInterval { get; } = Fixed(0.5f);

        public static Settled<float> HudMagnifierTriggerFactor { get; } = Fixed(8f);

        public static Settled<bool> HudTargetSizeTolerance { get; } = Fixed(true);

        public static Settled<bool> PoweredAimpoint { get; } = Fixed(true);

        public static Settled<bool> CorrectSeekerFlightTime { get; } = Fixed(true);

        public static Settled<bool> SampleTerrainHeight { get; } = Fixed(true);

        public static Settled<float> AimpointStepScale { get; } = Fixed(4f);

        public static Settled<bool> LeadLockedAimpoint { get; } = Fixed(true);

        public static Settled<bool> GuidanceBudget { get; } = Fixed(true);

        public static Settled<float> GuidanceBudgetMilliradians { get; } = Fixed(5f);

        public static Settled<float> AiGuidanceBudgetMilliradians { get; } = Fixed(45f);

        public static Settled<float> AiGateMargin { get; } = Fixed(0.7f);

        public static Settled<bool> AngularDispersion { get; } = Fixed(true);

        public static Settled<float> DispersionMilliradians { get; } = Fixed(1.5f);

        public static Settled<float> MinDispersionMeters { get; } = Fixed(12f);

        public static Settled<float> MaxDispersionMeters { get; } = Fixed(0f);

        public static Settled<bool> SingleTargetSalvo { get; } = Fixed(true);

        public static Settled<bool> RoundRadarSignature { get; } = Fixed(true);

        public static Settled<float> RoundRCS { get; } = Fixed(0.01f);

        public static Settled<bool> RoundIRSignature { get; } = Fixed(false);

        public static Settled<float> RoundIRIntensity { get; } = Fixed(0.2f);

        public static Settled<float> RoundDamageTolerance { get; } = Fixed(1.0f);

        public static Settled<bool> StoreCardFields { get; } = Fixed(true);

        public static Settled<float> RoundCostMillions { get; } = Fixed(0.025f);

        public static Settled<string> ExtraHardpoints { get; } =
            Fixed("AttackHelo1:2,3,4; UtilityHelo1:0,1; trainer:1,2; VTOLTrainer1:3,4; " +
                  "MiG-15:2; COIN:2,3; CAS1:2,3,4,5,6,7; RAH-72:0,1,2,3; F-16M:1,2,3,4; Shrike:2,3");

        public static Settled<bool> UseStockEffects { get; } = Fixed(true);

        public static ConfigEntry<string> MotorEffectDonor { get; private set; } = null!;

        public static Settled<string> WarheadEffectDonor { get; } =
            Fixed("Rocket2, Rocket_MLRS1, Rocket1, AGR, AGM1");

        public static Settled<string> LaunchEffectDonor { get; } =
            Fixed("Rocket_MLRS1, Rocket2, Rocket1, AGR");

        public static Settled<bool> HideFiredRounds { get; } = Fixed(true);

        public static Settled<string> RoundContainerName { get; } = Fixed("Rounds");

        public static Settled<string> RoundNamePrefix { get; } = Fixed("Round");

        public static Settled<bool> SpinRounds { get; } = Fixed(true);

        public static Settled<float> SpinDegreesPerSecond { get; } = Fixed(640f);

        public static Settled<float> GlowNozzleInset { get; } = Fixed(0.10f);

        public static Settled<float> GlowSizeScale { get; } = Fixed(0.55f);

        public static Settled<bool> AiEmployment { get; } = Fixed(true);

        public static Settled<float> AiSalvoEconomyRange { get; } = Fixed(5000f);

        public static Settled<float> AiFullSalvoRange { get; } = Fixed(17000f);

        public static Settled<float> AiSalvoNear { get; } = Fixed(12f);

        public static Settled<float> AiSalvoFar { get; } = Fixed(18f);

        public static Settled<float> AiOverwhelmFactor { get; } = Fixed(2.0f);

        public static Settled<float> AiPreferredMinRange { get; } = Fixed(8000f);

        public static Settled<float> AiEgressSeconds { get; } = Fixed(25f);

        public static Settled<int> AiTargetsPerPass { get; } = Fixed(3);

        public static Settled<string> AiHexAircraft { get; } = Fixed("CI-22");

        public static Settled<int> AiSelfDefenceSets { get; } = Fixed(2);

        public static Settled<float> RoundRadarSize { get; } = Fixed(0.05f);

        public static Settled<bool> AiConvoyAim { get; } = Fixed(true);

        public static Settled<int> AiConvoyMinVehicles { get; } = Fixed(3);

        public static Settled<bool> RoundsCountAsAttacks { get; } = Fixed(true);

        public static Settled<float> RoundThreatRadius { get; } = Fixed(150f);

        public static Settled<float> AiAimLeadMetres { get; } = Fixed(4000f);

        public static Settled<float> AiRangeBias { get; } = Fixed(0.007f);

        public static Settled<float> AiTimeOfFlightBias { get; } = Fixed(1.03f);

        public static Settled<float> AiPromisingFactor { get; } = Fixed(3f);

        public static Settled<float> AiGiveUpSeconds { get; } = Fixed(8f);

        public static Settled<float> AiCrossingCeiling { get; } = Fixed(2.5f);

        public static Settled<int> AiBurstsPerApproach { get; } = Fixed(3);

        public static Settled<float> AiLoiterSeconds { get; } = Fixed(14f);

        public static Settled<float> AiPassSeconds { get; } = Fixed(75f);

        public static Settled<float> AiBurstSeconds { get; } = Fixed(2.5f);

        public static Settled<float> AiAbortRange { get; } = Fixed(2000f);

        public static Settled<float> AiArcSmoothing { get; } = Fixed(0.35f);

        public static Settled<float> AiCrossingLead { get; } = Fixed(0.35f);

        public static Settled<float> AiTrimGain { get; } = Fixed(0.35f);

        public static Settled<float> AiTrimLead { get; } = Fixed(0.5f);

        public static Settled<float> AiTrimRateSmoothing { get; } = Fixed(0.35f);

        public static Settled<float> AiTrimSettleDegrees { get; } = Fixed(1.5f);

        public static Settled<float> AiTrimDeadband { get; } = Fixed(0.3f);

        public static Settled<float> AiTrimLimitDegrees { get; } = Fixed(4f);

        public static Settled<float> AiAimSlewDegreesPerSecond { get; } = Fixed(2.5f);

        public static Settled<float> AiAimEffort { get; } = Fixed(0.3f);

        public static Settled<float> AiMaxBankDegrees { get; } = Fixed(35f);

        public static Settled<float> AiSettleSeconds { get; } = Fixed(0.2f);

        public static Settled<float> AiSteadyRateDegrees { get; } = Fixed(14f);

        public static Settled<float> AiHeloShotInterval { get; } = Fixed(0.6f);

        public static Settled<float> AiHeloSteadyRateDegrees { get; } = Fixed(18f);

        public static Settled<float> AiHeloPopUpRange { get; } = Fixed(9000f);

        public static Settled<float> AiHeloMaxSideSpeed { get; } = Fixed(6f);

        public static Settled<float> AiHeloRecoverTimeout { get; } = Fixed(8f);

        public static Settled<float> AiHeloAbortAlignDegrees { get; } = Fixed(20f);

        public static Settled<float> AiHeloRunInAlignDegrees { get; } = Fixed(8f);

        public static Settled<float> AiHeloRunInSeconds { get; } = Fixed(2.5f);

        public static Settled<float> AiHeloPitchRateDegrees { get; } = Fixed(12f);

        public static Settled<float> AiHeloPitchTimeout { get; } = Fixed(6f);

        public static Settled<int> AiHeloSalvo { get; } = Fixed(19);
        public static Settled<float> AiHeloFireSeconds { get; } = Fixed(4.5f);

        public static Settled<float> AiHeloBreakDegrees { get; } = Fixed(10f);
        public static Settled<float> AiHeloBreakSeconds { get; } = Fixed(6f);

        public static Settled<float> AiHeloMaxLoftDegrees { get; } = Fixed(38f);

        public static Settled<float> AiStationaryTolerance { get; } = Fixed(1.15f);

        public static Settled<float> AiUnroutedTolerance { get; } = Fixed(1.5f);

        public static Settled<float> AiSolveInterval { get; } = Fixed(1f);

        public static Settled<float> AiSolverStepScale { get; } = Fixed(8f);

        public static Settled<float> AiClusterRadius { get; } = Fixed(200f);

        public static Settled<float> TuningLaunchAltitude { get; } = Fixed(1500f);

        public static Settled<float> TuningLaunchSpeed { get; } = Fixed(170f);

        public static Settled<float> TuningTargetRangeKm { get; } = Fixed(20f);

        public static Settled<float> TargetRangeMinKm { get; } = Fixed(15f);

        public static Settled<float> TargetRangeMaxKm { get; } = Fixed(20f);

        public static Settled<string> FlightModelDonors { get; } =
            Fixed("Rocket1,Rocket2,Rocket_MLRS1");

        public static Settled<KeyCode> ControlRoundKey { get; } = Fixed(KeyCode.F6);

        public static Settled<string> ControlRoundName { get; } = Fixed("MLRS");

        public static Settled<float> ControlRoundElevation { get; } = Fixed(25f);

        public static Settled<float> ControlRoundBoost { get; } = Fixed(40f);

        public static Settled<float> MountVerticalOffset { get; } =
            Fixed(PluginInfo.Mounts[0].FlushOffset);

        private Harmony? _harmony;

        private void Awake()
        {
            try
            {
                Instance = this;
                Log = base.Logger;

                BindConfig();

                _harmony = new Harmony(PluginInfo.GUID);

                Patch(typeof(Encyclopedia_AfterLoad_RegistrationPatch));

                Patch(typeof(WeaponMount_Initialize_NullPrefabGuard));

                Patch(typeof(WeaponMount_Initialize_HideWeaponPrefabPatch));

                Patch(typeof(Hardpoint_SpawnMount_OffsetPatch));

                Patch(typeof(MissileLauncher_OnEnable_SwapPatch));

                Patch(typeof(Unit_SyncAmmo_DivergencePatch));

                if (AiEmployment.Value)
                {
                    Patch(typeof(AIPilotCombatModes_UseMissiles_TenpinPatch));

                    Patch(typeof(AIHeloCombatState_UseMissiles_TenpinPatch));
                    Patch(typeof(Autopilot_AutoAim_BankPatch));

                    if (AiShotAudit.Value)
                    {
                        Patch(typeof(Missile_OnStartClient_AuditPatch));
                        Patch(typeof(Missile_Detonate_AuditPatch));
                    }

                if (RoundsCountAsAttacks.Value)
                {

                    Patch(typeof(Missile_OnStartClient_ThreatPatch));
                    Patch(typeof(Missile_Detonate_ThreatPatch));
                }
                }

                Patch(typeof(AircraftParameters_GetRandomStandardLoadout_TenpinPatch));
                Patch(typeof(WeaponManager_SelectAIAircraftWeapons_TenpinPatch));
                Patch(typeof(Spawner_SpawnAircraft_TenpinLoadoutPatch));

                Patch(typeof(Missile_OnStartClient_BudgetPatch));

                if (UseStockEffects.Value)
                    Patch(typeof(Missile_OnStartClient_EffectsPatch));

                if (UseStockEffects.Value)
                    Patch(typeof(Missile_OnStartClient_FlightAudioPatch));

                Patch(typeof(WeaponStation_LaunchMount_CyclePatch));

                Patch(typeof(WeaponManager_Fire_SingleTargetPatch));

                Patch(typeof(Missile_OnStartClient_SpinPatch));

                Patch(typeof(Missile_OnStartClient_SignaturePatch));

                Patch(typeof(Missile_OnStartClient_IRSignaturePatch));

                Patch(typeof(InertialSeekerShell_Initialize_Patch));

                Patch(typeof(Kinematics_GetBallisticAimPoint_FlightTimePatch));

                Patch(typeof(WeaponManager_Fire_ReleaseAssistPatch));
                Patch(typeof(PilotPlayerState_PlayerAxisControls_TiltAssistPatch));

                Patch(typeof(UnitMapIcon_SetIcon_RoundIconPatch));

                Patch(typeof(TenpinLauncher.WeaponStation_AccountAmmo_PrunePatch));

                Patch(typeof(Missile_OnStartClient_WaterPatch));

                Patch(typeof(Missile_OnStartClient_StrikeFinPatch));

                AuditPatchClasses();

                gameObject.AddComponent<AssetCheckRunner>();
                gameObject.AddComponent<Hud.TenpinHud>();

                if (CheckBallistics.Value)
                {

                    bool ok = RoundSpecFactory.Resolve(Log);
                    Log.LogInfo($"[RocketPod] Flight model reflection: {(ok ? "OK" : "FAILED")}");

                    BallisticsCheck.Install(_harmony);
                    gameObject.AddComponent<BallisticsRunner>();
                }

                try
                {
                    using System.IO.Stream? bundle = typeof(Plugin).Assembly
                        .GetManifestResourceStream(PluginInfo.BundleName);
                    Log.LogInfo(bundle == null
                        ? "[RocketPod] No embedded bundle in this DLL, which is a packaging fault."
                        : $"[RocketPod] Embedded bundle: {bundle.Length:N0} bytes. If a Unity " +
                          "re-export is not showing up in game, check this number changed.");
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"[RocketPod] Could not measure the embedded bundle: {ex.Message}");
                }

                Log.LogInfo($"[RocketPod] Loaded v{PluginInfo.Version}. " +
                            $"ballisticsCheck={CheckBallistics.Value}");

                if (_patchFailures > 0)
                    base.Logger.LogError(
                        $"[RocketPod] {_patchFailures} patch class(es) failed to register, so " +
                        "some features are MISSING rather than broken. Search this log for " +
                        "'FAILED to register' for the list. The mod is otherwise running.");
            }
            catch (Exception ex)
            {
                base.Logger.LogError($"[RocketPod] Awake failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void Patch(Type patchClass)
        {
            try
            {
                _patchRegistered.Add(patchClass);
                _harmony!.PatchAll(patchClass);
            }
            catch (Exception ex)
            {
                _patchFailures++;
                base.Logger.LogError(
                    $"[RocketPod] Patch class '{patchClass.Name}' FAILED to register and was " +
                    $"skipped: {ex.Message}. Everything else still loaded. If a feature is " +
                    "missing in game, this is the line that says which one.");
            }
        }

        private int _patchFailures;

        private readonly HashSet<Type> _patchRegistered = new HashSet<Type>();

        private void AuditPatchClasses()
        {
            try
            {
                var orphans = new List<string>();

                foreach (Type t in typeof(Plugin).Assembly.GetTypes())
                {
                    if (_patchRegistered.Contains(t)) continue;
                    if (t.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Length == 0) continue;
                    orphans.Add(t.Name);
                }

                if (orphans.Count == 0)
                {
                    Log.LogInfo("[RocketPod] Patch audit: every [HarmonyPatch] class in this assembly is " +
                         "registered.");
                    return;
                }

                orphans.Sort();
                Log.LogInfo($"[RocketPod] Patch audit: {orphans.Count} [HarmonyPatch] class(es) were NOT " +
                     $"registered this session - {string.Join(", ", orphans.ToArray())}. Some are " +
                     "config-gated and belong here; one that is not is a patch doing nothing at " +
                     "all, which is what happened to the map icons, the ride heights and the " +
                     "mount-name repair.");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"[RocketPod] Patch audit could not run: {ex.Message}");
            }
        }

        private void BindConfig()
        {

            AiLoadoutChance = Config.Bind(
                "AI", "LoadoutChance", 0.25f,
                new ConfigDescription(
                    "Chance an AI flight carries the AGR-31 on a pylon that can take " +
                    "it, rolled per hardpoint set. 1 arms every cleared aircraft, 0 " +
                    "arms none.",
                    new AcceptableValueRange<float>(0f, 1f),
                    new ConfigurationManagerAttributes { Order = 100 }));

            AiForceLoadout = Config.Bind(
                "Advanced", "AiForceLoadout", false,
                new ConfigDescription(
                    "Arms every AI flight on a cleared airframe with the 'Saturation " +
                    "and Self Defence' loadout, ignoring LoadoutChance.",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true, Order = 40 }));

            HudEnabled = Config.Bind(
                "HUD", "Enabled", true,
                new ConfigDescription(
                    "Draws the AGR-31's own weapon HUD while the pod is the selected " +
                    "store. Off leaves the stock missile UI.",
                    null,
                    new ConfigurationManagerAttributes { Order = 100 }));

            HudModeKey = Config.Bind(
                "HUD", "ModeKey", KeyCode.B,
                new ConfigDescription(
                    "Switches between direct (CCIP) and artillery (CCRP) " +
                    "presentation.",
                    null,
                    new ConfigurationManagerAttributes { Order = 90 }));

            HudDesignateKey = Config.Bind(
                "HUD", "DesignateKey", KeyCode.T,
                new ConfigDescription(
                    "Designates the ground point under the cursor in artillery mode, " +
                    "while the map is maximized. The game's untarget button clears " +
                    "it. A unit lock overrides a ground designation.",
                    null,
                    new ConfigurationManagerAttributes { Order = 80 }));

            HudPanelAnchor = Config.Bind(
                "HUD", "PanelAnchor", Hud.HudAnchor.RightBelowWeapons,
                new ConfigDescription(
                    "Where the text block and the magnifier sit on screen.",
                    null,
                    new ConfigurationManagerAttributes { Order = 70 }));

            HudMaxRangeArc = Config.Bind(
                "HUD", "MaxRangeArc", true,
                new ConfigDescription(
                    "Draws the maximum-reach arc on the ground in artillery mode.",
                    null,
                    new ConfigurationManagerAttributes { Order = 60 }));

            HudMagnifier = Config.Bind(
                "HUD", "Magnifier", true,
                new ConfigDescription(
                    "Draws a magnified inset of the area around the designated point.",
                    null,
                    new ConfigurationManagerAttributes { Order = 50 }));

            HudMapMarker = Config.Bind(
                "HUD", "MapMarker", true,
                new ConfigDescription(
                    "Marks the designated ground point on the map, as a diamond in " +
                    "your HUD colour, while the pod is the selected weapon.",
                    null,
                    new ConfigurationManagerAttributes { Order = 45 }));

            HudNoseAimBelowKph = Config.Bind(
                "HUD", "NoseAimBelowKph", 100f,
                new ConfigDescription(
                    "Below this airspeed the impact point is drawn along the " +
                    "aircraft's nose instead of along its velocity. Horizontal only. " +
                    "Fades out over the 40 km/h above the threshold; 0 turns it off.",
                    new AcceptableValueRange<float>(0f, 300f),
                    new ConfigurationManagerAttributes { Order = 44 }));

            HudTerrainCheck = Config.Bind(
                "HUD", "TerrainCheck", true,
                new ConfigDescription(
                    "Draws a designated point that is out of sight as a dashed " +
                    "diamond instead of a solid one.",
                    null,
                    new ConfigurationManagerAttributes { Order = 43 }));

            HudHideWithGear = Config.Bind(
                "HUD", "HideWithGear", true,
                new ConfigDescription(
                    "Hides the weapon HUD while the landing gear is down.",
                    null,
                    new ConfigurationManagerAttributes { Order = 40 }));

            HudCockpitOnly = Config.Bind(
                "HUD", "CockpitOnly", true,
                new ConfigDescription(
                    "Hides the HUD on the external cameras.",
                    null,
                    new ConfigurationManagerAttributes { Order = 30 }));

            ReleaseAssist = Config.Bind(
                "Assist", "Release assist", true,
                new ConfigDescription(
                    "Fires the pod while you hold the trigger, at the moment the " +
                    "rockets will land on the designated point. Releasing the trigger " +
                    "stops it. With no designation, a target out of reach, or the HUD " +
                    "hidden, the trigger fires normally.",
                    null,
                    new ConfigurationManagerAttributes { Order = 43 }));

            TiltAssist = Config.Bind(
                "Assist", "Tilt assist", false,
                new ConfigDescription(
                    "Adds pitch to bring the rockets onto the range you need while a " +
                    "point is designated. Your stick input is added on top and is " +
                    "never capped.",
                    null,
                    new ConfigurationManagerAttributes { Order = 42 }));

            ReleaseAssistKey = Config.Bind(
                "Assist", "ReleaseAssistKey", KeyCode.U,
                new ConfigDescription(
                    "Turns the release assist on and off in flight. The HUD shows " +
                    "AUTO while it is armed.",
                    null,
                    new ConfigurationManagerAttributes { Order = 44 }));

            TiltAssistKey = Config.Bind(
                "Assist", "TiltAssistKey", KeyCode.Y,
                new ConfigDescription(
                    "Arms and disarms the tilt assist in flight. It starts every " +
                    "sortie disarmed. The HUD shows TILT ARMED when armed and TILT " +
                    "while it is flying the shot.",
                    null,
                    new ConfigurationManagerAttributes { Order = 41 }));

            TiltAssistAuthority = Config.Bind(
                "Assist", "Tilt assist authority", 0.30f,
                new ConfigDescription(
                    "Fraction of the pitch axis the tilt assist may use. Your own " +
                    "stick is added on top and is never limited.",
                    new AcceptableValueRange<float>(0f, 1f),
                    new ConfigurationManagerAttributes { Order = 41 }));

            MotorEffectDonor = Config.Bind(
                "Effects", "Motor effect donor", "AAM1",
                new ConfigDescription(
                    "Which stock missile's motor effect the rockets borrow, by " +
                    "jsonKey. A comma separated list is allowed and the first that " +
                    "has fire wins; empty picks by closest burn time. Read per round. " +
                    "Suggested: AAM1, AGM2, AAM3.",
                    null,

                    new ConfigurationManagerAttributes { IsAdvanced = true, Order = 99 }));

            SillyEffectsEnabled = Config.Bind(
                "Effects", "Silly effects", false,
                new ConfigDescription(
                    "Flies the AGR-31's own cyan effects instead of a borrowed stock " +
                    "plume.",
                    null,
                    new ConfigurationManagerAttributes { Order = 100 }));

            StrikeFinHold = Config.Bind(
                "Advanced", "StrikeFinHold", 0.25f,
                new ConfigDescription(
                    "Seconds the AGR-51's fins stay folded after it leaves the pod.",
                    new AcceptableValueRange<float>(0f, 2f),
                    new ConfigurationManagerAttributes { IsAdvanced = true, Order = 97 }));

            StrikeFinSweep = Config.Bind(
                "Advanced", "StrikeFinSweep", 0.18f,
                new ConfigDescription(
                    "Seconds the AGR-51's fins take to swing out once they start.",
                    new AcceptableValueRange<float>(0.02f, 2f),
                    new ConfigurationManagerAttributes { IsAdvanced = true, Order = 96 }));

            WaterBackstop = Config.Bind(
                "Advanced", "WaterBackstop", true,
                new ConfigDescription(
                    "Ends a rocket that goes into the sea and is not detonated by the " +
                    "game. Leave this on: without it such a rocket is never removed at " +
                    "all, and enough of them will stutter the frame rate.",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true, Order = 98 }));

            DumpTuningReadout = Config.Bind(
                "Advanced", "TuningReadout", false,
                new ConfigDescription(
                    "Prints the round's maximum range and the stock damage table at " +
                    "the missions menu.",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true, Order = 100 }));

            DumpPrefabRenderers = Config.Bind(
                "Advanced", "DumpPrefabRenderers", false,
                new ConfigDescription(
                    "Dumps the mounted prefab's transforms, meshes, materials, " +
                    "shaders, layers and scales at the missions menu, next to a stock " +
                    "pod's.",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true, Order = 90 }));

            DumpFlightModels = Config.Bind(
                "Advanced", "DumpFlightModels", false,
                new ConfigDescription(
                    "Dumps every stock round's drag curve, lift curve, torque, PID " +
                    "and fin area next to ours, sorted by drag per unit mass.",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true, Order = 80 }));

            LaunchTrace = Config.Bind(
                "Advanced", "LaunchTrace", false,
                new ConfigDescription(
                    "Traces the launch path and prints a verdict. Detail for the " +
                    "first eight shots, counters after that, one summary once the " +
                    "salvo goes quiet.",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true, Order = 69 }));

            AiShotAudit = Config.Bind(
                "Advanced", "AiShotAudit", false,
                new ConfigDescription(
                    "Prints, per AI shot, where the salvo landed against where the " +
                    "profile predicted, with the miss distance.",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true, Order = 68 }));

            AiShotAuditCount = Config.Bind(
                "Advanced", "AiShotAuditCount", 12,
                new ConfigDescription(
                    "How many audited shots AiShotAudit prints before going quiet.",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true, Order = 67 }));

            AiReport = Config.Bind(
                "Advanced", "AiReport", false,
                new ConfigDescription(
                    "Prints a line each time an AI declines to shoot, naming the " +
                    "reason it held fire.",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true, Order = 66 }));

            AiReportSeconds = Config.Bind(
                "Advanced", "AiReportSeconds", 10f,
                new ConfigDescription(
                    "Seconds between repeats of the same AiReport reason.",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true, Order = 65 }));

            CheckBallistics = Config.Bind(
                "Advanced", "CheckBallistics", false,
                new ConfigDescription(
                    "Predicts every launch with the trajectory solver and scores it " +
                    "at impact. Costs three full integrations per launch plus a per- " +
                    "tick sample of every live round; a 42-round salvo is visibly " +
                    "laggy.",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true, Order = 70 }));

            CheckOnlyWeapon = Config.Bind(
                "Advanced", "CheckOnlyWeapon", "",
                new ConfigDescription(
                    "Only scores rounds whose unit name contains this text, e.g. " +
                    "\"MLRS\". Empty scores every launch.",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true, Order = 60 }));

            CheckFileName = Config.Bind(
                "Advanced", "CheckFileName", "rocketpod-ballistics.txt",
                new ConfigDescription(
                    "Name of the ballistics file, written under the BepInEx folder.",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true, Order = 50 }));
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }

    internal sealed class AssetCheckRunner : MonoBehaviour
    {
        private float _next = 2f;
        private int _attempts;

        private void Update()
        {
            if (_attempts > 30) { enabled = false; return; }
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 2f;
            _attempts++;

            Encyclopedia? enc = null;
            enc = GameData.EncyclopediaOrNull();
            if (enc == null) return;

            try
            {
                EncyclopediaRegistration.EnsureRegisteredAndRebuild();

                if (EncyclopediaRegistration.ResolvedMounts.Count == 0)
                {

                    return;
                }

                ExtraHardpoints.Apply();

                FxShaderBinding.RunOnce();

                TextureRescue.RunOnce();

                UnifiedStation.RunOnce();

                WarheadEffects.RunOnce();
                AssetCheck.RunOnce();

                if (Plugin.DumpFlightModels.Value) FlightModelDump.RunOnce();
                if (Plugin.DumpTuningReadout.Value) TuningReadout.RunOnce();
                if (Plugin.DumpPrefabRenderers.Value) PrefabDiagnostics.RunOnce();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Tenpin] Asset check runner failed: {ex.Message}");
            }
            enabled = false;
        }
    }

    internal sealed class BallisticsRunner : MonoBehaviour
    {
        private void FixedUpdate()
        {
            BallisticsCheck.SampleFlight();
        }

        private void Update()
        {
            if (Input.GetKeyDown(Plugin.ControlRoundKey.Value))
            {
                try
                {
                    BallisticsCheck.FireControlRound();
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"[RocketPod] Control round failed: {ex.Message}");
                }
            }
        }
    }
}
