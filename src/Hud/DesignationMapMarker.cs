using UnityEngine;
using UnityEngine.UI;

namespace RocketPod.Hud
{

    internal sealed class DesignationMapMarker
    {

        private const float MarkerPixels = 30f;

        private const float MapPixelSpan = 900f;

        private GameObject? _go;
        private RectTransform? _rect;
        private Image? _image;

        private static Sprite? _sprite;

        internal void Show(Vector3 world)
        {
            if (!Plugin.HudMapMarker.Value) { Hide(); return; }

            DynamicMap? map = SceneSingleton<DynamicMap>.i;
            if (map == null || map.mapImage == null || map.iconLayer == null) { Hide(); return; }

            if (!Ensure(map)) return;
            Refresh(map, world);
        }

        internal void Hide()
        {
            if (_go != null && _go.activeSelf) _go.SetActive(false);
        }

        internal void Destroy()
        {
            if (_go != null) Object.Destroy(_go);
            _go = null;
            _rect = null;
            _image = null;
        }

        private bool Ensure(DynamicMap map)
        {

            if (_go != null && _rect != null && _image != null &&
                _rect.parent == map.iconLayer.transform)
            {
                return true;
            }

            Destroy();

            _go = new GameObject("TenpinDesignationMarker");
            _rect = _go.AddComponent<RectTransform>();
            _rect.SetParent(map.iconLayer.transform, worldPositionStays: false);
            _rect.sizeDelta = new Vector2(MarkerPixels, MarkerPixels);
            _rect.anchorMin = _rect.anchorMax = new Vector2(0.5f, 0.5f);
            _rect.pivot = new Vector2(0.5f, 0.5f);

            _image = _go.AddComponent<Image>();
            _image.sprite = MarkerSprite();

            _image.raycastTarget = false;

            return true;
        }

        private void Refresh(DynamicMap map, Vector3 world)
        {
            if (_rect == null || _image == null || _go == null) return;

            Transform img = map.mapImage.transform;
            float dimension = map.mapDimension;
            if (dimension <= 0f) { Hide(); return; }

            float metresToPixels = MapPixelSpan * img.lossyScale.x / dimension;
            Vector3 origin = img.position;
            var screen = new Vector3(
                origin.x + world.x * metresToPixels,
                origin.y + world.z * metresToPixels,
                origin.z);

            if (map.mapBackground != null &&
                !RectTransformUtility.RectangleContainsScreenPoint(
                    map.mapBackground.rectTransform, screen, null))
            {
                Hide();
                return;
            }

            _rect.position = screen;

            float inverse = img.localScale.x != 0f ? 1f / img.localScale.x : 1f;
            _rect.localScale = Vector3.one * inverse;

            _image.color = new Color(
                PlayerSettings.hudColorR / 255f,
                PlayerSettings.hudColorG / 255f,
                PlayerSettings.hudColorB / 255f,
                1f);

            if (!_go.activeSelf) _go.SetActive(true);
        }

        private static Sprite MarkerSprite()
        {
            if (_sprite != null) return _sprite;

            const int size = 64;
            const float half = size / 2f;
            const float outer = 30f;
            const float thickness = 3f;
            const float dot = 3.5f;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - half);
                    float dy = Mathf.Abs(y + 0.5f - half);

                    float l1 = dx + dy;
                    float ring = Mathf.Abs(l1 - outer);

                    float alpha = 0f;
                    if (ring <= thickness) alpha = 1f - Mathf.Clamp01((ring - (thickness - 1f)));
                    float centre = Mathf.Sqrt(dx * dx + dy * dy);
                    if (centre <= dot) alpha = Mathf.Max(alpha, 1f - Mathf.Clamp01(centre - (dot - 1f)));

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false);

            _sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
            return _sprite;
        }
    }
}
