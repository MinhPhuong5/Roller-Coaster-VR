using UnityEngine;

public class RandomTitanAttack : MonoBehaviour
{
    [SerializeField] private string stateName = "Zombie Attack";
    [SerializeField] private Vector2 speedRange = new Vector2(0.88f, 1.12f);

    void Start()
    {
        Animator animator = GetComponent<Animator>();
        if (animator == null)
            return;

        animator.Play(stateName, 0, Random.value);
        animator.speed = Random.Range(speedRange.x, speedRange.y);
    }
}
