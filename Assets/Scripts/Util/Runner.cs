using System;
using System.Collections;
using UnityEngine;

// Lets static code run coroutines and delayed calls. One hidden instance per scene (recreated on demand).
public class Runner : MonoBehaviour {
    static Runner instance;

    static Runner I {
        get {
            if (instance == null) instance = new GameObject("[Runner]").AddComponent<Runner>();
            return instance;
        }
    }

    public static Coroutine Run(IEnumerator routine) { return I.StartCoroutine(routine); }

    public static void Delay(float seconds, Action action) { I.StartCoroutine(DelayRoutine(seconds, action)); }

    public static void DelayRealtime(float seconds, Action action) { I.StartCoroutine(DelayRealtimeRoutine(seconds, action)); }

    static IEnumerator DelayRoutine(float s, Action a) {
        yield return new WaitForSeconds(s);
        a?.Invoke();
    }

    static IEnumerator DelayRealtimeRoutine(float s, Action a) {
        yield return new WaitForSecondsRealtime(s);
        a?.Invoke();
    }
}
