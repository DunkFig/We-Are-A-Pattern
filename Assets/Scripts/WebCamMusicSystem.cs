using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Net.Sockets;
using System.Text;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class WebCamMusicSystem : MonoBehaviour
{
    // small delay before first UDP send
    private float udpSendDelay = 5f;
    private float startTime;
    private bool udpReady = false;

    [Header("WebCam Settings")]
    [Tooltip("Which index in WebCamTexture.devices should we use?")]
    public int webcamIndex = 0;

    [Header("Pure‐Color Highlights")]
    public RawImage redHighlight;
    public RawImage brightHighlight;       // repurposed greenHighlight
    public RawImage blueHighlight;

    [Header("UI Setup")]
    public RawImage webcamDisplay;
    public Image[] stripeImages = new Image[20];
    public RawImage darkestHighlight;
    public Image thresholdIndicator;

    [Header("Performance Settings")]
    [Range(1, 60)] public float frameRate = 12f;
    private float frameInterval;
    private float lastFrameTime;

    [Header("Increments")]
    [Tooltip("Amount to add to R accumulator when R threshold is met")]
    public int redIncrement = 20;
    [Tooltip("Amount to add to Brightness accumulator when bright threshold is met")]
    [Range(0, 1000)] public int brightIncrement = 20;
    [Tooltip("Amount to add to B accumulator when B threshold is met")]
    [Range(0, 1000)] public int blueIncrement = 20;
    [Tooltip("Amount to multiply stripe index for immediate CV")]
    public int darkIncrement = 100;

    [Header("Color Thresholds")]
    [Tooltip("R/G/B > high && others < low to advance accumulator")]
    public float highColorThreshold = 0.8f;
    public float lowColorThreshold = 0.2f;

    [Header("Dark & Bright Thresholds")]
    [Tooltip("Sum of R+G+B below this fires a dark-threshold hit")]
    public float darkThreshold = 100f;
    [Tooltip("Sum of R+G+B above this fires a bright-threshold hit")]
    public float brightThreshold = 500f;

    [Header("Dark‐Threshold Accumulator")]
    [Tooltip("How many threshold crossings before incrementing bar")]
    public int darkHitsToIncrement = 3;
    [Tooltip("Accumulator wraps at this maximum (e.g. 4096)")]
    public int maxDarkAcc = 4096;
    [Tooltip("Amount to add to dark accumulator each time")]
    public int darkBarIncrement = 100;
    public RawImage darkBar;
    public AudioClip darkResetClip;

    [Header("Brightness Accumulator")]
    [Tooltip("Accumulator wraps at this maximum (e.g. 4096)")]
    public int maxBrightAcc = 4096;
    public AudioClip brightResetClip;
    public RawImage brightBar;

    [Header("Speed & Smoothing")]
    [Tooltip("Interpolation speed for all lerps")]
    [Range(0.01f, 5f)] public float SonicTransitionSpeed = 1.0f;
    [Tooltip("Playback speed when dark bar at minimum")]
    public float minSpeed = 0.5f;
    [Tooltip("Playback speed when dark bar at maximum")]
    public float maxSpeed = 1.5f;

    [Header("Serial Settings")]
    public string portName = "/dev/cu.usbmodem144101";
    public int baudRate = 115200;

    [Header("MIDI Settings")]
    [Tooltip("One MIDI note per stripe index")]
    public int[] midiNotes;
    [Tooltip("Velocity to send with MIDI note on")]
    public int midiVelocity = 127;
    [Tooltip("Seconds to wait between MIDI sends")]
    public float midiCooldownSeconds = 0.5f;

    [Header("Processing Settings")]
    public int divisionAmount = 20;

    [Header("Timeline Link")]
    public TownTimelineController townTimeline;

    [Header("Events")]
    public UnityEvent OnDarkestThreshold;

    [Header("Post Processing Ranges")]
    public Volume postProcessVolume;
    private LensDistortion lensDistortion;
    private MotionBlur motionBlur;
    private DepthOfField depthOfField;
    private Vignette vignette;
    private ChromaticAberration chromatic;
    private Bloom bloom;

    [Header("Reset Sounds")]
    public AudioSource audioSource;
    public AudioClip redResetClip;
    public AudioClip blueResetClip;

    [Header("UI Bars")]
    public RawImage rBar, bBar;
    private float rBarMaxHeight, bBarMaxHeight, darkBarMaxHeight, brightBarMaxHeight;

    // internals
    private WebCamTexture webcamTex;
    private Color[] stripeColors;
    private int darkestStripeIndex = -1;
    private float darkestBrightness = float.MaxValue;
    private Color32[] pixels;
    private int camWidth, camHeight;

    private UdpClient udpClient;
    private bool prevDarkState = false;

    // accumulators & CVs
    private int darkCV = 0;    
    private int darkAcc = 0;   
    private int darkHitCount = 0;
    private int rAcc = 0, brightAcc = 0, bAcc = 0;

    // timeline speed
    private float targetSpeed = 1f, currentSpeed = 1f;

    // MIDI cooldown
    private float lastMidiTime = -Mathf.Infinity;

    void Start()
    {
        // list cameras
        var cams = WebCamTexture.devices;
        for (int i = 0; i < cams.Length; i++)
            Debug.Log($"Camera {i}: {cams[i].name}");

        startTime = Time.time;
        Application.runInBackground = true;
        frameInterval = 1f / frameRate;
        lastFrameTime = Time.time;

        // setup chosen webcam
        if (webcamIndex >= 0 && webcamIndex < cams.Length)
            webcamTex = new WebCamTexture(cams[webcamIndex].name);
        else
            webcamTex = new WebCamTexture();

        webcamTex.Play();
        webcamDisplay.texture = webcamTex;
        InvokeRepeating(nameof(TryInitPixels), 0.5f, 0.5f);

        // UDP bridge
        udpClient = new UdpClient();
        udpClient.Connect("127.0.0.1", 9000);

        stripeColors = new Color[divisionAmount];

        // fetch post processing overrides
        if (postProcessVolume != null)
        {
            postProcessVolume.profile.TryGet(out lensDistortion);
            postProcessVolume.profile.TryGet(out motionBlur);
            postProcessVolume.profile.TryGet(out vignette);
            postProcessVolume.profile.TryGet(out depthOfField);
            postProcessVolume.profile.TryGet(out chromatic);
            postProcessVolume.profile.TryGet(out bloom);
        }

        // store bar heights
        if (rBar       != null) rBarMaxHeight    = rBar.rectTransform.sizeDelta.y;
        if (bBar       != null) bBarMaxHeight    = bBar.rectTransform.sizeDelta.y;
        if (darkBar    != null) darkBarMaxHeight = darkBar.rectTransform.sizeDelta.y;
        if (brightBar  != null) brightBarMaxHeight = brightBar.rectTransform.sizeDelta.y;

        // hide highlights
        darkestHighlight.enabled = false;
        redHighlight.enabled     = false;
        brightHighlight.enabled  = false;
        blueHighlight.enabled    = false;
    }

    void TryInitPixels()
    {
        if (webcamTex.width > 16)
        {
            camWidth  = webcamTex.width;
            camHeight = webcamTex.height;
            pixels    = new Color32[camWidth * camHeight];
            CancelInvoke(nameof(TryInitPixels));
        }
    }

    void Update()
    {
        if (Time.time - lastFrameTime < frameInterval) return;
        lastFrameTime = Time.time;
        if (pixels == null || !webcamTex.isPlaying) return;

        webcamTex.GetPixels32(pixels);
        AnalyzeFrame();
        UpdateUI();
        UpdateAccumulators();
        UpdatePostProcessing();
        UpdateUIBars();
        SendSerial();
    }

    void AnalyzeFrame()
    {
        int stripeWidth = camWidth / divisionAmount;
        darkestBrightness = float.MaxValue;

        for (int i = 0; i < divisionAmount; i++)
        {
            int xStart = i * stripeWidth;
            int xEnd   = (i == divisionAmount - 1) ? camWidth : xStart + stripeWidth;
            ulong r = 0, g = 0, b = 0;
            int count = 0;

            for (int x = xStart; x < xEnd; x++)
            {
                int idx = (camHeight - 1) * camWidth + x;
                r += pixels[idx].r;
                g += pixels[idx].g;
                b += pixels[idx].b;
                count++;
            }

            float avgR = r / (float)count;
            float avgG = g / (float)count;
            float avgB = b / (float)count;
            stripeColors[i] = new Color(avgR / 255f, avgG / 255f, avgB / 255f);

            float brightness = avgR + avgG + avgB;
            if (brightness < darkestBrightness)
            {
                darkestBrightness  = brightness;
                darkestStripeIndex = i;
            }
        }
    }

    void UpdateUI()
    {
        // update stripe swatches
        for (int i = 0; i < divisionAmount; i++)
            stripeImages[i].color = stripeColors[i];

        // darkest highlight
        bool nowDark = darkestStripeIndex >= 0;
        darkestHighlight.enabled = nowDark;
        if (nowDark)
            darkestHighlight.rectTransform.position =
                stripeImages[darkestStripeIndex].rectTransform.position;

        // immediate CV from darkest stripe
        if (nowDark)
            darkCV = Mathf.Clamp(darkestStripeIndex * darkIncrement, 0, maxDarkAcc - 1);

        // threshold crossing for darkness
        bool darkHit = darkestBrightness < darkThreshold;
        thresholdIndicator.enabled = darkHit;
        if (darkHit && !prevDarkState)
        {
            OnDarkestThreshold?.Invoke();

            // MIDI send on darkest threshold
            if (Time.time - lastMidiTime >= midiCooldownSeconds && 
                darkestStripeIndex >= 0 && midiNotes != null && darkestStripeIndex < midiNotes.Length)
            {
                string midiMsg = $"MIDI {midiNotes[darkestStripeIndex]} {midiVelocity}";
                udpClient.Send(Encoding.UTF8.GetBytes(midiMsg), midiMsg.Length);
                lastMidiTime = Time.time;
            }

            // accumulate dark hits
            darkHitCount++;
            if (darkHitCount >= darkHitsToIncrement)
            {
                darkHitCount = 0;
                darkAcc += darkBarIncrement;
                if (darkAcc >= maxDarkAcc)
                {
                    darkAcc %= maxDarkAcc;
                    if (audioSource && darkResetClip != null)
                        audioSource.PlayOneShot(darkResetClip);
                }
                // recalc speed target
                float t = darkAcc / (float)(maxDarkAcc - 1);
                targetSpeed = Mathf.Lerp(minSpeed, maxSpeed, t);
            }
        }
        prevDarkState = darkHit;

        // pure-red & pure-blue highlights (unchanged)
        int bestR = -1, bestB = -1;
        float vR = -1f, vB = -1f;
        for (int i = 0; i < divisionAmount; i++)
        {
            var c = stripeColors[i];
            if (c.r > highColorThreshold && c.g < lowColorThreshold && c.b < lowColorThreshold && c.r > vR)
            { vR = c.r; bestR = i; }
            if (c.b > highColorThreshold && c.r < lowColorThreshold && c.g < lowColorThreshold && c.b > vB)
            { vB = c.b; bestB = i; }
        }
        redHighlight.enabled  = bestR >= 0;
        blueHighlight.enabled = bestB >= 0;
        if (bestR >= 0) redHighlight.rectTransform.position  = stripeImages[bestR].rectTransform.position;
        if (bestB >= 0) blueHighlight.rectTransform.position = stripeImages[bestB].rectTransform.position;

        // brightness highlight
        int bestBright = -1;
        float vBright = brightThreshold;
        for (int i = 0; i < divisionAmount; i++)
        {
            var c = stripeColors[i];
            float bVal = (c.r + c.g + c.b) * 255f; 
            if (bVal > vBright)
            {
                vBright = bVal;
                bestBright = i;
            }
        }
        brightHighlight.enabled = bestBright >= 0;
        if (bestBright >= 0)
            brightHighlight.rectTransform.position = stripeImages[bestBright].rectTransform.position;
    }

    void UpdateAccumulators()
    {
        if (darkestStripeIndex < 0) return;
        var c = stripeColors[darkestStripeIndex];

        // R accumulator
        if (c.r > highColorThreshold && c.g < lowColorThreshold && c.b < lowColorThreshold)
        {
            rAcc += redIncrement;
            if (rAcc >= 4096) { rAcc %= 4096; audioSource.PlayOneShot(redResetClip); }
        }

        // Brightness accumulator
        float total = (c.r + c.g + c.b) * 255f;
        if (total > brightThreshold)
        {
            brightAcc += brightIncrement;
            if (brightAcc >= maxBrightAcc) { brightAcc %= maxBrightAcc; audioSource.PlayOneShot(brightResetClip); }
        }

        // B accumulator
        if (c.b > highColorThreshold && c.r < lowColorThreshold && c.g < lowColorThreshold)
        {
            bAcc += blueIncrement;
            if (bAcc >= 4096) { bAcc %= 4096; audioSource.PlayOneShot(blueResetClip); }
        }
    }

    void UpdatePostProcessing()
    {
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * SonicTransitionSpeed);
        if (townTimeline != null)
            townTimeline.SetTargetSpeed(currentSpeed);

        float fd = darkAcc / 4095f;
        if (motionBlur   != null) motionBlur.intensity.value       = Mathf.Lerp(0f, 1f, fd);
        if (depthOfField != null) depthOfField.focusDistance.value = Mathf.Lerp(15f, 130f, fd);

        float fr = rAcc / 4095f;
        if (lensDistortion != null) lensDistortion.intensity.value = Mathf.Lerp(-1f, 0.139f, fr);

        float fb = brightAcc / 4095f;
        if (vignette != null) vignette.smoothness.value = Mathf.Lerp(0f, 1f, fb);

        float fc = bAcc / 4095f;
        if (chromatic != null) chromatic.intensity.value = Mathf.Lerp(0f, 1f, fc);
        if (bloom     != null) bloom.intensity.value     = Mathf.Lerp(0f, 1f, fc);
    }

    void UpdateUIBars()
    {
        if (darkBar != null)
        {
            var rt = darkBar.rectTransform;
            float fd = darkAcc / 4095f;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, darkBarMaxHeight * fd);
        }
        if (rBar != null)
        {
            var rt = rBar.rectTransform;
            float fr = rAcc / 4095f;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, rBarMaxHeight * fr);
        }
        if (brightBar != null)
        {
            var rt = brightBar.rectTransform;
            float fb = brightAcc / 4095f;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, brightBarMaxHeight * fb);
        }
        if (bBar != null)
        {
            var rt = bBar.rectTransform;
            float fc = bAcc / 4095f;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, bBarMaxHeight * fc);
        }
    }

    void SendSerial()
    {
        if (!udpReady)
        {
            if (Time.time - startTime < udpSendDelay) return;
            udpReady = true;
            Debug.Log("✅ UDP ready.");
        }
        // DarkAcc, RAcc, BrightAcc, BAcc
        string output = $"{darkAcc} {rAcc} {brightAcc} {bAcc}";
        Debug.Log(output);
        byte[] message = Encoding.UTF8.GetBytes(output);
        try { udpClient.Send(message, message.Length); }
        catch (System.Exception e)
        {
            Debug.LogWarning("🔌 UDP send failed: " + e.Message);
            udpReady = false;
            startTime = Time.time;
        }
    }

    void OnDestroy()
    {
        if (webcamTex != null && webcamTex.isPlaying)
            webcamTex.Stop();
    }
}
