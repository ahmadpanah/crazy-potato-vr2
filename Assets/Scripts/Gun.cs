using UnityEngine;

public class Gun : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] GameObject muzzle;
    [SerializeField] Transform muzzlePosition;
    [SerializeField] GameObject projectile;

    [Header("Config")]
    [SerializeField] float fireDistance = 10;
    [SerializeField] float fireRate = 0.5f;

    Transform player;
    Vector2 offset;
    private float timesinceLastShot = 0f;
    Transform closetEnemy;
    Animator anim;

    private void Start()
    {
        anim = GetComponent<Animator>();
        timesinceLastShot = fireRate;
        player = GameObject.Find("Player").transform;
    }

    private void Update()
    {
        transform.position = (Vector2)player.position + offset;

        FindClosestEnemy();
    }

    void FindClosestEnemy()
    {
        closetEnemy = null;
        Enemy[] enemies = FindObjectsOfType<Enemy>();

        foreach (Enemy enemy in enemies)
        {
            float distance = Vector2.Distance(transform.position,enemy.transform.position);
            if(distance <= fireDistance)
            {
                closetEnemy = enemy.transform;
            }
        }
    }


}
