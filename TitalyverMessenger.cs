using System;
using System.Collections.Generic;
using System.Text;

using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Diagnostics;



namespace Titalyver2
{
    public class Message
    {
        protected const UInt32 MMF_MaxSize = 1024 * 1024 * 64;

        protected const string MMF_Name = "Titalyver Message Data MMF";
        protected const string WriteEvent_Name = "Titalyver Message Write Event";
        protected const string Mutex_Name = "Titalyver Message Mutex";

        protected Mutex Mutex;
        protected EventWaitHandle EventWaitHandle;


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




        public bool IsValid() { return Mutex != null; }

        public static int GetTimeOfDay()
        {
            DateTime now = DateTime.Now;
            return ((now.Hour * 60 + now.Minute) * 60 + now.Second) * 1000 + now.Millisecond;
        }


        protected bool Initialize()
        {
            Terminalize();
            try
            {
                Mutex = new Mutex(false, Mutex_Name);
                EventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, WriteEvent_Name);
            }
            catch (Exception e)
            {
                Terminalize();
                Debug.WriteLine(e.Message);
                return false;
            }
            return true;
        }

        protected void Terminalize()
        {
            EventWaitHandle?.Dispose();
            EventWaitHandle = null;

            Mutex?.Dispose();
            Mutex = null;
        }


        public Message() {}

        ~Message() { Terminalize(); }

        protected class MutexLock : IDisposable
        {
            private readonly Mutex Mutex;
            public bool Result { get; private set; }
            public MutexLock(Mutex mutex, int timeout_millisec)
            {
                Mutex = mutex;
                Result = mutex.WaitOne(timeout_millisec);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (disposing)
                {
                    // TODO: マネージド状態を破棄します (マネージド オブジェクト)
                }
                if (Result)
                {
                    Mutex.ReleaseMutex();
                    Result = false;
                }
            }

            // // TODO: 'Dispose(bool disposing)' にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします
            ~MutexLock()
            {
                // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
                Dispose(disposing: false);
            }

            public void Dispose()
            {
                // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }
        }


    };

    public class Messenger : Message
    {
	    private MemoryMappedFile MemoryMappedFile;
        public new bool Initialize()
        {
            if (base.Initialize())
            {
                try
                {
                    try
                    {
                        using (MemoryMappedFile test = MemoryMappedFile.OpenExisting(MMF_Name))
                        {
                            Terminalize();
                            return false;
                        }
                    }
                    catch (FileNotFoundException) { }
                    MemoryMappedFile = MemoryMappedFile.CreateOrOpen(MMF_Name, MMF_MaxSize, MemoryMappedFileAccess.ReadWrite);
                }
                catch (Exception e)
                {
                    Terminalize();
                    Debug.WriteLine(e.Message);
                    return false;
                }
                return true;
            }
            return false;

        }
        public new void Terminalize()
        {
            MemoryMappedFile?.Dispose();
            MemoryMappedFile = null;
            base.Terminalize();
        }

        public bool Update(EnumPlaybackEvent pbevent, double seektime, byte[] json)
        {
            Int32 timeofday = GetTimeOfDay();
            int size = 4 + 8 + 4 + 4 + 4 + json.Length;

            using (MutexLock ml = new MutexLock(Mutex, 100))
            {
                if (!ml.Result)
                    return false;
                using (MemoryMappedViewAccessor mmva = MemoryMappedFile.CreateViewAccessor(0, size, MemoryMappedFileAccess.ReadWrite))
                {
                    Int64 offset = 0;
                    mmva.Write(offset, (Int32)pbevent); offset += 4;
                    mmva.Write(offset, seektime); offset += 8;
                    mmva.Write(offset, timeofday); offset += 4;
                    mmva.Write(offset, timeofday); offset += 4;
                    mmva.Write(offset, json.Length); offset += 4;
                    mmva.WriteArray(offset, json, 0, json.Length);
                }

                _ = EventWaitHandle.Set();
            }
            return true;
        }
	    public bool Update(EnumPlaybackEvent pbevent, double seektime)
        {
            Int32 timeofday = GetTimeOfDay();
            int size = 4 + 8 + 4;

            using (MutexLock ml = new MutexLock(Mutex, 100))
            {
                if (!ml.Result)
                    return false;
                using (MemoryMappedViewAccessor mmva = MemoryMappedFile.CreateViewAccessor(0, size, MemoryMappedFileAccess.ReadWrite))
                {
                    Int64 offset = 0;
                    mmva.Write(offset, (Int32)pbevent); offset += 4;
                    mmva.Write(offset, seektime); offset += 8;
                    mmva.Write(offset, timeofday);
                }
                _ = EventWaitHandle.Set();
            }
            return true;
        }



        public Messenger() { }
        ~Messenger() { Terminalize(); }

    }
}
