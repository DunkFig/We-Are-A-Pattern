using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class PS4MenuController : MonoBehaviour
{
    [Header("Menu")]
    public GameObject menuPanel;
    public Selectable[] menuItems;      // sliders and buttons in the order you want

    [Header("Systems")]
    public WebCamMusicSystem webCamMusic;
    public GameObject freeFlyCam;
    public CameraSystemController cameraSystemController;

    int currentIndex = 0;
    bool menuOpen = false, usingFreeFly = false;
    bool dpadYInUse = false, dpadXInUse = false;

    void Update()
    {
        // --- 1) Catch *any* button press on Joystick1 and log ---
        //    (buttons 0 through 19)
        for (int b = 0; b <= 19; b++)
        {
            KeyCode code = KeyCode.Joystick1Button0 + b;
            if (Input.GetKeyDown(code))
                Debug.Log($"<color=cyan>Joystick1 Button{b} pressed</color>");
        }

        // --- 2) Log D-pad presses if you like ---
        float dpadX = Input.GetAxis("DPadX");
        float dpadY = Input.GetAxis("DPadY");
        if (dpadY >  0.5f) Debug.Log("D-pad Up");
        if (dpadY < -0.5f) Debug.Log("D-pad Down");
        if (dpadX >  0.5f) Debug.Log("D-pad Right");
        if (dpadX < -0.5f) Debug.Log("D-pad Left");

        // --- 3) OPTIONS (Button 9) toggles your menu ---
        if (Input.GetKeyDown(KeyCode.Joystick1Button9))
        {
            menuOpen = !menuOpen;
            menuPanel.SetActive(menuOpen);

            if (menuOpen)
            {
                currentIndex = 0;
                EventSystem.current.SetSelectedGameObject(menuItems[0].gameObject);
            }
            else
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        // --- 4) If menu is open: D-pad navigates, Cross (0) “clicks” ---
        if (menuOpen)
        {
            // vertical nav
            if (Mathf.Abs(dpadY) > 0.5f)
            {
                if (!dpadYInUse)
                {
                    currentIndex = (currentIndex + (dpadY < 0 ? 1 : -1) + menuItems.Length)
                                 % menuItems.Length;
                    EventSystem.current.SetSelectedGameObject(menuItems[currentIndex].gameObject);
                    dpadYInUse = true;
                }
            }
            else dpadYInUse = false;

            // horizontal tweak
            if (Mathf.Abs(dpadX) > 0.5f)
            {
                if (!dpadXInUse)
                {
                    if (menuItems[currentIndex] is Slider s)
                    {
                        float step = s.wholeNumbers ? 1f : s.maxValue * 0.01f;
                        s.value = Mathf.Clamp(s.value + (dpadX > 0 ? step : -step),
                                              s.minValue, s.maxValue);
                    }
                    dpadXInUse = true;
                }
            }
            else dpadXInUse = false;

            // Cross to “submit”
            if (Input.GetKeyDown(KeyCode.Joystick1Button0))
            {
                if (menuItems[currentIndex] is Button btn)
                    btn.onClick.Invoke();
            }
        }

        // --- 5) TRIANGLE (Button 3) toggles free-fly vs camera system ---
        if (Input.GetKeyDown(KeyCode.Joystick1Button3))
        {
            usingFreeFly = !usingFreeFly;
            freeFlyCam.SetActive(usingFreeFly);
            webCamMusic.enabled              = !usingFreeFly;
            cameraSystemController.enabled   = !usingFreeFly;
        }

        // --- 6) SQUARE (Button 2) steps to next camera if not in free-fly ---
        if (!usingFreeFly && Input.GetKeyDown(KeyCode.Joystick1Button2))
        {
            cameraSystemController.StepToNextPairOrGroup();
        }
    }
}
