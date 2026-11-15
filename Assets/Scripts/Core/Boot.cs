using UnityEngine;

// Runs before the first scene: physics and time settings live in code so the project setup is visible
// in one place (gravity, solver quality, which layers collide).
public static class Boot {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init() {
        Physics.gravity = new Vector3(0f, -World.Gravity, 0f);
        Physics.defaultSolverIterations = 12;
        Physics.defaultSolverVelocityIterations = 4;
        Physics.bounceThreshold = 1.5f;
        Physics.sleepThreshold = 0.02f;
        Physics.defaultMaxAngularSpeed = 40f;
        Time.fixedDeltaTime = World.FixedStep;
        Time.maximumDeltaTime = 0.1f;

        // The whole layer matrix is set here: everything collides except the pairs below, whatever the
        // project's saved physics settings say (a stale matrix there once let blocks fall through the ground).
        for (int a = 0; a < 32; a++)
            for (int b = a; b < 32; b++) Physics.IgnoreLayerCollision(a, b, false);
        Ignore(Layers.Projectile, Layers.Projectile);
        Ignore(Layers.Projectile, Layers.Debris);
        Ignore(Layers.Debris, Layers.Debris);
        Ignore(Layers.Debris, Layers.Commander);
        for (int l = 0; l < 32; l++) { Ignore(Layers.Effect, l); Ignore(Layers.IconStage, l); }
        Physics.IgnoreLayerCollision(Layers.IconStage, Layers.IconStage, true);

        Application.targetFrameRate = 120;
        GameSettings.Apply();
    }

    static void Ignore(int a, int b) { Physics.IgnoreLayerCollision(a, b, true); }
}
