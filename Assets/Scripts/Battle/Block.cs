using UnityEngine;

// One fortress building block (local position relative to the fortress root, z = 0).
public struct BlockSpec {
    public Vector3 center;
    public Vector3 size;          // z <= 0 means World.BlockDepth
    public BlockMaterial mat;

    public BlockSpec(Vector3 center, Vector3 size, BlockMaterial mat) { this.center = center; this.size = size; this.mat = mat; }
}

// Block look = physical material + damage stage + the owner's class "skin" (a soldier's stone is grey
// granite, a wizard's is dark rune-carved slate, a god's is white marble). Physics never changes with skin.
public static class BlockVisuals {
    static readonly Material[,] mats = new Material[6, 3];
    static readonly Material[,,] skinned = new Material[Classes.Count, 6, 3];

    public static Material Get(BlockMaterial m, int stage, HeroClass skin) {
        Material mat = skinned[(int)skin, (int)m, stage];
        if (mat != null) return mat;
        mat = ClassLooks.Skin(skin, m, stage) ?? ClassSkins.Block(m, stage, skin) ?? Get(m, stage);
        skinned[(int)skin, (int)m, stage] = mat;
        return mat;
    }

    // Neutral look (no class skin).
    public static Material Get(BlockMaterial m, int stage) {
        Material mat = mats[(int)m, stage];
        if (mat != null) return mat;
        Texture2D tex = TexKit.Block(m, stage);
        switch (m) {
            case BlockMaterial.Glass: mat = Mats.Transparent(Color.white, 0.93f, tex); break;
            case BlockMaterial.Ice: mat = Mats.Transparent(Color.white, 0.86f, tex); break;
            case BlockMaterial.Metal: mat = Mats.LitTex(tex, Color.white, 0.55f, 0.55f); break;
            case BlockMaterial.Stone: mat = Mats.LitTex(tex, Color.white, 0.1f); break;
            case BlockMaterial.Explosive: mat = Mats.LitTex(tex, Color.white, 0.25f); break;
            default: mat = Mats.LitTex(tex, Color.white, 0.16f); break;
        }
        mats[(int)m, stage] = mat;
        return mat;
    }
}

public static class PhysMats {
    static readonly PhysicsMaterial[] blocks = new PhysicsMaterial[6];
    static PhysicsMaterial ground, commander, bouncy, debris;

    static PhysicsMaterial Make(string name, float friction, float bounce, PhysicsMaterialCombine bounceCombine = PhysicsMaterialCombine.Average) {
        return new PhysicsMaterial(name) {
            dynamicFriction = friction, staticFriction = friction * 1.1f, bounciness = bounce,
            frictionCombine = PhysicsMaterialCombine.Average, bounceCombine = bounceCombine,
        };
    }

    public static PhysicsMaterial ForBlock(BlockMaterial m) {
        if (blocks[(int)m] == null) { BlockMatDef d = BlockMats.Get(m); blocks[(int)m] = Make(d.name, d.friction, d.bounce); }
        return blocks[(int)m];
    }

    public static PhysicsMaterial Ground => ground ??= Make("Ground", 0.85f, 0f);
    public static PhysicsMaterial Commander => commander ??= Make("Commander", 0.9f, 0f);
    public static PhysicsMaterial Bouncy => bouncy ??= Make("Bouncy", 0.4f, 0.45f, PhysicsMaterialCombine.Maximum);
    public static PhysicsMaterial Debris => debris ??= Make("Debris", 0.6f, 0.15f);
}

[DisallowMultipleComponent]
public class Block : MonoBehaviour, IDamageable {
    public BlockMaterial material;
    [System.NonSerialized] public BlockMatDef def;
    public HeroClass skin;          // visual style of the owner's class
    public Vector3 size;
    public float maxHP, hp;
    public Fortress fortress;
    public Rigidbody rb;
    public BoxCollider box;
    public MeshRenderer rend;

    public int Stage { get; private set; }
    public bool IsBroken { get; private set; }
    public bool IsLost { get; private set; }
    public bool IsBurning => burnTime > 0f;
    public bool Charred { get => charred; set => charred = value; }   // saved and restored by a time rewind
    public float Volume => size.x * size.y * size.z;
    public Fortress Owner => fortress;
    public bool IsAlive => this != null && !IsBroken && !IsLost;

    public event System.Action<Block> Broken;   // also raised when the block is lost in the sea

    const float SettleGrace = 1.5f;   // no impact damage while the fortress settles after spawning
    const float MaxShove = 4.5f;      // m/s a direct projectile hit may give a block
    const float BurnDps = 9f;

    float spawnTime, lastImpactSfx, burnTime, burnTick, spreadTick;
    Fortress burnSource;
    GameObject fireFx;
    bool splashed;
    bool charred;                     // burned out once: spreading fire passes it by (a new blast can still light it)

    public static Block Create(BlockSpec spec, Fortress f, Transform parent) {
        Vector3 size = spec.size;
        if (size.z <= 0f) size.z = World.BlockDepth;
        BlockMatDef def = BlockMats.Get(spec.mat);

        HeroClass skin = f != null ? f.setup.heroClass : HeroClass.Soldier;
        GameObject go = new GameObject(def.name + " block") { layer = Layers.Block };
        go.transform.SetParent(parent, false);
        go.transform.localPosition = spec.center;
        go.AddComponent<MeshFilter>().sharedMesh = MeshKit.Box(size);
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = BlockVisuals.Get(spec.mat, 0, skin);
        BoxCollider bc = go.AddComponent<BoxCollider>();
        bc.size = size;
        bc.sharedMaterial = PhysMats.ForBlock(spec.mat);
        Rigidbody rb = go.AddComponent<Rigidbody>();
        rb.mass = Mathf.Max(0.25f, size.x * size.y * size.z * def.density);
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.linearDamping = 0.05f;
        rb.angularDamping = 0.2f;

        Block b = go.AddComponent<Block>();
        b.material = spec.mat;
        b.def = def;
        b.skin = skin;
        b.size = size;
        b.fortress = f;
        b.rb = rb;
        b.box = bc;
        b.rend = mr;
        b.maxHP = Mathf.Max(def.minHP, b.Volume * def.hpPerVolume);
        b.hp = b.maxHP;
        b.spawnTime = Time.time;
        rb.Sleep();
        return b;
    }

    public void ApplyDamage(DamageInfo d) {
        if (!IsAlive || d.amount <= 0f) return;
        float amount = d.amount;
        if (def.brittle && (d.type == DamageType.Explosion || d.type == DamageType.Impact || d.type == DamageType.Crush)) amount *= 1.4f;
        if (d.type == DamageType.Fire && !def.flammable) amount *= 0.25f;
        if (d.type == DamageType.Lightning && def.conductive) amount *= 1.3f;

        float before = hp;
        hp = Mathf.Max(0f, hp - amount);
        if (fortress != null) fortress.MarkDirty();
        if (d.source != null && d.source != fortress) d.source.damageDealt += before - hp;
        UpdateStage();

        Vector3 p = d.point == Vector3.zero ? transform.position : d.point;
        if (d.type != DamageType.Fire && amount > maxHP * 0.08f)
            VFX.Chips(p, material, Mathf.Clamp(Mathf.RoundToInt(amount / 6f), 3, 14), d.impulse.sqrMagnitude > 0.01f ? -d.impulse.normalized : Vector3.up);
        if (hp <= 0f) Break(d);
    }

    public void Break(DamageInfo d) {
        if (IsBroken || IsLost) return;
        IsBroken = true;
        hp = 0f;
        StopColliding();
        if (fortress != null) fortress.OnBlockGone(this);
        Broken?.Invoke(this);
        Embedded.ReleaseAll(transform, rb != null ? rb.linearVelocity : Vector3.zero);
        Debris.SpawnFrom(this, d);
        VFX.Shatter(transform.position, size, material);
        SFX.Play(BreakSound(material), transform.position, 0.9f);
        if (material == BlockMaterial.Explosive) {
            Vector3 p = transform.position;
            Fortress src = d.source;
            Runner.Delay(0.07f, () => Explosion.Create(p, 3.3f, 75f, 13f, src, DamageType.Explosion, BlastStyle.Keg));
        }
        Destroy(gameObject);
    }

    // Taken out of the game whole, without debris (eaten by the anteater, beamed up by a UFO, rewound).
    // Counts as lost for the fortress like a block that fell into the sea.
    public void Vanish() { Lose(); }

    // Time rewind: set health without any hit effects.
    public void RestoreHP(float health) {
        hp = Mathf.Clamp(health, 1f, maxHP);
        UpdateStage();
    }

    // Used by the collapse of a defeated fortress.
    public void Crumble() {
        if (!IsAlive) return;
        Vector3 kick = new Vector3(Random.Range(-2f, 2f), Random.Range(1f, 3f), 0f);
        Break(new DamageInfo(hp + 1f, DamageType.Crush, transform.position, null, kick * Mathf.Max(1f, rb.mass)));
    }

    public void Ignite(float seconds, Fortress source) {
        if (!IsAlive || !def.flammable || transform.position.y < World.WaterY) return;
        if (burnTime <= 0f) {
            fireFx = VFX.Fire(transform, Vector3.zero, Mathf.Max(size.x, size.y));
            SFX.Play(SoundId.FireIgnite, transform.position, 0.5f);
        }
        burnTime = Mathf.Max(burnTime, seconds);
        burnSource = source;
    }

    public void Extinguish() {
        burnTime = 0f;
        if (fireFx != null) Destroy(fireFx);
        fireFx = null;
    }

    void FixedUpdate() {
        if (!IsAlive) return;
        float y = transform.position.y;
        if (y < World.WaterY && !splashed) {
            splashed = true;
            Extinguish();
            VFX.Splash(new Vector3(transform.position.x, World.WaterY, 0f), Mathf.Max(size.x, size.y));
            SFX.Play(SoundId.Splash, transform.position, 0.7f);
        }
        if (y < World.KillY) { Lose(); return; }

        if (burnTime > 0f) {
            float dt = Time.fixedDeltaTime;
            burnTime -= dt; burnTick += dt; spreadTick += dt;
            if (burnTick >= 0.25f) {
                burnTick = 0f;
                ApplyDamage(new DamageInfo(BurnDps * 0.25f, DamageType.Fire, transform.position, burnSource));
                if (!IsAlive) return;
            }
            if (spreadTick >= 0.5f) { spreadTick = 0f; SpreadFire(); }
            if (burnTime <= 0f) {
                Extinguish();
                charred = true;
            }
        }
    }

    // Fire creeps to touching wood. Each block burns once that way, so a fire runs through and dies out instead of
    // relighting its neighbours back and forth until the whole fortress is gone.
    void SpreadFire() {
        Collider[] near = Physics.OverlapBox(transform.position, size * 0.5f + Vector3.one * 0.35f, transform.rotation, Layers.BlockMask);
        foreach (Collider c in near) {
            Block o = c.GetComponent<Block>();
            if (o != null && o != this && !o.IsBurning && !o.charred && o.def.flammable && Random.value < 0.35f) o.Ignite(4f, burnSource);
        }
    }

    void Lose() {
        if (IsLost || IsBroken) return;
        IsLost = true;
        StopColliding();
        if (fortress != null) fortress.OnBlockGone(this);
        Broken?.Invoke(this);
        Destroy(gameObject);
    }

    // Destroy happens at the end of the frame; until then a dead block must not shove its own debris
    // (spawned inside it) or anything still resting on it.
    void StopColliding() {
        if (box != null) box.enabled = false;
        if (rb != null) rb.detectCollisions = false;
    }

    void UpdateStage() {
        int s = hp > maxHP * 0.66f ? 0 : hp > maxHP * 0.33f ? 1 : 2;
        if (s == Stage) return;
        Stage = s;
        rend.sharedMaterial = BlockVisuals.Get(material, s, skin);
    }

    void OnCollisionEnter(Collision c) {
        if (!IsAlive || Time.time < spawnTime + SettleGrace) return;
        if (c.rigidbody != null && c.rigidbody.GetComponent<Projectile>() != null) {
            // Projectiles deal their own damage. Masonry absorbs most of the blow: a struck block is shoved,
            // not fired out of the wall like a toy brick.
            rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, MaxShove);
            rb.angularVelocity = Vector3.ClampMagnitude(rb.angularVelocity, 4f);
            return;
        }
        float dv = c.impulse.magnitude / rb.mass;
        if (dv <= BlockMats.ImpactThreshold) return;
        bool fromDebris = c.gameObject.layer == Layers.Debris;
        float dmg = (dv - BlockMats.ImpactThreshold) * def.fragility * maxHP * (fromDebris ? 0.35f : 1f);
        Vector3 p = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
        if (Time.time > lastImpactSfx + 0.15f) {
            lastImpactSfx = Time.time;
            SFX.PlayImpact(material, p, Mathf.Clamp01(dv / 15f));
        }
        ApplyDamage(new DamageInfo(dmg, DamageType.Impact, p, null, c.impulse));
    }

    static SoundId BreakSound(BlockMaterial m) {
        switch (m) {
            case BlockMaterial.Wood: return SoundId.BreakWood;
            case BlockMaterial.Metal: return SoundId.BreakMetal;
            case BlockMaterial.Glass:
            case BlockMaterial.Ice: return SoundId.Shatter;
            default: return SoundId.BreakStone;
        }
    }
}
