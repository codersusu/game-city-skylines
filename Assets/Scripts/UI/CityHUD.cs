using System;
using System.Collections.Generic;
using UnityEngine;

namespace Seabright
{
    public class CityHUD : MonoBehaviour
    {
        public CityGame Game;
        public static readonly Color Teal = new Color(.43f,.88f,.75f);
        static readonly Color Ink = new Color(.045f,.087f,.11f,.96f), PanelColor = new Color(.07f,.12f,.145f,.95f), Muted = new Color(.56f,.67f,.7f), White = new Color(.93f,.96f,.94f), Gold = new Color(.95f,.74f,.42f);
        Font regular,bold;
        Texture2D mini;
        readonly Dictionary<int,Texture2D> iconTextures=new Dictionary<int,Texture2D>();
        bool confirmNewCity, soundSettings;
        GUIStyle sliderTrack, sliderThumb;
        int speedBeforeModal;
        string modalSaveError;
        public bool ModalOpen => confirmNewCity || soundSettings;
        GUIStyle text;
        float scale,W,H,nextMap;
        string tooltip;
        readonly string[] layerNames={"Natural view","Zoning","Electricity","Water supply","Traffic flow","Land value"};

        void Awake() { regular=Resources.Load<Font>("Interface");bold=Resources.Load<Font>("InterfaceBold"); mini=new Texture2D(48,48,TextureFormat.RGBA32,false){filterMode=FilterMode.Point}; }
        void Layout() { scale=Mathf.Min(Screen.width/1600f,Screen.height/960f);W=Screen.width/scale;H=Screen.height/scale; }
        public bool PointerOverUI(Vector3 mouse) {
            Layout();if(Game.Photo)return false;if(Game.Help||ModalOpen)return true;
            Vector2 p=new Vector2(mouse.x/scale,(Screen.height-mouse.y)/scale);
            if(Game.Speed==0&&new Rect(W/2-175,108,350,88).Contains(p))return true;
            if(Game.Overlay>0&&new Rect(W-307,Game.Selected.x>=0?560:414,291,164).Contains(p))return true;
            if(Time.unscaledTime<Game.NoticeUntil&&new Rect(W/2-375,108,750,42).Contains(p))return true;
            if(new Rect(W-156,H-158,134,27).Contains(p))return true;
            if(p.y<108 || p.y>H-143 || ((Game.Tool!=TileKind.Empty||Game.Demolish)&&new Rect(W/2-395,H-181,790,45).Contains(p)) || new Rect(16,112,268,526).Contains(p) || new Rect(16,H-328,222,183).Contains(p) || new Rect(W-84,110,70,300).Contains(p))return true;
            return Game.Selected.x>=0&&new Rect(W-342,110,252,452).Contains(p);
        }
        void OnGUI() {
            if(Game==null||Game.Sim==null)return;Layout();GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
            if(Game.InputDiagnostics&&(Event.current.type==EventType.MouseDown||Event.current.type==EventType.MouseUp)) Debug.Log("SEABRIGHT_GUI_POINTER "+Event.current.type+" pos="+Event.current.mousePosition+" input="+Input.mousePosition+" scale="+scale+" screen="+Screen.width+"x"+Screen.height);
            if(text==null)text=new GUIStyle(GUI.skin.label){font=regular,padding=new RectOffset(0,0,0,0)};
            tooltip=null;
            if(Game.Photo) { Label(new Rect(30,H-60,300,30),"SEABRIGHT",21,White,true);Label(new Rect(W-245,H-48,220,22),"F  Return to city management",12,White);return; }
            if(Game.Sim.Population>=600) {
                DrawDistrictLabel("MARINA QUARTER",CitySimulation.World(33,20)+Vector3.up*7);
                DrawDistrictLabel("CEDAR HEIGHTS",CitySimulation.World(15,31)+Vector3.up*14);
            }
            bool modal = Game.Help||ModalOpen;
            GUI.enabled = !modal;
            Header();
            if(Game.Budget) BudgetPanel();else CityPanel();
            MapPanel();Layers();Toolbar();
            if(Game.Selected.x>=0)Inspector();
            GUI.enabled = true;
            if(Game.Help)HelpPanel();
            if(confirmNewCity)NewCityPanel();
            if(soundSettings)SoundPanel();
            if(Game.Speed==0&&!modal) {
                float py=Time.unscaledTime<Game.NoticeUntil?156:110;
                Rect pause=new Rect(W/2-169,py,338,34);
                Panel(pause,new Color(.24f,.17f,.075f,.98f));
                Label(new Rect(pause.x,pause.y+10,pause.width,20),"PAUSED   ·   CLICK TO RESUME  1×",11,Gold,true,TextAnchor.UpperCenter);
                if(InvisibleButton(pause,"Resume city growth, cars and pedestrians · Space"))Game.Speed=1;
            }
            if(Time.unscaledTime<Game.NoticeUntil&&!modal) {
                float tw=Mathf.Min(740,Game.Notice.Length*6.8f+52);float ty=108;
                Panel(new Rect(W/2-tw/2,ty,tw,39),Ink);
                Dot(new Vector2(W/2-tw/2+17,ty+19),5,Teal);
                Label(new Rect(W/2-tw/2+32,ty+11,tw-42,22),Game.Notice,12,White);
            }
            if(tooltip!=null&&!modal) {
                float tw=Mathf.Min(600,tooltip.Length*6.5f+26);Vector2 p=Event.current.mousePosition;
                float tx=Mathf.Clamp(p.x-tw/2,12,W-tw-12),ty=Mathf.Min(p.y-42,H-160);
                Panel(new Rect(tx,ty,tw,31),Ink);Label(new Rect(tx+12,ty+8,tw-24,20),tooltip,11,White);
            }
            RectFill(new Rect(0,H-29,W,29),new Color(.04f,.08f,.1f,.9f));
            Label(new Rect(23,H-21,510,20),"SEABRIGHT  /  A city in balance                 UNITY 6  ·  PLAYABLE DEMO",10,Muted);
            Label(new Rect(W-616,H-21,592,20),"W A S D  Move     Q E  Orbit     Scroll  Zoom     Space  Pause     F  Photo",10,Muted,false,TextAnchor.UpperRight);
        }

        void Header() {
            Panel(new Rect(22,20,W-44,76),Ink);
            Icon(0,new Rect(43,39,35,36),Teal);
            Label(new Rect(93,34,220,29),"SEABRIGHT",24,White,true);
            Label(new Rect(95,65,230,18),"COASTAL CITY   /   REGION 01",9,Muted,true);
            Sep(313,36,43);
            Metric(338,"TREASURY","$"+Game.Sim.Money.ToString("N0"),Game.Sim.Income-Game.Sim.Expenses>=0?"+ $"+(Game.Sim.Income-Game.Sim.Expenses).ToString("N0")+" / day":"− $"+(Game.Sim.Expenses-Game.Sim.Income).ToString("N0")+" / day",Game.Sim.Income>=Game.Sim.Expenses?Teal:Gold,174);
            if(InvisibleButton(new Rect(328,28,194,60),"Open the city budget and tax policy")) Game.Budget=!Game.Budget;
            Metric(545,"POPULATION",Game.Sim.Population.ToString("N0"),"citizens call this home",Muted,160);
            Metric(732,"HAPPINESS",Game.Sim.Happiness.ToString("0")+"%",Game.Sim.Happiness>75?"Life is good here":"Residents need attention",Teal,158);
            Metric(919,"TRAFFIC FLOW",Game.Sim.TrafficFlow.ToString("0")+"%",Game.Sim.TrafficFlow>70?"Moving comfortably":"Roads under pressure",Teal,158);
            float rx=W-463;Sep(rx,36,43);
            Label(new Rect(rx+24,35,130,18),"DAY "+Game.Sim.Day.ToString("000"),12,White,true);
            Label(new Rect(rx+24,57,130,18),Game.Speed==0?"PAUSED · PRESS SPACE":Game.Night?"21:40  ·  BLUE HOUR":"16:20  ·  CLEAR SKY",9,Game.Speed==0?Gold:Muted);
            for(int i=0;i<3;i++) {int v=i==0?0:i==1?1:3;Rect r=new Rect(rx+146+i*36,37,31,35);if(Button(r,i==0?"Ⅱ":i==1?"1×":"3×",Game.Speed==v,i==0?"Pause city growth and all traffic":i==1?"Run simulation at normal speed":"Run simulation 3 times faster",12))Game.Speed=v;}
            if(Button(new Rect(W-177,37,40,35),Game.Night?"DAY":"NIGHT",false,"Toggle day / night · N",9))Game.ToggleNight();
            if(Button(new Rect(W-129,37,40,35),"SAVE",false,"Save city · F5",9))Game.Save();
            if(Button(new Rect(W-81,37,37,35),"?",Game.Help,"Controls and how to play",17))Game.Help=!Game.Help;
        }
        void Metric(float x,string name,string value,string detail,Color color,float width) {
            Label(new Rect(x,31,width,17),name,9,Muted,true);Label(new Rect(x,46,width,27),value,21,White,true);Label(new Rect(x,74,width,15),detail,9,color);
        }
        void CityPanel() {
            var s=Game.Sim;
            Panel(new Rect(22,118,250,348),PanelColor);
            Label(new Rect(41,138,210,20),"CITY PULSE",11,White,true);
            Label(new Rect(41,169,212,30),s.Population<150?"Small beginnings.":s.Population<600?"A city taking shape.":"Your own skyline.",21,White,true);
            Label(new Rect(41,208,212,47),"Extend a street. Paint homes and jobs.\nKeep time running and services supplied\nto turn empty zones into a neighborhood.",11,Muted);
            Rule(41,270,212);
            Label(new Rect(41,285,204,20),"DEVELOPMENT DEMAND",9,Muted,true);
            Demand(41,310,"Homes",s.ResidentialDemand,CategoryColor(TileKind.Residential));
            Demand(41,343,"Commerce",s.CommercialDemand,CategoryColor(TileKind.Commercial));
            Demand(41,376,"Industry",s.IndustrialDemand,CategoryColor(TileKind.Industrial));
            int noRoad=0,noPower=0,noWater=0,building=0;
            foreach(var t in s.Tiles)if(CitySimulation.IsZone(t.Kind)) {
                if(!t.Connected)noRoad++;else if(!t.Powered)noPower++;else if(!t.Watered)noWater++;else if(t.Level==0)building++;
            }
            string alert=noRoad>0?noRoad+" zones need a connected roadside.":noPower>0?noPower+" zones need electricity.":noWater>0?noWater+" zones need water.":Game.Speed==0?"Paused: growth and all traffic are stopped.":building>0?building+" supplied zones are developing.":"Build connected homes and jobs to grow.";
            Wrapped(new Rect(41,414,214,41),alert,10,noRoad+noPower+noWater>0||Game.Speed==0?Gold:Teal);
            Panel(new Rect(22,480,250,151),PanelColor);
            TileKind next=!s.IsUnlocked(TileKind.Office)?TileKind.Office:!s.IsUnlocked(TileKind.HighResidential)?TileKind.HighResidential:TileKind.Stadium;
            bool complete=s.IsUnlocked(TileKind.Stadium);
            int target=s.UnlockRequirement(next);
            Label(new Rect(41,498,212,19),complete?"CITY MILESTONES COMPLETE":"YOUR NEXT MILESTONE",9,Teal,true);
            Icon(complete?13:next==TileKind.Office?12:next==TileKind.HighResidential?11:13,new Rect(41,526,27,29),CategoryColor(next));
            Label(new Rect(79,526,174,26),complete?"A thriving city":next==TileKind.Office?"Office district":next==TileKind.HighResidential?"Modern towers":"Coastal stadium",18,White,true);
            Label(new Rect(41,565,212,19),complete?s.PeakPopulation.ToString("N0")+" citizens reached":s.PeakPopulation.ToString("N0")+" / "+target.ToString("N0")+" citizens reached",11,Muted);
            Bar(new Rect(41,590,212,6),complete?1:(float)s.PeakPopulation/Mathf.Max(1,target),CategoryColor(next));
            Label(new Rect(41,609,212,17),complete?"All facilities available in the toolbar.":"New buildings + a city development grant",10,Muted);
        }
        void BudgetPanel() {
            var s=Game.Sim;Panel(new Rect(22,118,250,410),PanelColor);
            Label(new Rect(41,138,205,25),"CITY BUDGET",12,White,true);
            Label(new Rect(41,180,212,18),"DAILY BALANCE",9,Muted,true);
            Label(new Rect(41,201,212,34),(s.Income>=s.Expenses?"+ $":"− $")+Mathf.Abs(s.Income-s.Expenses).ToString("N0"),27,Teal,true);
            BudgetRow(41,257,"Tax revenue","$"+s.Income.ToString("N0"),Teal);
            BudgetRow(41,289,"City operations","− $"+s.Expenses.ToString("N0"),Gold);
            Rule(41,330,211);Label(new Rect(41,349,130,20),"TAX POLICY",9,Muted,true);
            Label(new Rect(183,345,70,24),s.TaxRate.ToString("0")+"%",20,White,true,TextAnchor.UpperRight);
            if(Button(new Rect(41,382,34,28),"−",false,"Reduce tax rate")) {s.TaxRate=Mathf.Max(5,s.TaxRate-1);s.Recalculate();}
            Bar(new Rect(87,393,122,5),(s.TaxRate-5)/15,Teal);
            if(Button(new Rect(219,382,34,28),"+",false,"Increase tax rate")) {s.TaxRate=Mathf.Min(20,s.TaxRate+1);s.Recalculate();}
            Label(new Rect(41,434,210,44),"Lower taxes attract residents.\nHigher taxes fund city services.",12,Muted);
            if(Button(new Rect(41,485,211,27),"Back to city pulse",false,"Close budget",11))Game.Budget=false;
        }
        void BudgetRow(float x,float y,string label,string amount,Color c) {Label(new Rect(x,y,130,20),label,12,Muted);Label(new Rect(x+120,y,92,20),amount,13,c,true,TextAnchor.UpperRight);}
        void Demand(float x,float y,string name,float v,Color c) {Dot(new Vector2(x+4,y+6),4,c);Label(new Rect(x+15,y-1,110,18),name,12,White);Bar(new Rect(x+112,y+3,77,6),v/100,c);Label(new Rect(x+190,y-2,24,19),Mathf.RoundToInt(v).ToString(),10,Muted,false,TextAnchor.UpperRight);}
        void Objective(float x,float y,string label,bool done) {Panel(new Rect(x,y-2,13,13),done?Teal:new Color(.24f,.33f,.35f));if(done)Label(new Rect(x,y-3,13,15),"✓",10,Ink,true,TextAnchor.UpperCenter);Label(new Rect(x+23,y-2,198,20),label,11,done?White:Muted);}

        void Layers() {
            float x=W-77;Panel(new Rect(x,118,55,285),PanelColor);Label(new Rect(x,133,55,15),"VIEWS",8,Muted,true,TextAnchor.UpperCenter);
            for(int i=0;i<6;i++){Rect r=new Rect(x+8,156+i*39,39,34);if(IconButton(r,20+i,Game.Overlay==i,layerNames[i]))Game.SetOverlay(i);}
            if(Game.Overlay>0) {
                bool supply=Game.Overlay==2||Game.Overlay==3;
                float ly=Game.Selected.x>=0?566:420;
                Panel(new Rect(W-301,ly,279,supply?151:87),Ink);Label(new Rect(W-283,ly+17,245,20),layerNames[Game.Overlay].ToUpperInvariant(),10,White,true);
                string legend=Game.Overlay==1?"Homes / shops / industry / towers / offices":Game.Overlay==2?"Mint = supplied    Coral = no power":Game.Overlay==3?"Blue = supplied    Coral = no water":Game.Overlay==4?"Mint = smooth    Amber = pressure":"Mint = desirable    Amber = lower value";
                Wrapped(new Rect(W-283,ly+44,245,36),legend,11,Muted);
                if(supply) {
                    float used=Game.Overlay==2?Game.Sim.PowerUse:Game.Sim.WaterUse,capacity=Game.Overlay==2?Game.Sim.PowerCapacity:Game.Sim.WaterCapacity;
                    Label(new Rect(W-283,ly+78,245,22),used.ToString("N0")+" / "+capacity.ToString("N0")+"  capacity used",12,White,true);
                    Bar(new Rect(W-283,ly+109,242,7),capacity>0?used/capacity:1,used<=capacity?Teal:Gold);
                    Label(new Rect(W-283,ly+129,245,18),"Road-connected stations supply nearby lots.",10,Muted);
                }
            }
        }
        void Toolbar() {
            const float width=1134,slot=85;
            float x=(W-width)/2,y=H-132;
            Panel(new Rect(x,y,width,89),Ink);
            TileKind[] kinds={TileKind.Empty,TileKind.Road,TileKind.Residential,TileKind.Commercial,TileKind.Industrial,TileKind.Park,TileKind.Power,TileKind.Water,TileKind.Clinic,TileKind.HighResidential,TileKind.Office,TileKind.Stadium,TileKind.Empty};
            string[] names={"Inspect","Roads","Homes","Shops","Industry","Parks","Power","Water","Health","Towers","Offices","Stadium","Bulldoze"};
            int[] icons={1,2,3,4,5,6,7,8,9,11,12,13,10};
            for(int i=0;i<kinds.Length;i++) {
                float bx=x+15+i*slot;Rect r=new Rect(bx,y+7,79,75);
                bool locked=!Game.Sim.IsUnlocked(kinds[i]);
                bool active=i==12?Game.Demolish:!Game.Demolish&&Game.Tool==kinds[i]&&i!=12;
                bool hover=r.Contains(Event.current.mousePosition);
                if(active||hover)Panel(r,active?new Color(.16f,.3f,.28f):new Color(.14f,.21f,.24f));
                Color color=i==0?White:i==12?new Color(.98f,.58f,.44f):CategoryColor(kinds[i]);
                Color badge=new Color(color.r*.15f+.035f,color.g*.15f+.05f,color.b*.15f+.06f,1);
                Panel(new Rect(bx+19,y+12,41,36),badge);
                // Explicit draw colors keep glyphs visible in a linear-color-space player.
                // Locked tools retain their category hue and are identified by a population requirement.
                Icon(icons[i],new Rect(bx+26,y+17,27,27),color);
                if(locked) {Panel(new Rect(bx+54,y+10,15,15),Ink);Icon(14,new Rect(bx+56,y+12,11,11),Gold);}
                Label(new Rect(bx,y+53,79,19),names[i],11,locked?Muted:active?Teal:White,true,TextAnchor.UpperCenter);
                string sub=locked?Game.Sim.UnlockRequirement(kinds[i]).ToString("N0")+" people":i==0?"SELECT":i==12?"REMOVE":i<5?"PAINT / BUILD":"BUILD";
                Label(new Rect(bx,y+69,79,14),sub,8,locked?Gold:Muted,false,TextAnchor.UpperCenter);
                if(active)RectFill(new Rect(bx+23,y+2,33,2),color);
                string tip=locked?names[i]+" unlock at "+Game.Sim.UnlockRequirement(kinds[i]).ToString("N0")+" residents":names[i]+(i==0?" · Click a building or zone":i==12?" · B · Remove structures":" · $"+Game.Sim.Cost(kinds[i]).ToString("N0")+(i==9||i==10?" · Zone beside a supplied road":i==11?" · Needs a clear 3 × 3 site":""));
                if(InvisibleButton(r,tip)) {
                    if(locked) {Game.Toast(tip+". Grow serviced homes and jobs to reach the milestone.");Game.Audio.Play(CityAudio.Cue.Denied);}
                    else {Game.SetTool(kinds[i]);if(i==12)Game.Demolish=true;}
                }
            }
            if(Game.Tool!=TileKind.Empty||Game.Demolish) {
                string hint=Game.Demolish?"BULLDOZE   ·   Click to remove   ·   Right-click to cancel":Game.Tool==TileKind.Road?"ROADS   ·   Drag from an existing street   ·   $"+Game.Sim.Cost(TileKind.Road).ToString("0")+" / segment":Game.Tool==TileKind.Stadium?"STADIUM   ·   Click the center of a clear 3 × 3 site near a road":CitySimulation.IsZone(Game.Tool)?ToolName(Game.Tool).ToUpperInvariant()+"   ·   Paint beside a road   ·   Supplied zones grow while time runs":ToolName(Game.Tool).ToUpperInvariant()+"   ·   Place beside a connected road   ·   Esc to cancel";
                Panel(new Rect(W/2-390,y-45,780,34),Ink);Label(new Rect(W/2-375,y-34,750,20),hint,11,Teal,false,TextAnchor.UpperCenter);
            }
            if(Button(new Rect(W-156,H-157,134,25),Game.Audio.Muted?"SOUND  /  MUTED":"SOUND & MUSIC",false,"Music, ambience and effects volume · M to mute",10))OpenSoundSettings();
            if(Button(new Rect(W-156,H-126,134,25),"NEW CITY",false,"Begin a new settlement · Current city can be saved first",10)) {speedBeforeModal=Game.Speed;Game.Speed=0;modalSaveError=null;confirmNewCity=true;}
            if(Button(new Rect(W-156,H-96,62,24),"PHOTO",false,"Hide interface · F",9))Game.Photo=true;
            if(Button(new Rect(W-87,H-96,65,24),"HOME",false,"Reset camera · Home",9))Game.Rig.ResetView();
            if(Button(new Rect(W-156,H-67,134,24),"LOAD CITY",false,"Restore saved city · F9",9))Game.Load();
        }
        void MapPanel() {
            float y=H-310;Panel(new Rect(22,y,212,164),PanelColor);
            Label(new Rect(36,y+13,180,17),"SEABRIGHT BAY",9,White,true);
            if(Time.unscaledTime>nextMap) {
                nextMap=Time.unscaledTime+2;
                Color[] pixels=new Color[48*48];
                for(int z=0;z<48;z++)for(int x=0;x<48;x++) {var t=Game.Sim.Get(x,z);Color c=!CitySimulation.IsLand(x,z)?new Color(.11f,.3f,.37f):new Color(.27f,.37f,.3f);if(t!=null&&t.Kind!=TileKind.Empty)c=t.Kind==TileKind.Road?new Color(.64f,.72f,.68f):CategoryColor(t.Kind);pixels[z*48+x]=c;}
                mini.SetPixels(pixels);mini.Apply();
            }
            Rect mr=new Rect(35,y+39,186,110);GUI.DrawTexture(mr,mini);
            Vector3 f=Game.Rig.Focus;Vector2 p=new Vector2(mr.x+(f.x/480+.5f)*mr.width,mr.y+(1-(f.z/480+.5f))*mr.height);Dot(p,4,White);
            Label(new Rect(201,y+44,17,20),"N",10,White,true);
            if(InvisibleButton(mr,"Click the map to move your camera")) {var mp=Event.current.mousePosition;Game.Rig.Focus=new Vector3(((mp.x-mr.x)/mr.width-.5f)*480,0,(.5f-(mp.y-mr.y)/mr.height)*480);}
        }
        void Inspector() {
            var t=Game.Sim.Get(Game.Selected.x,Game.Selected.y);if(t==null)return;
            t=Game.Sim.GetAnchor(t);if(t==null)return;
            float x=W-342;Panel(new Rect(x,118,251,431),PanelColor);
            Label(new Rect(x+19,138,190,20),"PROPERTY DETAILS",9,Muted,true);
            if(Button(new Rect(x+209,129,28,27),"×",false,"Close inspector",16))Game.Selected=new Vector2Int(-1,-1);
            Label(new Rect(x+19,174,213,34),t.Kind==TileKind.Empty?"Open land":ToolName(t.Kind),21,White,true);
            Label(new Rect(x+19,211,212,23),"BLOCK "+t.X.ToString("00")+" · "+t.Z.ToString("00")+(t.Level>0?"   /   LEVEL "+t.Level:"   /   UNDEVELOPED"),9,Muted,true);
            Rule(x+19,242,212);
            Stat(x+19,259,"Residents",t.Residents.ToString("N0"));Stat(x+19,285,"Jobs",t.Jobs.ToString("N0"));
            Stat(x+19,311,"Road access",t.Connected?"Connected":"Disconnected",t.Connected?Teal:Gold);
            Stat(x+19,337,"Electricity",t.Powered?"Supplied":"No supply",t.Powered?Teal:Gold);
            Stat(x+19,363,"Water",t.Watered?"Supplied":"No supply",t.Watered?Teal:Gold);
            Stat(x+19,389,"Land value",t.LandValue.ToString("0")+" / 100");
            Rule(x+19,420,212);
            bool zone=CitySimulation.IsZone(t.Kind);
            string state=!zone?(t.Kind==TileKind.Empty?"READY FOR YOUR NEXT IDEA":"CITY INFRASTRUCTURE"):!t.Connected?"WAITING FOR ROAD ACCESS":!t.Powered?"WAITING FOR ELECTRICITY":!t.Watered?"WAITING FOR WATER":Game.Speed==0?"PAUSED · RESUME TIME TO GROW":t.Level==0?"CONSTRUCTION  "+Mathf.RoundToInt(t.Growth*100)+"%":"ESTABLISHED · DEMAND DRIVES GROWTH";
            Label(new Rect(x+19,438,216,20),state,9,zone?CategoryColor(t.Kind):Teal,true);
            if(zone&&t.Level==0) {
                Bar(new Rect(x+19,464,212,6),t.Growth,CategoryColor(t.Kind));
                string reason=!t.Connected?"Extend a road from the regional connection.":!t.Powered||!t.Watered?"Use the supply views to find the gap.":Game.Speed==0?"Click 1× or 3× to start construction.":"Demand brings builders, then new occupants.";
                Wrapped(new Rect(x+19,483,217,49),reason,10,Muted);
            } else Label(new Rect(x+19,474,217,44),zone?"Jobs, parks and reliable services attract\nresidents and support denser development.":t.Kind==TileKind.Stadium?"A regional destination that brings visitors\nand raises the appeal of nearby properties.":"Connected roads carry traffic and distribute\nservices to the surrounding neighborhood.",11,Muted);
        }
        void Stat(float x,float y,string key,string value,Color? c=null) {Label(new Rect(x,y,105,20),key,11,Muted);Label(new Rect(x+96,y,106,20),value,11,c??White,true,TextAnchor.UpperRight);}
        void HelpPanel() {
            RectFill(new Rect(0,0,W,H),new Color(.01f,.025f,.04f,.65f));float x=W/2-300,y=H/2-255;
            Panel(new Rect(x,y,600,510),Ink);Label(new Rect(x+35,y+29,500,20),"WELCOME TO SEABRIGHT",10,Teal,true);
            Label(new Rect(x+35,y+64,530,49),"A city is a living thing.",31,White,true);
            Label(new Rect(x+35,y+126,520,53),"Start with a coastal village. Extend streets and zone homes, shops\nand industry. Grow your population to unlock a modern skyline.",15,Muted);
            Rule(x+35,y+201,530);
            string[] a={"MOVE & EXPLORE","BUILD & MANAGE","READ YOUR CITY","KEEP YOUR PROGRESS"};
            string[] b={"WASD / arrows · Q E orbit · scroll zoom · middle-drag tilt","1 roads · 2 homes · 3 shops · 4 industry · 5 parks · B bulldoze","Supply views reveal gaps. Inspect empty zones for construction progress.","F5 save · F9 load · Space pause · N night · F photo · M mute"};
            for(int i=0;i<4;i++){Label(new Rect(x+35,y+222+i*52,530,20),a[i],9,Teal,true);Label(new Rect(x+35,y+241+i*52,530,23),b[i],12,White);}
            if(Button(new Rect(x+35,y+448,530,36),"Start growing Seabright",true,"Start exploring",13))Game.Help=false;
        }
        void DrawDistrictLabel(string name,Vector3 world) {Vector3 p=Game.Cam.WorldToScreenPoint(world);if(p.z<=0)return;float x=p.x/scale,y=(Screen.height-p.y)/scale;if(x<305||x>W-340||y<170||y>H-180)return;Label(new Rect(x-101,y+1,204,21),name,10,new Color(0,0,0,.65f),true,TextAnchor.UpperCenter);Label(new Rect(x-102,y,204,21),name,10,new Color(1,1,1,.84f),true,TextAnchor.UpperCenter);}
        public static string ToolName(TileKind k) {return k==TileKind.Residential?"Family homes":k==TileKind.Commercial?"Shops & cafes":k==TileKind.Industrial?"Industry":k==TileKind.HighResidential?"Residential towers":k==TileKind.Office?"Office district":k==TileKind.Stadium?"Coastal stadium":k==TileKind.Power?"Solar station":k==TileKind.Water?"Waterworks":k==TileKind.Clinic?"Health clinic":k==TileKind.Park?"Neighborhood park":k==TileKind.Road?"City street":"Inspect";}
        static Color CategoryColor(TileKind k) {
            switch(k) {
                case TileKind.Residential:return new Color(.48f,.89f,.59f);
                case TileKind.Commercial:return new Color(.37f,.74f,1f);
                case TileKind.Industrial:return new Color(1f,.73f,.32f);
                case TileKind.HighResidential:return new Color(.77f,.63f,1f);
                case TileKind.Office:return new Color(.3f,.89f,.91f);
                case TileKind.Stadium:return new Color(1f,.55f,.43f);
                case TileKind.Park:return new Color(.61f,.87f,.43f);
                case TileKind.Power:return new Color(1f,.87f,.43f);
                case TileKind.Water:return new Color(.36f,.77f,1f);
                case TileKind.Clinic:return new Color(1f,.61f,.7f);
                default:return White;
            }
        }
        public void CloseModal() {soundSettings=false;if(!confirmNewCity)return;confirmNewCity=false;Game.Speed=speedBeforeModal;}
        public void OpenSoundSettings() {if(confirmNewCity)return;Game.Help=false;soundSettings=true;}
        void SoundPanel() {
            RectFill(new Rect(0,0,W,H),new Color(.01f,.025f,.04f,.72f));float x=W/2-280,y=H/2-224;
            Panel(new Rect(x,y,560,448),Ink);
            Label(new Rect(x+32,y+25,440,20),"THE SOUND OF SEABRIGHT",10,Teal,true);
            Label(new Rect(x+32,y+60,450,42),"Sound & music",29,White,true);
            Label(new Rect(x+32,y+107,490,24),"Find your balance of music, nature and city life.",13,Muted);
            if(Button(new Rect(x+495,y+24,33,30),"×",false,"Close sound settings · Esc",20))CloseModal();
            Rule(x+32,y+145,496);
            var a=Game.Audio;
            float master=SoundSlider(x+32,y+168,"Master volume",a.Master);
            float music=SoundSlider(x+32,y+212,"Music",a.Music);
            float ambience=SoundSlider(x+32,y+256,"City ambience",a.Ambience);
            float effects=SoundSlider(x+32,y+300,"Sound effects",a.Effects);
            a.SetVolumes(master,music,ambience,effects);
            if(Button(new Rect(x+32,y+357,150,36),a.Muted?"UNMUTE  ·  M":"MUTE  ·  M",a.Muted,"Toggle all city audio",12))a.SetMuted(!a.Muted);
            if(Button(new Rect(x+194,y+357,172,36),"TEST EFFECT",false,"Play a construction sound at your chosen volume",11))a.Play(CityAudio.Cue.Facility);
            if(Button(new Rect(x+378,y+357,150,36),"RESET LEVELS",false,"Restore the recommended sound mix",11))a.RestoreDefaults();
            Label(new Rect(x+32,y+411,496,20),a.Muted?"SOUND IS MUTED  ·  Unmute to hear your changes.":"Saved automatically  ·  Music and nature continue while paused.",11,a.Muted?Gold:Muted);
        }
        float SoundSlider(float x,float y,string label,float value) {
            if(sliderTrack==null) {
                sliderTrack=new GUIStyle(GUIStyle.none){fixedHeight=24,padding=new RectOffset(8,8,0,0)};
                sliderThumb=new GUIStyle(GUIStyle.none){fixedWidth=16,fixedHeight=24};
            }
            Label(new Rect(x,y+3,130,23),label,13,White);
            Rect track=new Rect(x+142,y,305,24);
            float result=GUI.HorizontalSlider(track,value,0,1,sliderTrack,sliderThumb);
            Bar(new Rect(track.x+8,y+10,track.width-16,4),result,Game.Audio.Muted?Muted:Teal);
            Dot(new Vector2(track.x+8+(track.width-16)*result,y+12),7,Game.Audio.Muted?Muted:White);
            Label(new Rect(x+458,y+3,38,22),Mathf.RoundToInt(result*100)+"%",12,Muted,true,TextAnchor.UpperRight);
            return result;
        }
        void NewCityPanel() {
            RectFill(new Rect(0,0,W,H),new Color(.01f,.025f,.04f,.72f));float x=W/2-300,y=H/2-165;
            Panel(new Rect(x,y,600,330),Ink);
            Label(new Rect(x+32,y+27,536,20),"A NEW BEGINNING",10,Teal,true);
            Label(new Rect(x+32,y+62,536,40),"Build a city from the ground up.",27,White,true);
            Label(new Rect(x+32,y+117,536,62),"Begin with a small village, an entry road and essential utilities.\nThe current session will be replaced. Save it first to keep your\nprogress, or start fresh and leave your existing save untouched.",13,Muted);
            Rule(x+32,y+205,536);
            if(Button(new Rect(x+32,y+232,170,40),"SAVE & START",true,"Save this city, then begin a new settlement",12)) {
                if(Game.Save()) {confirmNewCity=false;Game.NewCity();}
                else modalSaveError=Game.Notice;
            }
            if(Button(new Rect(x+215,y+232,170,40),"START FRESH",false,"Replace the current session; existing save file remains available",12)) {confirmNewCity=false;Game.NewCity();}
            if(Button(new Rect(x+398,y+232,170,40),"CANCEL",false,"Return to your city",12))CloseModal();
            Label(new Rect(x+32,y+291,536,18),modalSaveError??"Load your saved city at any time with LOAD CITY or F9.",11,modalSaveError==null?Muted:Gold);
        }

        void Label(Rect r,string s,int size,Color c,bool heavy=false,TextAnchor align=TextAnchor.UpperLeft) {text.font=heavy?bold:regular;text.fontSize=size;text.normal.textColor=c;text.alignment=align;text.wordWrap=false;GUI.Label(r,s,text);}
        void Wrapped(Rect r,string s,int size,Color c) {text.font=regular;text.fontSize=size;text.normal.textColor=c;text.alignment=TextAnchor.UpperLeft;text.wordWrap=true;GUI.Label(r,s,text);}
        bool Button(Rect r,string label,bool active,string tip,int size=14) {bool hover=r.Contains(Event.current.mousePosition);Panel(r,active?new Color(.2f,.37f,.33f):hover?new Color(.19f,.27f,.29f):new Color(.12f,.19f,.21f));Label(new Rect(r.x,r.y+(r.height-size)*.5f-1,r.width,r.height),label,size,active?Teal:White,true,TextAnchor.UpperCenter);return InvisibleButton(r,tip);}
        bool IconButton(Rect r,int icon,bool active,string tip) {bool hover=r.Contains(Event.current.mousePosition);if(active||hover)Panel(r,active?new Color(.2f,.37f,.33f):new Color(.19f,.27f,.29f));Icon(icon,new Rect(r.x+10,r.y+7,r.width-20,r.height-14),active?Teal:Muted);return InvisibleButton(r,tip);}
        bool InvisibleButton(Rect r,string tip) {if(r.Contains(Event.current.mousePosition))tooltip=tip;bool clicked=GUI.Button(r,GUIContent.none,GUIStyle.none);if(clicked)Game.Audio?.Play(CityAudio.Cue.Ui);return clicked;}
        void Panel(Rect r,Color c) {DrawColoredTexture(r,c,8);}
        void DrawColoredTexture(Rect r,Color c,float radius) {
            // Keep icons, rules and badges on the same explicit color path as the panels.
            // Converting the chosen sRGB colors here keeps contrast consistent in the linear renderer.
            Color old=GUI.color;GUI.color=Color.white;
            GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,c.linear,0,radius);
            GUI.color=old;
        }
        void RectFill(Rect r,Color c) {DrawColoredTexture(r,c,0);}
        void Rule(float x,float y,float w) {RectFill(new Rect(x,y,w,1),new Color(.32f,.43f,.46f,.35f));}
        void Sep(float x,float y,float h) {RectFill(new Rect(x,y,1,h),new Color(.32f,.43f,.46f,.3f));}
        void Dot(Vector2 p,float radius,Color c) {Panel(new Rect(p.x-radius,p.y-radius,radius*2,radius*2),c);}
        void Bar(Rect r,float amount,Color c) {Panel(r,new Color(.18f,.26f,.28f));if(amount>0)Panel(new Rect(r.x,r.y,r.width*Mathf.Clamp01(amount),r.height),c);}
        void Icon(int id,Rect r,Color c) {
            if(!iconTextures.TryGetValue(id,out Texture2D texture)) {
                texture=CreateIconTexture(id);iconTextures.Add(id,texture);
            }
            // Only the root canvas scale transforms icons. Rotating IMGUI quads around
            // a scaled pivot displaced strokes when the player window was resized.
            Color old=GUI.color;GUI.color=Color.white;
            GUI.DrawTexture(r,texture,ScaleMode.StretchToFill,true,0,c.linear,0,0);
            GUI.color=old;
        }
        static Texture2D CreateIconTexture(int id) {
            const int size=96;
            var coverage=new float[size*size];
            Action<float,float,float,float> l=(a,b,d,e)=>RasterIconLine(coverage,size,a,b,d,e);
            Action<float,float,float,float> box=(a,b,d,e)=>{l(a,b,a+d,b);l(a+d,b,a+d,b+e);l(a+d,b+e,a,b+e);l(a,b+e,a,b);};
            Action<float,float,float,float> fill=(a,b,d,e)=>RasterIconBox(coverage,size,a,b,d,e);
            switch(id){
                case 0:fill(0,.45f,.21f,.55f);fill(.31f,0,.24f,1);fill(.65f,.25f,.3f,.75f);break;
                case 1: l(.15f,.02f,.25f,.85f);l(.15f,.02f,.86f,.52f);l(.25f,.85f,.46f,.58f);l(.46f,.58f,.86f,.52f);break;
                case 2: case 24: l(.2f,0,.2f,1);l(.8f,0,.8f,1);l(.5f,0,.5f,.2f);l(.5f,.4f,.5f,.6f);l(.5f,.8f,.5f,1);break;
                case 3: l(.04f,.43f,.5f,.05f);l(.5f,.05f,.96f,.43f);box(.19f,.43f,.62f,.52f);box(.44f,.68f,.18f,.27f);break;
                case 4: box(.12f,.43f,.76f,.5f);l(.04f,.43f,.96f,.43f);l(.04f,.43f,.18f,.12f);l(.18f,.12f,.82f,.12f);l(.82f,.12f,.96f,.43f);l(.32f,.14f,.27f,.43f);l(.65f,.14f,.72f,.43f);box(.28f,.61f,.23f,.32f);break;
                case 5: l(.1f,.9f,.1f,.4f);l(.1f,.4f,.37f,.2f);l(.37f,.2f,.37f,.4f);l(.37f,.4f,.65f,.2f);l(.65f,.2f,.65f,.9f);l(.65f,.9f,.1f,.9f);box(.76f,.04f,.15f,.86f);break;
                case 6: case 25:l(.5f,.15f,.12f,.65f);l(.12f,.65f,.87f,.65f);l(.87f,.65f,.5f,.15f);l(.5f,.65f,.5f,.97f);break;
                case 7: case 22:l(.58f,.04f,.17f,.57f);l(.17f,.57f,.49f,.57f);l(.49f,.57f,.4f,.97f);l(.4f,.97f,.86f,.39f);l(.86f,.39f,.55f,.39f);l(.55f,.39f,.58f,.04f);break;
                case 8: case 23:l(.5f,.03f,.16f,.58f);l(.16f,.58f,.19f,.79f);l(.19f,.79f,.36f,.94f);l(.36f,.94f,.67f,.94f);l(.67f,.94f,.84f,.76f);l(.84f,.76f,.82f,.56f);l(.82f,.56f,.5f,.03f);break;
                case 9:box(.37f,.05f,.26f,.9f);box(.05f,.37f,.9f,.26f);break;
                case 10:box(.15f,.64f,.7f,.25f);l(.25f,.64f,.42f,.23f);l(.42f,.23f,.7f,.23f);l(.7f,.23f,.84f,.57f);l(.15f,.48f,.06f,.91f);break;
                case 11: box(.07f,.33f,.36f,.64f);box(.5f,.04f,.41f,.93f);l(.62f,.23f,.8f,.23f);l(.62f,.44f,.8f,.44f);l(.62f,.65f,.8f,.65f);l(.19f,.53f,.3f,.53f);l(.19f,.75f,.3f,.75f);break;
                case 12: box(.14f,.08f,.68f,.88f);l(.37f,.08f,.37f,.96f);l(.61f,.08f,.61f,.96f);l(.14f,.31f,.82f,.31f);l(.14f,.56f,.82f,.56f);l(.14f,.8f,.82f,.8f);break;
                case 13: l(.07f,.34f,.25f,.14f);l(.25f,.14f,.76f,.14f);l(.76f,.14f,.94f,.34f);l(.94f,.34f,.94f,.74f);l(.94f,.74f,.76f,.94f);l(.76f,.94f,.25f,.94f);l(.25f,.94f,.07f,.74f);l(.07f,.74f,.07f,.34f);box(.27f,.34f,.48f,.4f);l(.51f,.34f,.51f,.74f);l(.17f,0,.17f,.22f);l(.84f,0,.84f,.22f);break;
                case 14:box(.16f,.44f,.68f,.5f);l(.3f,.44f,.3f,.23f);l(.3f,.23f,.4f,.09f);l(.4f,.09f,.6f,.09f);l(.6f,.09f,.7f,.23f);l(.7f,.23f,.7f,.44f);l(.5f,.63f,.5f,.77f);break;
                case 20:l(.5f,.05f,.07f,.48f);l(.07f,.48f,.5f,.9f);l(.5f,.9f,.94f,.48f);l(.94f,.48f,.5f,.05f);break;
                case 21:box(.05f,.05f,.35f,.35f);box(.6f,.05f,.35f,.35f);box(.05f,.6f,.35f,.35f);box(.6f,.6f,.35f,.35f);break;
            }
            var pixels=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++) {
                // Glyph definitions use UI coordinates with y increasing downwards.
                pixels[(size-1-y)*size+x]=new Color32(255,255,255,(byte)Mathf.RoundToInt(coverage[y*size+x]*255f));
            }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false,true) {
                name="Seabright UI icon "+id,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,
                hideFlags=HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);texture.Apply(false,true);
            return texture;
        }
        static void RasterIconLine(float[] coverage,int size,float ax,float ay,float bx,float by) {
            const float margin=4f;
            float extent=size-margin*2-1, radius=3.45f;
            Vector2 a=new Vector2(margin+ax*extent,margin+ay*extent),b=new Vector2(margin+bx*extent,margin+by*extent);
            Vector2 delta=b-a;float lengthSquared=delta.sqrMagnitude;
            int x0=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.x,b.x)-radius-1)),x1=Mathf.Min(size-1,Mathf.CeilToInt(Mathf.Max(a.x,b.x)+radius+1));
            int y0=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.y,b.y)-radius-1)),y1=Mathf.Min(size-1,Mathf.CeilToInt(Mathf.Max(a.y,b.y)+radius+1));
            for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++) {
                Vector2 p=new Vector2(x+.5f,y+.5f);
                float t=lengthSquared>0?Mathf.Clamp01(Vector2.Dot(p-a,delta)/lengthSquared):0;
                float alpha=Mathf.Clamp01((radius+.75f-Vector2.Distance(p,a+delta*t))/1.5f);
                int index=y*size+x;coverage[index]=Mathf.Max(coverage[index],alpha);
            }
        }
        static void RasterIconBox(float[] coverage,int size,float x,float y,float width,float height) {
            const float margin=4f;
            float extent=size-margin*2-1,left=margin+x*extent,top=margin+y*extent,right=left+width*extent,bottom=top+height*extent;
            for(int py=Mathf.Max(0,Mathf.FloorToInt(top-1));py<=Mathf.Min(size-1,Mathf.CeilToInt(bottom+1));py++)
                for(int px=Mathf.Max(0,Mathf.FloorToInt(left-1));px<=Mathf.Min(size-1,Mathf.CeilToInt(right+1));px++) {
                    float edge=Mathf.Min(Mathf.Min(px+.5f-left,right-px-.5f),Mathf.Min(py+.5f-top,bottom-py-.5f));
                    int index=py*size+px;coverage[index]=Mathf.Max(coverage[index],Mathf.Clamp01(edge+.5f));
                }
        }
        void OnDestroy() {
            foreach(var texture in iconTextures.Values)if(texture!=null)Destroy(texture);
            iconTextures.Clear();if(mini!=null)Destroy(mini);
        }
    }
}
