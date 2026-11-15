using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

// The few assets that must be made in the editor (fonts, base materials with the right shader set-up,
// the post-processing profile). SiegeBuilder creates Resources/GameAssets.asset; everything else
// (meshes, textures, coloured material copies) is generated at runtime from code.
public class GameAssets : ScriptableObject {
    static GameAssets instance;

    public static GameAssets I {
        get {
            if (instance == null) instance = Resources.Load<GameAssets>("GameAssets");
            return instance;
        }
    }

    [Header("Fonts (TextMesh Pro)")]
    public TMP_FontAsset fontBold;
    public TMP_FontAsset fontMedium;
    public Material fontBoldOutline;    // dark outline + soft drop shadow, for titles and banners
    public Material fontMediumOutline;  // thin dark outline, for text over the 3D world

    [Header("Base materials — runtime copies get colours/textures (see Mats)")]
    public Material litOpaque;          // URP/Lit opaque
    public Material litEmissive;        // URP/Lit opaque with _EMISSION on
    public Material litTransparent;     // URP/Lit transparent (alpha blend)
    public Material unlitOpaque;        // URP/Unlit opaque
    public Material unlitTransparent;   // URP/Unlit transparent (alpha blend)
    public Material particleAdditive;   // URP/Particles/Unlit additive
    public Material particleAlpha;      // URP/Particles/Unlit alpha blend

    [Header("Post-processing")]
    public VolumeProfile postProfile;   // bloom, tonemapping, colour grading, vignette
}
