using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WebCamSetUp : MonoBehaviour
{
    WebCamTexture webcamTexture;

    void Start()
    {
        WebCamDevice[] devices = WebCamTexture.devices;

        if (devices.Length > 0)
        {
            Debug.Log("Webcam found: " + devices[0].name);
            webcamTexture = new WebCamTexture(devices[0].name);
            Renderer renderer = GetComponent<Renderer>();
            renderer.material.mainTexture = webcamTexture;
            webcamTexture.Play();
        }
        else
        {
            Debug.LogWarning("No webcam devices found.");
        }
    }

    void Update()
    {
        // Optional updates here
    }
}
