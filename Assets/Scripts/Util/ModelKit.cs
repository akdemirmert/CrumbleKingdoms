using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Builds visual-only models out of primitive meshes (no colliders are ever added here).
public static class ModelKit {
    static readonly Dictionary<PrimitiveType, Mesh> prims = new Dictionary<PrimitiveType, Mesh>();

    public static Mesh Primitive(PrimitiveType type) {
        if (prims.TryGetValue(type, out Mesh m) && m != null) return m;
        GameObject tmp = GameObject.CreatePrimitive(type);
        m = tmp.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(tmp);
        prims[type] = m;
        return m;
    }

    public static GameObject Root(string name, Transform parent = null) {
        GameObject go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    public static GameObject Part(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 localScale, Material mat, Vector3 localEuler = default) {
        return MeshPart(parent, Primitive(type), localPos, localScale, mat, localEuler, type.ToString());
    }

    public static GameObject MeshPart(Transform parent, Mesh mesh, Vector3 localPos, Vector3 localScale, Material mat, Vector3 localEuler = default, string name = "Part") {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(localEuler);
        go.transform.localScale = localScale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        if (parent != null) go.layer = parent.gameObject.layer;
        return go;
    }

    public static void SetLayer(GameObject go, int layer) {
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }

    public static void Shadows(GameObject go, bool cast, bool receive = true) {
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) {
            r.shadowCastingMode = cast ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = receive;
        }
    }
}
