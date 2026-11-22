using System.IO;
using UnityEditor;
using UnityEngine;

// [MODCONTENT] Lets tools start Play mode in an already-open editor: create the file
// Temp/modcontent_play (relative to the project folder) and the editor enters Play
// within a second. Create Temp/modcontent_stop to leave Play mode.
[InitializeOnLoad]
public static class ModContentAutoPlay
{
    private const string PlayFlag = "Temp/modcontent_play";
    private const string StopFlag = "Temp/modcontent_stop";
    private static double _next;

    static ModContentAutoPlay()
    {
        EditorApplication.update += Poll;
    }

    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < _next) { return; }
        _next = EditorApplication.timeSinceStartup + 0.5;

        if (File.Exists(PlayFlag))
        {
            File.Delete(PlayFlag);
            if (!EditorApplication.isPlaying)
            {
                Debug.Log("[MODCONTENT] Auto-play requested");
                EditorApplication.isPlaying = true;
            }
        }

        if (File.Exists(StopFlag))
        {
            File.Delete(StopFlag);
            if (EditorApplication.isPlaying) { EditorApplication.isPlaying = false; }
        }
    }
}
