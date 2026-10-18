using System.Collections.Generic;
using UnityEngine;

/// <summary>Recorded voice lines for this scene, matched to the subtitle text they speak (filled by the builder).</summary>
public class VoiceBank : MonoBehaviour
{
    [TextArea] public string[] texts = new string[0];
    public AudioClip[] clips = new AudioClip[0];

    static VoiceBank _inst;
    Dictionary<string, AudioClip> _map;

    void Awake() => _inst = this;

    static string Key(string s) => s.Trim().ToLowerInvariant();

    public static AudioClip Find(string text)
    {
        if (!_inst || string.IsNullOrEmpty(text)) return null;
        if (_inst._map == null)
        {
            _inst._map = new Dictionary<string, AudioClip>();
            for (int i = 0; i < _inst.texts.Length && i < _inst.clips.Length; i++)
                if (_inst.clips[i]) _inst._map[Key(_inst.texts[i])] = _inst.clips[i];
        }
        return _inst._map.TryGetValue(Key(text), out var c) ? c : null;
    }
}
