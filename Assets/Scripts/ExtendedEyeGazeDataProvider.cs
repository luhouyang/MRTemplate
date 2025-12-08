// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using UnityEngine;
using System;
using System.Collections.Generic;
using Microsoft.MixedReality.OpenXR;
using Microsoft.MixedReality.EyeTracking;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;

[DisallowMultipleComponent]
public class ExtendedEyeGazeDataProvider : MonoBehaviour
{
    public static EyeTrackingTarget LookedAtEyeTarget { get; private set; }

    public enum GazeType
    {
        Left,
        Right,
        Combined
    }

    public struct GazeReading
    {
        public bool IsValid;
        public Vector3 EyePosition;
        public Vector3 GazeDirection;
        public bool IsLookingAtAttachedObject;
        public Vector3 HitPosition;
        public DateTime Timestamp;
        public Vector3 HeadPosition;
        public Vector3 HeadForward;

        public GazeReading(bool isValid, Vector3 position, Vector3 direction, DateTime timestamp, bool isLookingAtObj = false, Vector3 hitPos = default(Vector3))
        {
            IsValid = isValid;
            EyePosition = position;
            GazeDirection = direction;
            Timestamp = timestamp;
            IsLookingAtAttachedObject = isLookingAtObj;
            HitPosition = hitPos;
            HeadPosition = Vector3.zero;
            HeadForward = Vector3.forward;
        }
    }

    private Camera _mainCamera;
    private EyeGazeTrackerWatcher _watcher;
    private EyeGazeTracker _eyeGazeTracker;
    private EyeGazeTrackerReading _eyeGazeTrackerReading;
    private System.Numerics.Vector3 _trackerSpaceGazeOrigin;
    private System.Numerics.Vector3 _trackerSpaceGazeDirection;

    private GazeReading _gazeReading;
    private GazeReading _invalidGazeReading = new GazeReading(false, Vector3.zero, Vector3.zero, DateTime.MinValue, false, Vector3.zero);

    private bool _gazePermissionEnabled;
    private bool _readingSucceeded;
    private SpatialGraphNode _eyeGazeTrackerNode;
    private Pose _eyeGazeTrackerPose;
    private Matrix4x4 _eyeGazeTrackerSpaceToPlayspace = new Matrix4x4();
    private Matrix4x4 _eyeGazeTrackerSpaceToWorld = new Matrix4x4();
    private Transform _mixedRealityPlayspace;

    private Collider _attachedCollider;

    [SerializeField]
    [Tooltip("Layers to include when checking for EyeTrackingTargets")]
    private LayerMask gazeTargetLayers = Physics.DefaultRaycastLayers;

    private const float MaxGazeDistance = 50.0f;

    private async void Start()
    {
        _mainCamera = Camera.main;
        if (_mainCamera != null && _mainCamera.transform.parent != null)
        {
            _mixedRealityPlayspace = _mainCamera.transform.parent;
        }

        _attachedCollider = GetComponent<Collider>();

        //Debug.Log("Initializing ExtendedEyeTracker");
#if ENABLE_WINMD_SUPPORT
        _gazePermissionEnabled = await AskForEyePosePermission();
#else
        _gazePermissionEnabled = true;
#endif

        if (!_gazePermissionEnabled)
        {
            //Debug.LogError("Gaze is disabled");
            return;
        }

        _watcher = new EyeGazeTrackerWatcher();
        _watcher.EyeGazeTrackerAdded += _watcher_EyeGazeTrackerAdded;
        _watcher.EyeGazeTrackerRemoved += _watcher_EyeGazeTrackerRemoved;
        await _watcher.StartAsync();
    }

    private void Update()
    {
        GazeReading currentReading = GetWorldSpaceGazeReading(GazeType.Combined, DateTime.UtcNow);

        if (currentReading.IsValid)
        {
            RaycastHit hit;
            EyeTrackingTarget newTarget = null;

            if (currentReading.IsLookingAtAttachedObject)
            {
                if (Physics.Raycast(currentReading.EyePosition, currentReading.GazeDirection, out hit, MaxGazeDistance, gazeTargetLayers))
                {
                    newTarget = hit.collider.GetComponent<EyeTrackingTarget>();
                }
            }

            if (newTarget != LookedAtEyeTarget)
            {
                if (LookedAtEyeTarget != null) LookedAtEyeTarget.OnLookAway?.Invoke();
                if (newTarget != null) newTarget.OnLookAtStart?.Invoke();
                LookedAtEyeTarget = newTarget;
            }
        }
        else
        {
            if (LookedAtEyeTarget != null)
            {
                LookedAtEyeTarget.OnLookAway?.Invoke();
                LookedAtEyeTarget = null;
            }
        }
    }

    public int GetWorldSpaceGazeReadingsSince(DateTime sinceTimestamp, GazeType gazeType, List<GazeReading> bufferToFill, int maxSamplesLimit = 200)
    {
        bufferToFill.Clear();

        if (!_gazePermissionEnabled || _eyeGazeTracker == null) return 0;

        int count = 0;
        DateTime itrTimestamp = sinceTimestamp;

        while (true)
        {
            var reading = _eyeGazeTracker.TryGetReadingAfterTimestamp(itrTimestamp);

            if (reading == null) break;

            if (reading.Timestamp <= itrTimestamp)
            {
                itrTimestamp = itrTimestamp.AddTicks(1);
                continue;
            }

            GazeReading processedReading = ProcessSingleReading(reading, gazeType);

            if (processedReading.IsValid)
            {
                bufferToFill.Add(processedReading);
                itrTimestamp = reading.Timestamp;
                itrTimestamp = itrTimestamp.AddTicks(100000);
                count++;
            }
            else
            {
                itrTimestamp = reading.Timestamp;
            }

            // DYNAMIC LIMIT CHECK
            if (count >= maxSamplesLimit) break;
        }

        return count;
    }

    private GazeReading ProcessSingleReading(EyeGazeTrackerReading rawReading, GazeType gazeType)
    {
        _readingSucceeded = false;
        switch (gazeType)
        {
            case GazeType.Left:
                _readingSucceeded = rawReading.TryGetLeftEyeGazeInTrackerSpace(out _trackerSpaceGazeOrigin, out _trackerSpaceGazeDirection);
                break;
            case GazeType.Right:
                _readingSucceeded = rawReading.TryGetRightEyeGazeInTrackerSpace(out _trackerSpaceGazeOrigin, out _trackerSpaceGazeDirection);
                break;
            case GazeType.Combined:
                _readingSucceeded = rawReading.TryGetCombinedEyeGazeInTrackerSpace(out _trackerSpaceGazeOrigin, out _trackerSpaceGazeDirection);
                break;
        }

        if (_readingSucceeded && _eyeGazeTrackerNode.TryLocate(rawReading.SystemRelativeTime.Ticks, out _eyeGazeTrackerPose))
        {
            _eyeGazeTrackerSpaceToPlayspace.SetTRS(_eyeGazeTrackerPose.position, _eyeGazeTrackerPose.rotation, Vector3.one);
            _eyeGazeTrackerSpaceToWorld = (_mixedRealityPlayspace != null) ?
                    _mixedRealityPlayspace.localToWorldMatrix * _eyeGazeTrackerSpaceToPlayspace :
                    _eyeGazeTrackerSpaceToPlayspace;

            GazeReading result = new GazeReading();
            result.Timestamp = rawReading.Timestamp;
            result.EyePosition = _eyeGazeTrackerSpaceToWorld.MultiplyPoint3x4(ToUnity(_trackerSpaceGazeOrigin));
            result.GazeDirection = _eyeGazeTrackerSpaceToWorld.MultiplyVector(ToUnity(_trackerSpaceGazeDirection));

            result.HeadPosition = _eyeGazeTrackerSpaceToWorld.GetColumn(3);
            result.HeadForward = _eyeGazeTrackerSpaceToWorld.GetColumn(2);

            result.IsLookingAtAttachedObject = CheckIfGazeHitsTargetLayer(result.EyePosition, result.GazeDirection, out Vector3 hitPos);
            result.HitPosition = hitPos;

            result.IsValid = true;
            return result;
        }

        return _invalidGazeReading;
    }

    public GazeReading GetWorldSpaceGazeReading(GazeType gazeType, DateTime timestamp)
    {
        if (_gazePermissionEnabled && _eyeGazeTracker != null)
        {
            var reading = _eyeGazeTracker.TryGetReadingAtTimestamp(timestamp);
            if (reading != null)
            {
                return ProcessSingleReading(reading, gazeType);
            }
        }

        if (CoreServices.InputSystem?.EyeGazeProvider != null && gazeType == GazeType.Combined)
        {
            _gazeReading.Timestamp = DateTime.Now;
            _gazeReading.EyePosition = CoreServices.InputSystem.EyeGazeProvider.GazeOrigin;
            _gazeReading.GazeDirection = CoreServices.InputSystem.EyeGazeProvider.GazeDirection;

            if (CameraCache.Main != null)
            {
                _gazeReading.HeadPosition = CameraCache.Main.transform.position;
                _gazeReading.HeadForward = CameraCache.Main.transform.forward;
            }

            _gazeReading.IsValid = CoreServices.InputSystem.EyeGazeProvider.IsEyeTrackingDataValid || Application.isEditor;

            if (_gazeReading.IsValid)
            {
                _gazeReading.IsLookingAtAttachedObject = CheckIfGazeHitsTargetLayer(_gazeReading.EyePosition, _gazeReading.GazeDirection, out Vector3 hitPos);
                _gazeReading.HitPosition = hitPos;
                return _gazeReading;
            }
        }

        return _invalidGazeReading;
    }

    public GazeReading GetCameraSpaceGazeReading(GazeType gazeType)
    {
        return GetCameraSpaceGazeReading(gazeType, DateTime.UtcNow);
    }

    public GazeReading GetCameraSpaceGazeReading(GazeType gazeType, DateTime timestamp)
    {
        var reading = GetWorldSpaceGazeReading(gazeType, timestamp);
        if (!reading.IsValid) return reading;

        reading.EyePosition = _mainCamera.transform.InverseTransformPoint(reading.EyePosition);
        reading.GazeDirection = _mainCamera.transform.InverseTransformDirection(reading.GazeDirection).normalized;
        if (reading.IsLookingAtAttachedObject)
        {
            reading.HitPosition = _mainCamera.transform.InverseTransformPoint(reading.HitPosition);
        }
        return reading;
    }

    private bool CheckIfGazeHitsTargetLayer(Vector3 origin, Vector3 direction, out Vector3 hitPoint)
    {
        hitPoint = Vector3.zero;
        RaycastHit hit;
        if (Physics.Raycast(origin, direction, out hit, MaxGazeDistance, gazeTargetLayers))
        {
            hitPoint = hit.point;
            return true;
        }
        return false;
    }

    private void _watcher_EyeGazeTrackerRemoved(object sender, EyeGazeTracker e)
    {
        _eyeGazeTracker = null;
        //Debug.Log("EyeGazeTracker removed");
    }

    private async void _watcher_EyeGazeTrackerAdded(object sender, EyeGazeTracker e)
    {
        //Debug.Log("EyeGazeTracker added");
        try
        {
            await e.OpenAsync(true);
            _eyeGazeTracker = e;
            var supportedFrameRates = _eyeGazeTracker.SupportedTargetFrameRates;

            if (supportedFrameRates.Count > 0)
            {
                EyeGazeTrackerFrameRate highestRate = supportedFrameRates[0];
                foreach (var rate in supportedFrameRates)
                {
                    if (rate.FramesPerSecond > highestRate.FramesPerSecond)
                    {
                        highestRate = rate;
                    }
                }

                _eyeGazeTracker.SetTargetFrameRate(highestRate);
                //Debug.Log($"ExtendedEyeGazeDataProvider: Set Target Frame Rate to {highestRate.FramesPerSecond} FPS");
            }

            _eyeGazeTrackerNode = SpatialGraphNode.FromDynamicNodeId(e.TrackerSpaceLocatorNodeId);
        }
        catch (Exception ex)
        {
            Debug.LogError("Unable to open EyeGazeTracker\r\n" + ex.ToString());
        }
    }

#if ENABLE_WINMD_SUPPORT
    private async System.Threading.Tasks.Task<bool> AskForEyePosePermission()
    {
        var accessStatus = await Windows.Perception.People.EyesPose.RequestAccessAsync();
        return accessStatus == Windows.UI.Input.GazeInputAccessStatus.Allowed;
    }
#endif

    private static UnityEngine.Vector3 ToUnity(System.Numerics.Vector3 v) => new UnityEngine.Vector3(v.X, v.Y, -v.Z);
}