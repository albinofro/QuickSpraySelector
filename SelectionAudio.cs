using System;
using System.Reflection;
using HarmonyLib;
using Reptile;
using UnityEngine;
using UnityEngine.Audio;

namespace QuickPickGraffiti
{
    internal sealed class SelectionAudio : IDisposable
    {
        internal const int MaxVoices = 3;
        private static readonly MethodInfo randomClipMethod = AccessTools.Method(typeof(AudioManager),
            "GetRandomAudioClipFromID", new[] { typeof(SfxCollectionID), typeof(AudioClipID) });
        private static readonly FieldInfo mixerGroupsField = AccessTools.Field(typeof(AudioManager), "mixerGroups");
        private readonly Transform parent;
        private readonly Action<string> log;
        private readonly float[] startedAt = new float[MaxVoices];
        private GameObject audioRoot;
        private AudioSource[] voices;
        private AudioManager boundManager;
        private Func<SfxCollectionID, AudioClipID, AudioClip> randomClip;
        private AudioMixerGroup uiGroup;
        private bool disabled;

        internal SelectionAudio(Transform parent, Action<string> logger)
        {
            this.parent = parent;
            log = logger;
        }

        internal void Play(AudioManager manager)
        {
            if (disabled || manager == null) return;
            try
            {
                if (!ReferenceEquals(boundManager, manager))
                {
                    StopVoices();
                    if (randomClipMethod == null || mixerGroupsField == null)
                        throw new InvalidOperationException("Native UI sound routing unavailable.");
                    var groups = mixerGroupsField.GetValue(manager) as AudioMixerGroup[];
                    if (groups == null || groups.Length <= 2 || groups[2] == null)
                        throw new InvalidOperationException("Native UI sound mixer unavailable.");
                    var nextRandomClip = (Func<SfxCollectionID, AudioClipID, AudioClip>)Delegate.CreateDelegate(
                        typeof(Func<SfxCollectionID, AudioClipID, AudioClip>), manager, randomClipMethod);
                    randomClip = nextRandomClip;
                    uiGroup = groups[2];
                    boundManager = manager;
                }
                AudioClip clip = randomClip(SfxCollectionID.GraffitiSfx, AudioClipID.sprayCanPop);
                if (clip == null) return;
                if (audioRoot == null) CreateVoices();
                int index = FindVoice();
                AudioSource voice = voices[index];
                // Play owns exactly one clip per source. Reusing the oldest cannot exceed three.
                voice.Stop();
                voice.clip = clip;
                voice.outputAudioMixerGroup = uiGroup;
                voice.pitch = 1f;
                voice.Play();
                startedAt[index] = Time.unscaledTime;
            }
            catch (Exception ex)
            {
                disabled = true;
                StopVoices();
                if (log != null) log("Selector sounds disabled: " + ex.Message);
            }
        }

        private void CreateVoices()
        {
            audioRoot = new GameObject("QuickPickSelectionAudio") { hideFlags = HideFlags.HideAndDontSave };
            audioRoot.transform.SetParent(parent, false);
            voices = new AudioSource[MaxVoices];
            for (int i = 0; i < MaxVoices; i++)
            {
                AudioSource voice = audioRoot.AddComponent<AudioSource>();
                voices[i] = voice;
                voice.playOnAwake = false;
                voice.loop = false;
                voice.spatialBlend = 0f;
                voice.pitch = 1f;
                voice.volume = 1f;
                voice.ignoreListenerPause = true;
            }
        }

        private int FindVoice()
        {
            int oldest = 0;
            for (int i = 0; i < MaxVoices; i++)
            {
                if (!voices[i].isPlaying) return i;
                if (startedAt[i] < startedAt[oldest]) oldest = i;
            }
            return oldest;
        }

        private void StopVoices()
        {
            if (voices == null) return;
            for (int i = 0; i < voices.Length; i++)
                if (voices[i] != null) voices[i].Stop();
        }

        public void Dispose()
        {
            disabled = true;
            StopVoices();
            if (audioRoot != null) UnityEngine.Object.Destroy(audioRoot);
            audioRoot = null;
            voices = null;
            boundManager = null;
            randomClip = null;
            uiGroup = null;
        }
    }
}
