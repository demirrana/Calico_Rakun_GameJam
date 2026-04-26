using System;
using UnityEngine;

public class CardEventArgs
{
    public class ChooseCardEventArgs : EventArgs
    {
        public bool IsPlayer1sTurn { get; set; }
        public Card ChosenCard { get; set; }

        public ChooseCardEventArgs(bool isPlayer1sTurn, Card card)
        {
            IsPlayer1sTurn = isPlayer1sTurn;
            ChosenCard = card;
        }
    }
}