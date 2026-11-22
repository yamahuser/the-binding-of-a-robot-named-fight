using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// [MODCONTENT] Pressing Play in the editor always boots the real game from the first
// build scene, whatever scene happens to be open (the editor starts on an empty
// Untitled scene, where Play does nothing). Toggle via Mod Content > Play From Boot Scene.
[InitializeOnLoad]
public static class ModContentPlayFromBoot
{
    private const string PrefKey = "ModContent.PlayFromBoot";
    private const string MenuPath = "Mod Content/Play From Boot Scene";

    static ModContentPlayFromBoot()
    {
        EditorApplication.delayCall += Apply;
    }

    private static bool Enabled
    {
        get { return EditorPrefs.GetBool(PrefKey, true); }
        set { EditorPrefs.SetBool(PrefKey, value); }
    }

    private static void Apply()
    {
        if (!Enabled)
        {
            EditorSceneManager.playModeStartScene = null;
            return;
        }

        foreach (var s in EditorBuildSettings.scenes)
        {
            if (!s.enabled) continue;
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(s.path);
            if (scene != null)
            {
                EditorSceneManager.playModeStartScene = scene;
                Debug.Log("[MODCONTENT] Play will boot from " + s.path);
            }
            return;
        }
    }

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        Enabled = !Enabled;
        Apply();
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }
}
