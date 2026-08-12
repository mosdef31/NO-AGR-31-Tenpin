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
        public static ConfigEntry<bool> HudMapMarker { get; private set; } = null!;
        public static ConfigEntry<float> HudNoseAimBelowKph { get; private set; } = null!;
        public static ConfigEntry<bool> HudTerrainCheck { get; private set; } = null!;
        public static ConfigEntry<bool> HudHideWithGear { get; private set; } = null!;
        public static ConfigEntry<bool> HudCockpitOnly { get; private set; } = null!;

        public static ConfigEntry<bool> ReleaseAssist { get; private set; } = null!;
        public static ConfigEntry<KeyCode> ReleaseAssistKey { get; private set; } = null!;
        public static ConfigEntry<bool> TiltAssist { get; private set; } = null!;
        public static ConfigEntry<KeyCode> TiltAssistKey { get; private set; } = null!;
        public static ConfigEntry<float> TiltAssistAuthority { get; private set; } = null!;

        public static ConfigEntry<bool> FunEffectsEnabled { get; private set; } = null!;

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
            Fixed("AttackHelo1:2,3,4; UtilityHelo1:0,1; trainer:1,2; VTOLTrainer1:3,4; " +
                  "MiG-15:2; COIN:2,3; RAH-72:0,1,2,3; F-16M:1,2,3,4; Shrike:2,3");

        public static Settled<bool> UseStockEffects { get; } = Fixed(true);

        public static Settled<string> MotorEffectDonor { get; } =
            Fixed("Rocket_MLRS1, Rocket2, Rocket1, AGR");

        public static Settled<string> WarheadEffectDonor { get; } =
            Fixed("Rocket2, Rocket_MLRS1, Rocket1, AGR, AGM1");

        public static Settled<string> LaunchEffectDonor { get; } =
            Fixed("Rocket_MLRS1, Rocket2, Rocket1, AGR");

        public static Settled<bool> HideFiredRounds { get; } = Fixed(true);

        public static Settled<string> RoundContainerName { get; } = Fixed("Rounds");

        public static Settled<string> RoundNamePrefix { get; } = Fixed("Round");

        public static Settled<bool> SpinRounds { get; } = Fixed(true);

        public static Settled<float> SpinDegreesPerSecond { get; } = Fixed(640f);

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

                _harmony.PatchAll(typeof(WeaponMount_Initialize_NullPrefabGuard));

                _harmony.PatchAll(typeof(Missile_OnStartClient_BudgetPatch));

                if (UseStockEffects.Value)
                    _harmony.PatchAll(typeof(Missile_OnStartClient_EffectsPatch));

                if (UseStockEffects.Value)
                    _harmony.PatchAll(typeof(MissileLauncher_Fire_AudioPatch));

                if (UseStockEffects.Value)
                    _harmony.PatchAll(typeof(MissileLauncher_OnEnable_LaunchParticlesPatch));

                if (UseStockEffects.Value)
                    _harmony.PatchAll(typeof(Missile_OnStartClient_FlightAudioPatch));

                _harmony.PatchAll(typeof(WeaponStation_LaunchMount_CyclePatch));

                _harmony.PatchAll(typeof(WeaponManager_Fire_SingleTargetPatch));

                _harmony.PatchAll(typeof(MissileLauncher_OnEnable_RoundVisualsPatch));

                _harmony.PatchAll(typeof(Missile_OnStartClient_SpinPatch));

                _harmony.PatchAll(typeof(Missile_OnStartClient_SignaturePatch));

                _harmony.PatchAll(typeof(Missile_OnStartClient_IRSignaturePatch));

                _harmony.PatchAll(typeof(InertialSeekerShell_Initialize_Patch));

                _harmony.PatchAll(typeof(Kinematics_GetBallisticAimPoint_FlightTimePatch));

                _harmony.PatchAll(typeof(WeaponManager_Fire_ReleaseAssistPatch));
                _harmony.PatchAll(typeof(PilotPlayerState_PlayerAxisControls_TiltAssistPatch));

                _harmony.PatchAll(typeof(UnitMapIcon_SetIcon_RoundIconPatch));

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

            HudMapMarker = Config.Bind(
                "HUD", "MapMarker", true,
                new ConfigDescription(
                    "Mark the designated ground point on the map. The designation is made on the " +
                    "map and until now was drawn only in the cockpit, so the one screen where " +
                    "you set it gave no confirmation that it took or where it landed. The mark " +
                    "is a diamond in your own HUD colour, and it appears only while the pod is " +
                    "the selected weapon.",
                    null,
                    new ConfigurationManagerAttributes { Order = 45 }));

            HudNoseAimBelowKph = Config.Bind(
                "HUD", "NoseAimBelowKph", 100f,
                new ConfigDescription(
                    "Below this airspeed the impact point is drawn as though the aircraft were " +
                    "moving straight along its nose, instead of along its actual velocity. " +
                    "Rockets keep whatever sideways speed you hand them, so when you are slow " +
                    "enough for a small drift to be a large part of your speed, a truthful cue " +
                    "wanders around under you and cannot be laid on a target. This one answers " +
                    "'where do they go if I stop drifting', which is the question you can fly " +
                    "to. Only the horizontal is substituted; a descent is always shown " +
                    "truthfully. It fades out over the 40 km/h above the threshold, and above " +
                    "that it does nothing at all - a jet's cue is unaffected. 0 turns it off.",
                    new AcceptableValueRange<float>(0f, 300f),
                    new ConfigurationManagerAttributes { Order = 44 }));

            HudTerrainCheck = Config.Bind(
                "HUD", "TerrainCheck", true,
                new ConfigDescription(
                    "Draw a designated point you cannot actually see as a dashed diamond instead " +
                    "of a solid one. The HUD is drawn over the world rather than in it, so a " +
                    "mark standing on a ridge and a mark three kilometres behind that ridge " +
                    "appear in the same place at the same size - this is the only thing telling " +
                    "them apart. One raycast per frame.",
                    null,
                    new ConfigurationManagerAttributes { Order = 43 }));

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

            ReleaseAssist = Config.Bind(
                "Assist", "Release assist", true,
                new ConfigDescription(
                    "Hold the trigger and the pod fires itself the moment the rockets will land on " +
                    "the point you designated, instead of you judging the release by eye. Holding " +
                    "the trigger is the consent - nothing fires unless you are already holding it, " +
                    "and letting go always stops it. It only ever delays a shot you aimed at " +
                    "something: with no designation, or a target out of reach, or the HUD hidden, " +
                    "the trigger behaves exactly as it does now. Once a salvo starts it stays open " +
                    "for the whole ripple.",
                    null,
                    new ConfigurationManagerAttributes { Order = 43 }));

            TiltAssist = Config.Bind(
                "Assist", "Tilt assist", false,
                new ConfigDescription(
                    "Fly the heading and let the pod fly the elevation. While the pod is selected " +
                    "and you have a point designated, this adds pitch to bring the rockets onto the " +
                    "range you need, so lining up a long shot is a matter of pointing at the target " +
                    "rather than finding the loft by hand. Your own stick input is added on top and " +
                    "is never capped, so you can overpower it at any moment, and it does nothing at " +
                    "all with no designation. OFF by default because it changes how the aircraft " +
                    "flies.",
                    null,
                    new ConfigurationManagerAttributes { Order = 42 }));

            ReleaseAssistKey = Config.Bind(
                "Assist", "ReleaseAssistKey", KeyCode.U,
                new ConfigDescription(
                    "Turns the release assist on and off in flight, so a shot that wants taking by " +
                    "hand does not need a trip to the settings. Unlike the tilt assist this one " +
                    "starts every sortie ON, matching its setting - it only ever delays a shot you " +
                    "aimed at something you designated, so having it on is the safe state. The HUD " +
                    "says AUTO while it is armed.",
                    null,
                    new ConfigurationManagerAttributes { Order = 44 }));

            TiltAssistKey = Config.Bind(
                "Assist", "TiltAssistKey", KeyCode.Y,
                new ConfigDescription(
                    "Arms and disarms the tilt assist in flight. It starts every sortie disarmed, " +
                    "because something that moves the aircraft should be switched on deliberately " +
                    "rather than found already running. The HUD shows TILT ARMED when it is on and " +
                    "TILT when it is actually flying the shot.",
                    null,
                    new ConfigurationManagerAttributes { Order = 41 }));

            TiltAssistAuthority = Config.Bind(
                "Assist", "Tilt assist authority", 0.30f,
                new ConfigDescription(
                    "How much of the pitch axis the tilt assist is allowed to use, as a fraction. " +
                    "0.30 is just under a third of full deflection, which settles a long shot in a " +
                    "couple of seconds and is still far too little to fight you for the aircraft - " +
                    "your own stick is added on top and is never limited. It was 0.10 and was too " +
                    "weak to be useful. Raise it if it still settles too slowly for you.",
                    new AcceptableValueRange<float>(0f, 1f),
                    new ConfigurationManagerAttributes { Order = 41 }));

            FunEffectsEnabled = Config.Bind(
                "Effects", "Fun effects", false,
                new ConfigDescription(
                    "Play the AGR-31's own authored effects: a cyan motor plume, an ignition " +
                    "ember burst, a nozzle light, a smoke trail and a flash at the tube. It is a " +
                    "deliberately gamey look and it is not what the weapon is supposed to be, " +
                    "which is why it ships OFF - off borrows the game's own rocket effects. The " +
                    "sound is the same either way. Takes effect on the next round fired.",
                    null,
                    new ConfigurationManagerAttributes { Order = 100 }));

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
            enc = GameData.EncyclopediaOrNull();
            if (enc == null) return;

            try
            {
                EncyclopediaRegistration.EnsureRegisteredAndRebuild();

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
