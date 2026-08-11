using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace RocketPod
{

    [HarmonyPatch(typeof(UnitMapIcon), "SetIcon")]
    internal static class UnitMapIcon_SetIcon_RoundIconPatch
    {
        private static Sprite? _sprite;
        private static bool _logged;

        private const int SurveyLines = 12;

        private static int _surveyed;

        private const float IconSize = 0.6f;

        [HarmonyPostfix]
        private static void Postfix(UnitMapIcon __instance, Unit unit)
        {
            try
            {
                if (unit == null || unit.definition == null) return;

                if (_surveyed < SurveyLines)
                {
                    _surveyed++;
                    Plugin.Log.LogInfo(
                        $"[Tenpin] Map icon survey {_surveyed}/{SurveyLines}: " +
                        $"unit '{unit.unitName}', definition '{unit.definition.unitName}', " +
                        $"sprite '{unit.definition.mapIcon?.name ?? "NULL"}', " +
                        $"mapIconSize {unit.definition.mapIconSize:0.##}, " +
                        $"ours={(unit.definition.unitName == PluginInfo.MissileUnitName)}");
                }

                if (unit.definition.unitName != PluginInfo.MissileUnitName) return;

                Sprite dart = RoundSprite();

                float hadSize = unit.definition.mapIconSize;

                if (!_logged)
                {
                    _logged = true;
                    Sprite? had = unit.definition.mapIcon;
                    Plugin.Log.LogInfo(
                        $"[Tenpin] Round map icon: the definition had " +
                        (had == null ? "NO sprite, which Unity draws as a filled white square"
                                     : $"sprite '{had.name}'") +
                        $" at mapIconSize {unit.definition.mapIconSize:0.##}. Replaced with a " +
                        $"generated dart at {IconSize}. Logged once.");
                }

                unit.definition.mapIcon = dart;
                unit.definition.mapIconSize = IconSize;

                if (__instance.iconImage != null)
                {
                    __instance.iconImage.sprite = dart;
                    __instance.iconImage.transform.localScale =
                        IconScale(__instance.iconImage.transform.localScale, hadSize);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[Tenpin] Map icon patch failed: {ex.Message}");
            }
        }

        private static Vector3 IconScale(Vector3 current, float hadSize)
        {
            if (hadSize <= 0.001f) return current;
            return current * (IconSize / hadSize);
        }

        private static Sprite RoundSprite()
        {
            if (_sprite != null) return _sprite;

            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[size * size];
            const float halfWidth = 6f;
            const float noseY = 27f;
            const float tailY = 5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f - size * 0.5f;
                    float py = y + 0.5f;

                    float t = Mathf.InverseLerp(noseY, tailY, py);
                    float allowed = halfWidth * t;

                    float alpha = 0f;
                    if (py <= noseY && py >= tailY)
                    {

                        alpha = Mathf.Clamp01(allowed - Mathf.Abs(px) + 1f);
                    }

                    pixels[y * size + x] =
                        new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false);

            _sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
            return _sprite;
        }
    }
}
