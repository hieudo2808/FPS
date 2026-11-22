#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FPS.Tests
{
    /// <summary>
    /// Records what the game shows and plays, for reviewing sound against picture: frames of the Game view (or of a
    /// review camera) packed into JPEG contact sheets, plus every voice started/ended on this peer with its clip,
    /// gain, pitch and its distance/pan from the camera. Timestamps share one real-time clock.
    /// </summary>
    [DefaultExecutionOrder(-32000)]
    public sealed class AvRecorder : MonoBehaviour
    {
        public const int W = 640, H = 360, Cols = 5, Rows = 4;
        const float Fps = 20f;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        string root, key, title, music;
        Camera review;
        Transform listener;
        RenderTexture screenRT, frameRT;
        Texture2D sheet;
        int cell, sheetIndex;
        double start, lastFrame;
        bool recording;
        readonly List<double> frames = new();
        readonly List<string> events = new();
        readonly List<string> sheets = new();
        public readonly List<string> Written = new();

        // Video takes: every frame at VideoFps as its own JPEG plus the mixed audio the listener hears, as a WAV.
        public const int VideoW = 1280;
        const float VideoFps = 30f;
        bool video;
        RenderTexture videoRT;
        AudioTap tap;
        int videoFrame, pendingWrites;
        string videoDir;

        public void Init(string directory)
        {
            root = directory;
            Directory.CreateDirectory(root);
            frameRT = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            sheet = new Texture2D(W * Cols, H * Rows, TextureFormat.RGB24, false);
            GameAudio.VoiceStarted += OnStart;
            GameAudio.VoiceEnded += OnEnd;
            StartCoroutine(Loop());
        }

        void OnDestroy()
        {
            GameAudio.VoiceStarted -= OnStart;
            GameAudio.VoiceEnded -= OnEnd;
            if (screenRT != null) screenRT.Release();
            if (frameRT != null) frameRT.Release();
            if (videoRT != null) videoRT.Release();
            if (tap != null) Destroy(tap);
        }

        /// <summary>Start a video take of the Game view: frames to key/NNNNN.jpg, the listener's mix to key.wav.</summary>
        public void BeginVideo(string sceneKey, string sceneTitle)
        {
            EditorUtility.audioMasterMute = false;
            Begin(sceneKey, sceneTitle, null);
            video = true;
            videoFrame = 0;
            int h = Mathf.RoundToInt(VideoW * (float)Screen.height / Mathf.Max(1, Screen.width)) & ~1;
            if (videoRT == null || videoRT.height != h)
            {
                if (videoRT != null) videoRT.Release();
                videoRT = new RenderTexture(VideoW, h, 0, RenderTextureFormat.ARGB32);
            }
            videoDir = Path.Combine(root, sceneKey);
            if (Directory.Exists(videoDir)) Directory.Delete(videoDir, true);
            Directory.CreateDirectory(videoDir);
            if (listener != null)
            {
                tap = listener.GetComponent<AudioTap>();
                if (tap == null) tap = listener.gameObject.AddComponent<AudioTap>();
                tap.Begin(AudioSettings.dspTime);
            }
        }

        static double Now => Time.realtimeSinceStartupAsDouble;

        // Video frames are stamped with the start of the frame they show (this runs before every other Update),
        // the same clock the audio events are logged on, not with the end-of-frame moment they are read back.
        double frameStart;
        void Update() => frameStart = Now;

        /// <summary>Start a take. reviewCamera null = the Game view as the local player sees it.</summary>
        public void Begin(string sceneKey, string sceneTitle, Camera reviewCamera)
        {
            key = sceneKey;
            title = sceneTitle;
            review = reviewCamera;
            if (review != null) review.targetTexture = frameRT;
            listener = null;
            foreach (var candidate in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (candidate.isActiveAndEnabled) { listener = candidate.transform; break; }
            music = GameAudio.CurrentMusic.ToString();
            frames.Clear(); events.Clear(); sheets.Clear();
            cell = 0; sheetIndex = 0;
            start = Now;
            lastFrame = -1;
            recording = true;
        }

        public IEnumerator End()
        {
            yield return new WaitForEndOfFrame();
            recording = false;
            string audioFile = null;
            int audioRate = 0, audioChannels = 0;
            double audioSeconds = 0, audioLead = 0;
            if (video)
            {
                video = false;
                if (tap != null)
                {
                    float[] samples = tap.End(out audioRate, out audioChannels, out audioLead);
                    audioFile = key + ".wav";
                    WriteWav(Path.Combine(root, audioFile), samples, audioRate, audioChannels);
                    audioSeconds = audioChannels > 0 ? samples.Length / (double)audioChannels / audioRate : 0;
                }
                float until = Time.realtimeSinceStartup + 20f;
                while (System.Threading.Volatile.Read(ref pendingWrites) > 0 && Time.realtimeSinceStartup < until) yield return null;
            }
            if (cell > 0) FlushSheet();
            if (review != null) review.targetTexture = null;
            var json = new StringBuilder();
            json.Append("{\"key\":\"").Append(key).Append("\",\"title\":").Append(Quote(title))
                .Append(",\"audio\":").Append(audioFile != null ? Quote(audioFile) : "null")
                .Append(",\"audioRate\":").Append(audioRate).Append(",\"audioChannels\":").Append(audioChannels)
                .Append(",\"audioSeconds\":").Append(audioSeconds.ToString("0.###", Inv))
                .Append(",\"audioLead\":").Append(audioLead.ToString("0.####", Inv))
                .Append(",\"audioStats\":").Append(Quote(tap != null ? tap.Stats : ""))
                .Append(",\"videoW\":").Append(videoRT != null ? videoRT.width : 0).Append(",\"videoH\":").Append(videoRT != null ? videoRT.height : 0)
                .Append(",\"music\":\"").Append(music).Append("\",\"w\":").Append(W).Append(",\"h\":").Append(H)
                .Append(",\"cols\":").Append(Cols).Append(",\"rows\":").Append(Rows)
                .Append(",\"view\":\"").Append(review != null ? "3P" : "1P").Append("\",\"frames\":[");
            for (int i = 0; i < frames.Count; i++) json.Append(i > 0 ? "," : "").Append(frames[i].ToString("0.###", Inv));
            json.Append("],\"sheets\":[");
            for (int i = 0; i < sheets.Count; i++) json.Append(i > 0 ? "," : "").Append(Quote(sheets[i]));
            json.Append("],\"events\":[").Append(string.Join(",", events)).Append("]}");
            string path = Path.Combine(root, key + ".json");
            File.WriteAllText(path, json.ToString());
            Written.Add(path);
            Debug.Log($"[AvRecorder] {key}: {frames.Count} frames, {sheets.Count} sheets, {events.Count} audio events");
        }

        IEnumerator Loop()
        {
            var endOfFrame = new WaitForEndOfFrame();
            while (true)
            {
                yield return endOfFrame;
                if (!recording || Now - lastFrame < 1.0 / (video ? VideoFps * 1.05 : Fps)) continue;
                lastFrame = Now;
                if (video) CaptureVideoFrame();
                else Capture();
            }
        }

        void CaptureVideoFrame()
        {
            if (screenRT == null || screenRT.width != Screen.width || screenRT.height != Screen.height)
            {
                if (screenRT != null) screenRT.Release();
                screenRT = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
            }
            ScreenCapture.CaptureScreenshotIntoRenderTexture(screenRT);
            if (SystemInfo.graphicsUVStartsAtTop) Graphics.Blit(screenRT, videoRT, new Vector2(1f, -1f), new Vector2(0f, 1f));
            else Graphics.Blit(screenRT, videoRT);
            string file = Path.Combine(videoDir, $"{videoFrame++:00000}.jpg");
            int w = videoRT.width, h = videoRT.height;
            frames.Add(Now - start);
            System.Threading.Interlocked.Increment(ref pendingWrites);
            frames[frames.Count - 1] = frameStart - start;
            AsyncGPUReadback.Request(videoRT, 0, TextureFormat.RGB24, request =>
            {
                if (request.hasError) { System.Threading.Interlocked.Decrement(ref pendingWrites); return; }
                byte[] raw = request.GetData<byte>().ToArray();
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        byte[] jpg = ImageConversion.EncodeArrayToJPG(raw, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8_SRGB,
                            (uint)w, (uint)h, 0, 88);
                        File.WriteAllBytes(file, jpg);
                    }
                    finally { System.Threading.Interlocked.Decrement(ref pendingWrites); }
                });
            });
        }

        static void WriteWav(string path, float[] samples, int rate, int channels)
        {
            using var stream = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(stream);
            int bytes = samples.Length * 2;
            w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes); w.Write(Encoding.ASCII.GetBytes("WAVE"));
            w.Write(Encoding.ASCII.GetBytes("fmt ")); w.Write(16); w.Write((short)1); w.Write((short)channels);
            w.Write(rate); w.Write(rate * channels * 2); w.Write((short)(channels * 2)); w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data")); w.Write(bytes);
            foreach (float s in samples) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), -32768, 32767));
        }

        void Capture()
        {
            if (review == null)
            {
                if (screenRT == null || screenRT.width != Screen.width || screenRT.height != Screen.height)
                {
                    if (screenRT != null) screenRT.Release();
                    screenRT = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
                }
                ScreenCapture.CaptureScreenshotIntoRenderTexture(screenRT);
                // The captured screen is upside down where UVs start at the top (D3D, Vulkan, Metal).
                if (SystemInfo.graphicsUVStartsAtTop) Graphics.Blit(screenRT, frameRT, new Vector2(1f, -1f), new Vector2(0f, 1f));
                else Graphics.Blit(screenRT, frameRT);
            }
            var previous = RenderTexture.active;
            RenderTexture.active = frameRT;
            int x = (cell % Cols) * W, y = (Rows - 1 - cell / Cols) * H;
            sheet.ReadPixels(new Rect(0, 0, W, H), x, y, false);
            RenderTexture.active = previous;
            frames.Add(Now - start);
            if (++cell == Cols * Rows) FlushSheet();
        }

        void FlushSheet()
        {
            sheet.Apply(false);
            string file = $"{key}_{sheetIndex:000}.jpg";
            File.WriteAllBytes(Path.Combine(root, file), sheet.EncodeToJPG(62));
            sheets.Add(file);
            sheetIndex++;
            cell = 0;
        }

        void OnStart(AudioStartInfo info)
        {
            if (!recording || info.Clip == null) return;
            float distance = 0f, pan = 0f;
            if (!info.Local && listener != null)
            {
                Transform ear = review != null ? review.transform : listener;
                Vector3 to = info.Position - ear.position;
                distance = to.magnitude;
                pan = distance > 0.01f ? Vector3.Dot(to / distance, ear.right) : 0f;
            }
            string clipPath = AssetDatabase.GetAssetPath(info.Clip);
            events.Add(string.Format(Inv,
                "{{\"t\":{0:0.###},\"kind\":\"start\",\"id\":{1},\"name\":{2},\"clip\":{3},\"vol\":{4:0.###},\"pitch\":{5:0.###},\"spatial\":{6:0.##},\"min\":{7:0.##},\"max\":{8:0.##},\"dist\":{9:0.##},\"pan\":{10:0.##},\"local\":{11},\"loop\":{12}}}",
                Now - start, info.Id, Quote(info.Sound != null ? info.Sound.name : info.Clip.name), Quote(clipPath),
                info.Volume, info.Pitch, info.SpatialBlend, info.MinDistance, info.MaxDistance, distance, pan,
                info.Local ? "true" : "false", info.Loop ? "true" : "false"));
        }

        void OnEnd(int id)
        {
            if (recording && id != 0)
                events.Add(string.Format(Inv, "{{\"t\":{0:0.###},\"kind\":\"end\",\"id\":{1}}}", Now - start, id));
        }

        static string Quote(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
    /// <summary>Copies the mixed output the AudioListener hears while a video take records (audio thread).</summary>
    public sealed class AudioTap : MonoBehaviour
    {
        readonly object gate = new();
        float[] buffer = new float[48000 * 2 * 30];
        long written, blocks, gapFrames, overlapFrames, expected;
        volatile bool recording;
        int channels = 2, rate;
        double beginDsp, beginReal;

        /// <summary>Diagnostics of the last take: blocks, frames of silence filled in, frames overwritten, clocks.</summary>
        public string Stats { get; private set; }

        /// <summary>dspAtBegin: the audio clock read on the main thread at the take's time zero.</summary>
        public void Begin(double dspAtBegin)
        {
            rate = AudioSettings.outputSampleRate;
            lock (gate) { written = blocks = gapFrames = overlapFrames = 0; expected = -1; System.Array.Clear(buffer, 0, buffer.Length); }
            beginDsp = dspAtBegin;
            beginReal = Time.realtimeSinceStartupAsDouble;
            recording = true;
        }

        /// <summary>
        /// The recorded mix on the take's clock: every block is written at its own DSP time, so a block the audio
        /// thread delivers late or in a burst, or one that never reached the filter, cannot shift what follows.
        /// </summary>
        public float[] End(out int sampleRate, out int channelCount, out double leadSeconds)
        {
            recording = false;
            double dspSpan = AudioSettings.dspTime - beginDsp, realSpan = Time.realtimeSinceStartupAsDouble - beginReal;
            lock (gate)
            {
                sampleRate = rate;
                channelCount = channels;
                leadSeconds = 0;
                Stats = $"blocks={blocks} gapMs={gapFrames * 1000.0 / rate:0} overlapMs={overlapFrames * 1000.0 / rate:0} dspSpan={dspSpan:0.000} realSpan={realSpan:0.000}";
                var copy = new float[written];
                System.Array.Copy(buffer, copy, written);
                return copy;
            }
        }

        void OnAudioFilterRead(float[] data, int channelCount)
        {
            if (!recording) return;
            double dsp = AudioSettings.dspTime;
            lock (gate)
            {
                channels = channelCount;
                long frame = (long)System.Math.Round((dsp - beginDsp) * rate);
                int frames = data.Length / channelCount;
                if (expected >= 0)
                {
                    if (frame > expected) gapFrames += frame - expected;
                    else if (frame < expected) overlapFrames += expected - frame;
                }
                expected = frame + frames;
                blocks++;
                long at = frame * channelCount;
                int from = 0;
                if (at < 0) { from = (int)System.Math.Min(-at, data.Length); at = 0; }
                long end = at + data.Length - from;
                if (end > buffer.Length) System.Array.Resize(ref buffer, (int)System.Math.Max(end, buffer.Length * 2L));
                System.Array.Copy(data, from, buffer, at, data.Length - from);
                if (end > written) written = end;
            }
        }
    }
}
#endif
