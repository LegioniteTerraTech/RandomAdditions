using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using TerraTechETCUtil;

public class TorpedoProjectile : RandomAdditions.TorpedoProjectile { };
namespace RandomAdditions
{
    public class TorpedoProjectile : ExtProj
    {
        // With Watermod installed, any missile that goes into the water with this projectile type
        //   will change their thrust value to the one specified here.
        /*
           "RandomAdditions.TorpedoProjectile": {
                "SubmergedThrust" : 0.5,                //Thrust to apply underwater
                "ThrustUntilProjectileDeath" : false,   //Should we thrust until we explode?
           },
            // Make sure to add a new GameObject with the name "_subProp" and position it to where the thrust should be
         */
        public const float BubblesOpacity = 0.35f;
        public const float BubblesStartSize = 0.10f;
        public const float BubblesMaxSize = 0.15f;
        public const float Brightener = 0.65f;
        public static Color waterColor = new Color(0.125f, 0.35f, 0.65f, 0.875f);
        public static Color waterColorBright = new Color(Brightener, Brightener, Brightener, 0) * (new Color(1, 1, 1, 1) - waterColor) + waterColor - new Color(0, 0, 0, 0.5f);


        public float SubmergedThrust = 0.5f;// how much force to apply when in the water

        public bool ThrustUntilProjectileDeath = false;

        public bool isSubmerged = false;

        //AutoCollection
        private bool killThrust = false;
        private Vector3 addedThrustPosition;
        private Vector3 addedThrustDirection;
        private Transform thisTrans;
        private bool runAnim = false;
        private bool runParticles = false;
        private ParticleSystem ps;
        private Spinner spin;
        private AnimetteController anim;
        private static Material bubbleMaterial;
        public override void Pool()
        {
            if (KickStart.isWaterModPresent)// don't fire if water mod is not present
            {
                DebugRandAddi.Info("RandomAdditions: Launched TorpedoProjectile on " + gameObject.name);
                thisTrans = gameObject.transform;
                var isTransformPresent = gameObject.transform.Find("_subProp");
                if (isTransformPresent)
                {
                    addedThrustPosition = isTransformPresent.transform.localPosition;
                    addedThrustDirection = thisTrans.InverseTransformDirection(isTransformPresent.transform.forward);
                    ps = isTransformPresent.GetComponent<ParticleSystem>();
                    if (ps == null)
                    {
                        if (bubbleMaterial == null)
                        {
                            Texture2D image = null;
                            foreach (var item in Resources.FindObjectsOfTypeAll<Texture2D>())
                            {
                                if (item.name == "t_sph_Liquid_oil_01")
                                    image = item;
                            }
                            //var shader = Shader.Find("Standard");
                            Shader shader = Shader.Find(name);
                            //var shader = Shader.Find("Shield");
                            //var shader = Shader.Find("Unlit/Transparent");
                            //var shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply");
                            if (shader == null)
                            {
                                IEnumerable<Shader> shaders = Resources.FindObjectsOfTypeAll<Shader>();
                                /*
                                foreach (var item in shaders)
                                {
                                    if (item && !item.name.NullOrEmpty())
                                        DebugWater.Log(item.name);
                                }
                                */
                                shaders = shaders.Where(s2 => s2.name == name); ////Standard
                                shader = shaders.ElementAt(0);
                                if (shader == null)
                                    DebugRandAddi.Log("RandomAdditions: failed to get shader");
                            }

                            if (image != null)
                            {
                                bubbleMaterial = new Material(shader)
                                {
                                    mainTexture = image,
                                    color = waterColorBright,
                                };
                            }
                        }

                        var ps2 = isTransformPresent.gameObject.AddComponent<ParticleSystem>();

                        var m = ps2.main;
                        m.simulationSpace = ParticleSystemSimulationSpace.World;
                        m.startLifetime = 16f;
                        m.startSize = BubblesStartSize;
                        m.startSize3D = false;
                        m.playOnAwake = false;
                        m.maxParticles = 500;
                        m.startSpeed = 6f;
                        m.loop = true;

                        var e = ps2.emission;
                        e.rateOverTime = 16f;
                        e.rateOverDistance = 2.5f;

                        var s = ps2.shape;
                        s.shapeType = ParticleSystemShapeType.Cone;
                        //s.shapeType = ParticleSystemShapeType.Circle;
                        s.angle = 0f;
                        s.radius = 1f;

                        var c = ps2.colorOverLifetime;
                        c.enabled = true;
                        c.color = new Color(0.561f, 0.937f, 0.875f, BubblesOpacity);


                        var r = ps2.GetComponent<ParticleSystemRenderer>();
                        r.renderMode = ParticleSystemRenderMode.Billboard;
                        r.material = bubbleMaterial;
                        r.maxParticleSize = BubblesMaxSize;

                        ps = ps2;
                        ps2.Stop();
                    }
                    if (ps)
                        ps.Clear(false);
                    //DebugRandAddi.Log("RandomAdditions: Projectile " + gameObject.name + " Thrust is " + addedThrustDirection + " | and position is " + addedThrustPosition);
                }
                else
                {
                    addedThrustPosition = Vector3.zero;
                    addedThrustDirection = Vector3.forward;
                    DebugRandAddi.Log("RandomAdditions: Projectile " + gameObject.name + " does not have any previous effectors or thrust transforms!  Defaulting to the center of the projectile!  \nAdd a \"_subProp\" to your projectile's JSON!");
                }
                spin = GetComponent<Spinner>();
                anim = KickStart.FetchAnimette(transform, "_subProp", AnimCondition.TorpedoProjectile);
            }
        }
        public override void Fire(FireData fireData)
        {
            killThrust = false;
            SetAnimation(true);
        }
        public void KillSubmergedThrust()
        {
            killThrust = true;
        }
        private void FixedUpdate()
        {
            if (KickStart.isWaterModPresent)// don't fire if water mod is not present
            {
                if (gameObject.transform.position.y <= KickStart.WaterHeight)
                {
                    isSubmerged = true;
                    if (ThrustUntilProjectileDeath || !killThrust)
                    {
                        //DebugRandAddi.Log("RandomAdditions: Projectile " + gameObject.name + " is thrusting submerged!");
                        PB.rbody.AddForceAtPosition(thisTrans.TransformDirection(addedThrustDirection.normalized * SubmergedThrust), thisTrans.TransformPoint(addedThrustPosition));
                        SetParticles(true);
                    }
                    else
                    {
                        SetAnimation(false);
                        SetParticles(false);
                    }
                }
                else
                {
                    isSubmerged = false;
                    SetParticles(false);
                }
            }
        }

        private void SetAnimation(bool run)
        {
            if (run != runAnim)
            {
                runAnim = run;
                if (run)
                {
                    if (spin)
                        spin.SetAutoSpin(true);
                    if (anim)
                        anim.Run();
                }
                else
                {
                    if (spin)
                        spin.SetAutoSpin(false);
                    if (anim)
                        anim.Stop();
                }
            }
        }

        private void SetParticles(bool run)
        {
            if (run != runParticles)
            {
                runParticles = run;
                if (run)
                {
                    if (ps && !ps.isPlaying)
                        ps.Play(false);
                }
                else
                {
                    if (ps && ps.isPlaying)
                        ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }
    }
}
