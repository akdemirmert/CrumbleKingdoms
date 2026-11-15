using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;

// Development check of fortress physics without playing: builds a fortress on a flat ground box in an
// empty scene, lets it settle, then hits it (a boulder-sized impulse, then a bomb-sized blast) and logs
// how many blocks were displaced. Run: -executeMethod PhysicsProbe.Run
public static class PhysicsProbe {
    public static void Run() {
        foreach (FortressStyle style in new[] { FortressStyle.Outpost, FortressStyle.Watchtower, FortressStyle.Keep, FortressStyle.Bastion, FortressStyle.Ziggurat, FortressStyle.Citadel })
            foreach (int tier in new[] { 0, 2 })
                Probe(style, tier);
    }

    static void Probe(FortressStyle style, int tier) {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Physics.simulationMode = SimulationMode.Script;
        Physics.gravity = new Vector3(0f, -World.BaseGravity, 0f);
        Physics.defaultSolverIterations = 12;
        Physics.defaultSolverVelocityIterations = 4;
        Physics.sleepThreshold = 0.02f;

        GameObject ground = new GameObject("Ground") { layer = Layers.Ground };
        ground.transform.position = new Vector3(0f, -15f, 2f);
        ground.AddComponent<BoxCollider>().size = new Vector3(40f, 30f, 14f);

        FortressSetup s = new FortressSetup {
            team = 0, name = "Probe", heroClass = HeroClass.Soldier, controller = ControllerType.Bot,
            style = style, tier = tier, glass = true, seed = 7, x = 0f, groundY = 0f, facing = 1, commanderHP = 100f,
            teamColor = Color.blue,
        };
        Fortress f = Fortress.Build(s, 0);
        string name = style + " t" + tier;
        Step(60);
        Dictionary<Block, Vector3> start = Positions(f);
        Report(name + " settled  ", f, start);

        // A boulder hit (4 kg at 25 m/s) on the enemy-facing wall at mid height.
        Block wall = Outermost(f, 1);
        if (wall != null) wall.rb.AddForce(new Vector3(-4.5f, 0f, 0f), ForceMode.VelocityChange);   // as capped by Block.MaxShove
        Step(180);
        Report(name + " boulder  ", f, start);

        // A bomb blast at the same wall face.
        Bounds b = f.GetBounds();
        Explosion.Push(new Vector3(b.max.x + 0.3f, b.center.y, 0f), 3.6f, 14f, 0.35f, out _);
        Step(180);
        Report(name + " +bomb    ", f, start);
        Physics.simulationMode = SimulationMode.FixedUpdate;
    }

    static void Step(int n) { for (int i = 0; i < n; i++) Physics.Simulate(1f / 60f); }

    static Dictionary<Block, Vector3> Positions(Fortress f) {
        Dictionary<Block, Vector3> d = new Dictionary<Block, Vector3>();
        foreach (Block b in f.blocks) if (b != null) d[b] = b.transform.position;
        return d;
    }

    static Block Outermost(Fortress f, int side) {
        Bounds bb = f.GetBounds();
        Block best = null;
        float bestScore = float.MinValue;
        foreach (Block b in f.blocks) {
            if (b == null) continue;
            float score = b.transform.position.x * side - Mathf.Abs(b.transform.position.y - bb.center.y) * 0.5f;
            if (score > bestScore) { bestScore = score; best = b; }
        }
        return best;
    }

    static void Report(string label, Fortress f, Dictionary<Block, Vector3> start) {
        int moved = 0, fallen = 0;
        foreach (var kv in start) {
            if (kv.Key == null) continue;
            Vector3 d = kv.Key.transform.position - kv.Value;
            if (d.magnitude > 0.5f) moved++;
            if (d.y < -1f) fallen++;
        }
        float cmdDrop = f.commander != null ? f.commander.transform.position.y - (f.transform.position.y + f.blueprint.commanderSpot.y) : 0f;
        Debug.Log("[Probe] " + label + ": blocks " + start.Count + "  moved>0.5m " + moved + "  dropped>1m " + fallen +
                  "  commander dy " + cmdDrop.ToString("0.00"));
    }
}
