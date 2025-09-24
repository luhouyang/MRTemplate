using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.MRTemplate;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RealWorldModel
{
    public class RealWorldModelRecorder : MonoBehaviour
    {
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            gameObject.GetComponent<DrawOn3DTexture>().DrawAtThisHitPos(gameObject.transform.InverseTransformVector(CoreServices.InputSystem.GazeProvider.HitPosition));
        }
    }
}
