using UnityEngine;

// The Candidate's weapons: an election campaign. His passive is the vote (CandidateVotes, below): every hit he lands wins
// votes, and every 20 % of them makes all four of his skills stronger. Each weapon lives in its own file:
//   CandidateWeaponsStamp.cs       Ballot Stamp: a voting stamp; where it lands a giant stamp comes down from the sky
//   CandidateWeaponsShout.cs       Rally Shout: a shout wave shoves the nearest enemy fortress, then echoes back as a shield
//   CandidateWeaponsBus.cs         Campaign Bus: a bus thrown dead straight that blows up on what it hits
//   CandidateWeaponsSignature.cs   Signature: a giant pen signs a decree in the air that silences every enemy for a turn
//   CandidateWeaponsSounds.cs      the class's own synthesised sounds
// His look (commander, block skins, fortress decorations) is in World/CandidateLook*.cs; the decorations also start his
// vote count (CandidateVotes.Attach) when his fortress is built.
static class CandidateWeapons {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() {
        WeaponFactory.Register(WeaponId.BallotStamp, CandidateStamp.Fire, CandidateStamp.Model);
        WeaponFactory.Register(WeaponId.RallyShout, CandidateShout.Fire, CandidateShout.Model);
        WeaponFactory.Register(WeaponId.CampaignBus, CandidateBus.Fire, CandidateBus.Model);
        WeaponFactory.Register(WeaponId.Signature, CandidateSignature.Fire, CandidateSignature.Model);
    }
}

// Colours and materials shared by the Candidate's weapons and his look: a neutral campaign of teal, navy and gold (no
// real party's colours or symbols; his mark is a tick in a circle).
public static class CandidateKit {
    public static readonly Color Teal = WeaponDefs.Hex("27B5C4");               // the class colour
    public static readonly Color TealHdr = new Color(0.6f, 3.2f, 3.6f);         // its glow (above 1 blooms)
    public static readonly Color Navy = new Color(0.1f, 0.14f, 0.3f);
    public static readonly Color Gold = new Color(1f, 0.8f, 0.32f);
    public static readonly Color Ink = new Color(0.42f, 0.2f, 0.78f);           // stamp ink: violet
    public static readonly Color InkHdr = new Color(1.6f, 0.7f, 3.4f);
    public static readonly Color Paper = new Color(0.97f, 0.96f, 0.92f);
    public static readonly Color VoteRed = new Color(0.9f, 0.16f, 0.2f);       // the vote bar on his card

    public static Material TealMat => Mats.Lit(Teal, 0.45f);
    public static Material TealGlowMat => Mats.Emissive(Teal, TealHdr);
    public static Material NavyMat => Mats.Lit(Navy, 0.4f);
    public static Material GoldMat => Mats.Lit(Gold, 0.75f, 0.6f);
    public static Material InkMat => Mats.Lit(Ink, 0.5f);
    public static Material InkGlowMat => Mats.Emissive(Ink, InkHdr);
    public static Material PaperMat => Mats.Lit(Paper, 0.3f);

    // Effects are drawn just in front of the blocks (see VFX.FrontZ).
    public static Vector3 Front(Vector3 p) { return new Vector3(p.x, p.y, Mathf.Min(p.z, VFX.FrontZ)); }
}

// The Candidate's votes, his passive. All the damage he deals to enemy blocks and commanders (Fortress.damageDealt, which
// Block and Commander keep) wins votes: DamagePerVote damage a percent, up to 100 %. They never fall again (healing does
// not take a vote back). Every TierSize % is a tier (0..MaxTier) that makes all four of his skills stronger: Damage
// grows by DamagePerTier and Radius (blast radii, pierce budgets, the shout's reach) by RadiusPerTier per tier; each
// skill may also do more at higher tiers (see its file). A TurnEffect that lives the whole battle on every Candidate
// fortress, shown under his card as a big red bar with a mark every tier; a new tier is called out over him, and his
// supporters (CandidateCrowd) gather in the background beside his fortress, more with every tier, jumping and clapping.
public class CandidateVotes : TurnEffect {
    public const float DamagePerVote = 12f;     // damage for one percent: a good hit on a keep is worth a tier
    public const int TierSize = 20, MaxTier = 5;
    public const float DamagePerTier = 0.15f, RadiusPerTier = 0.06f;

    // Testing (AutoPlaytest bench): >= 0 fixes every Candidate's tier.
    public static int ForcedTier = -1;

    float votes;   // percent
    int calledTier;

    public override string StatusText => "Votes";                     // the card's big bar shows the percent beside it
    public override Color StatusColor => CandidateKit.VoteRed;
    public override float StatusMeter => votes / 100f;

    public static CandidateVotes Of(Fortress f) {
        if (f == null) return null;
        foreach (TurnEffect e in All)
            if (e is CandidateVotes v && v.owner == f) return v;
        return null;
    }

    // Starts the count for a Candidate's fortress (once, when it is built).
    public static CandidateVotes Attach(Fortress f) {
        CandidateVotes v = Of(f);
        if (v != null || f == null) return v;
        GameObject go = new GameObject("Votes " + f.DisplayName);
        v = go.AddComponent<CandidateVotes>();
        v.Init(f, f);
        return v;
    }

    public static float Votes(Fortress f) { CandidateVotes v = Of(f); return v != null ? v.votes : 0f; }

    public static int Tier(Fortress f) {
        if (ForcedTier >= 0) return Mathf.Min(ForcedTier, MaxTier);
        return Mathf.Min(MaxTier, Mathf.FloorToInt(Votes(f) / TierSize));
    }

    public static float Damage(Fortress f) { return 1f + DamagePerTier * Tier(f); }
    public static float Radius(Fortress f) { return 1f + RadiusPerTier * Tier(f); }

    void Update() {
        if (owner == null) return;
        votes = Mathf.Max(votes, Mathf.Min(100f, owner.damageDealt / DamagePerVote));
        int tier = Mathf.Min(MaxTier, Mathf.FloorToInt(votes / TierSize));
        if (tier > calledTier) {
            calledTier = tier;
            CallOut(tier);
        }
    }

    // A new tier: the crowd cheers and the share of the vote rises over him.
    void CallOut(int tier) {
        if (!owner.CommanderAlive) return;
        Vector3 at = owner.commander.transform.position + Vector3.up * 3f;
        VFX.FloatingText(at, "VOTES " + tier * TierSize + "%!", UIKit.Lighten(CandidateKit.Teal, 0.35f), 1.3f);
        VFX.MagicBurst(owner.commander.AimPoint, CandidateKit.Teal, 0.8f);
        CandidateSounds.Play(CandidateSounds.Cheer, at, 0.9f);
    }
    // It is made when the fortress is built (turn 0), so a time rewind never cancels it; the end of the battle does.
}
