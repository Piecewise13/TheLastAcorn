using UnityEngine;

public class Acorn : MonoBehaviour, ICollectible
{
    [SerializeField] int value = 1; // define this with team
    public int Value => value;
}

