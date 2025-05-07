using UnityEngine;
using UnityEngine.UI;
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
    [Range(1, 60)] public float frameRate = 12f; // You can tune in Inspector

    private float frameInterval;
    private float lastFrameTime;

    [Header("Increment Settings")]
    [Range(0, 1000)] public int redIncrement = 20;
    [Range(0, 1000)] public int greenIncrement = 20;
    [Range(0, 1000)] public int blueIncrement = 20;
    [Range(0, 1000)] public int darkIncrement = 20;

    [Header("Centering Behavior")]
    [Range(0, 4096)] public float CenteringThreshold = 500f;
    [Range(0f, 1f)] public float CenteringSpeed = 0.05f; // How fast it returns to center

    // Internal values (start centered)
    private float darkCV = 2048f;
    private float redCV = 2048f;
    private float greenCV = 2048f;
    private float blueCV = 2048f;

    UdpClient udpClient;

    [Header("Serial Settings")]
    public string portName = "/dev/cu.usbmodem144101"; // Adjust for your Linux Arduino port
    public int baudRate = 115200;

    [Header("Processing Settings")]
    public int divisionAmount = 20;
    public float brightnessThreshold = 100f;

    private WebCamTexture webcamTex;

    private Color[] stripeColors;
    private int darkestStripeIndex = -1;
    private float darkestBrightness = float.MaxValue;

    private Color32[] pixels;
    private int camWidth, camHeight;

    void Start()
    {
        Application.runInBackground = true;
        
        frameInterval = 1f / frameRate;
        lastFrameTime = Time.time;

        // Initialize webcam
        webcamTex = new WebCamTexture();
        webcamTex.Play();
        webcamDisplay.texture = webcamTex;

        // Wait for camera to initialize
        InvokeRepeating("TryInitPixels", 0.5f, 0.5f);

        // Open Serial
       udpClient = new UdpClient();
       udpClient.Connect("127.0.0.1", 9000); // Match Python bridge port

        stripeColors = new Color[divisionAmount];
    }

    void TryInitPixels()
    {
        if (webcamTex.width > 16)
        {
            camWidth = webcamTex.width;
            camHeight = webcamTex.height;
            pixels = new Color32[camWidth * camHeight];
            CancelInvoke("TryInitPixels");
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
        {
            stripeImages[i].color = stripeColors[i];
        }

        if (darkestStripeIndex >= 0)
        {
            RectTransform highlightRT = darkestHighlight.rectTransform;
            highlightRT.position = stripeImages[darkestStripeIndex].rectTransform.position;
        }

        thresholdIndicator.enabled = (darkestBrightness < brightnessThreshold);
        if (thresholdIndicator.enabled)
        {
            RectTransform tr = thresholdIndicator.rectTransform;
            tr.position = stripeImages[darkestStripeIndex].rectTransform.position + new Vector3(60, 0, 0);

                // DARK logic — normalized to center point
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

    // RED logic
    float redDelta = (color.r - 0.5f) * 2f; // range from -1 to 1
    redCV += redDelta * redIncrement;

    // GREEN logic
    float greenDelta = (color.g - 0.5f) * 2f;
    greenCV += greenDelta * greenIncrement;

    // BLUE logic
    float blueDelta = (color.b - 0.5f) * 2f;
    blueCV += blueDelta * blueIncrement;

    // Clamp all CV values to valid DAC range
    redCV = Mathf.Clamp(redCV, 0, 4095);
    greenCV = Mathf.Clamp(greenCV, 0, 4095);
    blueCV = Mathf.Clamp(blueCV, 0, 4095);
    darkCV = Mathf.Clamp(darkCV, 0, 4095);

    // Send to Arduino
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
        // if (serialPort != null && serialPort.IsOpen) serialPort.Close();
        // if (webcamTex != null && webcamTex.isPlaying) webcamTex.Stop();
    }
}
