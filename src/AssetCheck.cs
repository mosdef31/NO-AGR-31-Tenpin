using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Shared.Ballistics;

namespace RocketPod
{

    internal static class AssetCheck
    {
        private const BindingFlags Inst =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private static bool _ran;

        internal static void RunOnce()
        {
            if (_ran) return;
            _ran = true;

            var ok = new List<string>();
            var fail = new List<string>();

            try
            {
                Audit(ok, fail);
            }
            catch (Exception ex)
            {
                fail.Add($"the check itself threw, which is a bug in AssetCheck rather than in the bundle: {ex}");
            }

            foreach (string s in ok) Plugin.Log.LogInfo($"[Tenpin] OK: {s}");
            foreach (string s in fail) Plugin.Log.LogError($"[Tenpin] FAIL: {s}");

            Plugin.Log.LogInfo(
                fail.Count == 0
                    ? $"[Tenpin] Asset check passed, {ok.Count} checks."
                    : $"[Tenpin] Asset check found {fail.Count} problem(s), to fix in Unity.");
        }

        private const string BaseMap = "_BaseMap";

        private static void CheckShaders(Renderer[] renderers, List<string> ok, List<string> fail)
        {
            var bad = new List<string>();
            var good = new List<string>();

            foreach (Renderer r in renderers)
            {
                foreach (Material? m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    Shader s = m.shader;

                    if (s == null)
                    {
                        bad.Add($"'{m.name}' has no shader at all");
                    }
                    else if (!s.isSupported)
                    {
                        bad.Add($"'{m.name}' uses '{s.name}', which this build reports as unsupported");
                    }
                    else if (s.name == "Standard" ||
                             s.name.StartsWith("Legacy Shaders/", StringComparison.Ordinal) ||
                             s.name.IndexOf("InternalErrorShader", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        bad.Add($"'{m.name}' uses the built-in '{s.name}' shader");
                    }
                    else
                    {
                        good.Add($"'{m.name}'/{s.name}");

                        if (!(r is ParticleSystemRenderer) &&
                            m.HasProperty(BaseMap) && m.GetTexture(BaseMap) == null)
                        {
                            bad.Add($"'{m.name}' is on '{s.name}' but has NO base map bound, " +
                                    "so it renders as flat white");
                        }
                    }
                }
            }

            if (bad.Count > 0)
            {
                fail.Add($"{string.Join("; ", bad.ToArray())}. This game renders with URP, which has " +
                         "no pass for a built-in shader, so the mesh is not drawn at all - the pod " +
                         "is invisible while everything else about it works. Set the material's " +
                         "shader to 'Universal Render Pipeline/Lit' in Unity and re-export. If the " +
                         "complaint is a MISSING BASE MAP rather than a shader, the material reached " +
                         "the bundle without its textures bound - see the body-strip note in the " +
                         "Unity project's TenpinEffects.cs.");
            }
            else if (good.Count > 0)
            {
                ok.Add($"materials on a supported shader: {string.Join(", ", good.ToArray())}.");
            }
        }

        private static void CheckMotors(Missile missile, List<string> ok, List<string> fail)
        {
            var fMotors = typeof(Missile).GetField("motors", Inst);
            if (fMotors?.GetValue(missile) is not Array motors || motors.Length == 0)
            {
                fail.Add("the rocket prefab has no motor stages, so it can only ever coast.");
                return;
            }

            for (int i = 0; i < motors.Length; i++)
            {
                object? stage = motors.GetValue(i);
                if (stage == null) { fail.Add($"motor stage {i} is null."); continue; }

                float thrust = MotorFloat(stage, "thrust");
                float burnTime = MotorFloat(stage, "burnTime");
                float fuelMass = MotorFloat(stage, "fuelMass");
                float topSpeed = MotorFloat(stage, "topSpeed");

                var bad = new List<string>();
                if (thrust <= 0f) bad.Add("thrust is 0");
                if (burnTime <= 0f) bad.Add("burnTime is 0");
                if (fuelMass <= 0f) bad.Add("fuelMass is 0");
                if (topSpeed <= 0f)
                    bad.Add("topSpeed is 0, which gates the thrust off entirely - " +
                            "Motor.Thrust only pushes while speed < topSpeed, so it never pushes. " +
                            "Set it to the stock placeholder 299792450 to mean 'no cap'");

                if (bad.Count > 0)
                    fail.Add($"motor stage {i}: {string.Join("; ", bad.ToArray())}. The round will " +
                             "leave the tube on its ejection velocity and then free-fall.");
                else
                {

                    float mass = typeof(Missile).GetField("mass", Inst)?.GetValue(missile) is float m && m > 0f
                        ? m
                        : 0f;
                    string accel = mass > 0f ? $", {thrust / mass:F0} m/s^2 at ignition" : "";
                    ok.Add($"motor stage {i}: {thrust:F0} N for {burnTime:F2} s on {fuelMass:F1} kg " +
                           $"of fuel{accel}.");
                }
            }
        }

        private static void CheckFlightMass(Missile missile, Rigidbody? rb, WeaponInfo? info,
                                            List<string> ok, List<string> fail)
        {
            if (typeof(Missile).GetField("mass", Inst)?.GetValue(missile) is not float mass)
            {
                fail.Add("Missile.mass could not be read, so the round's flight mass is unknown. " +
                         "Re-check the decompile against this game build.");
                return;
            }

            if (mass <= 0f)
            {
                fail.Add($"Missile.mass is {mass:0.##} on the rocket prefab. StartMissile copies it " +
                         "over the Rigidbody's mass on every launch, so the round would fly at that " +
                         "mass whatever the Rigidbody says.");
                return;
            }

            if (info != null && info.massPerRound > 0f &&
                Mathf.Abs(info.massPerRound - mass) > 0.01f)
            {
                fail.Add($"the round FLIES at {mass:0.##} kg (Missile.mass) but is CARRIED as " +
                         $"{info.massPerRound:0.##} kg (WeaponInfo.massPerRound). StartMissile does " +
                         "`rb.mass = mass`, so Missile.mass is the only one the flight model sees " +
                         "and massPerRound is the only one the loadout sees. Every range figure in " +
                         "the mod is solved for the carriage mass, so while these disagree the " +
                         "round does not fly the trajectory anything predicts for it. Set " +
                         $"Missile.mass to {info.massPerRound:0.##} on the rocket prefab and " +
                         "re-export the bundle.");
            }
            else if (rb != null && Mathf.Abs(rb.mass - mass) > 0.01f)
            {

                ok.Add($"round flies at {mass:0.##} kg (Missile.mass); the Rigidbody's " +
                       $"{rb.mass:0.##} kg is overwritten by StartMissile and does not matter.");
            }
            else
            {
                ok.Add($"round flies at {mass:0.##} kg, which is what it is carried as.");
            }
        }

        private static float MotorFloat(object stage, string field)
        {
            FieldInfo? f = stage.GetType().GetField(field, Inst);
            return f?.GetValue(stage) is float v ? v : 0f;
        }

        private static void CheckFlightModel(Missile missile, List<string> ok, List<string> fail)
        {
            var fDrag = typeof(Missile).GetField("dragCurve", Inst);
            var fLift = typeof(Missile).GetField("liftCurve", Inst);
            var fTorque = typeof(Missile).GetField("torque", Inst);
            var fSupersonic = typeof(Missile).GetField("supersonicDrag", Inst);
            var fFinArea = typeof(Missile).GetField("finArea", Inst);

            var drag = fDrag?.GetValue(missile) as AnimationCurve;
            var lift = fLift?.GetValue(missile) as AnimationCurve;

            float cd0 = drag != null && drag.length > 0 ? drag.Evaluate(0f) : 0f;
            float supersonic = fSupersonic != null ? (float)fSupersonic.GetValue(missile) : 0f;
            float finArea = fFinArea != null ? (float)fFinArea.GetValue(missile) : 0f;

            if (drag == null || drag.length == 0)
            {
                fail.Add("dragCurve is EMPTY on the rocket prefab, so dragCurve.Evaluate() returns " +
                         "0 at every angle and the round flies with NO DRAG AT ALL. " +
                         $"supersonicDrag ({supersonic:0.###}) cannot compensate: it MULTIPLIES the " +
                         "drag vector, and a multiple of zero is zero. Author the curve in Unity.");
            }
            else if (cd0 <= 0f)
            {
                fail.Add($"dragCurve evaluates to {cd0:0.####} at zero angle of attack, so the round " +
                         "has effectively no drag through the part of the flight that matters. " +
                         "The stock MLRS rocket reads 0.1 here.");
            }
            else
            {
                ok.Add($"dragCurve is authored, Cd0 {cd0:0.###} over {drag.length} key(s), " +
                       $"finArea {finArea:0.###} m2, supersonicDrag {supersonic:0.###}.");
            }

            if (lift == null || lift.length == 0)
                fail.Add("liftCurve is EMPTY on the rocket prefab, so the round generates no lift " +
                         "at any angle of attack and cannot manoeuvre aerodynamically.");
            else
                ok.Add($"liftCurve is authored over {lift.length} key(s).");

            float torque = fTorque != null ? (float)fTorque.GetValue(missile) : 0f;
            if (torque <= 0f)
                fail.Add("torque is 0 on the rocket prefab. ApplyAero ends in " +
                         "`AddRelativeTorque(inputs * torque)`, so the seeker's steering output is " +
                         "multiplied to nothing and the round CANNOT STEER. The INS guidance, its " +
                         "CEP dispersion, and this mod's powered aimpoint are all inert until this " +
                         "is non-zero.");
            else
                ok.Add($"torque is {torque:0.###}, so the seeker can actually steer the round.");

            var pidFactors = typeof(Missile).GetField("PIDFactors", Inst)?.GetValue(missile);
            if (pidFactors != null)
            {
                var fPID = pidFactors.GetType().GetField("PID", Inst);
                if (fPID?.GetValue(pidFactors) is Vector3 pid)
                {
                    if (pid == Vector3.zero)
                        fail.Add("PIDFactors.PID is (0,0,0) on the rocket prefab, so the steering " +
                                 "PID outputs zero regardless of error and the round cannot steer " +
                                 "even with a non-zero torque. Both must be set.");
                    else
                        ok.Add($"PIDFactors.PID is ({pid.x:0.##}, {pid.y:0.##}, {pid.z:0.##}).");
                }
            }
        }

        private static void CheckSeekerTargetlessSafety(
            MissileSeeker seeker, List<string> ok, List<string> fail)
        {
            var bad = new List<string>();
            foreach (string flag in new[] { "useDatalink", "useLaser" })
            {
                FieldInfo? f = seeker.GetType().GetField(flag, Inst);
                if (f?.GetValue(seeker) is bool on && on) bad.Add(flag);
            }

            if (bad.Count > 0)
                fail.Add($"the seeker has {string.Join(" and ", bad.ToArray())} enabled. This round " +
                         "flies with no locked target, so targetUnit is null, and both flags reach " +
                         "FactionHQ methods that dereference it on their first instruction. The " +
                         "throw happens inside Seek(), which runs before ApplyAero and " +
                         "DetectCollisions, so the round gets no drag and no impact detection and " +
                         "passes through terrain. useDatalink defaults to TRUE - turn both off.");
            else
                ok.Add("seeker has datalink and laser off, so a null target cannot throw in Seek().");
        }

        private static void Audit(List<string> ok, List<string> fail)
        {
            IList<WeaponMount> mounts = EncyclopediaRegistration.ResolvedMounts;
            if (mounts == null || mounts.Count == 0)
            {
                fail.Add($"no WeaponMount resolved from the bundle. Expected an asset whose path " +
                         $"contains '{PluginInfo.MountAssetFragment}'. Everything below is unchecked.");
                return;
            }

            foreach (WeaponMount mount in mounts)
            {
                if (mount == null) continue;
                AuditMount(mount, ok, fail);
            }

            CheckSwapPatchRegistered(ok, fail);
        }

        private static void CheckSwapPatchRegistered(List<string> ok, List<string> fail)
        {
            MethodBase? target = AccessTools.Method(typeof(MissileLauncher), "OnEnable");
            if (target == null)
            {
                fail.Add("MissileLauncher.OnEnable could not be resolved, so the launcher swap has " +
                         "nothing to hook. The game's MissileLauncher has changed shape and " +
                         "LauncherSwap will never run - every pod stays on the stock component, " +
                         "which fires for the HOST ONLY.");
                return;
            }

            bool patchedAtAll = false;
            foreach (MethodBase m in Harmony.GetAllPatchedMethods())
            {
                if (m != target) continue;
                patchedAtAll = true;
                break;
            }

            bool ours = false;
            if (patchedAtAll)
            {
                Patches? info = Harmony.GetPatchInfo(target);
                if (info?.Postfixes != null)
                {
                    foreach (Patch patch in info.Postfixes)
                    {
                        if (patch.PatchMethod?.DeclaringType
                            != typeof(MissileLauncher_OnEnable_SwapPatch)) continue;
                        ours = true;
                        break;
                    }
                }
            }

            if (!ours)
            {
                fail.Add("MissileLauncher_OnEnable_SwapPatch is NOT registered with Harmony. The " +
                         "[HarmonyPatch] attribute alone does nothing - Plugin.Awake must call " +
                         "_harmony.PatchAll(typeof(MissileLauncher_OnEnable_SwapPatch)). Without " +
                         "it TenpinLauncher is never attached, every pod keeps the stock " +
                         "MissileLauncher, and that component gates its spawn on owner.LocalSim - " +
                         "so the pod fires for the HOST ONLY and says nothing about it. This mod " +
                         "has shipped an unregistered patch class twice; this check is why the " +
                         "third time is loud.");
                return;
            }

            ok.Add("MissileLauncher_OnEnable_SwapPatch is registered on MissileLauncher.OnEnable, " +
                   "so each pod instance will be swapped to TenpinLauncher as it comes up.");
        }

        private static void AuditMount(WeaponMount mount, List<string> ok, List<string> fail)
        {
            ok.Add($"WeaponMount resolved, jsonKey '{mount.jsonKey}'.");

            int expected = PluginInfo.RoundsFor(mount.jsonKey);
            if (expected < 0)
                fail.Add($"WeaponMount.jsonKey is '{mount.jsonKey}' but the DLL expects " +
                         $"{PluginInfo.MountKeyList}. Lookups key off this " +
                         "exactly, and the round-count check below cannot run without it.");

            if (mount.info == null)
            {
                fail.Add("WeaponMount.info is null. Weapon.GetMass() dereferences info.massPerRound, " +
                         "so this is a NullReferenceException waiting at load.");
            }
            else
            {
                ok.Add($"WeaponInfo present, weaponName '{mount.info.weaponName}', " +
                       $"massPerRound {mount.info.massPerRound} kg.");
                if (!PluginInfo.IsOurWeaponName(mount.info.weaponName))
                    fail.Add($"WeaponInfo.weaponName is '{mount.info.weaponName}' but the DLL expects " +
                             $"'{PluginInfo.WeaponInfoName}'. The loadout list reads this field.");

                PluginInfo.WeaponSpec? wspec = PluginInfo.WeaponForName(mount.info.weaponName);
                string wantShort = wspec?.ShortName ?? PluginInfo.ShortName;
                if (mount.info.shortName != wantShort)
                    fail.Add($"WeaponInfo.shortName is '{mount.info.shortName}' but this weapon's " +
                             $"is '{wantShort}'. WeaponIndicator draws this string next to the " +
                             "weapon icon in flight, so the pod flies under the wrong name while " +
                             "every other field reads correctly.");
                else
                    ok.Add($"WeaponInfo.shortName is '{mount.info.shortName}', which is what the " +
                           "in-flight indicator draws.");

                if (mount.info.weaponIcon == null)
                    fail.Add("WeaponInfo.weaponIcon is null, so the in-flight indicator draws no " +
                             "icon for this pod.");
                else
                    ok.Add($"WeaponInfo.weaponIcon is '{mount.info.weaponIcon.name}'. Compare it " +
                           "against the other pods' in this same log - a shared icon here is the " +
                           "AGR-51 flying under the AGR-31's identity, which nothing else catches.");

                if (mount.info.gun)
                    fail.Add("WeaponInfo.gun is true. Both stock AGRs are gun=false missile-type " +
                             "stores; on the gun path the rocket model is never seen in flight.");
                if (!mount.info.missile)
                    fail.Add("WeaponInfo.missile is false. The HUD picks its UI off these flags.");
            }

            if (mount.prefab == null)
            {
                fail.Add("WeaponMount.prefab is null - nothing to hang on the pylon.");
                return;
            }

            var launcher = mount.prefab.GetComponentInChildren<MissileLauncher>();
            if (launcher == null)
            {
                fail.Add("the mounted prefab has no MissileLauncher. Hardpoint.SpawnMount registers " +
                         "weapons via GetComponentsInChildren<Weapon>(), so without one the pod is " +
                         "scenery that cannot be selected or fired.");
                return;
            }
            ok.Add("mounted prefab carries a MissileLauncher.");

            foreach ((string what, bool present) in new[]
            {
                ("Rigidbody", mount.prefab.GetComponentInChildren<Rigidbody>() != null),
                ("Missile", mount.prefab.GetComponentInChildren<Missile>() != null),
            })
            {
                if (present)
                    fail.Add($"the mounted prefab carries a {what}. It is parented to the hardpoint " +
                             "and must never fly - that component belongs on the rocket prefab only.");
            }
            if (mount.prefab.GetComponentInChildren<Rigidbody>() == null &&
                mount.prefab.GetComponentInChildren<Missile>() == null)
            {
                ok.Add("mounted prefab has no Rigidbody or Missile, so it will stay on the pylon.");
            }

            var renderers = mount.prefab.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                fail.Add("the mounted prefab has no Renderer anywhere in it, so the pod is " +
                         "invisible on the pylon. The mesh child was not carried into the prefab.");
            }
            else
            {
                int drawable = renderers.Count(r =>
                    r.enabled && r.gameObject.activeSelf && r.sharedMaterial != null);
                if (drawable == 0)
                    fail.Add($"the mounted prefab has {renderers.Length} Renderer(s) but none is " +
                             "enabled, active and carrying a material, so nothing is drawn. " +
                             "DumpPrefabRenderers says which of the three it is.");
                else
                    ok.Add($"mounted prefab has {drawable} drawable renderer(s).");

                CheckShaders(renderers, ok, fail);
            }

            Vector3 rootPos = mount.prefab.transform.localPosition;
            if (Mathf.Abs(rootPos.x) > 0.001f || Mathf.Abs(rootPos.z) > 0.001f)
                fail.Add($"the mounted prefab's ROOT sits at {rootPos}. Instantiate keeps the " +
                         "prefab's local transform, so a non-zero X or Z hangs the pod that far " +
                         "outboard or forward of EVERY pylon. Only Y is meant to be non-zero - " +
                         "that is the drop below the pylon.");
            else
                ok.Add($"mounted prefab root is on the pylon axis, hanging {rootPos.y:F2} m below it.");

            if (string.IsNullOrWhiteSpace(mount.mountName))
                fail.Add("WeaponMount.mountName is blank, so the loadout entry has no label. " +
                         "Initialize() only regenerates it for a MountedMissile or a gun pod.");
            else
                ok.Add($"mountName '{mount.mountName}'.");

            if (mount.info != null)
            {
                float expectedMass = mount.emptyMass + mount.info.massPerRound * mount.ammo;
                if (Math.Abs(mount.mass - expectedMass) > 0.001f)
                    fail.Add($"WeaponMount.mass is {mount.mass:F1} kg, expected {expectedMass:F1} " +
                             $"(emptyMass {mount.emptyMass:F1} + {mount.ammo} x " +
                             $"{mount.info.massPerRound:F1}). It is [HideInInspector] and meant to " +
                             "be derived, so a wrong value means the derivation did not run.");
                else
                    ok.Add($"mount mass {mount.mass:F1} kg loaded, {mount.emptyMass:F1} kg empty.");
            }

            if (mount.info != null && Plugin.StoreCardFields.Value)
            {
                if (mount.info.weaponPrefab == null)
                    fail.Add("WeaponInfo.weaponPrefab is null, so the loadout store card shows " +
                             "no seeker type, no AP, no HE, no RCS and no price. StoreCard.Apply " +
                             "should have set it from MissileDefinition.unitPrefab.");
                else if (mount.info.costPerRound <= 0f)
                    fail.Add("WeaponInfo.costPerRound is zero, so the pod is free in the loadout " +
                             "screen and contributes nothing to the aircraft's value.");
                else
                    ok.Add($"store card wired - weaponPrefab '{mount.info.weaponPrefab.name}', " +
                           $"${mount.info.costPerRound * 1000f:0.#}k a round.");
            }

            if (expected > 0)
            {
                if (launcher.ammo != expected)
                    fail.Add($"MissileLauncher.ammo is {launcher.ammo}, expected {expected}. " +
                             "This is the live round count.");
                if (mount.ammo != expected)
                    fail.Add($"WeaponMount.ammo is {mount.ammo}, expected {expected}. This is what " +
                             "the loadout charges for and what rearming checks against; nothing copies it " +
                             "to or from MissileLauncher.ammo.");
                if (launcher.ammo == expected && mount.ammo == expected)
                    ok.Add($"round count is {expected} on both WeaponMount and MissileLauncher.");
            }

            if (launcher.info == null)
                fail.Add("MissileLauncher.info is null. Weapon.GetMass() reads info.massPerRound.");

            MissileDefinition? def = launcher.missile;
            if (def == null)
            {
                fail.Add("MissileLauncher.missile is null - the pod has nothing to fire.");
            }
            else
            {
                ok.Add($"MissileLauncher.missile is '{def.unitName}' (jsonKey '{def.jsonKey}').");
                if (!PluginInfo.IsOurRound(def.jsonKey))
                    fail.Add($"MissileDefinition.jsonKey is '{def.jsonKey}' but the DLL expects " +
                             $"'{PluginInfo.MissileKey}'.");

                GameObject? flying = def.unitPrefab;
                if (flying == null)
                {
                    fail.Add("MissileDefinition.unitPrefab is null - nothing spawns at launch.");
                }
                else
                {

                    GameObject? byInfo = launcher.info != null
                        ? launcher.info.weaponPrefab : null;
                    if (byInfo != null && !ReferenceEquals(byInfo, flying))

                        fail.Add($"MissileDefinition.unitPrefab is '{flying.name}' but this " +
                                 $"weapon's WeaponInfo names '{byInfo.name}' as its round. The " +
                                 "engine spawns what the DEFINITION names, so this pod fires " +
                                 "another weapon's rocket - which looks like a modelling mistake " +
                                 "in the air and logs nothing. Every check below this line has " +
                                 $"just been run against '{flying.name}', not against ours. " +
                                 $"MissileLauncher.info is '{launcher.info?.name ?? "null"}' " +
                                 $"(weaponName '{launcher.info?.weaponName ?? "null"}'), " +
                                 $"WeaponMount.info is '{mount.info?.name ?? "null"}' " +
                                 $"(weaponName '{mount.info?.weaponName ?? "null"}'), and they are " +
                                 $"{(ReferenceEquals(launcher.info, mount.info) ? "the SAME object" : "DIFFERENT objects")}.");
                    else if (byInfo != null)
                        ok.Add($"the definition and the WeaponInfo agree the round is '{flying.name}'.");

                    var missile = flying.GetComponent<Missile>();
                    if (missile == null)
                        fail.Add("the rocket prefab has no Missile component.");
                    else
                        ok.Add("rocket prefab has a Missile component.");

                    var rb = flying.GetComponent<Rigidbody>();
                    if (rb == null)
                    {
                        fail.Add("the rocket prefab has no Rigidbody - it will not fly.");
                    }
                    else if (rb.interpolation == RigidbodyInterpolation.None)
                    {

                        fail.Add("the rocket prefab's Rigidbody has interpolation set to None, so " +
                                 "it is drawn only at fixed-step physics positions and visibly " +
                                 "ticks between frames - obvious in slow motion. Set it to " +
                                 "Interpolate, which is what the ashm-m3 missile uses.");
                    }
                    else
                    {
                        ok.Add($"rocket Rigidbody interpolation is {rb.interpolation}, so it draws " +
                               "smoothly between physics steps.");
                    }

                    if (missile != null)
                    {
                        if (missile.definition == null)
                            fail.Add("the rocket prefab's Missile.definition is null. Unit.Awake reads " +
                                     "definition.radarSize during Instantiate and Spawner.SpawnMissile " +
                                     "reads definition.unitName, so launching throws before " +
                                     "ServerObjectManager.Spawn is reached: the round ejects on its " +
                                     "ejection velocity and then free-falls, motorless. Point it at " +
                                     $"'{PluginInfo.MissileKey}'.");
                        else if (!ReferenceEquals(missile.definition, def))
                            fail.Add($"the rocket prefab's Missile.definition is " +
                                     $"'{missile.definition.name}' but the pod fires '{def.name}'. " +
                                     "These must be the same asset - the pair is a mutual reference.");
                        else
                            ok.Add("rocket prefab's Missile.definition points back at the pod's own " +
                                   "MissileDefinition.");

                        CheckMotors(missile, ok, fail);
                        CheckFlightModel(missile, ok, fail);
                        CheckFlightMass(missile, rb, mount.info, ok, fail);
                    }

                    var seeker = flying.GetComponentInChildren<MissileSeeker>();
                    if (seeker == null)
                    {
                        fail.Add("the rocket prefab has no MissileSeeker. Missile.LocalStart calls " +
                                 "seeker.Initialize() with no null check, so launching throws. Use " +
                                 "InertialSeekerShell - its serialized CEP is the dispersion this " +
                                 "weapon is built around.");
                    }
                    else
                    {

                        FieldInfo? fMissile = typeof(MissileSeeker).GetField("missile", Inst);
                        var wired = fMissile?.GetValue(seeker) as Missile;
                        if (wired == null)
                            fail.Add($"the seeker's 'missile' field is not wired. It is a " +
                                     "SerializeField that nothing assigns at runtime, so " +
                                     "Initialize() throws immediately and Seek() throws every tick. " +
                                     "Seek runs before ApplyAero and DetectCollisions in " +
                                     "ServerFixedUpdate, which has no try/catch, so the round flies " +
                                     "with no drag and no collision detection - through terrain, " +
                                     "forever. Drag the Missile component onto it.");
                        else if (!ReferenceEquals(wired, missile))
                            fail.Add("the seeker's 'missile' field points at a different Missile " +
                                     "than the one on this prefab.");
                        else
                            ok.Add($"seeker is {seeker.GetType().Name} and its 'missile' field is wired.");

                        CheckSeekerTargetlessSafety(seeker, ok, fail);
                    }

                    if (missile != null && missile.GetWeaponInfo() == null)
                        fail.Add("the rocket prefab's Missile.info is null. GetWeaponInfo() returns " +
                                 "it and the HUD and AI read it; point it at the WeaponInfo.");
                }
            }

            var fTransforms = typeof(MissileLauncher).GetField("launchTransforms", Inst);
            if (fTransforms?.GetValue(launcher) is Transform[] tubes)
            {
                if (tubes.Length == 0)
                    fail.Add($"MissileLauncher.launchTransforms is empty, so all {launcher.ammo} " +
                             "rockets leave from the prefab origin rather than from the tube mouths.");
                else if (expected > 0 && tubes.Length != expected)
                    fail.Add($"MissileLauncher.launchTransforms has {tubes.Length} entries but the pod " +
                             $"has {expected} tubes. Fire() cycles them, so the count must match.");
                else
                    ok.Add($"{tubes.Length} launch transforms, one per tube.");

                for (int i = 0; i < tubes.Length; i++)
                {
                    if (tubes[i] == null)
                        fail.Add($"launchTransforms[{i}] is null - Fire() will throw on that tube.");
                }
            }
            else
            {
                fail.Add("could not read MissileLauncher.launchTransforms by reflection. The field may " +
                         "have been renamed in this game build - re-check the decompile.");
            }

            var fInterval = typeof(MissileLauncher).GetField("fireInterval", Inst);
            if (fInterval?.GetValue(launcher) is float interval)
            {
                if (interval <= 0f)
                {
                    fail.Add("fireInterval is 0, so the whole pod empties in one frame rather than " +
                             "rippling. The real T-107 fires 12 rounds in 7 to 9 s.");
                }

                else if (mount.info != null &&
                         Math.Abs(mount.info.fireInterval - interval) > 0.001f)
                {
                    fail.Add($"MissileLauncher.fireInterval is {interval:F2} s but " +
                             $"WeaponInfo.fireInterval is {mount.info.fireInterval:F2} s. " +
                             "WeaponStation.Ready() gates on the WeaponInfo one and " +
                             "MissileLauncher.Fire on its own, so the slower silently wins. " +
                             "Set both.");
                }
                else
                {
                    ok.Add($"fireInterval {interval:F2} s on both the launcher and the WeaponInfo " +
                           $"({launcher.ammo} rounds in {interval * Math.Max(0, launcher.ammo - 1):F1} s " +
                           "from one pod).");
                }
            }
        }
    }
}
