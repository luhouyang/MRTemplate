using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.MRTemplate;
using Microsoft.MixedReality.Toolkit.Utilities;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

// Contains DataModule, Audio init, SetRecording, Experiment flows, QNA
namespace IndividualModel
{
    public class IndividualModelRecorder : MonoBehaviour
    {
        // Control flow flags
        private bool savedGaze;
        private float timer = 0;
        public bool isRecording = false;
        private bool isRecordingAudio = false;
        private int experimentNumber = 1;

        // Experiment duration control
        private float recordGazeDuration = 60.0f;
        private float recordVoiceDuration = 45.0f;

        // Record audio
        private AudioSource audioSource;

        // QNA
        private Dictionary<string, string> answerChoices = new Dictionary<string, string> {
            {"4", "面白い・気になる形だ" },
            {"6", "美しい・芸術的だ" },
            {"8", "不思議・意味不明" },
            {"2", "不気味・不安・怖い" },
            {"5", "何も感じない" },
        };
        private Dictionary<string, int> markerMapping = new Dictionary<string, int> {
            {"4", 0 },
            {"6", 1 },
            {"8", 2 },
            {"2", 3 },
            {"5", 4 },
        };
        private Vector3 lastLocalHitPosition = Vector3.zero;
        private Vector3 globalHitPosition = Vector3.zero;
        private Vector3 hitNormal = Vector3.zero;

        // Heatmap
        private DrawOn3DTexture heatmapSource;
        private GameObject[] markerPrefabs;

        [Header("View Blocker")]
        [SerializeField] private GameObject viewBlocker;

        [Header("Prompt")]
        [SerializeField] private GameObject promptObject;
        // Audio recording prompt variables
        private Vector3 promptInitialPosition;
        private string question = "「この土器/土偶の全体的あるいは部分的な印象をなるべく具体的な言葉を使って45秒以内で話してください」";
        //private string question = "Please speak your overall or partial impression of this pottery/clay figurine in 45 seconds or less using as specific words as possible.";
        private string enterText = "「Enter」キーを押してください";
        //private string enterText = "Press 'ENTER'";
        private float rotationSpeed = 5f;
        private float rotationThresholdDegrees = 1.0f;
        private float followDistance = 1.5f;
        private float moveSpeed = 5f;
        private Vector3 rotationDisplacement = new Vector3(0f, 0f, 0f);
        private float horizontalDisplacement = 0.0f;
        private float verticalDisplacement = 0.15f;
        private GameObject objectWithCollider = null;
        private Quaternion targetRotation; // The rotation we are trying to achieve
        private Vector3 targetPosition; // The position we are trying to achieve

        [Header("Audio Recording Settings")]
        public int audioSampleRate = 44100;
        private AudioClip recordedAudio;
        private float chunkStartTime = 0f;
        private int chunkIndex = 0;

        public GameObject QNAPrompt;
        public string sessionPath;
        public DataModule dataModule;

        // Start is called before the first frame update
        void Start()
        {
            heatmapSource = GetComponent<DrawOn3DTexture>();

            GameObject audioObject = new GameObject("AudioRecorder");
            audioSource = audioObject.AddComponent<AudioSource>();
            DontDestroyOnLoad(audioObject);

            // Instruction prompt
            viewBlocker.SetActive(true);
            promptObject.SetActive(true);
            promptInitialPosition = promptObject.transform.localPosition;
            promptObject.GetComponent<TextMeshPro>().SetText(enterText);

            // Audio prompt
            if (objectWithCollider == null)
            {
                Collider coll = GetComponent<Collider>();
                if (coll == null)
                {
                    coll = GetComponentInChildren<Collider>();
                }
                if (coll != null)
                {
                    objectWithCollider = coll.gameObject;
                }
                else
                {
                    Debug.LogWarning("FaceUser: No collider found on this GameObject or its children.");
                }
            }

            targetRotation = promptObject.transform.rotation;
            targetPosition = promptObject.transform.position; // Initialize target position
        }

        // Update is called once per frame
        void Update()
        {
            if (experimentNumber == 1)
            {
                EyeGazeAndQNAThenVoice();
            }
        }

        public void SetIsRecording(bool val)
        {
            /* Reset & clear variables (isRecording, timer, startingTime, savedGaze) */
            isRecording = val;
            timer = recordGazeDuration + recordVoiceDuration; // reset timer
            savedGaze = !val; // reset recording state

            /* Toggle viewBlocker */
            viewBlocker.SetActive(!val);

            if (val && IndividualModelController.currentModel != null)
            {
                /* Create data directory */
                string saveDir = Path.Combine(Application.persistentDataPath, sessionPath, gameObject.name);
                if (!Directory.Exists(saveDir))
                {
                    Directory.CreateDirectory(saveDir);
                }
                dataModule = new DataModule(saveDir, Time.unscaledTimeAsDouble, gameObject, gameObject.GetComponent<MeshFilter>());
            }
            else
            {
                promptObject.GetComponent<TextMeshPro>().SetText("");
                promptObject.transform.localPosition = promptInitialPosition;
            }

            // start audio recording here
            if (val && !isRecordingAudio)
            {

            }
            else if (!val && isRecordingAudio)
            {
                /* Stop voice recording */
                StopAudioRecording();

                /* Export voice recording data */
                dataModule.SaveFileList();
            }
        }

        public void SetExperimentNumber(int n)
        {
            experimentNumber = n;
        }

        public void SetMarkerPrefabs(GameObject[] mps)
        {
            markerPrefabs = mps;
        }

        #region EXPERIMENT FLOWS
        private void EyeGazeAndQNAThenVoice()
        {
            /* CHECK IF RECORDING STARTED */
            if (!isRecording || IndividualModelController.currentModel == null) return;

            /* GET GAZED OBJECT */
            var eyeTarget = EyeTrackingTarget.LookedAtEyeTarget;
            var gazedObject = eyeTarget != null ? eyeTarget.gameObject : null;

            /* RECORD GAZE DATA */
            if (timer > recordVoiceDuration)
            {
                timer -= Time.deltaTime;

                // Pass the gazed voxel ID to RecordGazeData
                Vector3[] gaze = dataModule.RecordGazeData(gazedObject);
                if (gaze[0] != Vector3.zero)
                {
                    lastLocalHitPosition = gaze[0];
                    globalHitPosition = gaze[1];
                    hitNormal = gaze[2];
                }

                promptObject.GetComponent<TextMeshPro>().SetText($"VIEWING TIME: {(timer - recordVoiceDuration):F1}");

                CheckKeyboardInput();
            }
            /* RECORD VOICE DATA */
            else if (timer > 0)
            {
                timer -= Time.deltaTime;

                promptObject.GetComponent<TextMeshPro>().SetText(question + $"TIME: {timer:F1}");
                Vector3 cameraPosition = CameraCache.Main.transform.position;
                Vector3 currentPosition = promptObject.transform.position;
                Quaternion currentRotation = promptObject.transform.rotation;
                Transform mainCameraTransform = CameraCache.Main.transform;
                Vector3 directionToCamera = -(cameraPosition - currentPosition + rotationDisplacement).normalized;
                targetRotation = Quaternion.LookRotation(directionToCamera);

                float rotationStep = rotationSpeed * Time.deltaTime;
                promptObject.transform.rotation = Quaternion.Slerp(currentRotation, targetRotation, rotationStep);

                // Snap rotation if close enough
                if (Quaternion.Angle(currentRotation, targetRotation) < rotationThresholdDegrees)
                {
                    promptObject.transform.rotation = targetRotation;
                }
                targetPosition = cameraPosition + mainCameraTransform.forward * followDistance + mainCameraTransform.right * horizontalDisplacement + mainCameraTransform.up * verticalDisplacement;

                // Smoothly move towards the target position
                float moveStep = moveSpeed * Time.deltaTime;
                promptObject.transform.position = Vector3.Lerp(currentPosition, targetPosition, moveStep);

                if (!savedGaze)
                {
                    GetComponent<DrawOn3DTexture>().ToggleLiveHeatmap(false);
                    IndividualModelController.StopSineWave();
                    QNAPrompt.SetActive(false);

                    /* Start voice recording */
                    StartAudioRecording();

                    savedGaze = true;
                }
            }
            else
            {
                promptObject.GetComponent<TextMeshPro>().SetText("");

                dataModule.ExportPointCloud();
                dataModule.ExportQuestionnaireAnswers();
                dataModule.Export3DModel(gameObject);
                StopAudioRecording();
                dataModule.SaveFileList();
                SetIsRecording(false);

                IndividualModelController.ToggleRecorded();
            }
        }
        #endregion

        #region QNA FUNCTIONS
        private bool isPlayingAudio = false;
        private float audioTimer = 1.0f;
        private void CheckKeyboardInput()
        {
            string answerKey = "";
            if (Input.GetKey(KeyCode.Alpha4) || Input.GetKey(KeyCode.Keypad4))
            {
                answerKey = "4";
            }
            else if (Input.GetKey(KeyCode.Alpha6) || Input.GetKey(KeyCode.Keypad6))
            {
                answerKey = "6";
            }
            else if (Input.GetKey(KeyCode.Alpha8) || Input.GetKey(KeyCode.Keypad8))
            {
                answerKey = "8";
            }
            else if (Input.GetKey(KeyCode.Alpha2) || Input.GetKey(KeyCode.Keypad2))
            {
                answerKey = "2";
            }
            else if (Input.GetKey(KeyCode.Alpha5) || Input.GetKey(KeyCode.Keypad5))
            {
                answerKey = "5";
            }
            else
            {
                if (!isPlayingAudio)
                {
                    IndividualModelController.StopSineWave();
                }
                else
                {
                    audioTimer -= Time.deltaTime;

                    if (audioTimer < 0)
                    {
                        isPlayingAudio = false;
                        audioTimer = 1.0f;
                    }
                }
            }

            if (answerKey != "")
            {
                dataModule.OnQuestionnaireAnswered(answerChoices[answerKey], lastLocalHitPosition);
                if (gameObject.GetComponent<DrawOn3DTexture>().enabled == true)
                {
                    gameObject.GetComponent<DrawOn3DTexture>().SpawnMarkerAtPosition(markerPrefabs[markerMapping[answerKey]], globalHitPosition, hitNormal);
                }
            }
        }
        #endregion

        public void ResetAll()
        {
            if (isRecording)
            {
                SetIsRecording(false);
            }
            promptObject.GetComponent<TextMeshPro>().SetText(enterText);

            if (heatmapSource != null)
            {
                heatmapSource.ClearDrawing();
            }
        }

        #region AUDIO DATA
        private void StartAudioRecording()
        {
            /* RECORD AUDIO FOR 60 SECONDS */
            recordedAudio = Microphone.Start(null, false, 60, audioSampleRate); // loop = false
            isRecordingAudio = true;
            chunkStartTime = Time.time;
            Debug.Log("Started audio recording (60s chunk).");
        }

        private void StopAudioRecording()
        {
            if (!isRecordingAudio) return;
            Microphone.End(null);

            /* SAVE AUDIO CLIP */
            dataModule.SaveAudioData(recordedAudio, chunkIndex);
            isRecordingAudio = false;
            Debug.Log("Stopped audio recording and saved final chunk.");
        }
        #endregion
    }
}
