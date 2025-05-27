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

    // [Header("MIDI Settings")]
    // public int[] stripeMidiNotes = new int[20]; // one MIDI note per stripe
    // public int midiVelocity = 100;

    [Header("Pure‐Color Highlights")]
    public RawImage redHighlight;
    public RawImage greenHighlight;
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
    [Tooltip("Amount to multiply stripe index for dark CV")]
    public int darkIncrement = 100;
    [Tooltip("Amount to add to R accumulator when R threshold is met")]
    [Range(0, 1000)] public int redIncrement = 20;
    [Range(0, 1000)] public int greenIncrement = 20;
    [Range(0, 1000)] public int blueIncrement = 20;

    [Header("Color Thresholds")]
    [Tooltip("R/G/B > high && others < low to advance accumulator")]
    public float highColorThreshold = 0.8f;
    public float lowColorThreshold = 0.2f;

    [Header("Serial Settings")]
    public string portName = "/dev/cu.usbmodem144101";
    public int baudRate = 115200;

    [Header("Processing Settings")]
    public int divisionAmount = 20;
    public float brightnessThreshold = 100f;

    [Header("Timeline Link")]
    public TownTimelineController townTimeline;

    [Header("Events")]
    public UnityEvent OnDarkestThreshold;

    [Header("Post Processing")]
    public Volume postProcessVolume;
    private LensDistortion lensDistortion;
    private MotionBlur motionBlur;
    private Vignette vignette;
    private DepthOfField depthOfField;

    [Header("Reset Sounds")]
    public AudioSource audioSource;
    public AudioClip redResetClip;
    public AudioClip greenResetClip;
    public AudioClip blueResetClip;

    [Header("UI Bars")]
    public RawImage rBar, gBar, bBar;
    private float rBarMaxHeight, gBarMaxHeight, bBarMaxHeight;

    // internal
    private WebCamTexture webcamTex;
    private Color[] stripeColors;
    private int darkestStripeIndex = -1;
    private float darkestBrightness = float.MaxValue;
    private Color32[] pixels;
    private int camWidth, camHeight;

    private UdpClient udpClient;
    private bool prevThresholdState = false;

    // control values
    private int darkCV = 0;
    private int rAcc = 0, gAcc = 0, bAcc = 0;

    // timeline speed (unchanged behavior)
    private float targetSpeed = 0f, currentSpeed = 0f;

    void Start()
    {
        var cams = WebCamTexture.devices;
        for (int i = 0; i < cams.Length; i++)
            Debug.Log($"Camera {i}: {cams[i].name}");
        // record start time for UDP delay
        startTime = Time.time;

        Application.runInBackground = true;
        frameInterval = 1f / frameRate;
        lastFrameTime = Time.time;

        // setup webcam
        if (webcamIndex >= 0 && webcamIndex < cams.Length) {
            string devName = cams[webcamIndex].name;
            webcamTex = new WebCamTexture(devName);
        } else {
            Debug.LogWarning($"WebCam index {webcamIndex} out of range, falling back to default camera.");
            webcamTex = new WebCamTexture();
        }

        webcamTex.Play();
        webcamDisplay.texture = webcamTex;
        InvokeRepeating(nameof(TryInitPixels), 0.5f, 0.5f);

        // setup UDP
        udpClient = new UdpClient();
        udpClient.Connect("127.0.0.1", 9000);

        // setup arrays
        stripeColors = new Color[divisionAmount];

        // grab post-processing overrides
        if (postProcessVolume != null)
        {
            postProcessVolume.profile.TryGet(out lensDistortion);
            postProcessVolume.profile.TryGet(out motionBlur);
            postProcessVolume.profile.TryGet(out vignette);
            postProcessVolume.profile.TryGet(out depthOfField);
        }

        // store initial UI bar widths
        if (rBar  != null) rBarMaxHeight  = rBar .rectTransform.sizeDelta.y;
        if (gBar  != null) gBarMaxHeight  = gBar .rectTransform.sizeDelta.y;
        if (bBar  != null) bBarMaxHeight  = bBar .rectTransform.sizeDelta.y;

        darkestHighlight.enabled = false;
        redHighlight.enabled     = false;
        greenHighlight.enabled   = false;
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
        // throttle frame rate
        if (Time.time - lastFrameTime < frameInterval) return;
        lastFrameTime = Time.time;
        if (pixels == null || !webcamTex.isPlaying) return;

        // read webcam
        webcamTex.GetPixels32(pixels);
        AnalyzeFrame();
        UpdateUI();
        UpdateAccumulators();
        UpdatePostProcessing();
        UpdateUIBars();
        SendSerial();
    }

    // find darkest stripe each frame
    void AnalyzeFrame()
    {
        int stripeWidth = camWidth / divisionAmount;
        darkestBrightness = float.MaxValue;

        for (int i = 0; i < divisionAmount; i++)
        {
            int xStart = i * stripeWidth;
            int xEnd   = (i == divisionAmount - 1) ? camWidth : xStart + stripeWidth;
            ulong r=0, g=0, b=0;
            int count = 0;

            for (int x = xStart; x < xEnd; x++)
            {
                int idx = 0 * camWidth + x;
                r += pixels[idx].r;
                g += pixels[idx].g;
                b += pixels[idx].b;
                count++;
            }

            float avgR = r / (float)count;
            float avgG = g / (float)count;
            float avgB = b / (float)count;
            stripeColors[i] = new Color(avgR/255f, avgG/255f, avgB/255f);

            

            float brightness = avgR + avgG + avgB;
            if (brightness < darkestBrightness)
            {
                darkestBrightness    = brightness;
                darkestStripeIndex   = i;
            }
        }
    }

    // highlight UI, fire MIDI on threshold, update darkCV
    void UpdateUI()
{
    // 1) Update stripe colors
    for (int i = 0; i < divisionAmount; i++)
        stripeImages[i].color = stripeColors[i];

    // 2) Find darkest
    bool haveDarkest = darkestStripeIndex >= 0;
    if (haveDarkest)
    {
        darkestHighlight.enabled = true;
        var dRT = darkestHighlight.rectTransform;
        dRT.position = stripeImages[darkestStripeIndex]
                           .rectTransform.position;
    }
    else
    {
        darkestHighlight.enabled = false;
    }

    // 3) Update darkCV every frame
    if (haveDarkest)
        darkCV = (darkestStripeIndex * darkIncrement) % 4096;

    // 4) Fire MIDI on crossing threshold
    // bool nowThreshold = (darkestBrightness < brightnessThreshold);
    // thresholdIndicator.enabled = nowThreshold;

    // if (nowThreshold && !prevThresholdState)
    // {
    //     OnDarkestThreshold?.Invoke();

    //     int note = stripeMidiNotes[darkestStripeIndex];
    //     string msg = $"MIDI {note} {midiVelocity}";
    //     byte[] bytes = Encoding.UTF8.GetBytes(msg);
    //     udpClient.Send(bytes, bytes.Length);
    // }
    // prevThresholdState = nowThreshold;

    // 5) Find “purest” R/G/B candidates
    int bestRedIdx   = -1, bestGreenIdx = -1, bestBlueIdx = -1;
    float bestRedVal = -1f, bestGreenVal = -1f, bestBlueVal = -1f;

    for (int i = 0; i < divisionAmount; i++)
    {
        Color c = stripeColors[i];

        // pure red?
        if (c.r > highColorThreshold &&
            c.g < lowColorThreshold &&
            c.b < lowColorThreshold &&
            c.r > bestRedVal)
        {
            bestRedVal = c.r;
            bestRedIdx = i;
        }

        // pure green?
        if (c.g > highColorThreshold &&
            c.r < lowColorThreshold &&
            c.b < lowColorThreshold &&
            c.g > bestGreenVal)
        {
            bestGreenVal = c.g;
            bestGreenIdx = i;
        }

        // pure blue?
        if (c.b > highColorThreshold &&
            c.r < lowColorThreshold &&
            c.g < lowColorThreshold &&
            c.b > bestBlueVal)
        {
            bestBlueVal = c.b;
            bestBlueIdx = i;
        }
    }

    // 6) Position or hide each color‐highlight
    if (bestRedIdx >= 0)
    {
        redHighlight.enabled = true;
        redHighlight.rectTransform.position =
            stripeImages[bestRedIdx].rectTransform.position;
    }
    else redHighlight.enabled = false;

    if (bestGreenIdx >= 0)
    {
        greenHighlight.enabled = true;
        greenHighlight.rectTransform.position =
            stripeImages[bestGreenIdx].rectTransform.position;
    }
    else greenHighlight.enabled = false;

    if (bestBlueIdx >= 0)
    {
        blueHighlight.enabled = true;
        blueHighlight.rectTransform.position =
            stripeImages[bestBlueIdx].rectTransform.position;
    }
    else blueHighlight.enabled = false;
}


    // update R/G/B accumulators based on thresholds
    void UpdateAccumulators()
    {
        if (darkestStripeIndex < 0) return;

        Color c = stripeColors[darkestStripeIndex];

        // R channel
        if (c.r > highColorThreshold && c.g < lowColorThreshold && c.b < lowColorThreshold)
        {
            rAcc += redIncrement;
            if (rAcc >= 4096)
            {
                rAcc %= 4096;
                audioSource.PlayOneShot(redResetClip);
            }
        }

        // G channel
        if (c.g > highColorThreshold && c.r < lowColorThreshold && c.b < lowColorThreshold)
        {
            gAcc += greenIncrement;
            if (gAcc >= 4096)
            {
                gAcc %= 4096;
                audioSource.PlayOneShot(greenResetClip);
            }
        }

        // B channel
        if (c.b > highColorThreshold && c.r < lowColorThreshold && c.g < lowColorThreshold)
        {
            bAcc += blueIncrement;
            if (bAcc >= 4096)
            {
                bAcc %= 4096;
                audioSource.PlayOneShot(blueResetClip);
            }
        }
    }

    // map accumulators to post-processing
    void UpdatePostProcessing()
    {
        float fR = rAcc / 4096f;
        float fG = gAcc / 4096f;
        float fB = bAcc / 4096f;

        // Lens distortion: -1 → 0.139
        if (lensDistortion != null)
            lensDistortion.intensity.value = Mathf.Lerp(-1f, 0.139f, fR);

        // Motion blur: 0 → 1
        if (motionBlur != null)
            motionBlur.intensity.value = Mathf.Lerp(0f, 1f, fG);

        // Vignette smoothness: 1 → 0
        if (vignette != null)
            vignette.smoothness.value = Mathf.Lerp(1f, 0f, fG);

        // Depth of field: 130 → 15
        if (depthOfField != null)
            depthOfField.focusDistance.value = Mathf.Lerp(130f, 15f, fB);
    }

    // update the UI bars to reflect accumulators
    void UpdateUIBars()
    {
    if (rBar != null)
    {
        var rt = rBar.rectTransform;
        // keep the same width, update height
        rt.sizeDelta = new Vector2(rt.sizeDelta.x,
                                   rBarMaxHeight * (rAcc / 4096f));
    }
    if (gBar != null)
    {
        var rt = gBar.rectTransform;
        rt.sizeDelta = new Vector2(rt.sizeDelta.x,
                                   gBarMaxHeight * (gAcc / 4096f));
    }
    if (bBar != null)
    {
        var rt = bBar.rectTransform;
        rt.sizeDelta = new Vector2(rt.sizeDelta.x,
                                   bBarMaxHeight * (bAcc / 4096f));
    }
}

    // send four channel values via UDP
    void SendSerial()
    {
        if (!udpReady)
        {
            if (Time.time - startTime < udpSendDelay) return;
            udpReady = true;
            Debug.Log("✅ UDP ready.");
        }

        string output = $"{darkCV} {rAcc} {gAcc} {bAcc}";
        byte[] message = Encoding.UTF8.GetBytes(output);

        try
        {
            udpClient.Send(message, message.Length);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("🔌 UDP send failed: " + e.Message);
            udpReady = false;
            startTime = Time.time;
        }
    }

    void OnDestroy()
    {
        if (webcamTex != null && webcamTex.isPlaying) webcamTex.Stop();
    }
}
