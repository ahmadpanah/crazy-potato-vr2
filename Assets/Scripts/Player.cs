using UnityEngine;
using TMPro;

public class Player : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI healthText;
    Animator anim;
    Rigidbody2D rb;

    float moveSpeed = 6;
    int maxHealth = 100;
    int currentHealth;

    bool dead = false;

    float moveHorizontal,moveVertical;
    Vector2 movement;

    int facingDirection = 1; // 1 = right, -1 = left

    private void Start(){
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
    }

}
