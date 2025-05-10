// WebCamMusicSystem.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Net.Sockets;
using System.Text;

public class WebCamMusicSystem : MonoBehaviour
{
    [Header("UI Setup")]
    public RawImage webcamDisplay;
    public Image[] stripeImages = new Image[20];
    public RawImage darkestHighlight;
    public Image thresholdIndicator;

    [Header("Performance Settings")]
    [Range(1, 60)] public float frameRate = 12f;
    private float frameInterval;
    private float lastFrameTime;

    [Header("Transition Settings")]
    [Tooltip("How quickly CV values and timeline speed change")]
    public float SonicTransitionSpeed = 1.5f;

    [Header("Increment Settings")]
    [Range(0, 1000)] public int redIncrement = 20;
    [Range(0, 1000)] public int greenIncrement = 20;
    [Range(0, 1000)] public int blueIncrement = 20;
    [Range(0, 1000)] public int darkIncrement = 20;

    [Header("Centering Behavior")]
    [Range(0, 4096)] public float CenteringThreshold = 500f;
    [Range(0f, 1f)] public float CenteringSpeed = 0.05f;

    [Header("Serial Settings")]
    public string udpAddress = "127.0.0.1";
    public int udpPort = 9000;

    [Header("Processing Settings")]
    public int divisionAmount = 20;
    public float brightnessThreshold = 100f;

    [Header("Timeline Control")]
    public TownTimelineController townTimeline;

    [Header("Events")]
    public UnityEvent OnDarkestThreshold;

    private WebCamTexture webcamTex;
    private Color[] stripeColors;
    private int darkestStripeIndex = -1;
    private float darkestBrightness = float.MaxValue;
    private Color32[] pixels;
    private int camWidth, camHeight;
    private UdpClient udpClient;

    private bool thresholdTriggered = false;
    private float darkCV = 2048f, redCV = 2048f, greenCV = 2048f, blueCV = 2048f;
    private float targetDark = 2048f, targetRed = 2048f, targetGreen = 2048f, targetBlue = 2048f;
    private float timelineSpeedTarget = 0f;

    void Start()
    {
        Application.runInBackground = true;
        frameInterval = 1f / frameRate;
        lastFrameTime = Time.time;

        webcamTex = new WebCamTexture();
        webcamTex.Play();
        webcamDisplay.texture = webcamTex;
        InvokeRepeating(nameof(TryInitPixels), 0.5f, 0.5f);

        udpClient = new UdpClient();
        udpClient.Connect(udpAddress, udpPort);

        stripeColors = new Color[divisionAmount];
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
        LerpToTargets();
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

        bool thresholdNow = darkestBrightness < brightnessThreshold;
        thresholdIndicator.enabled = thresholdNow;

        if (thresholdNow && !thresholdTriggered)
        {
            OnDarkestThreshold?.Invoke();
            SetNewTargetsFromDarkest();
            thresholdTriggered = true;
        }
        else if (!thresholdNow)
        {
            thresholdTriggered = false;
        }
    }

    void SetNewTargetsFromDarkest()
    {
        if (darkestStripeIndex < 0 || darkestStripeIndex >= stripeColors.Length) return;

        Color c = stripeColors[darkestStripeIndex];
        targetRed = Mathf.Clamp(2048f + ((c.r - 0.5f) * 2f * redIncrement), 0f, 4095f);
        targetGreen = Mathf.Clamp(2048f + ((c.g - 0.5f) * 2f * greenIncrement), 0f, 4095f);
        targetBlue = Mathf.Clamp(2048f + ((c.b - 0.5f) * 2f * blueIncrement), 0f, 4095f);
        targetDark = Mathf.Clamp(2048f + ((darkestStripeIndex / (float)(divisionAmount - 1) - 0.5f) * 2f * darkIncrement), 0f, 4095f);

        float mappedSpeed = Mathf.Lerp(5f, -5f, darkestStripeIndex / (float)(divisionAmount - 1));
        if (townTimeline != null)
        {
            townTimeline.SetTargetSpeed(mappedSpeed);
        }
    }

    void LerpToTargets()
    {
        darkCV = Mathf.Lerp(darkCV, targetDark, Time.deltaTime / SonicTransitionSpeed);
        redCV = Mathf.Lerp(redCV, targetRed, Time.deltaTime / SonicTransitionSpeed);
        greenCV = Mathf.Lerp(greenCV, targetGreen, Time.deltaTime / SonicTransitionSpeed);
        blueCV = Mathf.Lerp(blueCV, targetBlue, Time.deltaTime / SonicTransitionSpeed);
    }

    void UpdateUI()
    {
        for (int i = 0; i < divisionAmount; i++)
            stripeImages[i].color = stripeColors[i];

        if (darkestStripeIndex >= 0)
        {
            var highlightRT = darkestHighlight.rectTransform;
            highlightRT.position = stripeImages[darkestStripeIndex].rectTransform.position;

            if (thresholdIndicator.enabled)
            {
                var tr = thresholdIndicator.rectTransform;
                tr.position = highlightRT.position + new Vector3(60, 0, 0);
            }
        }
    }

    void SendSerial()
    {
        string output = $"{(int)darkCV} {(int)redCV} {(int)greenCV} {(int)blueCV}";
        byte[] message = Encoding.UTF8.GetBytes(output + "\n");

        try
        {
            udpClient.Send(message, message.Length);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("UDP send failed: " + e.Message);
        }
    }

    void OnDestroy()
    {
        if (webcamTex != null && webcamTex.isPlaying) webcamTex.Stop();
    }
}
