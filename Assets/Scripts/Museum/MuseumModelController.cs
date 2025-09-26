using IndividualModel;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.MRTemplate;
using Microsoft.MixedReality.Toolkit.SpatialAwareness;
using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

// Controls MuseumLayout loading
namespace MuseumModel
{
    public class MuseumModelController : MonoBehaviour
    {
        // Control flow flags
        private float timer = 0;
        public bool isRecording = false;
        private int experimentNumber = 1;

        // Experiment duration control
        private float recordGazeDuration = 150.0f;

        private List<GameObject> models;

        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            if (experimentNumber == 1)
            {
                EyeGazeFlow();
            }
        }

        public void SetModels(List<GameObject> models)
        {
            this.models = models;
        }

        public void SetIsRecording(bool val)
        {
            isRecording = val;
            timer = recordGazeDuration; // reset timer

            if (val)
            {
                foreach (GameObject model in models)
                {
                    model.GetComponent<MuseumModelRecorder>().viewBlocker.SetActive(false);
                    model.GetComponent<EyeTrackingTarget>().enabled = true;
                    model.SetActive(true);
                }
            }
            else
            {
                SaveFiles();
                foreach (GameObject model in models)
                {
                    model.GetComponent<MuseumModelRecorder>().viewBlocker.SetActive(true);
                    model.GetComponent<EyeTrackingTarget>().enabled = false;
                    model.SetActive(false);
                }
            }
        }

        public void SetExperimentNumber(int n)
        {
            experimentNumber = n;
        }

        public void SaveFiles()
        {
            for (int j = 0; j < models.Count; j++)
            {
                MuseumModelRecorder modelRecorder = models[j].GetComponent<MuseumModelRecorder>();
                if (modelRecorder != null)
                {
                    Debug.Log(modelRecorder.name);
                    modelRecorder.SaveData();
                }
            }
        }

        public void ResetAll()
        {

        }

        #region EXPERIMENT FLOWS
        private void EyeGazeFlow()
        {
            /* CHECK IF RECORDING STARTED */
            if (!isRecording || MuseumLayoutController.currentLayout == null) return;

            /* GET GAZED OBJECT */
            var eyeTarget = EyeTrackingTarget.LookedAtEyeTarget;
            var gazedObject = eyeTarget != null ? eyeTarget.gameObject : null;

            /* RECORD GAZE DATA */
            if (timer > 0)
            {
                timer -= Time.deltaTime;

                if (gazedObject != null)
                {
                    foreach (GameObject model in models)
                    {
                        if (gazedObject.name == model.name)
                        {
                            model.GetComponent<MuseumModelRecorder>().dataModule.RecordGazeData(model);
                        }
                    }
                }
            }
            else
            {
                // Save files
                SetIsRecording(false);

                MuseumLayoutController.ToggleRecorded();
            }
        }
        #endregion
    }
}