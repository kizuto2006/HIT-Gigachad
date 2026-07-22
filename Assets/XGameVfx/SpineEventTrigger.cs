using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Spine.Unity;
using System;
using UnityEngine.Events;

public class SpineEventTrigger : MonoBehaviour
{
    [SerializeField] private SkeletonGraphic skeletonGraphic;
    Spine.AnimationState animationState;
    public SpineEventHolder[] SpineEventHolder;

    private void Awake()
    {
        skeletonGraphic = GetComponent<SkeletonGraphic>();
        animationState = skeletonGraphic.AnimationState;
        animationState.Event += OnUserDefinedEvent;
    }

    public void OnUserDefinedEvent(Spine.TrackEntry trackEntry, Spine.Event e)
    {
        for (int i = 0; i < SpineEventHolder.Length; i++)
        {
            if (e.Data.Name == SpineEventHolder[i].eventName)
            {
                SpineEventHolder[i].unityAction.Invoke();
            }
        }
    }
}

[System.Serializable]
public class SpineEventHolder 
{
    [SpineEvent] public string eventName;
    public UnityEvent unityAction;
}
