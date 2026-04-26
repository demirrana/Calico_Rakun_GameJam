using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class TileData
{
    public Transform tileTransform;
    public bool hasTreasure = false;

    [Header("Tuzak Sistemi (Trap System)")]
    public UnityEvent onTrapTriggered;

    public TrapType trapType = TrapType.None;

    public GameObject trapElement;

    public bool isBlocked = false;
}

public enum TrapType { None, Mine, Piston, Teleport }