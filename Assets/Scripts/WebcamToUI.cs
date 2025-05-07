using UnityEngine;
using UnityEngine.UI;

public class WebcamToUI : MonoBehaviour
{
    public RawImage rawImage;         // assign in inspector
    private WebCamTexture webcamTexture;

    void Start()
    {
        if (WebCamTexture.devices.Length > 0)
        {
            WebCamDevice device = WebCamTexture.devices[0];
            webcamTexture = new WebCamTexture(device.name, 640, 480, 30);
            rawImage.texture = webcamTexture;
            rawImage.material.mainTexture = webcamTexture;
            webcamTexture.Play();
        }
        else
        {
            Debug.LogWarning("No webcam found.");
        }
    }
}
