using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.MRTemplate;
using Microsoft.MixedReality.Toolkit.SpatialAwareness;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Utilities;
using TMPro;

// Control model loading, model init (timing, experiment selection)
namespace IndividualModel
{
    public class IndividualModelController : MonoBehaviour
    {
        [Header("Main Settings")]
        [SerializeField] private List<GameObject> groups;
        [SerializeField] private GameObject adminButton;
        [SerializeField] private GameObject startButton;
        [SerializeField] private GameObject heatmapButton;

        [Header("UI Settings")]
        [SerializeField] private GameObject promptObject;
        [SerializeField] private GameObject groupPromptObject;
        [SerializeField] private GameObject QNAPrompt;

        [Header("Participant Language Settings")]
        [SerializeField] private GameObject languageQNA;
        [SerializeField] private bool askLanguage = false;
        private GameObject popupInstance;
        private bool isAskingLanguage = false;

        [Header("Marker Spawning Settings")]
        [Tooltip("An array of 3D marker prefabs to be spawned. Assign your marker objects here in the Inspector.")]
        [SerializeField] private bool enableLiveHeatmapOnStart = false;
        [SerializeField] private GameObject[] markerPrefabs;

        // Flow control variables
        private GameObject group;
        private int groupIndex = 0;
        private List<GameObject> models = new List<GameObject>();
        private int currentModelIndex = 0;
        private Vector3 previousModelPosition = Vector3.zero;
        public static GameObject currentModel;

        // File saving variables
        private string sessionPath;

        // recording state
        private bool admin = false;
        private bool heatmapState = false;
        public static bool recorded = false;

        void Awake() // Use Awake to get component reference before Start
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.loop = true;
        }

        // Start is called before the first frame update
        void Start()
        {
            sessionPath = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");

            for (int i = 0; i < groups.Count; i++)
            {
                models = groups[i].GetComponent<GroupItems>().GetModels();
                for (int j = 0; j < models.Count; j++)
                {
                    IndividualModelRecorder modelRecorder = models[j].GetComponent<IndividualModelRecorder>();
                    if (modelRecorder != null)
                    {
                        modelRecorder.sessionPath = sessionPath;
                        modelRecorder.QNAPrompt = QNAPrompt;
                        modelRecorder.SetMarkerPrefabs(markerPrefabs);
                        models[j].GetComponent<EyeTrackingTarget>().enabled = false;
                        models[j].SetActive(false);
                    }
                }
            }

            heatmapState = enableLiveHeatmapOnStart;
            SetAllLiveHeatmap(enableLiveHeatmapOnStart);

            promptObject.SetActive(false);
            QNAPrompt.SetActive(true);

            group = groups[0];
            models = group.GetComponent<GroupItems>().GetModels();
            groupPromptObject.GetComponent<TextMeshPro>().SetText(group.name);

            for (int i = 0; i < models.Count; i++)
            {
                models[i].transform.parent.gameObject.SetActive(true);
            }

            LoadModel();

            if (askLanguage) { ShowQuestionnaire(); }
        }

        // Update is called once per frame
        void Update()
        {
            // Auto load next
            if (recorded)
            {
                LoadNext();
            }

            // Ask for participant language
            if (isAskingLanguage)
            {
                if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKey(KeyCode.Keypad4))
                {
                    OnQuestionnaireAnswered("ENGLISH");
                }
                else if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKey(KeyCode.Keypad5))
                {
                    OnQuestionnaireAnswered("JAPANESE");
                }
                else if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKey(KeyCode.Keypad6))
                {
                    OnQuestionnaireAnswered("MANDARIN");
                }
                else if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKey(KeyCode.Keypad8))
                {
                    OnQuestionnaireAnswered("TAMIL");
                }
                else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKey(KeyCode.Keypad2))
                {
                    OnQuestionnaireAnswered("OTHER");
                }
            }

            // Show group number only before first model viewing is started
            if (currentModelIndex == 0 && !currentModel.GetComponent<IndividualModelRecorder>().isRecording)
            {
                groupPromptObject.SetActive(true);
                if ((Input.GetKey(KeyCode.Alpha7) || Input.GetKey(KeyCode.Keypad7)) && (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9)))
                {
                    groupIndex++;
                    if (groupIndex >= groups.Count())
                    {
                        groupIndex = 0;
                    }
                    SelectGroup(groupIndex);
                }

                if ((Input.GetKey(KeyCode.Alpha1) || Input.GetKey(KeyCode.Keypad1)) && (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)))
                {
                    groupIndex--;
                    if (groupIndex < 0)
                    {
                        groupIndex = groups.Count() - 1;
                    }
                    SelectGroup(groupIndex);
                }
            }
        }

        public void SetExperimentNumber(int experimentNumber)
        {
            for (int i = 0; i < groups.Count; i++)
            {
                models = groups[i].GetComponent<GroupItems>().GetModels();
                for (int j = 0; j < models.Count; j++)
                {
                    IndividualModelRecorder modelRecorder = models[j].GetComponent<IndividualModelRecorder>();
                    if (modelRecorder != null)
                    {
                        modelRecorder.SetExperimentNumber(experimentNumber);
                    }
                }
            }
        }

        public void StartRecording()
        {
            if (!currentModel.GetComponent<IndividualModelRecorder>().isRecording && !recorded && !isAskingLanguage)
            {
                groupPromptObject.SetActive(false);
                QNAPrompt.SetActive(true);
                startButton.SetActive(false);
                currentModel.GetComponent<IndividualModelRecorder>().SetIsRecording(true);
                currentModel.GetComponent<EyeTrackingTarget>().enabled = true;
            }
        }

        public void StopRecording()
        {
            if (currentModel.GetComponent<IndividualModelRecorder>().isRecording)
            {
                currentModel.GetComponent<IndividualModelRecorder>().SetIsRecording(false);
                currentModel.GetComponent<IndividualModelRecorder>().dataModule.ExportPointCloud();
                //currentModel.GetComponent<IndividualModelRecorder>().dataModule.Export3DModel(currentModel);
                currentModel.GetComponent<IndividualModelRecorder>().dataModule.ExportQuestionnaireAnswers();
                currentModel.GetComponent<EyeTrackingTarget>().enabled = false;
            }
        }

        #region Model Manipulation
        public void LoadModel()
        {
            QNAPrompt.SetActive(true);

            // Reset previous model position and rotation if there was a previous model
            if (previousModelPosition != Vector3.zero)
            {
                currentModel.transform.parent.SetPositionAndRotation(previousModelPosition, new Quaternion());
                StopRecording();
                currentModel.GetComponent<DrawOn3DTexture>().ClearDrawing();
                currentModel.SetActive(false);
            }

            // Select the next model
            currentModel = models[currentModelIndex];
            currentModel.SetActive(true);
            currentModel.GetComponent<IndividualModelRecorder>().ResetAll();

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
            if (((!currentModel.GetComponent<IndividualModelRecorder>().isRecording && recorded) || admin) && !isAskingLanguage)
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
            if (((!currentModel.GetComponent<IndividualModelRecorder>().isRecording && recorded) || admin) && !isAskingLanguage)
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

        #region Group Manipulation
        public void SelectGroup(int groupNumber)
        {
            for (int j = 0; j < models.Count(); j++)
            {
                models[j].transform.parent.gameObject.SetActive(false);
                IndividualModelRecorder recorder = models[j].GetComponent<IndividualModelRecorder>();
                recorder.ResetAll();
            }

            group = groups[groupNumber];
            models = group.GetComponent<GroupItems>().GetModels();

            sessionPath = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");
            for (int j = 0; j < models.Count(); j++)
            {
                models[j].transform.parent.gameObject.SetActive(true);
                IndividualModelRecorder recorder = models[j].GetComponent<IndividualModelRecorder>();
                recorder.sessionPath = sessionPath;
                recorder.ResetAll();
            }

            promptObject.SetActive(false);
            groupPromptObject.GetComponent<TextMeshPro>().SetText(group.name);

            groupIndex = groupNumber;

            currentModelIndex = 0;
            LoadModel();

            if (popupInstance != null)
            {
                Destroy(popupInstance.gameObject);
                if (askLanguage) { ShowQuestionnaire(); }
            }
            else
            {
                if (askLanguage) { ShowQuestionnaire(); }
            }
        }

        public List<GameObject> GetGroups()
        {
            return groups;
        }
        #endregion

        #region Admin Panel Toggles
        public void SetAllLiveHeatmap(bool val)
        {
            heatmapButton.GetComponent<ButtonUI>().setIsPressed(val);

            for (int i = 0; i < groups.Count; i++)
            {
                List<GameObject> m = groups[i].GetComponent<GroupItems>().GetModels();
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

            for (int i = 0; i < groups.Count; i++)
            {
                List<GameObject> m = groups[i].GetComponent<GroupItems>().GetModels();
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

        #region LANGUAGE QNA
        private void ShowQuestionnaire()
        {
            if (languageQNA == null)
            {
                Debug.LogError("Questionnaire popup prefab is not assigned!");
                return;
            }
            isAskingLanguage = true;
            popupInstance = Instantiate(languageQNA);
            popupInstance.SetActive(true);
            popupInstance.transform.position = CameraCache.Main.transform.position + CameraCache.Main.transform.forward * 0.4f; // x meters in front
            popupInstance.transform.forward = CameraCache.Main.transform.forward; // Orient towards the user
        }

        private void OnQuestionnaireAnswered(string selectedAnswer)
        {
            string saveDir = Path.Combine(Application.persistentDataPath, sessionPath);
            if (!Directory.Exists(saveDir))
            {
                Directory.CreateDirectory(saveDir);
            }
            StringBuilder language_sb = new StringBuilder();
            language_sb.AppendLine("language");
            language_sb.AppendLine(selectedAnswer);
            File.WriteAllText(Path.Combine(saveDir, "language.txt"), language_sb.ToString());
            isAskingLanguage = false;
            Destroy(popupInstance.gameObject);
        }
        #endregion

        #region Generate Tone
        [Header("Base Tone Settings")]
        [Range(100, 5000)]
        public static float baseFrequency = 587.33f;
        [Range(0.1f, 1f)]
        public static float amplitude = 0.5f;

        [Header("Interval Settings")]
        [Range(0.1f, 2f)]
        public static float phaseShift = 0.5f;

        public static float sampleRate = 44100f;

        private static float phase1;
        private static float phase2;

        private static AudioSource audioSource;
        private static bool isPlayingSound = false;

        public static IEnumerator PlayMajorFifthInterval(float duration)
        {
            isPlayingSound = true;
            phase1 = 0f;
            phase2 = 0f;
            audioSource.Play();

            yield return new WaitForSeconds(duration);

            //StopSineWave();
        }

        public static void StopSineWave()
        {
            audioSource.Stop();
            isPlayingSound = false;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (isPlayingSound)
            {
                float majorThirdFrequency = baseFrequency * (3f / 2f);

                for (int i = 0; i < data.Length; i += channels)
                {
                    // Generate base sine wave sample
                    float sample1 = amplitude * Mathf.Sin(phase1 * 2 * Mathf.PI);

                    // Generate major third sine wave sample
                    float sample2 = amplitude * Mathf.Sin(phase2 * 2 * Mathf.PI);

                    // Mix the two samples (simple addition, can be normalized if desired)
                    float mixedSample = (sample1 + sample2) * 0.5f;

                    // Apply to all channels
                    for (int channel = 0; channel < channels; channel++)
                    {
                        data[i + channel] = mixedSample;
                    }

                    // Increment phases for both frequencies
                    phase1 = (phase1 + (baseFrequency / sampleRate) * phaseShift) % 1f;
                    phase2 = (phase2 + (majorThirdFrequency / sampleRate) * phaseShift) % 1f;
                }
            }
            else
            {
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = 0f;
                }
            }
        }
        #endregion
    }
}
