using IndividualModel;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.MRTemplate;
using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using static Microsoft.MixedReality.Toolkit.MRTemplate.DataModule;

// Contains Experiment flow, SetRecording
namespace MuseumModel
{
    public class MuseumLayoutController : MonoBehaviour
    {
        [Header("Main Settings")]
        [SerializeField] private List<GameObject> layoutButtons;
        [SerializeField] private GameObject adminButton;
        [SerializeField] private GameObject startButton;
        [SerializeField] private GameObject heatmapButton;

        [Header("UI Settings")]
        [SerializeField] private GameObject layoutPromptObject;

        [Header("Heatmap Settings")]
        [SerializeField] private bool enableLiveHeatmapOnStart = false;

        // Flow control variables
        private GameObject layout;
        private GameObject button;
        private int currentLayoutIndex = 0;
        private Vector3 previousLayoutPosition = Vector3.zero;
        public static GameObject currentLayout;
        private List<GameObject> models = new List<GameObject>();

        // File saving variables
        private string sessionPath;

        // recording state
        private bool admin = false;
        private bool heatmapState = false;
        public static bool recorded = false;

        // Start is called before the first frame update
        void Start()
        {
            sessionPath = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");

            for (int i = 0; i < layoutButtons.Count; i++)
            {
                models = layoutButtons[i].GetComponent<GroupItems>().GetModels();
                layout = layoutButtons[i].GetComponent<GroupItems>().GetLayout();
                layout.GetComponent<MuseumModelController>().SetModels(models);
                for (int j = 0; j < models.Count; j++)
                {
                    MuseumModelRecorder modelRecorder = models[j].GetComponent<MuseumModelRecorder>();
                    if (modelRecorder != null)
                    {
                        modelRecorder.viewBlocker.SetActive(true);
                        models[j].GetComponent<EyeTrackingTarget>().enabled = false;
                        models[j].SetActive(false);
                    }
                }
                layout.SetActive(false);
            }

            heatmapState = enableLiveHeatmapOnStart;
            SetAllLiveHeatmap(enableLiveHeatmapOnStart);

            layout = layoutButtons[0].GetComponent<GroupItems>().GetLayout();
            models = layoutButtons[0].GetComponent<GroupItems>().GetModels();
            layoutPromptObject.GetComponent<TextMeshPro>().SetText(layoutButtons[0].name);
            for (int i = 0; i < models.Count; i++)
            {
                models[i].transform.parent.gameObject.SetActive(true);
            }
            layout.SetActive(true);

            LoadLayout();
        }

        // Update is called once per frame
        void Update()
        {
            // Show group number only before first layout viewing is started
            if (!currentLayout.GetComponent<MuseumModelController>().isRecording)
            {
                layoutPromptObject.SetActive(true);
                if ((Input.GetKey(KeyCode.Alpha7) || Input.GetKey(KeyCode.Keypad7)) && (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9)))
                {
                    currentLayoutIndex++;
                    if (currentLayoutIndex >= layoutButtons.Count())
                    {
                        currentLayoutIndex = 0;
                    }
                    SelectLayout(currentLayoutIndex);
                }

                if ((Input.GetKey(KeyCode.Alpha1) || Input.GetKey(KeyCode.Keypad1)) && (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)))
                {
                    currentLayoutIndex--;
                    if (currentLayoutIndex < 0)
                    {
                        currentLayoutIndex = layoutButtons.Count() - 1;
                    }
                    SelectLayout(currentLayoutIndex);
                }
            }
        }

        public void SetExperimentNumber(int experimentNumber)
        {
            for (int i = 0; i < layoutButtons.Count; i++)
            {
                MuseumModelController modelController = layoutButtons[i].GetComponent<GroupItems>().GetLayout().GetComponent<MuseumModelController>();
                modelController.SetExperimentNumber(experimentNumber);
            }
        }

        public void StartRecording()
        {
            if (!currentLayout.GetComponent<MuseumModelController>().isRecording && !recorded)
            {
                layoutPromptObject.SetActive(false);
                startButton.SetActive(false);
                currentLayout.GetComponent<MuseumModelController>().SetIsRecording(true);
            }
        }

        public void StopRecording()
        {
            if (currentLayout.GetComponent<MuseumModelController>().isRecording)
            {
                currentLayout.GetComponent<MuseumModelController>().SetIsRecording(false);
            }
        }

        #region Layout Manipulation
        public void LoadLayout()
        {
            // Reset previous layout position and rotation if there was a previous layout
            if (previousLayoutPosition != Vector3.zero)
            {
                currentLayout.transform.SetPositionAndRotation(previousLayoutPosition, new Quaternion());
                StopRecording();
                currentLayout.SetActive(false);
            }

            // Select the next layout
            models = layoutButtons[currentLayoutIndex].GetComponent<GroupItems>().GetModels();
            currentLayout = layoutButtons[currentLayoutIndex].GetComponent<GroupItems>().GetLayout();
            currentLayout.GetComponent<MuseumModelController>().SetModels(models);
            currentLayout.SetActive(true);
            currentLayout.GetComponent<MuseumModelController>().ResetAll();

            sessionPath = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");
            for (int j = 0; j < models.Count(); j++)
            {
                models[j].transform.parent.gameObject.SetActive(true);
                MuseumModelRecorder recorder = models[j].GetComponent<MuseumModelRecorder>();
                recorder.InitializeState(sessionPath);
                recorder.ResetAll();
            }

            // Record the original transform
            previousLayoutPosition = currentLayout.transform.position;

            // Move the layout to the viewing area
            currentLayout.transform.position = transform.position;

            recorded = false;

            startButton.SetActive(true);

            if (admin)
            {
                ToggleAdminMode();
            }
        }

        public void SelectLayout(int layoutNumber)
        {
            for (int j = 0; j < models.Count(); j++)
            {
                models[j].transform.parent.gameObject.SetActive(false);
                MuseumModelRecorder recorder = models[j].GetComponent<MuseumModelRecorder>();
                recorder.ResetAll();
            }

            layoutPromptObject.SetActive(true);
            layoutPromptObject.GetComponent<TextMeshPro>().SetText(layout.name);

            currentLayoutIndex = layoutNumber;

            LoadLayout();
        }

        public List<GameObject> GetLayouts()
        {
            return layoutButtons;
        }
        #endregion

        #region Admin Panel Toggles
        public void SetAllLiveHeatmap(bool val)
        {
            heatmapButton.GetComponent<ButtonUI>().setIsPressed(val);

            for (int i = 0; i < layoutButtons.Count; i++)
            {
                List<GameObject> m = layoutButtons[i].GetComponent<GroupItems>().GetModels();
                for (int j = 0; j < m.Count(); j++)
                {
                    m[j].GetComponent<DrawOn3DTexture>().ToggleLiveHeatmap(val);
                    m[j].GetComponent<DrawOn3DTexture>().enabled = val;
                }
            }
        }

        public void ToggleAllLiveHeatmap()
        {
            heatmapState = !heatmapState;
            heatmapButton.GetComponent<ButtonUI>().setIsPressed(heatmapState);

            for (int i = 0; i < layoutButtons.Count; i++)
            {
                List<GameObject> m = layoutButtons[i].GetComponent<GroupItems>().GetModels();
                for (int j = 0; j < m.Count(); j++)
                {
                    m[j].GetComponent<DrawOn3DTexture>().ToggleLiveHeatmap(heatmapState);
                    m[j].GetComponent<DrawOn3DTexture>().enabled = heatmapState;
                }
            }
        }

        public void ToggleAdminMode()
        {
            admin = !admin;

            adminButton.GetComponent<ButtonUI>().setIsPressed(admin);
        }

        public static void ToggleRecorded()
        {
            recorded = !recorded;
        }
        #endregion
    }
}
