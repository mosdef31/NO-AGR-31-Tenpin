using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using RocketPod.Ballistics;
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
        public static ConfigEntry<bool> HudHideWithGear { get; private set; } = null!;
        public static ConfigEntry<bool> HudCockpitOnly { get; private set; } = null!;

        public static ConfigEntry<bool> DumpTuningReadout { get; private set; } = null!;
        public static ConfigEntry<bool> DumpPrefabRenderers { get; private set; } = null!;
        public static ConfigEntry<bool> DumpFlightModels { get; private set; } = null!;
        public static ConfigEntry<bool> CheckBallistics { get; private set; } = null!;
        public static ConfigEntry<string> CheckOnlyWeapon { get; private set; } = null!;
        public static ConfigEntry<string> CheckFileName { get; private set; } = null!;

        public static Settled<float> HudStepScale { get; } = Fixed(6f);

        public static Settled<float> HudReleaseToleranceMeters { get; } = Fixed(0f);

        public static Settled<float> HudReachArcDegrees { get; } = Fixed(14f);

        public static Settled<float> HudFootprintMinPixels { get; } = Fixed(9f);

        public static Settled<float> HudMagnifierPixels { get; } = Fixed(190f);

        public static Settled<float> HudMagnifierTriggerFactor { get; } = Fixed(8f);

        public static Settled<bool> HudTargetSizeTolerance { get; } = Fixed(true);

        public static Settled<bool> PoweredAimpoint { get; } = Fixed(true);

        public static Settled<bool> CorrectSeekerFlightTime { get; } = Fixed(true);

        public static Settled<bool> SampleTerrainHeight { get; } = Fixed(true);

        public static Settled<float> AimpointStepScale { get; } = Fixed(4f);

        public static Settled<bool> GuidanceBudget { get; } = Fixed(true);

        public static Settled<float> GuidanceBudgetMilliradians { get; } = Fixed(5f);

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
            Fixed("AttackHelo1:2,3,4; UtilityHelo1:0,1; trainer:1,2; VTOLTrainer1:3,4");

        public static Settled<bool> UseStockEffects { get; } = Fixed(true);

        public static Settled<string> MotorEffectDonor { get; } = Fixed("");

        public static Settled<bool> HideFiredRounds { get; } = Fixed(true);

        public static Settled<string> RoundContainerName { get; } = Fixed("Rounds");

        public static Settled<string> RoundNamePrefix { get; } = Fixed("Round");

        public static Settled<bool> SpinRounds { get; } = Fixed(true);

        public static Settled<float> SpinDegreesPerSecond { get; } = Fixed(420f);

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
            Fixed(MountRideHeight.Flush7);

        private Harmony? _harmony;

        private void Awake()
        {
            try
            {
                Instance = this;
                Log = base.Logger;

                BindConfig();

                _harmony = new Harmony(PluginInfo.GUID);

                _harmony.PatchAll(typeof(Encyclopedia_AfterLoad_RegistrationPatch));

                if (UseStockEffects.Value)
                    _harmony.PatchAll(typeof(Missile_OnStartClient_EffectsPatch));

                _harmony.PatchAll(typeof(WeaponStation_LaunchMount_CyclePatch));

                _harmony.PatchAll(typeof(WeaponManager_Fire_SingleTargetPatch));

                _harmony.PatchAll(typeof(MissileLauncher_OnEnable_RoundVisualsPatch));

                _harmony.PatchAll(typeof(Missile_OnStartClient_SpinPatch));

                _harmony.PatchAll(typeof(Missile_OnStartClient_SignaturePatch));

                _harmony.PatchAll(typeof(Missile_OnStartClient_IRSignaturePatch));

                _harmony.PatchAll(typeof(InertialSeekerShell_Initialize_Patch));

                _harmony.PatchAll(typeof(Kinematics_GetBallisticAimPoint_FlightTimePatch));

                gameObject.AddComponent<AssetCheckRunner>();
                gameObject.AddComponent<Hud.TenpinHud>();

                if (CheckBallistics.Value)
                {

                    bool ok = RoundSpecFactory.Resolve(Log);
                    Log.LogInfo($"[RocketPod] Flight model reflection: {(ok ? "OK" : "FAILED")}");

                    BallisticsCheck.Install(_harmony);
                    gameObject.AddComponent<BallisticsRunner>();
                }

                Log.LogInfo($"[RocketPod] Loaded v{PluginInfo.Version}. " +
                            $"ballisticsCheck={CheckBallistics.Value}");
            }
            catch (Exception ex)
            {
                base.Logger.LogError($"[RocketPod] Awake failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void BindConfig()
        {

            HudEnabled = Config.Bind(
                "HUD", "Enabled", true,
                new ConfigDescription(
                    "Draw the AGR-31's own weapon HUD while the pod is the selected store. " +
                    "Off falls back to the stock missile UI, which has no impact cue at all.",
                    null,
                    new ConfigurationManagerAttributes { Order = 100 }));

            HudModeKey = Config.Bind(
                "HUD", "ModeKey", KeyCode.B,
                new ConfigDescription(
                    "Switch between direct (CCIP) and artillery (CCRP) presentation. A keypress " +
                    "rather than an automatic range threshold, deliberately: a HUD that changes " +
                    "itself mid-attack feels broken even when the switch is correct.",
                    null,
                    new ConfigurationManagerAttributes { Order = 90 }));

            HudDesignateKey = Config.Bind(
                "HUD", "DesignateKey", KeyCode.T,
                new ConfigDescription(
                    "In artillery mode, designate the ground point under the cursor while the map " +
                    "is MAXIMIZED. Clearing is not on this key - it is on the game's own untarget " +
                    "button, so one button means 'forget that target' whatever kind it is. A unit " +
                    "lock always wins over a ground designation.",
                    null,
                    new ConfigurationManagerAttributes { Order = 80 }));

            HudPanelAnchor = Config.Bind(
                "HUD", "PanelAnchor", Hud.HudAnchor.RightBelowWeapons,
                new ConfigDescription(
                    "Where the text block and the magnifier sit. Presets rather than raw " +
                    "coordinates, because the useful positions are decided by what else is on " +
                    "screen: the chat log and kill feed own the top left, the stock weapon panel " +
                    "owns the top right, and the bottom edge carries the gear and flap cues. " +
                    "RightBelowWeapons clears all of them.",
                    null,
                    new ConfigurationManagerAttributes { Order = 70 }));

            HudMaxRangeArc = Config.Bind(
                "HUD", "MaxRangeArc", true,
                new ConfigDescription(
                    "Draw the maximum-reach arc on the ground in artillery mode. It answers 'can " +
                    "I touch that from here' at a glance instead of by comparing two numbers.",
                    null,
                    new ConfigurationManagerAttributes { Order = 60 }));

            HudMagnifier = Config.Bind(
                "HUD", "Magnifier", true,
                new ConfigDescription(
                    "Draw a magnified inset of the area around the designated point. Lofting a " +
                    "long shot puts the cues low on the screen over the canopy rail, exactly when " +
                    "you are trying to lay one mark on another to within a few pixels. The inset " +
                    "magnifies the same marks rather than re-rendering them, so it cannot " +
                    "disagree with them, and it still works when the pipper has gone off the " +
                    "bottom of the screen.",
                    null,
                    new ConfigurationManagerAttributes { Order = 50 }));

            HudHideWithGear = Config.Bind(
                "HUD", "HideWithGear", true,
                new ConfigDescription(
                    "Hide the weapon HUD while the landing gear is down, which is what every " +
                    "stock weapon HUD does.",
                    null,
                    new ConfigurationManagerAttributes { Order = 40 }));

            HudCockpitOnly = Config.Bind(
                "HUD", "CockpitOnly", true,
                new ConfigDescription(
                    "Hide the HUD on the external cameras. Ours is drawn to a screen-space " +
                    "overlay canvas of our own, so unlike the stock HUD - which lives on the " +
                    "cockpit glass and simply is not in shot from outside - nothing hides it for " +
                    "us, and it would otherwise float over an orbit or chase view.",
                    null,
                    new ConfigurationManagerAttributes { Order = 30 }));

            DumpTuningReadout = Config.Bind(
                "Advanced", "TuningReadout", false,
                new ConfigDescription(
                    "Print the round's MAXIMUM range and the stock damage table at the missions " +
                    "menu. The range comes from an elevation sweep to 70 degrees, so it reports " +
                    "what the round is capable of rather than whatever loft was flown - and it " +
                    "needs no flight at all.",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true, Order = 100 }));

            DumpPrefabRenderers = Config.Bind(
                "Advanced", "DumpPrefabRenderers", false,
                new ConfigDescription(
                    "Dump the mounted prefab's transforms, meshes, materials, shaders, layers and " +
                    "scales at the missions menu, next to a stock pod's. This is what diagnoses " +
                    "an invisible or mis-shaded weapon from a log instead of by guessing.",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true, Order = 90 }));

            DumpFlightModels = Config.Bind(
                "Advanced", "DumpFlightModels", false,
                new ConfigDescription(
                    "Dump every stock round's drag curve, lift curve, torque, PID and fin area " +
                    "next to ours, sorted by drag per unit mass. These are serialized ASSET values " +
                    "and cannot be read from a decompile, which matters because Tenpin once " +
                    "shipped with empty aero curves and zero torque and nothing noticed.",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true, Order = 80 }));

            CheckBallistics = Config.Bind(
                "Advanced", "CheckBallistics", false,
                new ConfigDescription(
                    "Predict every launch with the trajectory solver and score it at impact. NOT " +
                    "FREE: each launch runs three full integrations to ground impact plus a " +
                    "per-tick sample of every live round, and a seven-tube pod on six stations is " +
                    "42 launches in a few seconds, which is visibly laggy. Turn it on for a " +
                    "measurement run and fire ONE pod, ideally with CheckOnlyWeapon set.",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true, Order = 70 }));

            CheckOnlyWeapon = Config.Bind(
                "Advanced", "CheckOnlyWeapon", "",
                new ConfigDescription(
                    "Only score rounds whose unit name contains this text, e.g. \"MLRS\". Empty " +
                    "scores every launch, which gets noisy fast in a real mission.",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true, Order = 60 }));

            CheckFileName = Config.Bind(
                "Advanced", "CheckFileName", "rocketpod-ballistics.txt",
                new ConfigDescription(
                    "Written under the BepInEx folder. A file rather than the log because " +
                    "LogOutput.log is truncated on the next launch, and this is exactly the " +
                    "output that gets read one launch too late.",
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
            try { enc = Encyclopedia.i; } catch {  }
            if (enc == null) return;

            try
            {
                EncyclopediaRegistration.EnsureRegisteredAndRebuild();

                ExtraHardpoints.Apply();

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
