using UnityEngine;

public class DefenderController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float changeDirectionTime = 3f;
    [SerializeField] private float arenaSize = 9f;

    private Vector3 currentDirection;
    private float timer;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogError($"{gameObject.name} : Aucun Rigidbody trouvé sur cet objet ! Ajoute-en un dans l'Inspector.");
            enabled = false; 
            return;
        }

        ResetDefender();
    }
    public void ResetDefender()
    {
        Vector3 spawnPos = new Vector3(
            Random.Range(-arenaSize, arenaSize),
            transform.localPosition.y,
            Random.Range(-arenaSize, arenaSize)
        );

        // On repositionne via le Rigidbody plutôt que transform directement,
        // pour rester cohérent avec un corps kinematic
        rb.position = spawnPos;

        currentDirection = new Vector3(
            Random.Range(-1f, 1f),
            0,
            Random.Range(-1f, 1f)
        ).normalized;

        timer = 0f;
    }

    void FixedUpdate()
    {
        timer += Time.fixedDeltaTime;

        if (timer >= changeDirectionTime)
        {
            currentDirection = new Vector3(
                Random.Range(-1f, 1f),
                0,
                Random.Range(-1f, 1f)
            ).normalized;
            timer = 0f;
        }

        // Calcul de la position cible
        Vector3 targetPos = rb.position + currentDirection * moveSpeed * Time.fixedDeltaTime;

        // Clamp AVANT d'appliquer le déplacement (et non après, sur transform directement)
        targetPos.x = Mathf.Clamp(targetPos.x, -arenaSize + 0.5f, arenaSize - 0.5f);
        targetPos.z = Mathf.Clamp(targetPos.z, -arenaSize + 0.5f, arenaSize - 0.5f);

        rb.MovePosition(targetPos);
    }

    public Vector3 Velocity => rb.linearVelocity;
}