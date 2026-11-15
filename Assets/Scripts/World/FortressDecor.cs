using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Visual-only details on a built fortress, in the style of its class (the team colour is only on flags and
// banners, so two fortresses of one class still tell their sides apart):
//   Soldier: square towers with battlements, crimson heater shields, torches, iron braziers and a palisade of
//            sharpened stakes in front of the walls.
//   Wizard:  tall blue spires with gold rims and a crystal floating above each, glowing rune circles on the
//            walls, hovering arcane lamps, violet fire, glowing crystals growing at the foot of the walls.
//   God:     golden domes and a golden pediment over the hall, sun discs, braziers of holy fire that burn day
//            and night, a runestone with glowing runes in front of the gate.
// Every other class gets only the class-neutral team flag and banners here; the rest of its look is registered
// in ClassLooks by its own file.
// Everything is parented to a block (so it topples and falls with it) and has no collider. At night up to
// MaxLights point lights per fortress make fires and crystals light up the walls.
public static class FortressDecor {
    const int MaxLights = 3;
    const float FlagPole = 2.3f, SpireHeight = 2.3f;

    static readonly Color PoleWood = new Color(0.36f, 0.25f, 0.16f), Oak = new Color(0.47f, 0.32f, 0.18f);
    static readonly Color Gold = new Color(1f, 0.78f, 0.3f), Iron = new Color(0.22f, 0.22f, 0.24f);
    static readonly Color Steel = new Color(0.6f, 0.63f, 0.68f), Crimson = new Color(0.62f, 0.07f, 0.1f);
    static readonly Color Silver = new Color(0.78f, 0.8f, 0.88f), Linen = new Color(0.95f, 0.93f, 0.88f);
    static readonly Color SpireBlue = new Color(0.22f, 0.22f, 0.6f), SpireShadow = new Color(0.07f, 0.06f, 0.2f);
    static readonly Color Violet = new Color(0.72f, 0.36f, 1f), Cyan = new Color(0.32f, 0.88f, 1f), Amber = new Color(1f, 0.62f, 0.2f);
    static readonly Color FireHot = new Color(1f, 0.78f, 0.35f), FireCool = new Color(1f, 0.45f, 0.12f);
    static readonly Color HolyHot = new Color(1f, 0.97f, 0.82f), HolyCool = new Color(1f, 0.74f, 0.26f);
    static readonly Color ArcaneHot = new Color(0.86f, 0.72f, 1f), ArcaneCool = new Color(0.45f, 0.2f, 1f);

    static Mesh heaterMesh, raysMesh;

    // One decoration pass: the fortress's class and the lights it has left.
    class Job {
        public HeroClass hero;
        public Color team;
        public int facing;
        public bool night;
        public int lights;

        public bool TakeLight() {
            if (!night || lights >= MaxLights) return false;
            lights++;
            return true;
        }
    }

    public static void Apply(Fortress f) {
        FortressPlan plan = f.blueprint as FortressPlan;
        bool styled = IsStyled(f.HeroClass);
        if (plan != null && f.blocks.Count == plan.blocks.Count) Place(f, plan, styled);   // anchors need the original block order
        if (!styled) ClassLooks.Decorate(f);
    }

    // The classes this file dresses; any other class brings its own decorations (ClassLooks).
    static bool IsStyled(HeroClass c) { return c == HeroClass.Soldier || c == HeroClass.Wizard || c == HeroClass.God; }

    static void Place(Fortress f, FortressPlan plan, bool styled) {
        Job job = new Job { hero = f.HeroClass, team = f.TeamColor, facing = f.Facing >= 0 ? 1 : -1, night = ArenaBuilder.IsNight };
        Dictionary<int, float> roofs = new Dictionary<int, float>();   // turret block -> height of its roof (a flag goes above it)
        foreach (DecorAnchor a in plan.decor) {
            Block host = Host(f, a);
            if (styled && host != null && a.kind == DecorKind.TurretRoof) roofs[a.block] = TurretRoof(job, host, a.local);
        }
        foreach (DecorAnchor a in plan.decor) {
            Block host = Host(f, a);
            if (host == null || (!styled && a.kind != DecorKind.Flag && a.kind != DecorKind.Banner)) continue;
            Transform t = host.transform;
            switch (a.kind) {
                case DecorKind.Flag:
                    roofs.TryGetValue(a.block, out float roof);
                    Flag(job, t, a.local + Vector3.up * roof);
                    break;
                case DecorKind.Banner: Banner(job, t, a.local); break;
                case DecorKind.Shield: Emblem(job, t, a.local); break;
                case DecorKind.Torch: WallLight(job, t, a.local); break;
                case DecorKind.Brazier: Brazier(job, t, a.local); break;
                case DecorKind.Pediment: Pediment(job, t, a.local, a.span); break;
                case DecorKind.Base: Base(job, host, a.local, a.span); break;
            }
        }
    }

    static Block Host(Fortress f, DecorAnchor a) {
        if (a.block < 0 || a.block >= f.blocks.Count) return null;
        Block b = f.blocks[a.block];
        return b != null ? b : null;
    }

    static Material Cloth(Color team) { return Mats.LitTex(WorldTextures.Emblem, team, 0.15f); }
    static Material GoldMetal => Mats.Lit(Gold, 0.62f, 0.8f);
    static Material GoldGlow => Mats.Emissive(Gold, Gold * 0.22f, 0.72f);   // a god's gold: never dull, softly blooms
    static Material IronMetal => Mats.Lit(Iron, 0.45f, 0.6f);
    static Material Glowing(Color c, float hdr) { return Mats.Emissive(c * 0.7f, c * hdr); }

    // ------------------------------------------------------------------ flags and banners (team colour)
    // A pole with a class finial (other classes: a plain gold knob) and a cloth that streams with the wind.
    static void Flag(Job job, Transform host, Vector3 at) {
        Transform root = Root("Flag", host, at);
        Material pole = job.hero == HeroClass.God ? GoldGlow
                      : job.hero == HeroClass.Wizard ? Mats.Lit(new Color(0.16f, 0.13f, 0.22f), 0.3f) : Mats.Lit(PoleWood, 0.2f);
        Part(root, WorldMeshes.Frustum(6, 0.05f, 0.035f, FlagPole), Vector3.zero, pole);
        Vector3 top = Vector3.up * FlagPole;
        switch (job.hero) {
            case HeroClass.Soldier: Part(root, MeshKit.Cone(6, 0.06f, 0.22f), top, Mats.Lit(Steel, 0.5f, 0.7f)); break;   // spear tip
            case HeroClass.Wizard: Crystal(root, top + Vector3.up * 0.02f, 0.07f, 0.17f, Violet, 0f); break;
            case HeroClass.God: Part(root, WorldMeshes.Ball(0.09f), top + Vector3.up * 0.04f, GoldGlow); break;
            default: Part(root, WorldMeshes.Ball(0.085f), top + Vector3.up * 0.04f, GoldMetal); break;
        }
        FlagCloth.Create(root, new Vector3(0f, FlagPole - 0.08f, 0f), Cloth(job.team), job.facing);
    }

    // A swallow-tailed banner hanging from a rod of iron (soldier), silver (wizard) or gold.
    static void Banner(Job job, Transform host, Vector3 at) {
        Transform root = Root("Banner", host, at + new Vector3(0f, 0f, -0.02f));
        Material rod = job.hero == HeroClass.Soldier ? IronMetal : job.hero == HeroClass.Wizard ? Mats.Lit(Silver, 0.6f, 0.8f)
                     : job.hero == HeroClass.God ? GoldGlow : GoldMetal;
        Part(root, WorldMeshes.Frustum(6, 0.025f, 0.025f, 0.6f), new Vector3(-0.3f, 0.02f, -0.02f), rod, new Vector3(0f, 0f, -90f));
        for (int s = -1; s <= 1; s += 2) Part(root, WorldMeshes.Ball(0.04f), new Vector3(s * 0.31f, 0.02f, -0.02f), rod);
        ModelKit.MeshPart(root, WorldMeshes.Banner(0.46f, 0.95f, 0.16f), Vector3.zero, Vector3.one, Cloth(job.team), default, "Cloth");
    }

    // ------------------------------------------------------------------ turret roofs
    // Returns the roof's height above the turret's top.
    static float TurretRoof(Job job, Block turret, Vector3 at) {
        switch (job.hero) {
            case HeroClass.Wizard: return Spire(job, turret.transform, at, turret.size);
            case HeroClass.God: return Dome(turret.transform, at, turret.size);
            default: return Battlements(turret.transform, at, turret.size, turret.rend.sharedMaterial);
        }
    }

    // Soldier: a flat, crenellated cap - an overhanging slab with merlons at the corners and mid-front/back.
    static float Battlements(Transform host, Vector3 at, Vector3 size, Material stone) {
        Transform root = Root("Battlements", host, at);
        float hx = size.x * 0.5f + 0.06f, hz = size.z * 0.5f + 0.06f;
        const float slab = 0.12f, m = 0.22f, mh = 0.26f;
        Box(root, new Vector3(0f, slab * 0.5f, 0f), new Vector3(hx * 2f, slab, hz * 2f), stone);
        for (int i = -1; i <= 1; i++)
            for (int j = -1; j <= 1; j += 2)
                Box(root, new Vector3(i * (hx - m * 0.5f), slab + mh * 0.5f, j * (hz - m * 0.5f)), new Vector3(m, mh, m), stone);
        return slab + mh;
    }

    // Wizard: a tall cone of blue fish-scale tiles with a gold rim and spike, and a crystal floating above.
    static float Spire(Job job, Transform host, Vector3 at, Vector3 size) {
        Transform root = Root("Spire", host, at);
        float r = 0.5f * Mathf.Sqrt(size.x * size.x + size.z * size.z) + 0.08f;
        Part(root, WorldMeshes.Frustum(12, r, 0f, SpireHeight), Vector3.zero, DecorTextures.RoofTiles(SpireBlue, SpireShadow));
        Part(root, WorldMeshes.Frustum(12, r + 0.03f, r - 0.02f, 0.1f), Vector3.zero, GoldMetal);
        Part(root, WorldMeshes.Frustum(6, 0.035f, 0f, 0.4f), Vector3.up * (SpireHeight - 0.05f), GoldMetal);
        Transform crystal = Crystal(root, Vector3.up * (SpireHeight + 0.7f), 0.15f, 0.34f, Violet, 0.09f);
        if (job.TakeLight()) PointLight(crystal, Vector3.zero, Violet, 2.4f, 7f, false);
        return SpireHeight;
    }

    // God: a golden dome on a gold drum with a short spike.
    static float Dome(Transform host, Vector3 at, Vector3 size) {
        Transform root = Root("Dome", host, at);
        float r = 0.5f * size.z + 0.04f, squash = size.x / size.z;
        Part(root, WorldMeshes.Frustum(16, r + 0.04f, r + 0.04f, 0.1f), Vector3.zero, GoldGlow, default, new Vector3(squash, 1f, 1f));
        Part(root, WorldMeshes.Dome(16, 4, r), Vector3.up * 0.08f, GoldGlow, default, new Vector3(squash, 1f, 1f));
        Part(root, WorldMeshes.Frustum(6, 0.045f, 0f, 0.3f), Vector3.up * (r + 0.06f), GoldGlow);
        return r + 0.06f;
    }

    // ------------------------------------------------------------------ wall emblems
    static void Emblem(Job job, Transform host, Vector3 at) {
        switch (job.hero) {
            case HeroClass.Wizard: RuneCircle(host, at); break;
            case HeroClass.God: SunDisc(host, at, 1f); break;
            default: HeaterShield(host, at); break;
        }
    }

    // Soldier: a crimson heater shield on a steel rim, with a white bend and a gold boss.
    static void HeaterShield(Transform host, Vector3 at) {
        Transform root = Root("Shield", host, at);
        Part(root, HeaterMesh(), new Vector3(0f, 0f, -0.01f), Mats.Lit(Steel, 0.5f, 0.7f), default, Vector3.one * 1.13f);
        Part(root, HeaterMesh(), new Vector3(0f, 0.01f, -0.025f), Mats.Lit(Crimson, 0.35f));
        Box(root, new Vector3(0f, 0.02f, -0.03f), new Vector3(0.075f, 0.48f, 0.01f), Mats.Lit(Linen, 0.3f), new Vector3(0f, 0f, 38f));
        Part(root, WorldMeshes.Ball(0.05f), new Vector3(0f, 0.05f, -0.045f), GoldMetal);
    }

    // Wizard: a glowing rune circle that turns slowly around a cyan gem.
    static void RuneCircle(Transform host, Vector3 at) {
        Transform root = Root("Rune Circle", host, at + new Vector3(0f, 0f, -0.02f));
        GameObject circle = Part(root, WorldMeshes.Quad(), Vector3.zero, Mats.UnlitTransparent(new Color(1.7f, 0.9f, 2.8f, 1f), DecorTextures.RuneCircle),
                                 default, Vector3.one * 0.66f);
        circle.AddComponent<DecorMotion>().spin = new Vector3(0f, 0f, 16f);
        Part(root, WorldMeshes.Ball(0.05f), new Vector3(0f, 0f, -0.03f), Glowing(Cyan, 2.6f));
    }

    // God: a golden sun disc with twelve rays and a glowing amber gem.
    static void SunDisc(Transform host, Vector3 at, float scale) {
        Transform root = Root("Sun Disc", host, at);
        root.localScale = Vector3.one * scale;
        Part(root, RaysMesh(), new Vector3(0f, 0f, -0.012f), GoldGlow);
        Part(root, WorldMeshes.Disc(16, 0.16f), new Vector3(0f, 0f, -0.022f), GoldGlow);
        Part(root, WorldMeshes.Ball(0.065f), new Vector3(0f, 0f, -0.04f), Glowing(Amber, 2.4f));
    }

    // ------------------------------------------------------------------ lights and fires
    static void WallLight(Job job, Transform host, Vector3 at) {
        switch (job.hero) {
            case HeroClass.Wizard: ArcaneLamp(job, host, at); break;
            case HeroClass.God: GoldenLamp(job, host, at); break;
            default: Torch(job, host, at); break;
        }
    }

    // Soldier: an iron bracket and a wooden torch that burns at night.
    static void Torch(Job job, Transform host, Vector3 at) {
        Transform root = Root("Torch", host, at);
        Box(root, new Vector3(0f, 0f, -0.1f), new Vector3(0.09f, 0.09f, 0.22f), IronMetal);
        Part(root, WorldMeshes.Frustum(5, 0.035f, 0.05f, 0.42f), new Vector3(0f, -0.12f, -0.2f), Mats.Lit(PoleWood, 0.2f), new Vector3(-18f, 0f, 0f));
        Part(root, WorldMeshes.Frustum(6, 0.06f, 0.065f, 0.09f), new Vector3(0f, 0.25f, -0.3f), Mats.Lit(new Color(0.12f, 0.1f, 0.08f), 0.1f), new Vector3(-18f, 0f, 0f));
        if (job.night) Fire(job, root, new Vector3(0f, 0.36f, -0.33f), 0.3f, FireHot, FireCool, 2.1f);
    }

    // Wizard: a silver bracket holding a hovering orb of violet light (it glows by day as well).
    static void ArcaneLamp(Job job, Transform host, Vector3 at) {
        Transform root = Root("Arcane Lamp", host, at);
        Material silver = Mats.Lit(Silver, 0.6f, 0.8f);
        Box(root, new Vector3(0f, 0f, -0.1f), new Vector3(0.07f, 0.07f, 0.22f), silver);
        Part(root, WorldMeshes.Frustum(6, 0.09f, 0.04f, 0.06f), new Vector3(0f, -0.02f, -0.24f), silver);
        Transform orb = ModelKit.MeshPart(root, WorldMeshes.Ball(0.085f), new Vector3(0f, 0.2f, -0.24f), Vector3.one, Glowing(Violet, 3f)).transform;
        DecorMotion hover = orb.gameObject.AddComponent<DecorMotion>();
        hover.bob = 0.05f;
        hover.spin = new Vector3(0f, 60f, 0f);
        if (job.TakeLight()) PointLight(orb, Vector3.zero, Violet, 2f, 6f, false);
    }

    // God: a gold bracket with a small bowl; holy fire burns in it at night, an ember glows by day.
    static void GoldenLamp(Job job, Transform host, Vector3 at) {
        Transform root = Root("Golden Lamp", host, at);
        Box(root, new Vector3(0f, 0f, -0.1f), new Vector3(0.07f, 0.07f, 0.22f), GoldGlow);
        Part(root, WorldMeshes.Frustum(8, 0.06f, 0.13f, 0.11f), new Vector3(0f, 0.02f, -0.25f), GoldGlow);
        if (job.night) Fire(job, root, new Vector3(0f, 0.17f, -0.25f), 0.3f, HolyHot, HolyCool, 2f);
        else Part(root, WorldMeshes.Ball(0.06f), new Vector3(0f, 0.13f, -0.25f), Glowing(Amber, 2.2f));
    }

    // A bowl on a stand. A god's is gold and burns with holy fire day and night; soldiers' and wizards' are iron
    // and burn at night, a wizard's with violet fire.
    static void Brazier(Job job, Transform host, Vector3 at) {
        Transform root = Root("Brazier", host, at);
        bool god = job.hero == HeroClass.God;
        Material metal = god ? GoldGlow : IronMetal;
        Part(root, WorldMeshes.Frustum(6, 0.07f, 0.05f, 0.22f), Vector3.zero, metal);
        Part(root, WorldMeshes.Frustum(8, 0.12f, 0.24f, 0.17f), Vector3.up * 0.2f, metal);
        if (!god && !job.night) return;
        Color hot = god ? HolyHot : job.hero == HeroClass.Wizard ? ArcaneHot : FireHot;
        Color cool = god ? HolyCool : job.hero == HeroClass.Wizard ? ArcaneCool : FireCool;
        Fire(job, root, Vector3.up * 0.42f, 0.48f, hot, cool, 2.6f);
    }

    // Flames plus, while the fortress has lights left (night only), a flickering point light.
    static void Fire(Job job, Transform parent, Vector3 at, float size, Color hot, Color cool, float intensity) {
        FlameEmitter.Create(parent, at, size, hot, cool);
        if (job.TakeLight()) PointLight(parent, at + new Vector3(0f, 0.15f, -0.35f), cool, intensity, 6.5f, true);
    }

    static void PointLight(Transform parent, Vector3 at, Color color, float intensity, float range, bool flicker) {
        GameObject go = ModelKit.Root("Decor Light", parent);
        go.transform.localPosition = at;
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
        if (flicker) go.AddComponent<FlameFlicker>();
    }

    // ------------------------------------------------------------------ the god's pediment
    // A low golden gable over the hall: a marble face set in a gold frame, a sun disc and gold finials.
    static void Pediment(Job job, Transform host, Vector3 at, float span) {
        if (job.hero != HeroClass.God || span < 1f) return;
        float h = Mathf.Min(0.6f, span * 0.16f), depth = World.BlockDepth + 0.2f;
        Transform root = Root("Pediment", host, at);
        Part(root, MeshKit.Wedge(new Vector3(span, h, depth)), Vector3.zero, GoldGlow);
        Material marble = ClassSkins.Block(BlockMaterial.Stone, 0, HeroClass.God) ?? Mats.Lit(Linen, 0.5f);
        Part(root, MeshKit.Wedge(new Vector3(span * 0.84f, h * 0.7f, depth + 0.02f)), Vector3.up * 0.05f, marble);
        for (int s = -1; s <= 1; s += 2) Part(root, WorldMeshes.Ball(0.1f), new Vector3(s * span * 0.5f, 0.06f, -depth * 0.5f), GoldGlow);
        Part(root, WorldMeshes.Frustum(6, 0.06f, 0f, 0.32f), new Vector3(0f, h - 0.03f, -depth * 0.5f + 0.06f), GoldGlow);
        SunDisc(root, new Vector3(0f, h * 0.42f, -depth * 0.5f - 0.02f), Mathf.Min(0.75f, h * 1.6f));
    }

    // ------------------------------------------------------------------ in front of the walls
    // `clear` = how far beyond the pillar's outer face the ground is free (a plinth may stand there).
    static void Base(Job job, Block pillar, Vector3 at, float clear) {
        Transform host = pillar.transform;
        float face = pillar.size.x * 0.5f + clear;
        switch (job.hero) {
            case HeroClass.Wizard: CrystalCluster(host, at + new Vector3(job.facing * (face + 0.45f), 0f, -0.5f), job.facing); break;
            case HeroClass.God: Runestone(host, at + new Vector3(job.facing * (face + 0.5f), 0f, -0.55f), job.facing); break;
            default: Palisade(host, at + new Vector3(job.facing * face, 0f, 0f), job.facing); break;
        }
    }

    // Soldier: five sharpened stakes leaning towards the enemy, tied by a rail.
    static void Palisade(Transform host, Vector3 at, int facing) {
        Transform root = Root("Palisade", host, at);
        Material wood = Mats.Lit(Oak, 0.2f);
        float[] zs = { -0.55f, 0.2f, -0.15f, 0.55f, -0.75f };
        for (int i = 0; i < zs.Length; i++) {
            float h = 0.95f + 0.12f * (i % 3);
            Part(root, WorldMeshes.Frustum(5, 0.075f, 0.01f, h), new Vector3(facing * (0.25f + i * 0.27f), -0.05f, zs[i]), wood,
                 new Vector3(0f, 0f, -facing * 24f));
        }
        Box(root, new Vector3(facing * 0.85f, 0.38f, -0.18f), new Vector3(1.35f, 0.07f, 0.07f), wood);
    }

    // Wizard: three glowing crystals growing out of the ground.
    static void CrystalCluster(Transform host, Vector3 at, int facing) {
        Transform root = Root("Crystals", host, at);
        Crystal(root, Vector3.zero, 0.13f, 0.42f, Violet, 0f).localRotation = Quaternion.Euler(0f, 20f, -facing * 12f);
        Crystal(root, new Vector3(facing * 0.24f, 0f, 0.12f), 0.09f, 0.3f, Cyan, 0f).localRotation = Quaternion.Euler(0f, 0f, -facing * 30f);
        Crystal(root, new Vector3(-facing * 0.2f, 0f, -0.08f), 0.08f, 0.24f, Violet, 0f).localRotation = Quaternion.Euler(10f, 0f, facing * 26f);
    }

    // God: a standing runestone with a band of glowing golden runes on its face.
    static void Runestone(Transform host, Vector3 at, int facing) {
        Transform root = Root("Runestone", host, at);
        root.localRotation = Quaternion.Euler(0f, -facing * 14f, -facing * 4f);
        Material stone = Mats.Lit(new Color(0.55f, 0.55f, 0.6f), 0.15f);
        Box(root, new Vector3(0f, 0.58f, 0f), new Vector3(0.44f, 1.16f, 0.2f), stone);
        Part(root, WorldMeshes.Dome(10, 2, 0.22f), new Vector3(0f, 1.16f, 0f), stone, default, new Vector3(1f, 0.55f, 0.45f));
        Part(root, WorldMeshes.Quad(), new Vector3(0f, 0.62f, -0.105f), Mats.UnlitTransparent(new Color(2.4f, 1.7f, 0.55f, 1f), DecorTextures.RuneBand),
             default, new Vector3(DecorTextures.RuneBandWidth, DecorTextures.RuneBandHeight, 1f));
    }

    // ------------------------------------------------------------------ helpers
    // A glowing crystal: a six-sided bipyramid (point up), optionally hovering and turning (DecorMotion).
    static Transform Crystal(Transform parent, Vector3 at, float radius, float height, Color color, float hover) {
        Transform root = Root("Crystal", parent, at);
        Material m = Glowing(color, 2.6f);
        Part(root, WorldMeshes.Frustum(6, radius, 0f, height), Vector3.zero, m);
        Part(root, WorldMeshes.Frustum(6, radius, 0f, height * 0.45f), Vector3.zero, m, new Vector3(180f, 0f, 0f));
        if (hover > 0f) {
            DecorMotion motion = root.gameObject.AddComponent<DecorMotion>();
            motion.bob = hover;
            motion.spin = new Vector3(0f, 45f, 0f);
        }
        return root;
    }

    // A heater shield's face: flat top, sides curving into a point (facing -z, 0.44 x 0.6 m).
    static Mesh HeaterMesh() {
        if (heaterMesh != null) return heaterMesh;
        MeshBuilder b = new MeshBuilder();
        List<Vector2> outline = new List<Vector2> { new Vector2(0.22f, 0.27f), new Vector2(-0.22f, 0.27f) };
        for (int k = 0; k <= 4; k++) {   // down the left side to the point...
            float t = k / 4f;
            outline.Add(new Vector2(-0.22f * (1f - t * t), 0.02f - 0.35f * t));
        }
        for (int k = 3; k >= 0; k--) {   // ...and back up the right side
            float t = k / 4f;
            outline.Add(new Vector2(0.22f * (1f - t * t), 0.02f - 0.35f * t));
        }
        int c = b.Vertex(Vector3.zero, new Vector2(0.5f, 0.5f));
        for (int i = 0; i < outline.Count; i++) {
            Vector2 p = outline[i], q = outline[(i + 1) % outline.Count];
            b.Triangle(c, b.Vertex(q, q + Vector2.one * 0.5f), b.Vertex(p, p + Vector2.one * 0.5f));   // counter-clockwise outline: c, next, this
        }
        heaterMesh = b.ToMesh("Heater Shield");
        return heaterMesh;
    }

    // Twelve triangular rays around a disc (facing -z), for the sun discs.
    static Mesh RaysMesh() {
        if (raysMesh != null) return raysMesh;
        MeshBuilder b = new MeshBuilder();
        for (int k = 0; k < 12; k++) {
            float a = k * Mathf.PI / 6f, w = 0.13f;
            Vector3 left = new Vector3(Mathf.Cos(a + w), Mathf.Sin(a + w)) * 0.14f, right = new Vector3(Mathf.Cos(a - w), Mathf.Sin(a - w)) * 0.14f;
            Vector3 tip = new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * (k % 2 == 0 ? 0.33f : 0.26f);
            b.Triangle(b.Vertex(left, Vector2.zero), b.Vertex(tip, Vector2.up), b.Vertex(right, Vector2.right));
        }
        raysMesh = b.ToMesh("Sun Rays");
        return raysMesh;
    }

    static Transform Root(string name, Transform parent, Vector3 at) {
        Transform t = ModelKit.Root(name, parent).transform;
        t.localPosition = at;
        return t;
    }

    static GameObject Part(Transform parent, Mesh mesh, Vector3 at, Material m, Vector3 euler = default, Vector3 scale = default) {
        return ModelKit.MeshPart(parent, mesh, at, scale == default ? Vector3.one : scale, m, euler);
    }

    static GameObject Box(Transform parent, Vector3 at, Vector3 size, Material m, Vector3 euler = default) {
        return ModelKit.Part(parent, PrimitiveType.Cube, at, size, m, euler);
    }
}

// Small looping flame for torches and braziers (additive particles that rise and fade), in any fire colour.
public static class FlameEmitter {
    public static ParticleSystem Create(Transform parent, Vector3 localPos, float size, Color hot, Color cool) {
        GameObject go = ModelKit.Root("Flame", parent);
        go.transform.localPosition = localPos;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.55f, size);
        main.startColor = new ParticleSystem.MinMaxGradient(hot, cool);
        main.gravityModifier = -0.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;
        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 26f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = size * 0.15f;
        ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.15f));
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.Lerp(hot, Color.white, 0.4f), 0f), new GradientColorKey(hot, 0.4f),
                          new GradientColorKey(cool * 0.8f, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = Mats.ParticleAdditive(TexKit.SoftCircle);
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
        return ps;
    }
}

// Makes a point light breathe like a real flame.
public class FlameFlicker : MonoBehaviour {
    Light lightSource;
    float baseIntensity, seed;

    void Start() {
        lightSource = GetComponent<Light>();
        baseIntensity = lightSource != null ? lightSource.intensity : 0f;
        seed = Random.value * 100f;
    }

    void Update() {
        if (lightSource == null) return;
        float n = Mathf.PerlinNoise(seed, Time.time * 6f);
        lightSource.intensity = baseIntensity * (0.75f + 0.45f * n);
    }
}

// Bobs and turns a decoration (floating crystals, hovering lamps, turning rune circles).
public class DecorMotion : MonoBehaviour {
    public float bob, bobSpeed = 1.7f;
    public Vector3 spin;   // degrees per second around the local axes
    Vector3 home;
    float phase;

    void Start() {
        home = transform.localPosition;
        phase = Random.value * 10f;
    }

    void Update() {
        if (bob > 0f) transform.localPosition = home + Vector3.up * (Mathf.Sin(Time.time * bobSpeed + phase) * bob);
        if (spin != Vector3.zero) transform.Rotate(spin * Time.deltaTime, Space.Self);
    }
}

// The cloth of a flag: a double-sided grid rippled every frame. It swings round the pole to stream the way
// the wind blows (BattleManager.Wind), so every fortress flag doubles as a wind sock; with no wind it droops.
public class FlagCloth : MonoBehaviour {
    const int Cols = 9, Rows = 5;
    const float Length = 1.15f, Height = 0.72f;

    Mesh mesh;
    Vector3[] verts;
    float yaw, phase, strength;

    public static FlagCloth Create(Transform parent, Vector3 localPos, Material mat, int facing) {
        GameObject go = ModelKit.Root("Cloth", parent);
        go.transform.localPosition = localPos;
        FlagCloth c = go.AddComponent<FlagCloth>();
        c.yaw = facing >= 0 ? 180f : 0f;   // no wind: the flag hangs away from the enemy
        c.phase = Random.value * 10f;
        c.Build(mat);
        c.Update();
        return c;
    }

    void Build(Material mat) {
        int n = Cols * Rows;
        verts = new Vector3[n * 2];
        Vector2[] uv = new Vector2[n * 2];
        int[] tris = new int[(Cols - 1) * (Rows - 1) * 12];
        int t = 0;
        for (int j = 0; j < Rows; j++)
            for (int i = 0; i < Cols; i++) {
                int k = j * Cols + i;
                uv[k] = uv[k + n] = new Vector2(i / (Cols - 1f), 1f - j / (Rows - 1f));
                if (i == Cols - 1 || j == Rows - 1) continue;
                int tl = k, tr = k + 1, bl = k + Cols, br = k + Cols + 1;
                // front side (seen from -z) and back side (same quad, reversed winding, own vertices)
                tris[t++] = bl; tris[t++] = tl; tris[t++] = tr;
                tris[t++] = bl; tris[t++] = tr; tris[t++] = br;
                tris[t++] = bl + n; tris[t++] = tr + n; tris[t++] = tl + n;
                tris[t++] = bl + n; tris[t++] = br + n; tris[t++] = tr + n;
            }
        mesh = new Mesh { name = "Flag" };
        mesh.MarkDynamic();
        mesh.vertices = verts;
        mesh.uv = uv;
        mesh.triangles = tris;
        mesh.bounds = new Bounds(new Vector3(0f, -Height * 0.5f, 0f), new Vector3(Length * 2.4f, Height * 2f, Length * 2.4f));
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        gameObject.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    void Update() {
        if (mesh == null) return;
        float wind = BattleManager.Wind;
        strength = Mathf.MoveTowards(strength, Mathf.Clamp01(Mathf.Abs(wind) / 1.6f), Time.deltaTime);
        if (Mathf.Abs(wind) > 0.05f) yaw = Mathf.MoveTowardsAngle(yaw, wind > 0f ? 0f : 180f, 200f * Time.deltaTime);
        transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

        float t = Time.time * (4f + 5f * strength) + phase;
        float amp = 0.06f + 0.1f * strength, droop = 0.4f * (1f - strength);
        int n = Cols * Rows;
        for (int j = 0; j < Rows; j++)
            for (int i = 0; i < Cols; i++) {
                float k = i / (Cols - 1f);
                float x = k * Length * (1f - 0.18f * (1f - strength) * k);
                float y = -j / (Rows - 1f) * Height - droop * k * k;
                float z = amp * k * Mathf.Sin(k * 5.5f - t) + 0.02f * Mathf.Sin(j * 1.7f + t * 1.3f) * k;
                verts[j * Cols + i] = verts[j * Cols + i + n] = new Vector3(x, y, z);
            }
        mesh.vertices = verts;
        mesh.RecalculateNormals();
    }
}
