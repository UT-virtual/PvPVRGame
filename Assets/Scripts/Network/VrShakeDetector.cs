using UnityEngine;
using UnityEngine.XR;

internal sealed class VrShakeDetector
{
    private const double WindowSeconds = 0.5;
    private const float DirectionSpeed = 0.8f;
    private const float RestSpeed = 0.25f;
    private const double RestSeconds = 0.08;
    private const double MaxSampleGap = 0.1;
    private const float MaxPositionStep = 0.5f;
    private const int RequiredReversals = 3;

    private readonly XRNode hand;
    private InputDevice previousDevice;
    private double previousSampleTime = double.NegativeInfinity;
    private bool hasPreviousPosition;
    private Vector3 previousPosition;

    private bool measuring;
    private double deadline;
    private Vector3 axis;
    private int lastDirection;
    private int reversals;
    private bool waitingForRest;
    private double restStartedAt = -1.0;

    public VrShakeDetector(XRNode hand)
    {
        this.hand = hand;
    }

    public bool Sample(double now)
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(hand);
        if (!device.isValid ||
            !device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) || !tracked)
        {
            Reset();
            return false;
        }

        double elapsed = now - previousSampleTime;
        if (!device.Equals(previousDevice) || elapsed <= 0.0 || elapsed > MaxSampleGap)
        {
            Reset();
        }

        bool hasTrackingState = device.TryGetFeatureValue(CommonUsages.trackingState, out InputTrackingState state);
        bool hasPosition = device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position) &&
            (!hasTrackingState || (state & InputTrackingState.Position) != 0);
        bool hasVelocity = device.TryGetFeatureValue(CommonUsages.deviceVelocity, out Vector3 velocity) &&
            (!hasTrackingState || (state & InputTrackingState.Velocity) != 0);

        Vector3 displacement = position - previousPosition;
        bool canEstimateVelocity = hasPosition && hasPreviousPosition;
        previousDevice = device;
        previousSampleTime = now;
        hasPreviousPosition = hasPosition;
        previousPosition = position;

        if (canEstimateVelocity && displacement.sqrMagnitude > MaxPositionStep * MaxPositionStep)
        {
            // A discontinuity in tracking space is not a hand gesture.
            ResetGesture();
            return false;
        }

        if (!hasVelocity)
        {
            if (!canEstimateVelocity)
            {
                ResetGesture();
                return false;
            }

            // Device positions are in tracking space, so avatar locomotion does not affect this estimate.
            velocity = displacement / (float)elapsed;
        }

        if (float.IsNaN(velocity.sqrMagnitude) || float.IsInfinity(velocity.sqrMagnitude))
        {
            Reset();
            return false;
        }

        return UpdateGesture(velocity, now);
    }

    internal bool UpdateGesture(Vector3 velocity, double now)
    {
        float speed = velocity.magnitude;
        if (waitingForRest)
        {
            if (speed > RestSpeed)
            {
                restStartedAt = -1.0;
                return false;
            }

            if (restStartedAt < 0.0)
                restStartedAt = now;
            if (now - restStartedAt >= RestSeconds)
                waitingForRest = false;

            return false;
        }

        if (measuring && now > deadline)
            measuring = false;

        if (!measuring)
        {
            if (speed < DirectionSpeed)
                return false;

            measuring = true;
            deadline = now + WindowSeconds;
            axis = velocity / speed;
            lastDirection = 1;
            reversals = 0;
            return false;
        }

        float speedAlongAxis = Vector3.Dot(velocity, axis);
        // Preserve the previous direction through the slow part of a turnaround.
        if (Mathf.Abs(speedAlongAxis) < DirectionSpeed)
            return false;

        int direction = speedAlongAxis > 0f ? 1 : -1;
        if (direction == lastDirection)
            return false;

        lastDirection = direction;
        reversals++;
        if (reversals < RequiredReversals)
            return false;

        measuring = false;
        waitingForRest = true;
        restStartedAt = -1.0;
        return true;
    }

    public void Reset()
    {
        previousDevice = default;
        previousSampleTime = double.NegativeInfinity;
        hasPreviousPosition = false;
        ResetGesture();
    }

    private void ResetGesture()
    {
        measuring = false;
        waitingForRest = false;
        restStartedAt = -1.0;
    }
}
