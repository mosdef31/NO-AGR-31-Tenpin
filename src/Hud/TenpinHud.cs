using System;
using System.Collections.Generic;
using RocketPod.Ballistics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RocketPod.Hud
{

    public enum HudAnchor
    {

        RightBelowWeapons,

        TopRight,

        LowerRight,

        TopLeft,

        LowerLeft,
    }

    internal enum HudMode
    {

        Direct,

        Artillery,
    }

    internal sealed class TenpinHud : MonoBehaviour
    {
        private const float ReferenceHeight = 1080f;

        private Canvas? _canvas;
        private HudVectorGraphic? _vec;
        private readonly List<TextMeshProUGUI> _texts = new List<TextMeshProUGUI>();
        private TMP_FontAsset? _font;
        private bool _fontSearched;

        private HudMode _mode = HudMode.Direct;

        private Vector3 _impactSmoothed;
        private bool _hasImpact;

        private TrajectorySolver.RoundSpec? _spec;
        private Missile? _specSource;

        private Vector3 _groundDesignation;
        private bool _hasGroundDesignation;
        private float _designationRadius;

        private float _maxRange;
        private float _nextMaxRange;

        private static bool _loggedFontMissing;

        private static readonly System.Reflection.FieldInfo? _fEjection =
            HarmonyLib.AccessTools.Field(typeof(MissileLauncher), "ejectionVelocity");

        private void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneChanged;
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnSceneChanged;
        }

        private void OnSceneChanged(UnityEngine.SceneManagement.Scene from,
                                    UnityEngine.SceneManagement.Scene to)
        {
            ClearDesignation("scene changed");
            _hasImpact = false;
            _spec = null;
            _specSource = null;
            _maxRange = 0f;
            Hide();
        }

        private static bool InPlay()
        {
            try
            {
                if (GameplayUI.GameIsPaused) return false;

                GameState state = GameManager.gameState;
                if (state != GameState.SinglePlayer && state != GameState.Multiplayer &&
                    state != GameState.Editor)
                    return false;

                GameplayUI? ui = SceneSingleton<GameplayUI>.i;
                if (ui != null && ui.gameplayCanvas != null && !ui.gameplayCanvas.enabled)
                    return false;
            }
            catch
            {

                return false;
            }
            return true;
        }

        private void Update()
        {
            try
            {
                if (!InPlay())
                {

                    Hide();
                    return;
                }

                if (Input.GetKeyDown(Plugin.HudModeKey.Value)) ToggleMode();
                HandleDesignateInput();
                Draw();
            }
            catch (Exception ex)
            {
                Hide();
                Plugin.Log.LogError($"[Tenpin] HUD failed and was hidden this frame: {ex}");
            }
        }

        private void ToggleMode()
        {

            if (!OurWeaponSelected(out _, out _)) return;

            _mode = _mode == HudMode.Direct ? HudMode.Artillery : HudMode.Direct;
            _hasImpact = false;
            Plugin.Log.LogInfo($"[Tenpin] HUD mode: {_mode}");
        }

        private void Draw()
        {
            if (!OurWeaponSelected(out Aircraft aircraft, out WeaponStation station))
            {
                Hide();
                return;
            }

            Camera cam = Camera.main;
            if (cam == null) { Hide(); return; }

            EnsureCanvas();
            if (_vec == null) return;

            TrajectorySolver.RoundSpec? spec = ResolveSpec(station);
            if (spec == null) { Hide(); return; }

            Vector3 launch = aircraft.transform.GlobalPosition().AsVector3();
            Vector3 velocity = aircraft.rb.velocity + aircraft.transform.forward * EjectionSpeed(station);

            float step = Mathf.Max(1f, Plugin.HudStepScale.Value);
            TrajectorySolver.Result r = TrajectorySolver.Integrate(
                spec, launch, velocity, groundY: 0f, wind: default, stepScale: step);

            if (r.Hit && Plugin.SampleTerrainHeight.Value)
            {
                float groundY = AimpointChannel.SampleGroundHeightPublic(r.ImpactPoint);
                if (groundY > 0.5f)
                {
                    TrajectorySolver.Result onTerrain = TrajectorySolver.Integrate(
                        spec, launch, velocity, groundY: groundY, wind: default, stepScale: step);
                    if (onTerrain.Hit) r = onTerrain;
                }
            }

            if (!r.Hit) { Hide(); return; }

            float launchSpeed = aircraft.rb.velocity.magnitude;
            float lerpRate = LowSpeedLaunch.PipperLerpRate(launchSpeed);

            Vector3 target = r.ImpactPoint + aircraft.rb.velocity * 0.3f;
            _impactSmoothed = _hasImpact
                ? Vector3.Lerp(_impactSmoothed, target, lerpRate * Time.deltaTime)
                : target;
            _hasImpact = true;

            Vector3 world = new GlobalPosition(_impactSmoothed.x, _impactSmoothed.y, _impactSmoothed.z)
                .ToLocalPosition();

            Vector3 toPoint = (world - cam.transform.position).normalized;
            Vector3 screen = cam.WorldToScreenPoint(world);
            bool impactVisible =
                Vector3.Dot(toPoint, cam.transform.forward) >= 0.5f && screen.z > 0f;

            if (_mode == HudMode.Direct && !impactVisible) { Hide(); return; }

            Color hud = HudColor();
            float px = Screen.height / ReferenceHeight;

            _vec.enabled = true;
            _vec.Begin();

            float slant = (world - cam.transform.position).magnitude;
            float cep = EffectiveCep(slant);
            var pip = new Vector2(screen.x, screen.y);

            if (_mode == HudMode.Direct)
                DrawDirect(cam, pip, world, cep, hud, px, r, station, aircraft, launch, velocity, spec);
            else
                DrawArtillery(cam, pip, impactVisible, cep, hud, px, r, station, aircraft, launch, velocity, spec);

            _vec.End();
        }

        private void DrawDirect(Camera cam, Vector2 pip, Vector3 world, float cep,
                                Color hud, float px, TrajectorySolver.Result r,
                                WeaponStation station, Aircraft aircraft,
                                Vector3 launch, Vector3 velocity,
                                TrajectorySolver.RoundSpec spec)
        {
            if (_vec == null) return;

            Color cue = hud;
            bool designated = TryGetDesignation(aircraft, station, out Vector3 dtarget);
            bool onTarget = false;
            bool inReach = true;
            float missDist = 0f;

            if (designated)
            {
                float groundY = Plugin.SampleTerrainHeight.Value
                    ? Mathf.Max(0f, AimpointChannel.SampleGroundHeightPublic(dtarget))
                    : 0f;
                dtarget.y = groundY;

                Vector3 miss = r.ImpactPoint - dtarget;
                miss.y = 0f;
                missDist = miss.magnitude;

                Vector3 flat = dtarget - launch;
                flat.y = 0f;

                float maxRange = MaxRangeThrottled(spec, launch, aircraft, velocity);
                inReach = maxRange <= 0f || flat.magnitude <= maxRange;
                onTarget = inReach && missDist <= ReleaseTolerance(cep, _designationRadius);

                cue = onTarget ? FireCue(hud) : inReach ? hud : Warn(hud);
            }

            const int segments = 32;
            var ring = new Vector2[segments];
            bool ringOk = true;
            for (int i = 0; i < segments; i++)
            {
                float t = i / (float)segments * Mathf.PI * 2f;
                Vector3 p = world + new Vector3(Mathf.Cos(t), 0f, Mathf.Sin(t)) * cep;
                Vector3 s = cam.WorldToScreenPoint(p);
                if (s.z <= 0f) { ringOk = false; break; }
                ring[i] = new Vector2(s.x, s.y);
            }
            if (ringOk) _vec.Polyline(ring, 1.6f * px, Fade(cue, 0.45f), closed: true);

            Vector2? fpm = FlightPathMarkerScreen(cam);
            if (fpm.HasValue)
            {
                Vector2 d = fpm.Value - pip;
                if (d.magnitude > 34f * px)
                {
                    Vector2 from = pip + d.normalized * (16f * px);
                    Vector2 to = fpm.Value - d.normalized * (12f * px);
                    _vec.Line(from, to, 1.4f * px, Fade(hud, 0.7f));
                }
            }

            float rad = 5.5f * px;
            _vec.Circle(pip, rad, 2.1f * px, cue);
            _vec.Line(pip + new Vector2(0f, rad * 1.55f), pip + new Vector2(0f, rad * 2.4f), 2f * px, cue);
            _vec.Line(pip - new Vector2(0f, rad * 1.55f), pip - new Vector2(0f, rad * 2.4f), 2f * px, cue);
            _vec.Line(pip + new Vector2(rad * 1.55f, 0f), pip + new Vector2(rad * 2.4f, 0f), 2f * px, cue);
            _vec.Line(pip - new Vector2(rad * 1.55f, 0f), pip - new Vector2(rad * 2.4f, 0f), 2f * px, cue);

            if (designated && TryProject(cam, dtarget, out Vector2 dscreen))
            {
                float d = 7f * px;
                var diamond = new[]
                {
                    dscreen + new Vector2(0f, d), dscreen + new Vector2(d, 0f),
                    dscreen - new Vector2(0f, d), dscreen - new Vector2(d, 0f),
                };
                _vec.Polyline(diamond, 1.8f * px, cue, closed: true);
            }

            int i2 = 0;
            Vector2 org = PanelOrigin(px);
            SetText(ref i2, pip + new Vector2(20f * px, -14f * px), $"ToF {r.TimeOfFlight:0.0}", cue, px);
            SetText(ref i2, pip + new Vector2(20f * px, -30f * px), $"CEP {cep:0}", Fade(hud, 0.75f), px);
            SetText(ref i2, org, "AGR-31 CCIP", hud, px);
            SetText(ref i2, org - new Vector2(0f, 18f * px),

                    $"RDY {station.Ammo}  RPL {station.FullAmmo}", Fade(hud, 0.75f), px);

            if (designated)
            {
                SetText(ref i2, org - new Vector2(0f, 36f * px),
                        $"MISS {missDist:0}", Fade(hud, 0.6f), px);

                if (!inReach)
                    SetText(ref i2, Centre(px, 0f), "OUT OF REACH", Warn(hud), px);
                else if (onTarget)
                    SetText(ref i2, Centre(px, 0f), "FIRE", FireCue(hud), px);
            }

            HideTextsFrom(i2);
        }

        private void DrawArtillery(Camera cam, Vector2 pip, bool impactVisible, float cep,
                                   Color hud, float px, TrajectorySolver.Result r,
                                   WeaponStation station, Aircraft aircraft,
                                   Vector3 launch, Vector3 velocity,
                                   TrajectorySolver.RoundSpec spec)
        {
            if (_vec == null) return;

            int i2 = 0;
            Vector2 org = PanelOrigin(px);
            float left = org.x;
            float top = org.y;
            SetText(ref i2, new Vector2(left, top), "AGR-31 CCRP", hud, px);
            SetText(ref i2, new Vector2(left, top - 18f * px),

                    $"RDY {station.Ammo}  RPL {station.FullAmmo}", Fade(hud, 0.75f), px);

            float maxRange = MaxRangeThrottled(spec, launch, aircraft, velocity);

            bool designated = TryGetDesignation(aircraft, station, out Vector3 target);

            if (designated)
            {
                float groundY = Plugin.SampleTerrainHeight.Value
                    ? Mathf.Max(0f, AimpointChannel.SampleGroundHeightPublic(target))
                    : 0f;
                target.y = groundY;

                Vector3 flat = target - launch;
                flat.y = 0f;
                float rangeToTarget = flat.magnitude;

                Vector3 miss = r.ImpactPoint - target;
                miss.y = 0f;
                float missDist = miss.magnitude;
                float tolerance = ReleaseTolerance(cep, _designationRadius);
                bool inReach = maxRange > 0f && rangeToTarget <= maxRange;
                bool onTarget = inReach && missDist <= tolerance;

                Color cue = onTarget ? FireCue(hud) : hud;

                bool toScale = FootprintRing(cam, target, cep, Fade(cue, 0.85f), 1.7f * px, out float ringScale);
                FootprintRing(cam, target, cep * 2.08f * ringScale, Fade(cue, 0.40f), 1.3f * px, out _);

                bool haveTarget = TryProject(cam, target, out Vector2 tgt);
                if (haveTarget)
                {
                    float d = 7f * px;
                    var diamond = new[]
                    {
                        tgt + new Vector2(0f, d), tgt + new Vector2(d, 0f),
                        tgt - new Vector2(0f, d), tgt - new Vector2(d, 0f),
                    };
                    _vec.Polyline(diamond, 1.8f * px, cue, closed: true);
                }

                if (impactVisible)
                {
                    ImpactCross(pip, cue, px, onTarget);

                    if (haveTarget && (pip - tgt).magnitude > 26f * px)
                        DottedLine(pip, tgt, 1.4f * px, Fade(cue, 0.85f), 5f * px, 6f * px);
                }

                SetText(ref i2, new Vector2(left, top - 36f * px),
                        $"RNG {rangeToTarget / 1000f:0.0}  MAX {maxRange / 1000f:0.0}",
                        inReach ? Fade(hud, 0.75f) : Warn(hud), px);

                Vector3 bearing = flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.forward;
                float alongError = Vector3.Dot(miss, bearing);
                float crossError = Vector3.Dot(miss, Vector3.Cross(Vector3.up, bearing));

                SetText(ref i2, new Vector2(left, top - 54f * px),
                        $"{(alongError >= 0f ? "LONG" : "SHORT")} {Mathf.Abs(alongError):0} " +
                        $"{(crossError >= 0f ? "R" : "L")} {Mathf.Abs(crossError):0}",
                        Fade(hud, 0.75f), px);

                SetText(ref i2, new Vector2(left, top - 72f * px),
                        $"CEP {cep:0}{(toScale ? "" : " *")}", Fade(hud, 0.6f), px);

                if (!inReach)
                    SetText(ref i2, Centre(px, 0f), "OUT OF REACH", Warn(hud), px);
                else if (onTarget)
                    SetText(ref i2, Centre(px, 0f), "FIRE", FireCue(hud), px);

                Magnifier(ref i2, cam, target, cep, missDist, tolerance, cue, hud, px);
            }
            else
            {
                SetText(ref i2, new Vector2(left, top - 36f * px),
                        $"MAX {maxRange / 1000f:0.0}", Fade(hud, 0.75f), px);
                SetText(ref i2, new Vector2(left, top - 54f * px),
                        $"NO DESIGNATION - LOCK A UNIT OR {Plugin.HudDesignateKey.Value} ON THE MAP",
                        Fade(hud, 0.6f), px);
            }

            if (Plugin.HudMaxRangeArc.Value && maxRange > 0f)
                ReachArc(cam, launch, aircraft.transform.forward, maxRange, Fade(hud, 0.35f), 1.2f * px);

            HideTextsFrom(i2);
        }

        private bool TryGetDesignation(Aircraft aircraft, WeaponStation station, out Vector3 target)
        {
            target = default;
            _designationRadius = 0f;

            Unit? locked = SelectedTarget(aircraft, station);
            if (locked != null)
            {
                target = locked.transform.GlobalPosition().AsVector3();

                _designationRadius = Mathf.Max(0f, locked.maxRadius);
                return true;
            }

            if (_hasGroundDesignation)
            {
                target = _groundDesignation;
                return true;
            }
            return false;
        }

        internal void ClearDesignation(string why)
        {
            if (!_hasGroundDesignation) return;
            _hasGroundDesignation = false;
            _designationRadius = 0f;
            Plugin.Log.LogInfo($"[Tenpin] Ground designation cleared ({why}).");
        }

        private static Unit? SelectedTarget(Aircraft aircraft, WeaponStation station)
        {
            try
            {
                List<Unit>? list = aircraft.weaponManager?.GetTargetList();
                if (list != null)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        Unit u = list[i];
                        if (u != null && !u.disabled) return u;
                    }
                }
            }
            catch {  }

            try { return station.GetStationTarget(); }
            catch { return null; }
        }

        private void HandleDesignateInput()
        {
            if (!OurWeaponSelected(out _, out _)) return;

            if (ClearPressed())
            {
                if (_hasGroundDesignation)
                {
                    _hasGroundDesignation = false;
                    Plugin.Log.LogInfo("[Tenpin] Ground designation cleared.");
                }
                return;
            }

            if (!Input.GetKeyDown(Plugin.HudDesignateKey.Value)) return;

            DynamicMap? map = SceneSingleton<DynamicMap>.i;
            if (map == null) return;
            if (!DynamicMap.mapMaximized) return;
            if (!map.IsCursorInMapRectangle()) return;
            if (!map.TryGetCursorCoordinates(out GlobalPosition p)) return;

            _groundDesignation = p.AsVector3();
            _hasGroundDesignation = true;
            Plugin.Log.LogInfo(
                $"[Tenpin] Ground designation set at {_groundDesignation.x:0}, {_groundDesignation.z:0}.");
        }

        private static bool ClearPressed()
        {
            try
            {
                Rewired.Player? input = GameManager.playerInput;
                if (input == null) return false;
                return input.GetButtonTimedPressUp("Cancel", 0f, PlayerSettings.clickDelay)
                    || input.GetButtonTimedPressDown("Cancel", PlayerSettings.pressDelay);
            }
            catch
            {

                return false;
            }
        }

        private void Magnifier(ref int index, Camera cam, Vector3 target, float cep,
                               float missDist, float tolerance, Color cue, Color hud, float px)
        {
            if (_vec == null || !Plugin.HudMagnifier.Value) return;

            float trigger = tolerance * Mathf.Max(1f, Plugin.HudMagnifierTriggerFactor.Value);
            if (missDist > trigger) return;

            if (!TryProject(cam, target, out Vector2 tgt)) return;

            Vector3 impactGlobal = _impactSmoothed;
            if (!TryProject(cam, impactGlobal, out Vector2 imp)) return;

            float size = Mathf.Max(60f, Plugin.HudMagnifierPixels.Value) * px;
            float half = size * 0.5f;

            var centre = MagnifierCentre(px, half);

            if (!TryProject(cam, target + new Vector3(cep * 2.08f, 0f, 0f), out Vector2 edge)) return;
            float outerPx = (edge - tgt).magnitude;
            if (outerPx < 1e-3f) return;

            float zoom = Mathf.Clamp(half * 0.62f / outerPx, 1f, 400f);

            Vector2 Map(Vector2 p) => centre + (p - tgt) * zoom;

            float t = 10f * px;
            Color frame = Fade(hud, 0.5f);
            foreach (var (cx, cy) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
            {
                var corner = centre + new Vector2(cx * half, cy * half);
                _vec.Line(corner, corner - new Vector2(cx * t, 0f), 1.4f * px, frame);
                _vec.Line(corner, corner - new Vector2(0f, cy * t), 1.4f * px, frame);
            }

            MappedGroundRing(cam, target, cep, Map, Fade(cue, 0.9f), 1.8f * px, half, centre);
            MappedGroundRing(cam, target, cep * 2.08f, Map, Fade(cue, 0.45f), 1.4f * px, half, centre);

            float d = 6f * px;
            _vec.Polyline(new[]
            {
                centre + new Vector2(0f, d), centre + new Vector2(d, 0f),
                centre - new Vector2(0f, d), centre - new Vector2(d, 0f),
            }, 1.8f * px, cue, closed: true);

            Vector2 mapped = Map(imp);
            Vector2 clamped = new Vector2(
                Mathf.Clamp(mapped.x, centre.x - half, centre.x + half),
                Mathf.Clamp(mapped.y, centre.y - half, centre.y + half));
            float a = 7f * px;
            _vec.Line(clamped + new Vector2(-a, 0f), clamped + new Vector2(a, 0f), 1.9f * px, cue);
            _vec.Line(clamped + new Vector2(0f, -a), clamped + new Vector2(0f, a), 1.9f * px, cue);

            SetText(ref index, new Vector2(centre.x - half, centre.y - half - 16f * px),
                    $"x{zoom:0}  CEP {cep:0}", Fade(hud, 0.6f), px);
        }

        private void MappedGroundRing(Camera cam, Vector3 centre, float radius,
                                      Func<Vector2, Vector2> map, Color color, float width,
                                      float half, Vector2 panelCentre)
        {
            if (_vec == null || radius <= 0f) return;

            const int segments = 40;
            var run = new List<Vector2>();
            for (int i = 0; i <= segments; i++)
            {
                float a = (i % segments) / (float)segments * Mathf.PI * 2f;
                Vector3 p = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;

                bool ok = TryProject(cam, p, out Vector2 s);
                Vector2 m = ok ? map(s) : default;
                if (ok && Mathf.Abs(m.x - panelCentre.x) <= half && Mathf.Abs(m.y - panelCentre.y) <= half)
                {
                    run.Add(m);
                    continue;
                }
                if (run.Count > 1) _vec.Polyline(run.ToArray(), width, color);
                run.Clear();
            }
            if (run.Count > 1) _vec.Polyline(run.ToArray(), width, color);
        }

        private void DottedLine(Vector2 a, Vector2 b, float width, Color color,
                                float dash, float gap)
        {
            if (_vec == null) return;

            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 1e-3f) return;

            Vector2 dir = d / len;
            float stride = Mathf.Max(1f, dash + gap);
            for (float s = 0f; s < len; s += stride)
            {
                float e = Mathf.Min(s + dash, len);
                _vec.Line(a + dir * s, a + dir * e, width, color);
            }
        }

        private void GroundRing(Camera cam, Vector3 centre, float radius, Color color, float width)
        {
            if (_vec == null || radius <= 0f) return;

            const int segments = 40;
            var pts = new Vector2[segments];
            for (int i = 0; i < segments; i++)
            {
                float t = i / (float)segments * Mathf.PI * 2f;
                Vector3 p = centre + new Vector3(Mathf.Cos(t), 0f, Mathf.Sin(t)) * radius;
                if (!TryProject(cam, p, out pts[i])) return;
            }
            _vec.Polyline(pts, width, color, closed: true);
        }

        private bool FootprintRing(Camera cam, Vector3 centre, float radius, Color color,
                                   float width, out float scale)
        {
            scale = 1f;
            if (_vec == null || radius <= 0f) return true;

            if (!TryProject(cam, centre, out Vector2 mid)) return true;
            if (!TryProject(cam, centre + new Vector3(radius, 0f, 0f), out Vector2 edge))
                return true;

            float minPx = Mathf.Max(0f, Plugin.HudFootprintMinPixels.Value) *
                          (Screen.height / ReferenceHeight);
            float actual = (edge - mid).magnitude;

            bool toScale = true;
            if (minPx > 0f && actual > 0.01f && actual < minPx)
            {
                scale = minPx / actual;
                radius *= scale;
                toScale = false;
            }

            GroundRing(cam, centre, radius, color, width);
            return toScale;
        }

        private void DottedPolyline(IList<Vector2> pts, float width, Color color,
                                    float dash, float gap)
        {
            if (_vec == null || pts.Count < 2) return;

            float stride = Mathf.Max(1f, dash + gap);
            float phase = 0f;

            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector2 a = pts[i];
                Vector2 b = pts[i + 1];
                float len = (b - a).magnitude;
                if (len < 1e-3f) continue;

                Vector2 dir = (b - a) / len;
                float s = 0f;
                while (s < len)
                {
                    float cycle = phase % stride;
                    if (cycle < dash)
                    {
                        float run = Mathf.Min(dash - cycle, len - s);
                        _vec.Line(a + dir * s, a + dir * (s + run), width, color);
                        s += run; phase += run;
                    }
                    else
                    {
                        float skip = Mathf.Min(stride - cycle, len - s);
                        s += skip; phase += skip;
                    }
                }
            }
        }

        private void ReachArc(Camera cam, Vector3 from, Vector3 heading, float radius,
                              Color color, float width)
        {
            if (_vec == null) return;

            Vector3 flat = new Vector3(heading.x, 0f, heading.z);
            if (flat.sqrMagnitude < 1e-6f) return;
            flat.Normalize();

            const int steps = 24;

            float halfSweep = Mathf.Clamp(Plugin.HudReachArcDegrees.Value, 2f, 90f);

            var run = new List<Vector2>();
            for (int i = 0; i <= steps; i++)
            {
                float deg = -halfSweep + (2f * halfSweep) * (i / (float)steps);
                Vector3 dir = Quaternion.AngleAxis(deg, Vector3.up) * flat;
                Vector3 p = from + dir * radius;
                p.y = 0f;

                if (TryProject(cam, p, out Vector2 s))
                {
                    run.Add(s);
                    continue;
                }
                if (run.Count > 1) DottedPolyline(run, width, color, 9f, 7f);
                run.Clear();
            }
            if (run.Count > 1) DottedPolyline(run, width, color, 9f, 7f);
        }

        private void ImpactCross(Vector2 at, Color hud, float px, bool onTarget)
        {
            if (_vec == null) return;

            float gap = 5f * px;
            float arm = 14f * px;
            float w = 2.1f * px;

            _vec.Line(at + new Vector2(gap, 0f), at + new Vector2(arm, 0f), w, hud);
            _vec.Line(at - new Vector2(gap, 0f), at - new Vector2(arm, 0f), w, hud);
            _vec.Line(at + new Vector2(0f, gap), at + new Vector2(0f, arm), w, hud);
            _vec.Line(at - new Vector2(0f, gap), at - new Vector2(0f, arm), w, hud);
            if (!onTarget) return;

            float b = 11f * px;
            var box = new[]
            {
                at + new Vector2(-b, -b), at + new Vector2(b, -b),
                at + new Vector2(b, b), at + new Vector2(-b, b),
            };
            _vec.Polyline(box, w, hud, closed: true);
        }

        private static bool TryProject(Camera cam, Vector3 global, out Vector2 screen)
        {
            screen = default;
            Vector3 local = new GlobalPosition(global.x, global.y, global.z).ToLocalPosition();
            Vector3 s = cam.WorldToScreenPoint(local);
            if (s.z <= 0f) return false;
            screen = new Vector2(s.x, s.y);
            return true;
        }

        private float MaxRangeThrottled(TrajectorySolver.RoundSpec spec, Vector3 launch,
                                        Aircraft aircraft, Vector3 velocity)
        {
            if (Time.unscaledTime < _nextMaxRange && _maxRange > 0f) return _maxRange;
            _nextMaxRange = Time.unscaledTime + 0.5f;

            try
            {
                _maxRange = TrajectorySolver.MaxRange(
                    spec, launch, aircraft.transform.forward, velocity.magnitude,
                    groundY: 0f, wind: default, stepDegrees: 10f);
            }
            catch (Exception ex)
            {
                _maxRange = 0f;
                Plugin.Log.LogError($"[Tenpin] Max-range sweep failed: {ex.Message}");
            }
            return _maxRange;
        }

        private static float ReleaseTolerance(float cep, float targetRadius = 0f)
        {
            float configured = Plugin.HudReleaseToleranceMeters.Value;
            float accuracy = configured > 0f ? configured : cep;
            if (!Plugin.HudTargetSizeTolerance.Value) return accuracy;
            return accuracy + Mathf.Max(0f, targetRadius);
        }

        private static Vector2 Centre(float px, float dy)
            => new Vector2(Screen.width * 0.5f - 70f * px, Screen.height * 0.5f + dy - 90f * px);

        private const float BlockWidth = 330f;

        private static Vector2 PanelOrigin(float px)
        {
            float margin = 40f * px;
            float w = BlockWidth * px;
            float right = Screen.width - margin - w;
            float top = Screen.height - 60f * px;

            return Plugin.HudPanelAnchor.Value switch
            {

                HudAnchor.RightBelowWeapons => new Vector2(right, Screen.height * 0.72f),
                HudAnchor.TopRight          => new Vector2(right, top),
                HudAnchor.LowerRight        => new Vector2(right, Screen.height * 0.42f),
                HudAnchor.LowerLeft         => new Vector2(margin, Screen.height * 0.42f),
                _                           => new Vector2(margin, top),
            };
        }

        private static Vector2 MagnifierCentre(float px, float half)
        {
            Vector2 origin = PanelOrigin(px);
            float y = origin.y - (90f * px) - half;
            y = Mathf.Max(y, half + 20f * px);
            return new Vector2(origin.x + half, y);
        }

        private static Color Warn(Color hud)
            => Theme(t => t.Warning, Color.Lerp(hud, new Color(1f, 0.72f, 0.1f, 1f), 0.8f));

        private static Color FireCue(Color hud)
            => Theme(t => t.Alert, Color.Lerp(hud, new Color(1f, 0.2f, 0.12f, 1f), 0.85f));

        private static Color Theme(Func<NuclearOption.UIStyleSystem.ColorTheme, Color> pick,
                                   Color fallback)
        {
            try
            {
                Color c = pick(NuclearOption.UIStyleSystem.ThemeManager.Active.ColorTheme);
                return new Color(c.r, c.g, c.b, 1f);
            }
            catch
            {
                return fallback;
            }
        }

        private static float EffectiveCep(float slantRange)
        {
            float cep = slantRange * (Plugin.DispersionMilliradians.Value / 1000f);
            float floor = Plugin.MinDispersionMeters.Value;
            float ceiling = Plugin.MaxDispersionMeters.Value;
            if (floor > 0f) cep = Mathf.Max(cep, floor);
            if (ceiling > 0f) cep = Mathf.Min(cep, ceiling);
            return cep;
        }

        private static Color HudColor()
        {
            return new Color(
                PlayerSettings.hudColorR / 255f,
                PlayerSettings.hudColorG / 255f,
                PlayerSettings.hudColorB / 255f,
                1f);
        }

        private static Color Fade(Color c, float a) => new Color(c.r, c.g, c.b, a);

        private static Vector2? FlightPathMarkerScreen(Camera cam)
        {
            FlightHud? fh = SceneSingleton<FlightHud>.i;
            if (fh == null || fh.velocityVector == null) return null;
            Vector3 p = fh.velocityVector.transform.position;
            return new Vector2(p.x, p.y);
        }

        private static float EjectionSpeed(WeaponStation station)
        {

            try
            {
                if (_fEjection == null) return 0f;
                foreach (Weapon w in station.Weapons)
                {
                    if (w is MissileLauncher ml &&
                        _fEjection.GetValue(ml) is Vector3 v)
                    {
                        return v.z;
                    }
                }
            }
            catch {  }
            return 0f;
        }

        private bool OurWeaponSelected(out Aircraft aircraft, out WeaponStation station)
        {
            aircraft = null!;
            station = null!;

            if (!Plugin.HudEnabled.Value) return false;

            CombatHUD? hud = SceneSingleton<CombatHUD>.i;
            if (hud == null || hud.aircraft == null || hud.aircraft.disabled) return false;

            WeaponStation? ws = hud.GetWeaponStation();
            if (ws == null || ws.WeaponInfo == null) return false;
            if (ws.WeaponInfo.weaponName != PluginInfo.WeaponInfoName) return false;
            if (ws.Ammo <= 0) return false;

            if (Plugin.HudHideWithGear.Value && hud.aircraft.gearDeployed) return false;

            if (Plugin.HudCockpitOnly.Value &&
                CameraStateManager.cameraMode != CameraMode.cockpit) return false;

            aircraft = hud.aircraft;
            station = ws;
            return true;
        }

        private TrajectorySolver.RoundSpec? ResolveSpec(WeaponStation station)
        {
            Missile? prefab = EncyclopediaRegistration.ResolvedMissile?.unitPrefab != null
                ? EncyclopediaRegistration.ResolvedMissile.unitPrefab.GetComponent<Missile>()
                : null;
            if (prefab == null) return null;

            if (_spec == null || !ReferenceEquals(prefab, _specSource))
            {
                _spec = RoundSpecFactory.FromMissile(prefab, Plugin.Log);
                _specSource = prefab;
            }
            return _spec;
        }

        private void EnsureCanvas()
        {
            if (_canvas != null) return;

            var go = new GameObject("TenpinHud");
            go.transform.SetParent(transform, worldPositionStays: false);

            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            _canvas.sortingOrder = 50;

            var vecGo = new GameObject("Strokes");
            vecGo.transform.SetParent(go.transform, worldPositionStays: false);
            _vec = vecGo.AddComponent<HudVectorGraphic>();
            _vec.raycastTarget = false;
            Stretch(_vec.rectTransform);
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = Vector2.zero;
        }

        private void SetText(ref int index, Vector2 at, string content, Color color, float px)
        {
            TextMeshProUGUI? t = TextAt(index);
            if (t == null) { index++; return; }

            t.enabled = true;
            t.text = content;
            t.color = color;
            t.fontSize = 15f * px;
            t.rectTransform.anchoredPosition = at;
            index++;
        }

        private TextMeshProUGUI? TextAt(int index)
        {
            while (_texts.Count <= index)
            {
                TMP_FontAsset? font = Font();
                if (font == null) return null;

                var go = new GameObject($"Text{_texts.Count}");
                go.transform.SetParent(_canvas!.transform, worldPositionStays: false);
                var t = go.AddComponent<TextMeshProUGUI>();
                t.font = font;
                t.raycastTarget = false;
                t.alignment = TextAlignmentOptions.BottomLeft;
                t.enableWordWrapping = false;

                RectTransform rt = t.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.zero;
                rt.pivot = Vector2.zero;
                rt.sizeDelta = new Vector2(420f, 26f);

                _texts.Add(t);
            }
            return _texts[index];
        }

        private TMP_FontAsset? Font()
        {
            if (_font != null) return _font;

            var any = UnityEngine.Object.FindObjectOfType<TextMeshProUGUI>();
            if (any != null && any.font != null)
            {
                _font = any.font;
                return _font;
            }

            if (!_fontSearched && Time.timeSinceLevelLoad > 20f)
            {
                _fontSearched = true;
                if (!_loggedFontMissing)
                {
                    _loggedFontMissing = true;
                    Plugin.Log.LogWarning(
                        "[Tenpin] No TextMeshProUGUI found to borrow a font from, so the HUD's " +
                        "text readouts will not render. The vector cues are unaffected.");
                }
            }
            return null;
        }

        private void HideTextsFrom(int index)
        {
            for (int i = index; i < _texts.Count; i++)
            {
                if (_texts[i] != null) _texts[i].enabled = false;
            }
        }

        private void Hide()
        {
            _hasImpact = false;
            if (_vec != null)
            {
                _vec.Begin();
                _vec.End();
                _vec.enabled = false;
            }
            HideTextsFrom(0);
        }
    }
}
