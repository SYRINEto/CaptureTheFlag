using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class CaptureTheFlagAgent : Agent
{
    [Header("Target References")]
    [SerializeField] private Transform flagTransform;
    [SerializeField] private Transform safeZoneTransform;

    [Header("Spawn Areas (Box Colliders with 'Is Trigger' Enabled)")]
    [SerializeField] private BoxCollider flagSpawnArea;
    [SerializeField] private BoxCollider safeZoneSpawnArea;

    [Header("Defenders")]
    [SerializeField] private ZoneDefender[] defenders;

    [Header("Movement Parameters")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float turnSpeed = 180f;

    private Rigidbody rb;
    private bool hasFlag;
    private float previousDistance;
    private Vector3 initialLocalPosition;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        initialLocalPosition = transform.localPosition;
    }

    public override void OnEpisodeBegin()
    {
        // 1. Reset agent state and physics
        hasFlag = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // 2. Reset agent position relative to local arena root
        transform.localPosition = initialLocalPosition;
        transform.localRotation = Quaternion.identity;

        // 3. Randomize flag position within the flag spawn zone
        if (flagSpawnArea != null)
        {
            flagTransform.position = GetRandomPointInBounds(flagSpawnArea.bounds);
        }
        flagTransform.gameObject.SetActive(true);

        // 4. Randomize safe zone position with a minimum distance check from the flag
        if (safeZoneSpawnArea != null)
        {
            safeZoneTransform.position = GetRandomPointInBounds(safeZoneSpawnArea.bounds);

            int maxAttempts = 10;
            int attempts = 0;
            while (Vector3.Distance(safeZoneTransform.position, flagTransform.position) < 3.0f && attempts < maxAttempts)
            {
                safeZoneTransform.position = GetRandomPointInBounds(safeZoneSpawnArea.bounds);
                attempts++;
            }
        }

        // 5. Reset all defenders to new random locations in their assigned zones
        if (defenders != null)
        {
            foreach (var defender in defenders)
            {
                if (defender != null)
                {
                    defender.ResetDefender();
                }
            }
        }

        // 6. Set initial distance baseline using world positions
        previousDistance = Vector3.Distance(transform.position, flagTransform.position);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Observation 1: Flag state (1 float: 1.0 = holding flag, 0.0 = searching)
        sensor.AddObservation(hasFlag ? 1.0f : 0.0f);

        // Observation 2: Relative direction vector to active target in agent's local space (3 floats)
        Transform currentTarget = hasFlag ? safeZoneTransform : flagTransform;
        Vector3 relativeDir = transform.InverseTransformPoint(currentTarget.position).normalized;
        sensor.AddObservation(relativeDir);

        // Total Vector Observation Space Size = 4
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Continuous Action 0: Move Forward/Backward
        // Continuous Action 1: Rotate Left/Right
        float moveInput = actions.ContinuousActions[0];
        float turnInput = actions.ContinuousActions[1];

        // Apply Movement and Rotation
        transform.Translate(Vector3.forward * moveInput * moveSpeed * Time.deltaTime);
        transform.Rotate(Vector3.up * turnInput * turnSpeed * Time.deltaTime);

        // Calculate world distance to active target
        Transform currentTarget = hasFlag ? safeZoneTransform : flagTransform;
        float currentDistance = Vector3.Distance(transform.position, currentTarget.position);

        // Reward shaping (+0.1 per unit closer, -0.1 per unit further)
        float distanceDelta = previousDistance - currentDistance;
        AddReward(distanceDelta * 0.1f);
        previousDistance = currentDistance;

        // Small step penalty to encourage speed
        AddReward(-0.0005f);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Phase 1: Touch Flag (Trigger Collider)
        if (other.CompareTag("Flag") && !hasFlag)
        {
            hasFlag = true;
            AddReward(0.5f);
            flagTransform.gameObject.SetActive(false);

            // Switch distance baseline to Safe Zone
            previousDistance = Vector3.Distance(transform.position, safeZoneTransform.position);
        }

        // Phase 2: Deliver Flag to Safe Zone (Trigger Collider)
        if (other.CompareTag("SafeZone") && hasFlag)
        {
            AddReward(1.5f);
            EndEpisode();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Solid physical collision with outer walls or obstacles
        if (collision.gameObject.CompareTag("Wall") || collision.gameObject.CompareTag("Obstacle"))
        {
            AddReward(-1.0f);
            EndEpisode();
        }

        // Tagged by Defender
        if (collision.gameObject.CompareTag("Defender"))
        {
            AddReward(-1.5f);
            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Manual WASD/Arrow control in Unity Editor for testing
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxisRaw("Vertical");   // W/S or Up/Down
        continuousActions[1] = Input.GetAxisRaw("Horizontal"); // A/D or Left/Right
    }

    private Vector3 GetRandomPointInBounds(Bounds bounds)
    {
        return new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            bounds.min.y,
            Random.Range(bounds.min.z, bounds.max.z)
        );
    }
}