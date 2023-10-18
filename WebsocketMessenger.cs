using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Titalyver2;
using static MusicBeePlugin.Plugin;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace TitalyverG
{
    internal class WebsocketMessenger
    {
        private const string host = "127.0.0.1";
        private const uint port = 14738;


        private ClientWebSocket webSocket = null;

        public async Task Connect()
        {
            Uri uri = new Uri("ws://" + host + ":" + port + "/");
            await webSocket.ConnectAsync(uri, CancellationToken.None);
        }
        public async void Disconnect()
        {
            if ( webSocket != null && webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
            }
        }


        public enum EnumPlaybackEvent
        {
            Bit_Play = 1,
            Bit_Stop = 2,
            Bit_Seek = 4,

            NULL = 0,
            Play = 1,
            Stop = 2,

            Seek = 4,
            SeekPlay = 5,
            SeekStop = 6,
        };


        public bool IsValid()
        {
            return webSocket != null && webSocket.State == WebSocketState.Open;
        }

        public static int GetTimeOfDay()
        {
            DateTime now = DateTime.UtcNow;
            return ((now.Hour * 60 + now.Minute) * 60 + now.Second) * 1000 + now.Millisecond;
        }

        public bool Initialize()
        {
            Terminalize();
            try
            {
                webSocket = new ClientWebSocket();

            }
            catch (Exception e)
            {
                Terminalize();
                Debug.WriteLine(e.Message);
                return false;
            }
            _ = Connect();
            return true;
        }

        public void Terminalize()
        {
            if (webSocket != null)
            {
                Disconnect();
                webSocket.Dispose();
                webSocket = null;
            }
        }

        public WebsocketMessenger() { }

        ~WebsocketMessenger() { Terminalize(); }

        public async Task Update(WebsocketMessenger.EnumPlaybackEvent pbevent, double seektime,
            string p,string t, string[] ar,string al,double d, Dictionary<string, object> m)
        {
            JsonStruct json = new JsonStruct()
            {
                @event = (int)pbevent,
                seek = seektime,
                time = GetTimeOfDay(),
                path = p,
                title = t,
                artists = ar,
                album = al,
                duration = d,
                meta = m
            };
            using (var ms = new MemoryStream())
            {
                var settings = new DataContractJsonSerializerSettings() { UseSimpleDictionaryFormat = true };
                var serializer = new DataContractJsonSerializer(typeof(JsonStruct), settings);
                serializer.WriteObject(ms, json);
                var segment = new ArraySegment<byte>(ms.ToArray());
                await webSocket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
        [DataContract]
        public class JsonStruct
        {
            [DataMember]
            public int @event;
            [DataMember]
            public double seek;
            [DataMember]
            public int time;

            [DataMember]
            public string path;
            [DataMember]
            public string title;
            [DataMember]
            public string[] artists;
            [DataMember]
            public string album;
            [DataMember]
            public double duration;

            [DataMember]
            public Dictionary<string, object> meta = new Dictionary<string, object>();
        };




        public async Task Update(WebsocketMessenger.EnumPlaybackEvent pbevent, double seektime)
        {
            MinJsonStruct minJson = new MinJsonStruct
            {
                @event = (int)pbevent,
                seek = seektime,
                time = GetTimeOfDay()
            };

            using (var ms = new MemoryStream())
            {
                var settings = new DataContractJsonSerializerSettings() { UseSimpleDictionaryFormat = true };
                var serializer = new DataContractJsonSerializer(typeof(MinJsonStruct), settings);
                serializer.WriteObject(ms, minJson);
                var segment = new ArraySegment<byte>(ms.ToArray());
                await webSocket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
        [DataContract]
        private class MinJsonStruct
        {
            [DataMember]
            public int @event;
            [DataMember]
            public double seek;
            [DataMember]
            public int time;
        }



    };

}
