using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Seabright
{
    public sealed partial class CityView
    {
        readonly List<Vector3> steamSources=new List<Vector3>();
        ParticleSystem steam;
        float steamTimer;
        int steamSeed;
        private void UpdateSteam(float dt)
        {
            if(!steam){var shader=Resources.Load<Shader>("HarborSteam");if(!shader)return;var go=new GameObject("Harbor works • drifting steam");go.transform.SetParent(transform,false);steam=go.AddComponent<ParticleSystem>();var main=steam.main;main.loop=true;main.maxParticles=160;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startSpeed=0;main.startLifetime=8;main.startSize=2;main.startColor=new Color(.79f,.82f,.80f,.16f);var emission=steam.emission;emission.enabled=false;var size=steam.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.3f,1,2.8f));var color=steam.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(.6f,0),new GradientAlphaKey(.4f,.5f),new GradientAlphaKey(0,1)});color.color=gradient;var renderer=steam.GetComponent<ParticleSystemRenderer>();var mat=new Material(shader){name="Diffuse drifting steam"};mats["steam"]=mat;renderer.sharedMaterial=mat;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;steam.Play();}
            if(dt<=0){if(steam.isPlaying)steam.Pause();return;}if(steam.isPaused)steam.Play();
            steamTimer+=dt;if(steamTimer<.9f)return;steamTimer=0;
            foreach(var source in steamSources){steamSeed++;var emit=new ParticleSystem.EmitParams{position=source,velocity=new Vector3(.65f+H(steamSeed,1)*.25f,1.0f+H(steamSeed,2)*.7f,.3f),startSize=1.6f+H(steamSeed,3),startLifetime=7+H(steamSeed,4)*3,rotation=H(steamSeed,5)*360};steam.Emit(emit,1);}
        }
    }
}
