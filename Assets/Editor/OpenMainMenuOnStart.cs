using UnityEditor;
using UnityEditor.SceneManagement;

// When the project is opened with no scene loaded (a fresh copy has none remembered), open the main menu
// scene, so pressing Play starts the game from its first screen.
[InitializeOnLoad]
static class OpenMainMenuOnStart {
    const string MainMenu = "Assets/Scenes/MainMenu.unity";

    static OpenMainMenuOnStart() {
        if (UnityEngine.Application.isBatchMode || SessionState.GetBool("SiegeMainMenuOpened", false)) return;
        EditorApplication.delayCall += Open;
    }

    static void Open() {
        SessionState.SetBool("SiegeMainMenuOpened", true);
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!string.IsNullOrEmpty(EditorSceneManager.GetActiveScene().path)) return;   // a scene is already open
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenu) != null) EditorSceneManager.OpenScene(MainMenu);
    }
}
