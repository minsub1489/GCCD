using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
namespace GCCD.Dts
{
    // Official legacy PlayerRequest protocol. Loopback only; no cloud credentials.
    public sealed class LocalPlayerClient:IDisposable
    {
        [Serializable] class Status {public int ConnectedDeviceCount;public string[] ConnectedPositions;}
        [Serializable] class Dot {public int Index,Intensity;}
        [Serializable] class Frame {public int DurationMillis,Position,Texture;public Dot[] DotPoints;public int[] PathPoints=new int[0];}
        [Serializable] class Request {public string Type,Key;public Frame Frame;}
        [Serializable] class Message {public int[] Register=new int[0];public Request[] Submit;}
        ClientWebSocket socket;CancellationTokenSource lifetime;volatile string latest;bool connecting;Status status;
        readonly string key="gccd_"+Guid.NewGuid().ToString("N");readonly SemaphoreSlim sendLock=new SemaphoreSlim(1,1);
        public bool VestConnected {get {Refresh();return socket!=null&&socket.State==WebSocketState.Open&&status!=null&&status.ConnectedPositions!=null&&Array.IndexOf(status.ConnectedPositions,"Vest")>=0;}}
        public void Refresh(){string s=latest;if(s!=null){latest=null;try{status=JsonUtility.FromJson<Status>(s);}catch(ArgumentException){status=null;}}}
        public void BeginConnect(){if(connecting||(socket!=null&&socket.State==WebSocketState.Open))return;connecting=true;Connect();}
        async void Connect()
        {
            try{if(socket!=null)socket.Dispose();if(lifetime!=null)lifetime.Cancel();lifetime=new CancellationTokenSource();socket=new ClientWebSocket();
                using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token)){timeout.CancelAfter(1500);await socket.ConnectAsync(new Uri("ws://127.0.0.1:15881/v2/feedbacks"),timeout.Token).ConfigureAwait(false);}
                Receive(socket,lifetime.Token);
            }catch(Exception){status=null;}finally{connecting=false;}
        }
        async void Receive(ClientWebSocket client,CancellationToken token)
        {
            var bytes=new byte[65536];try{while(client.State==WebSocketState.Open&&!token.IsCancellationRequested){int offset=0;WebSocketReceiveResult r;do{r=await client.ReceiveAsync(new ArraySegment<byte>(bytes,offset,bytes.Length-offset),token).ConfigureAwait(false);if(r.MessageType==WebSocketMessageType.Close)return;offset+=r.Count;if(offset>=bytes.Length&&!r.EndOfMessage)throw new InvalidOperationException("Oversized Player status.");}while(!r.EndOfMessage);latest=Encoding.UTF8.GetString(bytes,0,offset);}}catch(Exception){try{client.Abort();}catch(ObjectDisposedException){}}
        }
        public void Submit(int[] values,int duration)
        {
            if(!VestConnected)throw new InvalidOperationException("Player에 연결된 조끼가 없습니다.");if(values==null||values.Length!=40)throw new ArgumentException("40 channels required.");
            var requests=new Request[2];for(int side=0;side<2;side++){var dots=new Dot[20];for(int i=0;i<20;i++){int v=values[side*20+i];if(v<0||v>100)throw new ArgumentException("Motor range.");dots[i]=new Dot{Index=i,Intensity=v};}requests[side]=new Request{Type="frame",Key=key+side,Frame=new Frame{DurationMillis=duration,Position=201+side,DotPoints=dots}};}
            Send(JsonUtility.ToJson(new Message{Submit=requests})).GetAwaiter().GetResult();
        }
        async Task Send(string message){using(var timeout=new CancellationTokenSource(700)){await sendLock.WaitAsync(timeout.Token).ConfigureAwait(false);try{byte[] bytes=Encoding.UTF8.GetBytes(message);await socket.SendAsync(new ArraySegment<byte>(bytes),WebSocketMessageType.Text,true,timeout.Token).ConfigureAwait(false);}finally{sendLock.Release();}}}
        public void Stop(){if(socket==null||socket.State!=WebSocketState.Open)return;Send(JsonUtility.ToJson(new Message{Submit=new[]{new Request{Type="turnOff",Key=key+"0"},new Request{Type="turnOff",Key=key+"1"}}})).GetAwaiter().GetResult();}
        public void Dispose(){try{Stop();}catch(Exception){}if(lifetime!=null)lifetime.Cancel();if(socket!=null)socket.Dispose();}
    }
}
