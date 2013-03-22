using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class LogEntry
{
    public double logTime;
    public String text;
    public CharacterAnimationState anim;
    public bool animDone;

    public LogEntry(double logTime, String text, CharacterAnimationState anim)
    {
        this.logTime = logTime;
        this.text = text;
        this.anim = anim;
        this.animDone = false;
    }
}

public class ManleyModule : PartModule
{
    //GUI related members
    private Rect requestPos, commPos;
    private KerbalInstructor portrait;
    private RenderTexture portraitTexture;
    private int portraitSize = 128;

    //Communication related members
    private List<LogEntry> log;

    private double getKspDistance()
    {
        if (Planetarium.fetch == null) { return 0; }
        CelestialBody kerbin = Planetarium.fetch.Sun.orbitingBodies[0];
        if (kerbin.name != "Kerbin") { Debug.Log("kerbin.name != Kerbin"); return 0; }
        return (kerbin.position - vessel.GetWorldPos3D()).magnitude;
    }
    //Time which it takes for a radio transmittion to travel from the ship to KSP and back.
    private double getKspRelayTime()
    {
        /*
         * Radio transmissions travel at the speed of light, which is 299,792,458m/s.
         */
        return getKspDistance() * 2 / 299792458.0;
        /*
         * Another option would be that it takes 1.25seconds for a radio transmittion to get from the Mun to Kerbal.
         * As that's what an Earth->moon transmition takes. The mun is 12,000,000m from Kerbin so,
         * this would make the speed of "radio" of about 9,600,000m/s
         */
        //return kspDistance() * 2 / 9600000.0;
        /*
         * A different version of this other option would be to see Minmus as the equivalent of the Earth moon.
         * With a distance of 47,000,000m in 1.25sec, this results in a speed of 37,600,000
         */
        //return kspDistance() * 2 / 37600000.0;

        /*
         * Another interresting number is Kerbin and Duna are 7,126,315,008m apart in the best case.
         * Compared to Earth-Mars, this takes 4.35minutes for a 1 way communication.
         * In the different KSP options:
         *  - Light speed: 23.8 seconds
         *  - Mun speed: 12.4 minutes
         *  - Minmus speed: 3.16 minutes
         * Which would make Minmus the most realistic option. But lightspeed the most playable.
         * As in worst case, Kerbin and Duna are 34,325,995,520m apart.
         *  - Light speed: 1.9 minutes
         *  - Mun speed: 1 hour
         *  - Minmus speed: 15.2 minutes
         */
    }

    public override void OnStart(StartState state)
    {
        if (portrait == null)
        {
            //There are 2 possible instuctors, "Instructor_Gene" and "Instructor_Wernher". Gene is the flight instructor and fits the most.
            // Some clever reflection tricks where used to get these names and the details no how to spawn this.
            GameObject gameObject = (GameObject)UnityEngine.Object.Instantiate(AssetBase.GetPrefab("Instructor_Gene"));
            portrait = gameObject.GetComponent<KerbalInstructor>();
            portraitTexture = new RenderTexture(this.portraitSize, this.portraitSize, 8);
            portrait.instructorCamera.targetTexture = this.portraitTexture;
            portrait.instructorCamera.ResetAspect();
        }

        if (requestPos.y == 0)
            requestPos = new Rect(0, 50, 10, 10);
        log = new List<LogEntry>();
        if (state != StartState.Editor)
            RenderingManager.AddToPostDrawQueue(3, new Callback(drawGUI));
        part.FindModelAnimators("antenna")[0].Play("antenna");
    }

    public void drawGUI()
    {
        if (vessel != FlightGlobals.ActiveVessel)
            return;

        if (log.Count > 0)
        {
            if (log[0].logTime < Planetarium.GetUniversalTime())
                commPos = GUILayout.Window(557, commPos, commGUI, "KSP Comm", GUILayout.Width(Screen.width / 3), GUILayout.Height(1));
            else
                commPos = GUILayout.Window(557, commPos, waitingForComm, "Waiting for KSP reply (" + (log[0].logTime - Planetarium.GetUniversalTime()).ToString("F0") + ")", GUILayout.Width(Screen.width / 3), GUILayout.Height(1));
        }
        else
        {
            commPos = GUILayout.Window(557, commPos, waitingForComm, "KSP reply delay: " + (getKspRelayTime()).ToString("F0"), GUILayout.Width(Screen.width / 3), GUILayout.Height(1));
        }
        commPos.x = 10;
        commPos.y = 50;

        requestPos = GUILayout.Window(556, requestPos, requestGUI, "Request", GUILayout.Width(1), GUILayout.Height(1));
        requestPos.x = Screen.width - requestPos.width;
    }

    public void waitingForComm(int windowID)
    {
        GUI.skin = HighLogic.Skin;
        GUILayout.BeginVertical();
        GUILayout.BeginHorizontal();
        GUILayout.Label(" ");
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
        GUI.DragWindow();
    }

    public void commGUI(int windowID)
    {
        GUI.skin = HighLogic.Skin;
        GUILayout.BeginVertical();
        GUILayout.BeginHorizontal();
        GUILayout.Box(portraitTexture, GUILayout.Width(portraitSize), GUILayout.Height(portraitSize));
        GUILayout.Space(5);
        GUILayout.Label(log[0].text);
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
        GUI.DragWindow();
    }

    public String formatDistance(double distance)
    {
        if (distance < 1000.0)
            return distance.ToString("F1") + "m";
        if (distance < 10000.0)
            return distance.ToString("F0") + "m";
        return (distance / 1000).ToString("F2") + "km";
    }

    public void requestGUI(int windowID)
    {
        GUI.skin = HighLogic.Skin;
        GUILayout.BeginVertical();
        if (GUILayout.Button("Altitude")) requestByID(1);
        if (GUILayout.Button("Orbit")) requestByID(2);
        GUILayout.Space(15);
        if (GUILayout.Button("Go on EVA"))
        {
            if (HighLogic.CurrentGame.Parameters.Flight.CanEVA)
            {
                Kerbal crew = null;
                foreach(ProtoCrewMember member in vessel.GetVesselCrew())
                {
                    if (member.KerbalRef != null)
                        crew = member.KerbalRef;
                }

                if (crew != null && FlightEVA.SpawnEVA(crew))
                {
                    //If we do not set the camera to flight mode when EVAing, we get some odd combination of 3th person control and 1st person camera.
                    CameraManager.Instance.SetCameraFlight();
                    crew.state = Kerbal.States.BAILED_OUT;
                }
            }
        }
        GUILayout.EndVertical();
        GUI.DragWindow();
    }

    private String getCrewName()
    {
        List<ProtoCrewMember> members = vessel.GetVesselCrew();
        if (members.Count < 1)
            return vessel.vesselName;
        return members[UnityEngine.Random.Range(0, members.Count)].name;
    }

    private void requestByID(int id)
    {
        if (UnityEngine.Random.Range(0, 100) < 5)
        {
            addLog("Sorry, we didn't quite get that.", false);
            return;
        }
        String message = "";
        switch (UnityEngine.Random.Range(0, 2))
        {
            case 0: message = "Roger " + vessel.vesselName + ",\n"; break;
            case 1: message = "Hello " + getCrewName() + ",\n"; break;
        }
        switch (id)
        {
            case 1:
                message += "Your altitude is " + formatDistance(vessel.altitude) + " over " + vessel.mainBody.theName + "\n";
                break;
            case 2:
                if (vessel.situation == Vessel.Situations.LANDED)
                {
                    addLog("You are still landed, no orbital information available.", false);
                    break;
                }
                if (vessel.situation == Vessel.Situations.PRELAUNCH)
                {
                    addLog("You are still here, right in front of us!", false);
                    return;
                }
                if (vessel.orbit.ApA > 0)
                {
                    message += "Your AP is " + formatDistance(vessel.orbit.ApA) + "\n";
                    message += "Time till AP is " + KSPUtil.PrintTime((int)Math.Abs(vessel.orbit.timeToAp), 5, false) + "\n";
                }
                if (vessel.orbit.PeA > 0)
                {
                    message += "Your PE is " + formatDistance(vessel.orbit.PeA) + "\n";
                    message += "Time till PE is " + KSPUtil.PrintTime((int)Math.Abs(vessel.orbit.timeToPe), 5, false) + "\n";
                }
                if (vessel.orbit.nextPatch != null && vessel.orbit.nextPatch.referenceBody != null)
                {
                    message += "Leaving this orbit towards " + vessel.orbit.nextPatch.referenceBody.theName + " in " + KSPUtil.PrintTime((int)Math.Abs(vessel.orbit.nextPatch.StartUT - Planetarium.GetUniversalTime()), 5, false) + "\n";
                }
                break;
        }
        addLog(message);
    }

    private void addLog(String text, bool positive = true)
    {
        CharacterAnimationState anim = portrait.anim_idle;
        if (positive)
        {
            switch (UnityEngine.Random.Range(0, 9))
            {
                case 0: anim = portrait.anim_idle_wonder; break;
                case 1: anim = portrait.anim_idle_lookAround; break;
                case 2: anim = portrait.anim_idle_sigh; break;
                case 3: anim = portrait.anim_true_nodA; break;
                case 4: anim = portrait.anim_true_nodB; break;
                case 5: anim = portrait.anim_true_smileA; break;
                case 6: anim = portrait.anim_true_smileB; break;
                case 7: anim = portrait.anim_true_thumbUp; break;
                case 8: anim = portrait.anim_true_thumbsUp; break;
            }
        }
        else
        {
            switch (UnityEngine.Random.Range(0, 7))
            {
                case 0: anim = portrait.anim_false_disagreeA; break;
                case 1: anim = portrait.anim_false_disagreeB; break;
                case 2: anim = portrait.anim_false_disagreeC; break;
                case 3: anim = portrait.anim_false_disappointed; break;
                case 4: anim = portrait.anim_false_sadA; break;
                case 5: anim = portrait.anim_idle_sigh; break;
                case 6: anim = portrait.anim_idle_wonder; break;
            }
        }
        log.Add(new LogEntry(Planetarium.GetUniversalTime() + getKspRelayTime(), text, anim));
    }

    public override void OnUpdate()
    {
        if (vessel != FlightGlobals.ActiveVessel)
            return;
        if (log.Count > 0)
        {
            if (!log[0].animDone && log[0].logTime < Planetarium.GetUniversalTime())
            {
                portrait.PlayEmote(log[0].anim);
                log[0].animDone = true;
            }
            if (log.Count > 1 && log[0].logTime + 1 < Planetarium.GetUniversalTime() && log[1].logTime < Planetarium.GetUniversalTime())
                log.RemoveAt(0);
            else if (log[0].logTime + 20 < Planetarium.GetUniversalTime())
                log.RemoveAt(0);
        }

        if (CameraManager.Instance.currentCameraMode != CameraManager.CameraMode.IVA)
        {
            //Force the camera into IVA mode. No map, no external view.
            CameraManager.Instance.SetCameraIVA();
        }
    }
}
