using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainMenuController : MonoBehaviour
{
    [Header("References")]
    public GameObject menuPanel;           
    public WebCamMusicSystem musicSystem;  

    [Header("Sliders & Value Labels")]
    public Slider brightnessSlider;
    public TMP_Text brightnessValueText;

    public Slider darknessSlider;
    public TMP_Text darknessValueText;

    public Slider minSpeedSlider;
    public TMP_Text minSpeedValueText;

    public Slider maxSpeedSlider;
    public TMP_Text maxSpeedValueText;

    [Header("Buttons")]
    public Button resetSceneButton;

    void Start()
    {
        // initialize slider positions from musicSystem
        brightnessSlider.value   = musicSystem.brightThreshold;
        darknessSlider.value    = musicSystem.darkThreshold;
        minSpeedSlider.value     = musicSystem.minSpeed;
        maxSpeedSlider.value     = musicSystem.maxSpeed;

        // set initial labels
        UpdateBrightnessText(brightnessSlider.value);
        UpdateDarknessText(darknessSlider.value);
        UpdateMinSpeedText(minSpeedSlider.value);
        UpdateMaxSpeedText(maxSpeedSlider.value);

        // hook up slider events
        brightnessSlider.onValueChanged.AddListener(v => {
            musicSystem.brightThreshold = v;
            UpdateBrightnessText(v);
        });
        darknessSlider.onValueChanged.AddListener(v => {
            musicSystem.darkThreshold = v;
            UpdateDarknessText(v);
        });
        minSpeedSlider.onValueChanged.AddListener(v => {
            musicSystem.minSpeed = v;
            UpdateMinSpeedText(v);
        });
        maxSpeedSlider.onValueChanged.AddListener(v => {
            musicSystem.maxSpeed = v;
            UpdateMaxSpeedText(v);
        });

        // now Reset just reconnects the camera, not reload the scene
        resetSceneButton.onClick.AddListener(() => {
            musicSystem.ReconnectCamera();
            menuPanel.SetActive(false);
        });

        // hide menu at start
        menuPanel.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            menuPanel.SetActive(!menuPanel.activeSelf);
    }

    // helper methods to update the text labels
    void UpdateBrightnessText(float v)   => brightnessValueText.text = v.ToString("F1");
    void UpdateDarknessText(float v)    => darknessValueText.text  = v.ToString("F1");
    void UpdateMinSpeedText(float v)    => minSpeedValueText.text  = v.ToString("F2");
    void UpdateMaxSpeedText(float v)    => maxSpeedValueText.text  = v.ToString("F2");
}
