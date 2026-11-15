using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using TMPro;

// Builds the project from code: settings, fonts, base materials, the GameAssets asset, both scenes
// and the Windows player. Everything else (meshes, textures, levels) is generated at runtime.
// Menu: Crumble Kingdoms > ...   Batch mode: -executeMethod SiegeBuilder.<Method>
public static class SiegeBuilder {
    const string ART = "Assets/Art";
    const string FNT = "Assets/Fonts";
    const string RES = "Assets/Resources";
    const string SCN = "Assets/Scenes";
    const string MenuScene = SCN + "/MainMenu.unity";
    const string BattleScene = SCN + "/Battle.unity";

    [MenuItem("Crumble Kingdoms/1. Set up project")]
    public static void SetupProject() {
        foreach (string d in new[] { ART, FNT, RES, SCN }) Directory.CreateDirectory(d);

        // Layers (numbers must match the Layers class)
        SerializedObject tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tm.FindProperty("layers");
        string[] names = { "Ground", "Block", "Projectile", "Debris", "Commander", "Effect", "IconStage" };
        for (int i = 0; i < names.Length; i++) layers.GetArrayElementAtIndex(8 + i).stringValue = names[i];
        tm.ApplyModifiedPropertiesWithoutUndo();

        PlayerSettings.companyName = "Mert";
        PlayerSettings.productName = "Crumble Kingdoms";
        PlayerSettings.bundleVersion = "1.0.0";
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.visibleInBackground = true;

        // Physics/time defaults (Boot.cs sets the same values at runtime)
        Physics.gravity = new Vector3(0f, -World.Gravity, 0f);
        Time.fixedDeltaTime = World.FixedStep;

        // URP quality: long shadow distance (the camera sits 30–100 m away), MSAA
        foreach (string p in new[] { "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset" }) {
            UniversalRenderPipelineAsset rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(p);
            if (rp == null) continue;
            rp.shadowDistance = 160f;
            rp.msaaSampleCount = 4;
            EditorUtility.SetDirty(rp);
        }

        MakeFont("Fredoka-500");
        MakeFont("Fredoka-700");
        AssetDatabase.SaveAssets();
        Debug.Log("[Siege] project set up");
    }

    static void MakeFont(string name) {
        string path = FNT + "/" + name + " SDF.asset";
        string chars = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
                       "·–—’‘“”…°×•←→↑↓▲▼★☆✓✗♥ÖÜÇŞĞİöüçşğıáéíóúÁÉÍÓÚàèñ";
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (existing != null) {   // add any characters that were added to the list since it was made
            existing.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            existing.TryAddCharacters(chars, out string _);
            existing.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(existing);
            return;
        }
        Font font = AssetDatabase.LoadAssetAtPath<Font>(FNT + "/" + name + ".ttf");
        TMP_FontAsset fa = TMP_FontAsset.CreateFontAsset(font, 72, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
        fa.name = name + " SDF";
        AssetDatabase.CreateAsset(fa, path);
        fa.atlasTextures[0].name = name + " Atlas";
        AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
        fa.material.name = name + " Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);
        fa.TryAddCharacters(chars, out string missing);
        if (!string.IsNullOrEmpty(missing)) Debug.Log("[Siege] " + name + " has no glyphs for: " + missing);
        fa.atlasPopulationMode = AtlasPopulationMode.Static;
        EditorUtility.SetDirty(fa);
    }

    // ------------------------------------------------------------------ assets
    [MenuItem("Crumble Kingdoms/2. Build assets")]
    public static void BuildAssets() {
        foreach (string d in new[] { ART, RES }) Directory.CreateDirectory(d);
        GameAssets ga = AssetDatabase.LoadAssetAtPath<GameAssets>(RES + "/GameAssets.asset");
        if (ga == null) {
            ga = ScriptableObject.CreateInstance<GameAssets>();
            AssetDatabase.CreateAsset(ga, RES + "/GameAssets.asset");
        }
        ga.fontBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FNT + "/Fredoka-700 SDF.asset");
        ga.fontMedium = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FNT + "/Fredoka-500 SDF.asset");
        ga.fontBoldOutline = TextMaterial(ga.fontBold, "Outline", new Color(0.08f, 0.06f, 0.1f), 0.22f, true);
        ga.fontMediumOutline = TextMaterial(ga.fontMedium, "Outline", new Color(0.06f, 0.06f, 0.1f), 0.15f, false);

        ga.litOpaque = Mat("Base_LitOpaque", "Universal Render Pipeline/Lit", false, 0, false, false);
        ga.litEmissive = Mat("Base_LitEmissive", "Universal Render Pipeline/Lit", false, 0, true, false);
        ga.litTransparent = Mat("Base_LitTransparent", "Universal Render Pipeline/Lit", true, 0, false, false);
        ga.unlitOpaque = Mat("Base_UnlitOpaque", "Universal Render Pipeline/Unlit", false, 0, false, false);
        ga.unlitTransparent = Mat("Base_UnlitTransparent", "Universal Render Pipeline/Unlit", true, 0, false, false);
        ga.particleAdditive = Mat("Base_ParticleAdditive", "Universal Render Pipeline/Particles/Unlit", true, 2, false, true);
        ga.particleAlpha = Mat("Base_ParticleAlpha", "Universal Render Pipeline/Particles/Unlit", true, 0, false, true);
        ga.postProfile = PostProfile();
        EditorUtility.SetDirty(ga);
        AssetDatabase.SaveAssets();
        Debug.Log("[Siege] assets built");
    }

    // blend: 0 alpha, 2 additive (URP BaseShaderGUI.BlendMode)
    static Material Mat(string name, string shader, bool transparent, int blend, bool emissive, bool particle) {
        string path = ART + "/" + name + ".mat";
        AssetDatabase.DeleteAsset(path);
        Material m = new Material(Shader.Find(shader)) { name = name };
        m.SetColor("_BaseColor", Color.white);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
        m.SetFloat("_Surface", transparent ? 1f : 0f);
        m.SetFloat("_Blend", blend);
        if (emissive) {
            m.SetColor("_EmissionColor", Color.white);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        if (particle) {
            m.SetFloat("_SoftParticlesEnabled", 0f);
            BaseShaderGUI.SetMaterialKeywords(m, null, ParticleGUI.SetMaterialKeywords);
        } else if (shader.EndsWith("/Lit")) {
            BaseShaderGUI.SetMaterialKeywords(m, LitGUI.SetMaterialKeywords);
        } else {
            BaseShaderGUI.SetMaterialKeywords(m);
        }
        if (emissive) m.EnableKeyword("_EMISSION");
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Material TextMaterial(TMP_FontAsset font, string name, Color outline, float width, bool shadow) {
        string path = FNT + "/" + font.name + " " + name + ".mat";
        AssetDatabase.DeleteAsset(path);
        Material m = new Material(font.material) { name = font.name + " " + name };
        m.EnableKeyword("OUTLINE_ON");
        m.SetFloat("_OutlineWidth", width);
        m.SetColor("_OutlineColor", outline);
        if (shadow) {
            m.EnableKeyword("UNDERLAY_ON");
            m.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.5f));
            m.SetFloat("_UnderlayOffsetX", 0.3f);
            m.SetFloat("_UnderlayOffsetY", -0.4f);
            m.SetFloat("_UnderlaySoftness", 0.35f);
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static VolumeProfile PostProfile() {
        string path = "Assets/Settings/PostFX_Siege.asset";
        AssetDatabase.DeleteAsset(path);
        VolumeProfile p = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(p, path);
        Bloom bloom = p.Add<Bloom>(true);
        bloom.threshold.Override(1.0f);
        bloom.intensity.Override(0.65f);
        bloom.scatter.Override(0.65f);
        Tonemapping tone = p.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.Neutral);
        ColorAdjustments ca = p.Add<ColorAdjustments>(true);
        ca.postExposure.Override(0f);
        ca.contrast.Override(12f);
        ca.saturation.Override(14f);
        Vignette vig = p.Add<Vignette>(true);
        vig.intensity.Override(0.22f);
        vig.smoothness.Override(0.45f);
        foreach (VolumeComponent comp in p.components) {
            comp.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(comp, p);
        }
        EditorUtility.SetDirty(p);
        return p;
    }

    // ------------------------------------------------------------------ scenes
    [MenuItem("Crumble Kingdoms/3. Build scenes")]
    public static void BuildScenes() {
        Directory.CreateDirectory(SCN);
        GameAssets ga = AssetDatabase.LoadAssetAtPath<GameAssets>(RES + "/GameAssets.asset");

        // Main menu
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Common(ga, new Vector3(0f, 9f, -55f));
        new GameObject("MainMenu").AddComponent<MainMenuUI>();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), MenuScene);

        // Battle
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Camera cam = Common(ga, new Vector3(0f, 6f, -60f));
        cam.gameObject.AddComponent<BattleCamera>();
        new GameObject("BattleManager").AddComponent<BattleManager>();
        new GameObject("HUD").AddComponent<BattleHUD>();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), BattleScene);

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MenuScene, true), new EditorBuildSettingsScene(BattleScene, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("[Siege] scenes built");
    }

    // Camera, sun, post-processing volume and event system shared by both scenes.
    static Camera Common(GameAssets ga, Vector3 camPos) {
        RenderSettings.skybox = null;
        // Fog on in the saved scenes so the build keeps the fog shader variants (ArenaLighting sets it per theme).
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.004f;
        RenderSettings.fogColor = new Color(0.7f, 0.8f, 0.9f);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.72f, 0.8f, 0.92f);
        RenderSettings.ambientEquatorColor = new Color(0.78f, 0.74f, 0.66f);
        RenderSettings.ambientGroundColor = new Color(0.35f, 0.33f, 0.3f);

        GameObject camGO = new GameObject("Main Camera") { tag = "MainCamera" };
        camGO.transform.SetPositionAndRotation(camPos, Quaternion.Euler(4f, 0f, 0f));
        Camera cam = camGO.AddComponent<Camera>();
        cam.fieldOfView = 40f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 1500f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.55f, 0.75f, 0.95f);
        camGO.AddComponent<AudioListener>();
        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

        GameObject sun = new GameObject("Directional Light");
        sun.transform.rotation = Quaternion.Euler(42f, -32f, 0f);
        Light light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.95f, 0.86f);
        light.intensity = 1.3f;
        light.shadows = LightShadows.Soft;
        RenderSettings.sun = light;

        GameObject volGO = new GameObject("Global Volume");
        Volume vol = volGO.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = ga.postProfile;

        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        InputSystemUIInputModule module = es.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
        return cam;
    }

    // ------------------------------------------------------------------ build
    [MenuItem("Crumble Kingdoms/4. Build Windows player")]
    public static void BuildWindows() {
        BuildPlayerOptions o = new BuildPlayerOptions {
            scenes = new[] { MenuScene, BattleScene },
            locationPathName = "Builds/Windows/CrumbleKingdoms.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };
        BuildReport report = BuildPipeline.BuildPlayer(o);
        Debug.Log("[Siege] Build " + report.summary.result + ": " + o.locationPathName + " (" + report.summary.totalErrors + " errors, " +
                  (report.summary.totalSize / (1024 * 1024)) + " MB)");
        if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    // Everything in order (batch mode entry point).
    public static void All() {
        SetupProject();
        BuildAssets();
        BuildScenes();
        BuildWindows();
    }

    public static void AllNoBuild() {
        SetupProject();
        BuildAssets();
        BuildScenes();
    }
}
