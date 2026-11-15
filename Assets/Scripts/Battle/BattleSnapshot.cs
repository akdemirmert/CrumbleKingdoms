using System.Collections.Generic;
using UnityEngine;

// The state of the battle at the start of a turn, so Dalí's melting clocks can rewind time:
// every block (place, rotation, health), every commander (place, health, alive), cooldowns and defeats.
// Restoring puts surviving blocks back where they were and rebuilds the broken ones.
public class BattleSnapshot {
    public int turn;

    class BlockRecord {
        public Block block;               // the live block at the time (may be gone by the rewind)
        public BlockMaterial material;
        public Vector3 size, localPosition;
        public Quaternion rotation;
        public float hp;
        public Vector3 velocity, spin;    // a wall still sagging at that moment carries on sagging after the rewind
        public bool asleep, charred;
    }

    class FortressRecord {
        public Fortress fortress;
        public readonly List<BlockRecord> blocks = new List<BlockRecord>();
        public Vector3 commanderPosition;
        public float commanderHP;
        public bool defeated;
        public readonly int[] cooldowns = new int[4];
        public int selectedSlot;
    }

    readonly List<FortressRecord> records = new List<FortressRecord>();
    readonly Dictionary<TurnEffect, int> effects = new Dictionary<TurnEffect, int>();   // their RewindState

    public static BattleSnapshot Take(BattleManager bm) {
        BattleSnapshot s = new BattleSnapshot { turn = bm.Turn };
        foreach (TurnEffect e in TurnEffect.All) if (e != null) s.effects[e] = e.RewindState;
        foreach (Fortress f in bm.fortresses) {
            FortressRecord r = new FortressRecord { fortress = f, defeated = f.defeated, selectedSlot = f.selectedSlot };
            System.Array.Copy(f.cooldowns, r.cooldowns, 4);
            foreach (Block b in f.blocks) {
                if (b == null || !b.IsAlive) continue;
                r.blocks.Add(new BlockRecord {
                    block = b, material = b.material, size = b.size, hp = b.hp,
                    localPosition = f.transform.InverseTransformPoint(b.transform.position),
                    rotation = b.transform.rotation,
                    velocity = b.rb.linearVelocity, spin = b.rb.angularVelocity, asleep = b.rb.IsSleeping(), charred = b.Charred,
                });
            }
            if (f.commander != null) {
                r.commanderPosition = f.commander.transform.position;
                r.commanderHP = f.commander.IsAlive ? f.commander.hp : 0f;
            }
            s.records.Add(r);
        }
        return s;
    }

    // keepCooldownsOf: the fortress that triggered the rewind keeps its current cooldowns (no free reuse).
    public void Restore(Fortress keepCooldownsOf) {
        // Everything thrown or broken since then disappears: every projectile still in the world, also the spent
        // ones that linger (a flare burning on the ground, a dagger in the wall), not only those still flying.
        foreach (Projectile p in Object.FindObjectsByType<Projectile>()) if (p != null) Object.Destroy(p.gameObject);
        Projectile.Active.Clear();
        foreach (KeyValuePair<TurnEffect, int> e in effects) if (e.Key != null) e.Key.RewindState = e.Value;
        foreach (Debris d in Object.FindObjectsByType<Debris>()) Object.Destroy(d.gameObject);
        foreach (Embedded e in Object.FindObjectsByType<Embedded>()) Object.Destroy(e.gameObject);

        foreach (FortressRecord r in records) {
            Fortress f = r.fortress;
            if (f == null) continue;
            List<Block> now = new List<Block>();
            foreach (BlockRecord br in r.blocks) {
                Block b = br.block;
                Vector3 pos = f.transform.TransformPoint(br.localPosition);
                if (b != null && b.IsAlive) {
                    b.rb.linearVelocity = Vector3.zero;
                    b.rb.angularVelocity = Vector3.zero;
                    b.rb.position = pos;
                    b.rb.rotation = br.rotation;
                    b.transform.SetPositionAndRotation(pos, br.rotation);
                    b.Extinguish();
                } else {
                    b = Block.Create(new BlockSpec(br.localPosition, br.size, br.material), f, f.transform);
                    b.transform.rotation = br.rotation;
                }
                b.RestoreHP(br.hp);
                b.Charred = br.charred;
                if (br.asleep) b.rb.Sleep();
                else {
                    b.rb.WakeUp();
                    b.rb.linearVelocity = br.velocity;
                    b.rb.angularVelocity = br.spin;
                }
                br.block = b;   // a second rewind to this moment finds the rebuilt block
                now.Add(b);
            }
            // Blocks that did not exist at that moment go away.
            foreach (Block b in f.blocks) {
                if (b == null || now.Contains(b)) continue;
                if (b.box != null) b.box.enabled = false;
                if (b.rb != null) b.rb.detectCollisions = false;
                Object.Destroy(b.gameObject);
            }
            f.blocks.Clear();
            f.blocks.AddRange(now);
            Physics.SyncTransforms();
            f.MarkDirty();
            f.defeated = r.defeated;
            if (f != keepCooldownsOf) {
                System.Array.Copy(r.cooldowns, f.cooldowns, 4);
                f.selectedSlot = r.selectedSlot;
            }
            if (f.commander != null && r.commanderHP > 0f) f.commander.Restore(r.commanderPosition, r.commanderHP);
        }
    }
}
