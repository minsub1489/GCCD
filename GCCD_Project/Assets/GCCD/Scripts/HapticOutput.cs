using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace GCCD
{
    public class HapticOutput : MonoBehaviour
    {
        [Tooltip("Off by default. Requires a separate local hardware bridge.")] public bool udpEnabled;
        [Range(1024,65535)] public int port=9050;
        [Range(0,1)] public float pairedGain=0.70f;
        public float[] levels = new float[4];
        public string status="SIMULATION • no hardware output";
        UdpClient client;
        float stopAt;
        public void Pulse(float[] values, float seconds=0.35f)
        {
            Array.Copy(values,levels,4); stopAt=Time.unscaledTime+seconds;
            Send(seconds);
        }
        void Update() { if(stopAt>0 && Time.unscaledTime>=stopAt) Stop(); }
        public void Stop() { Array.Clear(levels,0,4); stopAt=0; Send(0); }
        void Send(float seconds)
        {
            if(!udpEnabled) { status="SIMULATION • no hardware output"; return; }
            try {
                if(client==null) client=new UdpClient();
                var packet=new Packet { durationMs=Mathf.RoundToInt(seconds*1000), channels=(float[])levels.Clone() };
                byte[] data=Encoding.UTF8.GetBytes(JsonUtility.ToJson(packet));
                client.Send(data,data.Length,new IPEndPoint(IPAddress.Loopback,port));
                status="UDP sent • hardware delivery unverified";
            } catch(Exception e) { status="OUTPUT ERROR: "+e.GetType().Name; udpEnabled=false; }
        }
        void OnApplicationFocus(bool focus) { if(!focus) Stop(); }
        void OnDisable() { Stop(); client?.Close(); client=null; }
        [Serializable] class Packet { public int version=1; public int durationMs; public float[] channels; }
    }
}
