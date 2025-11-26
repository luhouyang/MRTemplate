using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.MRTemplate;


public class ERCGazeController : MonoBehaviour
{
    [SerializeField]
    private List<GameObject> groups;

    [SerializeField]
    private GameObject promptObject;

    [SerializeField] private GameObject adminOnUI;
    [SerializeField] private GameObject adminOffUI;

    [SerializeField] private GameObject startButton;

    [Header("Marker Spawning Settings")]
    [Tooltip("An array of 3D marker prefabs to be spawned. Assign your marker objects here in the Inspector.")]
    [SerializeField] private bool enableLiveHeatmapOnStart = false;

    private List<GameObject> models = new List<GameObject>();
    private int currentModelIndex = 0;
    private Vector3 previousModelPosition = Vector3.zero;
    public static GameObject currentModel;

    private string sessionPath;
    private GameObject group;

    // recording state
    private bool admin = false;
    public static bool recorded = false;

    private int groupIndex = 0;

    void Start()
    {
        sessionPath = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + "_precision";

        for (int i = 0; i < groups.Count; i++)
        {
            models = groups[i].GetComponent<GroupItems>().GetModels();
            for (int j = 0; j < models.Count; j++)
            {
                ERCGazeRecorder modelRecorder = models[j].GetComponent<ERCGazeRecorder>();
                if (modelRecorder != null)
                {
                    models[j].GetComponent<ERCGazeRecorder>().sessionPath = sessionPath;
                    //models[j].GetComponent<EyeTrackingTarget>().enabled = false;
                    models[j].SetActive(false);
                }
            }
        }

        //if (!enableLiveHeatmapOnStart) { DisableAllLiveHeatmap(); } 

        promptObject.SetActive(false);

        group = groups[0];
        models = group.GetComponent<GroupItems>().GetModels();

        for (int i = 0; i < models.Count; i++) 
        {
            models[i].transform.parent.gameObject.SetActive(true);
        }

        LoadModel();
    }

    void Update()
    {
        if (recorded)
        {
            LoadNext();
        }
    }

    public void StartRecording()
    {
        if (!currentModel.GetComponent<ERCGazeRecorder>().isRecording && !recorded)
        {
            startButton.SetActive(false);
            currentModel.GetComponent<ERCGazeRecorder>().SetIsRecording(true);
            //currentModel.GetComponent<EyeTrackingTarget>().enabled = true;
        }
    }

    public void StopRecording()
    {
        if (currentModel.GetComponent<ERCGazeRecorder>().isRecording)
        {
            currentModel.GetComponent<ERCGazeRecorder>().SetIsRecording(false);
            currentModel.GetComponent<ERCGazeRecorder>().SaveAllData();
            //currentModel.GetComponent<EyeTrackingTarget>().enabled = false;
        }
    }

    #region Model Manipulation
    public void LoadModel()
    {
        // Reset previous model position and rotation if there was a previous model
        if (previousModelPosition != Vector3.zero)
        {
            currentModel.transform.parent.SetPositionAndRotation(previousModelPosition, new Quaternion());
            StopRecording();
            //currentModel.GetComponent<DrawOn3DTexture>().ClearDrawing();
            currentModel.SetActive(false);
        }

        // Select the next model
        currentModel = models[currentModelIndex];
        currentModel.SetActive(true);
        currentModel.GetComponent<ERCGazeRecorder>().ResetAll();

        // Record the original transform
        previousModelPosition = currentModel.transform.parent.position;

        // Move the model to the viewing area
        currentModel.transform.parent.position = transform.position;

        recorded = false;

        startButton.SetActive(true);

        if (admin)
        {
            ToggleAdminMode();
        }
    }

    public void LoadPrevious()
    {
        if (((!currentModel.GetComponent<ERCGazeRecorder>().isRecording && recorded) || admin))
        {
            if (currentModelIndex == 0)
            {
                currentModelIndex = 0;
                StopRecording();
            }
            else
            {
                currentModelIndex--;
                LoadModel();
            }

            Debug.Log("Loading " + models[currentModelIndex].name);
        }
    }

    public void LoadNext()
    {
        if (((!currentModel.GetComponent<ERCGazeRecorder>().isRecording && recorded) || admin))
        {
            if (currentModelIndex == models.Count - 1)
            {
                promptObject.SetActive(true);
                StopRecording();
            }
            else
            {
                currentModelIndex++;
                LoadModel();
            }

            recorded = false;
            Debug.Log("Loading " + models[currentModelIndex].name);
        }
    }
    #endregion

    public void SelectGroup(int groupNumber)
    {
        for (int j = 0; j < models.Count(); j++)
        {
            models[j].transform.parent.gameObject.SetActive(false);
            ERCGazeRecorder recorder = models[j].GetComponent<ERCGazeRecorder>();
            recorder.ResetAll();
        }

        group = groups[groupNumber];
        models = group.GetComponent<GroupItems>().GetModels();

        sessionPath = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + "_precision";
        for (int j = 0; j < models.Count(); j++)
        {
            models[j].transform.parent.gameObject.SetActive(true);
            ERCGazeRecorder recorder = models[j].GetComponent<ERCGazeRecorder>();
            recorder.sessionPath = sessionPath;
            recorder.ResetAll();
        }

        promptObject.SetActive(false);

        groupIndex = groupNumber;

        currentModelIndex = 0;
        LoadModel();
    }

    #region Admin Panel Toggles
    //public void DisableAllLiveHeatmap()
    //{
    //    for (int i = 0; i < groups.Count; i++)
    //    {
    //        List<GameObject> m = groups[i].GetComponent<GroupItems>().GetModels();
    //        for (int j = 0; j < m.Count(); j++)
    //        {
    //            m[j].GetComponent<DrawOn3DTexture>().ToggleLiveHeatmap(false);
    //            m[j].GetComponent<DrawOn3DTexture>().enabled = false;
    //        }
    //    }
    //}

    public void ToggleAdminMode()
    {
        admin = !admin;

        if (admin)
        {
            adminOnUI.SetActive(true);
            adminOffUI.SetActive(false);
        }
        else
        {
            adminOnUI.SetActive(false);
            adminOffUI.SetActive(true);
        }
    }

    public static void ToggleRecorded()
    {
        recorded = !recorded;
    }
    #endregion
}
