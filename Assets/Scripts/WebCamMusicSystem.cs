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

    [Header("Increment Settings")]
    [Range(0, 1000)] public int redIncrement = 20;
    [Range(0, 1000)] public int greenIncrement = 20;
    [Range(0, 1000)] public int blueIncrement = 20;
    [Range(0, 1000)] public int darkIncrement = 20;

    [Header("Centering Behavior")]
    [Range(0, 4096)] public float CenteringThreshold = 500f;
    [Range(0f, 1f)] public float CenteringSpeed = 0.05f;

    // Internal DAC values
    private float darkCV = 2048f;
    private float redCV = 2048f;
    private float greenCV = 2048f;
    private float blueCV = 2048f;

    [Header("Serial Settings")]
    public string portName = "/dev/cu.usbmodem144101";
    public int baudRate = 115200;

    [Header("Processing Settings")]
    public int divisionAmount = 20;
    public float brightnessThreshold = 100f;

    [Header("Events")]
    public UnityEvent OnDarkestThreshold;

    private WebCamTexture webcamTex;
    private Color[] stripeColors;
    private int darkestStripeIndex = -1;
    private float darkestBrightness = float.MaxValue;
    private Color32[] pixels;
    private int camWidth, camHeight;

    private UdpClient udpClient;
    private bool prevThresholdState = false;

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
        udpClient.Connect("127.0.0.1", 9000);

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

        // fire event on rising edge
        if (nowThreshold && !prevThresholdState)
            OnDarkestThreshold?.Invoke();

        prevThresholdState = nowThreshold;

        if (nowThreshold)
        {
            var tr = thresholdIndicator.rectTransform;
            tr.position = stripeImages[darkestStripeIndex].rectTransform.position + new Vector3(60, 0, 0);

            float darkDelta = ((darkestStripeIndex / (float)(divisionAmount - 1)) - 0.5f) * 2f;
            darkCV += darkDelta * darkIncrement;
        }
    }

    void SendSerial()
    {
        if (darkestStripeIndex < 0) return;

        if (darkestBrightness > CenteringThreshold)
        {
            redCV   = Mathf.Lerp(redCV,   2048f, CenteringSpeed);
            greenCV = Mathf.Lerp(greenCV, 2048f, CenteringSpeed);
            blueCV  = Mathf.Lerp(blueCV,  2048f, CenteringSpeed);
            darkCV  = Mathf.Lerp(darkCV,  2048f, CenteringSpeed);
        }

        Color color = stripeColors[darkestStripeIndex];
        redCV   += (color.r   - 0.5f) * 2f * redIncrement;
        greenCV += (color.g - 0.5f) * 2f * greenIncrement;
        blueCV  += (color.b  - 0.5f) * 2f * blueIncrement;

        redCV   = Mathf.Clamp(redCV,   0, 4095);
        greenCV = Mathf.Clamp(greenCV, 0, 4095);
        blueCV  = Mathf.Clamp(blueCV,  0, 4095);
        darkCV  = Mathf.Clamp(darkCV,  0, 4095);

        string output = $"{(int)darkCV} {(int)redCV} {(int)greenCV} {(int)blueCV}";
        byte[] message = Encoding.UTF8.GetBytes(output);

        try
        {
            udpClient.Send(message, message.Length);
            Debug.Log("📡 Sent to bridge: " + output);
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
