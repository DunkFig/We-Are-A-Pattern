using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Net.Sockets;
using System.Text;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class WebCamMusicSystem : MonoBehaviour
{
    private float udpSendDelay = 5f;  // Wait 5 seconds before first send
    private float startTime;
    private bool udpReady = false;


    [Header("MIDI Settings")]
    public int[] stripeMidiNotes = new int[20]; // Map each stripe to a MIDI note
    public int midiVelocity = 100;

    [Header("UI Setup")]
    public RawImage webcamDisplay;
    public Image[] stripeImages = new Image[20];
    public RawImage darkestHighlight;
    public Image thresholdIndicator;

    [Header("Performance Settings")]
    [Range(1, 60)] public float frameRate = 12f;
    private float frameInterval;
    private float lastFrameTime;

    [Header("Increment Settings")]
    [Range(0, 1000)] public int redIncrement = 20;
    [Range(0, 1000)] public int greenIncrement = 20;
    [Range(0, 1000)] public int blueIncrement = 20;
    [Range(0, 1000)] public int darkIncrement = 20;

    [Header("Centering Behavior")]
    [Range(0, 4096)] public float CenteringThreshold = 500f;
    [Range(0f, 1f)] public float CenteringSpeed = 0.05f;

    [Header("Sonic Transition")]
    [Range(0.01f, 5f)] public float SonicTransitionSpeed = 1.0f;

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

    [Header("Audio Feedback")]
    public AudioSource audioSource;
    public AudioClip[] stripeClips = new AudioClip[20];

    private WebCamTexture webcamTex;
    private Color[] stripeColors;
    private int darkestStripeIndex = -1;
    private float darkestBrightness = float.MaxValue;
    private Color32[] pixels;
    private int camWidth, camHeight;

    private UdpClient udpClient;
    private bool prevThresholdState = false;

    private float darkCV = 2048f, redCV = 2048f, greenCV = 2048f, blueCV = 2048f;
    private float targetDarkCV = 2048f, targetRedCV = 2048f, targetGreenCV = 2048f, targetBlueCV = 2048f;

    private float targetSpeed = 0f;
    private float currentSpeed = 0f;

    void Start()
    {
        startTime = Time.time;

        Application.runInBackground = true;
        frameInterval = 1f / frameRate;
        lastFrameTime = Time.time;

        webcamTex = new WebCamTexture();
        webcamTex.Play();
        webcamDisplay.texture = webcamTex;
        InvokeRepeating(nameof(TryInitPixels), 0.5f, 0.5f);

        udpClient = new UdpClient();
        udpClient.Connect("127.0.0.1", 9000);

        stripeColors = new Color[divisionAmount];

        if (postProcessVolume != null)
        {
            postProcessVolume.profile.TryGet(out lensDistortion);
            postProcessVolume.profile.TryGet(out motionBlur);
            postProcessVolume.profile.TryGet(out vignette);
            postProcessVolume.profile.TryGet(out depthOfField);
        }
    }

    void TryInitPixels()
    {
        if (webcamTex.width > 16)
        {
            camWidth = webcamTex.width;
            camHeight = webcamTex.height;
            pixels = new Color32[camWidth * camHeight];
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
        UpdateLerps();
        SendSerial();
    }

    void AnalyzeFrame()
    {
        int stripeWidth = camWidth / divisionAmount;
        darkestBrightness = float.MaxValue;

        for (int i = 0; i < divisionAmount; i++)
        {
            int xStart = i * stripeWidth;
            int xEnd = (i == divisionAmount - 1) ? camWidth : xStart + stripeWidth;
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
                darkestBrightness = brightness;
                darkestStripeIndex = i;
            }
        }
    }

    void UpdateUI()
    {
        for (int i = 0; i < divisionAmount; i++)
            stripeImages[i].color = stripeColors[i];

        if (darkestStripeIndex >= 0)
        {
            var highlightRT = darkestHighlight.rectTransform;
            highlightRT.position = stripeImages[darkestStripeIndex].rectTransform.position;
        }

        bool nowThreshold = darkestBrightness < brightnessThreshold;
        thresholdIndicator.enabled = nowThreshold;

        if (nowThreshold && !prevThresholdState)
        {
            OnDarkestThreshold?.Invoke();

            float positionFactor = darkestStripeIndex / (float)(divisionAmount - 1);
            targetSpeed = Mathf.Lerp(5f, -5f, positionFactor);

            var color = stripeColors[darkestStripeIndex];
            targetDarkCV = Mathf.Clamp(positionFactor * 4096, 0, 4095);
            targetRedCV   = Mathf.Clamp(2048f + (color.r - 0.5f) * 2f * redIncrement, 0, 4095);
            targetGreenCV = Mathf.Clamp(2048f + (color.g - 0.5f) * 2f * greenIncrement, 0, 4095);
            targetBlueCV  = Mathf.Clamp(2048f + (color.b - 0.5f) * 2f * blueIncrement, 0, 4095);

            if (darkestStripeIndex >= 0 && darkestStripeIndex < stripeClips.Length && audioSource != null)
            {
                audioSource.clip = stripeClips[darkestStripeIndex];
                audioSource.Play();

                // Send MIDI Note via UDP bridge
                string midiMsg = $"MIDI {stripeMidiNotes[darkestStripeIndex]} {midiVelocity}";
                byte[] midiBytes = Encoding.UTF8.GetBytes(midiMsg);
                udpClient.Send(midiBytes, midiBytes.Length);

            }
        }

        prevThresholdState = nowThreshold;
    }

    void UpdateLerps()
    {
        float t = Time.deltaTime * SonicTransitionSpeed;

        redCV = Mathf.Lerp(redCV, targetRedCV, t);
        greenCV = Mathf.Lerp(greenCV, targetGreenCV, t);
        blueCV = Mathf.Lerp(blueCV, targetBlueCV, t);
        darkCV = Mathf.Lerp(darkCV, targetDarkCV, t);

        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, t);

        if (townTimeline != null)
        {
            townTimeline.playbackSpeed = currentSpeed;
        }

        UpdatePostProcessing(currentSpeed);
    }

    void UpdatePostProcessing(float speed)
    {
        float absSpeed = Mathf.Abs(speed) / 5f;
        if (lensDistortion != null)
            lensDistortion.intensity.value = Mathf.Lerp(0.139f, -1f, absSpeed);

        if (motionBlur != null)
            motionBlur.intensity.value = Mathf.Lerp(0f, 1f, absSpeed);

        if (vignette != null)
        {
            float sm = Mathf.InverseLerp(-5f, 5f, speed);
            vignette.smoothness.value = Mathf.Lerp(1f, 0f, sm);
        }

        if (depthOfField != null)
            depthOfField.focusDistance.value = Mathf.Lerp(130f, 15f, absSpeed);
    }

    void SendSerial()
    {
        if (!udpReady)
        {
            try
            {
                udpClient.Send(new byte[] { 0 }, 1);  // Try sending a ping byte
                udpReady = true;
                Debug.Log("✅ UDP ready.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("🔌 UDP still not ready: " + e.Message);
                return;
            }
        }

        string output = $"{(int)darkCV} {(int)redCV} {(int)greenCV} {(int)blueCV}";
        byte[] message = Encoding.UTF8.GetBytes(output);

        try
        {
            udpClient.Send(message, message.Length);
            Debug.Log("📡 Sent to bridge: " + output);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("🔌 Final UDP send failed: " + e.Message);
            udpReady = false;
        }
    }



    void OnDestroy()
    {
        if (webcamTex != null && webcamTex.isPlaying) webcamTex.Stop();
    }
}
