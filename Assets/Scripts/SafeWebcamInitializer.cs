using System;
using System.Collections;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

public class SafeWebcamInitializer : MonoBehaviour
{
    WebCamTexture webcamTexture;

    void Start()
    {
#if UNITY_IOS || UNITY_WEBGL
        StartCoroutine(AskForPermissionIfRequired(UserAuthorization.WebCam, () => InitializeCamera()));
#elif UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += (perm) => { StartCoroutine(DelayedInit()); };
            callbacks.PermissionDenied += (perm) => { Debug.LogWarning("Camera permission denied."); };
            Permission.RequestUserPermission(Permission.Camera, callbacks);
        }
        else
        {
            StartCoroutine(DelayedInit());
        }
#else
        InitializeCamera();
#endif
    }

#if UNITY_IOS || UNITY_WEBGL
    private IEnumerator AskForPermissionIfRequired(UserAuthorization auth, Action onGranted)
    {
        if (!Application.HasUserAuthorization(auth))
        {
            yield return Application.RequestUserAuthorization(auth);
        }

        if (Application.HasUserAuthorization(auth))
        {
            onGranted?.Invoke();
        }
        else
        {
            Debug.LogWarning($"{auth} permission denied");
        }
    }
#endif

    private IEnumerator DelayedInit()
    {
        yield return null; // delay one frame to allow Unity to process permission state
        InitializeCamera();
    }

    private void InitializeCamera()
    {
        Debug.Log("Initializing webcam...");
        webcamTexture = new WebCamTexture();

        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material.mainTexture = webcamTexture;
        }

        webcamTexture.Play();
    }
}
