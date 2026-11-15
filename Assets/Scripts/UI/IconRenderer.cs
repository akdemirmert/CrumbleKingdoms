using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Renders the real 3D models to sprites once (weapon icons, class portraits) and caches them for the
// whole game. A temporary orthographic camera looks at a temporary copy of the model far below the
// world (IconStage layer); everything temporary is removed in the same frame.
// Returns null when rendering is impossible; UIKit then draws a coloured circle with an initial.
public static class IconRenderer {
    const int Size = 256;
    static readonly Vector3 Stage = new Vector3(0f, -500f, 0f);
    static readonly Dictionary<WeaponId, Sprite> weapons = new Dictionary<WeaponId, Sprite>();
    static readonly Dictionary<HeroClass, Sprite> portraits = new Dictionary<HeroClass, Sprite>();
    static readonly HashSet<string> failed = new HashSet<string>();

    public static Sprite Weapon(WeaponId id) {
        if (weapons.TryGetValue(id, out Sprite s) && s != null) return s;
        WeaponDef def = WeaponDefs.Get(id);
        s = Render("weapon " + id, holder => {
            WeaponFactory.CreateModel(id, holder);
            holder.localRotation = Quaternion.Euler(12f, -28f, 38f);   // pointing up-right, slightly turned: reads as 3D
        }, UIKit.SlotColor, def.color, false);
        if (s != null) weapons[id] = s;
        return s;
    }

    public static Sprite Portrait(HeroClass c) {
        if (portraits.TryGetValue(c, out Sprite s) && s != null) return s;
        Color color = ClassDefs.Get(c).color;
        s = Render("portrait " + c, holder => {
            CommanderModel.Build(c, color, 1, holder);
            holder.localRotation = Quaternion.Euler(0f, -18f, 0f);     // three-quarter view
        }, Color.Lerp(UIKit.SlotColor, color, 0.3f), color, true);
        if (s != null) portraits[c] = s;
        return s;
    }

    static Sprite Render(string key, Action<Transform> build, Color background, Color glow, bool portrait) {
        if (failed.Contains(key)) return null;
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { failed.Add(key); return null; }
        GameObject stage = null, camGo = null;
        RenderTexture rt = null;
        try {
            stage = new GameObject("[IconStage] " + key);
            stage.transform.position = Stage;
            Transform holder = new GameObject("Holder").transform;
            holder.SetParent(stage.transform, false);
            build(holder);

            Bounds b = ModelBounds(stage);
            holder.position += Stage - b.center;     // centre the model on the stage
            b.center = Stage;
            Frame(b, portrait, out Vector3 look, out float halfSize);

            // Soft coloured halo behind the model.
            GameObject halo = ModelKit.Part(stage.transform, PrimitiveType.Quad, Vector3.zero, Vector3.one * halfSize * 2.3f,
                                            Mats.UnlitTransparent(UIKit.Alpha(glow, 0.5f), TexKit.SoftCircle));
            halo.transform.position = new Vector3(look.x, look.y, b.max.z + 0.3f);

            AddLight(stage.transform, new Vector3(0.45f, -0.55f, 0.7f), new Color(1f, 0.96f, 0.9f), 1.35f);
            AddLight(stage.transform, new Vector3(-0.35f, -0.25f, -0.9f), UIKit.Lighten(glow, 0.4f), 0.7f);   // rim from behind
            ModelKit.SetLayer(stage, Layers.IconStage);
            ModelKit.Shadows(stage, false, false);

            camGo = new GameObject("[IconCamera]") { layer = Layers.IconStage };
            camGo.transform.SetPositionAndRotation(new Vector3(look.x, look.y, b.min.z - 10f), Quaternion.identity);
            Camera cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.orthographic = true;
            cam.orthographicSize = halfSize;
            cam.aspect = 1f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.cullingMask = 1 << Layers.IconStage;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = b.size.z + 30f;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;

            rt = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32);
            UniversalRenderPipeline.SingleCameraRequest request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request)) {
                RenderPipeline.SubmitRenderRequest(cam, request);
            } else {
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
            }
            return ReadBack(rt, key, portrait);
        } catch (Exception e) {
            Debug.LogWarning("[Siege] icon render failed (" + key + "): " + e.Message);
            failed.Add(key);
            return null;
        } finally {
            Discard(stage);
            Discard(camGo);
            if (rt != null) RenderTexture.ReleaseTemporary(rt);
        }
    }

    // Bounds of the visible meshes only (trails/particles have no useful bounds yet and are hidden).
    static Bounds ModelBounds(GameObject root) {
        bool any = false;
        Bounds b = new Bounds(root.transform.position, Vector3.zero);
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true)) {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) { r.enabled = false; continue; }
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        if (!any || b.size.sqrMagnitude < 1e-6f) throw new InvalidOperationException("model has no visible mesh");
        return b;
    }

    // Where the camera looks and the orthographic half-size. Weapons: whole model with room for the halo.
    // Portraits: head to knees of the commander.
    static void Frame(Bounds b, bool portrait, out Vector3 look, out float halfSize) {
        if (!portrait) {
            look = b.center;
            halfSize = Mathf.Max(b.extents.x, b.extents.y) * 1.22f;
            return;
        }
        float frameH = b.size.y * 0.78f;
        look = new Vector3(b.center.x, b.max.y - frameH * 0.5f + b.size.y * 0.03f, b.center.z);
        halfSize = Mathf.Max(frameH * 0.5f, b.extents.x) * 1.1f;
    }

    static void AddLight(Transform parent, Vector3 direction, Color color, float intensity) {
        GameObject go = new GameObject("IconLight");
        go.transform.SetParent(parent, false);
        go.transform.rotation = Quaternion.LookRotation(direction.normalized);
        Light l = go.AddComponent<Light>();
        l.type = LightType.Directional;
        l.color = color;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
        l.cullingMask = 1 << Layers.IconStage;
    }

    // Copies the render into a texture, rounds it (circle for portraits) and adds a soft vignette.
    static Sprite ReadBack(RenderTexture rt, string key, bool circle) {
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true) {
            name = "Icon " + key, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear,
        };
        tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0, false);
        RenderTexture.active = prev;

        Color32[] px = tex.GetPixels32();
        float half = Size * 0.5f, corner = Size * 0.2f;
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++) {
                float dx = x + 0.5f - half, dy = y + 0.5f - half;
                float d;
                if (circle) d = Mathf.Sqrt(dx * dx + dy * dy) - (half - 1f);
                else {
                    float qx = Mathf.Abs(dx) - (half - 1f) + corner, qy = Mathf.Abs(dy) - (half - 1f) + corner;
                    d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - corner;
                }
                float shade = 1f - 0.3f * (dx * dx + dy * dy) / (half * half * 2f);
                int i = y * Size + x;
                Color32 c = px[i];
                px[i] = new Color32((byte)(c.r * shade), (byte)(c.g * shade), (byte)(c.b * shade), (byte)(Mathf.Clamp01(0.5f - d) * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(true);
        return TexKit.ToSprite(tex);
    }

    static void Discard(GameObject go) {
        if (go == null) return;
        go.SetActive(false);   // invisible immediately, so the next icon rendered this frame does not see it
        Object.Destroy(go);
    }
}
