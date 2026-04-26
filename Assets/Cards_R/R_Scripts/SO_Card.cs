using UnityEngine;

[CreateAssetMenu(fileName = "Card")]
public class SO_Card : ScriptableObject
{
    public CardManager.CardType cardType;
    public Sprite frontFace;
    public string hintText;
}