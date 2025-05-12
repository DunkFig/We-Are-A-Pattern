using System;
using HSD.AudioBounce.Main;
using HSD.AudioBounce.Utilities;
using UnityEngine;
using Color = System.Drawing.Color;

namespace HSD.AudioBounce.Demo
{

    public class StopAudioBounceFX : MonoBehaviour
    {
        public AB_MainController mainController;
        public AB_ProbeDistributor probeDistributor;
        public int refreshRate = 32;

        // Update is called once per frame
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                mainController.disable = !mainController.disable;
                probeDistributor.disable = !probeDistributor.disable;
            }
            
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Application.Quit();
            }
            
            if (Input.GetKeyDown(KeyCode.F1))
            {
                mainController.showGizmos = !mainController.showGizmos;
                probeDistributor.drawGizmos = !probeDistributor.drawGizmos;
            }
            
            if (Input.GetKey(KeyCode.F4))
            {
                refreshRate++;
                if (probeDistributor.framesToSpreadRaycasts < 1)
                {
                    probeDistributor.framesToSpreadRaycasts = 1;
                    refreshRate = 1;
                }
                else
                {
                    probeDistributor.framesToSpreadRaycasts = refreshRate;
                }
            }

            if (Input.GetKey(KeyCode.F3))
            {
                refreshRate--;
                if (probeDistributor.framesToSpreadRaycasts < 1)
                {
                    probeDistributor.framesToSpreadRaycasts = 1;
                    refreshRate = 1;
                }
                else
                {
                    probeDistributor.framesToSpreadRaycasts = refreshRate;
                }
                probeDistributor.framesToSpreadRaycasts = refreshRate;
            }
        }

        public void OnGUI()
        {
            if (mainController == null || probeDistributor == null)
            {
                return;
            }
           
            
            GUILayout.BeginArea(new Rect(10, 10, 350, 220)); // Adjust position and size as needed

            GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 14;
            labelStyle.normal.textColor = AB_Utilities.ToColor32(Color.SandyBrown); // Default text color

            GUIStyle onStyle = new GUIStyle(labelStyle);
            onStyle.normal.textColor = AB_Utilities.ToColor32(Color.ForestGreen); // Color for "ON"

            GUIStyle offStyle = new GUIStyle(labelStyle);
            offStyle.normal.textColor = AB_Utilities.ToColor32(Color.Crimson); // Color for "OFF"

            GUIStyle counterStyle = new GUIStyle(labelStyle);
            counterStyle.normal.textColor = AB_Utilities.ToColor32(Color.Tomato); // Counter text color

            GUILayout.Label("Controls:", labelStyle);

            // Toggle Occlusion System
            GUILayout.BeginHorizontal();
            GUILayout.Label("Space: Toggle AudioBounce", labelStyle, GUILayout.Width(185));
            GUILayout.Label(mainController.disable ? "OFF" : "ON", mainController.disable ? offStyle : onStyle);
            GUILayout.EndHorizontal();

            // Toggle Gizmos
            GUILayout.BeginHorizontal();
            GUILayout.Label("F1: Toggle Gizmos", labelStyle, GUILayout.Width(185));
            GUILayout.Label(mainController.showGizmos ? "ON" : "OFF", mainController.showGizmos ? onStyle : offStyle);
            GUILayout.EndHorizontal();

            // Refresh Rate
            GUILayout.BeginHorizontal();
            GUILayout.Label("F3/F4: Adjust Refresh Rate", labelStyle, GUILayout.Width(185));
            GUILayout.Label("FPS: " + refreshRate.ToString(), counterStyle);
            GUILayout.EndHorizontal();

            GUILayout.Label("E: Interact", labelStyle);
            GUILayout.Label("Escape: Quit Application", labelStyle);

            GUILayout.EndArea();
        }
        
    }
}