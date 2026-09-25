using System.Collections;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

/// <summary>
/// Machine Learning Agent for Capture The Flag (Group 2 - Maaouia).
/// All navigation, positioning, and sensor observations strictly operate
/// in transform.localPosition relative to the arena root.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class AgentMaaouia : Agent
{
    [Header("Scene References")]
    [SerializeField] private Transform flag;
    [SerializeField] private Transform safeZone;
    [SerializeField] private Collider safeZoneCollider;
    [SerializeField] private DefenderControllerMaaouia[] defenders;
    [SerializeField] private ObstacleControllerMaaouia[] obstacles;

    [Header("Visual Feedback Materials")]
    [SerializeField] private Renderer floorRenderer;
    [SerializeField] private Material defaultFloorMaterial;
    [SerializeField] private Material winFloorMaterial;
    [SerializeField] private Material loseFloorMaterial;

    [Header("Physical Movement Parameters")]
    [SerializeField] private float moveSpeed = 5.0f;
    [SerializeField] private float safeMargin = 1.2f;

    [Header("Arena Bounds (Local Space)")]
    [SerializeField] private float minX = -5.0f;
    [SerializeField] private float maxX = 5.0f;
    [SerializeField] private float minZ = -5.0f;
    [SerializeField] private float maxZ = 5.0f;

    [Header("Interaction Radii")]
    [SerializeField] private float flagPickupRadius = 1.0f;
    [SerializeField] private float safeZoneDeliveryRadius = 1.5f;

    [Header("Flag Carriage Offset")]
    [SerializeField] private Vector3 flagCarryOffset = new Vector3(0f, 1.2f, 0f);

    [Header("Safety Reset Threshold")]
    [SerializeField] private float fallResetY = -2.0f;

    [Header("Reward Tuning")]
    [SerializeField] private float flagPickupReward = 2.0f;
    [SerializeField] private float safeZoneReward = 8.0f;
    [SerializeField] private float defenderCollisionPenalty = -2.0f;
    [SerializeField] private float wallCollisionPenalty = -1.0f;
    [SerializeField] private float obstacleCollisionPenalty = -1.0f;
    [SerializeField] private float timePenalty = -0.0005f;

    [Header("Debug")]
    [SerializeField] private bool logRewards = false;

    private Rigidbody rb;
    private bool hasFlag;
    private float lastDistance;
    private bool episodeEnding;
    private float flagOriginalY = 0.5f;
    private float agentSpawnY = 0.5f;

    public override void Initialize()
    {
        EnsureRigidbody();
        if (flag != null) flagOriginalY = flag.localPosition.y;
        agentSpawnY = transform.localPosition.y;
    }

    private void EnsureRigidbody()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
        }

        if (rb != null)
        {
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    public override void OnEpisodeBegin()
    {
        StopAllCoroutines();
        episodeEnding = false;
        hasFlag = false;

        EnsureRigidbody();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Reset visual floor feedback
        SetFloorMaterial(defaultFloorMaterial);

        // 1. Randomize Safe Zone local position
        if (safeZone != null)
        {
            safeZone.localPosition = GetValidRandomLocalPosition(safeZone.localPosition.y);
        }

        // 2. Randomize Agent local position
        transform.localPosition = GetValidRandomLocalPosition(agentSpawnY);

        // 3. Randomize Flag local position (guaranteeing minimum distance from safe zone & agent)
        if (flag != null)
        {
            flag.localRotation = Quaternion.identity;
            int attempts = 0;
            float minSafeDist = 3.5f;
            Vector3 randomFlagPos;

            do
            {
                randomFlagPos = GetValidRandomLocalPosition(flagOriginalY);
                attempts++;
            }
            while (attempts < 50 && safeZone != null &&
                   (Vector3.Distance(randomFlagPos, safeZone.localPosition) < minSafeDist ||
                    Vector3.Distance(randomFlagPos, transform.localPosition) < 2.0f));

            flag.localPosition = randomFlagPos;
        }

        // 4. Reset all defenders
        if (defenders != null)
        {
            foreach (var def in defenders)
            {
                if (def != null) def.ResetDefender(true);
            }
        }

        // 5. Reset all dynamic obstacles
        if (obstacles != null)
        {
            foreach (var obs in obstacles)
            {
                if (obs != null) obs.ResetObstacle();
            }
        }

        // 6. Compute initial baseline distance to flag
        if (flag != null)
        {
            lastDistance = Vector3.Distance(transform.localPosition, flag.localPosition);
        }
    }

    private Vector3 GetValidRandomLocalPosition(float heightY)
    {
        return new Vector3(
            Random.Range(minX + safeMargin, maxX - safeMargin),
            heightY,
            Random.Range(minZ + safeMargin, maxZ - safeMargin)
        );
    }

    private void GiveRewardWithLog(float amount, string reason)
    {
        AddReward(amount);
        if (logRewards)
        {
            Debug.Log($"[Maaouia Reward] {(amount >= 0 ? "+" : "")}{amount:F3} ({reason}) | Total: {GetCumulativeReward():F3}");
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // 1. Vector to Flag in local space (3 floats)
        if (flag != null)
            sensor.AddObservation(flag.localPosition - transform.localPosition);
        else
            sensor.AddObservation(Vector3.zero);

        // 2. Vector to Safe Zone in local space (3 floats)
        if (safeZone != null)
            sensor.AddObservation(safeZone.localPosition - transform.localPosition);
        else
            sensor.AddObservation(Vector3.zero);

        // 3. Flag acquisition state (1 float)
        sensor.AddObservation(hasFlag ? 1.0f : 0.0f);

        // 4. Relative local positions of 3 Defenders (3 * 3 = 9 floats)
        for (int i = 0; i < 3; i++)
        {
            if (defenders != null && i < defenders.Length && defenders[i] != null)
                sensor.AddObservation(defenders[i].transform.localPosition - transform.localPosition);
            else
                sensor.AddObservation(Vector3.zero);
        }

        // 5. Relative local positions of 3 Moving Obstacles (3 * 3 = 9 floats)
        // TOTAL VECTOR OBSERVATIONS = 3 + 3 + 1 + 9 + 9 = EXACTLY 25 FLOATS
        for (int i = 0; i < 3; i++)
        {
            if (obstacles != null && i < obstacles.Length && obstacles[i] != null)
                sensor.AddObservation(obstacles[i].transform.localPosition - transform.localPosition);
            else
                sensor.AddObservation(Vector3.zero);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (episodeEnding) return;
        EnsureRigidbody();

        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];

        // Apply velocity in horizontal plane
        if (rb != null)
        {
            rb.linearVelocity = new Vector3(moveX * moveSpeed, rb.linearVelocity.y, moveZ * moveSpeed);
        }
        else
        {
            // Fallback kinematic translation if Rigidbody is ever unavailable
            transform.localPosition += new Vector3(moveX, 0f, moveZ) * moveSpeed * Time.deltaTime;
        }

        // Reward shaping: distance progress towards current objective
        Vector3 targetLocalPos = hasFlag && safeZone != null ? safeZone.localPosition : (flag != null ? flag.localPosition : transform.localPosition);
        float currentDist = Vector3.Distance(transform.localPosition, targetLocalPos);
        float progress = lastDistance - currentDist;
        float shaping = Mathf.Clamp(progress * 0.5f, -0.1f, 0.1f);
        GiveRewardWithLog(shaping, hasFlag ? "Progress to SafeZone" : "Progress to Flag");
        lastDistance = currentDist;

        // Radius auto-pickup / delivery (prevents tunneling through triggers)
        if (!hasFlag && currentDist <= flagPickupRadius)
        {
            CaptureFlag();
        }
        else if (hasFlag && currentDist <= safeZoneDeliveryRadius)
        {
            DeliverFlag();
        }

        // Time step penalty
        GiveRewardWithLog(timePenalty, "Step Penalty");

        // Defender danger zone penalty (proximity avoidance)
        if (defenders != null)
        {
            foreach (var def in defenders)
            {
                if (def == null) continue;
                float d = Vector3.Distance(transform.localPosition, def.transform.localPosition);
                if (d < 1.0f)
                {
                    GiveRewardWithLog(-0.01f, "Proximity to Defender");
                }
            }
        }

        // Dynamic obstacle danger zone penalty
        if (obstacles != null)
        {
            foreach (var obs in obstacles)
            {
                if (obs == null) continue;
                float d = Vector3.Distance(transform.localPosition, obs.transform.localPosition);
                if (d < 1.0f)
                {
                    GiveRewardWithLog(-0.01f, "Proximity to Obstacle");
                }
            }
        }
    }

    private void Update()
    {
        // Smoothly position held flag above agent
        if (hasFlag && flag != null)
        {
            flag.position = transform.position + flagCarryOffset;
            flag.rotation = transform.rotation;
        }
    }

    private void FixedUpdate()
    {
        if (episodeEnding) return;

        // Fall reset check
        if (transform.localPosition.y < fallResetY)
        {
            GiveRewardWithLog(-1.0f, "Fell out of arena");
            SetFloorMaterial(loseFloorMaterial);
            EndEpisode();
            return;
        }

        // Local arena boundaries collision fallback
        Vector3 pos = transform.localPosition;
        if (pos.x <= minX || pos.x >= maxX || pos.z <= minZ || pos.z >= maxZ)
        {
            GiveRewardWithLog(wallCollisionPenalty, "Hit boundary wall");
            if (hasFlag) GiveRewardWithLog(wallCollisionPenalty * 2f, "Crash with flag");
            SetFloorMaterial(loseFloorMaterial);
            EndEpisode();
        }
    }

    private void CaptureFlag()
    {
        if (hasFlag) return;
        hasFlag = true;

        if (safeZone != null)
        {
            lastDistance = Vector3.Distance(transform.localPosition, safeZone.localPosition);
        }

        GiveRewardWithLog(flagPickupReward, "Flag captured");
    }

    private void DeliverFlag()
    {
        if (!hasFlag || episodeEnding) return;
        episodeEnding = true;

        GiveRewardWithLog(safeZoneReward, "Mission Completed - Flag Delivered!");
        SetFloorMaterial(winFloorMaterial);

        StartCoroutine(EndEpisodeDelayed(1.0f));
    }

    private void HitDefender(string defenderName)
    {
        if (episodeEnding) return;
        episodeEnding = true;

        GiveRewardWithLog(defenderCollisionPenalty, $"Caught by {defenderName}");
        if (hasFlag) GiveRewardWithLog(wallCollisionPenalty * 2f, "Caught carrying flag");

        SetFloorMaterial(loseFloorMaterial);
        StartCoroutine(EndEpisodeDelayed(0.3f));
    }

    private void HitObstacle(string obstacleName)
    {
        if (episodeEnding) return;
        episodeEnding = true;

        GiveRewardWithLog(obstacleCollisionPenalty, $"Collision with obstacle {obstacleName}");
        if (hasFlag) GiveRewardWithLog(obstacleCollisionPenalty * 2f, "Obstacle hit carrying flag");

        SetFloorMaterial(loseFloorMaterial);
        StartCoroutine(EndEpisodeDelayed(0.3f));
    }

    private void HitWall()
    {
        if (episodeEnding) return;
        GiveRewardWithLog(wallCollisionPenalty, "Collision with wall");
        if (hasFlag) GiveRewardWithLog(wallCollisionPenalty * 2f, "Wall hit carrying flag");

        SetFloorMaterial(loseFloorMaterial);
        EndEpisode();
    }

    private IEnumerator EndEpisodeDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        episodeEnding = false;
        EndEpisode();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsMatchingTag(other.gameObject, "Flag") && !hasFlag)
        {
            CaptureFlag();
            return;
        }

        if (IsMatchingTag(other.gameObject, "SafeZone") && hasFlag && !episodeEnding)
        {
            DeliverFlag();
            return;
        }

        if (IsMatchingTag(other.gameObject, "Defender") && !episodeEnding)
        {
            HitDefender(other.name);
            return;
        }

        if ((IsMatchingTag(other.gameObject, "Obstacle") || other.GetComponent<ObstacleControllerMaaouia>() != null) && !episodeEnding)
        {
            HitObstacle(other.name);
            return;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        GameObject go = collision.gameObject;

        if (IsMatchingTag(go, "Flag") && !hasFlag)
        {
            CaptureFlag();
            return;
        }

        if (IsMatchingTag(go, "SafeZone") && hasFlag && !episodeEnding)
        {
            DeliverFlag();
            return;
        }

        if (IsMatchingTag(go, "Wall"))
        {
            HitWall();
            return;
        }

        if (IsMatchingTag(go, "Defender") && !episodeEnding)
        {
            HitDefender(go.name);
            return;
        }

        if ((IsMatchingTag(go, "Obstacle") || go.GetComponent<ObstacleControllerMaaouia>() != null) && !episodeEnding)
        {
            HitObstacle(go.name);
            return;
        }
    }

    private bool IsMatchingTag(GameObject go, string baseTag)
    {
        return go.CompareTag(baseTag) || go.CompareTag(baseTag.ToLower()) || go.CompareTag(baseTag.ToUpper());
    }

    private void SetFloorMaterial(Material mat)
    {
        if (floorRenderer != null && mat != null)
        {
            floorRenderer.material = mat;
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuous = actionsOut.ContinuousActions;
        continuous[0] = Input.GetAxisRaw("Horizontal");
        continuous[1] = Input.GetAxisRaw("Vertical");
    }
}
