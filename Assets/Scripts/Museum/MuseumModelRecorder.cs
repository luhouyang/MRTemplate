using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.MRTemplate;
using Microsoft.MixedReality.Toolkit.Utilities;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;

// Contains DataModule
namespace MuseumModel
{
    public class MuseumModelRecorder : MonoBehaviour
    {
        [Header("View Blocker")]
        [SerializeField] public GameObject viewBlocker;

        // Heatmap
        private DrawOn3DTexture heatmapSource;

        private string sessionPath;
        public DataModule dataModule;

        // Start is called before the first frame update
        void Start()
        {
            heatmapSource = GetComponent<DrawOn3DTexture>();
        }

        // Update is called once per frame
        void Update()
        {

        }

        public void InitializeState(string sessionPath)
        {
            this.sessionPath = sessionPath;
            string saveDir = Path.Combine(Application.persistentDataPath, sessionPath, gameObject.name);
            if (!Directory.Exists(saveDir))
            {
                Directory.CreateDirectory(saveDir);
            }
            dataModule = new DataModule(saveDir, Time.unscaledTimeAsDouble, gameObject, gameObject.GetComponent<MeshFilter>());
        }

        public void SaveData()
        {
            dataModule.ExportPointCloud();
            ResetAll();
        }

        public void ResetAll()
        {
            if (heatmapSource != null)
            {
                heatmapSource.ClearDrawing();
            }
        }
    }

}