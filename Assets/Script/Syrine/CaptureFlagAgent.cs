using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class CaptureFlagAgent : Agent
{
    [Header("Références Scène")]
    [SerializeField] private Transform flag;
    [SerializeField] private Transform safeZone;
    [SerializeField] private Collider safeZoneCollider;
    [SerializeField] private DefenderController[] defenders;
    [SerializeField] private ObstacleController[] obstacles;

    [Header("Feedback visuel")]
    [SerializeField] private Renderer floorRenderer;
    [SerializeField] private Material defaultFloorMaterial;
    [SerializeField] private Material successFloorMaterial;

    [Header("Paramètres Physiques")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float arenaSize = 9f;
    [SerializeField] private float safeMargin = 1.5f;

    [Header("Anti-collision spawn")]
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private float spawnCheckRadius = 0.5f;

    [Header("Portage du drapeau")]
    [SerializeField] private Vector3 flagCarryOffset = new Vector3(0f, 1.5f, 0f);

    [Header("Sécurité")]
    [SerializeField] private float fallResetY = -2f;

    [Header("Debug")]
    [SerializeField] private bool logRewards = true;

    private bool hasFlag = false;
    private Rigidbody rb;
    private float lastDistance;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnEpisodeBegin()
    {
        // La safe zone est un gros objet : on utilise un rayon de vérification plus large (2.5)
        // pour s'assurer qu'elle ne chevauche pas les murs, pas seulement son centre.
        safeZone.localPosition = GetValidRandomPosition(0.1f, 0f, 2.5f);

        foreach (var obstacle in obstacles)
        {
            if (obstacle != null) obstacle.ResetObstacle(safeZoneCollider, 1f);
        }

        transform.localPosition = GetValidRandomPosition(0.5f, 1f);
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        hasFlag = false;

        flag.localPosition = GetValidRandomPosition(0.5f, 1f);

        foreach (var defender in defenders)
        {
            if (defender != null) defender.ResetDefender();
        }

        lastDistance = Vector3.Distance(transform.localPosition, flag.localPosition);

        if (floorRenderer != null && defaultFloorMaterial != null)
            floorRenderer.material = defaultFloorMaterial;
    }

    private Vector3 GetValidRandomPosition(float height, float safeZoneMargin = 1f, float footprintRadius = -1f)
    {
        Vector3 pos;
        int attempts = 0;
        float checkRadius = footprintRadius > 0f ? footprintRadius : spawnCheckRadius;

        do
        {
            pos = new Vector3(
                Random.Range(-arenaSize + safeMargin, arenaSize - safeMargin),
                height,
                Random.Range(-arenaSize + safeMargin, arenaSize - safeMargin)
            );
            attempts++;
        }
        while ((Physics.CheckSphere(pos, checkRadius, wallLayer) || IsInsideSafeZone(pos, safeZoneMargin)) && attempts < 30);

        return pos;
    }

    private bool IsInsideSafeZone(Vector3 pos, float margin)
    {
        if (safeZoneCollider == null) return false;
        Bounds b = safeZoneCollider.bounds;
        b.Expand(margin);
        return b.Contains(pos);
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
        sensor.AddObservation(flag.localPosition - transform.localPosition);
        sensor.AddObservation(safeZone.localPosition - transform.localPosition);
        sensor.AddObservation(rb.linearVelocity);

        if (defenders.Length > 0 && defenders[0] != null)
        {
            sensor.AddObservation(defenders[0].transform.localPosition - transform.localPosition);
            sensor.AddObservation(defenders[0].Velocity);
        }
        else
        {
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(Vector3.zero);
        }

        sensor.AddObservation(hasFlag ? 1f : 0f);

        // Observation directe des 3 obstacles (position relative à l'agent)
        foreach (var obstacle in obstacles)
        {
            if (obstacle != null)
                sensor.AddObservation(obstacle.transform.localPosition - transform.localPosition);
            else
                sensor.AddObservation(Vector3.zero); // toujours ajouter 3 valeurs, même si l'obstacle est manquant
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
        if (rb == null) return;

        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];
        rb.linearVelocity = new Vector3(moveX, 0f, moveZ) * moveSpeed;

        Vector3 targetPos = hasFlag ? safeZone.localPosition : flag.localPosition;
        float currentDist = Vector3.Distance(transform.localPosition, targetPos);
        float progress = lastDistance - currentDist;
        float shaping = Mathf.Clamp(progress * 0.3f, -0.05f, 0.05f);
        GiveReward(shaping, hasFlag ? "Progression Safe Zone" : "Progression drapeau");
        lastDistance = currentDist;

        GiveReward(-0.0005f, "Pénalité de temps");

        foreach (var def in defenders)
        {
            if (def == null) continue;
            float d = Vector3.Distance(transform.localPosition, def.transform.localPosition);
            if (d < 1.5f)
                GiveReward(-0.03f, $"Zone de danger proche de {def.name}");
            else if (d < 3.0f)
                GiveReward(-0.008f, $"Zone de risque proche de {def.name}");
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
            EndEpisode();
        }
    }

    private bool episodeEnding = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Flag") && !hasFlag)
        {
            hasFlag = true;
            GiveReward(2f, "Drapeau capturé");
            Debug.Log("Drapeau capturé ! Direction Safe Zone.");
        }

        if (other.CompareTag("SafeZone") && hasFlag && !episodeEnding)
        {
            GiveReward(3f, "Mission accomplie");
            Debug.Log("MISSION ACCOMPLIE !");

            if (floorRenderer != null && successFloorMaterial != null)
                floorRenderer.material = successFloorMaterial;

            episodeEnding = true;
            StartCoroutine(EndEpisodeAfterDelay(1f));
        }

        if (other.CompareTag("Defender") && !episodeEnding)
        {
            GiveReward(-2f, $"Touché par {other.name}");
            Debug.Log("Touché par un défenseur !");
            episodeEnding = true;
            StartCoroutine(EndEpisodeAfterDelay(0.3f));
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
        if (collision.gameObject.CompareTag("Wall"))
        {
            GiveReward(-1f, "Collision mur");
            Debug.Log("Collision mur !");
            EndEpisode();
        }
        if (collision.gameObject.CompareTag("Obstacle"))
        {
            GiveReward(-1f, "Collision obstacle");
            Debug.Log("Collision obstacle !");
            EndEpisode();
        }
    }
}