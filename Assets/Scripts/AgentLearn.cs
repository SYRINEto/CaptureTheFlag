using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class AgentLearn : Agent
{
    [Header("Références Scène")]
    [SerializeField] private Transform flag;
    [SerializeField] private Transform safeZone;
    [SerializeField] private DefenderControllerAziz[] defenders;
    [SerializeField] private ObstacleControllerAziz[] obstacles;

    [Header("Feedback visuel")]
    [SerializeField] private Renderer floorRenderer;
    [SerializeField] private Material winFloorMaterial;
    [SerializeField] private Material loseFloorMaterial;

    [Header("Paramètres Physiques")]
    [SerializeField] private float moveSpeed = 1f;
    [SerializeField] private float safeMargin = 1.5f;

    [Header("Interaction Radii (Pickup & Delivery)")]
    [SerializeField] private float flagPickupRadius = 0.9f;
    [SerializeField] private float safeZoneDeliveryRadius = 1.6f;

    [Header("Arena Bounds")]
    [SerializeField] private float minX = -5.26f;
    [SerializeField] private float maxX = 4.98f;
    [SerializeField] private float minZ = -6.48f;
    [SerializeField] private float maxZ = 3.76f;

    [Header("Anti-collision spawn")]
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private float spawnCheckRadius = 0.5f;

    [Header("Portage du drapeau")]
    [SerializeField] private Vector3 flagCarryOffset = new Vector3(0f, 1.5f, 0f);

    [Header("Sécurité")]
    [SerializeField] private float fallResetY = -2f;

    [Header("Rewards — Inspector")]
    [SerializeField] private float flagPickupReward = 2f;
    [SerializeField] private float safeZoneReward = 8f;
    [SerializeField] private float defenderCollisionPenalty = -4f;
    [SerializeField] private float wallCollisionPenalty = -6f;
    [SerializeField] private float obstacleCollisionPenalty = -4f;
    [SerializeField] private float timePenalty = -0.002f;

    [Header("Debug")]
    [SerializeField] private bool logRewards = true;

    private bool hasFlag = false;
    private Rigidbody rb;
    private float lastDistance;

    private float spawnY;
    private float flagOriginalY;
    private Vector3 editorSpawnPosition;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.freezeRotation = true;
        editorSpawnPosition = transform.localPosition;
        spawnY = transform.localPosition.y;

        if (flag != null)
        {
            flagOriginalY = flag.localPosition.y;
        }

        if (obstacles == null || obstacles.Length == 0)
        {
            obstacles = Object.FindObjectsByType<ObstacleControllerAziz>(FindObjectsSortMode.None);
        }
    }

    public override void OnEpisodeBegin()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        int spawnAttempts = 0;
        do
        {
            transform.localPosition = new Vector3(
                Random.Range(minX + safeMargin, maxX - safeMargin),
                spawnY,
                Random.Range(minZ + safeMargin, maxZ - safeMargin)
            );
            spawnAttempts++;
        }
        while (IsNearAnyHazard(transform.localPosition, 1.2f) && spawnAttempts < 30);

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        hasFlag = false;

        // 1. Spawn safeZone first
        if (safeZone != null)
        {
            safeZone.localPosition = GetValidRandomPosition(safeZone.localPosition.y);
        }

        // 2. Spawn flag and guarantee it never spawns inside or near safeZone, agent, or hazards
        if (flag != null)
        {
            flag.rotation = Quaternion.identity;
            int attempts = 0;
            float minDistance = 3.5f;

            do
            {
                flag.localPosition = GetValidRandomPosition(flagOriginalY);
                attempts++;
            }
            while (safeZone != null && 
                   (Vector3.Distance(flag.localPosition, safeZone.localPosition) < minDistance ||
                    Vector3.Distance(flag.localPosition, transform.localPosition) < 2.0f ||
                    IsNearAnyHazard(flag.localPosition, 1.2f)) && 
                   attempts < 50);
        }

        foreach (var defender in defenders)
        {
            if (defender != null) defender.ResetDefender();
        }

        if (obstacles != null)
        {
            foreach (var obstacle in obstacles)
            {
                if (obstacle != null) obstacle.ResetObstacle();
            }
        }

        Vector2 agent2D = new Vector2(transform.localPosition.x, transform.localPosition.z);
        Vector2 flag2D = new Vector2(flag.localPosition.x, flag.localPosition.z);
        lastDistance = Vector2.Distance(agent2D, flag2D);
    }

    private bool IsNearAnyHazard(Vector3 pos, float minHazardDist)
    {
        if (defenders != null)
        {
            foreach (var def in defenders)
            {
                if (def != null && Vector3.Distance(pos, def.transform.localPosition) < minHazardDist)
                    return true;
            }
        }
        if (obstacles != null)
        {
            foreach (var obs in obstacles)
            {
                if (obs != null && Vector3.Distance(pos, obs.transform.localPosition) < minHazardDist)
                    return true;
            }
        }
        return false;
    }

    private Vector3 GetValidRandomPosition(float height)
    {
        Vector3 pos;
        int attempts = 0;
        do
        {
            pos = new Vector3(
                Random.Range(minX + safeMargin, maxX - safeMargin),
                height,
                Random.Range(minZ + safeMargin, maxZ - safeMargin)
            );
            attempts++;
        }
        while (Physics.CheckSphere(pos, spawnCheckRadius, wallLayer) && attempts < 30);

        return pos;
    }

    private void GiveReward(float amount, string reason)
    {
        AddReward(amount);
        if (logRewards)
        {
            string sign = amount >= 0 ? "+" : "";
            Debug.Log($"[REWARD] {sign}{amount:F3} ({reason}) | Cumulé: {GetCumulativeReward():F3}");
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // 1. Direction to flag (Vector3 = 3 floats)
        sensor.AddObservation(flag.localPosition - transform.localPosition);
        // 2. Direction to safe zone (Vector3 = 3 floats)
        sensor.AddObservation(safeZone.localPosition - transform.localPosition);

        // 3. Has flag state (1 float)
        sensor.AddObservation(hasFlag ? 1f : 0f);

        // 4. Observe ALL 3 defenders relative positions (3 * 3 = 9 floats)
        for (int i = 0; i < 3; i++)
        {
            if (defenders != null && i < defenders.Length && defenders[i] != null)
            {
                sensor.AddObservation(defenders[i].transform.localPosition - transform.localPosition);
            }
            else
            {
                sensor.AddObservation(Vector3.zero);
            }
        }

        // 5. Observe ALL 3 moving obstacles relative positions (3 * 3 = 9 floats)
        // TOTAL OBSERVATIONS: 3 + 3 + 1 + 9 + 9 = EXACTLY 25 FLOATS (matches space size 25)
        for (int i = 0; i < 3; i++)
        {
            if (obstacles != null && i < obstacles.Length && obstacles[i] != null)
            {
                sensor.AddObservation(obstacles[i].transform.localPosition - transform.localPosition);
            }
            else
            {
                sensor.AddObservation(Vector3.zero);
            }
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxisRaw("Horizontal");
        continuousActions[1] = Input.GetAxisRaw("Vertical");
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        float moveX = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float moveZ = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

        Vector3 moveDir = new Vector3(moveX, 0f, moveZ);
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        // 1. Direct translation — guaranteed to move regardless of physics/friction/kinematic states
        transform.localPosition += moveDir * moveSpeed * Time.deltaTime;

        // 2. Also update physics velocity for Rigidbody interaction
        if (rb != null)
        {
            Vector3 vel = new Vector3(moveX * moveSpeed, rb.linearVelocity.y, moveZ * moveSpeed);
            rb.linearVelocity = vel;
        }

        // --- Shaping 2D : encouragement fort et direct vers l'objectif (dans le plan horizontal X/Z) ---
        Vector2 agentPos2D = new Vector2(transform.localPosition.x, transform.localPosition.z);
        Vector3 target3D = hasFlag ? safeZone.localPosition : flag.localPosition;
        Vector2 targetPos2D = new Vector2(target3D.x, target3D.z);

        float currentDist = Vector2.Distance(agentPos2D, targetPos2D);
        float progress = lastDistance - currentDist;
        float shaping = Mathf.Clamp(progress * 0.5f, -0.1f, 0.1f); 
        GiveReward(shaping, hasFlag ? "Progression Safe Zone" : "Progression drapeau");
        lastDistance = currentDist;

        // Auto-détection de portée : capture et livraison instantanées dès qu'il est à portée (finie l'hésitation)
        if (!hasFlag && currentDist <= flagPickupRadius)
        {
            CaptureFlag();
        }
        else if (hasFlag && currentDist <= safeZoneDeliveryRadius)
        {
            DeliverFlag();
        }

        // Pénalité de temps (l'incite à se dépêcher et ne pas hésiter)
        GiveReward(timePenalty, "Pénalité de temps");

        // Pénalité de proximité immédiate seulement (ne le paralyse plus à distance)
        foreach (var def in defenders)
        {
            if (def == null) continue;
            float d = Vector3.Distance(transform.localPosition, def.transform.localPosition);
            if (d < 1.0f)
            {
                GiveReward(-0.01f, $"Danger immédiat {def.name}");
            }
        }

        // Pénalité de proximité immédiate pour les obstacles (l'incite à les éviter)
        if (obstacles != null)
        {
            foreach (var obs in obstacles)
            {
                if (obs == null) continue;
                float d = Vector3.Distance(transform.localPosition, obs.transform.localPosition);
                if (d < 1.0f)
                {
                    GiveReward(-0.01f, $"Danger immédiat obstacle {obs.name}");
                }
            }
        }
    }
    void Update()
    {
        if (hasFlag)
        {
            flag.position = transform.position + flagCarryOffset;
            flag.rotation = transform.rotation;
        }
    }

    private void FixedUpdate()
    {
        if (transform.localPosition.y < fallResetY)
        {
            GiveReward(-1f, "Agent tombé hors de l'arène");
            SetLoseMat();
            EndEpisode();
            return;
        }

        // Check if agent hits or exceeds arena bounds
        Vector3 pos = transform.localPosition;
        if (pos.x <= minX || pos.x >= maxX || pos.z <= minZ || pos.z >= maxZ)
        {
            GiveReward(wallCollisionPenalty, "Collision mur (limite arène)");

            // If he crashes while carrying the flag, give an extra penalty so flag-suicide is impossible
            if (hasFlag)
            {
                GiveReward(wallCollisionPenalty * 2f, "Suicide avec drapeau");
            }

            Debug.Log("Collision mur !");
            SetLoseMat();
            EndEpisode();
        }
    }
    private bool episodeEnding = false;

    private void CaptureFlag()
    {
        if (hasFlag) return;
        hasFlag = true;
        if (safeZone != null)
        {
            Vector2 agent2D = new Vector2(transform.localPosition.x, transform.localPosition.z);
            Vector2 safe2D = new Vector2(safeZone.localPosition.x, safeZone.localPosition.z);
            lastDistance = Vector2.Distance(agent2D, safe2D);
        }
        GiveReward(flagPickupReward, "Drapeau capturé");
        Debug.Log("Drapeau capturé ! Direction Safe Zone.");
    }

    private void DeliverFlag()
    {
        if (!hasFlag || episodeEnding) return;
        GiveReward(safeZoneReward, "Mission accomplie");
        Debug.Log("MISSION ACCOMPLIE !");

        SetWinMat();

        episodeEnding = true;
        StartCoroutine(EndEpisodeAfterDelay(1f));
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1. Flag pickup check (tag: flag)
        if (other.CompareTag("flag") && !hasFlag)
        {
            CaptureFlag();
            return;
        }

        // 2. SafeZone delivery check (tag: SafeZone)
        if (other.CompareTag("SafeZone") && hasFlag && !episodeEnding)
        {
            DeliverFlag();
            return;
        }

        // 3. Defender collision check (tag: defender)
        if (other.CompareTag("defender") && !episodeEnding)
        {
            GiveReward(defenderCollisionPenalty, $"Touché par {other.name}");

            // If he gets caught while carrying the flag, give the same severe penalty as colliding with the wall
            if (hasFlag)
            {
                GiveReward(wallCollisionPenalty * 2f, "Capturé par défenseur avec drapeau");
            }

            Debug.Log("Touché par un défenseur !");
            SetLoseMat();
            episodeEnding = true;
            StartCoroutine(EndEpisodeAfterDelay(0.3f));
            return;
        }

        // 4. Moving obstacle collision check (tag: obstacle or ObstacleController component)
        if ((other.CompareTag("obstacle") || other.GetComponent<ObstacleControllerAziz>() != null) && !episodeEnding)
        {
            GiveReward(obstacleCollisionPenalty, $"Touché par obstacle {other.name}");

            if (hasFlag)
            {
                GiveReward(obstacleCollisionPenalty * 2f, "Collision obstacle avec drapeau");
            }

            Debug.Log($"Touché par un obstacle ({other.name}) !");
            SetLoseMat();
            episodeEnding = true;
            StartCoroutine(EndEpisodeAfterDelay(0.3f));
            return;
        }
    }

    private System.Collections.IEnumerator EndEpisodeAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        episodeEnding = false;
        EndEpisode();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("flag") && !hasFlag)
        {
            CaptureFlag();
            return;
        }
        else if (collision.gameObject.CompareTag("SafeZone") && hasFlag && !episodeEnding)
        {
            DeliverFlag();
            return;
        }
        else if (collision.gameObject.CompareTag("wall"))
        {
            GiveReward(wallCollisionPenalty, "Collision mur");

            // If he crashes while carrying the flag, give an extra penalty so flag-suicide is impossible
            if (hasFlag)
            {
                GiveReward(wallCollisionPenalty * 2f, "Suicide avec drapeau");
            }

            Debug.Log("Collision mur !");
            SetLoseMat();
            EndEpisode();
        }
        else if (collision.gameObject.CompareTag("defender") && !episodeEnding)
        {
            GiveReward(defenderCollisionPenalty, $"Touché par {collision.gameObject.name}");

            // If he gets caught while carrying the flag, give the same severe penalty as colliding with the wall
            if (hasFlag)
            {
                GiveReward(wallCollisionPenalty * 2f, "Capturé par défenseur avec drapeau");
            }

            Debug.Log("Touché par un défenseur !");
            SetLoseMat();
            episodeEnding = true;
            StartCoroutine(EndEpisodeAfterDelay(0.3f));
        }
        else if ((collision.gameObject.CompareTag("obstacle") || collision.gameObject.GetComponent<ObstacleControllerAziz>() != null) && !episodeEnding)
        {
            GiveReward(obstacleCollisionPenalty, $"Touché par obstacle {collision.gameObject.name}");

            if (hasFlag)
            {
                GiveReward(obstacleCollisionPenalty * 2f, "Collision obstacle avec drapeau");
            }

            Debug.Log($"Touché par un obstacle ({collision.gameObject.name}) !");
            SetLoseMat();
            episodeEnding = true;
            StartCoroutine(EndEpisodeAfterDelay(0.3f));
        }
    }

    private void SetWinMat()
    {
        if (floorRenderer != null && winFloorMaterial != null)
            floorRenderer.material = winFloorMaterial;
    }

    private void SetLoseMat()
    {
        if (floorRenderer != null && loseFloorMaterial != null)
            floorRenderer.material = loseFloorMaterial;
    }
}