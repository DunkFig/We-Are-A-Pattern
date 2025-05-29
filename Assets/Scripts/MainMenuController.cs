using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenuController : MonoBehaviour
{
    [Header("References")]
    public GameObject menuPanel;           
    public WebCamMusicSystem musicSystem;  

    [Header("Sliders & Value Labels")]
    public Slider brightnessSlider;
    public TMP_Text brightnessValueText;

    public Slider darkIncSlider;
    public TMP_Text darkIncValueText;

    public Slider redIncSlider;
    public TMP_Text redIncValueText;

    // This slider now controls the Bright accumulator increment
    public Slider greenIncSlider;
    public TMP_Text greenIncValueText;

    public Slider blueIncSlider;
    public TMP_Text blueIncValueText;

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
        darkIncSlider.value      = musicSystem.darkIncrement;
        redIncSlider.value       = musicSystem.redIncrement;
        greenIncSlider.value     = musicSystem.brightIncrement;
        blueIncSlider.value      = musicSystem.blueIncrement;
        minSpeedSlider.value     = musicSystem.minSpeed;
        maxSpeedSlider.value     = musicSystem.maxSpeed;

        // set initial labels
        UpdateBrightnessText(brightnessSlider.value);
        UpdateDarkIncText   (darkIncSlider.value);
        UpdateRedIncText    (redIncSlider.value);
        UpdateGreenIncText  (greenIncSlider.value);
        UpdateBlueIncText   (blueIncSlider.value);
        UpdateMinSpeedText  (minSpeedSlider.value);
        UpdateMaxSpeedText  (maxSpeedSlider.value);

        // hook up slider events
        brightnessSlider.onValueChanged.AddListener(v => {
            musicSystem.brightThreshold = v;
            UpdateBrightnessText(v);
        });
        darkIncSlider.onValueChanged.AddListener(v => {
            musicSystem.darkIncrement = Mathf.RoundToInt(v);
            UpdateDarkIncText(v);
        });
        redIncSlider.onValueChanged.AddListener(v => {
            musicSystem.redIncrement = Mathf.RoundToInt(v);
            UpdateRedIncText(v);
        });
        greenIncSlider.onValueChanged.AddListener(v => {
            musicSystem.brightIncrement = Mathf.RoundToInt(v);
            UpdateGreenIncText(v);
        });
        blueIncSlider.onValueChanged.AddListener(v => {
            musicSystem.blueIncrement = Mathf.RoundToInt(v);
            UpdateBlueIncText(v);
        });
        minSpeedSlider.onValueChanged.AddListener(v => {
            musicSystem.minSpeed = v;
            UpdateMinSpeedText(v);
        });
        maxSpeedSlider.onValueChanged.AddListener(v => {
            musicSystem.maxSpeed = v;
            UpdateMaxSpeedText(v);
        });

        // reset‐scene button
        resetSceneButton.onClick.AddListener(ResetScene);

        // hide menu at start
        menuPanel.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            menuPanel.SetActive(!menuPanel.activeSelf);
    }

    public void ResetScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // helper methods to update the text labels
    void UpdateBrightnessText(float v) { brightnessValueText.text = v.ToString("F1"); }
    void UpdateDarkIncText   (float v) { darkIncValueText.text      = Mathf.RoundToInt(v).ToString(); }
    void UpdateRedIncText    (float v) { redIncValueText.text       = Mathf.RoundToInt(v).ToString(); }
    void UpdateGreenIncText  (float v) { greenIncValueText.text     = Mathf.RoundToInt(v).ToString(); }
    void UpdateBlueIncText   (float v) { blueIncValueText.text      = Mathf.RoundToInt(v).ToString(); }
    void UpdateMinSpeedText  (float v) { minSpeedValueText.text     = v.ToString("F2"); }
    void UpdateMaxSpeedText  (float v) { maxSpeedValueText.text     = v.ToString("F2"); }
}
