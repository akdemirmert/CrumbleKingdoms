using System.Collections.Generic;
using UnityEngine;

// Point-mass trajectories with the same integration Unity's physics step uses
// (semi-implicit Euler: v += a·dt, then p += v·dt), so previews and bot aim match real flights.
public static class Ballistics {
    public static float Dt => World.FixedStep;

    public static Vector3 Accel(float gravityScale, float windAccel) {
        return new Vector3(windAccel, -World.Gravity * gravityScale, 0f);
    }

    // Samples points along the path (every `every` steps) for up to maxTime seconds.
    public static void Trace(Vector3 p, Vector3 v, float gravityScale, float windAccel, float maxTime, List<Vector3> outPts, int every = 3) {
        outPts.Clear();
        Vector3 a = Accel(gravityScale, windAccel);
        int steps = Mathf.CeilToInt(maxTime / Dt);
        outPts.Add(p);
        for (int i = 1; i <= steps; i++) {
            v += a * Dt;
            p += v * Dt;
            if (i % every == 0) outPts.Add(p);
            if (p.y < World.KillY) break;
        }
    }

    // Height of the path where it crosses x = targetX, or NaN if it never gets there.
    public static float HeightAtX(Vector3 p, Vector3 v, float gravityScale, float windAccel, float targetX, float maxTime = 9f) {
        Vector3 a = Accel(gravityScale, windAccel);
        int dir = targetX >= p.x ? 1 : -1;
        int steps = Mathf.CeilToInt(maxTime / Dt);
        for (int i = 0; i < steps; i++) {
            Vector3 prev = p;
            v += a * Dt;
            p += v * Dt;
            if ((p.x - targetX) * dir >= 0f) {
                float t = Mathf.Abs(p.x - prev.x) > 1e-5f ? (targetX - prev.x) / (p.x - prev.x) : 1f;
                return Mathf.Lerp(prev.y, p.y, t);
            }
            if (p.y < World.KillY - 10f) return float.NaN;
        }
        return float.NaN;
    }

    // Finds a launch velocity (speed within the weapon's range × speedScale) that hits `target`.
    // high = a lob: the workable arc nearest HighAngle (not the steepest one, which climbs far out of sight and hangs for
    // seconds in the wind); otherwise the flattest one above ~12°.
    // Weapons without gravity (the ray gun) simply point straight at the target.
    const float HighAngle = 55f;

    public static bool Solve(Vector3 origin, Vector3 target, WeaponDef def, float windAccel, bool high, out Vector3 velocity, float speedScale = 1f) {
        if (def.gravityScale <= 0.001f) {
            Vector3 to = target - origin;
            to.z = 0f;
            velocity = to.normalized * def.maxSpeed * speedScale;
            return true;
        }
        int dir = target.x >= origin.x ? 1 : -1;
        float wind = windAccel * def.windFactor;
        float bestAngle = -1f, bestSpeed = 0f;
        for (float angle = 6f; angle <= 82f; angle += 1f) {
            Vector3 unit = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * dir, Mathf.Sin(angle * Mathf.Deg2Rad), 0f);
            float lo = def.minSpeed * speedScale, hi = def.maxSpeed * speedScale;
            float hLo = HeightAtX(origin, unit * lo, def.gravityScale, wind, target.x);
            float hHi = HeightAtX(origin, unit * hi, def.gravityScale, wind, target.x);
            if (float.IsNaN(hHi) || hHi < target.y) continue;               // too weak even at full power
            if (!float.IsNaN(hLo) && hLo > target.y) continue;              // overshoots even at minimum power
            for (int it = 0; it < 22; it++) {
                float mid = (lo + hi) * 0.5f;
                float h = HeightAtX(origin, unit * mid, def.gravityScale, wind, target.x);
                if (float.IsNaN(h) || h < target.y) lo = mid; else hi = mid;
            }
            float speed = (lo + hi) * 0.5f;
            bool better = bestAngle < 0f || (high ? Mathf.Abs(angle - HighAngle) < Mathf.Abs(bestAngle - HighAngle)
                                                  : (angle >= 12f && (bestAngle < 12f || angle < bestAngle)));
            if (better) { bestAngle = angle; bestSpeed = speed; }
        }
        if (bestAngle < 0f) {
            velocity = new Vector3(Mathf.Cos(45f * Mathf.Deg2Rad) * dir, Mathf.Sin(45f * Mathf.Deg2Rad), 0f) * def.maxSpeed * speedScale;
            return false;
        }
        velocity = new Vector3(Mathf.Cos(bestAngle * Mathf.Deg2Rad) * dir, Mathf.Sin(bestAngle * Mathf.Deg2Rad), 0f) * bestSpeed;
        return true;
    }

    // Angle (0 = straight at the enemy, 90 = up) and speed -> velocity.
    public static Vector3 FromAngle(float angleDeg, float speed, int facing) {
        float r = angleDeg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(r) * facing, Mathf.Sin(r), 0f) * speed;
    }

    public static float AngleOf(Vector3 v, int facing) {
        return Mathf.Atan2(v.y, v.x * facing) * Mathf.Rad2Deg;
    }
}
