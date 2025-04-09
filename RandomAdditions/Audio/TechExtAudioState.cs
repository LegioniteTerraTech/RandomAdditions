using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FMOD;
using FMODUnity;

namespace RandomAdditions
{
    internal class HelperGUI : MonoBehaviour
    {
        static HelperGUI helpGUI = null;
        private Rect Window = new Rect(0, 0, 280, 145);

        public static float value = 50f;
        public static float value2 = 50f;
        public static float value3 = 1f;

        public static void Init()
        {
            if (helpGUI == null)
                helpGUI = new GameObject().AddComponent<HelperGUI>();
        }
        private void OnGUI()
        {
            try
            {
                Window = GUI.Window(2958715, Window, GUIWindow, "Settings");
            }
            catch { }
        }
        private void GUIWindow(int ID)
        {
            GUILayout.Label("Value: " + value.ToString("F"));
            value = GUILayout.HorizontalSlider(value, 0, 300f);
            GUILayout.Label("Value2: " + value2.ToString("F"));
            value2 = GUILayout.HorizontalSlider(value2, 0, 300f);
            GUILayout.Label("Value3: " + value3.ToString("F"));
            value3 = GUILayout.HorizontalSlider(value3, 0, 10f);

            GUI.DragWindow();
        }
    }
    public class TechExtAudioStateWithLoopedFrequency
    {
        const float speedSound = 100f;

        private Sound sound;
        internal Channel ActiveAudio;
        private float Freq;
        private bool drivePaused = false;

        public TechExtAudioStateWithLoopedFrequency(Sound sfx)
        {
            sound = sfx;
            Freq = 1;
        }
        public void ResetFrequency()
        {
            Freq = 0;
        }
        public void UpdateFrequency(float freq)
        {
            if (freq > Freq)
                Freq = Mathf.Lerp(Freq, freq, 0.125f);
            else
                Freq = Mathf.Lerp(Freq, freq, 0.05f);
        }

        public void UpdateThis(Vector3 position)
        {
            try
            {
                VECTOR pos = position.ToFMODVector();
                //VECTOR velo = velocity.ToFMODVector();
                Vector3 sounder = (Camera.main.transform.position - position).normalized;
                VECTOR velo;
                // Use the doppler effect as a sort of makeshift pitch driver lol 
                if (Freq > 1)
                    velo = (sounder * ((Freq * Freq * speedSound) - speedSound)).ToFMODVector();
                else
                    velo = (sounder * ((-speedSound / Mathf.Max(0.01f, Freq)) + speedSound)).ToFMODVector();
                VECTOR ignored = Vector3.zero.ToFMODVector();
                ActiveAudio.set3DAttributes(ref pos, ref velo, ref ignored);
            }
            catch (Exception e)
            {
                TechExtAudio.debugEncapsulated("UpdateThis(2) set - " + e);
            }
        }
        public void UpdateVolume(float volume)
        {
            try
            {
                ActiveAudio.setVolume(volume);
            }
            catch (Exception e)
            {
                TechExtAudio.debugEncapsulated("UpdateVolume(2) set - " + e);
            }
        }
        public void PlayLooped(float volume, bool restart = true, bool playNow = true)
        {
            ActiveAudio.getPaused(out bool currentlyPaused);
            if (currentlyPaused)
                ActiveAudio.setPaused(false);
            bool currentlyPlaying;
            ActiveAudio.isPlaying(out currentlyPlaying);
            if (restart)
            {
                if (currentlyPlaying)
                {
                    ActiveAudio.stop();
                }
                ManMusicEnginesExt.StartSoundLooping(sound, ref ActiveAudio, volume, playNow);
            }
            else
            {
                if (!currentlyPlaying)
                {
                    ManMusicEnginesExt.StartSoundLooping(sound, ref ActiveAudio, volume, playNow);
                }
            }
        }
        public void Stop()
        {
            ActiveAudio.stop();
        }
        public void Pause(bool paused)
        {
            if (paused)
                ActiveAudio.setPaused(paused);
            else if (!drivePaused)
                ActiveAudio.setPaused(false);
        }
    }
    public class TechExtAudioState
    {
        private Sound sound;
        internal Channel ActiveAudio;
        private bool loop = false;

        public TechExtAudioState(Sound sfx, bool loop)
        {
            sound = sfx;
            this.loop = loop;
        }

        public void UpdateThis(Vector3 position)
        {
            try
            {
                VECTOR pos = position.ToFMODVector();
                VECTOR ignored = Vector3.zero.ToFMODVector();
                ActiveAudio.set3DAttributes(ref pos, ref ignored, ref ignored);
            }
            catch (Exception e)
            {
                TechExtAudio.debugEncapsulated("UpdateThis set - " + e);
            }
        }
        public void UpdateVolume(float volume)
        {
            try
            {
                ActiveAudio.setVolume(volume);
            }
            catch (Exception e)
            {
                TechExtAudio.debugEncapsulated("UpdateVolume set - " + e);
            }
        }
        public void Play(float volume, bool restart = true)
        {
            ActiveAudio.isPlaying(out bool currentlyPlaying);
            if (restart)
            {
                if (currentlyPlaying)
                {
                    ActiveAudio.stop();
                }
                if (loop)
                    ManMusicEnginesExt.StartSoundLooping(sound, ref ActiveAudio, volume);
                else
                    ManMusicEnginesExt.StartSound(sound, ref ActiveAudio, volume);
            }
            else
            {
                if (!currentlyPlaying)
                {
                    ManMusicEnginesExt.StartSound(sound, ref ActiveAudio, volume);
                }
            }
        }
        public void PlayLooped(float volume, bool restart = true)
        {
            ActiveAudio.getPaused(out bool currentlyPaused);
            if (currentlyPaused)
                ActiveAudio.setPaused(false);
            ActiveAudio.isPlaying(out bool currentlyPlaying);
            if (restart)
            {
                if (currentlyPlaying)
                {
                    ActiveAudio.stop();
                }
                loop = true;
                ManMusicEnginesExt.StartSoundLooping(sound, ref ActiveAudio, volume);
            }
            else
            {
                if (!currentlyPlaying || !ActiveAudio.hasHandle())
                {
                    loop = true;
                    ManMusicEnginesExt.StartSoundLooping(sound, ref ActiveAudio, volume);
                }
            }
        }
        public void Stop()
        {
            ActiveAudio.stop();
        }
        public void Pause(bool paused)
        {
            ActiveAudio.setPaused(paused);
        }
    }
}