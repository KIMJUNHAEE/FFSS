using System;
using System.Collections.Generic;
using UnityEngine;

namespace FFSS.Framework.Flow
{
    public enum GameFlowState
    {
        Boot,
        Title,
        Load,
        Field,
        Event,
        Combat,
        Break,
        Reward,
        Rest,
        ActTransition,
        Result
    }

    [Serializable]
    public sealed class GameFlowTransition
    {
        public GameFlowState from;
        public GameFlowState to;
    }

    [CreateAssetMenu(menuName = "FFSS/Flow/Game Flow Definition", fileName = "GameFlowDefinition")]
    public sealed class GameFlowDefinition : ScriptableObject
    {
        [SerializeField] private GameFlowState initialState = GameFlowState.Boot;
        [SerializeField] private List<GameFlowTransition> transitions = new List<GameFlowTransition>();

        public GameFlowState InitialState => initialState;

        public bool Allows(GameFlowState from, GameFlowState to)
        {
            if (from == to)
            {
                return true;
            }

            return transitions.Exists(transition => transition != null && transition.from == from && transition.to == to);
        }
    }
}
