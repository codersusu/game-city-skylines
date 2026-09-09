using UnityEngine;
namespace Seabright
{
    public class CityCamera : MonoBehaviour
    {
        public CityGame Game;
        public Vector3 Focus = new Vector3(5,0,-18);
        public float Distance = 445, Yaw = -28, Pitch = 48;
        Vector3 smoothFocus;
        float smoothDistance, smoothYaw, smoothPitch;
        void Start() { smoothFocus=Focus;smoothDistance=Distance;smoothYaw=Yaw;smoothPitch=Pitch; }
        public void ResetView() {
            bool small=Game!=null&&Game.Sim!=null&&Game.Sim.Population<150;
            Focus=small?CitySimulation.World(12,24):new Vector3(-35,0,5);
            Distance=small?185:360; Yaw=-28; Pitch=48;
        }
        void LateUpdate() {
            float dt = Mathf.Min(Time.unscaledDeltaTime,.06f);
            if (Game != null && !Game.Help && !(Game.HUD && Game.HUD.ModalOpen)) {
                Quaternion yaw = Quaternion.Euler(0,Yaw,0);
                Vector3 move = new Vector3((Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow)?1:0)-(Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow)?1:0),0,(Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow)?1:0)-(Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow)?1:0));
                Focus += yaw*move*Distance*.5f*dt;
                Yaw += ((Input.GetKey(KeyCode.E)?1:0)-(Input.GetKey(KeyCode.Q)?1:0))*55*dt;
                if (!Game.HUD || !Game.HUD.PointerOverUI(Input.mousePosition)) Distance = Mathf.Clamp(Distance-Input.mouseScrollDelta.y*Distance*.075f,45,700);
                if (Input.GetMouseButton(2)) { Yaw+=Input.GetAxis("Mouse X")*3; Pitch = Mathf.Clamp(Pitch-Input.GetAxis("Mouse Y")*2,25,78); }
                Focus.x=Mathf.Clamp(Focus.x,-240,240);Focus.z=Mathf.Clamp(Focus.z,-230,240);
            }
            float t=1-Mathf.Exp(-dt*9);
            smoothFocus=Vector3.Lerp(smoothFocus,Focus,t);smoothDistance=Mathf.Lerp(smoothDistance,Distance,t);smoothYaw=Mathf.Lerp(smoothYaw,Yaw,t);smoothPitch=Mathf.Lerp(smoothPitch,Pitch,t);
            Quaternion r=Quaternion.Euler(smoothPitch,smoothYaw,0);
            transform.SetPositionAndRotation(smoothFocus-r*Vector3.forward*smoothDistance,r);
        }
    }
}
