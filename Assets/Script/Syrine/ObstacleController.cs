using UnityEngine;

public class ObstacleController : MonoBehaviour
{
    [SerializeField] private float arenaSize = 9f;
    [SerializeField] private float safeMargin = 1.5f;
    [SerializeField] private LayerMask blockingLayer; 
    [SerializeField] private float spawnCheckRadius = 1f;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogError($"{gameObject.name} : Aucun Rigidbody trouvé ! Ajoute-en un dans l'Inspector.");
            enabled = false;
            return;
        }
    }

    public void ResetObstacle(Collider safeZoneCollider = null, float safeZoneMargin = 1f)
    {
        Vector3 pos;
        int attempts = 0;
        bool insideSafeZone;

        do
        {
            pos = new Vector3(
                Random.Range(-arenaSize + safeMargin, arenaSize - safeMargin),
                transform.localPosition.y,
                Random.Range(-arenaSize + safeMargin, arenaSize - safeMargin)
            );
            attempts++;

            insideSafeZone = false;
            if (safeZoneCollider != null)
            {
                Bounds b = safeZoneCollider.bounds;
                b.Expand(safeZoneMargin);
                insideSafeZone = b.Contains(pos);
            }
        }
        while ((Physics.CheckSphere(pos, spawnCheckRadius, blockingLayer) || insideSafeZone) && attempts < 30);

        rb.position = pos;

        // Rotation aléatoire pour varier l'orientation de la barrière
        rb.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
    }
}