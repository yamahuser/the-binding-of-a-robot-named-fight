using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

// Included in the isolated playtest checkout only.
public class SamusPlaytest : MonoBehaviour
{
    private readonly HashSet<string> _captured = new HashSet<string>();
    private string _directory;
    private Player _player;
    private static readonly int DirectColor = Shader.PropertyToID("_DirectSpriteColor");

    [RuntimeInitializeOnLoadMethod]
    private static void Install()
    {
        var arguments = Environment.GetCommandLineArgs();
        var index = Array.IndexOf(arguments, "-samus-playtest");
        if (index < 0 || index + 1 >= arguments.Length) { return; }
        var observer = new GameObject("Samus Playtest");
        DontDestroyOnLoad(observer);
        var test = observer.AddComponent<SamusPlaytest>();
        test._directory = arguments[index + 1];
        Directory.CreateDirectory(test._directory);
        test.StartCoroutine(test.Capture());
    }

    private IEnumerator Capture()
    {
        var properties = new MaterialPropertyBlock();
        while (true)
        {
            yield return new WaitForEndOfFrame();
            if (!_player) { _player = FindObjectOfType<Player>(); }
            if (!_player || !_player.mainRenderer || !_player.mainRenderer.sprite) { continue; }

            if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame && !_player.spiderForm)
            {
                _player.CollectMajorItem(MajorItem.Arachnomorph);
                _player.ToggleSpiderForm();
            }

            var renderer = _player.mainRenderer;
            var sprite = renderer.sprite;
            if (sprite.texture.name != "PlayerAtlas" && sprite.texture.name != "SpiderAtlas") { continue; }
            string label;
            if (sprite.name.StartsWith("SpiderForm_")) { label = "morph-ball"; }
            else if (int.TryParse(sprite.name.Substring("PlayerBase_".Length), out int index))
            {
                if (index < 4) { label = "standing"; }
                else if (index >= 20 && index < 40) { label = "running"; }
                else if (index >= 40 && index < 100) { label = "running-aim"; }
                else if (index >= 100 && index < 103 || index >= 120 && index < 123) { label = "jump"; }
                else { continue; }
            }
            else { continue; }
            renderer.GetPropertyBlock(properties);
            bool mirrored = renderer.transform.TransformVector(Vector3.right).x < 0f;
            label += mirrored ? "-left" : "-right";
            if (!_captured.Add(label)) { continue; }
            string result = $"{label}: {sprite.name}, {sprite.texture.name}, {sprite.rect.size}, direct color {properties.GetFloat(DirectColor)}";
            File.AppendAllLines(Path.Combine(_directory, "pose-results.txt"), new[] { result });
            ScreenCapture.CaptureScreenshot(Path.Combine(_directory, label + ".png"));
        }
    }
}
