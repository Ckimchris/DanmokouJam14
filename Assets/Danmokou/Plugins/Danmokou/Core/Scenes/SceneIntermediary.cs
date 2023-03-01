﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Scriptables;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Danmokou.Scenes {
/// <summary>
/// Manages scene transitions.
/// </summary>
public class SceneIntermediary : CoroutineRegularUpdater, ISceneIntermediary {
    public static bool IsFirstScene { get; private set; } = true;
    //Use a bool here since EngineStateManager is updated at end of frame.
    //We need to keep track of whether or not this process has been queued
    public static bool LOADING { get; private set; } = false;

    public CameraTransitionConfig defaultTransition = null!;
    private Cancellable sceneToken = new Cancellable();
    public ICancellee SceneBoundedToken => sceneToken;

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService<ISceneIntermediary>(this, new ServiceLocator.ServiceOptions { Unique = true });
        Listen(PreSceneUnload, () => {
            sceneToken.Cancel();
            sceneToken = new Cancellable();
        });
    }

    public SceneLoading? LoadScene(SceneRequest req) {
        if (EngineStateManager.State < EngineState.LOADING_PAUSE && !LOADING) {
            Logs.Log($"Successfully requested scene load for {req}.");
            IsFirstScene = false;
            LOADING = true;
            var stateToken = EngineStateManager.RequestState(EngineState.LOADING_PAUSE);
            var loader = new SceneLoading(new(), new(), new());
            RunRIEnumerator(WaitForSceneLoad(stateToken, req, loader, true));
            return loader;
        } else Logs.Log($"REJECTED scene load for {req}. Current game state is {EngineStateManager.State} " +
                        $"(loading: {LOADING})", true, LogLevel.WARNING);
        return null;
    }


    private IEnumerator WaitForSceneLoad(IDisposable stateToken, SceneRequest req, SceneLoading loader, bool transitionOnSame) {
        var currScene = SceneManager.GetActiveScene().name;
        float waitOut = 0f;
        if (transitionOnSame || currScene != req.scene.sceneName) {
            var transition = req.Transition ?? (req.scene.transitionIn == null ? defaultTransition : req.scene.transitionIn);
            ServiceLocator.Find<ICameraTransition>().Fade(transition, out var waitIn, out waitOut);
            Logs.Log($"Performing fade transition for {waitIn}s before loading scene.");
            for (; waitIn > ETime.FRAME_YIELD; waitIn -= ETime.FRAME_TIME) yield return null;
        }
        //Logs.Log($"Scene loading for {req} started.", level: LogLevel.DEBUG1);
        PreSceneUnload.OnNext(default);
        req.onPreLoad?.Invoke();
        loader.Preloading.SetResult(default);
        var op = SceneManager.LoadSceneAsync(req.scene.sceneName);
        while (!op.isDone) {
            yield return null;
        }
        Logs.Log($"Unity finished loading scene {req}. " +
                 $"The out transition will take {waitOut}s, but the scene will start immediately.",
            level: LogLevel.DEBUG3);
        req.onLoaded?.Invoke();
        loader.Loading.SetResult(default);
        req.onFinished?.Invoke();
        loader.Finishing.SetResult(default);
        stateToken.Dispose();
        for (; waitOut > ETime.FRAME_YIELD; waitOut -= ETime.FRAME_TIME) yield return null;
        LOADING = false;
        yield return null;
    }
    
    public override EngineState UpdateDuring => EngineState.LOADING_PAUSE;
    public override int UpdatePriority => UpdatePriorities.SOF;
    

    //Static stuff
    public static Event<Unit> PreSceneUnload { get; } = new Event<Unit>();
    public static Event<Unit> SceneUnloaded { get; } = new Event<Unit>();
    public static Event<Scene> SceneLoaded { get; } = new();

    public static void Attach() {
        SceneManager.sceneUnloaded += s => {
            Logs.Log($"Unity scene {s.name} was unloaded");
            SceneUnloaded.OnNext(default);
        };
        SceneManager.sceneLoaded += (s, m) => {
            Logs.Log($"Unity scene {s.name} was loaded via mode {m.ToString()}");
            SceneLoaded.OnNext(s);
        };
    }
}
}
