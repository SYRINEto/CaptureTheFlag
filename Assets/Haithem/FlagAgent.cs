using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class FlagAgent : Agent
{
    [Header("World Objects")]
    [SerializeField] private Transform flag;
    [SerializeField] private Transform goalZone;
    [SerializeField] private Collider goalZoneBounds;
    [SerializeField] private RoamingGuard[] guards;
    [SerializeField] private BarrierObject[] barriers;

    [Header("Visual Feedback")]
    [SerializeField] private Renderer floorRenderer;
    [SerializeField] private Material defaultFloorColor;
    [SerializeField] private Material successFloorColor;

    [Header("Agent Tuning")]
    [SerializeField] private float agentSpeed = 5f;
    [SerializeField] private float floorSize = 9f;
    [SerializeField] private float edgeSafeDistance = 1.5f;

    [Header("Spawn Rules")]
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private float spawnRadiusCheck = 0.5f;

    [Header("Flag Follow")]
    [SerializeField] private Vector3 flagAttachOffset = new Vector3(0f, 1.5f, 0f);

    [Header("Safety")]
    [SerializeField] private float fallThreshold = -2f;

    [Header("Diagnostics")]
    [SerializeField] private bool traceRewards = true;

    private bool holdingFlag;
    private Rigidbody agentRb;
    private float lastGap;
    private bool resolving;

    private void Awake()
    {
        agentRb = GetComponent<Rigidbody>();
    }

    public override void OnEpisodeBegin()
    {
        StopAllCoroutines();
        resolving = false;

        // The home zone is oversized, so it probes with a wider footprint.
        goalZone.localPosition = SpawnInRandomPoint(0.1f, 0f, 2.5f);

        foreach (var barrier in barriers)
            if (barrier != null) barrier.ResetBarrier(goalZoneBounds, 1f);

        transform.localPosition = SpawnInRandomPoint(0.5f, 1f);
        agentRb.linearVelocity = Vector3.zero;
        agentRb.angularVelocity = Vector3.zero;
        holdingFlag = false;

        flag.localPosition = SpawnInRandomPoint(0.5f, 1f);

        foreach (var guard in guards)
            if (guard != null) guard.ResetPosition();

        lastGap = Vector3.Distance(transform.localPosition, flag.localPosition);

        if (floorRenderer != null && defaultFloorColor != null)
            floorRenderer.material = defaultFloorColor;
    }

    private Vector3 SpawnInRandomPoint(float height, float safeZoneMargin = 1f, float footprintRadius = -1f)//float height, float zonePadding = 1f, float footprint = -1f
    {
        float probe = footprintRadius > 0f ? footprintRadius : spawnRadiusCheck;
        Vector3 candidate = Vector3.zero;
        int rolls = 0;
        do
        {
            candidate = new Vector3(
                Random.Range(-floorSize + edgeSafeDistance, floorSize - edgeSafeDistance),
                height,
                Random.Range(-floorSize + edgeSafeDistance, floorSize - edgeSafeDistance)
            );
            rolls++;
        }
        while (rolls < 30 &&
               (Physics.CheckSphere(candidate, probe, wallLayer) || CheckIsOverlaping(candidate, safeZoneMargin)));
        return candidate;
    }

    private bool CheckIsOverlaping(Vector3 position, float padding)
    {
        if (goalZoneBounds == null) return false;
        Bounds zone = goalZoneBounds.bounds;
        zone.Expand(padding);
        return zone.Contains(position);
    }

    private void RewardAgent(float amount, string note = "")
    {
        AddReward(amount);
        Academy.Instance.StatsRecorder.Add($"Ends/{note}", amount, StatAggregationMethod.Sum);
        if (traceRewards)
            Debug.Log($"[HEIST] {amount:+0.000;-0.000} {note} -- total {GetCumulativeReward():0.000}");
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(flag.localPosition - transform.localPosition);
        sensor.AddObservation(goalZone.localPosition - transform.localPosition);
        sensor.AddObservation(agentRb.linearVelocity);

        foreach (var guard in guards)
        {
            if (guard != null)
            {
                sensor.AddObservation(guard.transform.localPosition - transform.localPosition);
                sensor.AddObservation(guard.Velocity);
            }
            else
            {
                sensor.AddObservation(Vector3.zero);
                sensor.AddObservation(Vector3.zero);
            }
        }

        sensor.AddObservation(holdingFlag ? 1f : 0f);
        sensor.AddObservation(transform.localPosition.x / floorSize);
        sensor.AddObservation(transform.localPosition.z / floorSize);

        foreach (var barrier in barriers)
        {
            if (barrier != null)
                sensor.AddObservation(barrier.transform.localPosition - transform.localPosition);
            else
                sensor.AddObservation(Vector3.zero);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuous = actionsOut.ContinuousActions;
        continuous[0] = Input.GetAxisRaw("Horizontal");
        continuous[1] = Input.GetAxisRaw("Vertical");
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (agentRb == null) return;

        float steerX = actions.ContinuousActions[0];
        float steerZ = actions.ContinuousActions[1];
        agentRb.linearVelocity = new Vector3(steerX, 0f, steerZ) * agentSpeed;

        Vector3 destination = holdingFlag ? goalZone.localPosition : flag.localPosition;
        float currentGap = Vector3.Distance(transform.localPosition, destination);
        float gain = lastGap - currentGap;
        float shaping = Mathf.Clamp(gain * 0.3f, -0.05f, 0.05f);
        RewardAgent(shaping, holdingFlag ? "towards home" : "towards flag");
        lastGap = currentGap;

        RewardAgent(-0.0005f, "time tick");

        foreach (var guard in guards)
        {
            if (guard == null) continue;
            float gap = Vector3.Distance(transform.localPosition, guard.transform.localPosition);
            if (gap < 1.5f)
                RewardAgent(-0.03f, $"heat near {guard.name}");
            else if (gap < 3.0f)
                RewardAgent(-0.008f, $"heat around {guard.name}");
        }
    }

    private void Update()
    {
        if (holdingFlag)
        {
            flag.position = transform.position + flagAttachOffset;
            flag.rotation = transform.rotation;
        }
    }

    private void FixedUpdate()
    {
        if (transform.localPosition.y < fallThreshold)
        {
            RewardAgent(-1f, "out of bounds (fall)");
            //Academy.Instance.StatsRecorder.Add("Ends/fall", 1f, StatAggregationMethod.Sum);
            EndEpisode();
        }
    }


    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Flag") && !holdingFlag)
        {
            holdingFlag = true;
            lastGap = Vector3.Distance(transform.localPosition, goalZone.localPosition);
            RewardAgent(2f, "flag secured");
        }

        if (other.CompareTag("SafeZone") && holdingFlag && !resolving)
        {
            RewardAgent(3f, "mission complete");

            if (floorRenderer != null && successFloorColor != null)
                floorRenderer.material = successFloorColor;

            resolving = true;
            //Academy.Instance.StatsRecorder.Add("Ends/SafeZone", 1f, StatAggregationMethod.Sum);
            StartCoroutine(Dismiss(1f));
        }


        if (other.CompareTag("Defender") && !resolving)
        {
            RewardAgent(-2f, $"caught by {other.name}");
            resolving = true;

            StartCoroutine(Dismiss(0.3f));
        }
    }

    private System.Collections.IEnumerator Dismiss(float delay)
    {
        yield return new WaitForSeconds(delay);
        resolving = false;
        EndEpisode();
    }

    private void OnCollisionEnter(Collision hit)
    {
        if (hit.gameObject.CompareTag("Wall"))
        {
            RewardAgent(-1f, "wall crash");
            //Academy.Instance.StatsRecorder.Add("Ends/Wall", 1f, StatAggregationMethod.Sum);
            EndEpisode();
        }
        if (hit.gameObject.CompareTag("Obstacle"))
        {
            RewardAgent(-1f, "obstacle crash");
            //Academy.Instance.StatsRecorder.Add("Ends/Obstacle", 1f, StatAggregationMethod.Sum);
            EndEpisode();
        }
    }
}