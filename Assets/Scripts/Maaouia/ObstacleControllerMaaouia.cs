using UnityEngine;

public class ObstacleControllerMaaouia : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 1.0f;
    [SerializeField] private float changeDirectionTime = 3.0f;
    [SerializeField] private float destinationReachedThreshold = 0.35f;

    [Header("Local Arena Bounds")]
    [SerializeField] private float minX = -5.0f;
    [SerializeField] private float maxX = 5.0f;
    [SerializeField] private float minZ = -5.0f;
    [SerializeField] private float maxZ = 5.0f;

    [Header("Reset Behavior")]
    [SerializeField] private bool randomizeSpawnOnReset = true;

    private Vector3 initialLocalSpawn;
    private Vector3 targetLocalPosition;
    private float waitTimer;
    private bool isWaiting;
    private Vector3 localVelocity;

    public Vector3 Velocity => localVelocity;
    public float MoveSpeed { get => moveSpeed; set => moveSpeed = value; }

    private void Awake()
    {
        initialLocalSpawn = transform.localPosition;
    }

    private void Start()
    {
        PickNewLocalDestination();
    }

    private void Update()
    {
        if (isWaiting)
        {
            localVelocity = Vector3.zero;
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                isWaiting = false;
                PickNewLocalDestination();
            }
            return;
        }

        Vector3 direction = targetLocalPosition - transform.localPosition;
        direction.y = 0f;

        if (direction.magnitude <= destinationReachedThreshold)
        {
            isWaiting = true;
            waitTimer = changeDirectionTime;
            localVelocity = Vector3.zero;
            return;
        }

        Vector3 move = direction.normalized * moveSpeed * Time.deltaTime;
        transform.localPosition += move;
        localVelocity = move / Time.deltaTime;
    }

    public void PickNewLocalDestination()
    {
        targetLocalPosition = new Vector3(
            Random.Range(minX + 0.8f, maxX - 0.8f),
            transform.localPosition.y,
            Random.Range(minZ + 0.8f, maxZ - 0.8f)
        );
    }

    public void ResetObstacle()
    {
        isWaiting = false;
        localVelocity = Vector3.zero;

        if (randomizeSpawnOnReset)
        {
            transform.localPosition = new Vector3(
                Random.Range(minX + 0.8f, maxX - 0.8f),
                initialLocalSpawn.y,
                Random.Range(minZ + 0.8f, maxZ - 0.8f)
            );
        }
        else
        {
            transform.localPosition = initialLocalSpawn;
        }

        PickNewLocalDestination();
    }
}
